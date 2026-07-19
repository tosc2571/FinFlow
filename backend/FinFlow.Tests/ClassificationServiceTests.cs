using FinFlow.Api.Data;
using FinFlow.Api.Entities;
using FinFlow.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinFlow.Tests;

public class ClassificationServiceTests : IDisposable
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

    private static Transaction Tx(ImportBatch batch, string? counterparty, string? purpose,
        ClassificationStatus status = ClassificationStatus.NeedsReview, int? categoryId = null) => new()
    {
        ImportBatch = batch,
        SourceBank = "dkb",
        Amount = -1m,
        CounterpartyName = counterparty,
        Purpose = purpose,
        ClassificationStatus = status,
        CategoryId = categoryId,
        DedupeHash = Guid.NewGuid().ToString(),
    };

    [Fact]
    public void ReclassifyAll_UpdatesMatches_PreservesManualOverrides()
    {
        using AppDbContext ctx = CreateContext();
        Category groceries = new() { Name = "Lebensmittel" };
        Category manual = new() { Name = "Handverlesen" };
        ctx.Categories.AddRange(groceries, manual);
        ImportBatch batch = new() { SourceFileName = "seed.csv", DetectedBank = "dkb", Status = ImportStatus.Completed };
        ctx.ImportBatches.Add(batch);
        ctx.SaveChanges();
        ctx.ClassificationRules.Add(new ClassificationRule
        {
            Pattern = "REWE",
            CategoryId = groceries.Id,
            Status = ClassificationRuleStatus.Auto,
        });
        Transaction matching = Tx(batch, "REWE SAGT DANKE", "Einkauf");
        Transaction overridden = Tx(batch, "REWE SAGT DANKE", "Einkauf", ClassificationStatus.ManualOverride, manual.Id);
        Transaction unmatched = Tx(batch, "Wohnbau GmbH", "Miete");
        ctx.Transactions.AddRange(matching, overridden, unmatched);
        ctx.SaveChanges();

        ReclassifyResult result = new ClassificationService(ctx).ReclassifyAll();

        Assert.Equal(2, result.Reclassified);
        Assert.Equal(1, result.SkippedManualOverride);
        Assert.Equal(groceries.Id, ctx.Transactions.Single(t => t.Id == matching.Id).CategoryId);
        Assert.Equal(ClassificationStatus.Auto, ctx.Transactions.Single(t => t.Id == matching.Id).ClassificationStatus);
        // The manual override survives — category and status untouched.
        Assert.Equal(manual.Id, ctx.Transactions.Single(t => t.Id == overridden.Id).CategoryId);
        Assert.Equal(ClassificationStatus.ManualOverride, ctx.Transactions.Single(t => t.Id == overridden.Id).ClassificationStatus);
        Assert.Null(ctx.Transactions.Single(t => t.Id == unmatched.Id).CategoryId);
    }

    [Fact]
    public void TestPattern_CountsMatchesAndReturnsSample()
    {
        using AppDbContext ctx = CreateContext();
        ImportBatch batch = new() { SourceFileName = "seed.csv", DetectedBank = "dkb", Status = ImportStatus.Completed };
        ctx.ImportBatches.Add(batch);
        ctx.Transactions.AddRange(
            Tx(batch, "REWE SAGT DANKE", "Einkauf"),
            Tx(batch, "ALDI SUED", "Einkauf"),
            Tx(batch, "Wohnbau GmbH", "Miete"));
        ctx.SaveChanges();

        PatternTestResult result = new ClassificationService(ctx).TestPattern("REWE|ALDI");

        Assert.Equal(2, result.MatchCount);
        Assert.Equal(2, result.Sample.Count);
    }

    [Fact]
    public void TestPattern_InvalidRegex_Throws()
    {
        using AppDbContext ctx = CreateContext();

        // RegexParseException derives from ArgumentException — ThrowsAny accepts subtypes,
        // matching the endpoint's catch (ArgumentException) behavior.
        Assert.ThrowsAny<ArgumentException>(() => new ClassificationService(ctx).TestPattern("[invalid"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
