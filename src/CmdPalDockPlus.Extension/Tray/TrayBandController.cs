using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalDockPlus.Extension.Tray;

internal sealed partial class TrayBandController : IDisposable
{
    private readonly UiaTrayService _service = new();
    private bool _disposed;

    public TrayBandController()
    {
        Band = new WrappedDockItem([], "com.maxtomahawk.cmdpal.dockplus.tray", "Notification area")
        {
            Icon = new IconInfo("\uE7F4"),
        };
        _service.Changed += OnChanged;
        Rebuild();
    }

    public WrappedDockItem Band { get; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _service.Changed -= OnChanged;
        _service.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnChanged(object? sender, EventArgs e) => Rebuild();

    private void Rebuild()
    {
        var items = new List<IListItem>();
        foreach (var entry in _service.Snapshot)
        {
            var key = entry.Key;
            var command = new AnonymousCommand(() => _ = _service.TryInvoke(key))
            {
                Id = $"tray:{key}",
                Name = entry.DisplayName,
                Result = CommandResult.KeepOpen(),
            };
            items.Add(new ListItem(command)
            {
                Title = entry.DisplayName,
                Subtitle = entry.IsVisible ? "Tray" : "Tray · overflow",
                Icon = new IconInfo("\uE7F4"),
            });
        }

        var hiddenCommand = new AnonymousCommand(() => _ = _service.TryShowHiddenIcons())
        {
            Id = "tray:show-hidden",
            Name = "Show hidden icons",
            Result = CommandResult.KeepOpen(),
        };
        items.Add(new ListItem(hiddenCommand)
        {
            Title = "Hidden icons…",
            Subtitle = "Open the Windows overflow panel",
            Icon = new IconInfo("\uE70D"),
        });

        Band.Items = items.ToArray();
    }
}
