using CmdPalDockPlus.Core.Profiles;
using CmdPalDockPlus.Providers;
using CmdPalDockPlus.Windows;

namespace CmdPalDockPlus.Extension;

internal sealed class DockPlusRuntime : IAsyncDisposable
{
    public DockPlusRuntime()
    {
        var settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CmdPalDockPlus");
        ProfileStore = new ProfileStore(Path.Combine(settingsDirectory, "profiles.json"));
        Backend = new Win32WindowBackend(); Tracker = new WindowTracker(Backend); Activator = new WindowActivator(Backend); Launcher = new AppLauncher(); Providers = new ProviderHost();
        Coordinator = new DockCoordinator(ProfileStore, Tracker, Activator, Launcher, Providers);
    }
    public ProfileStore ProfileStore { get; } public Win32WindowBackend Backend { get; } public WindowTracker Tracker { get; } public WindowActivator Activator { get; } public AppLauncher Launcher { get; } public ProviderHost Providers { get; } public DockCoordinator Coordinator { get; }
    public Task InitializeAsync() => Coordinator.InitializeAsync();
    public async ValueTask DisposeAsync() { await Coordinator.DisposeAsync().ConfigureAwait(false); await Providers.DisposeAsync().ConfigureAwait(false); }
}
