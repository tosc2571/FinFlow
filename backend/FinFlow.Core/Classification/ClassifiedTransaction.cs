namespace FinFlow.Classification;

/// <summary>
/// TopCategory is Category's parent category name if it has one, else Category itself —
/// category hierarchy is capped at two levels (enforced in CategoryEndpoints), so this is a
/// direct lookup, never a multi-level walk. XlsxExporter groups sheets by TopCategory and
/// sections within a sheet by Category.
/// </summary>
public sealed record ClassifiedTransaction(
    LegacyTransaction Transaction,
    string Category,
    string TopCategory,
    string Status // "auto" | "prüfen" | "ignorieren"
);
