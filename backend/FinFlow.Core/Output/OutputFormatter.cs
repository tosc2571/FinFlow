using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FinFlow.Output;

/// <summary>Turns the filtered bookings into an output form: table, CSV, or JSON.</summary>
public static class OutputFormatter
{
    public static string Format(IReadOnlyList<LegacyTransaction> items, string format) => format.ToLowerInvariant() switch
    {
        "json" => Json(items),
        "csv"  => Csv(items),
        _      => Table(items),
    };

    private static string Json(IReadOnlyList<LegacyTransaction> items) =>
        JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });

    private static string Csv(IReadOnlyList<LegacyTransaction> items)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("SourceBank;BookingDate;ValueDate;Amount;Currency;CounterpartyName;IBAN;BIC;BookingType;Purpose");
        foreach (LegacyTransaction t in items)
            sb.AppendLine(string.Join(';', new[]
            {
                t.SourceBank,
                t.BookingDate?.ToString("yyyy-MM-dd") ?? "",
                t.ValueDate?.ToString("yyyy-MM-dd") ?? "",
                t.Amount.ToString(CultureInfo.InvariantCulture),
                t.Currency,
                Clean(t.CounterpartyName),
                Clean(t.CounterpartyIban),
                Clean(t.CounterpartyBic),
                Clean(t.BookingType),
                Clean(t.Purpose),
            }));
        return sb.ToString();
    }

    private static string Table(IReadOnlyList<LegacyTransaction> items)
    {
        if (items.Count == 0) return "No matches.";
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"{"Date",-10}  {"Amount",12}  {"Bank",-10}  {"Counterparty",-28}  Purpose");
        sb.AppendLine(new string('-', 100));
        foreach (LegacyTransaction t in items)
            sb.AppendLine(
                $"{t.BookingDate?.ToString("dd.MM.yyyy"),-10}  " +
                $"{t.Amount,12:N2}  " +
                $"{t.SourceBank,-10}  " +
                $"{Trunc(t.CounterpartyName, 28),-28}  " +
                $"{Trunc(t.Purpose, 40)}");
        sb.AppendLine(new string('-', 100));
        sb.AppendLine($"{items.Count} booking(s), total: {items.Sum(t => t.Amount):N2}");
        return sb.ToString();
    }

    private static string Clean(string? s) =>
        (s ?? "").Replace(';', ',').Replace('\n', ' ').Replace('\r', ' ');

    private static string Trunc(string? s, int max)
    {
        s = (s ?? "").Replace('\n', ' ').Replace('\r', ' ');
        return s.Length <= max ? s : s[..(max - 1)] + "…";
    }
}
