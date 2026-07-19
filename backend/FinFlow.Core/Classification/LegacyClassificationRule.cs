using System.Text.Json.Serialization;

namespace FinFlow.Classification;

/// <summary>
/// The CLI's JSON-file rule shape (pattern/category/status), loaded from rules.json.
/// Named "Legacy" because the Phase 2 data model introduces a persisted `ClassificationRule`
/// entity in FinFlow.Api with the same conceptual role but a different (DB-backed) shape.
/// </summary>
public sealed class LegacyClassificationRule
{
    [JsonPropertyName("pattern")]
    public required string Pattern { get; init; }

    [JsonPropertyName("category")]
    public required string Category { get; init; }

    /// <summary>"auto" | "prüfen" | "ignorieren"</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }
}
