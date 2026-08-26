using System.Threading.Channels;

namespace CmdPalDockPlus.Windows;

public sealed class WindowTracker : IWindowTracker
{
    private readonly IWindowBackend _backend;
    private readonly TimeSpan _debounce;
    private readonly Channel<bool> _requests = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false,
    });
    private readonly SemaphoreSlim _reconcileLock = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly object _snapshotGate = new();
    private IReadOnlyList<WindowSnapshot> _snapshot = [];
    private Task? _worker;
    private bool _started;

    public WindowTracker(IWindowBackend backend, TimeSpan? debounce = null)
    {
        _backend = backend;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(50);
        _backend.WindowChanged += OnBackendWindowChanged;
    }

    public event EventHandler<WindowSetChanged>? Changed;

    public IReadOnlyList<WindowSnapshot> Snapshot
    {
        get
        {
            lock (_snapshotGate)
            {
                return _snapshot;
            }
        }
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        await ReconcileAsync(cancellationToken).ConfigureAwait(false);
        _worker = Task.Run(() => WorkerAsync(_disposeCts.Token), CancellationToken.None);
    }

    public void RequestReconcile() => _requests.Writer.TryWrite(true);

    public async ValueTask ReconcileAsync(CancellationToken cancellationToken)
    {
        await _reconcileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var latest = (await _backend.EnumerateAsync(cancellationToken).ConfigureAwait(false))
                .OrderBy(window => (long)window.Hwnd)
                .ToArray();

            lock (_snapshotGate)
            {
                if (SequenceEqual(_snapshot, latest))
                {
                    return;
                }

                _snapshot = latest;
            }

            Changed?.Invoke(this, new WindowSetChanged(latest));
        }
        finally
        {
            _reconcileLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _backend.WindowChanged -= OnBackendWindowChanged;
        _disposeCts.Cancel();
        _requests.Writer.TryComplete();
        if (_worker is not null)
        {
            try
            {
                await _worker.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        await _backend.DisposeAsync().ConfigureAwait(false);
        _disposeCts.Dispose();
        _reconcileLock.Dispose();
    }

    private async Task WorkerAsync(CancellationToken cancellationToken)
    {
        await foreach (var request in _requests.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            _ = request;
            await Task.Delay(_debounce, cancellationToken).ConfigureAwait(false);
            while (_requests.Reader.TryRead(out var ignored))
            {
                _ = ignored;
            }

            await ReconcileAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnBackendWindowChanged(object? sender, EventArgs e) => RequestReconcile();

    private static bool SequenceEqual(IReadOnlyList<WindowSnapshot> left, IReadOnlyList<WindowSnapshot> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }
}
