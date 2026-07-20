namespace FinFlow.Api.Services;

/// <summary>
/// Resolves the stable, OS-appropriate directory FinFlow's database lives in when running as
/// a published app — independent of the working directory or which folder a release zip was
/// unpacked into, so upgrading to a new version doesn't leave data behind in the old folder.
/// Windows: %APPDATA%\FinFlow (roaming). Linux: ~/.local/share/finflow (XDG_DATA_HOME).
/// </summary>
public static class AppDataDirectory
{
    public static string Resolve()
    {
        string baseDir = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string folder = Path.Combine(baseDir, OperatingSystem.IsWindows() ? "FinFlow" : "finflow");
        Directory.CreateDirectory(folder);
        return folder;
    }
}
