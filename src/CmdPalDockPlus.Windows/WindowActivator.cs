namespace CmdPalDockPlus.Windows;

public sealed class WindowActivator(IWindowBackend backend)
{
    public async ValueTask FocusAsync(WindowSnapshot window, CancellationToken cancellationToken)
    {
        if (window.State == WindowState.Minimized)
        {
            await backend.ShowAsync(window.Hwnd, WindowShowCommand.Restore, cancellationToken).ConfigureAwait(false);
        }

        await backend.FocusAsync(window.Hwnd, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask RestoreAsync(nint hwnd, CancellationToken cancellationToken)
        => backend.ShowAsync(hwnd, WindowShowCommand.Restore, cancellationToken);

    public ValueTask MinimizeAsync(nint hwnd, CancellationToken cancellationToken)
        => backend.ShowAsync(hwnd, WindowShowCommand.Minimize, cancellationToken);

    public ValueTask MaximizeAsync(nint hwnd, CancellationToken cancellationToken)
        => backend.ShowAsync(hwnd, WindowShowCommand.Maximize, cancellationToken);

    public ValueTask CloseAsync(nint hwnd, CancellationToken cancellationToken)
        => backend.CloseAsync(hwnd, cancellationToken);
}
