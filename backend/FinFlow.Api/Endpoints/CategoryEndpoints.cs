using FinFlow.Api.Data;
using FinFlow.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Api.Endpoints;

public static class CategoryEndpoints
{
    public record CategoryRequest(string Name, int? ParentCategoryId, bool IsIncome, int SortOrder);
    public record CategoryDto(int Id, string Name, int? ParentCategoryId, bool IsIncome, int SortOrder);
    public record CategoryTreeNode(int Id, string Name, bool IsIncome, int SortOrder, List<CategoryTreeNode> Children);

    public static void MapCategoryEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/categories");

        group.MapGet("/", (AppDbContext db) =>
            db.Categories.AsNoTracking()
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
                .Select(c => new CategoryDto(c.Id, c.Name, c.ParentCategoryId, c.IsIncome, c.SortOrder))
                .ToList());

        group.MapGet("/tree", (AppDbContext db) =>
        {
            List<Category> all = db.Categories.AsNoTracking().ToList();
            return BuildTree(all, null);
        });

        group.MapPost("/", async (CategoryRequest req, AppDbContext db) =>
        {
            CategoryValidationError? error = await ValidateCategory(db, existing: null, req.Name, req.ParentCategoryId);
            if (error is not null) return error.ToResult();

            Category category = new()
            {
                Name = req.Name.Trim(),
                ParentCategoryId = req.ParentCategoryId,
                IsIncome = req.IsIncome,
                SortOrder = req.SortOrder,
            };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            return Results.Created($"/api/categories/{category.Id}",
                new CategoryDto(category.Id, category.Name, category.ParentCategoryId, category.IsIncome, category.SortOrder));
        });

        group.MapPut("/{id:int}", async (int id, CategoryRequest req, AppDbContext db) =>
        {
            Category? category = await db.Categories.FindAsync(id);
            if (category is null) return Results.NotFound();
            CategoryValidationError? error = await ValidateCategory(db, existing: category, req.Name, req.ParentCategoryId);
            if (error is not null) return error.ToResult();

            category.Name = req.Name.Trim();
            category.ParentCategoryId = req.ParentCategoryId;
            category.IsIncome = req.IsIncome;
            category.SortOrder = req.SortOrder;
            await db.SaveChangesAsync();
            return Results.Ok(new CategoryDto(category.Id, category.Name, category.ParentCategoryId, category.IsIncome, category.SortOrder));
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            Category? category = await db.Categories.FindAsync(id);
            if (category is null) return Results.NotFound();
            if (await HasChildren(db, id))
                return Results.Conflict(new { error = "Category has child categories — delete or reassign them first." });
            if (await db.ClassificationRules.AnyAsync(r => r.CategoryId == id))
                return Results.Conflict(new { error = "Category is used by classification rules — delete or reassign them first." });

            // Transactions fall back to uncategorized/needs-review instead of blocking the delete.
            List<Transaction> affected = await db.Transactions.Where(t => t.CategoryId == id).ToListAsync();
            foreach (Transaction t in affected)
            {
                t.CategoryId = null;
                t.ClassificationStatus = ClassificationStatus.NeedsReview;
                t.UpdatedAt = DateTime.UtcNow;
            }
            db.Categories.Remove(category);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static List<CategoryTreeNode> BuildTree(List<Category> all, int? parentId) =>
        [.. all.Where(c => c.ParentCategoryId == parentId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryTreeNode(c.Id, c.Name, c.IsIncome, c.SortOrder, BuildTree(all, c.Id)))];

    internal record CategoryValidationError(int StatusCode, string Message)
    {
        public IResult ToResult() =>
            StatusCode == StatusCodes.Status409Conflict ? Results.Conflict(new { error = Message }) : Results.BadRequest(new { error = Message });
    }

    /// <summary>
    /// Shared by POST and PUT. `existing` is null for a create (skips the cycle/has-children
    /// checks, which only make sense once a category already exists) and the category being
    /// updated for a PUT. Hierarchy is capped at exactly two levels: a category whose chosen
    /// parent is itself a sub-category is rejected, and a category that already has children of
    /// its own can't be given a parent (both directions of the same invariant).
    /// </summary>
    internal static async Task<CategoryValidationError?> ValidateCategory(
        AppDbContext db, Category? existing, string name, int? parentCategoryId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new(StatusCodes.Status400BadRequest, "Name must not be empty.");

        if (parentCategoryId is { } parentId)
        {
            Category? parent = await db.Categories.FindAsync(parentId);
            if (parent is null)
                return new(StatusCodes.Status400BadRequest, $"Unknown parent category {parentId}.");
            if (parent.ParentCategoryId is not null)
                return new(StatusCodes.Status400BadRequest, $"Cannot nest more than two levels — \"{parent.Name}\" is itself a sub-category.");

            if (existing is not null)
            {
                if (await WouldCreateCycle(db, existing.Id, parentId))
                    return new(StatusCodes.Status400BadRequest, "Parent assignment would create a cycle.");
                if (await HasChildren(db, existing.Id))
                    return new(StatusCodes.Status400BadRequest, $"Cannot make \"{existing.Name}\" a sub-category — it already has sub-categories of its own.");
            }
        }

        if (await IsDuplicateName(db, name, parentCategoryId, excludeId: existing?.Id))
            return new(StatusCodes.Status409Conflict, $"A category named \"{name.Trim()}\" already exists at this level.");

        return null;
    }

    // Siblings must be unique (case-insensitive); the same name under a different parent is fine —
    // matches normal tree/folder semantics rather than a single global namespace.
    internal static async Task<bool> IsDuplicateName(AppDbContext db, string name, int? parentCategoryId, int? excludeId)
    {
        string trimmed = name.Trim().ToLower();
        return await db.Categories.AnyAsync(c =>
            c.ParentCategoryId == parentCategoryId && c.Id != excludeId && c.Name.ToLower() == trimmed);
    }

    internal static async Task<bool> HasChildren(AppDbContext db, int categoryId) =>
        await db.Categories.AnyAsync(c => c.ParentCategoryId == categoryId);

    private static async Task<bool> WouldCreateCycle(AppDbContext db, int categoryId, int newParentId)
    {
        int? current = newParentId;
        while (current is { } c)
        {
            if (c == categoryId) return true;
            current = await db.Categories.Where(x => x.Id == c).Select(x => x.ParentCategoryId).FirstOrDefaultAsync();
        }
        return false;
    }
}
