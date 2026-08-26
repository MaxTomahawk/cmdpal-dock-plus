using CmdPalDockPlus.Core.Compatibility;

namespace CmdPalDockPlus.Extension.Compatibility;

internal interface IHoverEventSource : IAsyncDisposable
{
    event EventHandler<HoverEvent>? HoverChanged;
    bool IsConnected { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
}
