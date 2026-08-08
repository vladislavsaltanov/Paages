namespace Paages.Web.Interfaces;

public interface INavigationController
{
    public Task OpenNote(Guid noteId);
}