using System.Globalization;
using CmdPalDockPlus.Core.Profiles;
using CmdPalDockPlus.Core.Rules;
using CmdPalDockPlus.Core.Templates;

namespace CmdPalDockPlus.Core.Tiles;

public sealed class TileComposer
{
    public IReadOnlyList<DockTileState> Compose(AppProfile profile, IReadOnlyList<TileWindow> windows)
    {
        if (!profile.Enabled)
        {
            return [];
        }

        return profile.Grouping switch
        {
            GroupingMode.Separate => ComposeSeparate(profile, windows),
            GroupingMode.Smart => ComposeSmart(profile, windows),
            _ => [ComposeGroup(profile, windows, "default")],
        };
    }

    private static IReadOnlyList<DockTileState> ComposeSeparate(AppProfile profile, IReadOnlyList<TileWindow> windows)
    {
        if (windows.Count == 0)
        {
            return [ComposeGroup(profile, windows, "default")];
        }

        return windows
            .OrderBy(window => (long)window.Hwnd)
            .Select(window => ComposeForWindows(profile, [window], new TileIdentity($"{profile.Id}:hwnd:{((long)window.Hwnd).ToString("x", CultureInfo.InvariantCulture)}"), null))
            .ToArray();
    }

    private static IReadOnlyList<DockTileState> ComposeSmart(AppProfile profile, IReadOnlyList<TileWindow> windows)
    {
        if (windows.Count == 0)
        {
            return [ComposeGroup(profile, windows, "default")];
        }

        var buckets = new Dictionary<string, List<TileWindow>>(StringComparer.Ordinal);
        var result = new List<DockTileState>();
        foreach (var window in windows.OrderBy(w => (long)w.Hwnd))
        {
            var values = BuildValues(profile, [window], window);
            var evaluation = RuleEvaluator.Evaluate(profile.Rules, values);
            switch (evaluation.Grouping)
            {
                case RuleGrouping.Hidden:
                    continue;
                case RuleGrouping.Separate:
                    result.Add(ComposeForWindows(profile, [window], new TileIdentity($"{profile.Id}:hwnd:{((long)window.Hwnd).ToString("x", CultureInfo.InvariantCulture)}"), evaluation));
                    continue;
                case RuleGrouping.Group:
                    var keyTemplate = TemplateCompiler.Compile(evaluation.GroupKey ?? "default");
                    var renderedKey = keyTemplate.Evaluate(values);
                    var key = string.IsNullOrWhiteSpace(renderedKey) ? "default" : renderedKey;
                    if (!buckets.TryGetValue(key, out var bucket))
                    {
                        bucket = [];
                        buckets.Add(key, bucket);
                    }
                    bucket.Add(window);
                    continue;
                default:
                    if (!buckets.TryGetValue("default", out var defaultBucket))
                    {
                        defaultBucket = [];
                        buckets.Add("default", defaultBucket);
                    }
                    defaultBucket.Add(window);
                    continue;
            }
        }

        result.AddRange(buckets.OrderBy(kvp => kvp.Key, StringComparer.Ordinal).Select(kvp => ComposeGroup(profile, kvp.Value, kvp.Key)));
        return result;
    }

    private static DockTileState ComposeGroup(AppProfile profile, IReadOnlyList<TileWindow> windows, string groupKey)
        => ComposeForWindows(profile, windows, new TileIdentity($"{profile.Id}:group:{NormalizeKey(groupKey)}"), null);

    private static DockTileState ComposeForWindows(AppProfile profile, IReadOnlyList<TileWindow> windows, TileIdentity identity, RuleEvaluationResult? rule)
    {
        var selected = windows.FirstOrDefault(window => window.IsActive)
            ?? windows.OrderBy(window => window.MruRank).ThenBy(window => (long)window.Hwnd).FirstOrDefault();
        var values = BuildValues(profile, windows, selected);
        var titleTemplate = TemplateCompiler.Compile(rule?.TitleTemplate ?? profile.Display.Title);
        var subtitleTemplate = TemplateCompiler.Compile(rule?.SubtitleTemplate ?? profile.Display.Subtitle);
        var title = titleTemplate.Evaluate(values);
        var subtitle = subtitleTemplate.Evaluate(values);
        if (string.IsNullOrWhiteSpace(title))
        {
            title = profile.DisplayName;
        }

        return new(identity, title, subtitle, windows.ToArray(), selected?.Hwnd, rule?.IconTemplate ?? profile.Display.Icon);
    }

    private static Dictionary<string, object?> BuildValues(AppProfile profile, IReadOnlyList<TileWindow> windows, TileWindow? selected)
    {
        var values = selected is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(selected.Values, StringComparer.Ordinal);
        values["app.id"] = profile.Id;
        values["app.name"] = profile.DisplayName;
        values["process.executable"] = profile.Application.ExecutablePath;
        values["window.count"] = windows.Count;
        if (selected is not null)
        {
            values["window.title"] = selected.Title;
            values["window.isActive"] = selected.IsActive;
        }

        return values;
    }

    private static string NormalizeKey(string key)
        => string.Concat(key.Trim().Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? char.ToLowerInvariant(ch) : '-'));
}
