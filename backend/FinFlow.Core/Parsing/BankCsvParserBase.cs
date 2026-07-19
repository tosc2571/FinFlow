using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace FinFlow.Parsing;

/// <summary>
/// Handles the mechanics that are always the same across German bank CSVs:
/// auto-detecting encoding (UTF-8 vs. Windows-1252), skipping preamble rows,
/// finding the header row, detecting the delimiter, and parsing comma-decimal
/// amounts and various date formats.
///
/// A concrete bank only needs to say:
///   - HeaderSignature: how do I recognize this file?
///   - MapRow: which column goes into which field?
/// </summary>
public abstract class BankCsvParserBase : IBankCsvParser
{
    public abstract string BankName { get; }

    /// <summary>Tokens that must ALL appear in the header row for this parser to match.</summary>
    protected abstract string[] HeaderSignature { get; }

    /// <summary>Only used if the file isn't valid UTF-8 (older exports).</summary>
    protected virtual Encoding FallbackEncoding => Encoding.GetEncoding(1252); // Windows-1252
    protected virtual CultureInfo Culture => CultureInfo.GetCultureInfo("de-DE");
    protected virtual string[] DateFormats => new[] { "dd.MM.yyyy", "dd.MM.yy", "yyyy-MM-dd" };
    private static readonly char[] DelimiterCandidates = { ';', ',', '\t', '|' };

    public bool CanParse(string filePath)
    {
        foreach (string line in ReadLines(filePath).Take(40))
            if (MatchesHeader(line)) return true;
        return false;
    }

    public IReadOnlyList<LegacyTransaction> Parse(string filePath)
    {
        string[] lines = ReadLines(filePath).ToArray();

        // Some banks (e.g. ING) put metadata above the actual table. Find the header row.
        int headerIndex = Array.FindIndex(lines, MatchesHeader);
        if (headerIndex < 0)
            throw new InvalidDataException($"[{BankName}] header row not found in '{filePath}'.");

        char delimiter = DetectDelimiter(lines[headerIndex]);
        string content = string.Join('\n', lines.Skip(headerIndex));

        CsvConfiguration config = new CsvConfiguration(Culture)
        {
            Delimiter = delimiter.ToString(),
            HasHeaderRecord = true,
            MissingFieldFound = null,   // don't throw on missing columns
            BadDataFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim,
        };

        using StringReader reader = new StringReader(content);
        using CsvReader csv = new CsvReader(reader, config);
        csv.Read();
        csv.ReadHeader();

        List<LegacyTransaction> result = new List<LegacyTransaction>();
        while (csv.Read())
        {
            LegacyTransaction? tx = MapRow(csv);
            if (tx is not null) result.Add(tx);
        }
        return result;
    }

    /// <summary>Maps a row's columns onto the internal model. Implement per bank.</summary>
    protected abstract LegacyTransaction? MapRow(CsvReader csv);

    private bool MatchesHeader(string line) =>
        HeaderSignature.All(token => line.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static char DetectDelimiter(string headerLine)
    {
        // The delimiter that occurs most often in the header row.
        return DelimiterCandidates
            .OrderByDescending(c => headerLine.Count(ch => ch == c))
            .First();
    }

    /// <summary>Reads the file line by line, auto-detecting encoding (UTF-8, else fallback).</summary>
    private IEnumerable<string> ReadLines(string filePath) => ReadLinesAutoDetect(filePath, FallbackEncoding);

    /// <summary>Same as ReadLines but usable statically (e.g. from an inspector).</summary>
    public static string[] ReadLinesAutoDetect(string filePath, Encoding? fallback = null)
    {
        fallback ??= Encoding.GetEncoding(1252);
        byte[] bytes = File.ReadAllBytes(filePath);
        string text;

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2); // UTF-16 LE with BOM
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            text = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2); // UTF-16 BE with BOM
        }
        else if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            text = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3); // UTF-8 with BOM
        }
        else if (IsLikelyUtf16LeWithoutBom(bytes))
        {
            text = Encoding.Unicode.GetString(bytes); // UTF-16 LE without BOM
        }
        else
        {
            try
            {
                text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                text = fallback.GetString(bytes); // e.g. Windows-1252 / ISO-8859-1
            }
        }

        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    // Detect UTF-16 LE without BOM: at least 80% of even-indexed bytes are 0x00 (ASCII text).
    private static bool IsLikelyUtf16LeWithoutBom(byte[] bytes)
    {
        if (bytes.Length < 8) return false;
        int sampleSize = Math.Min(bytes.Length & ~1, 512); // even count, max 512 bytes
        int nullCount = 0;
        for (int i = 1; i < sampleSize; i += 2) // odd offsets = high byte in LE
            if (bytes[i] == 0x00) nullCount++;
        return nullCount * 10 >= (sampleSize / 2) * 8; // >= 80%
    }

    // ---------- helpers for the concrete parsers ----------

    /// <summary>Reads the first field whose column name exists (tolerates format variants).</summary>
    protected static string? Field(CsvReader csv, params string[] names)
    {
        foreach (string name in names)
            if (csv.TryGetField<string>(name, out string? value) && !string.IsNullOrWhiteSpace(value))
                return value!.Trim();
        return null;
    }

    /// <summary>
    /// Parses amounts regardless of format: "1.234,56" (DE) just as well as "1,234.56" or "1234.56" (EN).
    /// Rule: the rightmost ',' or '.' is the decimal separator.
    /// </summary>
    protected decimal ParseAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0m;
        string s = raw.Trim().Replace(" ", "").Replace(" ", "");
        if (s.Length == 0) return 0m;

        int lastComma = s.LastIndexOf(',');
        int lastDot   = s.LastIndexOf('.');
        char decimalSep = lastComma > lastDot ? ',' : (lastDot > lastComma ? '.' : '\0');

        if (decimalSep != '\0')
        {
            char thousandSep = decimalSep == ',' ? '.' : ',';
            s = s.Replace(thousandSep.ToString(), "").Replace(decimalSep, '.');
        }

        return decimal.TryParse(s, NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out decimal d) ? d : 0m;
    }

    /// <summary>"01.03.2025" -> DateOnly (tries several formats).</summary>
    protected DateOnly? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string s = raw.Trim();
        foreach (string fmt in DateFormats)
            if (DateOnly.TryParseExact(s, fmt, Culture, DateTimeStyles.None, out DateOnly d)) return d;
        return DateOnly.TryParse(s, Culture, DateTimeStyles.None, out DateOnly any) ? any : null;
    }
}
