using FinFlow.Api.Data;
using FinFlow.Api.Entities;
using FinFlow.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinFlow.Tests;

public class DashboardServiceTests : IDisposable
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

    private AppDbContext SeedContext(out int groceriesId)
    {
        AppDbContext ctx = CreateContext();
        Category groceries = new() { Name = "Lebensmittel" };
        ctx.Categories.Add(groceries);
        ImportBatch batch = new() { SourceFileName = "seed.csv", DetectedBank = "dkb", Status = ImportStatus.Completed };
        ctx.ImportBatches.Add(batch);
        ctx.SaveChanges();
        groceriesId = groceries.Id;

        Transaction Tx(DateOnly date, decimal amount, ClassificationStatus status, int? categoryId = null) => new()
        {
            ImportBatch = batch,
            SourceBank = "dkb",
            BookingDate = date,
            Amount = amount,
            ClassificationStatus = status,
            CategoryId = categoryId,
            DedupeHash = Guid.NewGuid().ToString(),
        };

        ctx.Transactions.AddRange(
            Tx(new DateOnly(2025, 1, 10), 2500m, ClassificationStatus.Auto),
            Tx(new DateOnly(2025, 1, 20), -850m, ClassificationStatus.NeedsReview),
            Tx(new DateOnly(2025, 2, 5), -45.50m, ClassificationStatus.Auto, groceries.Id),
            Tx(new DateOnly(2025, 2, 6), -99.99m, ClassificationStatus.Ignored, groceries.Id));
        ctx.SaveChanges();
        return ctx;
    }

    [Fact]
    public void GetSummary_ExcludesIgnoredFromSums()
    {
        using AppDbContext ctx = SeedContext(out _);

        DashboardSummary summary = new DashboardService(ctx).GetSummary(new TransactionFilterParams(Year: 2025));

        Assert.Equal(2500m, summary.Income);
        Assert.Equal(-895.50m, summary.Expenses); // -850 + -45.50; the ignored -99.99 not counted
        Assert.Equal(1604.50m, summary.Net);
        Assert.Equal(3, summary.TransactionCount);
        Assert.Equal(1, summary.NeedsReviewCount);
        Assert.Equal(1, summary.IgnoredCount);
    }

    [Fact]
    public void GetByCategory_GroupsWithSonstigesFallback()
    {
        using AppDbContext ctx = SeedContext(out int groceriesId);

        List<CategoryBreakdown> rows = new DashboardService(ctx).GetByCategory(new TransactionFilterParams(Year: 2025));

        Assert.Equal(2, rows.Count);
        CategoryBreakdown groceries = rows.Single(r => r.CategoryId == groceriesId);
        Assert.Equal(-45.50m, groceries.Total); // ignored transaction excluded despite same category
        CategoryBreakdown uncategorized = rows.Single(r => r.CategoryId == null);
        Assert.Equal("Sonstiges", uncategorized.CategoryName);
        Assert.Equal(2, uncategorized.Count);
    }

    [Fact]
    public void GetTrend_GroupsByMonth()
    {
        using AppDbContext ctx = SeedContext(out _);

        List<MonthlyTrend> trend = new DashboardService(ctx).GetTrend(new TransactionFilterParams(Year: 2025));

        Assert.Equal(2, trend.Count);
        MonthlyTrend january = trend.Single(m => m.Month == 1);
        Assert.Equal(2500m, january.Income);
        Assert.Equal(-850m, january.Expenses);
        MonthlyTrend february = trend.Single(m => m.Month == 2);
        Assert.Equal(-45.50m, february.Net); // ignored excluded
    }

    [Fact]
    public void GetSummary_ExcludesInternalTransfersFromSumsButCountsThemSeparately()
    {
        using AppDbContext ctx = CreateContext();
        ImportBatch batch = new() { SourceFileName = "seed.csv", DetectedBank = "dkb", Status = ImportStatus.Completed };
        ctx.ImportBatches.Add(batch);
        ctx.SaveChanges();

        Transaction Tx(decimal amount, ClassificationStatus status) => new()
        {
            ImportBatch = batch,
            SourceBank = "dkb",
            BookingDate = new DateOnly(2025, 3, 1),
            Amount = amount,
            ClassificationStatus = status,
            DedupeHash = Guid.NewGuid().ToString(),
        };

        ctx.Transactions.AddRange(
            Tx(2000m, ClassificationStatus.Auto),
            Tx(-500m, ClassificationStatus.InternalTransfer)); // moved to a savings account, not real spending
        ctx.SaveChanges();

        DashboardSummary summary = new DashboardService(ctx).GetSummary(new TransactionFilterParams(Year: 2025));

        Assert.Equal(2000m, summary.Income);
        Assert.Equal(0m, summary.Expenses); // the transfer must not count as an expense
        Assert.Equal(2000m, summary.Net);
        Assert.Equal(1, summary.TransactionCount); // only the real income counted
        Assert.Equal(1, summary.InternalTransferCount);
    }

    [Fact]
    public void GetByCategoryAndGetTrend_AlsoExcludeInternalTransfers()
    {
        using AppDbContext ctx = CreateContext();
        Category savings = new() { Name = "Sparen" };
        ctx.Categories.Add(savings);
        ImportBatch batch = new() { SourceFileName = "seed.csv", DetectedBank = "dkb", Status = ImportStatus.Completed };
        ctx.ImportBatches.Add(batch);
        ctx.SaveChanges();

        ctx.Transactions.Add(new Transaction
        {
            ImportBatch = batch,
            SourceBank = "dkb",
            BookingDate = new DateOnly(2025, 3, 1),
            Amount = -500m,
            ClassificationStatus = ClassificationStatus.InternalTransfer,
            CategoryId = savings.Id,
            DedupeHash = Guid.NewGuid().ToString(),
        });
        ctx.SaveChanges();

        DashboardService svc = new(ctx);
        Assert.Empty(svc.GetByCategory(new TransactionFilterParams(Year: 2025)));
        Assert.Empty(svc.GetTrend(new TransactionFilterParams(Year: 2025)));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
