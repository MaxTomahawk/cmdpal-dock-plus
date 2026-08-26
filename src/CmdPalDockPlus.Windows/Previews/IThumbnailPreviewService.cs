namespace CmdPalDockPlus.Windows.Previews;

public sealed record PreviewRequest(IReadOnlyList<nint> SourceWindows, PreviewRect Anchor, PreviewSize MaximumSize);

public interface IThumbnailPreviewService : IAsyncDisposable
{
    bool IsVisible { get; }
    ValueTask ShowAsync(PreviewRequest request, CancellationToken cancellationToken = default);
    ValueTask UpdateAsync(PreviewRequest request, CancellationToken cancellationToken = default);
    ValueTask HideAsync(CancellationToken cancellationToken = default);
}
