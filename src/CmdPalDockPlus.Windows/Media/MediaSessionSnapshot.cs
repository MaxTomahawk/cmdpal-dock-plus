namespace CmdPalDockPlus.Windows.Media;

public sealed record MediaSessionSnapshot(
    string SourceAppId,
    string? Title,
    string? Artist,
    string? Album,
    string PlaybackState,
    bool CanPlayPause,
    bool CanNext,
    bool CanPrevious);

public interface IMediaSessionService : IAsyncDisposable
{
    event EventHandler? Changed;

    IReadOnlyList<MediaSessionSnapshot> Snapshot { get; }

    ValueTask<bool> PlayPauseAsync(string sourceAppId, CancellationToken cancellationToken = default);
    ValueTask<bool> NextAsync(string sourceAppId, CancellationToken cancellationToken = default);
    ValueTask<bool> PreviousAsync(string sourceAppId, CancellationToken cancellationToken = default);
}
