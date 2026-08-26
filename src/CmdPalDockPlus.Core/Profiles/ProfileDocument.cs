namespace CmdPalDockPlus.Core.Profiles;

public sealed record ProfileDocument(int SchemaVersion, IReadOnlyList<AppProfile> Profiles)
{
    public const int CurrentSchemaVersion = 1;

    public static ProfileDocument Empty { get; } = new(CurrentSchemaVersion, []);
}
