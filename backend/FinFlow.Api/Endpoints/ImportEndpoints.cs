using FinFlow.Api.Data;
using FinFlow.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Api.Endpoints;

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/import");

        // Step 1 of the import UI: upload files once to see which bank each was detected as.
        // The UI shows the detection per file and lets the user override before the real import.
        group.MapPost("/analyze", async (HttpRequest request, ImportService svc) =>
        {
            IFormCollection form = await request.ReadFormAsync();
            List<AnalyzeFileResult> results = [];
            foreach (IFormFile file in form.Files)
            {
                string tmp = await SaveToTempFile(file);
                try { results.Add(svc.Analyze(tmp, file.FileName)); }
                finally { File.Delete(tmp); }
            }
            return Results.Ok(new { files = results, knownBanks = svc.KnownBanks });
        }).DisableAntiforgery();

        // Step 2: the actual import. Optional per-file bank override via a "banks" form field
        // parallel to the file order (empty string or "auto" = auto-detect).
        group.MapPost("/", async (HttpRequest request, ImportService svc) =>
        {
            IFormCollection form = await request.ReadFormAsync();
            string?[] banks = form["banks"].ToArray();
            List<ImportFileResult> results = [];
            for (int i = 0; i < form.Files.Count; i++)
            {
                IFormFile file = form.Files[i];
                string? hint = i < banks.Length ? banks[i] : null;
                string tmp = await SaveToTempFile(file);
                try { results.Add(await svc.ImportFileAsync(tmp, file.FileName, hint)); }
                finally { File.Delete(tmp); }
            }
            return Results.Ok(results);
        }).DisableAntiforgery();

        group.MapGet("/batches", (AppDbContext db) =>
            db.ImportBatches
                .OrderByDescending(b => b.ImportedAt)
                .Select(b => new
                {
                    b.Id,
                    b.SourceFileName,
                    b.DetectedBank,
                    b.ImportedAt,
                    b.TransactionCount,
                    b.DuplicateCount,
                    Status = b.Status.ToString(),
                    b.ErrorMessage,
                    // The date span actually covered by this batch's persisted transactions — a
                    // duplicate-only batch has none, so both are null (#62).
                    OldestTransactionDate = b.Transactions.Min(t => t.BookingDate),
                    NewestTransactionDate = b.Transactions.Max(t => t.BookingDate),
                })
                .ToList());

        group.MapGet("/batches/{id:int}", (int id, AppDbContext db) =>
            db.ImportBatches.Where(b => b.Id == id)
                .Select(b => new
                {
                    b.Id,
                    b.SourceFileName,
                    b.DetectedBank,
                    b.ImportedAt,
                    b.TransactionCount,
                    b.DuplicateCount,
                    Status = b.Status.ToString(),
                    b.ErrorMessage,
                    OldestTransactionDate = b.Transactions.Min(t => t.BookingDate),
                    NewestTransactionDate = b.Transactions.Max(t => t.BookingDate),
                })
                .FirstOrDefault() is { } batch
                ? Results.Ok(batch)
                : Results.NotFound());

        // Rollback: deleting a batch cascade-deletes its transactions (required FK).
        group.MapDelete("/batches/{id:int}", async (int id, AppDbContext db) =>
        {
            Entities.ImportBatch? batch = await db.ImportBatches.FirstOrDefaultAsync(b => b.Id == id);
            if (batch is null) return Results.NotFound();
            db.ImportBatches.Remove(batch);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static async Task<string> SaveToTempFile(IFormFile file)
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        await using FileStream stream = File.Create(path);
        await file.CopyToAsync(stream);
        return path;
    }
}
