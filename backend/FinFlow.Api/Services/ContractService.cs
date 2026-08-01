using System.Text.RegularExpressions;
using FinFlow.Api.Data;
using FinFlow.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Api.Services;

public record ContractRequest(
    string Name,
    decimal NominalAmount,
    ContractPeriod Period,
    DateOnly AnchorDate,
    string CounterpartyPattern,
    decimal AmountTolerance,
    int? CategoryId,
    bool IsActive = true);

public record ContractDto(
    int Id,
    string Name,
    decimal NominalAmount,
    decimal ExpectedAmount,
    decimal? MinObserved,
    decimal? MaxObserved,
    ContractPeriod Period,
    DateOnly AnchorDate,
    DateOnly NextDueDate,
    string CounterpartyPattern,
    decimal AmountTolerance,
    int? CategoryId,
    string? CategoryName,
    bool IsActive,
    decimal MonthlyEquivalent);

public enum OccurrenceStatus { Matched, AmountDeviation, Missing, Upcoming }

public record ContractOccurrence(DateOnly DueDate, OccurrenceStatus Status, int? TransactionId, decimal? ActualAmount, decimal ExpectedAmount);

public record ContractDetail(ContractDto Contract, List<ContractOccurrence> Occurrences);

public record MonthForecast(int Year, int Month, decimal ExpectedIncome, decimal ExpectedExpenses, decimal Net, List<ContractDto> DueContracts);

/// <summary>
/// Recurring-payment ("contract") tracking: matches imported transactions to contracts by
/// counterparty pattern + due-date window + amount tolerance, and turns that into a cost
/// overview and a monthly forecast. See issue #3 for the full spec and the reasoning behind
/// the fixed constants below (DueDateWindowDays, RollingAverageCount, default tolerances) —
/// none of these were pinned down in the issue itself, so they're documented judgment calls.
/// </summary>
public class ContractService(AppDbContext db)
{
    /// <summary>How many days a matched payment may fall before/after the computed due date —
    /// covers weekend/holiday bank processing delays without risking a false match against a
    /// neighboring occurrence (the shortest period, Monthly, is ~30 days apart).</summary>
    private const int DueDateWindowDays = 5;

    /// <summary>How many of the most recent matched payments feed the rolling-average expected
    /// amount — long enough to smooth out one-off fluctuations, short enough to react to a
    /// genuine price change within about a year for a monthly contract.</summary>
    private const int RollingAverageCount = 6;

    public const decimal DefaultExpenseTolerance = 0.05m;
    public const decimal DefaultIncomeTolerance = 0.20m;

    // --- CRUD ---

    public async Task<List<ContractDto>> GetAllAsync()
    {
        List<Contract> contracts = await db.Contracts.Include(c => c.Category).ToListAsync();
        List<ContractDto> result = [];
        foreach (Contract c in contracts)
            result.Add(await ToDtoAsync(c));
        return result;
    }

    public async Task<ContractDetail?> GetDetailAsync(int id)
    {
        Contract? contract = await db.Contracts.Include(c => c.Category).FirstOrDefaultAsync(c => c.Id == id);
        if (contract is null) return null;
        ContractDto dto = await ToDtoAsync(contract);
        List<ContractOccurrence> occurrences = await GetOccurrencesAsync(contract, dto.ExpectedAmount);
        return new ContractDetail(dto, occurrences);
    }

