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
            new(Tx("Arbeitgeber", 3000m), "Gehalt", "Gehalt", "auto"),
        ];

        XlsxExporter.Export(classified, _path);

        using XLWorkbook wb = new(_path);
        IXLWorksheet ws = wb.Worksheet("Gehalt");
        // Single-section layout: header row 1 has no "Kategorie" column.
        Assert.Equal("Datum", ws.Cell(1, 1).GetString());
        Assert.Equal("Bank", ws.Cell(1, 2).GetString());
        Assert.Equal("Betrag €", ws.Cell(1, 5).GetString());
        Assert.Equal("Status", ws.Cell(1, 7).GetString());
        Assert.Equal("Arbeitgeber", ws.Cell(2, 3).GetString());
        Assert.Equal("Summe", ws.Cell(3, 4).GetString());
        Assert.Equal(3000d, ws.Cell(3, 5).GetDouble());
    }

    [Fact]
    public void TopCategoryWithChildren_CreatesOneSectionPerChildWithKategorieColumn()
    {
        List<ClassifiedTransaction> classified =
        [
            new(Tx("Vermieter", -1000m), "Miete", "Wohnen", "auto"),
            new(Tx("Stadtwerke", -150m), "Nebenkosten", "Wohnen", "auto"),
        ];

        XlsxExporter.Export(classified, _path);

        using XLWorkbook wb = new(_path);
        IXLWorksheet ws = wb.Worksheet("Wohnen");

        // Section 1: "Miete" title, then an 8-column header including "Kategorie".
        Assert.Equal("Miete", ws.Cell(1, 1).GetString());
        Assert.Equal("Datum", ws.Cell(2, 1).GetString());
        Assert.Equal("Kategorie", ws.Cell(2, 2).GetString());
        Assert.Equal("Betrag €", ws.Cell(2, 6).GetString());
        Assert.Equal("Miete", ws.Cell(3, 2).GetString());
        Assert.Equal("Vermieter", ws.Cell(3, 4).GetString());
    }

    [Fact]
    public void TransactionsDirectlyOnParent_GetASonstigesSection()
    {
        List<ClassifiedTransaction> classified =
        [
            new(Tx("Vermieter", -1000m), "Miete", "Wohnen", "auto"),
            new(Tx("Direkt auf Wohnen", -50m), "Wohnen", "Wohnen", "auto"),
        ];

        XlsxExporter.Export(classified, _path);

        using XLWorkbook wb = new(_path);
        IXLWorksheet ws = wb.Worksheet("Wohnen");

        List<string> sectionTitles = [ws.Cell(1, 1).GetString()];
        // "Sonstiges" sorts after named children — find its title row by scanning column A.
        bool foundSonstiges = ws.Column(1).CellsUsed().Any(c => c.GetString() == "Sonstiges");
        Assert.True(foundSonstiges);
    }

    [Fact]
    public void ChildlessCategory_NeverGetsRelabeledSonstiges()
    {
        List<ClassifiedTransaction> classified =
        [
            new(Tx("Arbeitgeber", 3000m), "Gehalt", "Gehalt", "auto"),
        ];

        XlsxExporter.Export(classified, _path);

        using XLWorkbook wb = new(_path);
        IXLWorksheet ws = wb.Worksheet("Gehalt");
        Assert.DoesNotContain(ws.Column(1).CellsUsed(), c => c.GetString() == "Sonstiges");
    }

    [Fact]
    public void Overview_LinksSubCategoryRowToItsParentSheet()
    {
        List<ClassifiedTransaction> classified =
        [
            new(Tx("Vermieter", -1000m), "Miete", "Wohnen", "auto"),
        ];

        XlsxExporter.Export(classified, _path);

        using XLWorkbook wb = new(_path);
        IXLWorksheet overview = wb.Worksheet("Übersicht");
        IXLCell mieteCell = overview.Column(1).CellsUsed().Single(c => c.GetString() == "Miete");
        Assert.True(mieteCell.HasHyperlink);
        Assert.Equal("Wohnen", mieteCell.GetHyperlink().InternalAddress.Split('!')[0].Trim('\''));
    }

    [Fact]
    public void Overview_DisambiguatesSameNamedChildUnderDifferentParents()
    {
        List<ClassifiedTransaction> classified =
        [
            new(Tx("Versicherer A", -50m), "Versicherung", "Wohnen", "auto"),
            new(Tx("Versicherer B", -80m), "Versicherung", "Auto", "auto"),
        ];

        XlsxExporter.Export(classified, _path);

        using XLWorkbook wb = new(_path);
        IXLWorksheet overview = wb.Worksheet("Übersicht");
        List<IXLCell> versicherungRows = [.. overview.Column(1).CellsUsed().Where(c => c.GetString() == "Versicherung")];
        // Two distinct rows, not merged into one — each links to its own parent sheet.
        Assert.Equal(2, versicherungRows.Count);
        List<string> targets = [.. versicherungRows.Select(c => c.GetHyperlink().InternalAddress.Split('!')[0].Trim('\''))];
        Assert.Contains("Wohnen", targets);
        Assert.Contains("Auto", targets);
    }

    [Fact]
    public void Overview_LinksToReviewAndIgnoredSheetsWhenPresent()
    {
        List<ClassifiedTransaction> classified =
        [
            new(Tx("Arbeitgeber", 3000m), "Gehalt", "Gehalt", "auto"),
            new(Tx("Unbekannt", -20m), "Sonstiges", "Sonstiges", "prüfen"),
            new(Tx("Spam", -1m), "Sonstiges", "Sonstiges", "ignorieren"),
        ];

        XlsxExporter.Export(classified, _path);

        using XLWorkbook wb = new(_path);
        IXLWorksheet overview = wb.Worksheet("Übersicht");
        Assert.Contains(overview.Column(1).CellsUsed(), c => c.GetString() == "Zu prüfen" && c.HasHyperlink);
        Assert.Contains(overview.Column(1).CellsUsed(), c => c.GetString() == "Ignoriert" && c.HasHyperlink);
    }

    [Fact]
    public void Overview_IsAlwaysTheFirstSheet()
    {
        List<ClassifiedTransaction> classified =
        [
            new(Tx("Arbeitgeber", 3000m), "Gehalt", "Gehalt", "auto"),
        ];

        XlsxExporter.Export(classified, _path);

        using XLWorkbook wb = new(_path);
        Assert.Equal("Übersicht", wb.Worksheets.First().Name);
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
