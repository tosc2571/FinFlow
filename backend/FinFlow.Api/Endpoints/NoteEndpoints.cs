using FinFlow.Api.Services;

namespace FinFlow.Api.Endpoints;

public static class NoteEndpoints
{
    public record NoteRequest(string Name, string Content);
    public record NoteContentRequest(string Content);

    public static void MapNoteEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/notes");

        group.MapGet("/", (NotesService notes) => notes.List());

        group.MapGet("/{name}", (string name, NotesService notes) =>
        {
            NoteDetail? note = notes.Read(name);
            return note is null ? Results.NotFound() : Results.Ok(note);
        });

        group.MapPost("/", (NoteRequest req, NotesService notes) =>
        {
            NoteWriteResult result = notes.Create(req.Name, req.Content ?? "");
            return result switch
            {
                NoteWriteResult.Ok => Results.Created($"/api/notes/{req.Name}", notes.Read(req.Name)),
                NoteWriteResult.AlreadyExists => Results.Conflict(new { error = "A note with this name already exists." }),
                _ => Results.BadRequest(new { error = "Invalid note name." }),
            };
        });

        group.MapPut("/{name}", (string name, NoteContentRequest req, NotesService notes) =>
        {
            NoteWriteResult result = notes.Update(name, req.Content ?? "");
            return result switch
            {
                NoteWriteResult.Ok => Results.Ok(notes.Read(name)),
                NoteWriteResult.NotFound => Results.NotFound(),
                _ => Results.BadRequest(new { error = "Invalid note name." }),
            };
        });

        group.MapDelete("/{name}", (string name, NotesService notes) =>
        {
            NoteWriteResult result = notes.Delete(name);
            return result switch
            {
                NoteWriteResult.Ok => Results.NoContent(),
                NoteWriteResult.NotFound => Results.NotFound(),
                _ => Results.BadRequest(new { error = "Invalid note name." }),
            };
        });
    }
}
