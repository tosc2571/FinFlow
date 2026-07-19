using FinFlow.Api.Data;
using FinFlow.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Api.Services;

public record DashboardSummary(decimal Income, decimal Expenses, decimal Net, int TransactionCount, int NeedsReviewCount, int IgnoredCount);
public record CategoryBreakdown(int? CategoryId, string CategoryName, int Count, decimal Total);
public record MonthlyTrend(int Year, int Month, decimal Income, decimal Expenses, decimal Net);

/// <summary>
/// Dashboard aggregates. Filtering happens DB-side, the money sums in memory:
/// SQLite can't aggregate decimal columns server-side, and exact decimal sums
/// matter more than pushing them into SQL at personal-finance data volumes.
/// Ignored transactions are excluded from all sums (same as the XLSX export).
/// </summary>
public class DashboardService(AppDbContext db)
{
    public DashboardSummary GetSummary(TransactionFilterParams p)
    {
        var rows = TransactionFilters.Apply(db.Transactions.AsNoTracking(), p)
            .Select(t => new { t.Amount, t.ClassificationStatus })
            .ToList();
        var counted = rows.Where(r => r.ClassificationStatus != ClassificationStatus.Ignored).ToList();
        decimal income = counted.Where(r => r.Amount > 0).Sum(r => r.Amount);
        decimal expenses = counted.Where(r => r.Amount < 0).Sum(r => r.Amount);
        return new DashboardSummary(
            income,
            expenses,
            income + expenses,
            counted.Count,
            rows.Count(r => r.ClassificationStatus == ClassificationStatus.NeedsReview),
            rows.Count(r => r.ClassificationStatus == ClassificationStatus.Ignored));
    }

    public List<CategoryBreakdown> GetByCategory(TransactionFilterParams p)
    {
        var rows = TransactionFilters.Apply(db.Transactions.AsNoTracking(), p)
            .Where(t => t.ClassificationStatus != ClassificationStatus.Ignored)
            .Select(t => new { t.CategoryId, CategoryName = t.Category != null ? t.Category.Name : null, t.Amount })
            .ToList();
        return [.. rows
            .GroupBy(r => (r.CategoryId, r.CategoryName))
            .Select(g => new CategoryBreakdown(
                g.Key.CategoryId,
                g.Key.CategoryName ?? "Sonstiges", // same fallback label as the CLI classifier
                g.Count(),
                g.Sum(r => r.Amount)))
            .OrderBy(b => b.CategoryName)];
    }

    public List<MonthlyTrend> GetTrend(TransactionFilterParams p)
    {
        var rows = TransactionFilters.Apply(db.Transactions.AsNoTracking(), p)
            .Where(t => t.ClassificationStatus != ClassificationStatus.Ignored && t.BookingDate != null)
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
