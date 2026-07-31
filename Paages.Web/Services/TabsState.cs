using Microsoft.JSInterop;
namespace Paages.Web.Services;

public class TabsState
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private bool _cookieLoaded;

    public List<Guid> OpenTabsIds { get; private set; } = new List<Guid>();
    public Guid? ActiveTabId { get; private set; }

    public event Action? OnTabsChanged;

    public TabsState(IJSRuntime js) => _js = js;

    public async Task LoadFromCookiesAsync()
    {
        if (_cookieLoaded)
            return;

        _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/tabsState.js");
        var stored = await _module.InvokeAsync<StoredTabs?>("loadTabs");
        if (stored is null) return;

        var merged = stored.Tabs.ToList();
        foreach (var id in OpenTabsIds)
            if (!merged.Contains(id))
                merged.Add(id);

        OpenTabsIds = merged;
        ActiveTabId ??= stored.Active;
        OnTabsChanged?.Invoke();
    }

    public void Open(Guid id)
    {
        if (!OpenTabsIds.Contains(id))
            OpenTabsIds.Add(id);

        ActiveTabId = id;
        OnTabsChanged?.Invoke();
        _ = SaveAsync();
    }

    public Guid? Close(Guid id)
    {
        int idx = OpenTabsIds.IndexOf(id);
        if (idx == -1)
            return ActiveTabId;

        OpenTabsIds.RemoveAt(idx);

        if (ActiveTabId == id)
            ActiveTabId = OpenTabsIds.Count == 0 ?
                null : OpenTabsIds[Math.Min(idx, OpenTabsIds.Count - 1)];

        OnTabsChanged?.Invoke();
        _ = SaveAsync();
        return ActiveTabId;
    }

    async Task SaveAsync()
    {
        if (_module is null) return;
        await _module.InvokeVoidAsync("saveTabs", OpenTabsIds, ActiveTabId);
    }

    private record StoredTabs(List<Guid> Tabs, Guid? Active);
}