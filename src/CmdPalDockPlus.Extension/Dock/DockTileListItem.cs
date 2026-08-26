using CmdPalDockPlus.Core.Tiles;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalDockPlus.Extension;

internal sealed partial class DockTileListItem : ListItem
{
    private readonly DockCoordinator _coordinator;
    private DockTileState _state;

    public DockTileListItem(DockCoordinator coordinator, DockTileState state)
        : base(new DockTileCommand(coordinator, state.Identity))
    {
        _coordinator = coordinator;
        _state = state;
        Update(state);
    }

    public TileIdentity Identity => _state.Identity;

    public void Update(DockTileState state)
    {
        _state = state;
        Title = state.Title;
        Subtitle = state.Subtitle;
        Icon = new IconInfo("\uE8A5");
        MoreCommands = BuildContextCommands();
    }

    private IContextItem[] BuildContextCommands()
    {
        var commands = new List<IContextItem>
        {
            new CommandContextItem(new SmartAppMenuPage(_coordinator, Identity))
            {
                Title = "Smart App Menu",
            },
            new Separator(),
        };

        foreach (var window in _state.Windows)
        {
            var hwnd = window.Hwnd;
            var focus = new AnonymousCommand(() => _ = _coordinator.FocusWindowAsync(hwnd))
            {
                Id = $"window:focus:{(long)hwnd:x}",
                Name = "Focus window",
                Result = CommandResult.Dismiss(),
            };
            commands.Add(new CommandContextItem(focus)
            {
                Title = string.IsNullOrWhiteSpace(window.Title) ? $"Window 0x{(long)hwnd:x}" : window.Title,
                MoreCommands =
                [
                    Context("Minimize", $"window:minimize:{(long)hwnd:x}", () => _ = _coordinator.MinimizeWindowAsync(hwnd)),
                    Context("Maximize", $"window:maximize:{(long)hwnd:x}", () => _ = _coordinator.MaximizeWindowAsync(hwnd)),
                    Context("Close", $"window:close:{(long)hwnd:x}", () => _ = _coordinator.CloseWindowAsync(hwnd), critical: true),
                ],
            });
        }

        if (_state.Windows.Count != 0)
        {
            commands.Add(new Separator());
        }

        commands.Add(Context("New instance", $"app:new:{Identity.Value}", () => _ = _coordinator.LaunchNewAsync(Identity)));

        var profile = _coordinator.ProfileFor(Identity);
        if (!string.IsNullOrWhiteSpace(profile?.Application.ExecutablePath)
            && Path.IsPathFullyQualified(profile.Application.ExecutablePath))
        {
            commands.Add(Context("Open file location", $"app:location:{Identity.Value}", () => _ = _coordinator.OpenFileLocationAsync(Identity)));
        }

        if (_state.Windows.Count != 0)
        {
            commands.Add(Context("Close all windows", $"app:closeall:{Identity.Value}", () => _ = _coordinator.CloseAllAsync(Identity), critical: true));
        }

        if (profile?.UserActions.Count > 0)
        {
            commands.Add(new Separator());
            foreach (var action in profile.UserActions)
            {
                var actionId = action.Id;
                commands.Add(Context(action.DisplayName, $"profile:{profile.Id}:action:{actionId}", () => _ = _coordinator.RunUserActionAsync(Identity, actionId)));
            }
        }

        return commands.ToArray();
    }

    private static CommandContextItem Context(string title, string id, Action action, bool critical = false)
    {
        var command = new AnonymousCommand(action)
        {
            Id = id,
            Name = title,
            Result = CommandResult.KeepOpen(),
        };
        return new CommandContextItem(command) { Title = title, IsCritical = critical };
    }
}
