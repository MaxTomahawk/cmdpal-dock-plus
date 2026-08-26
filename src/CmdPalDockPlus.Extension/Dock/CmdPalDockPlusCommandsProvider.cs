using CmdPalDockPlus.Extension.SystemStatus;
using CmdPalDockPlus.Extension.Tray;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalDockPlus.Extension;

public sealed partial class CmdPalDockPlusCommandsProvider : CommandProvider
{
    private readonly DockPlusRuntime _runtime = new();
    private readonly SystemStatusBandController _systemStatus = new();
    private readonly TrayBandController _tray = new();
    private readonly ConfigurationPage _configurationPage;
    private readonly CommandItem _configurationCommand;
    private readonly WrappedDockItem _applicationsBand;
    private readonly WrappedDockItem _systemBand;
    private readonly Dictionary<string, CommandItem> _pinItems = new(StringComparer.Ordinal);

    public CmdPalDockPlusCommandsProvider()
    {
        DisplayName = "CmdPal Dock Plus";
        Id = "com.maxtomahawk.cmdpal.dockplus";
        Icon = new IconInfo("\uE8A5");
        Frozen = false;
        _configurationPage = new ConfigurationPage(_runtime);
        _configurationCommand = new CommandItem(_configurationPage)
        {
            Title = "Configure CmdPal Dock Plus",
            Subtitle = "Create Smart app/window tiles and choose live data",
            Icon = Icon,
        };
        _applicationsBand = new WrappedDockItem([], "com.maxtomahawk.cmdpal.dockplus.apps", "Smart applications")
        {
            Icon = Icon,
        };
        _systemBand = new WrappedDockItem(
            _systemStatus.Items,
            "com.maxtomahawk.cmdpal.dockplus.system",
            "System status")
        {
            Icon = new IconInfo("\uE713"),
        };
        _runtime.Coordinator.TilesChanged += OnTilesChanged;
        _ = InitializeRuntimeAsync();
    }

    public override ICommandItem[] TopLevelCommands() => [_configurationCommand];

    public override ICommandItem[] GetDockBands() => [_applicationsBand, _systemBand, _tray.Band];

    public override ICommandItem? GetCommandItem(string id)
        => _pinItems.TryGetValue(id, out var item) ? item : null;

    public override void Dispose()
    {
        _runtime.Coordinator.TilesChanged -= OnTilesChanged;
        _tray.Dispose();
        _systemStatus.Dispose();
        _runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
        base.Dispose();
    }

    private async Task InitializeRuntimeAsync()
    {
        await _runtime.InitializeAsync().ConfigureAwait(false);
        OnTilesChanged(this, EventArgs.Empty);
    }

    private void OnTilesChanged(object? sender, EventArgs e)
    {
        _applicationsBand.Items = _runtime.Coordinator.Items.Cast<IListItem>().ToArray();
        _pinItems.Clear();
        foreach (var item in _runtime.Coordinator.Items)
        {
            if (item.Command is null || string.IsNullOrWhiteSpace(item.Command.Id))
            {
                continue;
            }

            _pinItems[item.Command.Id] = new CommandItem(item.Command)
            {
                Title = item.Title,
                Subtitle = item.Subtitle,
                Icon = item.Icon,
                MoreCommands = item.MoreCommands,
            };
        }
    }
}