    public async Task<Contract> CreateAsync(ContractRequest req)
    {
        Contract contract = new()
        {
            Name = req.Name,
            NominalAmount = req.NominalAmount,
            Period = req.Period,
            AnchorDate = req.AnchorDate,
            CounterpartyPattern = req.CounterpartyPattern,
            AmountTolerance = req.AmountTolerance,
            CategoryId = req.CategoryId,
            IsActive = req.IsActive,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        await RematchExistingTransactionsAsync(contract);
        return contract;
    }

    /// <summary>Prefills a contract from an existing transaction and creates it immediately —
    /// Period defaults to Monthly (the most common case) since it can't be inferred from a
    /// single payment; the user adjusts it afterward on the Contracts page if it's wrong (e.g.
    /// an annual premium). The counterparty pattern is Regex.Escape'd so it starts as an exact
    /// match and can be broadened later.</summary>
    public async Task<Contract?> CreateFromTransactionAsync(int transactionId)
    {
        Transaction? source = await db.Transactions.FindAsync(transactionId);
        if (source is null) return null;

        string patternSource = source.CounterpartyName ?? source.Purpose ?? "";
        if (string.IsNullOrWhiteSpace(patternSource)) return null;

        decimal tolerance = source.Amount >= 0 ? DefaultIncomeTolerance : DefaultExpenseTolerance;
        Contract contract = new()
        {
            Name = source.CounterpartyName ?? source.Purpose ?? $"Contract from transaction {source.Id}",
            NominalAmount = source.Amount,
            Period = ContractPeriod.Monthly,
            AnchorDate = source.BookingDate ?? DateOnly.FromDateTime(source.CreatedAt),
            CounterpartyPattern = Regex.Escape(patternSource),
            AmountTolerance = tolerance,
            CategoryId = source.CategoryId,
            SourceTransactionId = source.Id,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        await RematchExistingTransactionsAsync(contract);
        return contract;
    }

    /// <returns>false if the contract doesn't exist or the category id is invalid.</returns>
    public async Task<bool> UpdateAsync(int id, ContractRequest req)
    {
        Contract? contract = await db.Contracts.FindAsync(id);
        if (contract is null) return false;

        contract.Name = req.Name;
        contract.NominalAmount = req.NominalAmount;
        contract.Period = req.Period;
        contract.AnchorDate = req.AnchorDate;
        contract.CounterpartyPattern = req.CounterpartyPattern;
        contract.AmountTolerance = req.AmountTolerance;
        contract.CategoryId = req.CategoryId;
        contract.IsActive = req.IsActive;
        contract.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await RematchExistingTransactionsAsync(contract);
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        Contract? contract = await db.Contracts.FindAsync(id);
        if (contract is null) return false;
        // SetNull cascade (see AppDbContext) — deleting a contract never deletes transactions.
        db.Contracts.Remove(contract);
        await db.SaveChangesAsync();
        return true;
    }

    // --- Matching ---

    /// <summary>Called by ImportService for each newly-persisted transaction. Links the
    /// transaction to the first active contract whose pattern, due-date window, and amount
    /// tolerance all match — strict on purpose, so a merely-plausible candidate doesn't corrupt
    /// the rolling average; see GetOccurrencesAsync for the looser "candidate" matching used to
    /// surface deviations for review instead of auto-linking them.</summary>
    public async Task TryMatchAsync(Transaction transaction)
    {
        if (transaction.BookingDate is null) return;
        List<Contract> contracts = await db.Contracts.Where(c => c.IsActive).OrderBy(c => c.Id).ToListAsync();
        foreach (Contract contract in contracts)
        {
            if (!MatchesPattern(contract, transaction)) continue;
            if (!IsWithinDueWindow(contract, transaction.BookingDate.Value, out _)) continue;

            decimal expected = await GetExpectedAmountAsync(contract);
            if (!IsWithinTolerance(contract, expected, transaction.Amount)) continue;

            transaction.ContractId = contract.Id;
            return;
        }
    }

    /// <summary>Links any existing, not-yet-linked transactions to a contract after it's
    /// created or edited — never reassigns a transaction already linked to a different
    /// contract, so editing one contract can't silently steal another's history.</summary>
    private async Task RematchExistingTransactionsAsync(Contract contract)
    {
        List<Transaction> candidates = await db.Transactions
            .Where(t => t.ContractId == null && t.BookingDate != null)
            .ToListAsync();

        decimal expected = await GetExpectedAmountAsync(contract);
        bool any = false;
        foreach (Transaction t in candidates)
        {
            if (!MatchesPattern(contract, t)) continue;
            if (!IsWithinDueWindow(contract, t.BookingDate!.Value, out _)) continue;
            if (!IsWithinTolerance(contract, expected, t.Amount)) continue;
            t.ContractId = contract.Id;
            any = true;
        }
        if (any) await db.SaveChangesAsync();
    }

    private static bool MatchesPattern(Contract contract, Transaction t)
    {
        string text = $"{t.CounterpartyName ?? ""} {t.Purpose ?? ""}".Trim();
        return Regex.IsMatch(text, contract.CounterpartyPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool IsWithinTolerance(Contract contract, decimal expected, decimal actual) =>
        Math.Abs(actual - expected) <= Math.Abs(expected) * contract.AmountTolerance;

    /// <summary>True if <paramref name="date"/> falls within ±DueDateWindowDays of the nearest
    /// occurrence date; that occurrence is returned via <paramref name="occurrence"/> either way.</summary>
    private static bool IsWithinDueWindow(Contract contract, DateOnly date, out DateOnly occurrence)
    {
        occurrence = NearestOccurrence(contract, date);
        return Math.Abs(occurrence.DayNumber - date.DayNumber) <= DueDateWindowDays;
    }

    private static DateOnly NearestOccurrence(Contract contract, DateOnly around)
    {
        DateOnly occurrence = contract.AnchorDate;
        DateOnly best = occurrence;
        int bestDiff = Math.Abs(occurrence.DayNumber - around.DayNumber);
        // Step in whichever direction gets us closer to `around`; occurrences are evenly
        // spaced so the distance is unimodal and this converges in a handful of steps.
        int direction = around > contract.AnchorDate ? 1 : -1;
        for (int i = 0; i < 1000; i++)
        {
            DateOnly next = AddPeriod(occurrence, contract.Period, direction);
            int diff = Math.Abs(next.DayNumber - around.DayNumber);
            if (diff > bestDiff) break;
            occurrence = next;
            bestDiff = diff;
            best = occurrence;
        }
        return best;
    }

    private static DateOnly AddPeriod(DateOnly date, ContractPeriod period, int direction = 1)
    {
        int months = period switch
        {
            ContractPeriod.Monthly => 1,
            ContractPeriod.Quarterly => 3,
            ContractPeriod.SemiAnnually => 6,
            ContractPeriod.Annually => 12,
            _ => throw new ArgumentOutOfRangeException(nameof(period)),
        };
        return date.AddMonths(months * direction);
    }

    /// <summary>How many occurrences per year — used for the monthly/yearly cost equivalent.</summary>
    private static int OccurrencesPerYear(ContractPeriod period) => period switch
    {
        ContractPeriod.Monthly => 12,
        ContractPeriod.Quarterly => 4,
        ContractPeriod.SemiAnnually => 2,
        ContractPeriod.Annually => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(period)),
    };

    // --- Expected amount / cost overview ---

    /// <summary>Rolling average of the last RollingAverageCount matched payments; falls back to
    /// NominalAmount while no history exists yet.</summary>
    public async Task<decimal> GetExpectedAmountAsync(Contract contract)
    {
        List<decimal> recent = await db.Transactions
            .Where(t => t.ContractId == contract.Id)
            .OrderByDescending(t => t.BookingDate)
            .Take(RollingAverageCount)
            .Select(t => t.Amount)
            .ToListAsync();
        return recent.Count > 0 ? recent.Average() : contract.NominalAmount;
    }

    private async Task<ContractDto> ToDtoAsync(Contract c)
    {
        decimal expected = await GetExpectedAmountAsync(c);
        List<decimal> recent = await db.Transactions
            .Where(t => t.ContractId == c.Id)
            .OrderByDescending(t => t.BookingDate)
            .Take(RollingAverageCount)
            .Select(t => t.Amount)
            .ToListAsync();

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly next = c.AnchorDate;
        while (next < today) next = AddPeriod(next, c.Period);

        return new ContractDto(
            c.Id, c.Name, c.NominalAmount, expected,
            recent.Count > 0 ? recent.Min() : null,
            recent.Count > 0 ? recent.Max() : null,
            c.Period, c.AnchorDate, next, c.CounterpartyPattern, c.AmountTolerance,
            c.CategoryId, c.Category?.Name, c.IsActive,
            expected * OccurrencesPerYear(c.Period) / 12m);
    }

    // --- Occurrences / deviations ---

    /// <summary>Past-due and near-future occurrences for a contract, each classified as
    /// Matched (a linked transaction covers it), AmountDeviation (an unlinked transaction
    /// matches by counterparty + due window but not amount — surfaced for manual review rather
    /// than silently ignored or auto-linked), Missing (no candidate at all), or Upcoming (due
    /// date hasn't arrived yet).</summary>
    public async Task<List<ContractOccurrence>> GetOccurrencesAsync(Contract contract, decimal expectedAmount)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly rangeEnd = AddPeriod(today, contract.Period);

        List<Transaction> all = await db.Transactions
            .Where(t => t.BookingDate != null)
            .Where(t => t.ContractId == contract.Id || t.ContractId == null)
            .ToListAsync();
        List<Transaction> linked = [.. all.Where(t => t.ContractId == contract.Id)];
        List<Transaction> unlinkedCandidates = [.. all.Where(t => t.ContractId == null && MatchesPattern(contract, t))];

        List<ContractOccurrence> result = [];
        DateOnly occurrence = contract.AnchorDate;
        while (occurrence <= rangeEnd)
        {
            DateOnly due = occurrence;
            Transaction? match = linked.FirstOrDefault(t => Math.Abs(t.BookingDate!.Value.DayNumber - due.DayNumber) <= DueDateWindowDays);
            if (match is not null)
            {
                result.Add(new ContractOccurrence(due, OccurrenceStatus.Matched, match.Id, match.Amount, expectedAmount));
            }
            else
            {
                Transaction? candidate = unlinkedCandidates.FirstOrDefault(t => Math.Abs(t.BookingDate!.Value.DayNumber - due.DayNumber) <= DueDateWindowDays);
                if (candidate is not null)
                    result.Add(new ContractOccurrence(due, OccurrenceStatus.AmountDeviation, candidate.Id, candidate.Amount, expectedAmount));
                else if (due > today)
                    result.Add(new ContractOccurrence(due, OccurrenceStatus.Upcoming, null, null, expectedAmount));
                else
                    result.Add(new ContractOccurrence(due, OccurrenceStatus.Missing, null, null, expectedAmount));
            }
            occurrence = AddPeriod(occurrence, contract.Period);
        }
        return result;
    }

    // --- Forecast ---

    public async Task<List<MonthForecast>> GetForecastAsync(int months)
    {
        List<Contract> contracts = await db.Contracts.Where(c => c.IsActive).Include(c => c.Category).ToListAsync();
        List<ContractDto> dtos = [];
        foreach (Contract c in contracts) dtos.Add(await ToDtoAsync(c));

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly rangeStart = new(today.Year, today.Month, 1);
        List<MonthForecast> result = [];

        for (int i = 0; i < months; i++)
        {
            DateOnly monthStart = rangeStart.AddMonths(i);
            DateOnly monthEnd = monthStart.AddMonths(1).AddDays(-1);

            List<ContractDto> due = [];
            foreach ((Contract contract, ContractDto dto) in contracts.Zip(dtos))
            {
                if (HasOccurrenceInRange(contract, monthStart, monthEnd))
                    due.Add(dto);
            }

            decimal income = due.Where(d => d.ExpectedAmount > 0).Sum(d => d.ExpectedAmount);
            decimal expenses = due.Where(d => d.ExpectedAmount < 0).Sum(d => d.ExpectedAmount);
            result.Add(new MonthForecast(monthStart.Year, monthStart.Month, income, expenses, income + expenses, due));
        }
        return result;
    }

    private static bool HasOccurrenceInRange(Contract contract, DateOnly rangeStart, DateOnly rangeEnd)
    {
        if (contract.AnchorDate > rangeEnd) return false;
        DateOnly occurrence = contract.AnchorDate;
        // Fast-forward close to the range instead of stepping from the anchor one period at a
        // time — matters for a years-old monthly contract being forecast far in the future.
        int monthsPerStep = contract.Period switch
        {
            ContractPeriod.Monthly => 1,
            ContractPeriod.Quarterly => 3,
            ContractPeriod.SemiAnnually => 6,
            ContractPeriod.Annually => 12,
            _ => 1,
        };
        int monthsToStart = ((rangeStart.Year - occurrence.Year) * 12 + (rangeStart.Month - occurrence.Month)) / monthsPerStep * monthsPerStep;
        if (monthsToStart > 0) occurrence = occurrence.AddMonths(monthsToStart);

        for (int i = 0; i < 24; i++)
        {
            if (occurrence > rangeEnd) return false;
            if (occurrence >= rangeStart) return true;
            occurrence = occurrence.AddMonths(monthsPerStep);
        }
        return false;
    }
}
