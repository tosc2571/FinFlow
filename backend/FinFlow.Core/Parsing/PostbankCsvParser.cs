using CsvHelper;

namespace FinFlow.Parsing;

/// <summary>
/// Postbank Berlin checking account. Verified header:
/// Buchungstag;Wert;Umsatzart;Begünstigter / Auftraggeber;Verwendungszweck;IBAN / Kontonummer;BIC;
/// Kundenreferenz;Mandatsreferenz;Gläubiger ID;Fremde Gebühren;Betrag;Abweichender Empfänger;
/// Anzahl der Aufträge;Anzahl der Schecks;Soll;Haben;Währung
/// "Soll"/"Haben" are only a fallback in case "Betrag" is ever empty.
/// </summary>
public sealed class PostbankCsvParser : BankCsvParserBase
{
    public override string BankName => "postbank";

    protected override string[] HeaderSignature =>
        new[] { "Umsatzart", "Begünstigter / Auftraggeber", "Anzahl der Schecks" };

    protected override LegacyTransaction? MapRow(CsvReader csv) => new LegacyTransaction
    {
        SourceBank = BankName,
        BookingDate = ParseDate(Field(csv, "Buchungstag")),
        ValueDate = ParseDate(Field(csv, "Wert")),
        Amount = ParseAmount(Field(csv, "Betrag", "Soll", "Haben")),
        Currency = Field(csv, "Währung") ?? "EUR",
        CounterpartyName = Field(csv, "Begünstigter / Auftraggeber"),
        CounterpartyIban = Field(csv, "IBAN / Kontonummer"),
        CounterpartyBic = Field(csv, "BIC"),
        Purpose = Field(csv, "Verwendungszweck"),
        BookingType = Field(csv, "Umsatzart"),
    };
}
