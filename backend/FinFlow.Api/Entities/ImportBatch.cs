namespace FinFlow.Api.Entities;

public class ImportBatch
{
    public int Id { get; set; }
    public int? BankAccountId { get; set; }
    public BankAccount? BankAccount { get; set; }
    public required string SourceFileName { get; set; }
    public required string DetectedBank { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public int TransactionCount { get; set; }
    public int DuplicateCount { get; set; }
    public ImportStatus Status { get; set; }
    public string? ErrorMessage { get; set; }

    public List<Transaction> Transactions { get; set; } = [];
}
