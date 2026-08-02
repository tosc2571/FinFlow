namespace FinFlow.Api.Services;

/// <summary>
/// Re-checks DatabaseBackup.BackupIfNeeded periodically while the app is running, not just at
/// startup (Program.cs) — needed so a long-running process, e.g. a Docker container with
/// `restart: unless-stopped` (only restarts on crash/host-reboot, not on a schedule), still
/// takes a first backup of a new day without ever being restarted. BackupIfNeeded itself is
/// still what enforces "at most once a day, only if something changed" — this just makes sure
/// that check keeps getting a chance to run.
/// </summary>
public class PeriodicBackupService(string dbPath, ILogger<PeriodicBackupService> logger) : BackgroundService
{
    // Backups themselves are capped at one a day, so checking more often than that buys
    // nothing but extra disk I/O for no benefit — a NAS running this continuously shouldn't
    // pay for polling faster than the thing it's polling for can even change.
    private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(CheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            RunCheck();
    }

    /// <summary>Exposed separately so a test can trigger one check without waiting on the timer.</summary>
    public void RunCheck()
    {
        AppSettings settings = AppSettingsStore.Load(dbPath);
        DatabaseBackup.BackupIfNeeded(dbPath, logger, enabled: settings.AutoBackupEnabled);
    }
}
