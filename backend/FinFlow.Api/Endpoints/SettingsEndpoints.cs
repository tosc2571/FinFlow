using FinFlow.Api.Services;

namespace FinFlow.Api.Endpoints;

public static class SettingsEndpoints
{
    public record SettingsDto(string Version, bool AutoBackupEnabled);
    public record UpdateSettingsRequest(bool AutoBackupEnabled);

    public static void MapSettingsEndpoints(this WebApplication app, string dbPath)
    {
        RouteGroupBuilder group = app.MapGroup("/api/settings");

        group.MapGet("/", () =>
        {
            AppSettings settings = AppSettingsStore.Load(dbPath);
            return Results.Ok(new SettingsDto(AppVersion.Current, settings.AutoBackupEnabled));
        });

        group.MapPut("/", (UpdateSettingsRequest req) =>
        {
            AppSettings settings = new(req.AutoBackupEnabled);
            AppSettingsStore.Save(dbPath, settings);
            return Results.Ok(new SettingsDto(AppVersion.Current, settings.AutoBackupEnabled));
        });
    }
}
