using System.Text.RegularExpressions;
using CmdPalDockPlus.Core.Profiles;
using CmdPalDockPlus.Windows;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalDockPlus.Extension;

internal sealed partial class RunningApplicationsPage : ListPage
{
    private readonly DockPlusRuntime _runtime;

    public RunningApplicationsPage(DockPlusRuntime runtime)
    {
        _runtime = runtime;
        Name = "Running applications";
        Title = "Choose an application";
        Icon = new IconInfo("\uE7C3");
        _runtime.Tracker.Changed += OnChanged;
    }

    public override IListItem[] GetItems()
        => _runtime.Coordinator.Windows
            .Where(window => !string.IsNullOrWhiteSpace(window.ExecutableName))
            .GroupBy(window => window.ExecutableName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var sample = group.First();
                var profile = CreateProfile(sample.ExecutableName, sample.ExecutablePath ?? sample.ExecutableName);
                return (IListItem)new ListItem(new ProfileEditorPage(_runtime, profile))
                {
                    Title = group.Key,
                    Subtitle = $"{group.Count()} window(s) · sample: {sample.Title}",
                    Icon = new IconInfo("\uE8A5"),
                };
            })
            .ToArray();

    private static AppProfile CreateProfile(string executableName, string target)
    {
        var id = Regex.Replace(Path.GetFileNameWithoutExtension(executableName).ToLowerInvariant(), "[^a-z0-9_-]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(id))
        {
            id = $"app-{Guid.NewGuid():N}";
        }

        return new AppProfile(
            id,
            Path.GetFileNameWithoutExtension(executableName),
            new ApplicationMatch(target, null),
            GroupingMode.Grouped,
            new DisplayTemplate("{window.title ?? app.name}", "{window.count} window(s)"));
    }

    private void OnChanged(object? sender, WindowSetChanged e) => RaiseItemsChanged();
}
