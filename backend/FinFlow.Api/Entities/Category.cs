namespace FinFlow.Api.Entities;

public class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public bool IsIncome { get; set; }
    public int SortOrder { get; set; }

    public List<Category> Children { get; set; } = [];
    public List<Transaction> Transactions { get; set; } = [];
    public List<ClassificationRule> ClassificationRules { get; set; } = [];
}
