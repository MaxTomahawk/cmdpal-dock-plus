using CmdPalDockPlus.Core.Profiles;
using CmdPalDockPlus.Providers.Media;
using CmdPalDockPlus.Windows;
using CmdPalDockPlus.Windows.Media;
using FluentAssertions;

namespace CmdPalDockPlus.Providers.Tests.Media;

public sealed class MediaProviderTests
{
    [Fact]
    public void EnrichOmitsFieldsWhenNoSessionMatchesApp()
    {
        var service = new FakeMediaSessionService([]);
        using var provider = new MediaProvider(service);
        var values = new Dictionary<string, object?>();
        provider.Enrich(Window("Code.exe"), new HashSet<string> { "media.title" }, values);
        values.Should().NotContainKey("media.title");
    }

    [Fact]
    public void EnrichAddsMetadataForMatchingSession()
    {
        var session = new MediaSessionSnapshot("Spotify.exe", "Song", "Artist", "Album", "Playing", true, true, true);
        var service = new FakeMediaSessionService([session]);
        using var provider = new MediaProvider(service);
        var values = new Dictionary<string, object?>();
        provider.Enrich(Window("Spotify.exe"), new HashSet<string> { "media.title", "media.artist", "media.album", "media.playbackState" }, values);
        values["media.title"].Should().Be("Song");
        values["media.artist"].Should().Be("Artist");
        values["media.album"].Should().Be("Album");
        values["media.playbackState"].Should().Be("Playing");
    }

    private static WindowSnapshot Window(string exe) => new((nint)1, Environment.ProcessId, exe, exe, "AppClass", WindowState.Restored, false, "DISPLAY1", 0, @"C:\App\" + exe);

    private sealed class FakeMediaSessionService(IReadOnlyList<MediaSessionSnapshot> sessions) : IMediaSessionService
    {
        public event EventHandler? Changed { add { } remove { } }
        public IReadOnlyList<MediaSessionSnapshot> Snapshot => sessions;
        public ValueTask<bool> PlayPauseAsync(string sourceAppId, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
        public ValueTask<bool> NextAsync(string sourceAppId, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
        public ValueTask<bool> PreviousAsync(string sourceAppId, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
