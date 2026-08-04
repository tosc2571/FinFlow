using FinFlow.Api.Services;
using Xunit;

namespace FinFlow.Tests;

public class NotesServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly string _dbPath;
    private readonly NotesService _service;

    public NotesServiceTests()
    {
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "finflow.db");
        _service = new NotesService(_dbPath);
    }

    [Fact]
    public void List_NoNotesDir_ReturnsEmpty()
    {
        Assert.Empty(_service.List());
    }

    [Fact]
    public void Create_ThenRead_RoundTripsContent()
    {
        NoteWriteResult result = _service.Create("Strategy", "# My strategy\n\nSave more.");

        Assert.Equal(NoteWriteResult.Ok, result);
        NoteDetail? note = _service.Read("Strategy");
        Assert.NotNull(note);
        Assert.Equal("Strategy", note!.Name);
        Assert.Equal("# My strategy\n\nSave more.", note.Content);
    }

    [Fact]
    public void Create_Duplicate_ReturnsAlreadyExists()
    {
        _service.Create("Strategy", "v1");

        NoteWriteResult result = _service.Create("Strategy", "v2");

        Assert.Equal(NoteWriteResult.AlreadyExists, result);
        Assert.Equal("v1", _service.Read("Strategy")!.Content);
    }

    [Theory]
    [InlineData("../secrets")]
    [InlineData("..\\secrets")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("..")]
    [InlineData("")]
    public void Create_InvalidName_ReturnsInvalidNameAndWritesNothing(string name)
    {
        NoteWriteResult result = _service.Create(name, "content");

        Assert.Equal(NoteWriteResult.InvalidName, result);
        Assert.False(Directory.Exists(Path.Combine(_dir, "notes")));
    }

    [Theory]
    [InlineData("../secrets")]
    [InlineData("a/b")]
    public void Read_InvalidName_ReturnsNull(string name)
    {
        Assert.Null(_service.Read(name));
    }

    [Fact]
    public void List_ReturnsSummariesSortedByName()
    {
        _service.Create("Zebra", "z");
        _service.Create("Alpha", "a");

        List<NoteSummary> notes = _service.List();

        Assert.Equal(["Alpha", "Zebra"], notes.Select(n => n.Name));
    }

    [Fact]
    public void Update_ExistingNote_ChangesContent()
    {
        _service.Create("Strategy", "v1");

        NoteWriteResult result = _service.Update("Strategy", "v2");

        Assert.Equal(NoteWriteResult.Ok, result);
        Assert.Equal("v2", _service.Read("Strategy")!.Content);
    }

    [Fact]
    public void Update_MissingNote_ReturnsNotFound()
    {
        Assert.Equal(NoteWriteResult.NotFound, _service.Update("Ghost", "content"));
    }

    [Fact]
    public void Delete_ExistingNote_RemovesIt()
    {
        _service.Create("Strategy", "v1");

        NoteWriteResult result = _service.Delete("Strategy");

        Assert.Equal(NoteWriteResult.Ok, result);
        Assert.Null(_service.Read("Strategy"));
    }

    [Fact]
    public void Delete_MissingNote_ReturnsNotFound()
    {
        Assert.Equal(NoteWriteResult.NotFound, _service.Delete("Ghost"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
