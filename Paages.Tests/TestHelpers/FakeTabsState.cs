using Paages.Domain.Interfaces;

namespace Paages.Tests.TestHelpers;

public class FakeTabsState : ITabsState
{
    public List<Guid> OpenTabsIds { get; } = [];
    public Guid? ActiveTabId => null;

    #pragma warning disable CS0067
    public event Action? OnTabsChanged;

    public void Open(Guid id) { }
    public Guid? Close(Guid id) => null;
    public Task LoadFromCookiesAsync() => Task.CompletedTask;
    public void OpenBackground(Guid id) { }
}