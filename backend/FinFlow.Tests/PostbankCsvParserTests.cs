using FinFlow.Parsing;
using Xunit;

namespace FinFlow.Tests;

/// <summary>
/// Postbank Berlin checking account.
/// Expected header:
/// Buchungstag;Wert;Umsatzart;Begünstigter / Auftraggeber;Verwendungszweck;IBAN / Kontonummer;BIC;
/// Kundenreferenz;Mandatsreferenz;Gläubiger ID;Fremde Gebühren;Betrag;Abweichender Empfänger;
/// Anzahl der Aufträge;Anzahl der Schecks;Soll;Haben;Währung
/// </summary>
public class PostbankCsvParserTests : CsvParserTestBase
{
    private readonly PostbankCsvParser _parser = new();

    private const string Header =
        "Buchungstag;Wert;Umsatzart;Begünstigter / Auftraggeber;Verwendungszweck;IBAN / Kontonummer;BIC;" +
        "Kundenreferenz;Mandatsreferenz;Gläubiger ID;Fremde Gebühren;Betrag;Abweichender Empfänger;" +
        "Anzahl der Aufträge;Anzahl der Schecks;Soll;Haben;Währung";

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
    [InlineData("Bezeichnung Auftragskonto;Name Zahlungsbeteiligter;Gekennzeichneter Umsatz")] // Volksbank
    public void CanParse_OtherBankHeader_ReturnsFalse(string header)
    {
        string path = CreateTempCsv(header + "\n");
        Assert.False(_parser.CanParse(path));
    }

    [Fact]
    public void Parse_OneRow_MapsFieldsCorrectly()
    {
        string row = string.Join(";",
            "01.03.2025", "01.03.2025", "Lastschrift", "REWE SAGT DANKE", "Einkauf",
            "DE02100100100006820101", "PBNKDEFF", "", "", "", "", "-45,67", "", "", "", "", "", "EUR");
        string path = CreateTempCsv(Header + "\n" + row + "\n");

        var txs = _parser.Parse(path);

        Assert.Single(txs);
        var tx = txs[0];
        Assert.Equal(new DateOnly(2025, 3, 1), tx.BookingDate);
        Assert.Equal(new DateOnly(2025, 3, 1), tx.ValueDate);
        Assert.Equal(-45.67m, tx.Amount);
        Assert.Equal("EUR", tx.Currency);
        Assert.Equal("REWE SAGT DANKE", tx.CounterpartyName);
        Assert.Equal("DE02100100100006820101", tx.CounterpartyIban);
        Assert.Equal("PBNKDEFF", tx.CounterpartyBic);
        Assert.Equal("Einkauf", tx.Purpose);
        Assert.Equal("Lastschrift", tx.BookingType);
    }

    [Fact]
    public void Parse_LeeresBetrag_FaelltAufHabenZurueck()
    {
        // If "Betrag" is ever empty, fall back to Soll/Haben.
        string row = string.Join(";",
            "05.03.2025", "05.03.2025", "Gutschrift", "Arbeitgeber AG", "Gehalt",
            "DE99ARBGEBER", "PBNKDEFF", "", "", "", "", "", "", "", "", "", "3000,00", "EUR");
        string path = CreateTempCsv(Header + "\n" + row + "\n");

        var txs = _parser.Parse(path);
        Assert.Single(txs);
        Assert.Equal(3000.00m, txs[0].Amount);
    }
}
