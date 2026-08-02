namespace FinFlow.Api.Entities;

/// <summary>
/// One of the user's own accounts, registered so TransferDetectionService can recognize money
/// moved between them (via CounterpartyIban) as an internal transfer rather than real income or
/// spending — see ClassificationStatus.InternalTransfer. Not otherwise linked to Transaction
/// today (Transaction.BankAccountId exists but nothing populates it); only Iban matters here.
/// </summary>
public class BankAccount
{
    public int Id { get; set; }
    public required string BankName { get; set; }
    public string? DisplayName { get; set; }
    public required string Iban { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Transaction> Transactions { get; set; } = [];
}
