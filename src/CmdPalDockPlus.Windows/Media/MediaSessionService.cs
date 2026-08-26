using Windows.Media.Control;

namespace CmdPalDockPlus.Windows.Media;

public sealed class MediaSessionService : IMediaSessionService
{
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly object _stateGate = new();
    private readonly HashSet<GlobalSystemMediaTransportControlsSession> _wiredSessions = [];
    private readonly Task _initialization;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private IReadOnlyList<MediaSessionSnapshot> _snapshot = [];
    private Dictionary<string, GlobalSystemMediaTransportControlsSession> _sessionsById = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public MediaSessionService()
    {
        _initialization = InitializeAsync();
    }

    public event EventHandler? Changed;

    public IReadOnlyList<MediaSessionSnapshot> Snapshot
    {
        get
        {
            lock (_stateGate)
            {
                return _snapshot;
            }
        }
    }

    public async ValueTask<bool> PlayPauseAsync(string sourceAppId, CancellationToken cancellationToken = default)
        => await InvokeAsync(sourceAppId, static session => session.TryTogglePlayPauseAsync(), cancellationToken).ConfigureAwait(false);

    public async ValueTask<bool> NextAsync(string sourceAppId, CancellationToken cancellationToken = default)
        => await InvokeAsync(sourceAppId, static session => session.TrySkipNextAsync(), cancellationToken).ConfigureAwait(false);

    public async ValueTask<bool> PreviousAsync(string sourceAppId, CancellationToken cancellationToken = default)
        => await InvokeAsync(sourceAppId, static session => session.TrySkipPreviousAsync(), cancellationToken).ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            await _initialization.ConfigureAwait(false);
        }
        catch
        {
            // Initialization failures are intentionally converted to an empty provider.
        }

        if (_manager is not null)
        {
            _manager.SessionsChanged -= OnSessionsChanged;
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
        }

        foreach (var session in _wiredSessions)
        {
            session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        }

        _wiredSessions.Clear();
        _refreshGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_disposed)
            {
                return;
            }

            _manager.SessionsChanged += OnSessionsChanged;
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            await RefreshAsync().ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            // Capability unavailable: provider stays empty and generic window data still works.
        }
        catch (InvalidOperationException)
        {
            // Media infrastructure unavailable in the current desktop/session.
        }
    }

    private async Task RefreshAsync()
    {
        if (_disposed || _manager is null)
        {
            return;
        }

        await _refreshGate.WaitAsync().ConfigureAwait(false);
        bool changed = false;
        try
        {
            var sessions = _manager.GetSessions().ToArray();
            var live = sessions.ToHashSet();

            foreach (var stale in _wiredSessions.Where(session => !live.Contains(session)).ToArray())
            {
                stale.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                stale.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                _wiredSessions.Remove(stale);
            }

            foreach (var session in sessions.Where(session => !_wiredSessions.Contains(session)))
            {
                session.MediaPropertiesChanged += OnMediaPropertiesChanged;
                session.PlaybackInfoChanged += OnPlaybackInfoChanged;
                _wiredSessions.Add(session);
            }

            var next = new List<MediaSessionSnapshot>(sessions.Length);
            var map = new Dictionary<string, GlobalSystemMediaTransportControlsSession>(StringComparer.OrdinalIgnoreCase);
            foreach (var session in sessions)
            {
                try
                {
                    var properties = await session.TryGetMediaPropertiesAsync();
                    var playback = session.GetPlaybackInfo();
                    var controls = playback.Controls;
                    var source = session.SourceAppUserModelId ?? string.Empty;
                    next.Add(new MediaSessionSnapshot(
                        source,
                        properties?.Title,
                        properties?.Artist,
                        properties?.AlbumTitle,
                        playback.PlaybackStatus.ToString(),
                        controls?.IsPlayPauseToggleEnabled == true,
                        controls?.IsNextEnabled == true,
                        controls?.IsPreviousEnabled == true));
                    if (!string.IsNullOrWhiteSpace(source))
                    {
                        map[source] = session;
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
                {
                    // A disappearing or inaccessible session is omitted from this snapshot.
                }
            }

            lock (_stateGate)
            {
                changed = !_snapshot.SequenceEqual(next);
                _snapshot = next;
                _sessionsById = map;
            }
        }
        finally
        {
            _refreshGate.Release();
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private async ValueTask<bool> InvokeAsync(
        string sourceAppId,
        Func<GlobalSystemMediaTransportControlsSession, Windows.Foundation.IAsyncOperation<bool>> command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _initialization.ConfigureAwait(false);
        GlobalSystemMediaTransportControlsSession? session;
        lock (_stateGate)
        {
            _sessionsById.TryGetValue(sourceAppId, out session);
        }

        if (session is null)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await command(session);
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) => QueueRefresh();

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) => QueueRefresh();

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => QueueRefresh();

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => QueueRefresh();

    private void QueueRefresh()
    {
        if (!_disposed)
        {
            _ = RefreshAsync();
        }
    }
}
