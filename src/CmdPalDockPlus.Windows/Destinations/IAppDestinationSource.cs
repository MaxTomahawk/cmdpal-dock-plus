using CmdPalDockPlus.Core.Profiles;

namespace CmdPalDockPlus.Windows.Destinations;

public interface IAppDestinationSource
{
    ValueTask<IReadOnlyList<AppDestination>> GetRecentAsync(ApplicationMatch application, int limit, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<AppDestination>> GetFrequentAsync(ApplicationMatch application, int limit, CancellationToken cancellationToken = default);
}
