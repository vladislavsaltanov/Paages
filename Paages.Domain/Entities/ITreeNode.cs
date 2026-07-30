namespace Paages.Domain.Entities;

public interface ITreeNode
{
    Guid Id { get; }
    int SortOrder { get; set; }
}