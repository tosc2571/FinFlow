namespace FinFlow.Api.Entities;

public class ClassificationRule
{
    public int Id { get; set; }
    public required string Pattern { get; set; }
    /// <summary>Null only when Status is InternalTransfer — a transfer rule needs no category,
    /// matching how the IBAN-based detection already forces the transaction's CategoryId to null.</summary>
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public ClassificationRuleStatus Status { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
