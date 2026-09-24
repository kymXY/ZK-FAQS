namespace FaqCms.Services;

/// <summary>
/// Powers a single shared confirmation modal (see Components/Shared/ConfirmDialog.razor,
/// mounted once in AdminLayout) so every "Delete" button across the admin panel can show
/// a real confirmation dialog with one line — await Confirm.ShowAsync(...) — instead of
/// each page wiring up its own modal or relying on the browser's plain confirm() popup.
/// </summary>
public class ConfirmService
{
    private TaskCompletionSource<bool>? _tcs;

    /// <summary>Raised when a page calls ShowAsync. ConfirmDialog subscribes to this to
    /// know when to render itself with the given details.</summary>
    public event Action<ConfirmRequest>? OnShow;

    /// <summary>Raised when the dialog should close (either button was clicked).</summary>
    public event Action? OnClose;

    public string ConfirmLabel { get; private set; } = "Delete";
    public bool Danger { get; private set; } = true;

    /// <summary>Shows the confirmation dialog and waits for the user's choice.</summary>
    /// <param name="title">Short heading, e.g. "Delete category?"</param>
    /// <param name="message">Longer description of what will happen.</param>
    /// <param name="confirmLabel">Label for the destructive button, e.g. "Delete" or "Delete permanently".</param>
    /// <param name="danger">Whether the confirm button should be styled as destructive (red).</param>
    public Task<bool> ShowAsync(string title, string message, string confirmLabel = "Delete", bool danger = true)
    {
        // If something is already awaiting a previous prompt (shouldn't normally happen,
        // since the UI is modal), resolve it as cancelled rather than leaking the Task.
        _tcs?.TrySetResult(false);

        _tcs = new TaskCompletionSource<bool>();
        ConfirmLabel = confirmLabel;
        Danger = danger;
        OnShow?.Invoke(new ConfirmRequest(title, message));
        return _tcs.Task;
    }

    internal void Resolve(bool result)
    {
        _tcs?.TrySetResult(result);
        _tcs = null;
        OnClose?.Invoke();
    }
}

public record ConfirmRequest(string Title, string Message);
