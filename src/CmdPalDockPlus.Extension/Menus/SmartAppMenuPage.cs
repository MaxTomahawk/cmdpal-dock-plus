using CmdPalDockPlus.Core.Profiles;
using CmdPalDockPlus.Core.Tiles;
using CmdPalDockPlus.Windows.Destinations;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalDockPlus.Extension;

internal sealed partial class SmartAppMenuPage : ListPage
{
    private readonly DockCoordinator _coordinator;
    private readonly TileIdentity _identity;
    private readonly IAppDestinationSource _destinations;
    private IReadOnlyList<AppDestination> _recent = [];
    private IReadOnlyList<AppDestination> _frequent = [];
    private int _loadStarted;

    public SmartAppMenuPage(
        DockCoordinator coordinator,
        TileIdentity identity,
        IAppDestinationSource? destinations = null)
    {
        _coordinator = coordinator;
        _identity = identity;
        _destinations = destinations ?? new ShellDestinationSource();
        Id = $"smart-menu:{identity.Value}";
        Name = "Smart App Menu";
        Title = coordinator.ProfileFor(identity)?.DisplayName ?? "App menu";
        Icon = new IconInfo("\uE712");
        ShowDetails = true;
    }

    public override IListItem[] GetItems()
    {
        StartDestinationLoadIfNeeded();
        return BuildItems();
    }

    private void StartDestinationLoadIfNeeded()
    {
        var profile = _coordinator.ProfileFor(_identity);
        if (profile is null || string.IsNullOrWhiteSpace(profile.Application.Aumid))
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _loadStarted, 1, 0) != 0)
        {
            return;
        }

        IsLoading = true;
        _ = LoadDestinationsAsync(profile.Application);
    }

    private async Task LoadDestinationsAsync(ApplicationMatch application)
    {
        try
        {
            var recentTask = _destinations.GetRecentAsync(application, 8).AsTask();
            var frequentTask = _destinations.GetFrequentAsync(application, 8).AsTask();
            await Task.WhenAll(recentTask, frequentTask).ConfigureAwait(false);

            var unique = DestinationDeduplicator.Deduplicate(
                recentTask.Result.Concat(frequentTask.Result),
                16);
            _recent = unique.Where(item => item.Kind == DestinationKind.Recent).Take(8).ToArray();
            _frequent = unique.Where(item => item.Kind == DestinationKind.Frequent).Take(8).ToArray();
        }
        catch (Exception)
        {
            _recent = [];
            _frequent = [];
        }
        finally
        {
            IsLoading = false;
            RaiseItemsChanged();
        }
    }

    private IListItem[] BuildItems()
    {
        var items = new List<IListItem>();
        _coordinator.TryGetStateByCommandId(DockCommandId.ForTile(_identity), out var state);
        if (state is not null)
        {
            foreach (var window in state.Windows)
            {
                items.Add(WindowItem(window));
            }
        }

        foreach (var destination in _recent)
        {
            items.Add(DestinationItem(destination, "Recent"));
        }

        foreach (var destination in _frequent)
        {
            items.Add(DestinationItem(destination, "Frequent"));
        }

        var profile = _coordinator.ProfileFor(_identity);
        if (profile is not null)
        {
            items.Add(ActionItem(
                "New instance",
                $"app:new:{_identity.Value}",
                "App action",
                () => _ = _coordinator.LaunchNewAsync(_identity)));

            if (!string.IsNullOrWhiteSpace(profile.Application.ExecutablePath)
                && Path.IsPathFullyQualified(profile.Application.ExecutablePath))
            {
                items.Add(ActionItem(
                    "Open file location",
                    $"app:location:{_identity.Value}",
                    "App action",
                    () => _ = _coordinator.OpenFileLocationAsync(_identity)));
            }

            if (state?.Windows.Count > 0)
            {
                items.Add(ActionItem(
                    "Close all windows",
                    $"app:closeall:{_identity.Value}",
                    "App action",
                    () => _ = _coordinator.CloseAllAsync(_identity),
                    critical: true));
            }

            foreach (var action in _coordinator.ProviderActions(_identity))
            {
                var actionId = action.Id;
                items.Add(ActionItem(
                    action.DisplayName,
                    $"provider:{_identity.Value}:{actionId}",
                    "Live app action",
                    () => _ = _coordinator.RunProviderActionAsync(_identity, actionId)));
            }

            foreach (var action in profile.UserActions)
            {
                var actionId = action.Id;
                items.Add(ActionItem(
                    action.DisplayName,
                    $"profile:{profile.Id}:action:{actionId}",
                    "Custom action",
                    () => _ = _coordinator.RunUserActionAsync(_identity, actionId)));
            }
        }

        return items.ToArray();
    }

    private ListItem WindowItem(TileWindow window)
    {
        var hwnd = window.Hwnd;
        var focus = new AnonymousCommand(() => _ = _coordinator.FocusWindowAsync(hwnd))
        {
            Id = $"window:focus:{(long)hwnd:x}",
            Name = "Focus window",
            Result = CommandResult.Dismiss(),
        };

        return new ListItem(focus)
        {
            Title = string.IsNullOrWhiteSpace(window.Title) ? $"Window 0x{(long)hwnd:x}" : window.Title,
            Subtitle = "Window",
            Icon = new IconInfo("\uE8A7"),
            MoreCommands =
            [
                Context("Minimize", $"window:minimize:{(long)hwnd:x}", () => _ = _coordinator.MinimizeWindowAsync(hwnd)),
                Context("Maximize", $"window:maximize:{(long)hwnd:x}", () => _ = _coordinator.MaximizeWindowAsync(hwnd)),
                Context("Close", $"window:close:{(long)hwnd:x}", () => _ = _coordinator.CloseWindowAsync(hwnd), critical: true),
            ],
        };
    }

    private ListItem DestinationItem(AppDestination destination, string category)
    {
        var command = new AnonymousCommand(() => _ = _coordinator.OpenDestinationAsync(destination))
        {
            Id = destination.Id,
            Name = $"Open {destination.DisplayName}",
            Result = CommandResult.Dismiss(),
        };

        return new ListItem(command)
        {
            Title = destination.DisplayName,
            Subtitle = $"{category} · {destination.Path}",
            Icon = new IconInfo("\uE8B7"),
        };
    }

    private static ListItem ActionItem(string title, string id, string category, Action action, bool critical = false)
    {
        var command = new AnonymousCommand(action)
        {
            Id = id,
            Name = title,
            Result = CommandResult.Dismiss(),
        };
        return new ListItem(command)
        {
            Title = title,
            Subtitle = category,
            Icon = new IconInfo(critical ? "\uE74D" : "\uE8D4"),
        };
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