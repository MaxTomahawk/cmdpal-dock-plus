using System.Windows.Automation;

namespace CmdPalDockPlus.Extension.Tray;

internal sealed record TrayEntry(
    string Key,
    string DisplayName,
    bool IsVisible,
    AutomationElement? Element,
    string? IconPath);

internal interface ITrayService : IDisposable
{
    event EventHandler? Changed;

    IReadOnlyList<TrayEntry> VisibleSnapshot { get; }

    IReadOnlyList<TrayEntry> OverflowSnapshot { get; }

    bool TryInvoke(string key);

    bool TryShowHiddenIcons();
}
