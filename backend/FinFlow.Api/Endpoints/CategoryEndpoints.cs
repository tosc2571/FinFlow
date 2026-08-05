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
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "Name must not be empty." });
            if (req.ParentCategoryId is { } parentId && !await db.Categories.AnyAsync(c => c.Id == parentId))
                return Results.BadRequest(new { error = $"Unknown parent category {parentId}." });
            if (await IsDuplicateName(db, req.Name, req.ParentCategoryId, excludeId: null))
                return Results.Conflict(new { error = $"A category named \"{req.Name.Trim()}\" already exists at this level." });

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
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "Name must not be empty." });
            if (req.ParentCategoryId is { } parentId)
            {
                if (!await db.Categories.AnyAsync(c => c.Id == parentId))
                    return Results.BadRequest(new { error = $"Unknown parent category {parentId}." });
                if (await WouldCreateCycle(db, id, parentId))
                    return Results.BadRequest(new { error = "Parent assignment would create a cycle." });
            }
            if (await IsDuplicateName(db, req.Name, req.ParentCategoryId, excludeId: id))
                return Results.Conflict(new { error = $"A category named \"{req.Name.Trim()}\" already exists at this level." });

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
            if (await db.Categories.AnyAsync(c => c.ParentCategoryId == id))
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

    // Siblings must be unique (case-insensitive); the same name under a different parent is fine —
    // matches normal tree/folder semantics rather than a single global namespace.
    internal static async Task<bool> IsDuplicateName(AppDbContext db, string name, int? parentCategoryId, int? excludeId)
    {
        string trimmed = name.Trim().ToLower();
        return await db.Categories.AnyAsync(c =>
            c.ParentCategoryId == parentCategoryId && c.Id != excludeId && c.Name.ToLower() == trimmed);
    }

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
