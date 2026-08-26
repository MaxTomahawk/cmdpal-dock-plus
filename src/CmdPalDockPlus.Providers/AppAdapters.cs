using CmdPalDockPlus.Windows;

namespace CmdPalDockPlus.Providers;

public sealed record CapabilityDescriptor(string Id, string DisplayName, string Description, string UpdateModel);

public interface IWindowDataAdapter
{
    string Id { get; }
    bool Supports(WindowSnapshot window);
    IReadOnlyList<CapabilityDescriptor> Capabilities { get; }
    void Enrich(WindowSnapshot window, IReadOnlySet<string> requestedFields, IDictionary<string, object?> values);
}

public sealed class VSCodeAdapter : IWindowDataAdapter
{
    public string Id => "vscode";
    public IReadOnlyList<CapabilityDescriptor> Capabilities { get; } = [
        new("vscode.workspace", "Workspace", "Workspace name parsed from the VS Code window title.", "event-driven"),
        new("vscode.file", "Current file", "Current editor/file label parsed from the VS Code window title.", "event-driven"),
        new("vscode.remote", "Remote", "Remote context when exposed in the title.", "event-driven")];

    public bool Supports(WindowSnapshot window) => string.Equals(window.ExecutableName, "Code.exe", StringComparison.OrdinalIgnoreCase) || window.Title.Contains("Visual Studio Code", StringComparison.OrdinalIgnoreCase);

    public void Enrich(WindowSnapshot window, IReadOnlySet<string> requestedFields, IDictionary<string, object?> values)
    {
        var title = window.Title;
        var marker = title.LastIndexOf(" - Visual Studio Code", StringComparison.OrdinalIgnoreCase);
        if (marker >= 0) title = title[..marker];
        var parts = title.Split(" - ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (requestedFields.Contains("vscode.file") && parts.Length >= 2) values["vscode.file"] = parts[0];
        if (requestedFields.Contains("vscode.workspace") && parts.Length != 0) values["vscode.workspace"] = parts.Length >= 2 ? parts[^1] : parts[0];
        if (requestedFields.Contains("vscode.remote")) values["vscode.remote"] = parts.FirstOrDefault(p => p.StartsWith('[', StringComparison.Ordinal) && p.EndsWith(']'));
    }
}

public sealed class BrowserAdapter : IWindowDataAdapter
{
    private static readonly string[] Executables = ["chrome.exe", "msedge.exe", "brave.exe", "vivaldi.exe", "opera.exe"];
    public string Id => "browser";
    public IReadOnlyList<CapabilityDescriptor> Capabilities { get; } = [
        new("browser.pageTitle", "Page title", "Current browser page/window title.", "event-driven"),
        new("browser.isPrivate", "Private window", "InPrivate/Incognito/Private state inferred from title.", "event-driven"),
        new("browser.product", "Browser", "Browser executable family.", "snapshot")];
    public bool Supports(WindowSnapshot window) => Executables.Contains(window.ExecutableName, StringComparer.OrdinalIgnoreCase);
    public void Enrich(WindowSnapshot window, IReadOnlySet<string> requestedFields, IDictionary<string, object?> values)
    {
        if (requestedFields.Contains("browser.product")) values["browser.product"] = Path.GetFileNameWithoutExtension(window.ExecutableName);
        if (requestedFields.Contains("browser.isPrivate")) values["browser.isPrivate"] = window.Title.Contains("InPrivate", StringComparison.OrdinalIgnoreCase) || window.Title.Contains("Incognito", StringComparison.OrdinalIgnoreCase) || window.Title.Contains("Private", StringComparison.OrdinalIgnoreCase);
        if (requestedFields.Contains("browser.pageTitle"))
        {
            var title = window.Title;
            foreach (var suffix in new[] { " - Google Chrome", " - Microsoft Edge", " - Brave", " - Vivaldi", " - Opera" }) if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) { title = title[..^suffix.Length]; break; }
            values["browser.pageTitle"] = title;
        }
    }
}

public sealed class TerminalAdapter : IWindowDataAdapter
{
    public string Id => "terminal";
    public IReadOnlyList<CapabilityDescriptor> Capabilities { get; } = [
        new("terminal.title", "Terminal title", "Current Windows Terminal title.", "event-driven"),
        new("terminal.shell", "Shell hint", "PowerShell/CMD/WSL hint when present in the title.", "event-driven")];
    public bool Supports(WindowSnapshot window) => string.Equals(window.ExecutableName, "WindowsTerminal.exe", StringComparison.OrdinalIgnoreCase) || string.Equals(window.ExecutableName, "wt.exe", StringComparison.OrdinalIgnoreCase);
    public void Enrich(WindowSnapshot window, IReadOnlySet<string> requestedFields, IDictionary<string, object?> values)
    {
        if (requestedFields.Contains("terminal.title")) values["terminal.title"] = window.Title;
        if (requestedFields.Contains("terminal.shell")) values["terminal.shell"] = window.Title.Contains("PowerShell", StringComparison.OrdinalIgnoreCase) ? "PowerShell" : window.Title.Contains("Command Prompt", StringComparison.OrdinalIgnoreCase) || window.Title.Contains("cmd", StringComparison.OrdinalIgnoreCase) ? "Command Prompt" : window.Title.Contains("Ubuntu", StringComparison.OrdinalIgnoreCase) || window.Title.Contains("WSL", StringComparison.OrdinalIgnoreCase) ? "WSL" : null;
    }
}

public sealed class ExplorerAdapter : IWindowDataAdapter
{
    public string Id => "explorer";
    public IReadOnlyList<CapabilityDescriptor> Capabilities { get; } = [new("explorer.locationName", "Location name", "Explorer location label from the top-level title.", "event-driven")];
    public bool Supports(WindowSnapshot window) => string.Equals(window.ExecutableName, "explorer.exe", StringComparison.OrdinalIgnoreCase) && string.Equals(window.ClassName, "CabinetWClass", StringComparison.OrdinalIgnoreCase);
    public void Enrich(WindowSnapshot window, IReadOnlySet<string> requestedFields, IDictionary<string, object?> values) { if (requestedFields.Contains("explorer.locationName")) values["explorer.locationName"] = window.Title; }
}
