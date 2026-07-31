public class AppState
{
    public DateTime? LastSavedAt { get; private set; }

    public event Action? OnChange;

    public void SetLastSaved(DateTime time)
    {
        LastSavedAt = time;
        OnChange?.Invoke();
    }
}