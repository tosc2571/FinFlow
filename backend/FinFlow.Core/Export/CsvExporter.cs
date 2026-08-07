using System.Globalization;
using System.Text;
using FinFlow.Classification;

namespace FinFlow.Export;

/// <summary>
/// Flat CSV mirroring the same classified data XlsxExporter uses — unlike the workbook, CSV has
/// no sheets/sections, so every transaction (auto/review/ignored alike) is one row, with
/// Category/Top Category/Status columns carrying what the workbook otherwise conveys via sheet
/// tabs, section headers, and row color.
/// </summary>
public static class CsvExporter
{
    public static string Export(IReadOnlyList<ClassifiedTransaction> classified)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Date;Category;Top Category;Bank;Counterparty;Purpose;Amount;Currency;Status");
        foreach (ClassifiedTransaction ct in classified)
        {
            LegacyTransaction t = ct.Transaction;
            sb.AppendLine(string.Join(';', new[]
            {
                t.BookingDate?.ToString("yyyy-MM-dd") ?? "",
                Clean(ct.Category),
                Clean(ct.TopCategory),
                t.SourceBank,
                Clean(t.CounterpartyName),
                Clean(t.Purpose),
                t.Amount.ToString(CultureInfo.InvariantCulture),
                t.Currency,
                ct.Status,
            }));
        }
        return sb.ToString();
    }

    private static string Clean(string? s) =>
        (s ?? "").Replace(';', ',').Replace('\n', ' ').Replace('\r', ' ');
}
