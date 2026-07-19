using System.Text.RegularExpressions;
using FinFlow.Api.Data;
using FinFlow.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Api.Services;

/// <summary>
/// A snapshot of the active DB rules, compiled once per operation.
/// First match wins, matched against counterparty + purpose — same semantics
/// as the CLI's Classifier, just sourced from the database instead of rules.json.
/// </summary>
public sealed class RuleSet
{
    private readonly List<(Regex Regex, int CategoryId, ClassificationStatus Status)> _rules;

    internal RuleSet(List<(Regex, int, ClassificationStatus)> rules) => _rules = rules;

    public (int? CategoryId, ClassificationStatus Status) Classify(string? counterpartyName, string? purpose)
    {
        string text = $"{counterpartyName ?? ""} {purpose ?? ""}".Trim();
        foreach ((Regex regex, int categoryId, ClassificationStatus status) in _rules)
        {
            if (regex.IsMatch(text))
                return (categoryId, status);
        }
        return (null, ClassificationStatus.NeedsReview);
    }
}

public record PatternTestMatch(int TransactionId, DateOnly? BookingDate, string? CounterpartyName, string? Purpose, decimal Amount);
public record PatternTestResult(int MatchCount, List<PatternTestMatch> Sample);
public record ReclassifyResult(int Reclassified, int SkippedManualOverride);

public class ClassificationService(AppDbContext db)
{
    public RuleSet LoadRuleSet() =>
        new([.. db.ClassificationRules
            .Where(r => r.IsActive)
            .OrderBy(r => r.Priority).ThenBy(r => r.Id)
            .AsEnumerable()
            .Select(r => (
                new Regex(r.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                r.CategoryId,
                MapStatus(r.Status)))]);

    /// <summary>
    /// Re-runs classification over all transactions except manual overrides —
    /// those survive re-classification by design (see the data-model plan).
    /// </summary>
    public ReclassifyResult ReclassifyAll()
    {
        RuleSet rules = LoadRuleSet();
        int skipped = db.Transactions.Count(t => t.ClassificationStatus == ClassificationStatus.ManualOverride);
        List<Transaction> candidates = [.. db.Transactions
            .Where(t => t.ClassificationStatus != ClassificationStatus.ManualOverride)];
        foreach (Transaction t in candidates)
        {
            (t.CategoryId, t.ClassificationStatus) = rules.Classify(t.CounterpartyName, t.Purpose);
            t.UpdatedAt = DateTime.UtcNow;
        }
        db.SaveChanges();
        return new ReclassifyResult(candidates.Count, skipped);
    }

    /// <summary>Dry-runs a pattern against existing transactions without changing anything.</summary>
    /// <exception cref="ArgumentException">The pattern is not a valid regex.</exception>
    public PatternTestResult TestPattern(string pattern, int sampleLimit = 20)
    {
        Regex regex = new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        List<PatternTestMatch> sample = [];
        int count = 0;
        foreach (Transaction t in db.Transactions.AsNoTracking().OrderBy(t => t.Id))
        {
            string text = $"{t.CounterpartyName ?? ""} {t.Purpose ?? ""}".Trim();
            if (!regex.IsMatch(text)) continue;
            count++;
            if (sample.Count < sampleLimit)
                sample.Add(new PatternTestMatch(t.Id, t.BookingDate, t.CounterpartyName, t.Purpose, t.Amount));
        }
        return new PatternTestResult(count, sample);
    }

    private static ClassificationStatus MapStatus(ClassificationRuleStatus status) => status switch
    {
        ClassificationRuleStatus.Auto => ClassificationStatus.Auto,
        ClassificationRuleStatus.Ignore => ClassificationStatus.Ignored,
        _ => ClassificationStatus.NeedsReview,
    };
}
