using FinFlow.Parsing;
using Xunit;

namespace FinFlow.Tests;

/// <summary>
/// ING DiBa checking account.
/// Expected header: Buchung;Valuta;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Betrag;Währung
/// (Metadata rows precede it — skipped automatically.)
/// </summary>
public class IngCsvParserTests : CsvParserTestBase
{
    private readonly IngCsvParser _parser = new();

    private const string Header =
        "Buchung;Valuta;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Betrag;Währung";

    [Fact]
    public void CanParse_ValidHeader_ReturnsTrue()
    {
        string path = CreateTempCsv(Header + "\n");
        Assert.True(_parser.CanParse(path));
    }

    [Fact]
    public void CanParse_HeaderWithLeadingMetadata_ReturnsTrue()
    {
        // ING exports have metadata rows before the actual header.
        string csv = "Umsatzanzeige\n\nKunde: Max Mustermann\nKonto: DE12 3456 7890\n\n" + Header + "\n";
        string path = CreateTempCsv(csv);
        Assert.True(_parser.CanParse(path));
    }

    [Theory]
    [InlineData("Kontonummer;Buchungsdatum;Valuta;Empfaenger 1;Betrag;Waehrung")]            // HVB
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
            "01.03.2025;01.03.2025;REWE;Lastschrift;Einkauf;-38,90;EUR\n";
        string path = CreateTempCsv(csv);

        var txs = _parser.Parse(path);

        Assert.Single(txs);
        var tx = txs[0];
        Assert.Equal(new DateOnly(2025, 3, 1), tx.BookingDate);
        Assert.Equal(-38.90m, tx.Amount);
        Assert.Equal("EUR", tx.Currency);
        Assert.Equal("REWE", tx.CounterpartyName);
        Assert.Equal("Lastschrift", tx.BookingType);
        Assert.Equal("Einkauf", tx.Purpose);
    }

    [Fact]
    public void Parse_WithSaldo_MapsSaldoCorrectly()
    {
        // Some ING exports have a balance ("Saldo") column.
        string header = "Buchung;Valuta;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung";
        string csv = header + "\n" +
            "05.03.2025;05.03.2025;Amazon;Online;Bestellung;;EUR;-79,99;EUR\n";
        string path = CreateTempCsv(csv);

        var txs = _parser.Parse(path);
        Assert.Single(txs);
        Assert.Equal(-79.99m, txs[0].Amount);
    }

    [Fact]
    public void Parse_WertstellungsdatumHeaderVariant_MapsValueDate()
    {
        // Newer ING exports name the value-date column "Wertstellungsdatum".
        string header = "Buchung;Wertstellungsdatum;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung";
        string csv = header + "\n" +
            "10.03.2025;11.03.2025;Telekom;Lastschrift;Rechnung;;EUR;-29,99;EUR\n";
        string path = CreateTempCsv(csv);

        var txs = _parser.Parse(path);
        Assert.Single(txs);
        Assert.Equal(new DateOnly(2025, 3, 11), txs[0].ValueDate);
    }
}
