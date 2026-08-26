using CmdPalDockPlus.Core.Profiles;
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
        .Select((title, index) => new TileWindow((nint)(index + 1), index + 10, title, index == 0, index, new Dictionary<string, object?>
        {
            ["window.title"] = title,
            ["window.count"] = titles.Length,
            ["app.name"] = "App",
        }))
        .ToArray();
}
