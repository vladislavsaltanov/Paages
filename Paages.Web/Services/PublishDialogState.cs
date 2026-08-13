namespace Paages.Web.Services;

public class PublishDialogState
{
    public bool IsOpen { get; private set; }
    public string? Slug { get; private set; }
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }
    private Guid _noteId;

    public event Action? OnChange;

    public void Open(Guid noteId)
    {
        _noteId = noteId;
        IsOpen = true;
        Slug = null;
        Error = null;
        OnChange?.Invoke();
    }

    public void SetLoading(bool loading) { IsLoading = loading; OnChange?.Invoke(); }
    public void SetResult(string slug) { Slug = slug; IsLoading = false; OnChange?.Invoke(); }
    public void SetError(string error) { Error = error; IsLoading = false; OnChange?.Invoke(); }
    public void Close() { IsOpen = false; OnChange?.Invoke(); }

    public Guid NoteId => _noteId;
}