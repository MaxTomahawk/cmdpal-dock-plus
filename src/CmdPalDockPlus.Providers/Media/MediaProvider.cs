using CmdPalDockPlus.Windows;
using CmdPalDockPlus.Windows.Media;

namespace CmdPalDockPlus.Providers.Media;

public sealed class MediaProvider : IWindowDataAdapter, IInvalidatingWindowDataAdapter, IDisposable, IAsyncDisposable
{
    private readonly IMediaSessionService _service;
    private bool _disposed;

    public MediaProvider(IMediaSessionService service)
    {
        _service = service;
        _service.Changed += OnChanged;
    }

    public string Id => "media";

    public IReadOnlyList<CapabilityDescriptor> Capabilities { get; } =
    [
        new("media.title", "Media title", "Current media title for this application.", "event-driven"),
        new("media.artist", "Artist", "Current media artist.", "event-driven"),
        new("media.album", "Album", "Current media album.", "event-driven"),
        new("media.playbackState", "Playback state", "Playing/paused/stopped state.", "event-driven"),
        new("media.sourceApp", "Media source", "Windows media-session source application id.", "event-driven"),
    ];

    public event EventHandler? DataInvalidated;

    public bool Supports(WindowSnapshot window) => Find(window) is not null;

    public void Enrich(WindowSnapshot window, IReadOnlySet<string> requestedFields, IDictionary<string, object?> values)
    {
        var session = Find(window);
        if (session is null)
        {
            return;
        }

        if (requestedFields.Contains("media.title")) values["media.title"] = session.Title;
        if (requestedFields.Contains("media.artist")) values["media.artist"] = session.Artist;
        if (requestedFields.Contains("media.album")) values["media.album"] = session.Album;
        if (requestedFields.Contains("media.playbackState")) values["media.playbackState"] = session.PlaybackState;
        if (requestedFields.Contains("media.sourceApp")) values["media.sourceApp"] = session.SourceAppId;
    }

    public IReadOnlyList<MediaActionDescriptor> Actions(WindowSnapshot window)
    {
        var session = Find(window);
        if (session is null)
        {
            return [];
        }

        var actions = new List<MediaActionDescriptor>(3);
        if (session.CanPlayPause) actions.Add(new("media.playPause", "Play / Pause", () => _service.PlayPauseAsync(session.SourceAppId)));
        if (session.CanPrevious) actions.Add(new("media.previous", "Previous", () => _service.PreviousAsync(session.SourceAppId)));
        if (session.CanNext) actions.Add(new("media.next", "Next", () => _service.NextAsync(session.SourceAppId)));
        return actions;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _service.Changed -= OnChanged;
        _service.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _service.Changed -= OnChanged;
        await _service.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private MediaSessionSnapshot? Find(WindowSnapshot window)
    {
        var executableStem = Path.GetFileNameWithoutExtension(window.ExecutableName);
        if (string.IsNullOrWhiteSpace(executableStem)) return null;

        var candidates = _service.Snapshot
            .Where(session => Matches(session.SourceAppId, executableStem, window.ExecutableName))
            .ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static bool Matches(string sourceAppId, string stem, string executableName)
        => string.Equals(sourceAppId, executableName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sourceAppId, stem, StringComparison.OrdinalIgnoreCase)
            || sourceAppId.Contains(stem, StringComparison.OrdinalIgnoreCase);

    private void OnChanged(object? sender, EventArgs e) => DataInvalidated?.Invoke(this, EventArgs.Empty);
}

public sealed record MediaActionDescriptor(string Id, string DisplayName, Func<ValueTask<bool>> InvokeAsync);
