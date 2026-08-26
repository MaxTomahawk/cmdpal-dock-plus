namespace CmdPalDockPlus.Windows;

public interface IWindowBackend : IAsyncDisposable
{
    event EventHandler? WindowChanged;

    ValueTask<IReadOnlyList<WindowSnapshot>> EnumerateAsync(CancellationToken cancellationToken);

    ValueTask FocusAsync(nint hwnd, CancellationToken cancellationToken);

    ValueTask ShowAsync(nint hwnd, WindowShowCommand command, CancellationToken cancellationToken);

    ValueTask CloseAsync(nint hwnd, CancellationToken cancellationToken);
}

public interface IWindowTracker : IAsyncDisposable
{
    IReadOnlyList<WindowSnapshot> Snapshot { get; }

    event EventHandler<WindowSetChanged>? Changed;

    ValueTask StartAsync(CancellationToken cancellationToken);

    ValueTask ReconcileAsync(CancellationToken cancellationToken);
}
