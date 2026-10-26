namespace FinFlow.Api.Entities;

public class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public bool IsIncome { get; set; }

    /// <summary>Controls export ordering (XlsxExporter) only — lower value sorts first, ties break
    /// alphabetically by name. Does not affect in-app ordering (category tree/dropdowns are
    /// always alphabetical, see #56).</summary>
    public int Priority { get; set; }

    public List<Category> Children { get; set; } = [];
    public List<Transaction> Transactions { get; set; } = [];
    public List<ClassificationRule> ClassificationRules { get; set; } = [];
}
