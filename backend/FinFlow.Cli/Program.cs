using System.Globalization;
using System.Text;
using FinFlow;
using FinFlow.Classification;
using FinFlow.Export;
using FinFlow.Filtering;
using FinFlow.Output;
using FinFlow.Parsing;

internal static class Program
{
    private static int Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        CliOptions options = CliOptions.Parse(args);
        if (options.ShowHelp || (options.Files.Count == 0 && options.Directory is null))
        {
            Console.WriteLine(CliOptions.UsageText);
            return options.ShowHelp ? 0 : 1;
        }

        ParserRegistry registry = new();

        // 1) Collect input files — each file carries its parser hint (null = auto)
        List<(string Path, string? Bank)> allFiles = [.. options.Files];
        if (options.Directory is { } dir)
        {
            if (!Directory.Exists(dir)) { Console.Error.WriteLine($"Directory not found: {dir}"); return 1; }
            foreach (string f in Directory.EnumerateFiles(dir, "*.csv", SearchOption.TopDirectoryOnly))
                allFiles.Add((f, options.Bank));
        }

        // 2) Pick the matching parser per file and normalize
        List<LegacyTransaction> all = [];
        foreach ((string file, string? fileBank) in allFiles.DistinctBy(f => f.Path))
        {
            if (!File.Exists(file)) { Console.Error.WriteLine($"File not found: {file}"); continue; }

            bool hasHint = fileBank is not null && !fileBank.Equals("auto", StringComparison.OrdinalIgnoreCase);
            IBankCsvParser? parser = hasHint
                ? registry.DetectWithHint(file, fileBank!)
                : registry.Detect(file);

            if (parser is null)
            {
                Console.Error.WriteLine(
                    $"No matching parser for '{file}'. " +
                    $"Known: {string.Join(", ", registry.Parsers.Select(p => p.BankName).Distinct())}. " +
                    $"Tip: set --bank <name> before --file.");
                continue;
            }

            try
            {
                IReadOnlyList<LegacyTransaction> parsed = parser.Parse(file);
                Console.Error.WriteLine($"[{parser.BankName}] {Path.GetFileName(file)}: {parsed.Count} booking(s).");
                all.AddRange(parsed);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reading '{file}': {ex.Message}");
            }
        }

        // 3) Filter and sort
        IEnumerable<LegacyTransaction> filtered = TransactionFilter.Apply(all, options.ToCriteria());
        filtered = options.Sort switch
        {
            "amount"      => filtered.OrderBy(t => t.Amount),
            "amount-desc" => filtered.OrderByDescending(t => t.Amount),
            "date-desc"   => filtered.OrderByDescending(t => t.BookingDate),
            _             => filtered.OrderBy(t => t.BookingDate),
        };
        List<LegacyTransaction> filteredList = [.. filtered];

        // 4) Classify and export as XLSX
        if (options.ExportPath is { } exportPath)
        {
            string? rulesFile = FindRulesFile(options.RulesPath);
            if (rulesFile is null)
            {
                Console.Error.WriteLine(
                    "rules.json not found. " +
                    "Pass --rules <path> or place a rules.json in the working directory " +
                    "(copy rules.example.json to rules.json to get started).");
                return 1;
            }

            try
            {
                Classifier classifier = new(rulesFile);
                List<ClassifiedTransaction> classified = classifier.ClassifyAll(filteredList);
                XlsxExporter.Export(classified, exportPath);

                int cAuto  = classified.Count(c => c.Status == "auto");
                int cPruef = classified.Count(c => c.Status == "prüfen");
                int cIgn   = classified.Count(c => c.Status == "ignorieren");
                Console.Error.WriteLine($"Export: {classified.Count} booking(s) → {exportPath}");
                Console.Error.WriteLine($"  Auto:      {cAuto,4}");
                Console.Error.WriteLine($"  Review:    {cPruef,4}  ← please check");
                Console.Error.WriteLine($"  Ignored:   {cIgn,4}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Export failed: {ex.Message}");
                return 1;
            }
        }

        // 5) Table output (data on stdout, status/error messages on stderr)
        Console.WriteLine(OutputFormatter.Format(filteredList, options.Format));
        return 0;
    }

    private static string? FindRulesFile(string? explicitPath)
    {
        if (explicitPath is not null) return File.Exists(explicitPath) ? explicitPath : null;
        string[] candidates =
        [
            Path.Combine(Directory.GetCurrentDirectory(), "rules.json"),
            Path.Combine(AppContext.BaseDirectory, "rules.json"),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }
}


