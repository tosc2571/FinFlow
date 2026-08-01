using Microsoft.Extensions.Logging;

namespace FinFlow.Api.Services;

/// <summary>
/// Copies the SQLite database to a dated file in a "backups" folder next to it, at most once
/// per calendar day (UTC) — a safety net against a bad migration or accidental data changes,
/// independent of any specific upgrade. Runs before the database is opened for the first time
/// in this process, so the source file isn't locked yet.
/// </summary>
public static class DatabaseBackup
{
    public static void BackupIfNeeded(string dbPath, ILogger logger, DateOnly? today = null, bool enabled = true)
    {
        if (!enabled)
            return;

        if (!File.Exists(dbPath))
            return; // fresh install — nothing to back up yet

        DateOnly date = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        string backupsDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dbPath))!, "backups");
        string backupPath = Path.Combine(backupsDir, $"finflow-{date:yyyy-MM-dd}.db");

        if (File.Exists(backupPath))
            return; // already backed up today

        try
        {
            Directory.CreateDirectory(backupsDir);
            File.Copy(dbPath, backupPath);
            logger.LogInformation("Created daily database backup at {BackupPath}", backupPath);
        }
        catch (Exception ex)
        {
            // A failed backup must never prevent the app from starting.
            logger.LogWarning(ex, "Failed to create daily database backup at {BackupPath}", backupPath);
        }
    }
}
