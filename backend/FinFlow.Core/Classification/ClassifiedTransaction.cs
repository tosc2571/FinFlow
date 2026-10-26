namespace FinFlow.Classification;

/// <summary>
/// TopCategory is Category's parent category name if it has one, else Category itself —
/// category hierarchy is capped at two levels (enforced in CategoryEndpoints), so this is a
/// direct lookup, never a multi-level walk. XlsxExporter groups sheets by TopCategory and
/// sections within a sheet by Category, ordering both (and Overview's rows) by the matching
/// *Priority field first, then alphabetically.
/// </summary>
public sealed record ClassifiedTransaction(
    LegacyTransaction Transaction,
    string Category,
    string TopCategory,
    string Status, // "auto" | "review" | "ignored"
    int CategoryPriority = 0,
    int TopCategoryPriority = 0
);
