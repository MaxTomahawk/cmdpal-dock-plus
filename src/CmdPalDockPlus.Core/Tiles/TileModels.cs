namespace CmdPalDockPlus.Core.Tiles;

public readonly record struct TileIdentity(string Value);

public sealed record TileWindow(
    nint Hwnd,
    int ProcessId,
    string Title,
    bool IsActive,
    long MruRank,
    IReadOnlyDictionary<string, object?> Values);

public sealed record DockTileState(
    TileIdentity Identity,
    string Title,
    string Subtitle,
    IReadOnlyList<TileWindow> Windows,
    nint? PrimaryHwnd,
    string? IconSource = null);

public static class DockCommandId
{
    public static string ForTile(TileIdentity identity) => $"tile:{identity.Value}";
}
