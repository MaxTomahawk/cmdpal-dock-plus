using CmdPalDockPlus.Core.Taskbar;
using FluentAssertions;

namespace CmdPalDockPlus.Core.Tests.Taskbar;

public sealed class TaskbarCaptureProtocolTests
{
    [Fact]
    public void ProgressValueRoundTripsIncludingZeroTotal()
    {
        var message = new ProgressValueChanged(12, 44, (nint)0x1234, 7, 0);

        TaskbarCaptureProtocol.Parse(TaskbarCaptureProtocol.Serialize(message))
            .Should().Be(message);
    }

    [Fact]
    public void OverlayRoundTrips()
    {
        var message = new OverlayChanged(
            2,
            44,
            (nint)0x1234,
            new TaskbarOverlay(1, 2, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, "mail"));

        var parsed = TaskbarCaptureProtocol.Parse(TaskbarCaptureProtocol.Serialize(message))
            .Should().BeOfType<OverlayChanged>().Subject;
        parsed.Sequence.Should().Be(2);
        parsed.ProcessId.Should().Be(44);
        parsed.Hwnd.Should().Be((nint)0x1234);
        parsed.Overlay.Width.Should().Be(1);
        parsed.Overlay.Height.Should().Be(2);
        parsed.Overlay.Description.Should().Be("mail");
        parsed.Overlay.Rgba.Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
    }

    [Fact]
    public void RejectsBadMagic()
    {
        var data = TaskbarCaptureProtocol.Serialize(new TaskbarProcessExited(1, 2));
        data[0] ^= 0xff;

        var act = () => TaskbarCaptureProtocol.Parse(data);
        act.Should().Throw<TaskbarCaptureProtocolException>();
    }

    [Fact]
    public void RejectsTruncatedMessage()
    {
        var data = TaskbarCaptureProtocol.Serialize(new ProgressValueChanged(1, 2, (nint)3, 4, 5));

        var act = () => TaskbarCaptureProtocol.Parse(data.AsSpan(0, data.Length - 1));
        act.Should().Throw<TaskbarCaptureProtocolException>();
    }

    [Fact]
    public void RejectsOversizedOverlayDimensions()
    {
        var message = new OverlayChanged(
            1,
            2,
            (nint)3,
            new TaskbarOverlay(257, 1, new byte[257 * 4], string.Empty));

        var act = () => TaskbarCaptureProtocol.Serialize(message);
        act.Should().Throw<TaskbarCaptureProtocolException>();
    }
}
