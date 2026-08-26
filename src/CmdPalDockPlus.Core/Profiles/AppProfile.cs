using CmdPalDockPlus.Core.Actions;
using CmdPalDockPlus.Core.Rules;

namespace CmdPalDockPlus.Core.Profiles;

public sealed record ApplicationMatch(string? ExecutablePath, string? Aumid);

public enum GroupingMode
{
    Grouped,
    Separate,
    Smart,
}

public sealed record DisplayTemplate(string Title, string Subtitle, string? Icon = null);

public sealed record NativeCaptureOptions(bool TaskbarState = false);

public sealed record AppProfile(
    string Id,
    string DisplayName,
    ApplicationMatch Application,
    GroupingMode Grouping,
    DisplayTemplate Display)
{
    public IReadOnlyList<DockRule> Rules { get; init; } = [];

    public IReadOnlyList<UserActionDefinition> UserActions { get; init; } = [];

    public NativeCaptureOptions NativeCapture { get; init; } = new();

    public bool Enabled { get; init; } = true;
}
