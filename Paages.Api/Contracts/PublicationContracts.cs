namespace Paages.Api.Contracts;

public record PublicationResponse(string Slug, string Title, DateTime PublishedAt, DateTime UpdatedAt);

public record PublicPageResponse(string Title, string ContentHtml, string AuthorNickname, DateTime UpdatedAt);