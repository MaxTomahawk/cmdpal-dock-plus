using System.Runtime.InteropServices;
using System.Text;

namespace CmdPalDockPlus.Windows;

public sealed class Win32WindowBackend : IWindowBackend
{
    private const int GwlExstyle = -20;
    private const long WsExToolwindow = 0x00000080L;
    private const long WsExAppwindow = 0x00040000L;
    private const uint GwOwner = 4;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint MonitorDefaultToNearest = 2;
    private const uint WmClose = 0x0010;
    private const int ErrorSuccess = 0;
    private const int ErrorInsufficientBuffer = 122;
    private const uint MaxAumidChars = 4096;

    private readonly WinEventHookPump _eventPump;
    private bool _disposed;

    public Win32WindowBackend()
    {
        _eventPump = new WinEventHookPump(() => WindowChanged?.Invoke(this, EventArgs.Empty));
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
                    var identity = ReadProcessIdentity(processId);
                    var executableName = string.IsNullOrWhiteSpace(identity.ExecutablePath)
                        ? string.Empty
                        : Path.GetFileName(identity.ExecutablePath);
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
                        identity.ExecutablePath,
                        identity.AppUserModelId));
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
        _eventPump.Dispose();
        return ValueTask.CompletedTask;
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

    private static (string ExecutablePath, string? AppUserModelId) ReadProcessIdentity(uint processId)
    {
        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == 0)
        {
            return (string.Empty, null);
        }

        try
        {
            var capacity = 32768;
            var pathBuilder = new StringBuilder(capacity);
            var executablePath = QueryFullProcessImageName(process, 0, pathBuilder, ref capacity)
                ? pathBuilder.ToString()
                : string.Empty;

            uint aumidLength = 0;
            var status = GetApplicationUserModelId(process, ref aumidLength, null);
            string? appUserModelId = null;
            if (status == ErrorInsufficientBuffer && aumidLength is > 1 and <= MaxAumidChars)
            {
                var aumidBuilder = new StringBuilder(checked((int)aumidLength));
                status = GetApplicationUserModelId(process, ref aumidLength, aumidBuilder);
                if (status == ErrorSuccess)
                {
                    var value = aumidBuilder.ToString().Trim();
                    appUserModelId = string.IsNullOrWhiteSpace(value) ? null : value;
                }
            }

            return (executablePath, appUserModelId);
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
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder exeName, ref int size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetApplicationUserModelId(nint process, ref uint applicationUserModelIdLength, StringBuilder? applicationUserModelId);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);
}
