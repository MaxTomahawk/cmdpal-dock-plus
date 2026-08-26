namespace CmdPalDockPlus.Core.Taskbar;

public sealed class TaskbarStateChangedEventArgs(nint? hwnd, int? processId) : EventArgs
{
    public nint? Hwnd { get; } = hwnd;
    public int? ProcessId { get; } = processId;
}

public sealed class TaskbarStateStore
{
    private readonly object _gate = new();
    private readonly Dictionary<nint, TaskbarWindowState> _states = [];
    private readonly Dictionary<int, ulong> _lastSequenceByProcess = [];

    public event EventHandler<TaskbarStateChangedEventArgs>? Changed;

    public TaskbarWindowState? Get(nint hwnd)
    {
        lock (_gate)
        {
            return _states.TryGetValue(hwnd, out var state) ? Clone(state) : null;
        }
    }

    public IReadOnlyDictionary<nint, TaskbarWindowState> Snapshot()
    {
        lock (_gate)
        {
            return _states.ToDictionary(pair => pair.Key, pair => Clone(pair.Value));
        }
    }

    public bool Apply(TaskbarCaptureMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        nint? changedHwnd = null;
        int? changedProcess = null;
        bool changed;
        lock (_gate)
        {
            if (_lastSequenceByProcess.TryGetValue(message.ProcessId, out var previous)
                && message.Sequence <= previous)
            {
                return false;
            }

            _lastSequenceByProcess[message.ProcessId] = message.Sequence;
            changed = message switch
            {
                TaskbarHello => false,
                ProgressStateChanged progressState => ApplyProgressState(progressState),
                ProgressValueChanged progressValue => ApplyProgressValue(progressValue),
                OverlayChanged overlay => ApplyOverlay(overlay),
                OverlayCleared clear => ApplyOverlayClear(clear),
                TaskbarProcessExited exited => ApplyProcessExit(exited.ProcessId),
                _ => false,
            };

            if (changed)
            {
                if (message is ProgressStateChanged state) changedHwnd = state.Hwnd;
                else if (message is ProgressValueChanged value) changedHwnd = value.Hwnd;
                else if (message is OverlayChanged overlay) changedHwnd = overlay.Hwnd;
                else if (message is OverlayCleared clear) changedHwnd = clear.Hwnd;
                else if (message is TaskbarProcessExited exited) changedProcess = exited.ProcessId;
            }
        }

        if (changed)
        {
            Changed?.Invoke(this, new TaskbarStateChangedEventArgs(changedHwnd, changedProcess));
        }

        return changed;
    }

    public bool RemoveProcess(int processId)
    {
        bool changed;
        lock (_gate)
        {
            changed = ApplyProcessExit(processId);
            _lastSequenceByProcess.Remove(processId);
        }

        if (changed)
        {
            Changed?.Invoke(this, new TaskbarStateChangedEventArgs(null, processId));
        }

        return changed;
    }

    private bool ApplyProgressState(ProgressStateChanged message)
    {
        var current = StateFor(message.Hwnd, message.ProcessId);
        var next = message.Mode == TaskbarProgressMode.None
            ? current with { ProgressMode = TaskbarProgressMode.None, Completed = null, Total = null }
            : current with { ProgressMode = message.Mode };
        return Replace(message.Hwnd, current, next);
    }

    private bool ApplyProgressValue(ProgressValueChanged message)
    {
        var current = StateFor(message.Hwnd, message.ProcessId);
        var mode = current.ProgressMode is TaskbarProgressMode.Error or TaskbarProgressMode.Paused
            ? current.ProgressMode
            : TaskbarProgressMode.Normal;
        var next = current with { ProgressMode = mode, Completed = message.Completed, Total = message.Total };
        return Replace(message.Hwnd, current, next);
    }

    private bool ApplyOverlay(OverlayChanged message)
    {
        var current = StateFor(message.Hwnd, message.ProcessId);
        var overlay = message.Overlay.Clone();
        var next = current with { Overlay = overlay };
        return Replace(message.Hwnd, current, next);
    }

    private bool ApplyOverlayClear(OverlayCleared message)
    {
        var current = StateFor(message.Hwnd, message.ProcessId);
        var next = current with { Overlay = null };
        return Replace(message.Hwnd, current, next);
    }

    private bool ApplyProcessExit(int processId)
    {
        var keys = _states.Where(pair => pair.Value.ProcessId == processId).Select(pair => pair.Key).ToArray();
        foreach (var key in keys)
        {
            _states.Remove(key);
        }

        _lastSequenceByProcess.Remove(processId);
        return keys.Length != 0;
    }

    private TaskbarWindowState StateFor(nint hwnd, int processId)
    {
        if (_states.TryGetValue(hwnd, out var current))
        {
            return current.ProcessId == processId
                ? current
                : new TaskbarWindowState(processId, TaskbarProgressMode.None, null, null, null);
        }

        return new TaskbarWindowState(processId, TaskbarProgressMode.None, null, null, null);
    }

    private bool Replace(nint hwnd, TaskbarWindowState current, TaskbarWindowState next)
    {
        if (Equivalent(current, next))
        {
            return false;
        }

        _states[hwnd] = next;
        return true;
    }

    private static bool Equivalent(TaskbarWindowState left, TaskbarWindowState right)
    {
        if (left.ProcessId != right.ProcessId
            || left.ProgressMode != right.ProgressMode
            || left.Completed != right.Completed
            || left.Total != right.Total)
        {
            return false;
        }

        if (ReferenceEquals(left.Overlay, right.Overlay)) return true;
        if (left.Overlay is null || right.Overlay is null) return false;
        return left.Overlay.Width == right.Overlay.Width
            && left.Overlay.Height == right.Overlay.Height
            && string.Equals(left.Overlay.Description, right.Overlay.Description, StringComparison.Ordinal)
            && left.Overlay.Rgba.AsSpan().SequenceEqual(right.Overlay.Rgba);
    }

    private static TaskbarWindowState Clone(TaskbarWindowState state)
        => state with { Overlay = state.Overlay?.Clone() };
}
