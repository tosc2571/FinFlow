using CsvHelper;

namespace FinFlow.Parsing;

/// <summary>
/// DKB checking account, new web portal (since 2023). Verified header:
/// "Buchungsdatum";"Wertstellung";"Status";"Zahlungspflichtige*r";"Zahlungsempfänger*in";
/// "Verwendungszweck";"Umsatztyp";"IBAN";"Betrag (€)";"Gläubiger-ID";"Mandatsreferenz";"Kundenreferenz"
/// Delimiter ; — a few metadata rows sit above the table (skipped automatically).
/// </summary>
public sealed class DkbCsvParser : BankCsvParserBase
{
    public override string BankName => "dkb";

    protected override string[] HeaderSignature =>
        new[] { "Buchungsdatum", "Zahlungsempfänger", "Umsatztyp" };

    protected override LegacyTransaction? MapRow(CsvReader csv) => new LegacyTransaction
    {
        SourceBank = BankName,
        BookingDate = ParseDate(Field(csv, "Buchungsdatum", "Buchungstag")),
        ValueDate = ParseDate(Field(csv, "Wertstellung")),
        Amount = ParseAmount(Field(csv, "Betrag (€)", "Betrag(€)", "Betrag (EUR)", "Betrag")),
        Currency = "EUR",
        // For incoming payments the counterparty is under "Zahlungspflichtige*r", for outgoing under "Zahlungsempfänger*in".
        CounterpartyName = Field(csv, "Zahlungsempfänger*in", "Zahlungspflichtige*r"),
        CounterpartyIban = Field(csv, "IBAN"),
        Purpose = Field(csv, "Verwendungszweck"),
        BookingType = Field(csv, "Umsatztyp", "Buchungstext"),
    };
}
