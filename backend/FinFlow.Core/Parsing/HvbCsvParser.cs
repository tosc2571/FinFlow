using CsvHelper;

namespace FinFlow.Parsing;

/// <summary>
/// HypoVereinsbank (UniCredit).
/// Header: Kontonummer;Buchungsdatum;Valuta;Empfaenger 1;Empfaenger 2;Verwendungszweck;Betrag;Waehrung
/// </summary>
public sealed class HvbCsvParser : BankCsvParserBase
{
    public override string BankName => "hvb";

    protected override string[] HeaderSignature =>
        new[] { "Kontonummer", "Buchungsdatum", "Empfaenger 1" };

    protected override LegacyTransaction? MapRow(CsvReader csv) => new LegacyTransaction
    {
        SourceBank = BankName,
        BookingDate = ParseDate(Field(csv, "Buchungsdatum")),
        ValueDate = ParseDate(Field(csv, "Valuta")),
        Amount = ParseAmount(Field(csv, "Betrag")),
        Currency = Field(csv, "Waehrung") ?? "EUR",
        CounterpartyName = Field(csv, "Empfaenger 1"),
        CounterpartyIban = Field(csv, "Empfaenger 2"),
        Purpose = Field(csv, "Verwendungszweck"),
    };
}
