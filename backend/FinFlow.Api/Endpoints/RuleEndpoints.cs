using System.Text.RegularExpressions;
using FinFlow.Api.Data;
using FinFlow.Api.Entities;
using FinFlow.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Api.Endpoints;

public static class RuleEndpoints
{
    public record RuleRequest(string Pattern, int? CategoryId, ClassificationRuleStatus Status, int Priority, bool IsActive = true);
    public record RuleDto(int Id, string Pattern, int? CategoryId, string? CategoryName, ClassificationRuleStatus Status, int Priority, bool IsActive);
    public record TestPatternRequest(string Pattern);

    public static void MapRuleEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/rules");

        group.MapGet("/", (AppDbContext db) =>
            db.ClassificationRules.AsNoTracking()
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .Select(r => new RuleDto(r.Id, r.Pattern, r.CategoryId, r.Category != null ? r.Category.Name : null, r.Status, r.Priority, r.IsActive))
                .ToList());

        group.MapGet("/{id:int}", (int id, AppDbContext db) =>
            db.ClassificationRules.AsNoTracking().Where(r => r.Id == id)
                .Select(r => new RuleDto(r.Id, r.Pattern, r.CategoryId, r.Category != null ? r.Category.Name : null, r.Status, r.Priority, r.IsActive))
                .FirstOrDefault() is { } dto
                ? Results.Ok(dto)
                : Results.NotFound());

        group.MapPost("/", async (RuleRequest req, AppDbContext db) =>
        {
            if (Validate(req) is { } error) return error;
            Category? category = null;
            if (req.CategoryId is { } categoryId)
            {
                category = await db.Categories.FindAsync(categoryId);
                if (category is null) return Results.BadRequest(new { error = $"Unknown category {categoryId}." });
            }

            ClassificationRule rule = new()
            {
                Pattern = req.Pattern,
                CategoryId = req.CategoryId,
                Status = req.Status,
                Priority = req.Priority,
                IsActive = req.IsActive,
            };
            db.ClassificationRules.Add(rule);
            await db.SaveChangesAsync();
            return Results.Created($"/api/rules/{rule.Id}",
                new RuleDto(rule.Id, rule.Pattern, rule.CategoryId, category?.Name, rule.Status, rule.Priority, rule.IsActive));
        });

        group.MapPut("/{id:int}", async (int id, RuleRequest req, AppDbContext db) =>
        {
            ClassificationRule? rule = await db.ClassificationRules.FindAsync(id);
            if (rule is null) return Results.NotFound();
            if (Validate(req) is { } error) return error;
            Category? category = null;
            if (req.CategoryId is { } categoryId)
            {
                category = await db.Categories.FindAsync(categoryId);
                if (category is null) return Results.BadRequest(new { error = $"Unknown category {categoryId}." });
            }

            rule.Pattern = req.Pattern;
            rule.CategoryId = req.CategoryId;
            rule.Status = req.Status;
            rule.Priority = req.Priority;
            rule.IsActive = req.IsActive;
            rule.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new RuleDto(rule.Id, rule.Pattern, rule.CategoryId, category?.Name, rule.Status, rule.Priority, rule.IsActive));
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            ClassificationRule? rule = await db.ClassificationRules.FindAsync(id);
            if (rule is null) return Results.NotFound();
            db.ClassificationRules.Remove(rule);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Dry-run a pattern against the existing transactions before saving it as a rule.
        group.MapPost("/test", (TestPatternRequest req, ClassificationService svc) =>
        {
            try
            {
                return Results.Ok(svc.TestPattern(req.Pattern));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = $"Invalid regex: {ex.Message}" });
            }
        });

        // Re-run classification over everything except manual overrides.
        group.MapPost("/reclassify", (ClassificationService svc) => Results.Ok(svc.ReclassifyAll()));
    }

    internal static IResult? Validate(RuleRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Pattern))
            return Results.BadRequest(new { error = "Pattern must not be empty." });
        try
        {
            _ = new Regex(req.Pattern);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = $"Invalid regex: {ex.Message}" });
        }

        // An internal-transfer rule needs no category (mirrors the IBAN-based detection, which
        // already forces the transaction's CategoryId to null) — every other status still does.
        if (req.Status == ClassificationRuleStatus.InternalTransfer)
        {
            if (req.CategoryId is not null)
                return Results.BadRequest(new { error = "Internal transfer rules must not have a category." });
        }
        else if (req.CategoryId is null)
        {
            return Results.BadRequest(new { error = "Category is required unless the status is Internal transfer." });
        }
        return null;
    }
}
