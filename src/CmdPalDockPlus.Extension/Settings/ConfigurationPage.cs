using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalDockPlus.Extension;

internal sealed partial class ConfigurationPage : ListPage
{
    private readonly DockPlusRuntime _runtime;

    public ConfigurationPage(DockPlusRuntime runtime)
    {
        _runtime = runtime;
        Id = "com.maxtomahawk.cmdpal.dockplus.configure";
        Name = "CmdPal Dock Plus";
        Title = "CmdPal Dock Plus profiles";
        Icon = new IconInfo("\uE713");
        _runtime.Coordinator.ProfilesChanged += OnChanged;
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>
        {
            new ListItem(new RunningApplicationsPage(_runtime))
            {
                Title = "Add from running application",
                Subtitle = "Pick a current top-level window and configure what the Dock shows",
                Icon = new IconInfo("\uE710"),
            },
            new ListItem(new ProfileEditorPage(_runtime, null))
            {
                Title = "Add manually",
                Subtitle = "Configure an executable path or AUMID",
                Icon = new IconInfo("\uE710"),
            },
        };

        foreach (var profile in _runtime.Coordinator.Profiles)
        {
            var id = profile.Id;
            items.Add(new ListItem(new ProfileEditorPage(_runtime, profile))
            {
                Title = profile.DisplayName,
                Subtitle = $"{profile.Grouping} · {profile.Display.Title} / {profile.Display.Subtitle}",
                Icon = new IconInfo("\uE8A5"),
                MoreCommands =
                [
                    new CommandContextItem(new AnonymousCommand(() => _runtime.Coordinator.DeleteProfileAsync(id).GetAwaiter().GetResult())
                    {
                        Id = $"profile:delete:{id}",
                        Name = "Delete profile",
                        Result = CommandResult.KeepOpen(),
                    })
                    {
                        Title = "Delete",
                        IsCritical = true,
                    },
                ],
            });
        }

        if (!string.IsNullOrWhiteSpace(_runtime.Coordinator.LastError))
        {
            items.Add(new ListItem(new NoOpCommand())
            {
                Title = "Configuration error",
                Subtitle = _runtime.Coordinator.LastError,
                Icon = new IconInfo("\uE7BA"),
            });
        }

        return items.ToArray();
    }

    private void OnChanged(object? sender, EventArgs e) => RaiseItemsChanged();
}
