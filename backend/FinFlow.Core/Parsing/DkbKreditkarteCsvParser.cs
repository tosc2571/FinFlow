using CsvHelper;

namespace FinFlow.Parsing;

/// <summary>
/// DKB credit card. Verified header:
/// Kartennummer;Zeitraum;Belegdatum;Eingangstag;Text/Verwendungszweck;Kurs;Betrag;Waehrung
/// </summary>
public sealed class DkbKreditkarteCsvParser : BankCsvParserBase
{
    public override string BankName => "dkb";

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
