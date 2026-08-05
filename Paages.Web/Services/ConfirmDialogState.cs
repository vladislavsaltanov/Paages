namespace Paages.Web.Services;

public class ConfirmDialogState
{
    public string? Message { get; private set; }
    public Func<Task>? OnConfirm { get; private set; }
    public bool IsOpen => Message is not null;

    public event Action? OnChange;

    public void Open(string message, Func<Task> onConfirm)
    {
        Message = message;
        OnConfirm = onConfirm;
        OnChange?.Invoke();
    }

    public void Close()
    {
        Message = null;
        OnConfirm = null;
        OnChange?.Invoke();
    }
}