using FinFlow.Api.Data;
using FinFlow.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Api.Services;

public record DashboardSummary(decimal Income, decimal Expenses, decimal Net, int TransactionCount, int NeedsReviewCount, int IgnoredCount, int InternalTransferCount);
public record CategoryBreakdown(int? CategoryId, string CategoryName, int Count, decimal Total);
public record MonthlyTrend(int Year, int Month, decimal Income, decimal Expenses, decimal Net);

/// <summary>
/// Dashboard aggregates. Filtering happens DB-side, the money sums in memory:
/// SQLite can't aggregate decimal columns server-side, and exact decimal sums
/// matter more than pushing them into SQL at personal-finance data volumes.
/// Ignored and InternalTransfer transactions are excluded from all sums (same as the XLSX
/// export) — neither represents real income or spending.
/// </summary>
public class DashboardService(AppDbContext db)
{
    private static bool ExcludedFromSums(ClassificationStatus status) =>
        status is ClassificationStatus.Ignored or ClassificationStatus.InternalTransfer;

    public DashboardSummary GetSummary(TransactionFilterParams p)
    {
        var rows = TransactionFilters.Apply(db.Transactions.AsNoTracking(), p)
            .Select(t => new { t.Amount, t.ClassificationStatus })
            .ToList();
        var counted = rows.Where(r => !ExcludedFromSums(r.ClassificationStatus)).ToList();
        decimal income = counted.Where(r => r.Amount > 0).Sum(r => r.Amount);
        decimal expenses = counted.Where(r => r.Amount < 0).Sum(r => r.Amount);
        return new DashboardSummary(
            income,
            expenses,
            income + expenses,
            counted.Count,
            rows.Count(r => r.ClassificationStatus == ClassificationStatus.NeedsReview),
            rows.Count(r => r.ClassificationStatus == ClassificationStatus.Ignored),
            rows.Count(r => r.ClassificationStatus == ClassificationStatus.InternalTransfer));
    }

    public List<CategoryBreakdown> GetByCategory(TransactionFilterParams p)
    {
        // Inlined rather than calling ExcludedFromSums: this Where still runs as part of the
        // IQueryable, before ToList(), so it must be SQL-translatable — an arbitrary C# method
        // call isn't (EF Core 8 throws instead of silently falling back to client evaluation).
        var rows = TransactionFilters.Apply(db.Transactions.AsNoTracking(), p)
            .Where(t => t.ClassificationStatus != ClassificationStatus.Ignored
                     && t.ClassificationStatus != ClassificationStatus.InternalTransfer)
            .Select(t => new { t.CategoryId, CategoryName = t.Category != null ? t.Category.Name : null, t.Amount })
            .ToList();
        return [.. rows
            .GroupBy(r => (r.CategoryId, r.CategoryName))
            .Select(g => new CategoryBreakdown(
                g.Key.CategoryId,
                g.Key.CategoryName ?? "Other", // same fallback label as the export pipeline
                g.Count(),
                g.Sum(r => r.Amount)))
            .OrderBy(b => b.CategoryName)];
    }

    public List<MonthlyTrend> GetTrend(TransactionFilterParams p)
    {
        var rows = TransactionFilters.Apply(db.Transactions.AsNoTracking(), p)
            .Where(t => t.ClassificationStatus != ClassificationStatus.Ignored
                     && t.ClassificationStatus != ClassificationStatus.InternalTransfer
                     && t.BookingDate != null)
            .Select(t => new { t.BookingDate, t.Amount })
            .ToList();
        return [.. rows
            .GroupBy(r => (r.BookingDate!.Value.Year, r.BookingDate.Value.Month))
            .Select(g => new MonthlyTrend(
                g.Key.Year,
                g.Key.Month,
                g.Where(r => r.Amount > 0).Sum(r => r.Amount),
                g.Where(r => r.Amount < 0).Sum(r => r.Amount),
                g.Sum(r => r.Amount)))
            .OrderBy(m => m.Year).ThenBy(m => m.Month)];
    }
}
