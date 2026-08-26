using CmdPalDockPlus.Core.Compatibility;
using CmdPalDockPlus.Extension.Compatibility;
using CmdPalDockPlus.Windows.Previews;

namespace CmdPalDockPlus.Extension.Previews;

internal sealed class HoverPreviewCoordinator : IAsyncDisposable
{
    private readonly IHoverEventSource _source;
    private readonly IThumbnailPreviewService _preview;
    private readonly DockCoordinator _dock;
    private readonly HoverPreviewStateMachine _state = new();
    private readonly object _gate = new();
    private CancellationTokenSource? _transitionCts;
    private bool _disposed;

    public HoverPreviewCoordinator(IHoverEventSource source, IThumbnailPreviewService preview, DockCoordinator dock)
    {
        _source = source;
        _preview = preview;
        _dock = dock;
        _source.HoverChanged += OnHoverChanged;
    }

    public bool IsBridgeConnected => _source.IsConnected;

    public Task StartAsync(CancellationToken cancellationToken = default) => _source.StartAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _source.HoverChanged -= OnHoverChanged;
        CancellationTokenSource? transition;
        lock (_gate)
        {
            transition = _transitionCts;
            _transitionCts = null;
        }
        transition?.Cancel();
        transition?.Dispose();
        await _preview.HideAsync().ConfigureAwait(false);
        await _source.DisposeAsync().ConfigureAwait(false);
        await _preview.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private void OnHoverChanged(object? sender, HoverEvent evt)
    {
        _state.Apply(evt);
        CancellationTokenSource next;
        lock (_gate)
        {
            _transitionCts?.Cancel();
            _transitionCts?.Dispose();
            next = _transitionCts = new CancellationTokenSource();
        }

        _ = evt.Kind == HoverEventKind.Enter
            ? ShowAfterDelayAsync(evt.CommandId, next.Token)
            : HideAfterDelayAsync(evt.CommandId, next.Token);
    }

    private async Task ShowAfterDelayAsync(string commandId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            if (!string.Equals(_state.CurrentCommandId, commandId, StringComparison.Ordinal)) return;
            if (_state.CurrentAnchor is not { } anchor) return;
            if (!_dock.TryGetStateByCommandId(commandId, out var tile)) return;

            var windows = tile.Windows.Select(window => window.Hwnd).ToArray();
            if (windows.Length == 0) return;
            await _preview.ShowAsync(
                new PreviewRequest(
                    windows,
                    new PreviewRect(anchor.X, anchor.Y, anchor.Width, anchor.Height),
                    new PreviewSize(960, 660)),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task HideAfterDelayAsync(string commandId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).ConfigureAwait(false);
            if (_state.CurrentCommandId is not null && !string.Equals(_state.CurrentCommandId, commandId, StringComparison.Ordinal)) return;
            await _preview.HideAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
