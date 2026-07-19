namespace FinFlow.Api.Entities;

public class BankAccount
{
    public int Id { get; set; }
    public required string BankName { get; set; }
    public string? DisplayName { get; set; }
    public string? Iban { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Transaction> Transactions { get; set; } = [];
}
