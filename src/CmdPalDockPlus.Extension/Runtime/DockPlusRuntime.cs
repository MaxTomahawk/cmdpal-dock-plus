using CmdPalDockPlus.Core.Profiles;
using CmdPalDockPlus.Extension.Compatibility;
using CmdPalDockPlus.Extension.Previews;
using CmdPalDockPlus.Providers;
using CmdPalDockPlus.Windows;
using CmdPalDockPlus.Windows.Previews;

namespace CmdPalDockPlus.Extension;

internal sealed class DockPlusRuntime : IAsyncDisposable
{
    public DockPlusRuntime()
    {
        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CmdPalDockPlus");

        ProfileStore = new ProfileStore(Path.Combine(settingsDirectory, "profiles.json"));
        Backend = new Win32WindowBackend();
        Tracker = new WindowTracker(Backend);
        Activator = new WindowActivator(Backend);
        Launcher = new AppLauncher();
        Providers = new ProviderHost();
        Coordinator = new DockCoordinator(ProfileStore, Tracker, Activator, Launcher, Providers);
        HoverSource = new NamedPipeHoverEventSource();
        PreviewService = new DwmThumbnailPreviewService();
        HoverPreviews = new HoverPreviewCoordinator(HoverSource, PreviewService, Coordinator);
    }

    public ProfileStore ProfileStore { get; }
    public Win32WindowBackend Backend { get; }
    public WindowTracker Tracker { get; }
    public WindowActivator Activator { get; }
    public AppLauncher Launcher { get; }
    public ProviderHost Providers { get; }
    public DockCoordinator Coordinator { get; }
    public NamedPipeHoverEventSource HoverSource { get; }
    public DwmThumbnailPreviewService PreviewService { get; }
    public HoverPreviewCoordinator HoverPreviews { get; }

    public async Task InitializeAsync()
    {
        await Coordinator.InitializeAsync().ConfigureAwait(false);
        await HoverPreviews.StartAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await HoverPreviews.DisposeAsync().ConfigureAwait(false);
        await Coordinator.DisposeAsync().ConfigureAwait(false);
        await Providers.DisposeAsync().ConfigureAwait(false);
    }
}
