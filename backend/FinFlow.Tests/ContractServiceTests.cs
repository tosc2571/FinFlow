using FinFlow.Api.Data;
using FinFlow.Api.Entities;
using FinFlow.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinFlow.Tests;

public class ContractServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".db");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        AppDbContext ctx = new(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static Transaction Tx(AppDbContext ctx, DateOnly? date, decimal amount, string counterparty, int? contractId = null)
    {
        ImportBatch batch = new() { SourceFileName = "seed.csv", DetectedBank = "dkb", Status = ImportStatus.Completed };
        ctx.ImportBatches.Add(batch);
        return new()
        {
            ImportBatch = batch,
            SourceBank = "dkb",
            BookingDate = date,
            Amount = amount,
            CounterpartyName = counterparty,
            ClassificationStatus = ClassificationStatus.NeedsReview,
            DedupeHash = Guid.NewGuid().ToString(),
            ContractId = contractId,
        };
    }

    [Fact]
    public async Task TryMatchAsync_LinksTransactionWithinPatternDueWindowAndTolerance()
    {
        using AppDbContext ctx = CreateContext();
        ContractService svc = new(ctx);
        Contract rent = await svc.CreateAsync(new ContractRequest(
            "Rent", -1000m, ContractPeriod.Monthly, Today, "Landlord", 0.05m, null));

        Transaction t = Tx(ctx, Today, -1000m, "Landlord GmbH");
        await svc.TryMatchAsync(t);

        Assert.Equal(rent.Id, t.ContractId);
    }

    [Fact]
    public async Task TryMatchAsync_RejectsAmountOutsideTolerance()
    {
        using AppDbContext ctx = CreateContext();
        ContractService svc = new(ctx);
        await svc.CreateAsync(new ContractRequest(
            "Rent", -1000m, ContractPeriod.Monthly, Today, "Landlord", 0.05m, null));

        // -1500 is 50% off the nominal amount — well outside the 5% tolerance.
        Transaction t = Tx(ctx, Today, -1500m, "Landlord GmbH");
        await svc.TryMatchAsync(t);

        Assert.Null(t.ContractId);
    }

    [Fact]
    public async Task TryMatchAsync_RejectsOutsideDueWindow()
    {
        using AppDbContext ctx = CreateContext();
        ContractService svc = new(ctx);
        await svc.CreateAsync(new ContractRequest(
            "Rent", -1000m, ContractPeriod.Monthly, Today, "Landlord", 0.05m, null));

        // 15 days off the due date — outside the 5-day window, so no occurrence covers it.
        Transaction t = Tx(ctx, Today.AddDays(15), -1000m, "Landlord GmbH");
        await svc.TryMatchAsync(t);

        Assert.Null(t.ContractId);
    }

    [Fact]
    public async Task GetExpectedAmountAsync_UsesRollingAverageOfFluctuatingIncome()
    {
        using AppDbContext ctx = CreateContext();
        Contract salary = new()
        {
            Name = "Salary",
            NominalAmount = 3000m,
            Period = ContractPeriod.Monthly,
            AnchorDate = Today.AddMonths(-3),
            CounterpartyPattern = "Employer",
            AmountTolerance = 0.20m,
        };
        ctx.Contracts.Add(salary);
        ctx.SaveChanges();
        ctx.Transactions.AddRange(
            Tx(ctx, Today.AddMonths(-3), 2800m, "Employer AG", salary.Id),
            Tx(ctx, Today.AddMonths(-2), 3200m, "Employer AG", salary.Id),
            Tx(ctx, Today.AddMonths(-1), 3000m, "Employer AG", salary.Id));
        ctx.SaveChanges();
        ContractService svc = new(ctx);

        decimal expected = await svc.GetExpectedAmountAsync(salary);

        Assert.Equal(3000m, expected); // (2800 + 3200 + 3000) / 3

        // A new payment close to the fluctuating average, but far from NominalAmount's exact
        // value, still gets auto-linked — the whole point of the rolling average.
        Transaction current = Tx(ctx, Today, 3100m, "Employer AG");
        await svc.TryMatchAsync(current);
        Assert.Equal(salary.Id, current.ContractId);
    }

    [Fact]
    public async Task CreateAsync_RetroactivelyLinksExistingUnmatchedTransactions()
    {
        using AppDbContext ctx = CreateContext();
        ctx.Transactions.Add(Tx(ctx, Today, -45.99m, "Streaming Inc"));
        ctx.SaveChanges();
        ContractService svc = new(ctx);

        Contract streaming = await svc.CreateAsync(new ContractRequest(
            "Streaming", -45.99m, ContractPeriod.Monthly, Today, "Streaming", 0.05m, null));

        Transaction existing = ctx.Transactions.Single(t => t.CounterpartyName == "Streaming Inc");
        Assert.Equal(streaming.Id, existing.ContractId);
    }

    [Fact]
    public async Task RematchExistingTransactions_NeverStealsTransactionLinkedToAnotherContract()
    {
        using AppDbContext ctx = CreateContext();
        ContractService svc = new(ctx);
        Contract first = await svc.CreateAsync(new ContractRequest(
            "First", -50m, ContractPeriod.Monthly, Today, "Sub", 0.05m, null));

        Transaction t = Tx(ctx, Today, -50m, "Subscription Co", first.Id);
        ctx.Transactions.Add(t);
        ctx.SaveChanges();

        // A second contract that would also match the same transaction by pattern/date/amount.
        await svc.CreateAsync(new ContractRequest(
            "Second", -50m, ContractPeriod.Monthly, Today, "Sub", 0.05m, null));

        Transaction reloaded = ctx.Transactions.Single(x => x.Id == t.Id);
        Assert.Equal(first.Id, reloaded.ContractId);
    }

    [Fact]
    public async Task GetDetailAsync_ClassifiesOccurrences()
    {
        using AppDbContext ctx = CreateContext();
        DateOnly anchor = Today.AddMonths(-2);
        Contract contract = new()
        {
            Name = "Insurance",
            NominalAmount = -100m,
            Period = ContractPeriod.Monthly,
            AnchorDate = anchor,
            CounterpartyPattern = "Insurer",
            AmountTolerance = 0.05m,
        };
        ctx.Contracts.Add(contract);
        ctx.SaveChanges();

        // Occurrence at anchor (Today-2mo): a linked transaction -> Matched.
        ctx.Transactions.Add(Tx(ctx, anchor, -100m, "Insurer Ltd", contract.Id));
        // Occurrence at Today-1mo: an unlinked transaction matching pattern/date but not
        // amount -> surfaced as AmountDeviation, not silently auto-linked.
        ctx.Transactions.Add(Tx(ctx, Today.AddMonths(-1), -250m, "Insurer Ltd"));
        // Occurrence at Today: nothing at all -> Missing.
        // Occurrence at Today+1mo: in the future -> Upcoming.
        ctx.SaveChanges();
        ContractService svc = new(ctx);

        ContractDetail? detail = await svc.GetDetailAsync(contract.Id);

        Assert.NotNull(detail);
        Assert.Equal(4, detail!.Occurrences.Count);
        Assert.Equal(OccurrenceStatus.Matched, detail.Occurrences[0].Status);
        Assert.Equal(OccurrenceStatus.AmountDeviation, detail.Occurrences[1].Status);
        Assert.Equal(OccurrenceStatus.Missing, detail.Occurrences[2].Status);
        Assert.Equal(OccurrenceStatus.Upcoming, detail.Occurrences[3].Status);
    }

    [Fact]
    public async Task GetForecastAsync_SumsIncomeAndExpensesPerMonth()
    {
        using AppDbContext ctx = CreateContext();
        ContractService svc = new(ctx);
        await svc.CreateAsync(new ContractRequest(
            "Salary", 3000m, ContractPeriod.Monthly, Today, "Employer", 0.20m, null));
        await svc.CreateAsync(new ContractRequest(
            "Rent", -1000m, ContractPeriod.Monthly, Today, "Landlord", 0.05m, null));
        await svc.CreateAsync(new ContractRequest(
            "Annual insurance", -240m, ContractPeriod.Annually, Today, "Insurer", 0.05m, null));

        List<MonthForecast> forecast = await svc.GetForecastAsync(3);

        Assert.Equal(3, forecast.Count);
        MonthForecast thisMonth = forecast[0];
        Assert.Equal(3000m, thisMonth.ExpectedIncome);
        Assert.Equal(-1240m, thisMonth.ExpectedExpenses); // rent + the annual insurance both due this month
        Assert.Equal(1760m, thisMonth.Net);
        Assert.Equal(3, thisMonth.DueContracts.Count);

        MonthForecast nextMonth = forecast[1];
        Assert.Equal(3000m, nextMonth.ExpectedIncome);
        Assert.Equal(-1000m, nextMonth.ExpectedExpenses); // insurance not due again until next year
        Assert.Equal(2, nextMonth.DueContracts.Count);
    }

    [Fact]
    public async Task DeleteAsync_UnlinksTransactionsInsteadOfDeletingThem()
    {
        using AppDbContext ctx = CreateContext();
        ContractService svc = new(ctx);
        Contract rent = await svc.CreateAsync(new ContractRequest(
            "Rent", -1000m, ContractPeriod.Monthly, Today, "Landlord", 0.05m, null));
        ctx.Transactions.Add(Tx(ctx, Today, -1000m, "Landlord GmbH", rent.Id));
        ctx.SaveChanges();

        bool deleted = await svc.DeleteAsync(rent.Id);

        Assert.True(deleted);
        Transaction survivor = ctx.Transactions.Single();
        Assert.Null(survivor.ContractId);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
