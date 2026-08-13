namespace Paages.Domain.Entities;

public class Publication
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public Note Note { get; set; } = null!;
    public Guid UserId { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string ContentHtml { get; set; } = "";
    public string AuthorNickname { get; set; } = "";
    public DateTime PublishedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}