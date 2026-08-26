using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions;

namespace CmdPalDockPlus.Extension;

[Guid("8B74A82B-2D3E-47FD-913D-1BC84D15EF7A")]
public sealed partial class CmdPalDockPlusExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _disposedEvent;
    private readonly CmdPalDockPlusCommandsProvider _provider;

    public CmdPalDockPlusExtension(ManualResetEvent disposedEvent)
    {
        _disposedEvent = disposedEvent;
        _provider = new CmdPalDockPlusCommandsProvider();
    }

    public object? GetProvider(ProviderType providerType)
        => providerType == ProviderType.Commands ? _provider : null;

    public void Dispose()
    {
        _provider.Dispose();
        _disposedEvent.Set();
    }
}
