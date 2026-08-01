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

    public IBankCsvParser? Detect(string filePath) =>
        _parsers.FirstOrDefault(p => p.CanParse(filePath));

    /// <summary>
    /// Like Detect, but first filters to parsers with a matching BankName.
    /// Allows several parsers per bank (e.g. DKB checking + DKB credit card).
    /// </summary>
    public IBankCsvParser? DetectWithHint(string filePath, string bankHint) =>
        _parsers
            .Where(p => p.BankName.Equals(bankHint, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(p => p.CanParse(filePath));
}
