using FinFlow.Api.Services;

namespace FinFlow.Api.Endpoints;

public static class ContractEndpoints
{
    public static void MapContractEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/contracts");

        group.MapGet("/", async (ContractService svc) => Results.Ok(await svc.GetAllAsync()));

        group.MapGet("/forecast", async (int? months, ContractService svc) =>
            Results.Ok(await svc.GetForecastAsync(months ?? 12)));

        group.MapGet("/{id:int}", async (int id, ContractService svc) =>
            await svc.GetDetailAsync(id) is { } detail ? Results.Ok(detail) : Results.NotFound());

        // Both handlers below re-fetch the created contract as a ContractDto rather than
        // returning the tracked entity: retroactive matching (see RematchExistingTransactionsAsync)
        // can link an existing Transaction to the new Contract in the same DbContext, and EF's
        // navigation-property fixup then makes Contract.SourceTransaction.Contract point right
        // back at the same Contract — a cycle that blows up JSON serialization.
        group.MapPost("/", async (ContractRequest req, ContractService svc) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "Name must not be empty." });
            if (string.IsNullOrWhiteSpace(req.CounterpartyPattern))
                return Results.BadRequest(new { error = "CounterpartyPattern must not be empty." });

            Entities.Contract contract = await svc.CreateAsync(req);
            ContractDetail? detail = await svc.GetDetailAsync(contract.Id);
            return Results.Created($"/api/contracts/{contract.Id}", detail!.Contract);
        });

        group.MapPost("/from-transaction/{transactionId:int}", async (int transactionId, ContractService svc) =>
        {
            Entities.Contract? contract = await svc.CreateFromTransactionAsync(transactionId);
            if (contract is null) return Results.NotFound();
            ContractDetail? detail = await svc.GetDetailAsync(contract.Id);
            return Results.Created($"/api/contracts/{contract.Id}", detail!.Contract);
        });

        group.MapPut("/{id:int}", async (int id, ContractRequest req, ContractService svc) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "Name must not be empty." });
            if (string.IsNullOrWhiteSpace(req.CounterpartyPattern))
                return Results.BadRequest(new { error = "CounterpartyPattern must not be empty." });

            return await svc.UpdateAsync(id, req) ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id:int}", async (int id, ContractService svc) =>
            await svc.DeleteAsync(id) ? Results.NoContent() : Results.NotFound());
    }
}
