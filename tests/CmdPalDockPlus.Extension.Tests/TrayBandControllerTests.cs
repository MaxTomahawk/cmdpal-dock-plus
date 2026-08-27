using CmdPalDockPlus.Extension.Tray;
using FluentAssertions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalDockPlus.Extension.Tests;

public sealed class TrayBandControllerTests
{
    [Fact]
    public void VisibleBandExcludesOverflowAndUsesCapturedIcon()
    {
        var service = new FakeTrayService(
            [new TrayEntry("visible", "Visible app", true, null, @"C:\cache\visible.png")],
            [new TrayEntry("hidden", "Hidden app", false, null, @"C:\cache\hidden.png")]);

        using var controller = new TrayBandController(service);

        var items = controller.Band.Items;
        items.Select(item => item.Title).Should().Equal("Visible app", "Hidden icons…");
        items.Should().NotContain(item => item.Title == "Hidden app");

        var icon = items[0].Icon.Should().BeOfType<IconInfo>().Subject;
        icon.Dark.Icon.Should().Be(@"C:\cache\visible.png");
        icon.Light.Icon.Should().Be(@"C:\cache\visible.png");
    }

    [Fact]
    public void OpeningHiddenIconsNeverAddsOverflowItemsToBand()
    {
        var service = new FakeTrayService(
            [new TrayEntry("visible", "Visible app", true, null, null)],
            [new TrayEntry("hidden", "Hidden app", false, null, @"C:\cache\hidden.png")]);

        using var controller = new TrayBandController(service);
        var hiddenLauncher = controller.Band.Items.Single(item => item.Title == "Hidden icons…");

        _ = ((InvokableCommand)hiddenLauncher.Command!).Invoke();
        service.RaiseChanged();

        service.ShowHiddenCalls.Should().Be(1);
        controller.Band.Items.Select(item => item.Title).Should().Equal("Visible app", "Hidden icons…");
    }

    [Theory]
    [InlineData(0, "App actions")]
    [InlineData(1, "Window")]
    [InlineData(2, "Windows (2)…")]
    [InlineData(5, "Windows (5)…")]
    public void WindowChooserTitleMakesGroupedWindowsDiscoverable(int count, string expected)
    {
        DockTileListItem.WindowChooserTitle(count).Should().Be(expected);
    }

    private sealed class FakeTrayService : ITrayService
    {
        public FakeTrayService(IReadOnlyList<TrayEntry> visible, IReadOnlyList<TrayEntry> overflow)
        {
            VisibleSnapshot = visible;
            OverflowSnapshot = overflow;
        }

        public event EventHandler? Changed;

        public IReadOnlyList<TrayEntry> VisibleSnapshot { get; }
        public IReadOnlyList<TrayEntry> OverflowSnapshot { get; }
        public int ShowHiddenCalls { get; private set; }

        public bool TryInvoke(string key)
        {
            _ = key;
            return true;
        }

        public bool TryShowHiddenIcons()
        {
            ShowHiddenCalls++;
            return true;
        }

        public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
        }
    }
}
