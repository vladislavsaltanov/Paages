using Microsoft.AspNetCore.Components;

namespace Paages.Web.Services;

public abstract record ContextMenuEntry;

public record ContextMenuItem(string label, Func<Task> onClick, bool Destructive = false, MarkupString? Icon = null) : ContextMenuEntry;
public record ContextMenuSeparator() : ContextMenuEntry;

public class ContextMenuState
{
    public List<ContextMenuEntry>? Entries { get; private set; }
    public double X { get; private set; }
    public double Y { get; private set; }
    public bool IsOpen => Entries is not null;

    public event Action? OnChange;

    public void Open(double x, double y, List<ContextMenuEntry> entries)
    {
        X = x;
        Y = y;
        Entries = entries;
        OnChange?.Invoke();
    }

    public void Close()
    {
        Entries = null;
        OnChange?.Invoke();
    }
}