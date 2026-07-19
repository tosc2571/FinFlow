using System.Text.Json;
using System.Text.RegularExpressions;
using FinFlow.Classification;

namespace FinFlow;

public sealed class Classifier
{
    private readonly List<(Regex Regex, string Category, string Status)> _rules;

    public Classifier(string rulesPath)
    {
        string json = File.ReadAllText(rulesPath);
        JsonSerializerOptions opts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        LegacyClassificationRule[] rules = JsonSerializer.Deserialize<LegacyClassificationRule[]>(json, opts) ?? [];
        _rules = [.. rules
            .Select(r => (
                new Regex(r.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant),
                r.Category,
                r.Status
            ))];
    }

    public ClassifiedTransaction Classify(LegacyTransaction t)
    {
        string text = $"{t.CounterpartyName ?? ""} {t.Purpose ?? ""}".Trim();
        foreach ((Regex Regex, string Category, string Status) rule in _rules)
        {
            if (rule.Regex.IsMatch(text))
                return new ClassifiedTransaction(t, rule.Category, rule.Status);
        }
        return new ClassifiedTransaction(t, "Sonstiges", "prüfen");
    }

    public List<ClassifiedTransaction> ClassifyAll(IEnumerable<LegacyTransaction> transactions) =>
        [.. transactions.Select(Classify)];
}
