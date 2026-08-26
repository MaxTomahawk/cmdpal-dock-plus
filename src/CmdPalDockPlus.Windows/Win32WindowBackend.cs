using System.Runtime.InteropServices;
using System.Text;

namespace CmdPalDockPlus.Windows;

public sealed class Win32WindowBackend : IWindowBackend
{
    private const uint WineventOutOfContext = 0x0000;
    private const uint WineventSkipOwnProcess = 0x0002;
    private const uint EventSystemForeground = 0x0003;
    private const uint EventSystemMinimizeStart = 0x0016;
    private const uint EventSystemMinimizeEnd = 0x0017;
    private const uint EventObjectCreate = 0x8000;
    private const uint EventObjectDestroy = 0x8001;
    private const uint EventObjectShow = 0x8002;
    private const uint EventObjectHide = 0x8003;
    private const uint EventObjectNameChange = 0x800C;
    private const int ObjidWindow = 0;
    private const int GwlExstyle = -20;
    private const long WsExToolwindow = 0x00000080L;
    private const long WsExAppwindow = 0x00040000L;
    private const uint GwOwner = 4;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint MonitorDefaultToNearest = 2;
    private const uint WmClose = 0x0010;

    private readonly WinEventDelegate _winEventCallback;
    private readonly List<nint> _hooks = [];
    private bool _disposed;

    public Win32WindowBackend()
    {
        _winEventCallback = OnWinEvent;
        AddHook(EventSystemForeground, EventSystemForeground);
        AddHook(EventSystemMinimizeStart, EventSystemMinimizeEnd);
        AddHook(EventObjectCreate, EventObjectNameChange);
    }

    public event EventHandler? WindowChanged;

    public ValueTask<IReadOnlyList<WindowSnapshot>> EnumerateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new List<WindowSnapshot>();
        var foreground = GetForegroundWindow();
        long rank = 0;
        EnumWindows((hwnd, _) =>
        {
            if (IsEligibleWindow(hwnd))
            {
                GetWindowThreadProcessId(hwnd, out var processId);
                if (processId != 0)
                {
                    var title = ReadWindowText(hwnd);
                    var className = ReadClassName(hwnd);
                    var executablePath = ReadExecutablePath(processId);
                    var executableName = string.IsNullOrWhiteSpace(executablePath) ? string.Empty : Path.GetFileName(executablePath);
                    var state = IsIconic(hwnd) ? WindowState.Minimized : IsZoomed(hwnd) ? WindowState.Maximized : WindowState.Restored;
                    result.Add(new WindowSnapshot(
                        hwnd,
                        unchecked((int)processId),
                        executableName,
                        title,
                        className,
                        state,
                        hwnd == foreground,
                        ReadMonitorName(hwnd),
                        rank++,
                        executablePath));
                }
            }

            return true;
        }, 0);

        return ValueTask.FromResult<IReadOnlyList<WindowSnapshot>>(result);
    }

    public ValueTask FocusAsync(nint hwnd, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = BringWindowToTop(hwnd);
        _ = SetForegroundWindow(hwnd);
        return ValueTask.CompletedTask;
    }

    public ValueTask ShowAsync(nint hwnd, WindowShowCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = ShowWindow(hwnd, (int)command);
        return ValueTask.CompletedTask;
    }

    public ValueTask CloseAsync(nint hwnd, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = PostMessage(hwnd, WmClose, 0, 0);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        foreach (var hook in _hooks)
        {
            _ = UnhookWinEvent(hook);
        }

        _hooks.Clear();
        return ValueTask.CompletedTask;
    }

    private void AddHook(uint min, uint max)
    {
        var hook = SetWinEventHook(min, max, 0, _winEventCallback, 0, 0, WineventOutOfContext | WineventSkipOwnProcess);
        if (hook != 0)
        {
            _hooks.Add(hook);
        }
    }

    private void OnWinEvent(nint hook, uint eventType, nint hwnd, int objectId, int childId, uint threadId, uint eventTime)
    {
        if (hwnd == 0)
        {
            return;
        }

        if (eventType >= EventObjectCreate && eventType <= EventObjectNameChange && objectId != ObjidWindow)
        {
            return;
        }

        if (eventType is EventObjectCreate or EventObjectDestroy or EventObjectShow or EventObjectHide or EventObjectNameChange
            || eventType is EventSystemForeground or EventSystemMinimizeStart or EventSystemMinimizeEnd)
        {
            WindowChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool IsEligibleWindow(nint hwnd)
    {
        if (!IsWindowVisible(hwnd))
        {
            return false;
        }

        var exStyle = GetWindowLongPtr(hwnd, GwlExstyle).ToInt64();
        var owner = GetWindow(hwnd, GwOwner);
        if ((exStyle & WsExToolwindow) != 0 && (exStyle & WsExAppwindow) == 0)
        {
            return false;
        }

        return owner == 0 || (exStyle & WsExAppwindow) != 0;
    }

    private static string ReadWindowText(nint hwnd)
    {
        var length = Math.Clamp(GetWindowTextLength(hwnd), 0, 32767);
        if (length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        _ = GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string ReadClassName(nint hwnd)
    {
        var builder = new StringBuilder(512);
        _ = GetClassName(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string ReadExecutablePath(uint processId)
    {
        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == 0)
        {
            return string.Empty;
        }

        try
        {
            var capacity = 32768;
            var builder = new StringBuilder(capacity);
            return QueryFullProcessImageName(process, 0, builder, ref capacity) ? builder.ToString() : string.Empty;
        }
        finally
        {
            _ = CloseHandle(process);
        }
    }

    private static string ReadMonitorName(nint hwnd)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>(), DeviceName = string.Empty };
        return monitor != 0 && GetMonitorInfo(monitor, ref info) ? info.DeviceName : string.Empty;
    }

    private delegate bool EnumWindowsDelegate(nint hwnd, nint lParam);
    private delegate void WinEventDelegate(nint hook, uint eventType, nint hwnd, int objectId, int childId, uint threadId, uint eventTime);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool EnumWindows(EnumWindowsDelegate callback, nint lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hwnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder className, int maxCount);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsZoomed(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool BringWindowToTop(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfoEx info);
    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint module, WinEventDelegate callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWinEvent(nint hook);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder exeName, ref int size);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);
}
