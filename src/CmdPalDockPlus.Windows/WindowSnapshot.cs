namespace CmdPalDockPlus.Windows;

public enum WindowState
{
    Restored,
    Minimized,
    Maximized,
}

public enum WindowShowCommand
{
    Hide = 0,
    Show = 5,
    Minimize = 6,
    Maximize = 3,
    Restore = 9,
}

public sealed record WindowSnapshot(
    nint Hwnd,
    int ProcessId,
    string ExecutableName,
    string Title,
    string ClassName,
    WindowState State,
    bool IsActive,
    string Monitor,
    long MruRank,
    string? ExecutablePath = null);

public sealed record WindowSetChanged(IReadOnlyList<WindowSnapshot> Snapshot);
