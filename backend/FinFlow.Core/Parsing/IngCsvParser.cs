using CsvHelper;

namespace FinFlow.Parsing;

/// <summary>
/// ING (DiBa) checking/extra account, "Umsatzanzeige_&lt;IBAN&gt;_&lt;date&gt;.csv". Verified header:
/// Buchung;Valuta;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;[Saldo;Währung;]Betrag;Währung
/// Newer exports name the value-date column "Wertstellungsdatum" instead of "Valuta" — both are accepted.
/// Delimiter ; — metadata rows (customer, account, period, balance) sit above and are skipped
/// automatically. Encoding is usually ISO-8859-1 (auto-detected).
/// ING's CSV only gives the counterparty's name, no IBAN/BIC.
/// </summary>
public sealed class IngCsvParser : BankCsvParserBase
{
    public override string BankName => "ing";

    protected override string[] HeaderSignature =>
        new[] { "Buchung", "Auftraggeber/Empfänger", "Verwendungszweck" };

    protected override LegacyTransaction? MapRow(CsvReader csv) => new LegacyTransaction
    {
        SourceBank = BankName,
        BookingDate = ParseDate(Field(csv, "Buchung")),
        ValueDate = ParseDate(Field(csv, "Valuta", "Wertstellungsdatum")),
        Amount = ParseAmount(Field(csv, "Betrag")),
        Currency = Field(csv, "Währung") ?? "EUR",
        CounterpartyName = Field(csv, "Auftraggeber/Empfänger"),
        Purpose = Field(csv, "Verwendungszweck"),
        BookingType = Field(csv, "Buchungstext"),
        Balance = Field(csv, "Saldo") is { } s ? ParseAmount(s) : null,
    };
}
