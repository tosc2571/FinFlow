using System.Text;
using FinFlow.Api.Data;
using FinFlow.Api.Entities;
using FinFlow.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinFlow.Tests;

public class ImportServiceTests : IDisposable
{
    static ImportServiceTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".db");
    private readonly List<string> _tempFiles = [];

    private const string DkbHeader =
        "\"Buchungsdatum\";\"Wertstellung\";\"Status\";\"Zahlungspflichtige*r\";\"Zahlungsempfänger*in\";" +
        "\"Verwendungszweck\";\"Umsatztyp\";\"IBAN\";\"Betrag (€)\";\"Gläubiger-ID\";\"Mandatsreferenz\";\"Kundenreferenz\"";

    private const string DkbTwoRows = DkbHeader + "\n" +
        "\"01.03.2025\";\"01.03.2025\";\"Gebucht\";\"\";\"Landlord GmbH\";\"Miete März 2025\";\"Lastschrift\";\"DE11111100000000\";\"-1400,00\";\"\";\"\";\"\"\n" +
        "\"20.03.2025\";\"20.03.2025\";\"Gebucht\";\"\";\"REWE SAGT DANKE\";\"Einkauf\";\"Kartenzahlung\";\"DE22222200000000\";\"-45,67\";\"\";\"\";\"\"\n";

    private AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        AppDbContext ctx = new(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static ImportService CreateService(AppDbContext ctx) =>
        new(ctx, new ClassificationService(ctx), new ContractService(ctx), new TransferDetectionService(ctx));

    private string CreateTempCsv(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public void Analyze_DkbHeader_DetectsDkb()
    {
        using AppDbContext ctx = CreateContext();
        ImportService svc = CreateService(ctx);

        AnalyzeFileResult result = svc.Analyze(CreateTempCsv(DkbTwoRows), "export.csv");

        Assert.Equal("dkb", result.DetectedBank);
    }

    [Fact]
    public void Analyze_UnknownFormat_ReturnsNull()
    {
        using AppDbContext ctx = CreateContext();
        ImportService svc = CreateService(ctx);

        AnalyzeFileResult result = svc.Analyze(CreateTempCsv("foo;bar;baz\n1;2;3\n"), "unknown.csv");

        Assert.Null(result.DetectedBank);
    }

    [Fact]
    public async Task ImportFile_PersistsTransactionsAndBatch()
    {
        using AppDbContext ctx = CreateContext();
        ImportService svc = CreateService(ctx);

        ImportFileResult result = await svc.ImportFileAsync(CreateTempCsv(DkbTwoRows), "export.csv", null);

        Assert.Null(result.Error);
        Assert.Equal("dkb", result.Bank);
        Assert.Equal(2, result.Imported);
        Assert.Equal(0, result.Duplicates);

        ImportBatch batch = ctx.ImportBatches.Single();
        Assert.Equal(ImportStatus.Completed, batch.Status);
        Assert.Equal(2, batch.TransactionCount);
        Assert.Equal(2, ctx.Transactions.Count(t => t.ImportBatchId == batch.Id));
        Assert.All(ctx.Transactions, t => Assert.Equal(ClassificationStatus.NeedsReview, t.ClassificationStatus));
    }

    [Fact]
    public async Task ImportFile_ReimportSameFile_SkipsAllAsDuplicates()
    {
        using AppDbContext ctx = CreateContext();
        ImportService svc = CreateService(ctx);

        await svc.ImportFileAsync(CreateTempCsv(DkbTwoRows), "export.csv", null);
        ImportFileResult second = await svc.ImportFileAsync(CreateTempCsv(DkbTwoRows), "export.csv", null);

        Assert.Equal(0, second.Imported);
        Assert.Equal(2, second.Duplicates);
        Assert.Equal(2, ctx.Transactions.Count());

        // TransactionCount is the total rows found in the file (2), not just the persisted ones
        // (0, since every row was a duplicate) — the case the no-duplicates test can't cover.
        ImportBatch secondBatch = ctx.ImportBatches.Single(b => b.Id == second.BatchId);
        Assert.Equal(2, secondBatch.TransactionCount);
        Assert.Equal(2, secondBatch.DuplicateCount);
    }

    [Fact]
    public async Task ImportFile_MatchingRule_AssignsCategoryAndStatus()
    {
        using AppDbContext ctx = CreateContext();
        Category groceries = new() { Name = "Lebensmittel" };
        ctx.Categories.Add(groceries);
        ctx.SaveChanges();
        ctx.ClassificationRules.Add(new ClassificationRule
        {
            Pattern = "REWE|ALDI|LIDL",
            CategoryId = groceries.Id,
            Status = ClassificationRuleStatus.Ignore,
        });
        ctx.SaveChanges();
        ImportService svc = CreateService(ctx);

        await svc.ImportFileAsync(CreateTempCsv(DkbTwoRows), "export.csv", null);

        Transaction rewe = ctx.Transactions.Single(t => t.CounterpartyName == "REWE SAGT DANKE");
        Assert.Equal(groceries.Id, rewe.CategoryId);
        Assert.Equal(ClassificationStatus.Ignored, rewe.ClassificationStatus);

        Transaction rent = ctx.Transactions.Single(t => t.CounterpartyName == "Landlord GmbH");
        Assert.Null(rent.CategoryId);
        Assert.Equal(ClassificationStatus.NeedsReview, rent.ClassificationStatus);
    }

    [Fact]
    public async Task ImportFile_UnknownFormat_CreatesFailedBatch()
    {
        using AppDbContext ctx = CreateContext();
        ImportService svc = CreateService(ctx);

        ImportFileResult result = await svc.ImportFileAsync(CreateTempCsv("foo;bar;baz\n1;2;3\n"), "unknown.csv", null);

        Assert.NotNull(result.Error);
        Assert.Equal(0, result.Imported);
        ImportBatch batch = ctx.ImportBatches.Single();
        Assert.Equal(ImportStatus.Failed, batch.Status);
        Assert.NotNull(batch.ErrorMessage);
        Assert.Empty(ctx.Transactions);
    }

    [Fact]
    public async Task ImportFile_WrongBankHint_FailsInsteadOfMisparsing()
    {
        using AppDbContext ctx = CreateContext();
        ImportService svc = CreateService(ctx);

        // File is DKB, but the user forces "ing" — must fail, not silently misparse.
        ImportFileResult result = await svc.ImportFileAsync(CreateTempCsv(DkbTwoRows), "export.csv", "ing");

        Assert.NotNull(result.Error);
        Assert.Equal(0, result.Imported);
        Assert.Equal(ImportStatus.Failed, ctx.ImportBatches.Single().Status);
    }

    [Fact]
    public async Task ImportFile_MatchesExistingContract()
    {
        using AppDbContext ctx = CreateContext();
        Contract rent = new()
        {
            Name = "Rent",
            NominalAmount = -1400.00m,
            Period = ContractPeriod.Monthly,
            AnchorDate = new DateOnly(2025, 3, 1),
            CounterpartyPattern = "Landlord GmbH",
            AmountTolerance = 0.05m,
        };
        ctx.Contracts.Add(rent);
        ctx.SaveChanges();
        ImportService svc = CreateService(ctx);

        await svc.ImportFileAsync(CreateTempCsv(DkbTwoRows), "export.csv", null);

        Transaction landlord = ctx.Transactions.Single(t => t.CounterpartyName == "Landlord GmbH");
        Assert.Equal(rent.Id, landlord.ContractId);
        Transaction rewe = ctx.Transactions.Single(t => t.CounterpartyName == "REWE SAGT DANKE");
        Assert.Null(rewe.ContractId);
    }

    [Fact]
    public async Task ImportFile_CounterpartyIbanMatchesOwnAccount_ClassifiesAsInternalTransferOverAnyRule()
    {
        using AppDbContext ctx = CreateContext();
        Category rent = new() { Name = "Miete" };
        ctx.Categories.Add(rent);
        ctx.SaveChanges();
        // A rule that would otherwise categorize this row — transfer detection must win anyway.
        ctx.ClassificationRules.Add(new ClassificationRule
        {
            Pattern = "Landlord",
            CategoryId = rent.Id,
            Status = ClassificationRuleStatus.Auto,
        });
        ctx.BankAccounts.Add(new BankAccount { BankName = "dkb", DisplayName = "Tagesgeld", Iban = "DE11111100000000" });
        ctx.SaveChanges();
        ImportService svc = CreateService(ctx);

        await svc.ImportFileAsync(CreateTempCsv(DkbTwoRows), "export.csv", null);

        Transaction landlord = ctx.Transactions.Single(t => t.CounterpartyName == "Landlord GmbH");
        Assert.Equal(ClassificationStatus.InternalTransfer, landlord.ClassificationStatus);
        Assert.Null(landlord.CategoryId);

        // Unrelated row is unaffected and still gets classified normally.
        Transaction rewe = ctx.Transactions.Single(t => t.CounterpartyName == "REWE SAGT DANKE");
        Assert.Equal(ClassificationStatus.NeedsReview, rewe.ClassificationStatus);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        foreach (string f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }
}
