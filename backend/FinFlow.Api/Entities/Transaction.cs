using System.Security.Cryptography;
using System.Text;

namespace FinFlow.Api.Entities;

public class Transaction
{
    public int Id { get; set; }
    public int ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public int? BankAccountId { get; set; }
    public BankAccount? BankAccount { get; set; }

    public required string SourceBank { get; set; }
    public DateOnly? BookingDate { get; set; }
    public DateOnly? ValueDate { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? CounterpartyName { get; set; }
    public string? CounterpartyIban { get; set; }
    public string? CounterpartyBic { get; set; }
    public string? Purpose { get; set; }
    public string? BookingType { get; set; }
    public decimal? Balance { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public ClassificationStatus ClassificationStatus { get; set; } = ClassificationStatus.NeedsReview;

    /// <summary>
    /// Hash of bank + booking date + amount + counterparty + purpose, unique per transaction.
    /// Re-importing an overlapping date range must not double-count — see AppDbContext's
    /// unique index on this column.
    /// </summary>
    public required string DedupeHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static string ComputeDedupeHash(string sourceBank, DateOnly? bookingDate, decimal amount, string? counterpartyName, string? purpose)
    {
        string raw = $"{sourceBank}|{bookingDate}|{amount}|{counterpartyName}|{purpose}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }
}
