using Microsoft.Extensions.Logging;

namespace FinFlow.Api.Services;

/// <summary>
/// Copies the SQLite database to a dated file in a "backups" folder next to it — at most once
/// per calendar day (UTC), and only if the database actually changed since the last backup, so
/// an idle instance doesn't accumulate identical daily copies. Keeps only the most recent
/// <see cref="MaxBackups"/>; older ones are pruned automatically. Runs before the database is
/// opened for the first time in this process (Program.cs), and again periodically
/// (<see cref="PeriodicBackupService"/>) so a long-running process — e.g. a Docker container
/// that's never restarted — doesn't miss days.
/// </summary>
public static class DatabaseBackup
{
    public const int MaxBackups = 10;

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
        {
            Prune(backupsDir, logger); // already backed up today — still enforce retention
            return;
        }

        DateTime dbLastWrite = File.GetLastWriteTimeUtc(dbPath);
        if (LatestBackupSourceTime(backupsDir) is { } lastBackedUp && dbLastWrite <= lastBackedUp)
        {
            Prune(backupsDir, logger); // nothing changed since the last backup — no reason to make another
            return;
        }

        try
        {
            Directory.CreateDirectory(backupsDir);
            File.Copy(dbPath, backupPath);
            // Stamp the backup with the source DB's write time (not "now") so a later check can
            // tell whether the DB changed since *this* backup, without a separate state file.
            File.SetLastWriteTimeUtc(backupPath, dbLastWrite);
            logger.LogInformation("Created daily database backup at {BackupPath}", backupPath);
        }
        catch (Exception ex)
        {
            // A failed backup must never prevent the app from starting.
            logger.LogWarning(ex, "Failed to create daily database backup at {BackupPath}", backupPath);
            return;
        }

        Prune(backupsDir, logger);
    }

    private static DateTime? LatestBackupSourceTime(string backupsDir)
    {
        if (!Directory.Exists(backupsDir)) return null;
        string[] files = Directory.GetFiles(backupsDir, "finflow-*.db");
        return files.Length == 0 ? null : files.Max(File.GetLastWriteTimeUtc);
    }

    /// <summary>Keeps only the MaxBackups most recently written backups; deletes the rest.</summary>
    private static void Prune(string backupsDir, ILogger logger)
    {
        if (!Directory.Exists(backupsDir)) return;

        IEnumerable<string> stale = Directory.GetFiles(backupsDir, "finflow-*.db")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Skip(MaxBackups);

        foreach (string file in stale)
        {
            try
            {
                File.Delete(file);
                logger.LogInformation("Pruned old database backup {BackupPath}", file);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to prune old database backup {BackupPath}", file);
            }
        }
    }
}
