using FinFlow.Parsing;
using Xunit;

namespace FinFlow.Tests;

/// <summary>
/// Berliner Volksbank.
/// Expected header:
/// Bezeichnung Auftragskonto;IBAN Auftragskonto;BIC Auftragskonto;Bankname Auftragskonto;Buchungstag;
/// Valutadatum;Name Zahlungsbeteiligter;IBAN Zahlungsbeteiligter;BIC (SWIFT-Code) Zahlungsbeteiligter;
/// Buchungstext;Verwendungszweck;Betrag;Waehrung;Saldo nach Buchung;Bemerkung;Gekennzeichneter Umsatz;
/// Glaeubiger ID;Mandatsreferenz
/// </summary>
public class VolksbankCsvParserTests : CsvParserTestBase
{
    private readonly VolksbankCsvParser _parser = new();

    private const string Header =
        "Bezeichnung Auftragskonto;IBAN Auftragskonto;BIC Auftragskonto;Bankname Auftragskonto;Buchungstag;" +
        "Valutadatum;Name Zahlungsbeteiligter;IBAN Zahlungsbeteiligter;BIC (SWIFT-Code) Zahlungsbeteiligter;" +
        "Buchungstext;Verwendungszweck;Betrag;Waehrung;Saldo nach Buchung;Bemerkung;Gekennzeichneter Umsatz;" +
        "Glaeubiger ID;Mandatsreferenz";

    [Fact]
    public void CanParse_ValidHeader_ReturnsTrue()
    {
        string path = CreateTempCsv(Header + "\n");
        Assert.True(_parser.CanParse(path));
    }

    [Theory]
    [InlineData("Kontonummer;Buchungsdatum;Valuta;Empfaenger 1;Betrag;Waehrung")]            // HVB
    [InlineData("Buchung;Auftraggeber/Empfänger;Verwendungszweck;Betrag;Währung")]          // ING
    [InlineData("Buchungsdatum;Wertstellung;Status;Zahlungsempfänger*in;Umsatztyp")]        // DKB checking
    [InlineData("Kartennummer;Belegdatum;Eingangstag;Text/Verwendungszweck;Betrag")]         // DKB credit card
    [InlineData("datetime,date,transaction_id,asset_class,mcc_code")]                        // Trade Republic
    [InlineData("Buchungstag;Wert;Umsatzart;Begünstigter / Auftraggeber;Anzahl der Schecks")] // Postbank
    public void CanParse_OtherBankHeader_ReturnsFalse(string header)
    {
        string path = CreateTempCsv(header + "\n");
        Assert.False(_parser.CanParse(path));
    }

    [Fact]
    public void Parse_OneRow_MapsFieldsCorrectly()
    {
        string row = string.Join(";",
            "Girokonto", "DE11500105175400794430", "BEVODEBB", "Berliner Volksbank",
            "01.03.2025", "01.03.2025", "REWE SAGT DANKE", "DE99999999999999", "GENODEF1XXX",
            "Kartenzahlung", "Einkauf", "-45,67", "EUR", "1234,56", "", "", "", "");
        string path = CreateTempCsv(Header + "\n" + row + "\n");

        var txs = _parser.Parse(path);

        Assert.Single(txs);
        var tx = txs[0];
        Assert.Equal(new DateOnly(2025, 3, 1), tx.BookingDate);
        Assert.Equal(new DateOnly(2025, 3, 1), tx.ValueDate);
        Assert.Equal(-45.67m, tx.Amount);
        Assert.Equal("EUR", tx.Currency);
        Assert.Equal("REWE SAGT DANKE", tx.CounterpartyName);
        Assert.Equal("DE99999999999999", tx.CounterpartyIban);
        Assert.Equal("GENODEF1XXX", tx.CounterpartyBic);
        Assert.Equal("Einkauf", tx.Purpose);
        Assert.Equal("Kartenzahlung", tx.BookingType);
        Assert.Equal(1234.56m, tx.Balance);
    }
}
