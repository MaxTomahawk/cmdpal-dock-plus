using System.Runtime.InteropServices;

namespace CmdPalDockPlus.Windows.Previews;

public sealed class DwmThumbnailPreviewService : IThumbnailPreviewService
{
    private const uint WsPopup = 0x80000000;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExNoActivate = 0x08000000;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;
    private const uint DwmTnpRectDestination = 0x00000001;
    private const uint DwmTnpVisible = 0x00000008;
    private const uint DwmTnpOpacity = 0x00000004;

    private static readonly WndProc WindowProcedure = StaticWndProc;
    private static readonly object ClassGate = new();
    private static ushort _classAtom;
    private static readonly string ClassName = "CmdPalDockPlus.PreviewWindow";

    private readonly object _gate = new();
    private readonly List<nint> _thumbnails = [];
    private nint _window;
    private bool _disposed;

    public bool IsVisible { get; private set; }

    public ValueTask ShowAsync(PreviewRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ThrowIfDisposed();
            EnsureWindow();
            Apply(request);
            _ = ShowWindow(_window, SwShowNoActivate);
            IsVisible = request.SourceWindows.Count != 0;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(PreviewRequest request, CancellationToken cancellationToken = default)
        => ShowAsync(request, cancellationToken);

    public ValueTask HideAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ClearThumbnails();
            if (_window != 0)
            {
                _ = ShowWindow(_window, SwHide);
            }

            IsVisible = false;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed) return ValueTask.CompletedTask;
            _disposed = true;
            ClearThumbnails();
            if (_window != 0)
            {
                _ = DestroyWindow(_window);
                _window = 0;
            }

            IsVisible = false;
        }

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private void Apply(PreviewRequest request)
    {
        ClearThumbnails();
        if (request.SourceWindows.Count == 0)
        {
            _ = ShowWindow(_window, SwHide);
            IsVisible = false;
            return;
        }

        var layout = PreviewLayout.Calculate(request.SourceWindows.Count, request.MaximumSize);
        var x = request.Anchor.X + Math.Max(0, (request.Anchor.Width - layout.Bounds.Width) / 2);
        var y = Math.Max(0, request.Anchor.Y - layout.Bounds.Height - 8);
        _ = SetWindowPos(_window, -1, x, y, layout.Bounds.Width, layout.Bounds.Height, SwpNoActivate | SwpShowWindow);

        for (var i = 0; i < request.SourceWindows.Count; i++)
        {
            if (!IsWindow(request.SourceWindows[i])) continue;
            if (DwmRegisterThumbnail(_window, request.SourceWindows[i], out var thumbnail) < 0 || thumbnail == 0) continue;
            _thumbnails.Add(thumbnail);
            var cell = layout.Cells[i];
            var properties = new DwmThumbnailProperties
            {
                Flags = DwmTnpRectDestination | DwmTnpVisible | DwmTnpOpacity,
                Destination = new Rect(cell.X, cell.Y, cell.X + cell.Width, cell.Y + cell.Height),
                Opacity = 255,
                Visible = true,
            };
            _ = DwmUpdateThumbnailProperties(thumbnail, ref properties);
        }
    }

    private void EnsureWindow()
    {
        if (_window != 0) return;
        EnsureClass();
        var module = GetModuleHandle(null);
        _window = CreateWindowEx(
            WsExToolWindow | WsExNoActivate,
            ClassName,
            "CmdPal Dock Plus Preview",
            WsPopup,
            0,
            0,
            1,
            1,
            0,
            0,
            module,
            0);
        if (_window == 0)
        {
            throw new InvalidOperationException($"Could not create preview window (Win32 {Marshal.GetLastWin32Error()}).");
        }
    }

    private static void EnsureClass()
    {
        if (_classAtom != 0) return;
        lock (ClassGate)
        {
            if (_classAtom != 0) return;
            var wc = new WndClass
            {
                WndProc = WindowProcedure,
                Instance = GetModuleHandle(null),
                ClassName = ClassName,
            };
            _classAtom = RegisterClass(ref wc);
            if (_classAtom == 0)
            {
                var error = Marshal.GetLastWin32Error();
                const int ErrorClassAlreadyExists = 1410;
                if (error != ErrorClassAlreadyExists)
                {
                    throw new InvalidOperationException($"Could not register preview window class (Win32 {error}).");
                }
            }
        }
    }

    private void ClearThumbnails()
    {
        foreach (var thumbnail in _thumbnails)
        {
            _ = DwmUnregisterThumbnail(thumbnail);
        }

        _thumbnails.Clear();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static nint StaticWndProc(nint hwnd, uint message, nuint wParam, nint lParam)
        => DefWindowProc(hwnd, message, wParam, lParam);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WndProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClass
    {
        public uint Style;
        public WndProc WndProc;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public string? MenuName;
        public string ClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public Rect(int left, int top, int right, int bottom) { Left = left; Top = top; Right = right; Bottom = bottom; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DwmThumbnailProperties
    {
        public uint Flags;
        public Rect Destination;
        public Rect Source;
        public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool SourceClientAreaOnly;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? moduleName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClass(ref WndClass windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowEx(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] private static extern nint DefWindowProc(nint hwnd, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint hwnd);
    [DllImport("dwmapi.dll")] private static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmUnregisterThumbnail(nint thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmUpdateThumbnailProperties(nint thumbnail, ref DwmThumbnailProperties properties);
}
