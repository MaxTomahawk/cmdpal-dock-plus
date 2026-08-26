using CmdPalDockPlus.Windows;

namespace CmdPalDockPlus.Windows.Tests.Fakes;

internal sealed class FakeWindowBackend(params WindowSnapshot[] windows) : IWindowBackend
{
    private IReadOnlyList<WindowSnapshot> _windows = windows;

    public event EventHandler? WindowChanged;

    public int EnumerateCount { get; private set; }
    public List<(nint Hwnd, WindowShowCommand Command)> Shows { get; } = [];
    public List<nint> Focuses { get; } = [];
    public List<nint> Closes { get; } = [];

    public void SetWindows(params WindowSnapshot[] value) => _windows = value;
    public void RaiseChanged() => WindowChanged?.Invoke(this, EventArgs.Empty);

    public ValueTask<IReadOnlyList<WindowSnapshot>> EnumerateAsync(CancellationToken cancellationToken)
    {
        EnumerateCount++;
        return ValueTask.FromResult(_windows);
    }

    public ValueTask FocusAsync(nint hwnd, CancellationToken cancellationToken)
    {
        Focuses.Add(hwnd);
        return ValueTask.CompletedTask;
    }

    public ValueTask ShowAsync(nint hwnd, WindowShowCommand command, CancellationToken cancellationToken)
    {
        Shows.Add((hwnd, command));
        return ValueTask.CompletedTask;
    }

    public ValueTask CloseAsync(nint hwnd, CancellationToken cancellationToken)
    {
        Closes.Add(hwnd);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
