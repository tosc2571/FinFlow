using FinFlow.Api.Data;
using FinFlow.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Api.Services;

/// <summary>
/// Recognizes money moved between the user's own accounts (registered as BankAccount rows) as
/// an internal transfer rather than real income/spending, purely by counterparty IBAN — no
/// date/amount pair-matching needed. Works symmetrically once both accounts are registered
/// (each leg's counterparty is the other account) and even if only one leg is ever imported.
/// See issue #25 for the full reasoning.
/// </summary>
public class TransferDetectionService(AppDbContext db)
{
    /// <summary>Whitespace/case-insensitive — exports format IBANs inconsistently.</summary>
    public static string Normalize(string iban) =>
        new string(iban.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    /// <summary>Loaded once per import (like ClassificationService.LoadRuleSet), not per row.</summary>
    public async Task<HashSet<string>> GetOwnIbansAsync() =>
        [.. (await db.BankAccounts.Select(a => a.Iban).ToListAsync()).Select(Normalize)];

    public static bool IsInternalTransfer(HashSet<string> ownIbans, string? counterpartyIban) =>
        !string.IsNullOrWhiteSpace(counterpartyIban) && ownIbans.Contains(Normalize(counterpartyIban));

    /// <summary>
    /// Called after a BankAccount is created/updated: retroactively flags already-imported
    /// transactions matching its IBAN. Never touches ManualOverride (explicit user choice) or
    /// transactions already flagged as a transfer.
    /// </summary>
    public async Task RematchExistingTransactionsAsync(string iban)
    {
        string normalized = Normalize(iban);
        List<Transaction> candidates = await db.Transactions
            .Where(t => t.ClassificationStatus != ClassificationStatus.ManualOverride
                     && t.ClassificationStatus != ClassificationStatus.InternalTransfer
                     && t.CounterpartyIban != null)
            .ToListAsync();

        bool any = false;
        foreach (Transaction t in candidates)
        {
            if (Normalize(t.CounterpartyIban!) != normalized) continue;
            t.ClassificationStatus = ClassificationStatus.InternalTransfer;
            t.CategoryId = null;
            t.UpdatedAt = DateTime.UtcNow;
            any = true;
        }
        if (any) await db.SaveChangesAsync();
    }

    /// <summary>
    /// Called after a BankAccount is deleted: reverts transactions that were flagged as a
    /// transfer via that IBAN back to NeedsReview — unless they still match another remaining
    /// registered account, or were manually overridden since.
    /// </summary>
    public async Task UnmatchAsync(string removedIban)
    {
        string normalized = Normalize(removedIban);
        HashSet<string> remaining = await GetOwnIbansAsync();
        List<Transaction> candidates = await db.Transactions
            .Where(t => t.ClassificationStatus == ClassificationStatus.InternalTransfer && t.CounterpartyIban != null)
            .ToListAsync();

        bool any = false;
        foreach (Transaction t in candidates)
        {
            string n = Normalize(t.CounterpartyIban!);
            if (n != normalized || remaining.Contains(n)) continue;
            t.ClassificationStatus = ClassificationStatus.NeedsReview;
            t.UpdatedAt = DateTime.UtcNow;
            any = true;
        }
        if (any) await db.SaveChangesAsync();
    }
}
