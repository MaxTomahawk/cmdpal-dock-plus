using CmdPalDockPlus.Windows.Tests.Fakes;
using FluentAssertions;

namespace CmdPalDockPlus.Windows.Tests;

public sealed class WindowActivatorTests
{
    [Fact]
    public async Task FocusRestoresMinimizedWindowFirst()
    {
        var backend = new FakeWindowBackend();
        var activator = new WindowActivator(backend);
        var window = new WindowSnapshot((nint)5, 1, "app.exe", "Title", "Class", WindowState.Minimized, false, "DISPLAY1", 0);

        await activator.FocusAsync(window, default);

        backend.Shows.Should().ContainSingle().Which.Should().Be(((nint)5, WindowShowCommand.Restore));
        backend.Focuses.Should().ContainSingle().Which.Should().Be((nint)5);
    }

    [Fact]
    public async Task CloseUsesNormalCloseRequest()
    {
        var backend = new FakeWindowBackend();
        var activator = new WindowActivator(backend);
        await activator.CloseAsync((nint)9, default);
        backend.Closes.Should().Equal((nint)9);
    }
}
