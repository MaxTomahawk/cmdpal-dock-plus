using CmdPalDockPlus.Windows.Tests.Fakes;
using FluentAssertions;

namespace CmdPalDockPlus.Windows.Tests;

public sealed class WindowTrackerTests
{
    [Fact]
    public async Task ReconcilePublishesOnlyRealChanges()
    {
        var backend = new FakeWindowBackend(new WindowSnapshot((nint)42, 100, "Code.exe", "One", "Chrome_WidgetWin_1", WindowState.Restored, true, "DISPLAY1", 0));
        await using var tracker = new WindowTracker(backend, TimeSpan.FromMilliseconds(10));
        var changes = new List<WindowSetChanged>();
        tracker.Changed += (_, e) => changes.Add(e);

        await tracker.ReconcileAsync(default);
        await tracker.ReconcileAsync(default);

        tracker.Snapshot.Should().ContainSingle();
        changes.Should().HaveCount(1);
    }

    [Fact]
    public async Task BurstEventsAreCoalesced()
    {
        var backend = new FakeWindowBackend();
        await using var tracker = new WindowTracker(backend, TimeSpan.FromMilliseconds(20));
        await tracker.StartAsync(default);
        var before = backend.EnumerateCount;

        for (var i = 0; i < 10; i++)
        {
            backend.RaiseChanged();
        }

        await Task.Delay(120);
        (backend.EnumerateCount - before).Should().BeLessThanOrEqualTo(2);
    }
}
