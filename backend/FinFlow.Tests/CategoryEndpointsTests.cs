using FinFlow.Api.Data;
using FinFlow.Api.Endpoints;
using FinFlow.Api.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinFlow.Tests;

public class CategoryEndpointsTests : IDisposable
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

    private static int AddCategory(AppDbContext ctx, string name, int? parentId = null)
    {
        Category category = new() { Name = name, ParentCategoryId = parentId, IsIncome = false, SortOrder = 0 };
        ctx.Categories.Add(category);
        ctx.SaveChanges();
        return category.Id;
    }

    [Fact]
    public async Task IsDuplicateName_SameNameSameParent_ReturnsTrue()
    {
        using AppDbContext ctx = CreateContext();
        AddCategory(ctx, "Miete");

        bool result = await CategoryEndpoints.IsDuplicateName(ctx, "Miete", null, excludeId: null);

        Assert.True(result);
    }

    [Fact]
    public async Task IsDuplicateName_CaseInsensitiveAndWhitespace_ReturnsTrue()
    {
        using AppDbContext ctx = CreateContext();
        AddCategory(ctx, "Miete");

        bool result = await CategoryEndpoints.IsDuplicateName(ctx, "  miete  ", null, excludeId: null);

        Assert.True(result);
    }

    [Fact]
    public async Task IsDuplicateName_SameNameDifferentParent_ReturnsFalse()
    {
        using AppDbContext ctx = CreateContext();
        int parentA = AddCategory(ctx, "Wohnen");
        int parentB = AddCategory(ctx, "Sonstiges");
        AddCategory(ctx, "Miete", parentA);

        bool result = await CategoryEndpoints.IsDuplicateName(ctx, "Miete", parentB, excludeId: null);

        Assert.False(result);
    }

    [Fact]
    public async Task IsDuplicateName_DifferentName_ReturnsFalse()
    {
        using AppDbContext ctx = CreateContext();
        AddCategory(ctx, "Miete");

        bool result = await CategoryEndpoints.IsDuplicateName(ctx, "Nebenkosten", null, excludeId: null);

        Assert.False(result);
    }

    [Fact]
    public async Task IsDuplicateName_ExcludingItself_ReturnsFalse()
    {
        using AppDbContext ctx = CreateContext();
        int id = AddCategory(ctx, "Miete");

        bool result = await CategoryEndpoints.IsDuplicateName(ctx, "Miete", null, excludeId: id);

        Assert.False(result);
    }

    [Fact]
    public async Task IsDuplicateName_ExcludingOther_StillReturnsTrue()
    {
        using AppDbContext ctx = CreateContext();
        int id = AddCategory(ctx, "Miete");
        AddCategory(ctx, "Nebenkosten");

        bool result = await CategoryEndpoints.IsDuplicateName(ctx, "Miete", null, excludeId: id + 999);

        Assert.True(result);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
