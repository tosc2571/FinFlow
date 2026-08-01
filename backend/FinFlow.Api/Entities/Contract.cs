namespace FinFlow.Api.Entities;

/// <summary>
/// A recurring payment (rent, insurance, subscription, salary, ...). Income and expenses are
/// both contracts, distinguished by the sign of NominalAmount — same convention as
/// Transaction.Amount (negative = outgoing, positive = incoming).
/// </summary>
public class Contract
{
    public int Id { get; set; }
    public required string Name { get; set; }

    /// <summary>Amount at contract creation time — the fallback expected amount until real
    /// matched payments provide a rolling average (see ContractService.GetExpectedAmount).</summary>
    public decimal NominalAmount { get; set; }

    public ContractPeriod Period { get; set; }

    /// <summary>Due date of the source payment; later occurrences are anchor + n * period.</summary>
    public DateOnly AnchorDate { get; set; }

    /// <summary>Regex matched against counterparty + purpose, same convention as
    /// ClassificationRule.Pattern. Derived from the source transaction on creation
    /// (Regex.Escape'd, so it starts as an exact substring match) and user-editable after.</summary>
    public required string CounterpartyPattern { get; set; }

    /// <summary>Fraction of the expected amount a matched payment may deviate by, e.g. 0.05 =
    /// 5%. Income contracts default higher than expense contracts since salaries fluctuate
    /// (bonuses, overtime) while most expense contracts are fixed.</summary>
    public decimal AmountTolerance { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public bool IsActive { get; set; } = true;

    public int? SourceTransactionId { get; set; }
    public Transaction? SourceTransaction { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Transaction> Transactions { get; set; } = [];
}
