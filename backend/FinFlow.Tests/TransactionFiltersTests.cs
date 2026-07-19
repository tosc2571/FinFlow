using FinFlow.Api.Data;
using FinFlow.Api.Entities;
using FinFlow.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinFlow.Tests;

public class TransactionFiltersTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".db");

    private AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        AppDbContext ctx = new(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static int _hashCounter;

    private static Transaction Tx(ImportBatch batch, string bank, DateOnly date, decimal amount,
        string? counterparty = null, string? purpose = null,
        ClassificationStatus status = ClassificationStatus.NeedsReview, int? categoryId = null) => new()
    {
        ImportBatch = batch,
        SourceBank = bank,
        BookingDate = date,
        Amount = amount,
        CounterpartyName = counterparty,
        Purpose = purpose,
        ClassificationStatus = status,
        CategoryId = categoryId,
        DedupeHash = $"hash-{Interlocked.Increment(ref _hashCounter)}",
    };

    private AppDbContext SeedContext()
    {
        AppDbContext ctx = CreateContext();
        ImportBatch batch = new() { SourceFileName = "seed.csv", DetectedBank = "dkb", Status = ImportStatus.Completed };
        ctx.ImportBatches.Add(batch);
        ctx.Transactions.AddRange(
            Tx(batch, "dkb", new DateOnly(2024, 12, 31), -10m, "REWE SAGT DANKE", "Einkauf"),
            Tx(batch, "dkb", new DateOnly(2025, 1, 15), -850m, "Wohnbau GmbH", "Miete Januar"),
            Tx(batch, "ing", new DateOnly(2025, 3, 1), 2500m, "Arbeitgeber AG", "Gehalt"),
            Tx(batch, "ing", new DateOnly(2025, 6, 30), -45.67m, "REWE SAGT DANKE", "Einkauf", ClassificationStatus.Ignored));
        ctx.SaveChanges();
        return ctx;
    }

    [Fact]
    public void Apply_YearFilter_ExpandsToFullYearRange()
    {
        using AppDbContext ctx = SeedContext();

        List<Transaction> result = [.. TransactionFilters.Apply(ctx.Transactions, new TransactionFilterParams(Year: 2025))];

        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, t => t.BookingDate!.Value.Year == 2024);
    }

    [Fact]
    public void Apply_Contains_MatchesPurposeOrCounterpartyCaseInsensitively()
    {
        using AppDbContext ctx = SeedContext();

        List<Transaction> byCounterparty = [.. TransactionFilters.Apply(ctx.Transactions, new TransactionFilterParams(Contains: "rewe"))];
        List<Transaction> byPurpose = [.. TransactionFilters.Apply(ctx.Transactions, new TransactionFilterParams(Contains: "miete"))];

        Assert.Equal(2, byCounterparty.Count);
        Assert.Single(byPurpose);
    }

    [Fact]
    public void Apply_MinMax_FiltersDecimalAmounts()
    {
        using AppDbContext ctx = SeedContext();

        List<Transaction> result = [.. TransactionFilters.Apply(ctx.Transactions,
            new TransactionFilterParams(Min: -100m, Max: 0m))];

        Assert.Equal(2, result.Count); // -10 and -45.67; -850 below min, 2500 above max
    }

    [Fact]
    public void Apply_BankFilter_IsCaseInsensitive()
    {
        using AppDbContext ctx = SeedContext();

        List<Transaction> result = [.. TransactionFilters.Apply(ctx.Transactions, new TransactionFilterParams(Bank: "ING"))];

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal("ing", t.SourceBank));
    }

    [Fact]
    public void Apply_StatusFilter_MatchesExactly()
    {
        using AppDbContext ctx = SeedContext();

        List<Transaction> result = [.. TransactionFilters.Apply(ctx.Transactions,
            new TransactionFilterParams(Status: ClassificationStatus.Ignored))];

        Assert.Single(result);
    }

    [Fact]
    public void ApplySort_ByAmount_OrdersNumericallyInSqlite()
    {
        using AppDbContext ctx = SeedContext();

        // Executes in SQLite — verifies the (double) cast workaround for decimal ORDER BY.
        List<decimal> amounts = [.. TransactionFilters
            .ApplySort(ctx.Transactions, "amount")
            .Select(t => t.Amount)];

        Assert.Equal([-850m, -45.67m, -10m, 2500m], amounts);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
