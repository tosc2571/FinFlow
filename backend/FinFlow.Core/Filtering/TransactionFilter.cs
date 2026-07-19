namespace FinFlow.Filtering;

/// <summary>All filter criteria in one place. Null = "don't care".</summary>
public sealed class FilterCriteria
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string? Contains { get; set; }     // match in purpose OR counterparty
    public string? Counterparty { get; set; } // match in counterparty name
    public string? Bank { get; set; }         // only bookings from this bank
}

public static class TransactionFilter
{
    public static IEnumerable<LegacyTransaction> Apply(IEnumerable<LegacyTransaction> items, FilterCriteria c)
    {
        IEnumerable<LegacyTransaction> q = items;

        if (c.From is { } from) q = q.Where(t => t.BookingDate is { } d && d >= from);
        if (c.To is { } to) q = q.Where(t => t.BookingDate is { } d && d <= to);
        if (c.MinAmount is { } min) q = q.Where(t => t.Amount >= min);
        if (c.MaxAmount is { } max) q = q.Where(t => t.Amount <= max);
        if (!string.IsNullOrWhiteSpace(c.Bank))
            q = q.Where(t => t.SourceBank.Equals(c.Bank, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(c.Counterparty))
            q = q.Where(t => Has(t.CounterpartyName, c.Counterparty!));
        if (!string.IsNullOrWhiteSpace(c.Contains))
            q = q.Where(t => Has(t.Purpose, c.Contains!) || Has(t.CounterpartyName, c.Contains!));

        return q;
    }

    private static bool Has(string? haystack, string needle) =>
        haystack is not null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
