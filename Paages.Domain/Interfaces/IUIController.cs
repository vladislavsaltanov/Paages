using Paages.Domain.Entities;

namespace Paages.Domain.Interfaces;

public interface IUIController
{
    public event Action<HashSet<Guid> > ExpandFolders;

    public void InvokeFolderExpansion(HashSet<Guid> folderIds);
}