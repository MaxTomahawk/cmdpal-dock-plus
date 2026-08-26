using System.Security.Cryptography;
using System.Text;
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
            .Where(window => !string.IsNullOrWhiteSpace(window.ExecutableName)
                || !string.IsNullOrWhiteSpace(window.AppUserModelId))
            .GroupBy(window => new RunningApplicationKey(
                window.ExecutableName.ToLowerInvariant(),
                window.AppUserModelId?.ToLowerInvariant()))
            .OrderBy(group => DisplayName(group.First()), StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Key.AppUserModelId, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var sample = group.First();
                var profile = CreateProfile(sample);
                var identity = string.IsNullOrWhiteSpace(sample.AppUserModelId)
                    ? sample.ExecutableName
                    : sample.AppUserModelId;
                return (IListItem)new ListItem(new ProfileEditorPage(_runtime, profile))
                {
                    Title = DisplayName(sample),
                    Subtitle = $"{group.Count()} window(s) · {identity} · sample: {sample.Title}",
                    Icon = new IconInfo("\uE8A5"),
                };
            })
            .ToArray();

    private static AppProfile CreateProfile(WindowSnapshot sample)
    {
        var target = sample.ExecutablePath ?? (string.IsNullOrWhiteSpace(sample.ExecutableName) ? null : sample.ExecutableName);
        var identity = sample.AppUserModelId ?? sample.ExecutableName;
        var readable = Regex.Replace(
            Path.GetFileNameWithoutExtension(sample.ExecutableName).ToLowerInvariant(),
            "[^a-z0-9_-]+",
            "-").Trim('-');
        if (string.IsNullOrWhiteSpace(readable)) readable = "app";

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant()[..8];
        var id = $"{readable}-{hash}";

        return new AppProfile(
            id,
            DisplayName(sample),
            new ApplicationMatch(target, sample.AppUserModelId),
            GroupingMode.Grouped,
            new DisplayTemplate("{window.title ?? app.name}", "{window.count} window(s)"));
    }

    private static string DisplayName(WindowSnapshot window)
    {
        if (!string.IsNullOrWhiteSpace(window.AppUserModelId))
        {
            var value = window.AppUserModelId!;
            var bang = value.IndexOf('!');
            if (bang > 0) value = value[..bang];
            var underscore = value.IndexOf('_');
            if (underscore > 0) value = value[..underscore];
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        var executable = Path.GetFileNameWithoutExtension(window.ExecutableName);
        return string.IsNullOrWhiteSpace(executable) ? "Application" : executable;
    }

    private void OnChanged(object? sender, WindowSetChanged e) => RaiseItemsChanged();

    private sealed record RunningApplicationKey(string ExecutableName, string? AppUserModelId);
}
