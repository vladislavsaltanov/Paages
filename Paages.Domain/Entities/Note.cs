namespace Paages.Domain.Entities;

public class Note
{
    public Guid Id { get; set; }
    public Guid? FolderId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ContentHtml { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}