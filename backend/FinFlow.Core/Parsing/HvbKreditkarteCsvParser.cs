using CsvHelper;

namespace FinFlow.Parsing;

/// <summary>
/// HypoVereinsbank (UniCredit) credit card. Verified header:
/// Kartennummer;Zeitraum;Belegdatum;Eingangstag;Text/Verwendungszweck;Kurs;Betrag;Waehrung
///
/// This header is byte-identical to DkbKreditkarteCsvParser's (both banks' card statements
/// come out of the same processor format) — auto-detection (ParserRegistry.Detect, no hint)
/// therefore always resolves to whichever of the two is registered first and can't tell them
/// apart. Import an HVB card export via the "bank" dropdown/hint instead of relying on
/// auto-detect; DetectWithHint filters by BankName first, which does disambiguate correctly.
/// </summary>
public sealed class HvbKreditkarteCsvParser : BankCsvParserBase
{
    public override string BankName => "hvb";

    protected override string[] HeaderSignature =>
        new[] { "Kartennummer", "Belegdatum", "Eingangstag" };

    protected override LegacyTransaction? MapRow(CsvReader csv)
    {
        string? betrag = Field(csv, "Betrag");
        if (string.IsNullOrWhiteSpace(betrag)) return null;

        return new LegacyTransaction
        {
            SourceBank       = BankName,
            BookingDate      = ParseDate(Field(csv, "Eingangstag")),
            ValueDate        = ParseDate(Field(csv, "Belegdatum")),
            Amount           = ParseAmount(betrag),
            Currency         = Field(csv, "Waehrung") ?? "EUR",
            CounterpartyName = Field(csv, "Text/Verwendungszweck"),
            Purpose          = Field(csv, "Text/Verwendungszweck"),
            BookingType      = "Kreditkarte",
        };
    }
}