/// <summary>Deliberately minimal argument parser. Enough for a small tool; System.CommandLine later if needed.</summary>
internal sealed class CliOptions
{
    public List<(string Path, string? Bank)> Files { get; } = [];
    public string? Directory { get; private set; }
    public string? Bank { get; private set; }
    private string? _currentBank;
    public DateOnly? From { get; private set; }
    public DateOnly? To { get; private set; }
    public decimal? Min { get; private set; }
    public decimal? Max { get; private set; }
    public string? Contains { get; private set; }
    public string? Counterparty { get; private set; }
    public string Sort { get; private set; } = "date";
    public string Format { get; private set; } = "table";
    public bool ShowHelp { get; private set; }
    public string? ExportPath { get; private set; }
    public string? RulesPath { get; private set; }
    public int? Year { get; private set; }

    public FilterCriteria ToCriteria() => new()

    {
        From = From ?? (Year.HasValue ? new DateOnly(Year.Value, 1, 1) : null),
        To   = To   ?? (Year.HasValue ? new DateOnly(Year.Value, 12, 31) : null),
        MinAmount    = Min,
        MaxAmount    = Max,
        Contains     = Contains,
        Counterparty = Counterparty,
        Bank = null,
    };

    public static CliOptions Parse(string[] args)
    {
        CliOptions o = new();
        CultureInfo ci = CultureInfo.InvariantCulture;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for {a}");
            switch (a)
            {
                case "--file": case "-f":       o.Files.Add((Next(), o._currentBank)); break;
                case "--dir":  case "-d":       o.Directory = Next(); break;
                case "--bank": case "-b":       o.Bank = o._currentBank = Next(); break;
                case "--from":                  o.From = DateOnly.Parse(Next(), ci); break;
                case "--to":                    o.To   = DateOnly.Parse(Next(), ci); break;
                case "--year": case "-y":       o.Year = int.Parse(Next(), ci); break;
                case "--min":                   o.Min  = decimal.Parse(Next(), NumberStyles.Number | NumberStyles.AllowLeadingSign, ci); break;
                case "--max":                   o.Max  = decimal.Parse(Next(), NumberStyles.Number | NumberStyles.AllowLeadingSign, ci); break;
                case "--contains": case "-c":   o.Contains = Next(); break;
                case "--counterparty":          o.Counterparty = Next(); break;
                case "--sort":                  o.Sort   = Next().ToLowerInvariant(); break;
                case "--format":                o.Format = Next().ToLowerInvariant(); break;
                case "--export": case "-e":     o.ExportPath = Next(); break;
                case "--rules":  case "-r":     o.RulesPath  = Next(); break;
                case "--help":   case "-h":     o.ShowHelp = true; break;
                default:
                    if (!a.StartsWith('-')) o.Files.Add((a, o._currentBank));
                    else Console.Error.WriteLine($"Unknown option: {a}");
                    break;
            }
        }
        return o;
    }

    public const string UsageText = """
        finflow – read CSV account statements from various banks, normalize, filter,
        and export them for your tax return.

        Usage:
          finflow --file <file.csv> [options]
          finflow --dir <folder> [options]

        Input:
          -f, --file <path>        CSV file (can be repeated)
          -d, --dir <path>         All *.csv files in a folder
          -b, --bank <name|auto>   Force a parser (dkb, ing, hvb, traderepublic, postbank, volksbank) or auto (default)

        Filter:
          --from <yyyy-MM-dd>      Bookings from this date
          --to   <yyyy-MM-dd>      Bookings up to this date
          -y, --year <yyyy>        Whole year (shorthand for --from/--to)
          --min <amount>           Minimum amount (dot as decimal separator, e.g. -100 or 10.50)
          --max <amount>           Maximum amount
          -c, --contains <text>    Text in purpose OR counterparty
          --counterparty <text>    Text in the counterparty name

        Output:
          --sort <date|date-desc|amount|amount-desc>   Default: date
          --format <table|csv|json>                    Default: table
          -e, --export <file.xlsx>                      Tax export as an Excel file
          -r, --rules <rules.json>                      Classification rules (default: rules.json)
          -h, --help                                    This help text

        Examples:
          finflow -d ./statements -y 2024 --export taxes-2024.xlsx
          finflow -d ./statements -y 2024 --export taxes-2024.xlsx --rules my-rules.json
          finflow -d ./statements --from 2024-01-01 --to 2024-12-31 -c Rent --sort amount
        """;
}
