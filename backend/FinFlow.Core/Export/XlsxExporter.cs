using FinFlow.Classification;
using ClosedXML.Excel;

namespace FinFlow.Export;

public static class XlsxExporter
{
    private static readonly XLColor ColHeader     = XLColor.FromArgb(0x1F, 0x49, 0x7D);
    private static readonly XLColor ColHeaderFont = XLColor.White;
    private static readonly XLColor ColAuto       = XLColor.FromArgb(0xC6, 0xEF, 0xCE); // light green
    private static readonly XLColor ColPruefen    = XLColor.FromArgb(0xFF, 0xEB, 0x9C); // light yellow
    private static readonly XLColor ColTotal      = XLColor.FromArgb(0xDD, 0xEB, 0xF7); // light blue

    public static void Export(IReadOnlyList<ClassifiedTransaction> classified, string outputPath)
    {
        using XLWorkbook wb = new XLWorkbook();

        AddOverviewSheet(wb, classified);

        foreach (IGrouping<string, ClassifiedTransaction> group in classified
            .Where(c => c.Status != "ignorieren")
            .GroupBy(c => c.Category)
            .OrderBy(g => g.Key))
        {
            AddCategorySheet(wb, group.Key, group.ToList());
        }

        List<ClassifiedTransaction> pruef = classified
            .Where(c => c.Status == "prüfen")
            .OrderBy(c => c.Category).ThenBy(c => c.Transaction.BookingDate)
            .ToList();
        if (pruef.Count > 0)
            AddReviewSheet(wb, pruef);

        List<ClassifiedTransaction> ignored = classified.Where(c => c.Status == "ignorieren").ToList();
        if (ignored.Count > 0)
            AddIgnoredSheet(wb, ignored);

        wb.SaveAs(outputPath);
    }

