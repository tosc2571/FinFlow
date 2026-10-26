namespace FinFlow.Classification;

/// <summary>
/// TopCategory is Category's true root ancestor's name (walking the parent chain all the way up,
/// however deep the nesting goes — see #87), or Category itself if it has no parent. XlsxExporter
/// groups sheets by TopCategory and sections within a sheet by Category, ordering both (and
/// Overview's rows) by the matching *Priority field first, then alphabetically.
/// </summary>
public sealed record ClassifiedTransaction(
    LegacyTransaction Transaction,
    string Category,
    string TopCategory,
    string Status, // "auto" | "review" | "ignored"
    int CategoryPriority = 0,
    int TopCategoryPriority = 0
);
