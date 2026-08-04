using System.Text.RegularExpressions;

namespace FinFlow.Api.Services;

public record NoteSummary(string Name, DateTime UpdatedAt);
public record NoteDetail(string Name, string Content, DateTime UpdatedAt);

public enum NoteWriteResult { Ok, InvalidName, AlreadyExists, NotFound }

/// <summary>
/// Personal notes (issue #30) — plain .md files in a "notes" folder next to the database (same
/// pattern as DatabaseBackup's "backups" folder), not database rows. That means they're visible/
/// editable/backup-able as ordinary files outside the app too, and never locked into SQLite.
/// </summary>
public class NotesService(string dbPath)
{
    private readonly string _notesDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dbPath))!, "notes");

    /// <summary>
    /// Note names become filenames on disk, so they're restricted to a safe character set —
    /// this is the actual security boundary against path traversal (e.g. "../../secrets"), not
    /// just input validation UX. No slashes, no dots (rules out ".." and hidden/double-extension
    /// tricks), no backslashes.
    /// </summary>
    private static readonly Regex ValidName = new(@"^[A-Za-z0-9 _-]{1,100}$", RegexOptions.Compiled);

    public static bool IsValidName(string name) => ValidName.IsMatch(name);

    private string PathFor(string name) => Path.Combine(_notesDir, name + ".md");

    public List<NoteSummary> List()
    {
        if (!Directory.Exists(_notesDir)) return [];
        return [.. Directory.GetFiles(_notesDir, "*.md")
            .Select(p => new NoteSummary(Path.GetFileNameWithoutExtension(p), File.GetLastWriteTimeUtc(p)))
            .OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)];
    }

    public NoteDetail? Read(string name)
    {
        if (!IsValidName(name)) return null;
        string path = PathFor(name);
        if (!File.Exists(path)) return null;
        return new NoteDetail(name, File.ReadAllText(path), File.GetLastWriteTimeUtc(path));
    }

    public NoteWriteResult Create(string name, string content)
    {
        if (!IsValidName(name)) return NoteWriteResult.InvalidName;
        Directory.CreateDirectory(_notesDir);
        string path = PathFor(name);
        if (File.Exists(path)) return NoteWriteResult.AlreadyExists;
        File.WriteAllText(path, content);
        return NoteWriteResult.Ok;
    }

    public NoteWriteResult Update(string name, string content)
    {
        if (!IsValidName(name)) return NoteWriteResult.InvalidName;
        string path = PathFor(name);
        if (!File.Exists(path)) return NoteWriteResult.NotFound;
        File.WriteAllText(path, content);
        return NoteWriteResult.Ok;
    }

    public NoteWriteResult Delete(string name)
    {
        if (!IsValidName(name)) return NoteWriteResult.InvalidName;
        string path = PathFor(name);
        if (!File.Exists(path)) return NoteWriteResult.NotFound;
        File.Delete(path);
        return NoteWriteResult.Ok;
    }
}