    private static void AddOverviewSheet(XLWorkbook wb, IReadOnlyList<ClassifiedTransaction> all)
    {
        IXLWorksheet ws = wb.Worksheets.Add("Übersicht");
        int row = 1;

        WriteHeader(ws, row++, ["Kategorie", "Auto (Anzahl)", "Auto (Summe €)", "Prüfen (Anzahl)", "Prüfen (Summe €)", "Gesamt €"]);

        foreach (IGrouping<string, ClassifiedTransaction> g in all
            .Where(c => c.Status != "ignorieren")
            .GroupBy(c => c.Category)
            .OrderBy(g => g.Key))
        {
            List<ClassifiedTransaction> autoItems  = g.Where(c => c.Status == "auto").ToList();
            List<ClassifiedTransaction> pruefItems = g.Where(c => c.Status == "prüfen").ToList();

            ws.Cell(row, 1).Value = g.Key;
            ws.Cell(row, 2).Value = autoItems.Count;
            ws.Cell(row, 3).Value = (double)autoItems.Sum(c => c.Transaction.Amount);
            ws.Cell(row, 4).Value = pruefItems.Count;
            ws.Cell(row, 5).Value = (double)pruefItems.Sum(c => c.Transaction.Amount);
            ws.Cell(row, 6).Value = (double)g.Sum(c => c.Transaction.Amount);
            SetAmountFormat(ws, row, 3);
            SetAmountFormat(ws, row, 5);
            SetAmountFormat(ws, row, 6);
            row++;
        }

        // Total row
        ws.Cell(row, 1).Value = "Gesamt";
        ws.Cell(row, 6).Value = (double)all.Where(c => c.Status != "ignorieren").Sum(c => c.Transaction.Amount);
        SetAmountFormat(ws, row, 6);
        ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = ColTotal;
        ws.Range(row, 1, row, 6).Style.Font.Bold = true;

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    private static void AddCategorySheet(XLWorkbook wb, string category, List<ClassifiedTransaction> items)
    {
        IXLWorksheet ws = wb.Worksheets.Add(SafeName(category));
        int row = 1;

        WriteHeader(ws, row++, ["Datum", "Bank", "Empfänger", "Verwendungszweck", "Betrag €", "Währung", "Status"]);

        foreach (ClassifiedTransaction ct in items.OrderBy(c => c.Transaction.BookingDate))
        {
            LegacyTransaction t = ct.Transaction;
            ws.Cell(row, 1).Value = t.BookingDate?.ToString("dd.MM.yyyy") ?? "";
            ws.Cell(row, 2).Value = t.SourceBank;
            ws.Cell(row, 3).Value = t.CounterpartyName ?? "";
            ws.Cell(row, 4).Value = t.Purpose ?? "";
            ws.Cell(row, 5).Value = (double)t.Amount;
            ws.Cell(row, 6).Value = t.Currency;
            ws.Cell(row, 7).Value = ct.Status;
            SetAmountFormat(ws, row, 5);
            ws.Range(row, 1, row, 7).Style.Fill.BackgroundColor =
                ct.Status == "auto" ? ColAuto : ColPruefen;
            row++;
        }

        // Total row
        ws.Cell(row, 4).Value = "Summe";
        ws.Cell(row, 5).Value = (double)items.Sum(c => c.Transaction.Amount);
        SetAmountFormat(ws, row, 5);
        ws.Range(row, 1, row, 7).Style.Fill.BackgroundColor = ColTotal;
        ws.Range(row, 1, row, 7).Style.Font.Bold = true;

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        if (ws.Column(4).Width > 60) ws.Column(4).Width = 60;
    }

    private static void AddReviewSheet(XLWorkbook wb, List<ClassifiedTransaction> items)
    {
        IXLWorksheet ws = wb.Worksheets.Add("Zu prüfen");
        int row = 1;

        WriteHeader(ws, row++, ["Datum", "Kategorie", "Bank", "Empfänger", "Verwendungszweck", "Betrag €", "Währung"]);

        foreach (ClassifiedTransaction ct in items)
        {
            LegacyTransaction t = ct.Transaction;
            ws.Cell(row, 1).Value = t.BookingDate?.ToString("dd.MM.yyyy") ?? "";
            ws.Cell(row, 2).Value = ct.Category;
            ws.Cell(row, 3).Value = t.SourceBank;
            ws.Cell(row, 4).Value = t.CounterpartyName ?? "";
            ws.Cell(row, 5).Value = t.Purpose ?? "";
            ws.Cell(row, 6).Value = (double)t.Amount;
            ws.Cell(row, 7).Value = t.Currency;
            SetAmountFormat(ws, row, 6);
            ws.Range(row, 1, row, 7).Style.Fill.BackgroundColor = ColPruefen;
            row++;
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        if (ws.Column(5).Width > 60) ws.Column(5).Width = 60;
    }

    private static void AddIgnoredSheet(XLWorkbook wb, List<ClassifiedTransaction> ignored)
    {
        IXLWorksheet ws = wb.Worksheets.Add("Ignoriert");
        int row = 1;

        WriteHeader(ws, row++, ["Empfänger", "Kategorie", "Anzahl", "Summe €"]);

        foreach (IGrouping<string, ClassifiedTransaction> g in ignored
            .GroupBy(c => c.Transaction.CounterpartyName ?? "(unbekannt)")
            .OrderBy(g => g.Key))
        {
            ws.Cell(row, 1).Value = g.Key;
            ws.Cell(row, 2).Value = g.First().Category;
            ws.Cell(row, 3).Value = g.Count();
            ws.Cell(row, 4).Value = (double)g.Sum(c => c.Transaction.Amount);
            SetAmountFormat(ws, row, 4);
            row++;
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    private static void WriteHeader(IXLWorksheet ws, int row, string[] columns)
    {
        for (int col = 1; col <= columns.Length; col++)
        {
            ws.Cell(row, col).Value = columns[col - 1];
            ws.Cell(row, col).Style.Font.Bold = true;
            ws.Cell(row, col).Style.Fill.BackgroundColor = ColHeader;
            ws.Cell(row, col).Style.Font.FontColor = ColHeaderFont;
        }
        ws.Row(row).Height = 20;
    }

    private static void SetAmountFormat(IXLWorksheet ws, int row, int col) =>
        ws.Cell(row, col).Style.NumberFormat.Format = "#,##0.00";

    private static string SafeName(string name)
    {
        char[] invalid = new[] { '/', '\\', '?', '*', '[', ']', ':' };
        string safe = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        return safe.Length > 31 ? safe[..31] : safe;
    }
}
