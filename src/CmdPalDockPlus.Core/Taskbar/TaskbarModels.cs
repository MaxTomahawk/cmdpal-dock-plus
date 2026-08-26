namespace CmdPalDockPlus.Core.Taskbar;

public enum TaskbarProgressMode : uint
{
    None = 0,
    Indeterminate = 0x1,
    Normal = 0x2,
    Error = 0x4,
    Paused = 0x8,
}

public sealed record TaskbarOverlay(
    int Width,
    int Height,
    byte[] Rgba,
    string Description)
{
    public const int MaxDimension = 256;

    public TaskbarOverlay Clone()
        => new(Width, Height, Rgba.ToArray(), Description);
}

public sealed record TaskbarWindowState(
    int ProcessId,
    TaskbarProgressMode ProgressMode,
    ulong? Completed,
    ulong? Total,
    TaskbarOverlay? Overlay)
{
    public double? ProgressPercent
        => Completed is { } completed && Total is > 0
            ? Math.Clamp(completed * 100d / Total.Value, 0d, 100d)
            : null;
}

public abstract record TaskbarCaptureMessage(ulong Sequence, int ProcessId);

public sealed record TaskbarHello(ulong Sequence, int ProcessId, TaskbarArchitecture Architecture)
    : TaskbarCaptureMessage(Sequence, ProcessId);

public sealed record ProgressStateChanged(ulong Sequence, int ProcessId, nint Hwnd, TaskbarProgressMode Mode)
    : TaskbarCaptureMessage(Sequence, ProcessId);

public sealed record ProgressValueChanged(ulong Sequence, int ProcessId, nint Hwnd, ulong Completed, ulong Total)
    : TaskbarCaptureMessage(Sequence, ProcessId);

public sealed record OverlayChanged(ulong Sequence, int ProcessId, nint Hwnd, TaskbarOverlay Overlay)
    : TaskbarCaptureMessage(Sequence, ProcessId);

public sealed record OverlayCleared(ulong Sequence, int ProcessId, nint Hwnd)
    : TaskbarCaptureMessage(Sequence, ProcessId);

public sealed record TaskbarProcessExited(ulong Sequence, int ProcessId)
    : TaskbarCaptureMessage(Sequence, ProcessId);

public enum TaskbarArchitecture : ushort
{
    Unknown = 0,
    X86 = 1,
    X64 = 2,
    Arm64 = 3,
}
