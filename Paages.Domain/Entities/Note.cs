namespace Paages.Domain.Entities;

public class Note : ITreeNode
{
    public Guid Id { get; set; }
    public Guid? FolderId { get; set; }
    public Folder? Folder { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ContentHtml { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsPinned { get; set; }
    public int SortOrder { get; set; }
}