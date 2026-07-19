using FinFlow.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Api.Services;

/// <summary>
/// Query-side successor of the CLI's FilterCriteria: same semantics (Contains matches
/// purpose OR counterparty, --year expands to a full-year range), but applied as an
/// IQueryable so filtering and paging happen in SQLite.
/// </summary>
public record TransactionFilterParams(
    DateOnly? From = null,
    DateOnly? To = null,
    int? Year = null,
    decimal? Min = null,
    decimal? Max = null,
    string? Contains = null,
    string? Counterparty = null,
    string? Bank = null,
    int? CategoryId = null,
    ClassificationStatus? Status = null)
{
    public DateOnly? EffectiveFrom => From ?? (Year is { } y ? new DateOnly(y, 1, 1) : null);
    public DateOnly? EffectiveTo => To ?? (Year is { } y ? new DateOnly(y, 12, 31) : null);
}

public static class TransactionFilters
{
    public static IQueryable<Transaction> Apply(IQueryable<Transaction> q, TransactionFilterParams p)
    {
        if (p.EffectiveFrom is { } from) q = q.Where(t => t.BookingDate != null && t.BookingDate >= from);
        if (p.EffectiveTo is { } to) q = q.Where(t => t.BookingDate != null && t.BookingDate <= to);
        if (p.Min is { } min) q = q.Where(t => t.Amount >= min);
        if (p.Max is { } max) q = q.Where(t => t.Amount <= max);
        if (!string.IsNullOrWhiteSpace(p.Bank))
        {
            string bank = p.Bank.ToLower();
            q = q.Where(t => t.SourceBank.ToLower() == bank);
        }
        if (p.CategoryId is { } categoryId) q = q.Where(t => t.CategoryId == categoryId);
        if (p.Status is { } status) q = q.Where(t => t.ClassificationStatus == status);
        if (!string.IsNullOrWhiteSpace(p.Counterparty))
        {
            string pattern = $"%{p.Counterparty}%";
            q = q.Where(t => t.CounterpartyName != null && EF.Functions.Like(t.CounterpartyName, pattern));
        }
        if (!string.IsNullOrWhiteSpace(p.Contains))
        {
            string pattern = $"%{p.Contains}%";
            q = q.Where(t =>
                (t.Purpose != null && EF.Functions.Like(t.Purpose, pattern)) ||
                (t.CounterpartyName != null && EF.Functions.Like(t.CounterpartyName, pattern)));
        }
        return q;
    }

    // Amount is a decimal (stored as TEXT in SQLite, which can't ORDER BY it) —
    // the cast to double makes SQLite sort numerically via CAST(... AS REAL).
    public static IQueryable<Transaction> ApplySort(IQueryable<Transaction> q, string? sort) => sort switch
    {
        "amount"      => q.OrderBy(t => (double)t.Amount).ThenBy(t => t.Id),
        "amount-desc" => q.OrderByDescending(t => (double)t.Amount).ThenBy(t => t.Id),
        "date-desc"   => q.OrderByDescending(t => t.BookingDate).ThenByDescending(t => t.Id),
        _             => q.OrderBy(t => t.BookingDate).ThenBy(t => t.Id),
    };
}
