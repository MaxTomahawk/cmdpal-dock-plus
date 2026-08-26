using CmdPalDockPlus.Core.Taskbar;
using FluentAssertions;

namespace CmdPalDockPlus.Core.Tests.Taskbar;

public sealed class TaskbarStateStoreTests
{
    [Fact]
    public void ProgressValueAndStateAreCombinedPerWindow()
    {
        var store = new TaskbarStateStore();
        store.Apply(new ProgressStateChanged(1, 7, (nint)0x10, TaskbarProgressMode.Normal));
        store.Apply(new ProgressValueChanged(2, 7, (nint)0x10, 25, 100));

        var state = store.Get((nint)0x10);
        state.Should().NotBeNull();
        state!.ProgressMode.Should().Be(TaskbarProgressMode.Normal);
        state.Completed.Should().Be(25);
        state.Total.Should().Be(100);
        state.ProgressPercent.Should().Be(25);
    }

    [Fact]
    public void NoProgressClearsValue()
    {
        var store = new TaskbarStateStore();
        store.Apply(new ProgressValueChanged(1, 7, (nint)0x10, 5, 10));
        store.Apply(new ProgressStateChanged(2, 7, (nint)0x10, TaskbarProgressMode.None));

        store.Get((nint)0x10)!.Completed.Should().BeNull();
        store.Get((nint)0x10)!.Total.Should().BeNull();
    }

    [Fact]
    public void PausedStateSurvivesValueUpdate()
    {
        var store = new TaskbarStateStore();
        store.Apply(new ProgressStateChanged(1, 7, (nint)0x10, TaskbarProgressMode.Paused));
        store.Apply(new ProgressValueChanged(2, 7, (nint)0x10, 1, 2));

        store.Get((nint)0x10)!.ProgressMode.Should().Be(TaskbarProgressMode.Paused);
    }

    [Fact]
    public void StaleSequenceIsIgnored()
    {
        var store = new TaskbarStateStore();
        store.Apply(new ProgressValueChanged(10, 7, (nint)0x10, 8, 10));

        store.Apply(new ProgressValueChanged(9, 7, (nint)0x10, 1, 10)).Should().BeFalse();
        store.Get((nint)0x10)!.Completed.Should().Be(8);
    }

    [Fact]
    public void ProcessExitRemovesOwnedWindowsOnly()
    {
        var store = new TaskbarStateStore();
        store.Apply(new ProgressValueChanged(1, 7, (nint)0x10, 1, 2));
        store.Apply(new ProgressValueChanged(1, 8, (nint)0x20, 1, 2));
        store.Apply(new TaskbarProcessExited(2, 7));

        store.Get((nint)0x10).Should().BeNull();
        store.Get((nint)0x20).Should().NotBeNull();
    }

    [Fact]
    public void OverlayIsCopiedAndCleared()
    {
        var store = new TaskbarStateStore();
        var pixels = new byte[] { 1, 2, 3, 4 };
        store.Apply(new OverlayChanged(1, 7, (nint)0x10, new TaskbarOverlay(1, 1, pixels, "status")));
        pixels[0] = 99;

        store.Get((nint)0x10)!.Overlay!.Rgba[0].Should().Be(1);
        store.Apply(new OverlayCleared(2, 7, (nint)0x10));
        store.Get((nint)0x10)!.Overlay.Should().BeNull();
    }
}
