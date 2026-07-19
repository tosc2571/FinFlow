namespace FinFlow;

/// <summary>
/// One parser per bank CSV format. New bank = new class implementing this.
/// </summary>
public interface IBankCsvParser
{
    /// <summary>Short name, e.g. "dkb". Used for --bank and in output.</summary>
    string BankName { get; }

    /// <summary>Detects from the header row whether this parser matches the file (auto-detection).</summary>
    bool CanParse(string filePath);

    /// <summary>Reads the file and returns normalized bookings.</summary>
    IReadOnlyList<LegacyTransaction> Parse(string filePath);
}
