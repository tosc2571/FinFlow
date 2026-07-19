using FinFlow.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ClassificationRule> ClassificationRules => Set<ClassificationRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>()
            .HasIndex(t => t.DedupeHash)
            .IsUnique();

        // Self-referencing FK needs an explicit delete behavior — SQLite/EF Core can't
        // auto-resolve a cascade path for a tree structure.
        modelBuilder.Entity<Category>()
            .HasOne(c => c.ParentCategory)
            .WithMany(c => c.Children)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
