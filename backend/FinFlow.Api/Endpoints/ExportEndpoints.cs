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

    private static List<ClassifiedTransaction> LoadClassified(AppDbContext db, TransactionFilterParams filter)
    {
        // Materialize first, then project — ToLegacy/ToCliStatus are arbitrary C# methods EF
        // Core 8 can't translate to SQL; chaining .Select() onto the still-IQueryable pipeline
        // throws at runtime instead of falling back to client evaluation (see DashboardService
        // for the same pitfall).
        List<Entities.Transaction> transactions = [.. TransactionFilters.Apply(
                db.Transactions.AsNoTracking().Include(t => t.Category).ThenInclude(c => c!.ParentCategory),
                filter)
            .OrderBy(t => t.BookingDate).ThenBy(t => t.Id)];

        return [.. transactions.Select(t => new ClassifiedTransaction(
            ToLegacy(t),
            t.Category?.Name ?? "Sonstiges",
            t.Category?.ParentCategory?.Name ?? t.Category?.Name ?? "Sonstiges",
            ToCliStatus(t.ClassificationStatus)))];
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
    // InternalTransfer folds into the same "ignorieren" bucket as Ignored — excluded from the
    // money sheets either way, and not worth a dedicated sheet in the CLI's shared exporter.
    private static string ToCliStatus(Entities.ClassificationStatus status) => status switch
    {
        Entities.ClassificationStatus.Auto or Entities.ClassificationStatus.ManualOverride => "auto",
        Entities.ClassificationStatus.Ignored or Entities.ClassificationStatus.InternalTransfer => "ignorieren",
        _ => "prüfen",
    };

    private static string FileName(TransactionFilterParams filter, string extension) =>
        filter.Year is { } year ? $"taxes-{year}.{extension}" : $"finflow-export.{extension}";
}
