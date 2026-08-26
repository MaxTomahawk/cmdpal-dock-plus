using CmdPalDockPlus.Core.Profiles;

namespace CmdPalDockPlus.Windows.Tests;

public sealed class ApplicationWindowMatcherTests
{
    [Fact]
    public void MatchesExecutableNameCaseInsensitively()
    {
        var application = new ApplicationMatch(@"C:\Program Files\Example\Example.exe", null);
        var window = Snapshot("example.EXE", null);

        Assert.True(ApplicationWindowMatcher.Matches(application, window));
    }

    [Fact]
    public void MatchesAumidWhenExecutableIsNotConfigured()
    {
        var application = new ApplicationMatch(null, "Contoso.App_123!App");
        var window = Snapshot("ApplicationFrameHost.exe", "contoso.app_123!app");

        Assert.True(ApplicationWindowMatcher.Matches(application, window));
    }

    [Fact]
    public void AumidTakesPrecedenceOverSharedHostExecutable()
    {
        var application = new ApplicationMatch(@"C:\Windows\System32\ApplicationFrameHost.exe", "Contoso.One!App");
        var otherPackagedApp = Snapshot("ApplicationFrameHost.exe", "Contoso.Two!App");

        Assert.False(ApplicationWindowMatcher.Matches(application, otherPackagedApp));
    }

    [Fact]
    public void DoesNotMatchDifferentExecutableOrAumid()
    {
        var application = new ApplicationMatch(@"C:\Apps\One.exe", "Contoso.One!App");
        var window = Snapshot("Two.exe", "Contoso.Two!App");

        Assert.False(ApplicationWindowMatcher.Matches(application, window));
    }

    private static WindowSnapshot Snapshot(string executableName, string? aumid)
        => new(
            (nint)0x1234,
            42,
            executableName,
            "Example",
            "ExampleWindow",
            WindowState.Restored,
            false,
            @"\\.\DISPLAY1",
            0,
            @"C:\Apps\Example.exe",
            aumid);
}
