using CmdPalDockPlus.Core.Profiles;
using CmdPalDockPlus.Core.Rules;
using CmdPalDockPlus.Core.Tiles;
using FluentAssertions;

namespace CmdPalDockPlus.Core.Tests.Tiles;

public sealed class TileComposerTests
{
    [Fact]
    public void GroupedProfileProducesSingleTile()
    {
        var profile = Fixtures.Profile(GroupingMode.Grouped);
        var windows = Fixtures.Windows("One", "Two", "Three");
        var tiles = new TileComposer().Compose(profile, windows);
        tiles.Should().ContainSingle();
        tiles[0].Windows.Should().HaveCount(3);
    }

    [Fact]
    public void SeparateProfileProducesStableTilePerWindow()
    {
        var profile = Fixtures.Profile(GroupingMode.Separate);
        var tiles = new TileComposer().Compose(profile, Fixtures.Windows("One", "Two"));
        tiles.Select(t => t.Identity.Value).Should().Equal("app:hwnd:1", "app:hwnd:2");
    }

    [Fact]
    public void ZeroWindowsStillProducesLaunchTile()
    {
        var profile = Fixtures.Profile(GroupingMode.Grouped);
        var tiles = new TileComposer().Compose(profile, []);
        tiles.Should().ContainSingle();
        tiles[0].Title.Should().Be(profile.DisplayName);
    }

    [Fact]
    public void SmartGroupKeyCanUseLiveTemplateFields()
    {
        var profile = Fixtures.Profile(GroupingMode.Smart) with
        {
            Rules =
            [
                new DockRule(
                    "workspace",
                    [new RuleCondition("vscode.workspace", RuleOperator.Exists)],
                    [new GroupAction("{vscode.workspace}")]),
            ],
        };
        var windows = new[]
        {
            Fixtures.Window((nint)1, "One", "Alpha"),
            Fixtures.Window((nint)2, "Two", "Beta"),
            Fixtures.Window((nint)3, "Three", "Alpha"),
        };

        var tiles = new TileComposer().Compose(profile, windows);

        tiles.Select(tile => tile.Identity.Value).Should().Equal("app:group:alpha", "app:group:beta");
        tiles[0].Windows.Should().HaveCount(2);
        tiles[1].Windows.Should().ContainSingle();
    }

    [Fact]
    public void SmartGroupedTileUsesPresentationOverridesFromPrimaryWindowRule()
    {
        var profile = Fixtures.Profile(GroupingMode.Smart) with
        {
            Rules =
            [
                new DockRule(
                    "workspace",
                    [new RuleCondition("vscode.workspace", RuleOperator.Exists)],
                    [
                        new SetSubtitleTemplateAction("Workspace · {vscode.workspace}"),
                        new GroupAction("team"),
                    ]),
            ],
        };
        var windows = new[]
        {
            Fixtures.Window((nint)1, "Alpha file", "Alpha", isActive: true, mruRank: 0),
            Fixtures.Window((nint)2, "Beta file", "Beta", isActive: false, mruRank: 1),
        };

        var tile = new TileComposer().Compose(profile, windows).Should().ContainSingle().Subject;

        tile.Subtitle.Should().Be("Workspace · Alpha");
    }
}

internal static class Fixtures
{
    public static AppProfile Profile(GroupingMode mode) => new(
        "app",
        "App",
        new ApplicationMatch(@"C:\App\app.exe", null),
        mode,
        new DisplayTemplate("{window.title ?? app.name}", "{window.count}"));

    public static IReadOnlyList<TileWindow> Windows(params string[] titles) => titles
        .Select((title, index) => Window((nint)(index + 1), title, null, index == 0, index))
        .ToArray();

    public static TileWindow Window(nint hwnd, string title, string? workspace, bool isActive = false, long mruRank = 0)
    {
        var values = new Dictionary<string, object?>
        {
            ["window.title"] = title,
            ["app.name"] = "App",
        };
        if (workspace is not null) values["vscode.workspace"] = workspace;
        return new TileWindow(hwnd, checked((int)hwnd + 9), title, isActive, mruRank, values);
    }
}
