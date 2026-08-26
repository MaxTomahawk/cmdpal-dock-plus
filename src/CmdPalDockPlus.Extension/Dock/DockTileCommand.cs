using CmdPalDockPlus.Core.Tiles;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalDockPlus.Extension;

internal sealed partial class DockTileCommand : InvokableCommand
{
    private readonly DockCoordinator _coordinator;
    private readonly TileIdentity _identity;

    public DockTileCommand(DockCoordinator coordinator, TileIdentity identity)
    {
        _coordinator = coordinator;
        _identity = identity;
        Id = DockCommandId.ForTile(identity);
        Name = "Open app or focus window";
        Icon = new IconInfo("\uE8A5");
    }

    public override CommandResult Invoke()
    {
        _ = _coordinator.ActivateTileAsync(_identity);
        return CommandResult.Dismiss();
    }
}
