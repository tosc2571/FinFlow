using System.Text.Json;

namespace FinFlow.Api.Services;

public record AppSettings(bool AutoBackupEnabled = true);

/// <summary>
/// Persists small app-level settings as a JSON file next to the database — deliberately not a
/// database table, so the auto-backup toggle can be read before DatabaseBackup.BackupIfNeeded
/// touches the SQLite file for the first time (see Program.cs).
/// </summary>
public static class AppSettingsStore
{
    public static AppSettings Load(string dbPath)
    {
        string path = SettingsPath(dbPath);
        if (!File.Exists(path)) return new AppSettings();

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public static void Save(string dbPath, AppSettings settings)
    {
        File.WriteAllText(SettingsPath(dbPath), JsonSerializer.Serialize(settings));
    }

    private static string SettingsPath(string dbPath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dbPath))!, "settings.json");
}
