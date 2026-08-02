using Xunit;

namespace FinFlow.Tests;

public class ParserRegistryTests : CsvParserTestBase
{
    private readonly ParserRegistry _registry = new();

    [Fact]
    public void Detect_UnambiguousHeader_ReturnsTheMatchingParser()
    {
        string path = CreateTempCsv(
            "Buchungsdatum;Wertstellung;Status;Zahlungspflichtige*r;Zahlungsempfänger*in;" +
            "Verwendungszweck;Umsatztyp;IBAN;Betrag (€);Gläubiger-ID;Mandatsreferenz;Kundenreferenz\n");

        Assert.Equal("dkb", _registry.Detect(path)?.BankName);
    }

    /// <summary>
    /// DKB's and HVB's credit card exports share a byte-identical header — silently picking
    /// whichever parser is registered first would guess wrong for the other bank about half the
    /// time. Detect must report this as "not recognized" (null) instead of a confident guess.
    /// </summary>
    [Fact]
    public void Detect_HeaderMatchingTwoDifferentBanks_ReturnsNull()
    {
        string path = CreateTempCsv(
            "Kartennummer;Zeitraum;Belegdatum;Eingangstag;Text/Verwendungszweck;Kurs;Betrag;Waehrung\n");

        Assert.Null(_registry.Detect(path));
    }

    [Fact]
    public void DetectWithHint_AmbiguousHeader_StillResolvesViaTheExplicitHint()
    {
        string path = CreateTempCsv(
            "Kartennummer;Zeitraum;Belegdatum;Eingangstag;Text/Verwendungszweck;Kurs;Betrag;Waehrung\n");

        Assert.IsType<Parsing.HvbKreditkarteCsvParser>(_registry.DetectWithHint(path, "hvb"));
        Assert.IsType<Parsing.DkbKreditkarteCsvParser>(_registry.DetectWithHint(path, "dkb"));
    }
}
