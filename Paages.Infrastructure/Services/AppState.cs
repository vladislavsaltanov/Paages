public class AppState
{
    public DateTime? LastSavedAt { get; private set; }

    public event Action? OnChange;
    public event Action<Guid, string>? NoteRenamed;

    public event Action? OnTreeChanged;
    public void NotifyTreeChanged() => OnTreeChanged?.Invoke();

    public void SetLastSaved(DateTime time)
    {
        LastSavedAt = time;
        OnChange?.Invoke();
    }
    public void NotifyNoteRenamed(Guid noteId, string newTitle)
    {
        NoteRenamed?.Invoke(noteId, newTitle);
    }
}