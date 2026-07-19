using FinFlow.Parsing;
using Xunit;

namespace FinFlow.Tests;

/// <summary>
/// Trade Republic transaction export.
/// Expected header:
/// datetime,date,account_type,category,type,asset_class,name,symbol,shares,price,
/// amount,fee,tax,currency,original_amount,original_currency,fx_rate,description,
/// transaction_id,counterparty_name,counterparty_iban,payment_reference,mcc_code
/// </summary>
public class TradeRepublicCsvParserTests : CsvParserTestBase
{
    private readonly TradeRepublicCsvParser _parser = new();

    private const string Header =
        "datetime,date,account_type,category,type,asset_class,name,symbol,shares,price," +
        "amount,fee,tax,currency,original_amount,original_currency,fx_rate,description," +
        "transaction_id,counterparty_name,counterparty_iban,payment_reference,mcc_code";

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
    [InlineData("Buchungstag;Wert;Umsatzart;Begünstigter / Auftraggeber;Anzahl der Schecks")] // Postbank
    [InlineData("Bezeichnung Auftragskonto;Name Zahlungsbeteiligter;Gekennzeichneter Umsatz")] // Volksbank
    public void CanParse_OtherBankHeader_ReturnsFalse(string header)
    {
        string path = CreateTempCsv(header + "\n");
        Assert.False(_parser.CanParse(path));
    }

    [Fact]
    public void Parse_CashTransaction_MapsFieldsCorrectly()
    {
        string csv = Header + "\n" +
            "2025-03-01 10:00:00,2025-03-01,cash,Payment,cash_in,,,,,," +
            "500.00,0,0,EUR,500.00,EUR,1.0,Einzahlung," +
            "TX-001,Arbeitgeber AG,DE11ARBGEBER,,\n";
        string path = CreateTempCsv(csv);

        var txs = _parser.Parse(path);

        Assert.Single(txs);
        var tx = txs[0];
        Assert.Equal(new DateOnly(2025, 3, 1), tx.BookingDate);
        Assert.Equal(500.00m, tx.Amount);
        Assert.Equal("EUR", tx.Currency);
        Assert.Equal("Arbeitgeber AG", tx.CounterpartyName);
        Assert.Equal("DE11ARBGEBER", tx.CounterpartyIban);
        Assert.Equal("Einzahlung", tx.Purpose);
    }

    [Fact]
    public void Parse_StockTransaction_UsesAssetNameAsFallback()
    {
        // For stock purchases counterparty_name is empty → name (asset name) is used as fallback.
        string csv = Header + "\n" +
            "2025-03-15 09:30:00,2025-03-15,securities,Securities,buy,equity,Apple Inc,AAPL,2,175.50," +
            "-351.00,1,0,EUR,,-,,Kauf 2x AAPL," +
            "TX-002,,,\n";
        string path = CreateTempCsv(csv);

        var txs = _parser.Parse(path);

        Assert.Single(txs);
        Assert.Equal("Apple Inc", txs[0].CounterpartyName);
        Assert.Equal(-351.00m, txs[0].Amount);
    }
}
