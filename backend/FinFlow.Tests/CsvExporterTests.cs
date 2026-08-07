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
        Purpose = "Rent March",
    };

    [Fact]
    public void Export_HeaderIncludesCategoryAndStatusColumns()
    {
        string csv = CsvExporter.Export([]);

        Assert.StartsWith("Date;Category;Top Category;Bank;Counterparty;Purpose;Amount;Currency;Status", csv);
    }

    [Fact]
    public void Export_OneRow_IncludesCategoryTopCategoryAndStatus()
    {
        List<ClassifiedTransaction> classified = [new(Tx("Landlord", -1200m), "Rent", "Housing", "auto")];

        string csv = CsvExporter.Export(classified);
        string[] lines = csv.Trim().Split('\n');

        Assert.Equal(2, lines.Length);
        Assert.Equal("2025-03-01;Rent;Housing;dkb;Landlord;Rent March;-1200;EUR;auto", lines[1].TrimEnd('\r'));
    }

    [Fact]
    public void Export_IncludesIgnoredAndReviewTransactionsToo()
    {
        List<ClassifiedTransaction> classified =
        [
            new(Tx("A", 100m), "Salary", "Salary", "auto"),
            new(Tx("B", -20m), "Other", "Other", "review"),
            new(Tx("C", -1m), "Other", "Other", "ignored"),
        ];

        string csv = CsvExporter.Export(classified);

        Assert.Contains(";auto", csv);
        Assert.Contains(";review", csv);
        Assert.Contains(";ignored", csv);
    }

    [Fact]
    public void Export_ChildlessCategory_TopCategoryEqualsCategory()
    {
        List<ClassifiedTransaction> classified = [new(Tx("Employer", 3000m), "Salary", "Salary", "auto")];

        string csv = CsvExporter.Export(classified);

        Assert.Contains("Salary;Salary;", csv);
    }
}
