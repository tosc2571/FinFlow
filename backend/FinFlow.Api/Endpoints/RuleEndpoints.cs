using System.Text.RegularExpressions;
using FinFlow.Api.Data;
using FinFlow.Api.Entities;
using FinFlow.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Api.Endpoints;

public static class RuleEndpoints
{
    public record RuleRequest(string Pattern, int CategoryId, ClassificationRuleStatus Status, int Priority, bool IsActive = true);
    public record RuleDto(int Id, string Pattern, int CategoryId, string CategoryName, ClassificationRuleStatus Status, int Priority, bool IsActive);
    public record TestPatternRequest(string Pattern);

    public static void MapRuleEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/rules");

        group.MapGet("/", (AppDbContext db) =>
            db.ClassificationRules.AsNoTracking()
                .OrderBy(r => r.Priority).ThenBy(r => r.Id)
                .Select(r => new RuleDto(r.Id, r.Pattern, r.CategoryId, r.Category!.Name, r.Status, r.Priority, r.IsActive))
                .ToList());

        group.MapPost("/", async (RuleRequest req, AppDbContext db) =>
        {
            if (Validate(req) is { } error) return error;
            if (!await db.Categories.AnyAsync(c => c.Id == req.CategoryId))
                return Results.BadRequest(new { error = $"Unknown category {req.CategoryId}." });

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
            return Results.Created($"/api/rules/{rule.Id}", new { rule.Id });
        });

        group.MapPut("/{id:int}", async (int id, RuleRequest req, AppDbContext db) =>
        {
            ClassificationRule? rule = await db.ClassificationRules.FindAsync(id);
            if (rule is null) return Results.NotFound();
            if (Validate(req) is { } error) return error;
            if (!await db.Categories.AnyAsync(c => c.Id == req.CategoryId))
                return Results.BadRequest(new { error = $"Unknown category {req.CategoryId}." });

            rule.Pattern = req.Pattern;
            rule.CategoryId = req.CategoryId;
            rule.Status = req.Status;
            rule.Priority = req.Priority;
            rule.IsActive = req.IsActive;
            rule.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { rule.Id });
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

    private static IResult? Validate(RuleRequest req)
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
        return null;
    }
}
