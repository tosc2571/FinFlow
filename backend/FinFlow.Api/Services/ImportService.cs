using FinFlow.Api.Data;
using FinFlow.Api.Entities;

namespace FinFlow.Api.Services;

public record AnalyzeFileResult(string FileName, string? DetectedBank);

public record ImportFileResult(
    string FileName,
    string? Bank,
    int BatchId,
    int Imported,
    int Duplicates,
    string? Error);

/// <summary>
/// Wraps FinFlow.Core's parse pipeline for the API: detect the bank per file,
/// parse, skip duplicates via DedupeHash, classify against the DB rules
/// (first match wins, like the CLI's Classifier), and persist as an ImportBatch.
/// </summary>
public class ImportService(AppDbContext db, ClassificationService classification, ContractService contracts)
{
    private readonly ParserRegistry _registry = new();

    public IReadOnlyList<string> KnownBanks =>
        _registry.Parsers.Select(p => p.BankName).Distinct().ToList();

    /// <summary>Header-based auto-detection, used to pre-fill the bank per file in the import UI.</summary>
    public AnalyzeFileResult Analyze(string filePath, string fileName) =>
        new(fileName, _registry.Detect(filePath)?.BankName);

    public async Task<ImportFileResult> ImportFileAsync(string filePath, string fileName, string? bankHint)
    {
        bool hasHint = !string.IsNullOrEmpty(bankHint) && !bankHint.Equals("auto", StringComparison.OrdinalIgnoreCase);
        IBankCsvParser? parser = hasHint
            ? _registry.DetectWithHint(filePath, bankHint!)
            : _registry.Detect(filePath);

        if (parser is null)
            return FailBatch(fileName, bankHint,
                $"No matching parser for '{fileName}'. Known banks: {string.Join(", ", KnownBanks)}.");

        IReadOnlyList<LegacyTransaction> parsed;
        try
        {
            parsed = parser.Parse(filePath);
        }
        catch (Exception ex)
        {
            return FailBatch(fileName, parser.BankName, $"Error reading '{fileName}': {ex.Message}");
        }

        RuleSet rules = classification.LoadRuleSet();
        ImportBatch batch = new()
        {
            SourceFileName = fileName,
            DetectedBank = parser.BankName,
            Status = ImportStatus.Completed,
        };
        db.ImportBatches.Add(batch);

        HashSet<string> knownHashes = [.. db.Transactions.Select(t => t.DedupeHash)];
        int imported = 0, duplicates = 0;
        foreach (LegacyTransaction lt in parsed)
        {
            string hash = Transaction.ComputeDedupeHash(lt.SourceBank, lt.BookingDate, lt.Amount, lt.CounterpartyName, lt.Purpose);
            if (!knownHashes.Add(hash)) { duplicates++; continue; }

            (int? categoryId, ClassificationStatus status) = rules.Classify(lt.CounterpartyName, lt.Purpose);
            Transaction transaction = new()
            {
                ImportBatch = batch,
                SourceBank = lt.SourceBank,
                BookingDate = lt.BookingDate,
                ValueDate = lt.ValueDate,
                Amount = lt.Amount,
                Currency = lt.Currency,
                CounterpartyName = lt.CounterpartyName,
                CounterpartyIban = lt.CounterpartyIban,
                CounterpartyBic = lt.CounterpartyBic,
                Purpose = lt.Purpose,
                BookingType = lt.BookingType,
                Balance = lt.Balance,
                CategoryId = categoryId,
                ClassificationStatus = status,
                DedupeHash = hash,
            };
            await contracts.TryMatchAsync(transaction);
            db.Transactions.Add(transaction);
            imported++;
        }

        batch.TransactionCount = imported;
        batch.DuplicateCount = duplicates;
        await db.SaveChangesAsync();
        return new ImportFileResult(fileName, parser.BankName, batch.Id, imported, duplicates, null);
    }

    private ImportFileResult FailBatch(string fileName, string? bank, string error)
    {
        ImportBatch batch = new()
        {
            SourceFileName = fileName,
            DetectedBank = bank ?? "unknown",
            Status = ImportStatus.Failed,
            ErrorMessage = error,
        };
        db.ImportBatches.Add(batch);
        db.SaveChanges();
        return new ImportFileResult(fileName, bank, batch.Id, 0, 0, error);
    }

}
