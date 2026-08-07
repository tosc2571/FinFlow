namespace FinFlow.Api.Entities;

public class ImportBatch
{
    public int Id { get; set; }
    public int? BankAccountId { get; set; }
    public BankAccount? BankAccount { get; set; }
    public required string SourceFileName { get; set; }
    public required string DetectedBank { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Total rows parsed from the file — imported (TransactionCount - DuplicateCount)
    /// plus skipped duplicates, not just the persisted ones.</summary>
    public int TransactionCount { get; set; }
    public int DuplicateCount { get; set; }
    public ImportStatus Status { get; set; }
    public string? ErrorMessage { get; set; }

    public List<Transaction> Transactions { get; set; } = [];
}
