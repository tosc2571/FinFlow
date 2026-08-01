using FinFlow.Api.Services;
using Xunit;

namespace FinFlow.Tests;

public class AppSettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly string _dbPath;

    public AppSettingsStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "finflow.db");
    }

    [Fact]
    public void Load_NoSettingsFileYet_ReturnsDefaults()
    {
        AppSettings settings = AppSettingsStore.Load(_dbPath);

        Assert.True(settings.AutoBackupEnabled);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsTheValue()
    {
        AppSettingsStore.Save(_dbPath, new AppSettings(AutoBackupEnabled: false));

        AppSettings loaded = AppSettingsStore.Load(_dbPath);

        Assert.False(loaded.AutoBackupEnabled);
    }

    [Fact]
    public void Load_CorruptSettingsFile_FallsBackToDefaults()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ not valid json");

        AppSettings settings = AppSettingsStore.Load(_dbPath);

        Assert.True(settings.AutoBackupEnabled);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
