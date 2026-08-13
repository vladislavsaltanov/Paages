using Paages.Api.Contracts;
using Paages.Infrastructure.Services;

namespace Paages.Api.Endpoints;

public static class PublicationEndpoints
{
    public static void MapPublicationEndpoints(this WebApplication app)
    {
        var notes = app.MapGroup("/api/v1/notes/{noteId:guid}").RequireAuthorization();

        notes.MapPost("/publish", async (Guid noteId, PublicationService publications) =>
        {
            var publication = await publications.PublishAsync(noteId);
            return Results.Ok(new PublicationResponse(
                publication.Slug, publication.Title, publication.PublishedAt, publication.UpdatedAt));
        });

        notes.MapDelete("/publish", async (Guid noteId, PublicationService publications) =>
        {
            await publications.RevokeAsync(noteId);
            return Results.NoContent();
        });

        app.MapGet("/api/v1/pub/{slug}", async (string slug, PublicationService publications) =>
        {
            var publication = await publications.GetBySlugAsync(slug);
            return publication is null
                ? Results.NotFound()
                : Results.Ok(new PublicPageResponse(
                    publication.Title, publication.ContentHtml, publication.AuthorNickname, publication.UpdatedAt));
        }).RequireRateLimiting("public");
    }
}