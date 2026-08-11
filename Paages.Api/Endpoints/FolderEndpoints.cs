using Paages.Api.Contracts;
using Paages.Infrastructure.Services;

namespace Paages.Api.Endpoints;

public static class FolderEndpoints
{
    public static void MapFolderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/folders").RequireAuthorization();

        group.MapGet("/", async (NoteService folders) =>
            Results.Ok((await folders.GetFoldersAsync()).Select(ToResponse)));

        group.MapGet("/{id:guid}", async (Guid id, NoteService folders) =>
        {
            var folder = await folders.GetFolderAsync(id);
            return folder is null ? Results.NotFound() : Results.Ok(ToResponse(folder));
        });

        group.MapPost("/", async (CreateFolderRequest request, NoteService folders) =>
            Results.Ok(ToResponse(await folders.CreateFolderAsync(request.ParentId))));


        group.MapPut("/{id:guid}/rename", async (Guid id, RenameFolderRequest request, NoteService folders) =>
            Results.Ok(await folders.RenameFolderAsync(id, request.Name)));

        group.MapPut("/{id:guid}/move", async (Guid id, MoveRequest request, NoteService folders) =>
        {
            await folders.MoveAsync(id, isFolder: true, request.NewParentId, request.InsertBeforeId);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/pin", async (Guid id, NoteService folders) =>
        {
            await folders.TogglePinAsync(id, isFolder: true);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}", async (Guid id, NoteService folders) =>
        {
            await folders.DeleteFolderAsync(id);
            return Results.NoContent();
        });
    }

    private static FolderResponse ToResponse(Domain.Entities.Folder f) =>
        new(f.Id, f.Name, f.ParentId, f.IsPinned, f.SortOrder);
}