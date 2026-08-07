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

    [Fact]
    public async Task ValidateCategory_EmptyName_ReturnsBadRequest()
    {
        using AppDbContext ctx = CreateContext();

        CategoryEndpoints.CategoryValidationError? error = await CategoryEndpoints.ValidateCategory(ctx, existing: null, "  ", parentCategoryId: null);

        Assert.NotNull(error);
        Assert.Equal(400, error!.StatusCode);
    }

    [Fact]
    public async Task ValidateCategory_UnknownParent_ReturnsBadRequest()
    {
        using AppDbContext ctx = CreateContext();

        CategoryEndpoints.CategoryValidationError? error = await CategoryEndpoints.ValidateCategory(ctx, existing: null, "Miete", parentCategoryId: 999);

        Assert.NotNull(error);
        Assert.Equal(400, error!.StatusCode);
        Assert.Contains("Unknown parent", error.Message);
    }

    [Fact]
    public async Task ValidateCategory_ParentIsItselfASubCategory_ReturnsBadRequest()
    {
        using AppDbContext ctx = CreateContext();
        int topId = AddCategory(ctx, "Wohnen");
        int childId = AddCategory(ctx, "Miete", topId);

        CategoryEndpoints.CategoryValidationError? error = await CategoryEndpoints.ValidateCategory(ctx, existing: null, "Kaution", parentCategoryId: childId);

        Assert.NotNull(error);
        Assert.Equal(400, error!.StatusCode);
        Assert.Contains("more than two levels", error.Message);
    }

    [Fact]
    public async Task ValidateCategory_ValidTopLevelParent_ReturnsNull()
    {
        using AppDbContext ctx = CreateContext();
        int topId = AddCategory(ctx, "Wohnen");

        CategoryEndpoints.CategoryValidationError? error = await CategoryEndpoints.ValidateCategory(ctx, existing: null, "Miete", parentCategoryId: topId);

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateCategory_ReparentingCategoryWithChildren_ReturnsBadRequest()
    {
        using AppDbContext ctx = CreateContext();
        int otherTopId = AddCategory(ctx, "Auto");
        int parentWithChildrenId = AddCategory(ctx, "Wohnen");
        AddCategory(ctx, "Miete", parentWithChildrenId);
        Category parentWithChildren = await ctx.Categories.FindAsync(parentWithChildrenId) ?? throw new InvalidOperationException();

        CategoryEndpoints.CategoryValidationError? error = await CategoryEndpoints.ValidateCategory(ctx, existing: parentWithChildren, "Wohnen", parentCategoryId: otherTopId);

        Assert.NotNull(error);
        Assert.Equal(400, error!.StatusCode);
        Assert.Contains("already has sub-categories", error.Message);
    }

    [Fact]
    public async Task ValidateCategory_SelfReferencingParent_ReturnsBadRequest()
    {
        using AppDbContext ctx = CreateContext();
        int id = AddCategory(ctx, "Wohnen");
        Category category = await ctx.Categories.FindAsync(id) ?? throw new InvalidOperationException();

        CategoryEndpoints.CategoryValidationError? error = await CategoryEndpoints.ValidateCategory(ctx, existing: category, "Wohnen", parentCategoryId: id);

        Assert.NotNull(error);
        Assert.Equal(400, error!.StatusCode);
        Assert.Contains("cycle", error.Message);
    }

    [Fact]
    public async Task ValidateCategory_DuplicateName_ReturnsConflict()
    {
        using AppDbContext ctx = CreateContext();
        AddCategory(ctx, "Miete");

        CategoryEndpoints.CategoryValidationError? error = await CategoryEndpoints.ValidateCategory(ctx, existing: null, "Miete", parentCategoryId: null);

        Assert.NotNull(error);
        Assert.Equal(409, error!.StatusCode);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
