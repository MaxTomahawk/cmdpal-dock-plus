using CmdPalDockPlus.Windows.Destinations;

namespace CmdPalDockPlus.Extension;

internal sealed partial class DockCoordinator
{
    public Task OpenDestinationAsync(AppDestination destination)
        => _launcher.OpenDestinationAsync(destination, default).AsTask();
}
