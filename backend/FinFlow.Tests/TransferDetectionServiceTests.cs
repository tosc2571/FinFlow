using FinFlow.Api.Data;
using FinFlow.Api.Entities;
using FinFlow.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinFlow.Tests;

public class TransferDetectionServiceTests : IDisposable
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

    private static Transaction Tx(ImportBatch batch, decimal amount, string? counterpartyIban, ClassificationStatus status = ClassificationStatus.NeedsReview) => new()
    {
        ImportBatch = batch,
        SourceBank = "dkb",
        BookingDate = new DateOnly(2025, 3, 1),
        Amount = amount,
        CounterpartyIban = counterpartyIban,
        ClassificationStatus = status,
        DedupeHash = Guid.NewGuid().ToString(),
    };

    [Theory]
    [InlineData("DE12 3456 7890 1234 5678 90", "de12345678901234567890")]
    [InlineData("de12345678901234567890", "DE12345678901234567890")]
    public void Normalize_IgnoresWhitespaceAndCase(string a, string b) =>
        Assert.Equal(TransferDetectionService.Normalize(a), TransferDetectionService.Normalize(b));

    [Fact]
    public void IsInternalTransfer_MatchingOwnIban_ReturnsTrue()
    {
        HashSet<string> ownIbans = new() { "DE12345678901234567890" };
        Assert.True(TransferDetectionService.IsInternalTransfer(ownIbans, "de12 3456 7890 1234 5678 90"));
    }

    [Fact]
    public void IsInternalTransfer_UnknownOrMissingIban_ReturnsFalse()
    {
        HashSet<string> ownIbans = new() { "DE12345678901234567890" };
        Assert.False(TransferDetectionService.IsInternalTransfer(ownIbans, "DE99999999999999999999"));
        Assert.False(TransferDetectionService.IsInternalTransfer(ownIbans, null));
    }

    [Fact]
    public async Task GetOwnIbansAsync_ReturnsNormalizedRegisteredIbans()
    {
        using AppDbContext ctx = CreateContext();
        ctx.BankAccounts.Add(new BankAccount { BankName = "ING", DisplayName = "Tagesgeld", Iban = "DE12 3456 7890 1234 5678 90" });
        ctx.SaveChanges();

        HashSet<string> ibans = await new TransferDetectionService(ctx).GetOwnIbansAsync();

        Assert.Contains("DE12345678901234567890", ibans);
    }

    [Fact]
    public async Task RematchExistingTransactionsAsync_FlagsMatchingTransactionsAsInternalTransfer()
    {
        using AppDbContext ctx = CreateContext();
        Category groceries = new() { Name = "Lebensmittel" };
        ctx.Categories.Add(groceries);
        ImportBatch batch = new() { SourceFileName = "seed.csv", DetectedBank = "dkb", Status = ImportStatus.Completed };
        ctx.ImportBatches.Add(batch);
        ctx.SaveChanges();

        Transaction matching = Tx(batch, -500m, "DE12345678901234567890");
        matching.CategoryId = groceries.Id; // must be cleared once reclassified as a transfer
        Transaction other = Tx(batch, -30m, "DE99999999999999999999");
        Transaction manuallyOverridden = Tx(batch, -20m, "DE12345678901234567890", ClassificationStatus.ManualOverride);
        ctx.Transactions.AddRange(matching, other, manuallyOverridden);
        ctx.SaveChanges();

        await new TransferDetectionService(ctx).RematchExistingTransactionsAsync("DE12 3456 7890 1234 5678 90");

        Assert.Equal(ClassificationStatus.InternalTransfer, matching.ClassificationStatus);
        Assert.Null(matching.CategoryId);
        Assert.Equal(ClassificationStatus.NeedsReview, other.ClassificationStatus);
        // Explicit user choice is never overridden by automatic (re)matching.
        Assert.Equal(ClassificationStatus.ManualOverride, manuallyOverridden.ClassificationStatus);
    }

    [Fact]
    public async Task UnmatchAsync_RevertsToNeedsReview_UnlessManualOverrideOrStillMatchingAnotherAccount()
    {
        using AppDbContext ctx = CreateContext();
        ImportBatch batch = new() { SourceFileName = "seed.csv", DetectedBank = "dkb", Status = ImportStatus.Completed };
        ctx.ImportBatches.Add(batch);
        ctx.SaveChanges();

        Transaction toRevert = Tx(batch, -500m, "DE12345678901234567890", ClassificationStatus.InternalTransfer);
        Transaction keepManual = Tx(batch, -20m, "DE12345678901234567890", ClassificationStatus.ManualOverride);
        ctx.Transactions.AddRange(toRevert, keepManual);
        ctx.SaveChanges();

        await new TransferDetectionService(ctx).UnmatchAsync("DE12 3456 7890 1234 5678 90");

        Assert.Equal(ClassificationStatus.NeedsReview, toRevert.ClassificationStatus);
        Assert.Equal(ClassificationStatus.ManualOverride, keepManual.ClassificationStatus);
    }

    [Fact]
    public async Task UnmatchAsync_KeepsInternalTransfer_IfStillMatchingAnotherRegisteredAccount()
    {
        using AppDbContext ctx = CreateContext();
        ImportBatch batch = new() { SourceFileName = "seed.csv", DetectedBank = "dkb", Status = ImportStatus.Completed };
        ctx.ImportBatches.Add(batch);
        // A second registered account happens to share the same IBAN as the one being removed
        // (e.g. edited in place) — the transaction should stay flagged either way.
        ctx.BankAccounts.Add(new BankAccount { BankName = "ING", Iban = "DE12345678901234567890" });
        ctx.SaveChanges();

        Transaction stillMatched = Tx(batch, -500m, "DE12345678901234567890", ClassificationStatus.InternalTransfer);
        ctx.Transactions.Add(stillMatched);
        ctx.SaveChanges();

        await new TransferDetectionService(ctx).UnmatchAsync("DE12345678901234567890");

        Assert.Equal(ClassificationStatus.InternalTransfer, stillMatched.ClassificationStatus);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
