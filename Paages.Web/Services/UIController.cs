using Paages.Domain.Interfaces;
namespace Paages.Web.Services;

public class UIController: IUIController
{
    public event Action<HashSet<Guid>>? ExpandFolders;

    public void InvokeFolderExpansion(HashSet<Guid> folderIds) => ExpandFolders?.Invoke(folderIds);
}