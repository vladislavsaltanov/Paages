namespace Paages.Domain.Interfaces;

public interface ITabsState
{
    List<Guid> OpenTabsIds { get; }
    Guid? ActiveTabId { get; }

    event Action? OnTabsChanged;

    void Open(Guid id);
    Guid? Close(Guid id);
    Task LoadFromCookiesAsync();
}
