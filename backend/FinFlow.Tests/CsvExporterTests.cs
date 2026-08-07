using FinFlow.Classification;
using FinFlow.Export;
using Xunit;

namespace FinFlow.Tests;

public class CsvExporterTests
{
    private static LegacyTransaction Tx(string counterparty, decimal amount) => new()
    {
        SourceBank = "dkb",
        BookingDate = new DateOnly(2025, 3, 1),
        Amount = amount,
        Currency = "EUR",
        CounterpartyName = counterparty,
        Purpose = "Miete März",
    };

    [Fact]
    public void Export_HeaderIncludesCategoryAndStatusColumns()
    {
        string csv = CsvExporter.Export([]);

        Assert.StartsWith("Datum;Kategorie;Oberkategorie;Bank;Empfänger;Verwendungszweck;Betrag;Währung;Status", csv);
    }

    [Fact]
    public void Export_OneRow_IncludesCategoryTopCategoryAndStatus()
    {
        List<ClassifiedTransaction> classified = [new(Tx("Vermieter", -1200m), "Miete", "Wohnen", "auto")];

        string csv = CsvExporter.Export(classified);
        string[] lines = csv.Trim().Split('\n');

        Assert.Equal(2, lines.Length);
        Assert.Equal("2025-03-01;Miete;Wohnen;dkb;Vermieter;Miete März;-1200;EUR;auto", lines[1].TrimEnd('\r'));
    }

    [Fact]
    public void Export_IncludesIgnoredAndReviewTransactionsToo()
    {
        List<ClassifiedTransaction> classified =
        [
            new(Tx("A", 100m), "Gehalt", "Gehalt", "auto"),
            new(Tx("B", -20m), "Sonstiges", "Sonstiges", "prüfen"),
            new(Tx("C", -1m), "Sonstiges", "Sonstiges", "ignorieren"),
        ];

        string csv = CsvExporter.Export(classified);

        Assert.Contains(";auto", csv);
        Assert.Contains(";prüfen", csv);
        Assert.Contains(";ignorieren", csv);
    }

    [Fact]
    public void Export_ChildlessCategory_TopCategoryEqualsCategory()
    {
        List<ClassifiedTransaction> classified = [new(Tx("Arbeitgeber", 3000m), "Gehalt", "Gehalt", "auto")];

        string csv = CsvExporter.Export(classified);

        Assert.Contains("Gehalt;Gehalt;", csv);
    }
}
