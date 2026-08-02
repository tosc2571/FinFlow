using FinFlow.Api.Data;
using FinFlow.Api.Entities;
using FinFlow.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Api.Endpoints;

public static class BankAccountEndpoints
{
    public record BankAccountRequest(string BankName, string? DisplayName, string Iban);
    public record BankAccountDto(int Id, string BankName, string? DisplayName, string Iban);

    public static void MapBankAccountEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/accounts");

        group.MapGet("/", (AppDbContext db) =>
            db.BankAccounts.AsNoTracking()
                .OrderBy(a => a.BankName).ThenBy(a => a.DisplayName)
                .Select(a => new BankAccountDto(a.Id, a.BankName, a.DisplayName, a.Iban))
                .ToList());

        group.MapPost("/", async (BankAccountRequest req, AppDbContext db, TransferDetectionService transfers) =>
        {
            if (string.IsNullOrWhiteSpace(req.BankName))
                return Results.BadRequest(new { error = "Bank name must not be empty." });
            if (string.IsNullOrWhiteSpace(req.Iban))
                return Results.BadRequest(new { error = "IBAN must not be empty." });

            BankAccount account = new()
            {
                BankName = req.BankName.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? null : req.DisplayName.Trim(),
                Iban = req.Iban.Trim(),
            };
            db.BankAccounts.Add(account);
            await db.SaveChangesAsync();
            await transfers.RematchExistingTransactionsAsync(account.Iban);
            return Results.Created($"/api/accounts/{account.Id}",
                new BankAccountDto(account.Id, account.BankName, account.DisplayName, account.Iban));
        });

        group.MapPut("/{id:int}", async (int id, BankAccountRequest req, AppDbContext db, TransferDetectionService transfers) =>
        {
            BankAccount? account = await db.BankAccounts.FindAsync(id);
            if (account is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(req.BankName))
                return Results.BadRequest(new { error = "Bank name must not be empty." });
            if (string.IsNullOrWhiteSpace(req.Iban))
                return Results.BadRequest(new { error = "IBAN must not be empty." });

            string oldIban = account.Iban;
            account.BankName = req.BankName.Trim();
            account.DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? null : req.DisplayName.Trim();
            account.Iban = req.Iban.Trim();
            await db.SaveChangesAsync();

            if (TransferDetectionService.Normalize(oldIban) != TransferDetectionService.Normalize(account.Iban))
                await transfers.UnmatchAsync(oldIban);
            await transfers.RematchExistingTransactionsAsync(account.Iban);
            return Results.Ok(new BankAccountDto(account.Id, account.BankName, account.DisplayName, account.Iban));
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db, TransferDetectionService transfers) =>
        {
            BankAccount? account = await db.BankAccounts.FindAsync(id);
            if (account is null) return Results.NotFound();
            db.BankAccounts.Remove(account);
            await db.SaveChangesAsync();
            await transfers.UnmatchAsync(account.Iban);
            return Results.NoContent();
        });
    }
}
