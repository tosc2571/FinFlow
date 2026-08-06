using FinFlow.Parsing;
using Xunit;

namespace FinFlow.Tests;

/// <summary>
/// HypoVereinsbank (UniCredit). Handles both the old header (with counterparty columns) and the
/// new one HVB switched to, which dropped them:
/// Old: Kontonummer;Buchungsdatum;Valuta;Empfaenger 1;Empfaenger 2;Verwendungszweck;Betrag;Waehrung
/// New: Kontonummer;Buchungsdatum;Valuta;Verwendungszweck;Betrag;Waehrung
/// </summary>
public class HvbCsvParserTests : CsvParserTestBase
{
    private readonly HvbCsvParser _parser = new();

    private const string Header =
        "Kontonummer;Buchungsdatum;Valuta;Empfaenger 1;Empfaenger 2;Verwendungszweck;Betrag;Waehrung";

    private const string HeaderNoCounterparty =
        "Kontonummer;Buchungsdatum;Valuta;Verwendungszweck;Betrag;Waehrung";

    [Fact]
    public void CanParse_ValidHeader_ReturnsTrue()
    {
        string path = CreateTempCsv(Header + "\n");
        Assert.True(_parser.CanParse(path));
    }

    [Fact]
    public void CanParse_NewHeaderWithoutCounterpartyColumns_ReturnsTrue()
    {
        string path = CreateTempCsv(HeaderNoCounterparty + "\n");
        Assert.True(_parser.CanParse(path));
    }

    [Theory]
    [InlineData("Buchung;Auftraggeber/Empfänger;Verwendungszweck;Betrag;Währung")]          // ING
    [InlineData("Buchungsdatum;Wertstellung;Status;Zahlungsempfänger*in;Umsatztyp")]        // DKB checking
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
    public void Parse_OneRow_MapsFieldsCorrectly()
    {
        string csv = Header + "\n" +
            "DE12345678;01.03.2025;01.03.2025;Max Mustermann;DE99TESTIBAN;Miete März;-1200,00;EUR\n";
        string path = CreateTempCsv(csv);

        var txs = _parser.Parse(path);

        Assert.Single(txs);
        var tx = txs[0];
        Assert.Equal(new DateOnly(2025, 3, 1), tx.BookingDate);
        Assert.Equal(new DateOnly(2025, 3, 1), tx.ValueDate);
        Assert.Equal(-1200.00m, tx.Amount);
        Assert.Equal("EUR", tx.Currency);
        Assert.Equal("Max Mustermann", tx.CounterpartyName);
        Assert.Equal("DE99TESTIBAN", tx.CounterpartyIban);
        Assert.Equal("Miete März", tx.Purpose);
    }

    [Fact]
    public void Parse_NewHeaderOneRow_MapsFieldsCorrectlyWithNullCounterparty()
    {
        string csv = HeaderNoCounterparty + "\n" +
            "DE12345678;01.03.2025;01.03.2025;Miete März;-1200,00;EUR\n";
        string path = CreateTempCsv(csv);

        var txs = _parser.Parse(path);

        Assert.Single(txs);
        var tx = txs[0];
        Assert.Equal(new DateOnly(2025, 3, 1), tx.BookingDate);
        Assert.Equal(new DateOnly(2025, 3, 1), tx.ValueDate);
        Assert.Equal(-1200.00m, tx.Amount);
        Assert.Equal("EUR", tx.Currency);
        Assert.Null(tx.CounterpartyName);
        Assert.Null(tx.CounterpartyIban);
        Assert.Equal("Miete März", tx.Purpose);
    }
}
