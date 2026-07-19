using FinFlow.Parsing;
using Xunit;

namespace FinFlow.Tests;

/// <summary>
/// DKB credit card.
/// Expected header: Kartennummer;Zeitraum;Belegdatum;Eingangstag;Text/Verwendungszweck;Kurs;Betrag;Waehrung
/// </summary>
public class DkbKreditkarteCsvParserTests : CsvParserTestBase
{
    private readonly DkbKreditkarteCsvParser _parser = new();

    private const string Header =
        "Kartennummer;Zeitraum;Belegdatum;Eingangstag;Text/Verwendungszweck;Kurs;Betrag;Waehrung";

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
            "4111XXXXXXXX1234;01.01.2025-31.03.2025;28.02.2025;01.03.2025;Rewe Sagt Danke;1,00;-56,78;EUR\n";
        string path = CreateTempCsv(csv);

        var txs = _parser.Parse(path);

        Assert.Single(txs);
        var tx = txs[0];
        Assert.Equal(new DateOnly(2025, 3, 1), tx.BookingDate);
        Assert.Equal(new DateOnly(2025, 2, 28), tx.ValueDate);
        Assert.Equal(-56.78m, tx.Amount);
        Assert.Equal("EUR", tx.Currency);
        Assert.Equal("Rewe Sagt Danke", tx.CounterpartyName);
        Assert.Equal("Kreditkarte", tx.BookingType);
    }

    [Fact]
    public void Parse_EmptyBetrag_SkipsRow()
    {
        string csv = Header + "\n" +
            "4111XXXXXXXX1234;01.01.2025-31.03.2025;28.02.2025;01.03.2025;Dummy;;;\n";
        string path = CreateTempCsv(csv);

        var txs = _parser.Parse(path);
        Assert.Empty(txs);
    }
}
