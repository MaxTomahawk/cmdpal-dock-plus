using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CmdPalDockPlus.Core.Profiles;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalDockPlus.Extension;

internal sealed partial class ProfileEditorPage : ContentPage
{
    private readonly ProfileEditorForm _form; private readonly MarkdownContent _capabilities;
    public ProfileEditorPage(DockPlusRuntime runtime, AppProfile? profile) { Name = profile is null ? "Add Dock tile" : "Edit Dock tile"; Title = Name; Icon = new IconInfo("\uE713"); _form = new ProfileEditorForm(runtime, profile); _capabilities = new MarkdownContent(BuildCapabilities(runtime, profile)); }
    public override IContent[] GetContent() => [_form, _capabilities];
    private static string BuildCapabilities(DockPlusRuntime runtime, AppProfile? profile)
    {
        var sample = profile is null ? runtime.Coordinator.Windows.FirstOrDefault() : runtime.Coordinator.Windows.FirstOrDefault(window => string.Equals(Path.GetFileName(profile.Application.ExecutablePath), window.ExecutableName, StringComparison.OrdinalIgnoreCase));
        var capabilities = runtime.Providers.Probe(sample);
        var builder = new StringBuilder("## Available live data\nChoose fields by placing their ID in `{...}`. Fallback example: `{vscode.workspace ?? window.title}`.\n\n| Field | Update model | Meaning |\n|---|---|---|\n");
        foreach (var capability in capabilities.OrderBy(c => c.Id, StringComparer.Ordinal)) builder.AppendLine($"| `{capability.Id}` | {capability.UpdateModel} | {capability.Description.Replace("|", "\\|", StringComparison.Ordinal)} |");
        builder.AppendLine("\nOnly fields referenced by Title/Subtitle/Smart rules activate sampled providers."); return builder.ToString();
    }
}

internal sealed partial class ProfileEditorForm : FormContent
{
    private readonly DockPlusRuntime _runtime; private readonly AppProfile? _existing;
    public ProfileEditorForm(DockPlusRuntime runtime, AppProfile? profile)
    {
        _runtime = runtime; _existing = profile;
        TemplateJson = """
        { "$schema":"http://adaptivecards.io/schemas/adaptive-card.json","type":"AdaptiveCard","version":"1.6","body":[
          {"type":"Input.Text","id":"id","label":"Profile ID","value":"${id}","isRequired":true},
          {"type":"Input.Text","id":"displayName","label":"Display name","value":"${displayName}","isRequired":true},
          {"type":"Input.Text","id":"executable","label":"Executable path","value":"${executable}"},
          {"type":"Input.Text","id":"aumid","label":"AUMID (optional)","value":"${aumid}"},
          {"type":"Input.ChoiceSet","id":"grouping","label":"Window grouping","value":"${grouping}","choices":[{"title":"Grouped","value":"grouped"},{"title":"Separate per window","value":"separate"},{"title":"Smart rules","value":"smart"}]},
          {"type":"Input.Text","id":"title","label":"Title template","value":"${title}"},
          {"type":"Input.Text","id":"subtitle","label":"Subtitle template","value":"${subtitle}"},
          {"type":"Input.Toggle","id":"nativeCapture","title":"Capture native taskbar progress/overlay for this app","value":"${nativeCapture}","valueOn":"true","valueOff":"false"}],
          "actions":[{"type":"Action.Submit","title":"Save Dock tile"}] }
        """;
        DataJson = JsonSerializer.Serialize(new { id=profile?.Id??"", displayName=profile?.DisplayName??"", executable=profile?.Application.ExecutablePath??"", aumid=profile?.Application.Aumid??"", grouping=(profile?.Grouping??GroupingMode.Grouped).ToString().ToLowerInvariant(), title=profile?.Display.Title??"{window.title ?? app.name}", subtitle=profile?.Display.Subtitle??"{window.count} window(s)", nativeCapture=(profile?.NativeCapture.TaskbarState??false).ToString().ToLowerInvariant() });
    }
    public override CommandResult SubmitForm(string payload)
    {
        var input=JsonNode.Parse(payload)?.AsObject(); if(input is null)return CommandResult.ShowToast("Could not read profile form.");
        if(!Enum.TryParse<GroupingMode>(Text(input,"grouping"),true,out var grouping)) grouping=GroupingMode.Grouped;
        var profile=new AppProfile(Text(input,"id"),Text(input,"displayName"),new ApplicationMatch(EmptyToNull(Text(input,"executable")),EmptyToNull(Text(input,"aumid"))),grouping,new DisplayTemplate(Text(input,"title"),Text(input,"subtitle"))) { Rules=_existing?.Rules??[], UserActions=_existing?.UserActions??[], NativeCapture=new NativeCaptureOptions(string.Equals(Text(input,"nativeCapture"),"true",StringComparison.OrdinalIgnoreCase)) };
        var validation=ProfileValidator.Validate(profile); if(!validation.IsValid)return CommandResult.ShowToast(string.Join(" · ",validation.Errors)); _runtime.Coordinator.UpsertProfileAsync(profile).GetAwaiter().GetResult(); return CommandResult.GoBack();
    }
    private static string Text(JsonObject input,string key)=>input[key]?.ToString()?.Trim()??""; private static string? EmptyToNull(string value)=>string.IsNullOrWhiteSpace(value)?null:value;
}
