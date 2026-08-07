using ClosedXML.Excel;
using FinFlow.Classification;
using FinFlow.Export;
using Xunit;

namespace FinFlow.Tests;

public class XlsxExporterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xlsx");

    private static LegacyTransaction Tx(string counterparty, decimal amount) => new()
    {
        SourceBank = "dkb",
        BookingDate = new DateOnly(2025, 3, 1),
        Amount = amount,
        Currency = "EUR",
        CounterpartyName = counterparty,
        Purpose = counterparty,
    };

    [Fact]
    public void ChildlessTopCategory_MatchesPreHierarchyLayout()
    {
        List<ClassifiedTransaction> classified =
        [
            new(Tx("Employer", 3000m), "Salary", "Salary", "auto"),
        ];

        XlsxExporter.Export(classified, _path);

        using XLWorkbook wb = new(_path);
        IXLWorksheet ws = wb.Worksheet("Salary");
        // Single-section layout: header row 1 has no "Category" column.
        Assert.Equal("Date", ws.Cell(1, 1).GetString());
        Assert.Equal("Bank", ws.Cell(1, 2).GetString());
        Assert.Equal("Amount (€)", ws.Cell(1, 5).GetString());
        Assert.Equal("Status", ws.Cell(1, 7).GetString());
        Assert.Equal("Employer", ws.Cell(2, 3).GetString());
        Assert.Equal("Subtotal", ws.Cell(3, 4).GetString());
        Assert.Equal(3000d, ws.Cell(3, 5).GetDouble());
    }

    [Fact]
    public void TopCategoryWithChildren_CreatesOneSectionPerChildWithCategoryColumn()
    {
        List<ClassifiedTransaction> classified =
        [
            new(Tx("Landlord", -1000m), "Rent", "Housing", "auto"),
            new(Tx("Utility Co", -150m), "Utilities", "Housing", "auto"),
        ];

        XlsxExporter.Export(classified, _path);

        using XLWorkbook wb = new(_path);
        IXLWorksheet ws = wb.Worksheet("Housing");

        // Section 1: "Rent" title, then an 8-column header including "Category".
        Assert.Equal("Rent", ws.Cell(1, 1).GetString());
        Assert.Equal("Date", ws.Cell(2, 1).GetString());
        Assert.Equal("Category", ws.Cell(2, 2).GetString());
        Assert.Equal("Amount (€)", ws.Cell(2, 6).GetString());
        Assert.Equal("Rent", ws.Cell(3, 2).GetString());
        Assert.Equal("Landlord", ws.Cell(3, 4).GetString());
    }

    [Fact]
    public void TransactionsDirectlyOnParent_GetAnOtherSection()
    {
        List<ClassifiedTransaction> classified =
        [
            new(Tx("Landlord", -1000m), "Rent", "Housing", "auto"),
            new(Tx("Direct on Housing", -50m), "Housing", "Housing", "auto"),
        ];

        XlsxExporter.Export(classified, _path);

        using XLWorkbook wb = new(_path);
        IXLWorksheet ws = wb.Worksheet("Housing");

        Assert.Contains(ws.Column(1).CellsUsed(), c => c.GetString() == "Other");
    }

    [Fact]
    public void ChildlessCategory_NeverGetsRelabeledOther()
    {
        List<ClassifiedTransaction> classified =
        [
            new(Tx("Employer", 3000m), "Salary", "Salary", "auto"),
        ];

        XlsxExporter.Export(classified, _path);

        using XLWorkbook wb = new(_path);
        IXLWorksheet ws = wb.Worksheet("Salary");
        Assert.DoesNotContain(ws.Column(1).CellsUsed(), c => c.GetString() == "Other");
    }

    [Fact]
    public void Overview_LinksSubCategoryRowToItsParentSheet()
    {
        List<ClassifiedTransaction> classified =
        [
            new(Tx("Landlord", -1000m), "Rent", "Housing", "auto"),
        ];

        XlsxExporter.Export(classified, _path);

        using XLWorkbook wb = new(_path);
        IXLWorksheet overview = wb.Worksheet("Overview");
        IXLCell rentCell = overview.Column(1).CellsUsed().Single(c => c.GetString() == "Rent");
        Assert.True(rentCell.HasHyperlink);
        Assert.Equal("Housing", rentCell.GetHyperlink().InternalAddress.Split('!')[0].Trim('\''));
    }

    [Fact]
    public void Overview_DisambiguatesSameNamedChildUnderDifferentParents()
    {
        List<ClassifiedTransaction> classified =
        [
            new(Tx("Insurer A", -50m), "Insurance", "Housing", "auto"),
            new(Tx("Insurer B", -80m), "Insurance", "Car", "auto"),
        ];

        XlsxExporter.Export(classified, _path);

        using XLWorkbook wb = new(_path);
        IXLWorksheet overview = wb.Worksheet("Overview");
        List<IXLCell> insuranceRows = [.. overview.Column(1).CellsUsed().Where(c => c.GetString() == "Insurance")];
        // Two distinct rows, not merged into one — each links to its own parent sheet.
        Assert.Equal(2, insuranceRows.Count);
        List<string> targets = [.. insuranceRows.Select(c => c.GetHyperlink().InternalAddress.Split('!')[0].Trim('\''))];
        Assert.Contains("Housing", targets);
        Assert.Contains("Car", targets);
    }

    [Fact]
    public void Overview_LinksToReviewAndIgnoredSheetsWhenPresent()
    {
        List<ClassifiedTransaction> classified =
        [
            new(Tx("Employer", 3000m), "Salary", "Salary", "auto"),
            new(Tx("Unknown", -20m), "Other", "Other", "review"),
            new(Tx("Spam", -1m), "Other", "Other", "ignored"),
        ];

        XlsxExporter.Export(classified, _path);

        using XLWorkbook wb = new(_path);
        IXLWorksheet overview = wb.Worksheet("Overview");
        Assert.Contains(overview.Column(1).CellsUsed(), c => c.GetString() == "Needs review" && c.HasHyperlink);
        Assert.Contains(overview.Column(1).CellsUsed(), c => c.GetString() == "Ignored" && c.HasHyperlink);
    }

    [Fact]
    public void Overview_IsAlwaysTheFirstSheet()
    {
        List<ClassifiedTransaction> classified =
        [
            new(Tx("Employer", 3000m), "Salary", "Salary", "auto"),
        ];

        XlsxExporter.Export(classified, _path);

        using XLWorkbook wb = new(_path);
        Assert.Equal("Overview", wb.Worksheets.First().Name);
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
