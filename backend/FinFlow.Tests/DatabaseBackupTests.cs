using FinFlow.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FinFlow.Tests;

public class DatabaseBackupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly string _dbPath;

    public DatabaseBackupTests()
    {
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "finflow.db");
    }

    private string BackupPath(DateOnly date) => Path.Combine(_dir, "backups", $"finflow-{date:yyyy-MM-dd}.db");

    [Fact]
    public void BackupIfNeeded_NoSourceDb_DoesNothing()
    {
        DatabaseBackup.BackupIfNeeded(_dbPath, NullLogger.Instance, new DateOnly(2026, 7, 20));

        Assert.False(Directory.Exists(Path.Combine(_dir, "backups")));
    }

    [Fact]
    public void BackupIfNeeded_FirstCallToday_CreatesDatedBackup()
    {
        File.WriteAllText(_dbPath, "fake sqlite content");
        DateOnly today = new(2026, 7, 20);

        DatabaseBackup.BackupIfNeeded(_dbPath, NullLogger.Instance, today);

        Assert.True(File.Exists(BackupPath(today)));
        Assert.Equal("fake sqlite content", File.ReadAllText(BackupPath(today)));
    }

    [Fact]
    public void BackupIfNeeded_SecondCallSameDay_DoesNotOverwriteOrDuplicate()
    {
        File.WriteAllText(_dbPath, "version 1");
        DateOnly today = new(2026, 7, 20);
        DatabaseBackup.BackupIfNeeded(_dbPath, NullLogger.Instance, today);

        File.WriteAllText(_dbPath, "version 2 (later the same day)");
        DatabaseBackup.BackupIfNeeded(_dbPath, NullLogger.Instance, today);

        // The first backup of the day is preserved, not replaced by the later state.
        Assert.Equal("version 1", File.ReadAllText(BackupPath(today)));
        Assert.Single(Directory.GetFiles(Path.Combine(_dir, "backups")));
    }

    [Fact]
    public void BackupIfNeeded_Disabled_DoesNothing()
    {
        File.WriteAllText(_dbPath, "fake sqlite content");

        DatabaseBackup.BackupIfNeeded(_dbPath, NullLogger.Instance, new DateOnly(2026, 7, 20), enabled: false);

        Assert.False(Directory.Exists(Path.Combine(_dir, "backups")));
    }

    [Fact]
    public void BackupIfNeeded_NextDay_CreatesAnAdditionalBackup()
    {
        File.WriteAllText(_dbPath, "day one");
        DateOnly day1 = new(2026, 7, 20);
        DatabaseBackup.BackupIfNeeded(_dbPath, NullLogger.Instance, day1);

        File.WriteAllText(_dbPath, "day two");
        DateOnly day2 = day1.AddDays(1);
        DatabaseBackup.BackupIfNeeded(_dbPath, NullLogger.Instance, day2);

        Assert.True(File.Exists(BackupPath(day1)));
        Assert.True(File.Exists(BackupPath(day2)));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(_dir, "backups")).Length);
    }

    [Fact]
    public void BackupIfNeeded_NextDay_NoChangeSinceLastBackup_SkipsBackup()
    {
        File.WriteAllText(_dbPath, "unchanged");
        DateOnly day1 = new(2026, 7, 20);
        DatabaseBackup.BackupIfNeeded(_dbPath, NullLogger.Instance, day1);

        // No write to _dbPath happens here — the database is genuinely unchanged.
        DateOnly day2 = day1.AddDays(1);
        DatabaseBackup.BackupIfNeeded(_dbPath, NullLogger.Instance, day2);

        Assert.True(File.Exists(BackupPath(day1)));
        Assert.False(File.Exists(BackupPath(day2)));
        Assert.Single(Directory.GetFiles(Path.Combine(_dir, "backups")));
    }

    [Fact]
    public void BackupIfNeeded_MoreThanMaxBackups_PrunesOldestKeepingOnlyTheMostRecent()
    {
        File.WriteAllText(_dbPath, "content");
        DateOnly today = new(2026, 7, 20);
        string backupsDir = Path.Combine(_dir, "backups");
        Directory.CreateDirectory(backupsDir);

        // Simulate a backlog of 12 pre-existing backups (e.g. accumulated before automatic
        // pruning existed), with strictly increasing write times, oldest to newest — including
        // today's, so BackupIfNeeded takes the "already backed up today" prune-only path.
        string[] paths = new string[12];
        for (int i = 0; i < 12; i++)
        {
            DateOnly date = today.AddDays(-11 + i);
            string path = Path.Combine(backupsDir, $"finflow-{date:yyyy-MM-dd}.db");
            File.WriteAllText(path, "backup");
            File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i));
            paths[i] = path;
        }

        DatabaseBackup.BackupIfNeeded(_dbPath, NullLogger.Instance, today);

        Assert.Equal(DatabaseBackup.MaxBackups, Directory.GetFiles(backupsDir).Length);
        Assert.False(File.Exists(paths[0])); // oldest two pruned
        Assert.False(File.Exists(paths[1]));
        for (int i = 2; i < 12; i++)
            Assert.True(File.Exists(paths[i]), $"backup {i} (newer) should have been kept");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
