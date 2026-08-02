using FinFlow.Parsing;

namespace FinFlow;

/// <summary>
/// Knows every parser. Picks the right one either by name (--bank) or by auto-detection.
/// New bank? Add it to the list here (more specific formats first).
/// </summary>
public sealed class ParserRegistry
{
    private readonly List<IBankCsvParser> _parsers =
    [
        new DkbCsvParser(),
        new DkbKreditkarteCsvParser(),
        new IngCsvParser(),
        new HvbCsvParser(),
        new HvbKreditkarteCsvParser(),
        new TradeRepublicCsvParser(),
        new PostbankCsvParser(),
        new VolksbankCsvParser(),
    ];

    public IReadOnlyList<IBankCsvParser> Parsers => _parsers;

    /// <summary>
    /// If a file's header matches parsers from more than one bank (e.g. DKB's and HVB's credit
    /// card exports are byte-identical — see HvbKreditkarteCsvParser), silently picking the
    /// first-registered one would guess wrong roughly half the time. Returning null instead
    /// surfaces it as "not recognized" in the import UI, forcing a conscious pick via the bank
    /// dropdown (DetectWithHint) rather than a confident-looking but unreliable auto-guess.
    /// </summary>
    public IBankCsvParser? Detect(string filePath)
    {
        List<IBankCsvParser> matches = [.. _parsers.Where(p => p.CanParse(filePath))];
        return matches.Select(p => p.BankName).Distinct().Count() > 1
            ? null
            : matches.FirstOrDefault();
    }

    /// <summary>
    /// Like Detect, but first filters to parsers with a matching BankName.
    /// Allows several parsers per bank (e.g. DKB checking + DKB credit card).
    /// </summary>
    public IBankCsvParser? DetectWithHint(string filePath, string bankHint) =>
        _parsers
            .Where(p => p.BankName.Equals(bankHint, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(p => p.CanParse(filePath));
}
