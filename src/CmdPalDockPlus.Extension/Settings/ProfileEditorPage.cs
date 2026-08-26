using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CmdPalDockPlus.Core.Profiles;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalDockPlus.Extension;

internal sealed partial class ProfileEditorPage : ContentPage
{
    private readonly ProfileEditorForm _form;
    private readonly MarkdownContent _capabilities;

    public ProfileEditorPage(DockPlusRuntime runtime, AppProfile? profile)
    {
        Name = profile is null ? "Add Dock tile" : "Edit Dock tile";
        Title = Name;
        Icon = new IconInfo("\uE713");
        _form = new ProfileEditorForm(runtime, profile);
        _capabilities = new MarkdownContent(BuildCapabilities(runtime, profile));
    }

    public override IContent[] GetContent() => [_form, _capabilities];

    private static string BuildCapabilities(DockPlusRuntime runtime, AppProfile? profile)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Available live data");
        builder.AppendLine("Use any field below inside `{...}` in Title or Subtitle. `??` selects a fallback, e.g. `{vscode.workspace ?? window.title}` once the VS Code provider is available.");
        builder.AppendLine();
        builder.AppendLine("| Field | Update | Current/sample |");
        builder.AppendLine("|---|---|---|");
        var sample = profile is null ? runtime.Coordinator.Windows.FirstOrDefault() : runtime.Coordinator.Windows.FirstOrDefault(window => string.Equals(Path.GetFileName(profile.Application.ExecutablePath), window.ExecutableName, StringComparison.OrdinalIgnoreCase));
        builder.AppendLine($"| `app.name` | profile | {profile?.DisplayName ?? "App"} |");
        builder.AppendLine($"| `process.executable` | snapshot | {sample?.ExecutablePath ?? sample?.ExecutableName ?? "—"} |");
        builder.AppendLine($"| `process.pid` | event | {sample?.ProcessId.ToString() ?? "—"} |");
        builder.AppendLine($"| `window.title` | event | {sample?.Title ?? "—"} |");
        builder.AppendLine($"| `window.state` | event | {sample?.State.ToString() ?? "—"} |");
        builder.AppendLine($"| `window.isActive` | event | {sample?.IsActive.ToString() ?? "—"} |");
        builder.AppendLine($"| `window.monitor` | event | {sample?.Monitor ?? "—"} |");
        builder.AppendLine("| `window.count` | event | number of matching windows |");
        builder.AppendLine();
        builder.AppendLine("Smart/app-specific providers add fields to this list in the provider slice; generic window fields always remain available.");
        return builder.ToString();
    }
}

internal sealed partial class ProfileEditorForm : FormContent
{
    private readonly DockPlusRuntime _runtime;
    private readonly AppProfile? _existing;

    public ProfileEditorForm(DockPlusRuntime runtime, AppProfile? profile)
    {
        _runtime = runtime;
        _existing = profile;
        TemplateJson = """
        {
          "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
          "type": "AdaptiveCard",
          "version": "1.6",
          "body": [
            { "type": "Input.Text", "id": "id", "label": "Profile ID", "value": "${id}", "isRequired": true },
            { "type": "Input.Text", "id": "displayName", "label": "Display name", "value": "${displayName}", "isRequired": true },
            { "type": "Input.Text", "id": "executable", "label": "Executable path", "value": "${executable}", "placeholder": "C:\\Program Files\\App\\app.exe" },
            { "type": "Input.Text", "id": "aumid", "label": "AUMID (packaged apps, optional)", "value": "${aumid}" },
            {
              "type": "Input.ChoiceSet", "id": "grouping", "label": "Window grouping", "value": "${grouping}",
              "choices": [
                { "title": "Grouped — one tile for the app", "value": "grouped" },
                { "title": "Separate — one tile per window", "value": "separate" },
                { "title": "Smart — rule-driven grouping", "value": "smart" }
              ]
            },
            { "type": "Input.Text", "id": "title", "label": "Title template", "value": "${title}", "placeholder": "{window.title ?? app.name}" },
            { "type": "Input.Text", "id": "subtitle", "label": "Subtitle template", "value": "${subtitle}", "placeholder": "{window.count} window(s)" },
            { "type": "Input.Toggle", "id": "nativeCapture", "title": "Capture native taskbar progress/overlay for this app (optional/invasive)", "value": "${nativeCapture}", "valueOn": "true", "valueOff": "false" }
          ],
          "actions": [ { "type": "Action.Submit", "title": "Save Dock tile" } ]
        }
        """;
        DataJson = JsonSerializer.Serialize(new
        {
            id = profile?.Id ?? string.Empty,
            displayName = profile?.DisplayName ?? string.Empty,
            executable = profile?.Application.ExecutablePath ?? string.Empty,
            aumid = profile?.Application.Aumid ?? string.Empty,
            grouping = (profile?.Grouping ?? GroupingMode.Grouped).ToString().ToLowerInvariant(),
            title = profile?.Display.Title ?? "{window.title ?? app.name}",
            subtitle = profile?.Display.Subtitle ?? "{window.count} window(s)",
            nativeCapture = (profile?.NativeCapture.TaskbarState ?? false).ToString().ToLowerInvariant(),
        });
    }

    public override CommandResult SubmitForm(string payload)
    {
        var input = JsonNode.Parse(payload)?.AsObject();
        if (input is null)
        {
            return CommandResult.ShowToast("Could not read profile form.");
        }

        var id = Text(input, "id");
        var displayName = Text(input, "displayName");
        var executable = EmptyToNull(Text(input, "executable"));
        var aumid = EmptyToNull(Text(input, "aumid"));
        if (!Enum.TryParse<GroupingMode>(Text(input, "grouping"), true, out var grouping))
        {
            grouping = GroupingMode.Grouped;
        }

        var profile = new AppProfile(
            id,
            displayName,
            new ApplicationMatch(executable, aumid),
            grouping,
            new DisplayTemplate(Text(input, "title"), Text(input, "subtitle")))
        {
            Rules = _existing?.Rules ?? [],
            UserActions = _existing?.UserActions ?? [],
            NativeCapture = new NativeCaptureOptions(string.Equals(Text(input, "nativeCapture"), "true", StringComparison.OrdinalIgnoreCase)),
        };

        var validation = ProfileValidator.Validate(profile);
        if (!validation.IsValid)
        {
            return CommandResult.ShowToast(string.Join(" · ", validation.Errors));
        }

        _runtime.Coordinator.UpsertProfileAsync(profile).GetAwaiter().GetResult();
        return CommandResult.GoBack();
    }

    private static string Text(JsonObject input, string key) => input[key]?.ToString()?.Trim() ?? string.Empty;
    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
