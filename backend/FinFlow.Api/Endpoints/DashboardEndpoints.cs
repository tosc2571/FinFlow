using FinFlow.Api.Services;

namespace FinFlow.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/dashboard");

        group.MapGet("/summary", ([AsParameters] TransactionFilterParams filter, DashboardService svc) =>
            Results.Ok(svc.GetSummary(filter)));

        group.MapGet("/by-category", ([AsParameters] TransactionFilterParams filter, DashboardService svc) =>
            Results.Ok(svc.GetByCategory(filter)));

        group.MapGet("/trend", ([AsParameters] TransactionFilterParams filter, DashboardService svc) =>
            Results.Ok(svc.GetTrend(filter)));
    }
}
