using FinFlow.Api.Data;
using FinFlow.Api.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinFlow.Tests;

/// <summary>
/// SQLite has no native DECIMAL/DATE type, so decimal and DateOnly columns rely on EF Core's
/// value conversion — these tests exist to catch a silent precision/conversion regression early
/// (flagged as a risk to verify in the Phase 2 data model plan).
/// </summary>
public class AppDbContextTests : IDisposable
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

    private int CreateImportBatch(AppDbContext ctx)
    {
        ImportBatch batch = new() { SourceFileName = "test.csv", DetectedBank = "dkb", Status = ImportStatus.Completed };
        ctx.ImportBatches.Add(batch);
        ctx.SaveChanges();
        return batch.Id;
    }

    [Fact]
    public void Transaction_DecimalAndDateOnly_RoundTripExactly()
    {
        int transactionId;
        using (AppDbContext ctx = CreateContext())
        {
            int batchId = CreateImportBatch(ctx);
            Transaction tx = new()
            {
                ImportBatchId = batchId,
                SourceBank = "dkb",
                BookingDate = new DateOnly(2025, 3, 1),
                Amount = -1234.56m,
                DedupeHash = "test-hash-1",
            };
            ctx.Transactions.Add(tx);
            ctx.SaveChanges();
            transactionId = tx.Id;
        }

        using (AppDbContext ctx = CreateContext())
        {
            Transaction saved = ctx.Transactions.Single(t => t.Id == transactionId);
            Assert.Equal(new DateOnly(2025, 3, 1), saved.BookingDate);
            Assert.Equal(-1234.56m, saved.Amount);
        }
    }

    [Fact]
    public void Transaction_DedupeHash_MustBeUnique()
    {
        using AppDbContext ctx = CreateContext();
        int batchId = CreateImportBatch(ctx);

        ctx.Transactions.Add(new Transaction { ImportBatchId = batchId, SourceBank = "dkb", Amount = 1m, DedupeHash = "dup" });
        ctx.SaveChanges();

        ctx.Transactions.Add(new Transaction { ImportBatchId = batchId, SourceBank = "dkb", Amount = 2m, DedupeHash = "dup" });
        Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
    }

    public void Dispose()
    {
        // SQLite pools connections by default, which can keep the file handle open
        // briefly after the DbContext is disposed — release the pool before deleting.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
