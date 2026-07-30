using Paages.Domain.Enums;

namespace Paages.Domain.Entities;

public class Folder : ITreeNode
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public Folder? Parent { get; set; }
    public List<Folder> Children { get; set; } = new();
    public List<Note> Notes { get; set; } = new();
    public string Name { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public int SortOrder { get; set; } // TODO
}