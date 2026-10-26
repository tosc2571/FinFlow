using System.Text;
using FinFlow.Api.Data;
using FinFlow.Api.Services;
using FinFlow.Classification;
using FinFlow.Export;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Api.Endpoints;

/// <summary>
/// Reuses Core's exporters (XlsxExporter, CsvExporter) by mapping the persisted entities back
/// onto Core's LegacyTransaction/ClassifiedTransaction shapes.
/// </summary>
public static class ExportEndpoints
{
    public static void MapExportEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/export");

        group.MapGet("/xlsx", (AppDbContext db, [AsParameters] TransactionFilterParams filter) =>
        {
            List<ClassifiedTransaction> classified = LoadClassified(db, filter);

            // XlsxExporter writes to a path, not a stream — use a temp file.
            string tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xlsx");
            try
            {
                XlsxExporter.Export(classified, tmp);
                return Results.File(File.ReadAllBytes(tmp),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    FileName(filter, "xlsx"));
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        });

        group.MapGet("/csv", (AppDbContext db, [AsParameters] TransactionFilterParams filter) =>
        {
            string csv = CsvExporter.Export(LoadClassified(db, filter));
            return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", FileName(filter, "csv"));
        });
    }

    internal static List<ClassifiedTransaction> LoadClassified(AppDbContext db, TransactionFilterParams filter)
    {
        // Materialize first, then project — ToLegacy/ToCliStatus are arbitrary C# methods EF
        // Core 8 can't translate to SQL; chaining .Select() onto the still-IQueryable pipeline
        // throws at runtime instead of falling back to client evaluation (see DashboardService
        // for the same pitfall).
        List<Entities.Transaction> transactions = [.. TransactionFilters.Apply(
                db.Transactions.AsNoTracking().Include(t => t.Category),
                filter)
            .OrderBy(t => t.BookingDate).ThenBy(t => t.Id)];

        // Category nesting is arbitrarily deep (#70/#72) — load every category once and walk
        // each transaction's own category up to its true root in memory, rather than a
        // fixed-depth .ThenInclude chain that only ever reaches one ancestor.
        Dictionary<int, Entities.Category> categoriesById = db.Categories.AsNoTracking().ToDictionary(c => c.Id);

        return [.. transactions.Select(t =>
        {
            Entities.Category? topCategory = ResolveTopCategory(t.Category, categoriesById);
            return new ClassifiedTransaction(
                ToLegacy(t),
                t.Category?.Name ?? "Other",
                topCategory?.Name ?? "Other",
                ToExportStatus(t.ClassificationStatus),
                t.Category?.Priority ?? 0,
                topCategory?.Priority ?? 0);
        })];
    }

    /// <summary>
    /// Walks a category's parent chain up to its root (ParentCategoryId == null), however deep
    /// the nesting goes — a category directly on the root is its own result. The depth cap is
    /// only a defensive backstop against a corrupted cyclic parent chain (the API's own
    /// validation already rejects creating one), not a real limit.
    /// </summary>
    private static Entities.Category? ResolveTopCategory(Entities.Category? category, Dictionary<int, Entities.Category> categoriesById)
    {
        Entities.Category? current = category;
        for (int depth = 0; current?.ParentCategoryId is { } parentId && depth < 50; depth++)
            current = categoriesById.GetValueOrDefault(parentId);
        return current;
    }

    private static LegacyTransaction ToLegacy(Entities.Transaction t) => new()
    {
        SourceBank = t.SourceBank,
        BookingDate = t.BookingDate,
        ValueDate = t.ValueDate,
        Amount = t.Amount,
        Currency = t.Currency,
        CounterpartyName = t.CounterpartyName,
        CounterpartyIban = t.CounterpartyIban,
        CounterpartyBic = t.CounterpartyBic,
        Purpose = t.Purpose,
        BookingType = t.BookingType,
        Balance = t.Balance,
    };

    // ManualOverride is a user-confirmed category — green ("auto") in the workbook.
    // InternalTransfer folds into the same "ignored" bucket as Ignored — excluded from the
    // money sheets either way, and not worth a dedicated sheet in the shared exporter.
    private static string ToExportStatus(Entities.ClassificationStatus status) => status switch
    {
        Entities.ClassificationStatus.Auto or Entities.ClassificationStatus.ManualOverride => "auto",
        Entities.ClassificationStatus.Ignored or Entities.ClassificationStatus.InternalTransfer => "ignored",
        _ => "review",
    };

    private static string FileName(TransactionFilterParams filter, string extension) =>
        filter.Year is { } year ? $"taxes-{year}.{extension}" : $"finflow-export.{extension}";
}
