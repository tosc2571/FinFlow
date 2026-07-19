namespace FinFlow.Classification;

public sealed record ClassifiedTransaction(
    LegacyTransaction Transaction,
    string Category,
    string Status // "auto" | "prüfen" | "ignorieren"
);
