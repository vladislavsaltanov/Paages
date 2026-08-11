using Paages.Api.Contracts;
using Paages.Infrastructure.Services;

namespace Paages.Api.Endpoints;

public static class NoteEndpoints
{
    public static void MapNoteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/notes").RequireAuthorization();

        group.MapGet("/", async (NoteService notes) =>
            Results.Ok((await notes.GetNotesAsync()).Select(ToResponse)));

        group.MapGet("/{id:guid}", async (Guid id, NoteService notes) =>
        {
            var note = await notes.GetNoteAsync(id);
            return note is null ? Results.NotFound() : Results.Ok(ToResponse(note));
        });

        group.MapPost("/", async (CreateNoteRequest request, NoteService notes) =>
            Results.Ok(ToResponse(await notes.CreateNoteAsync(request.FolderId))));

        group.MapPut("/{id:guid}/content", async (Guid id, SaveContentRequest request, NoteService notes) =>
        {
            await notes.SaveNoteContentAsync(id, request.ContentHtml);
            return Results.NoContent();
        });

        group.MapPut("/{id:guid}/rename", async (Guid id, RenameNoteRequest request, NoteService notes) =>
            Results.Ok(await notes.RenameNoteAsync(id, request.Title)));

        group.MapPut("/{id:guid}/move", async (Guid id, MoveRequest request, NoteService notes) =>
        {
            await notes.MoveAsync(id, isFolder: false, request.NewParentId, request.InsertBeforeId);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/pin", async (Guid id, NoteService notes) =>
        {
            await notes.TogglePinAsync(id, isFolder: false);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/duplicate", async (Guid id, NoteService notes) =>
            Results.Ok(ToResponse(await notes.DuplicateNoteAsync(id))));

        group.MapDelete("/{id:guid}", async (Guid id, NoteService notes) =>
        {
            await notes.DeleteNoteAsync(id);
            return Results.NoContent();
        });
    }

    private static NoteResponse ToResponse(Domain.Entities.Note n) =>
        new(n.Id, n.Title, n.ContentHtml, n.FolderId, n.IsPinned, n.SortOrder, n.CreatedAt, n.UpdatedAt);
}