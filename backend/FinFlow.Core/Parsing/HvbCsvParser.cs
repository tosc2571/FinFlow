using CsvHelper;

namespace FinFlow.Parsing;

/// <summary>
/// HypoVereinsbank (UniCredit).
/// Old header: Kontonummer;Buchungsdatum;Valuta;Empfaenger 1;Empfaenger 2;Verwendungszweck;Betrag;Waehrung
/// New header (HVB dropped the counterparty columns): Kontonummer;Buchungsdatum;Valuta;Verwendungszweck;Betrag;Waehrung
/// Both are handled by one parser — the new header is a strict subset of the old one, so
/// CounterpartyName/CounterpartyIban just come out null for new-format rows (Field() already
/// tolerates a missing column; no MapRow change needed).
/// </summary>
public sealed class HvbCsvParser : BankCsvParserBase
{
    public override string BankName => "hvb";

    protected override string[] HeaderSignature =>
        new[] { "Kontonummer", "Buchungsdatum", "Valuta" };

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
