using CsvHelper;

namespace FinFlow.Parsing;

/// <summary>
/// Berliner Volksbank (this export format likely applies to other Genossenschaftsbanken too).
/// Verified header:
/// Bezeichnung Auftragskonto;IBAN Auftragskonto;BIC Auftragskonto;Bankname Auftragskonto;Buchungstag;
/// Valutadatum;Name Zahlungsbeteiligter;IBAN Zahlungsbeteiligter;BIC (SWIFT-Code) Zahlungsbeteiligter;
/// Buchungstext;Verwendungszweck;Betrag;Waehrung;Saldo nach Buchung;Bemerkung;Gekennzeichneter Umsatz;
/// Glaeubiger ID;Mandatsreferenz
/// </summary>
public sealed class VolksbankCsvParser : BankCsvParserBase
{
    public override string BankName => "volksbank";

    protected override string[] HeaderSignature =>
        new[] { "Auftragskonto", "Zahlungsbeteiligter", "Gekennzeichneter Umsatz" };

    protected override LegacyTransaction? MapRow(CsvReader csv) => new LegacyTransaction
    {
        SourceBank = BankName,
        BookingDate = ParseDate(Field(csv, "Buchungstag")),
        ValueDate = ParseDate(Field(csv, "Valutadatum")),
        Amount = ParseAmount(Field(csv, "Betrag")),
        Currency = Field(csv, "Waehrung") ?? "EUR",
        CounterpartyName = Field(csv, "Name Zahlungsbeteiligter"),
        CounterpartyIban = Field(csv, "IBAN Zahlungsbeteiligter"),
        CounterpartyBic = Field(csv, "BIC (SWIFT-Code) Zahlungsbeteiligter"),
        Purpose = Field(csv, "Verwendungszweck"),
        BookingType = Field(csv, "Buchungstext"),
        Balance = Field(csv, "Saldo nach Buchung") is { } s ? ParseAmount(s) : null,
    };
}
