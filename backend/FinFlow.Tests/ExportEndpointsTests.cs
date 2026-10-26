using FinFlow.Api.Data;
using FinFlow.Api.Endpoints;
using FinFlow.Api.Entities;
using FinFlow.Api.Services;
using FinFlow.Classification;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinFlow.Tests;

public class ExportEndpointsTests : IDisposable
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

    private static Transaction Tx(int? categoryId, string counterparty) => new()
    {
        SourceBank = "dkb",
        BookingDate = new DateOnly(2025, 3, 1),
        Amount = -10m,
        CounterpartyName = counterparty,
        CategoryId = categoryId,
        ClassificationStatus = ClassificationStatus.Auto,
        DedupeHash = Guid.NewGuid().ToString(),
    };

    [Fact]
    public void LoadClassified_ThreeLevelsDeep_AllRootAtTheTrueTopCategory()
    {
        // Real-world case (#87): Freizeit -> Digitalisierung -> Audible. Before the fix,
        // TopCategory only walked one level up, so the Audible transaction ended up rooted at
        // "Digitalisierung" instead of the true top-level "Freizeit" - splitting one conceptual
        // bucket across two sheets in the exported workbook.
        using AppDbContext ctx = CreateContext();
        Category freizeit = new() { Name = "Freizeit" };
        ctx.Categories.Add(freizeit);
        ctx.SaveChanges();
        Category digitalisierung = new() { Name = "Digitalisierung", ParentCategoryId = freizeit.Id };
        ctx.Categories.Add(digitalisierung);
        ctx.SaveChanges();
        Category audible = new() { Name = "Audible", ParentCategoryId = digitalisierung.Id };
        ctx.Categories.Add(audible);
        ctx.SaveChanges();
        ImportBatch batch = new() { SourceFileName = "seed.csv", DetectedBank = "dkb", Status = ImportStatus.Completed };
        ctx.ImportBatches.Add(batch);
        ctx.SaveChanges();

        Transaction onFreizeit = Tx(freizeit.Id, "Direct on Freizeit");
        Transaction onDigitalisierung = Tx(digitalisierung.Id, "Direct on Digitalisierung");
        Transaction onAudible = Tx(audible.Id, "Audible subscription");
        onFreizeit.ImportBatch = batch;
        onDigitalisierung.ImportBatch = batch;
        onAudible.ImportBatch = batch;
        ctx.Transactions.AddRange(onFreizeit, onDigitalisierung, onAudible);
        ctx.SaveChanges();

        List<ClassifiedTransaction> classified = ExportEndpoints.LoadClassified(ctx, new TransactionFilterParams());

        Assert.All(classified, c => Assert.Equal("Freizeit", c.TopCategory));
        Assert.Equal("Freizeit", classified.Single(c => c.Transaction.CounterpartyName == "Direct on Freizeit").Category);
        Assert.Equal("Digitalisierung", classified.Single(c => c.Transaction.CounterpartyName == "Direct on Digitalisierung").Category);
        Assert.Equal("Audible", classified.Single(c => c.Transaction.CounterpartyName == "Audible subscription").Category);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
