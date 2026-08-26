using CmdPalDockPlus.Windows.SystemStatus;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace CmdPalDockPlus.Extension.SystemStatus;

internal sealed partial class SystemStatusBandController : IDisposable
{
    private readonly VolumeService? _volume;
    private readonly NetworkStatusService _network;
    private readonly PowerStatusService _power;
    private readonly VolumeDockItem? _volumeItem;
    private readonly NetworkDockItem _networkItem;
    private readonly PowerDockItem _powerItem;
    private bool _disposed;

    public SystemStatusBandController()
    {
        try
        {
            _volume = new VolumeService();
            _volumeItem = new VolumeDockItem(_volume);
        }
        catch
        {
            _volume = null;
            _volumeItem = null;
        }

        _network = new NetworkStatusService();
        _power = new PowerStatusService();
        _networkItem = new NetworkDockItem(_network);
        _powerItem = new PowerDockItem(_power);
    }

    public IListItem[] Items
        => _volumeItem is null
            ? [_networkItem, _powerItem]
            : [_volumeItem, _networkItem, _powerItem];

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _volumeItem?.Dispose();
        _networkItem.Dispose();
        _powerItem.Dispose();
        _volume?.Dispose();
        _network.Dispose();
        _power.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed partial class VolumeDockItem : ListItem, IDisposable
    {
        private readonly VolumeService _service;

        public VolumeDockItem(VolumeService service)
            : base(new AnonymousCommand(service.ToggleMute)
            {
                Id = "system:volume:toggle-mute",
                Name = "Toggle mute",
                Result = CommandResult.KeepOpen(),
            })
        {
            _service = service;
            MoreCommands =
            [
                Context("Volume down 5%", "system:volume:down", () => _service.ChangePercent(-5)),
                Context("Volume up 5%", "system:volume:up", () => _service.ChangePercent(5)),
                Context("Sound settings", "system:volume:settings", () => OpenSettings("ms-settings:sound")),
            ];
            _service.Changed += OnChanged;
            Refresh();
        }

        public void Dispose()
        {
            _service.Changed -= OnChanged;
            GC.SuppressFinalize(this);
        }

        private void OnChanged(object? sender, EventArgs e) => Refresh();

        private void Refresh()
        {
            var snapshot = _service.Snapshot;
            Title = snapshot.IsMuted ? "Muted" : $"{snapshot.ClampedPercent}%";
            Subtitle = "Volume";
            Icon = new IconInfo(snapshot.IsMuted ? "\uE74F" : "\uE767");
        }
    }

    private sealed partial class NetworkDockItem : ListItem, IDisposable
    {
        private readonly NetworkStatusService _service;

        public NetworkDockItem(NetworkStatusService service)
            : base(new AnonymousCommand(() => OpenSettings("ms-settings:network-status"))
            {
                Id = "system:network:settings",
                Name = "Network settings",
                Result = CommandResult.Dismiss(),
            })
        {
            _service = service;
            _service.Changed += OnChanged;
            Refresh();
        }

        public void Dispose()
        {
            _service.Changed -= OnChanged;
            GC.SuppressFinalize(this);
        }

        private void OnChanged(object? sender, EventArgs e) => Refresh();

        private void Refresh()
        {
            var snapshot = _service.Snapshot;
            Title = snapshot.HasInternet ? snapshot.ProfileName : "Offline";
            Subtitle = snapshot.Connectivity.ToString();
            Icon = new IconInfo(snapshot.HasInternet ? "\uE701" : "\uEB55");
        }
    }

    private sealed partial class PowerDockItem : ListItem, IDisposable
    {
        private readonly PowerStatusService _service;

        public PowerDockItem(PowerStatusService service)
            : base(new AnonymousCommand(() => OpenSettings("ms-settings:batterysaver"))
            {
                Id = "system:power:settings",
                Name = "Power and battery settings",
                Result = CommandResult.Dismiss(),
            })
        {
            _service = service;
            _service.Changed += OnChanged;
            Refresh();
        }

        public void Dispose()
        {
            _service.Changed -= OnChanged;
            GC.SuppressFinalize(this);
        }

        private void OnChanged(object? sender, EventArgs e) => Refresh();

        private void Refresh()
        {
            var snapshot = _service.Snapshot;
            Title = snapshot.BatteryStatus == global::Windows.System.Power.BatteryStatus.NotPresent
                ? "AC"
                : $"{snapshot.ClampedPercent}%";
            Subtitle = snapshot.IsCharging ? "Charging" : snapshot.PowerSupplyStatus.ToString();
            Icon = new IconInfo(snapshot.IsCharging ? "\uE83E" : "\uE850");
        }
    }

    private static CommandContextItem Context(string title, string id, Action action)
    {
        var command = new AnonymousCommand(action)
        {
            Id = id,
            Name = title,
            Result = CommandResult.KeepOpen(),
        };
        return new CommandContextItem(command) { Title = title };
    }

    private static void OpenSettings(string uri)
        => _ = Launcher.LaunchUriAsync(new Uri(uri));
}
