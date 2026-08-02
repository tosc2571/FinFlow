using FinFlow.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FinFlow.Tests;

public class PeriodicBackupServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly string _dbPath;

    public PeriodicBackupServiceTests()
    {
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "finflow.db");
    }

    [Fact]
    public void RunCheck_AutoBackupEnabled_CreatesABackup()
    {
        File.WriteAllText(_dbPath, "content");
        PeriodicBackupService service = new(_dbPath, NullLogger<PeriodicBackupService>.Instance);

        service.RunCheck();

        Assert.True(Directory.Exists(Path.Combine(_dir, "backups")));
        Assert.Single(Directory.GetFiles(Path.Combine(_dir, "backups")));
    }

    [Fact]
    public void RunCheck_AutoBackupDisabledInSettings_DoesNothing()
    {
        File.WriteAllText(_dbPath, "content");
        AppSettingsStore.Save(_dbPath, new AppSettings(AutoBackupEnabled: false));
        PeriodicBackupService service = new(_dbPath, NullLogger<PeriodicBackupService>.Instance);

        service.RunCheck();

        Assert.False(Directory.Exists(Path.Combine(_dir, "backups")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
