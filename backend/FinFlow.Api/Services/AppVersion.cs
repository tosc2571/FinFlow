namespace FinFlow.Api.Services;

/// <summary>
/// Reads the running app's version from the plain "VERSION" file the release workflow writes
/// next to the published executable (same file scripts/finflow.sh compares against the latest
/// GitHub tag). Not present in development, where "dev" is reported instead.
/// </summary>
public static class AppVersion
{
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "VERSION");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : "dev";
    }
}
