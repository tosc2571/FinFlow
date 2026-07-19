using FinFlow.Parsing;
using Xunit;

namespace FinFlow.Tests;

/// <summary>
/// DKB checking account (new web portal since 2023).
/// Expected header:
/// "Buchungsdatum";"Wertstellung";"Status";"Zahlungspflichtige*r";"Zahlungsempfänger*in";
/// "Verwendungszweck";"Umsatztyp";"IBAN";"Betrag (€)";"Gläubiger-ID";"Mandatsreferenz";"Kundenreferenz"
/// </summary>
public class DkbCsvParserTests : CsvParserTestBase
{
    private readonly DkbCsvParser _parser = new();

    private const string Header =
        "\"Buchungsdatum\";\"Wertstellung\";\"Status\";\"Zahlungspflichtige*r\";\"Zahlungsempfänger*in\";" +
        "\"Verwendungszweck\";\"Umsatztyp\";\"IBAN\";\"Betrag (€)\";\"Gläubiger-ID\";\"Mandatsreferenz\";\"Kundenreferenz\"";

    [Fact]
    public void CanParse_ValidHeader_ReturnsTrue()
    {
        string path = CreateTempCsv(Header + "\n");
        Assert.True(_parser.CanParse(path));
    }

    [Theory]
    [InlineData("Kontonummer;Buchungsdatum;Valuta;Empfaenger 1;Betrag;Waehrung")]            // HVB
    [InlineData("Buchung;Auftraggeber/Empfänger;Verwendungszweck;Betrag;Währung")]          // ING
    [InlineData("Kartennummer;Belegdatum;Eingangstag;Text/Verwendungszweck;Betrag")]         // DKB credit card
    [InlineData("datetime,date,transaction_id,asset_class,mcc_code")]                        // Trade Republic
    [InlineData("Buchungstag;Wert;Umsatzart;Begünstigter / Auftraggeber;Anzahl der Schecks")] // Postbank
    [InlineData("Bezeichnung Auftragskonto;Name Zahlungsbeteiligter;Gekennzeichneter Umsatz")] // Volksbank
    public void CanParse_OtherBankHeader_ReturnsFalse(string header)
    {
        string path = CreateTempCsv(header + "\n");
        Assert.False(_parser.CanParse(path));
    }

    [Fact]
    public void Parse_Ausgang_MapsZahlungsempfaengerAsCounterparty()
    {
        string csv = Header + "\n" +
            "\"01.03.2025\";\"01.03.2025\";\"Gebucht\";\"\";\"Landlord GmbH\";" +
            "\"Miete März 2025\";\"Lastschrift\";\"DE11111100000000\";\"-1400,00\";\"\";\"\";\"\"";
        string path = CreateTempCsv(csv);

        var txs = _parser.Parse(path);

        Assert.Single(txs);
        var tx = txs[0];
        Assert.Equal(new DateOnly(2025, 3, 1), tx.BookingDate);
        Assert.Equal("Landlord GmbH", tx.CounterpartyName);
        Assert.Equal("Miete März 2025", tx.Purpose);
        Assert.Equal("Lastschrift", tx.BookingType);
    }

    [Fact]
    public void Parse_Eingang_MapsZahlungspflichtigerAsCounterparty()
    {
        string csv = Header + "\n" +
            "\"15.03.2025\";\"15.03.2025\";\"Gebucht\";\"Arbeitgeber AG\";\"\";" +
            "\"Gehalt März\";\"Gutschrift\";\"DE99ARBGEBER\";\"3000,00\";\"\";\"\";\"\"";
        string path = CreateTempCsv(csv);

        var txs = _parser.Parse(path);

        Assert.Single(txs);
        Assert.Equal("Arbeitgeber AG", txs[0].CounterpartyName);
        Assert.Equal(3000.00m, txs[0].Amount);
    }
}
