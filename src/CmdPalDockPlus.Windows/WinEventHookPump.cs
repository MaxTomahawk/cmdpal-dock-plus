using System.Runtime.InteropServices;

namespace CmdPalDockPlus.Windows;

internal delegate void WinEventHookCallback(
    nint hook,
    uint eventType,
    nint hwnd,
    int objectId,
    int childId,
    uint threadId,
    uint eventTime);

internal interface IWinEventHookNative
{
    uint CurrentThreadId { get; }
    void EnsureMessageQueue();
    nint SetHook(uint eventMin, uint eventMax, WinEventHookCallback callback);
    void Unhook(nint hook);
    bool PumpOnce();
    void RequestQuit(uint threadId);
}

internal sealed class WinEventHookPump : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventSystemMinimizeStart = 0x0016;
    private const uint EventSystemMinimizeEnd = 0x0017;
    private const uint EventObjectCreate = 0x8000;
    private const uint EventObjectDestroy = 0x8001;
    private const uint EventObjectShow = 0x8002;
    private const uint EventObjectHide = 0x8003;
    private const uint EventObjectNameChange = 0x800C;
    private const int ObjidWindow = 0;

    private readonly IWinEventHookNative _native;
    private readonly Action _windowChanged;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly List<nint> _hooks = [];
    private readonly WinEventHookCallback _callback;
    private uint _threadId;
    private Exception? _startupError;
    private bool _disposed;

    public WinEventHookPump(Action windowChanged)
        : this(new Win32WinEventHookNative(), windowChanged)
    {
    }

    internal WinEventHookPump(IWinEventHookNative native, Action windowChanged)
    {
        _native = native;
        _windowChanged = windowChanged;
        _callback = OnWinEvent;
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "CmdPal Dock Plus WinEvent pump",
        };
        _thread.Start();

        if (!_ready.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new TimeoutException("Timed out starting the WinEvent message pump.");
        }

        if (_startupError is not null)
        {
            throw new InvalidOperationException("Could not start the WinEvent message pump.", _startupError);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_threadId != 0)
        {
            _native.RequestQuit(_threadId);
        }

        _ = _thread.Join(TimeSpan.FromSeconds(5));
        _ready.Dispose();
    }

    private void Run()
    {
        try
        {
            _threadId = _native.CurrentThreadId;
            _native.EnsureMessageQueue();

            AddHook(EventSystemForeground, EventSystemForeground);
            AddHook(EventSystemMinimizeStart, EventSystemMinimizeEnd);
            AddHook(EventObjectCreate, EventObjectNameChange);
            _ready.Set();

            while (_native.PumpOnce())
            {
            }
        }
        catch (Exception ex)
        {
            _startupError = ex;
            _ready.Set();
        }
        finally
        {
            foreach (var hook in _hooks)
            {
                _native.Unhook(hook);
            }

            _hooks.Clear();
        }
    }

    private void AddHook(uint eventMin, uint eventMax)
    {
        var hook = _native.SetHook(eventMin, eventMax, _callback);
        if (hook != 0)
        {
            _hooks.Add(hook);
        }
    }

    private void OnWinEvent(nint hook, uint eventType, nint hwnd, int objectId, int childId, uint threadId, uint eventTime)
    {
        _ = hook;
        _ = childId;
        _ = threadId;
        _ = eventTime;

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
            _windowChanged();
        }
    }
}

internal sealed class Win32WinEventHookNative : IWinEventHookNative
{
    private const uint WineventOutOfContext = 0x0000;
    private const uint WineventSkipOwnProcess = 0x0002;
    private const uint WmQuit = 0x0012;
    private const uint PmNoRemove = 0x0000;

    public uint CurrentThreadId => GetCurrentThreadId();

    public void EnsureMessageQueue()
    {
        _ = PeekMessage(out _, 0, 0, 0, PmNoRemove);
    }

    public nint SetHook(uint eventMin, uint eventMax, WinEventHookCallback callback)
        => SetWinEventHook(
            eventMin,
            eventMax,
            0,
            callback,
            0,
            0,
            WineventOutOfContext | WineventSkipOwnProcess);

    public void Unhook(nint hook)
    {
        _ = UnhookWinEvent(hook);
    }

    public bool PumpOnce()
    {
        var result = GetMessage(out var message, 0, 0, 0);
        if (result <= 0)
        {
            return false;
        }

        _ = TranslateMessage(ref message);
        _ = DispatchMessage(ref message);
        return true;
    }

    public void RequestQuit(uint threadId)
    {
        _ = PostThreadMessage(threadId, WmQuit, 0, 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public nint Hwnd;
        public uint Value;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public Point Pt;
        public uint Private;
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern nint SetWinEventHook(
        uint eventMin,
        uint eventMax,
        nint module,
        WinEventHookCallback callback,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(nint hook);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out Message message, nint hwnd, uint min, uint max, uint removeMsg);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out Message message, nint hwnd, uint min, uint max);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref Message message);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(ref Message message);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint threadId, uint message, nuint wParam, nint lParam);
}
