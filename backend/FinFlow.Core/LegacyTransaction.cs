namespace FinFlow;

/// <summary>
/// The bank-agnostic, in-memory shape a parser produces straight from a CSV row. Named "Legacy"
/// because the Phase 2 data model introduced a persisted `Transaction` entity in FinFlow.Api with
/// the same conceptual role but a different (DB-backed) shape — this type is now only the
/// intermediate result `ImportService` maps into that entity, and the shape it's mapped back into
/// for XLSX/CSV export.
/// </summary>
public sealed class LegacyTransaction
{
    /// <summary>Which parser/bank produced this booking (e.g. "dkb").</summary>
    public required string SourceBank { get; init; }

    public DateOnly? BookingDate { get; init; }
    public DateOnly? ValueDate { get; init; }

    public decimal Amount { get; init; } // negative = outgoing, positive = incoming
    public string Currency { get; init; } = "EUR";

    public string? CounterpartyName { get; init; }
    public string? CounterpartyIban { get; init; }
    public string? CounterpartyBic { get; init; }

    public string? Purpose { get; init; }
    public string? BookingType { get; init; }
    public decimal? Balance { get; init; }
}
