namespace FinFlow.Api.Entities;

public class ClassificationRule
{
    public int Id { get; set; }
    public required string Pattern { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public ClassificationRuleStatus Status { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
