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

        List<ClassifiedTransaction> pruef = classified
            .Where(c => c.Status == "prüfen")
            .OrderBy(c => c.Category).ThenBy(c => c.Transaction.BookingDate)
            .ToList();
        List<ClassifiedTransaction> ignored = classified.Where(c => c.Status == "ignorieren").ToList();

        // Sheet names are resolved as we go so Übersicht (added first) can already link to them.
        Dictionary<string, IXLWorksheet> topSheets = [];
        foreach (IGrouping<string, ClassifiedTransaction> topGroup in classified
            .Where(c => c.Status != "ignorieren")
            .GroupBy(c => c.TopCategory)
            .OrderBy(g => g.Key))
        {
            topSheets[topGroup.Key] = AddTopCategorySheet(wb, topGroup.Key, topGroup.ToList());
        }

        IXLWorksheet? reviewSheet = pruef.Count > 0 ? AddReviewSheet(wb, pruef) : null;
        IXLWorksheet? ignoredSheet = ignored.Count > 0 ? AddIgnoredSheet(wb, ignored) : null;

        // Übersicht is inserted first (position 0) so it's always the workbook's opening sheet,
        // even though it's built last — once every other sheet (and thus every link target) exists.
        AddOverviewSheet(wb, classified, topSheets, reviewSheet, ignoredSheet).Position = 1;

        wb.SaveAs(outputPath);
    }

    private static IXLWorksheet AddOverviewSheet(
        XLWorkbook wb,
        IReadOnlyList<ClassifiedTransaction> all,
        Dictionary<string, IXLWorksheet> topSheets,
        IXLWorksheet? reviewSheet,
        IXLWorksheet? ignoredSheet)
    {
        IXLWorksheet ws = wb.Worksheets.Add("Übersicht");
        int row = 1;

        WriteHeader(ws, row++, ["Kategorie", "Auto (Anzahl)", "Auto (Summe €)", "Prüfen (Anzahl)", "Prüfen (Summe €)", "Gesamt €"]);

        // Grouped by (TopCategory, Category), not Category alone — two different sub-categories
        // under different parents may share a name (siblings must be unique, cross-parent reuse
        // is allowed), and a bare Category grouping would silently merge their totals together.
        foreach (IGrouping<(string TopCategory, string Category), ClassifiedTransaction> g in all
            .Where(c => c.Status != "ignorieren")
            .GroupBy(c => (c.TopCategory, c.Category))
            .OrderBy(g => g.Key.TopCategory).ThenBy(g => g.Key.Category))
        {
            List<ClassifiedTransaction> autoItems  = g.Where(c => c.Status == "auto").ToList();
            List<ClassifiedTransaction> pruefItems = g.Where(c => c.Status == "prüfen").ToList();

            ws.Cell(row, 1).Value = g.Key.Category;
            if (topSheets.TryGetValue(g.Key.TopCategory, out IXLWorksheet? target))
                SetInternalLink(ws.Cell(row, 1), target);
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
        row += 2;

        if (reviewSheet is not null)
        {
            ws.Cell(row, 1).Value = "Zu prüfen";
            SetInternalLink(ws.Cell(row, 1), reviewSheet);
            row++;
        }
        if (ignoredSheet is not null)
        {
            ws.Cell(row, 1).Value = "Ignoriert";
            SetInternalLink(ws.Cell(row, 1), ignoredSheet);
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        return ws;
    }

    /// <summary>
    /// One sheet per top-level category. A childless one gets a single, unlabeled section —
    /// identical output to before this feature existed. One with children gets one section per
    /// child plus a "Sonstiges" section for transactions on the parent itself (only if any exist),
    /// each row also carrying an explicit Kategorie column since the section header alone is lost
    /// once someone sorts/filters/copies rows out of the sheet.
    /// </summary>
    private static IXLWorksheet AddTopCategorySheet(XLWorkbook wb, string topCategory, List<ClassifiedTransaction> items)
    {
        IXLWorksheet ws = wb.Worksheets.Add(SafeName(topCategory));

        List<IGrouping<string, ClassifiedTransaction>> sections = [.. items
            .GroupBy(c => c.Category)
            .OrderBy(g => g.Key == topCategory ? 1 : 0) // "Sonstiges" (directly on the parent) last
            .ThenBy(g => g.Key)];

        bool isSingleSection = sections.Count == 1 && sections[0].Key == topCategory;
        int row = 1;

        if (isSingleSection)
        {
            WriteTransactionSection(ws, ref row, null, sections[0].ToList());
        }
        else
        {
            foreach (IGrouping<string, ClassifiedTransaction> section in sections)
            {
                string label = section.Key == topCategory ? "Sonstiges" : section.Key;
                WriteTransactionSection(ws, ref row, label, section.ToList());
                row++; // spacer between sections
            }

            ws.Cell(row, 5).Value = "Gesamt";
            ws.Cell(row, 6).Value = (double)items.Sum(c => c.Transaction.Amount);
            SetAmountFormat(ws, row, 6);
            ws.Range(row, 1, row, 8).Style.Fill.BackgroundColor = ColTotal;
            ws.Range(row, 1, row, 8).Style.Font.Bold = true;
            ws.SheetView.FreezeRows(1);
        }

        ws.Columns().AdjustToContents();
        int purposeCol = isSingleSection ? 4 : 5;
        if (ws.Column(purposeCol).Width > 60) ws.Column(purposeCol).Width = 60;
        return ws;
    }

    /// <summary>
    /// Writes one section: an optional bold section-title row (omitted for a sheet's only
    /// section, matching the pre-hierarchy single-category layout exactly), a column header, the
    /// transaction rows, and a subtotal row. A Kategorie column is added whenever there's a
    /// section label to show — i.e. whenever this sheet actually has more than one section.
    /// </summary>
    private static void WriteTransactionSection(IXLWorksheet ws, ref int row, string? sectionLabel, List<ClassifiedTransaction> items)
    {
        if (sectionLabel is not null)
        {
            ws.Cell(row, 1).Value = sectionLabel;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 13;
            row++;
        }

        string[] columns = sectionLabel is null
            ? ["Datum", "Bank", "Empfänger", "Verwendungszweck", "Betrag €", "Währung", "Status"]
            : ["Datum", "Kategorie", "Bank", "Empfänger", "Verwendungszweck", "Betrag €", "Währung", "Status"];
        WriteHeader(ws, row++, columns);

        int amountCol = sectionLabel is null ? 5 : 6;
        int statusCol = sectionLabel is null ? 7 : 8;

        foreach (ClassifiedTransaction ct in items.OrderBy(c => c.Transaction.BookingDate))
        {
            LegacyTransaction t = ct.Transaction;
            int col = 1;
            ws.Cell(row, col++).Value = t.BookingDate?.ToString("dd.MM.yyyy") ?? "";
            if (sectionLabel is not null) ws.Cell(row, col++).Value = ct.Category;
            ws.Cell(row, col++).Value = t.SourceBank;
            ws.Cell(row, col++).Value = t.CounterpartyName ?? "";
            ws.Cell(row, col++).Value = t.Purpose ?? "";
            ws.Cell(row, col++).Value = (double)t.Amount;
            ws.Cell(row, col++).Value = t.Currency;
            ws.Cell(row, col).Value = ct.Status;
            SetAmountFormat(ws, row, amountCol);
            ws.Range(row, 1, row, statusCol).Style.Fill.BackgroundColor =
                ct.Status == "auto" ? ColAuto : ColPruefen;
            row++;
        }

        int purposeCol = sectionLabel is null ? 4 : 5;
        ws.Cell(row, purposeCol).Value = "Summe";
        ws.Cell(row, amountCol).Value = (double)items.Sum(c => c.Transaction.Amount);
        SetAmountFormat(ws, row, amountCol);
        ws.Range(row, 1, row, statusCol).Style.Fill.BackgroundColor = ColTotal;
        ws.Range(row, 1, row, statusCol).Style.Font.Bold = true;
        row++;
    }

    private static IXLWorksheet AddReviewSheet(XLWorkbook wb, List<ClassifiedTransaction> items)
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
        return ws;
    }

    private static IXLWorksheet AddIgnoredSheet(XLWorkbook wb, List<ClassifiedTransaction> ignored)
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
        return ws;
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

    private static void SetInternalLink(IXLCell cell, IXLWorksheet target)
    {
        cell.SetHyperlink(new XLHyperlink(target.Cell(1, 1)));
        cell.Style.Font.FontColor = XLColor.Blue;
        cell.Style.Font.Underline = XLFontUnderlineValues.Single;
    }

    private static string SafeName(string name)
    {
        char[] invalid = new[] { '/', '\\', '?', '*', '[', ']', ':' };
        string safe = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        return safe.Length > 31 ? safe[..31] : safe;
    }
}
