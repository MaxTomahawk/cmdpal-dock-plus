using System.Collections.Concurrent;
using FluentAssertions;

namespace CmdPalDockPlus.Windows.Tests;

public sealed class WinEventHookPumpTests
{
    [Fact]
    public void WindowEventsAreDeliveredFromDedicatedPumpedThread()
    {
        var native = new FakeWinEventHookNative();
        using var changed = new ManualResetEventSlim(false);
        var callerThread = Environment.CurrentManagedThreadId;
        var callbackThread = 0;

        using var pump = new WinEventHookPump(native, () =>
        {
            callbackThread = Environment.CurrentManagedThreadId;
            changed.Set();
        });

        native.MessageQueueThread.Should().NotBe(0);
        native.HookThread.Should().Be(native.MessageQueueThread);
        native.HookThread.Should().NotBe(callerThread);
        native.HookCount.Should().Be(3);

        native.Raise(0x800C, (nint)42, objectId: 0);

        changed.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
        callbackThread.Should().Be(native.HookThread);
    }

    [Fact]
    public void DisposeRequestsQuitAndUnhooksEverything()
    {
        var native = new FakeWinEventHookNative();
        var pump = new WinEventHookPump(native, () => { });

        pump.Dispose();

        native.QuitRequested.Should().BeTrue();
        native.UnhookCount.Should().Be(3);
    }

    private sealed class FakeWinEventHookNative : IWinEventHookNative
    {
        private readonly ConcurrentQueue<Action> _messages = new();
        private readonly AutoResetEvent _signal = new(false);
        private readonly List<WinEventHookCallback> _callbacks = [];
        private int _nextHook = 1;
        private volatile bool _quit;

        public int MessageQueueThread { get; private set; }
        public int HookThread { get; private set; }
        public int HookCount => _callbacks.Count;
        public int UnhookCount { get; private set; }
        public bool QuitRequested { get; private set; }
        public uint CurrentThreadId => unchecked((uint)Environment.CurrentManagedThreadId);

        public void EnsureMessageQueue()
        {
            MessageQueueThread = Environment.CurrentManagedThreadId;
        }

        public nint SetHook(uint eventMin, uint eventMax, WinEventHookCallback callback)
        {
            _ = eventMin;
            _ = eventMax;
            HookThread = Environment.CurrentManagedThreadId;
            _callbacks.Add(callback);
            return (nint)_nextHook++;
        }

        public void Unhook(nint hook)
        {
            _ = hook;
            UnhookCount++;
        }

        public bool PumpOnce()
        {
            _signal.WaitOne(TimeSpan.FromSeconds(5));
            if (_messages.TryDequeue(out var action))
            {
                action();
            }

            return !_quit;
        }

        public void RequestQuit(uint threadId)
        {
            _ = threadId;
            QuitRequested = true;
            _messages.Enqueue(() => _quit = true);
            _signal.Set();
        }

        public void Raise(uint eventType, nint hwnd, int objectId)
        {
            var callbacks = _callbacks.ToArray();
            _messages.Enqueue(() =>
            {
                foreach (var callback in callbacks)
                {
                    callback(0, eventType, hwnd, objectId, 0, 0, 0);
                }
            });
            _signal.Set();
        }
    }
}
