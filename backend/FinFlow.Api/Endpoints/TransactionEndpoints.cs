using FinFlow.Api.Data;
using FinFlow.Api.Entities;
using FinFlow.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Api.Endpoints;

public static class TransactionEndpoints
{
    public record TransactionDto(
        int Id, string SourceBank, DateOnly? BookingDate, DateOnly? ValueDate, decimal Amount, string Currency,
        string? CounterpartyName, string? CounterpartyIban, string? CounterpartyBic, string? Purpose,
        string? BookingType, decimal? Balance, int? CategoryId, string? CategoryName,
        ClassificationStatus ClassificationStatus, int ImportBatchId, int? ContractId,
        // Not persisted anywhere — derived on read by re-running the current rule set against this
        // transaction, so it always reflects "what would classify this today", not history. Only
        // set for Auto/Ignored (rule-driven); ManualOverride/InternalTransfer were set by hand.
        int? MatchedRuleId = null, string? MatchedRulePattern = null);

    // categoryId null clears the category; without an explicit status, a set category
    // becomes ManualOverride (survives reclassification), a cleared one NeedsReview.
    public record UpdateTransactionRequest(int? CategoryId, ClassificationStatus? Status);
    public record BulkCategorizeRequest(List<int> TransactionIds, int? CategoryId);

    public static void MapTransactionEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/transactions");

        group.MapGet("/", (AppDbContext db, ClassificationService svc, [AsParameters] TransactionFilterParams filter,
            string? sort, int page = 1, int pageSize = 50) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 500);
            IQueryable<Transaction> q = TransactionFilters.Apply(db.Transactions.AsNoTracking(), filter);
            int total = q.Count();
            RuleSet rules = svc.LoadRuleSet();
            List<TransactionDto> items = [.. TransactionFilters.ApplySort(q, sort)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(ToDto)
                .AsEnumerable()
                .Select(dto => WithMatchedRule(dto, rules))];
            return Results.Ok(new { total, page, pageSize, items });
        });

        group.MapGet("/{id:int}", (int id, AppDbContext db, ClassificationService svc) =>
            db.Transactions.AsNoTracking().Where(t => t.Id == id).Select(ToDto).FirstOrDefault() is { } dto
                ? Results.Ok(WithMatchedRule(dto, svc.LoadRuleSet()))
                : Results.NotFound());

        group.MapPatch("/{id:int}", async (int id, UpdateTransactionRequest req, AppDbContext db) =>
        {
            Transaction? t = await db.Transactions.FindAsync(id);
            if (t is null) return Results.NotFound();
            if (req.CategoryId is { } categoryId && !await db.Categories.AnyAsync(c => c.Id == categoryId))
                return Results.BadRequest(new { error = $"Unknown category {categoryId}." });

            t.CategoryId = req.CategoryId;
            t.ClassificationStatus = req.Status
                ?? (req.CategoryId is null ? ClassificationStatus.NeedsReview : ClassificationStatus.ManualOverride);
            t.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { t.Id, t.CategoryId, t.ClassificationStatus });
        });

        group.MapPost("/bulk-categorize", async (BulkCategorizeRequest req, AppDbContext db) =>
        {
            if (req.CategoryId is { } categoryId && !await db.Categories.AnyAsync(c => c.Id == categoryId))
                return Results.BadRequest(new { error = $"Unknown category {categoryId}." });

            List<Transaction> transactions = await db.Transactions
                .Where(t => req.TransactionIds.Contains(t.Id))
                .ToListAsync();
            foreach (Transaction t in transactions)
            {
                t.CategoryId = req.CategoryId;
                t.ClassificationStatus = req.CategoryId is null
                    ? ClassificationStatus.NeedsReview
                    : ClassificationStatus.ManualOverride;
                t.UpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { updated = transactions.Count });
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            Transaction? t = await db.Transactions.FindAsync(id);
            if (t is null) return Results.NotFound();
            db.Transactions.Remove(t);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static readonly System.Linq.Expressions.Expression<Func<Transaction, TransactionDto>> ToDto =
        t => new TransactionDto(
            t.Id, t.SourceBank, t.BookingDate, t.ValueDate, t.Amount, t.Currency,
            t.CounterpartyName, t.CounterpartyIban, t.CounterpartyBic, t.Purpose,
            t.BookingType, t.Balance, t.CategoryId, t.Category != null ? t.Category.Name : null,
            t.ClassificationStatus, t.ImportBatchId, t.ContractId, null, null);

    // Regex matching can't be translated to SQL, so this runs client-side (in .NET, over the
    // already-paged rows) after ToDto's EF projection, not as part of it.
    internal static TransactionDto WithMatchedRule(TransactionDto dto, RuleSet rules)
    {
        if (dto.ClassificationStatus is not (ClassificationStatus.Auto or ClassificationStatus.Ignored))
            return dto;
        RuleSet.Match? match = rules.MatchFor(dto.CounterpartyName, dto.Purpose);
        return match is null ? dto : dto with { MatchedRuleId = match.RuleId, MatchedRulePattern = match.Pattern };
    }
}
