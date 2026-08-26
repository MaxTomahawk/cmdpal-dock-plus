# Providers and Smart Grouping Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn Dock Tile Profiles into capability-driven Smart profiles with provider-discovered fields/actions, dependency-aware watches, ordered rules, and initial VS Code/browser/Terminal adapters.

**Architecture:** Add a provider host between raw app/window state and tile composition. Providers advertise available fields through `ProbeAsync` and stream only fields actually requested by templates/rules through `WatchAsync`. The rule engine consumes the same field namespace as templates so grouping, hiding and display overrides use one coherent data model.

**Tech Stack:** .NET 10, async streams/channels, existing Core/Windows projects, xUnit, FluentAssertions, System.Text.RegularExpressions with bounded timeout, Command Palette Form/List pages.

**Spec:** `docs/superpowers/specs/2026-08-26-cmdpal-dock-plus-design.md`

## Global Constraints

- Generic WindowProvider works for every normal desktop application.
- App-specific adapters enhance generic data; adapter failure falls back to generic fields.
- Providers are activated only for requested fields/actions.
- CPU/memory sampling is off unless referenced.
- Rules are declarative; templates remain non-executable.
- First terminal grouping action wins; display overrides may accumulate before it.
- No provider may perform hot-loop retries faster than one second after failure.

---

## File map

```text
src/CmdPalDockPlus.Core/Providers/
src/CmdPalDockPlus.Core/Rules/
src/CmdPalDockPlus.Providers/
src/CmdPalDockPlus.Adapters.VSCode/
src/CmdPalDockPlus.Adapters.Browsers/
src/CmdPalDockPlus.Adapters.Terminal/
tests/CmdPalDockPlus.Core.Tests/Providers/
tests/CmdPalDockPlus.Core.Tests/Rules/
tests/CmdPalDockPlus.Providers.Tests/
```

---

### Task 1: Define provider contracts and dependency resolution

**Files:**
- Create: `src/CmdPalDockPlus.Core/Providers/ProviderField.cs`
- Create: `src/CmdPalDockPlus.Core/Providers/ProviderProbeResult.cs`
- Create: `src/CmdPalDockPlus.Core/Providers/DockDataChange.cs`
- Create: `src/CmdPalDockPlus.Core/Providers/DockTarget.cs`
- Create: `src/CmdPalDockPlus.Core/Providers/IDockDataProvider.cs`
- Create: `src/CmdPalDockPlus.Core/Providers/ProviderDependencyResolver.cs`
- Create: `tests/CmdPalDockPlus.Core.Tests/Providers/ProviderDependencyResolverTests.cs`

**Interfaces:**
- Produces: stable provider field metadata and dependency grouping by provider id.

- [ ] **Step 1: Write dependency tests**

```csharp
[Fact]
public void ResolvesOnlyProvidersNeededByFields()
{
    var catalog = new ProviderCatalog([
        new ProviderDescriptor("window", ["window.title", "window.state"]),
        new ProviderDescriptor("process", ["process.cpu", "process.memory"]),
        new ProviderDescriptor("vscode", ["vscode.workspace"])
    ]);

    var result = ProviderDependencyResolver.Resolve(
        ["window.title", "vscode.workspace"], catalog);

    result.Should().BeEquivalentTo(new Dictionary<string, IReadOnlySet<string>>
    {
        ["window"] = new HashSet<string> { "window.title" },
        ["vscode"] = new HashSet<string> { "vscode.workspace" }
    });
}
```

- [ ] **Step 2: Implement the contracts exactly**

```csharp
public sealed record ProviderField(
    string Id,
    string DisplayName,
    string Description,
    ProviderValueType ValueType,
    object? CurrentValue,
    UpdateModel UpdateModel);

public sealed record ProviderProbeResult(
    string ProviderId,
    bool Supported,
    IReadOnlyList<ProviderField> Fields,
    IReadOnlyList<ProviderActionDescriptor> Actions);

public interface IDockDataProvider
{
    string Id { get; }
    string DisplayName { get; }

    ValueTask<ProviderProbeResult> ProbeAsync(
        AppSnapshot app,
        WindowSnapshot? window,
        CancellationToken cancellationToken);

    IAsyncEnumerable<DockDataChange> WatchAsync(
        DockTarget target,
        IReadOnlySet<string> requestedFields,
        CancellationToken cancellationToken);
}
```

`ProviderValueType` is `String`, `Boolean`, `Number`, `Image`, `Enum`; `UpdateModel` is `EventDriven`, `Sampled`, `SnapshotOnly`.

- [ ] **Step 3: Reject unknown fields deterministically**

Add a test where a template references `foo.bar`; resolver returns a validation error `provider.field.unknown:foo.bar` rather than silently starting every provider.

- [ ] **Step 4: Run tests and commit**

```bash
dotnet test tests/CmdPalDockPlus.Core.Tests --filter ProviderDependencyResolverTests
git add src/CmdPalDockPlus.Core/Providers tests/CmdPalDockPlus.Core.Tests/Providers
git commit -m "feat: define dock data provider contracts"
```

---

### Task 2: Implement ProviderHost lifecycle and fault isolation

**Files:**
- Create: `src/CmdPalDockPlus.Providers/CmdPalDockPlus.Providers.csproj`
- Create: `src/CmdPalDockPlus.Providers/ProviderHost.cs`
- Create: `src/CmdPalDockPlus.Providers/ProviderSubscription.cs`
- Create: `src/CmdPalDockPlus.Providers/ProviderValueStore.cs`
- Create: `tests/CmdPalDockPlus.Providers.Tests/ProviderHostTests.cs`

**Interfaces:**
- Consumes: `IDockDataProvider`, target + requested field set.
- Produces: `ProviderHost.SubscribeAsync(DockTarget, IReadOnlySet<string>)`, deduplicated merged value stream.

- [ ] **Step 1: Test that unused providers never start**

```csharp
[Fact]
public async Task SubscribeStartsOnlyRequestedProvider()
{
    var window = new RecordingProvider("window", "window.title");
    var process = new RecordingProvider("process", "process.cpu");
    await using var host = new ProviderHost([window, process]);

    await using var sub = await host.SubscribeAsync(
        Fixtures.Target(), new HashSet<string> { "window.title" }, default);

    window.WatchCount.Should().Be(1);
    process.WatchCount.Should().Be(0);
}
```

- [ ] **Step 2: Implement reference-counted subscriptions**

Two tiles requesting `window.title` for the same target share one provider watch. Removing the final subscriber cancels the watch.

Key internal identity:

```csharp
internal readonly record struct ProviderWatchKey(
    string ProviderId,
    DockTargetId TargetId,
    string FieldSetFingerprint);
```

- [ ] **Step 3: Deduplicate identical values**

Test two consecutive `DockDataChange("window.title", "Same")` values produce one outward change. `ProviderValueStore` compares typed values and image fingerprints.

- [ ] **Step 4: Isolate provider exceptions**

A provider that throws terminates only its own watch. Host publishes `ProviderHealthChange` and retries with delays `1s, 2s, 5s, 10s, 30s`, capped at 30s. Cancellation skips retry.

- [ ] **Step 5: Run tests and commit**

```bash
dotnet test tests/CmdPalDockPlus.Providers.Tests
git add src/CmdPalDockPlus.Providers tests/CmdPalDockPlus.Providers.Tests
git commit -m "feat: host provider watches with fault isolation"
```

---

### Task 3: Add generic WindowProvider and ProcessProvider

**Files:**
- Create: `src/CmdPalDockPlus.Providers/Window/WindowProvider.cs`
- Create: `src/CmdPalDockPlus.Providers/Process/ProcessProvider.cs`
- Create: `src/CmdPalDockPlus.Providers/Process/IProcessMetricsReader.cs`
- Create: `src/CmdPalDockPlus.Providers/Process/ProcessMetricsReader.cs`
- Create: `tests/CmdPalDockPlus.Providers.Tests/Window/WindowProviderTests.cs`
- Create: `tests/CmdPalDockPlus.Providers.Tests/Process/ProcessProviderTests.cs`

**Interfaces:**
- Produces Window fields: `window.title`, `window.state`, `window.isActive`, `window.isMinimized`, `window.monitor`, `window.class`, `window.count`.
- Produces Process fields: `process.executable`, `process.pid`, `process.cpu`, `process.memory`, `process.uptime`.

- [ ] **Step 1: Test WindowProvider probe and live field filtering**

```csharp
[Fact]
public async Task WatchDoesNotEmitUnrequestedFields()
{
    var provider = new WindowProvider(_fakeTracker);
    var emitted = await provider.WatchAsync(
        Fixtures.Target(), new HashSet<string> { "window.title" }, default)
        .Take(2).ToListAsync();

    emitted.Should().OnlyContain(x => x.FieldId == "window.title");
}
```

- [ ] **Step 2: Implement WindowProvider from WindowTracker events**

No timer. Map tracker snapshots to requested fields and emit only affected target changes.

- [ ] **Step 3: Test that ProcessProvider does not sample when only static fields are requested**

```csharp
[Fact]
public async Task StaticProcessFieldsDoNotStartMetricsTimer()
{
    var metrics = new RecordingMetricsReader();
    var provider = new ProcessProvider(metrics);
    await provider.ProbeAsync(Fixtures.App(), Fixtures.Window(), default);
    await using var watch = provider.WatchAsync(
        Fixtures.Target(), new HashSet<string> { "process.executable" }, default)
        .GetAsyncEnumerator();

    metrics.SampleCount.Should().Be(0);
}
```

- [ ] **Step 4: Implement coalesced process metrics sampling**

Default requested metric interval: 2 seconds. One sample per PID feeds all subscribed fields/tiles. CPU uses process CPU-time delta divided by elapsed wall time and logical processor count; first sample returns null until a delta exists.

- [ ] **Step 5: Run provider tests and commit**

```bash
dotnet test tests/CmdPalDockPlus.Providers.Tests
git add src/CmdPalDockPlus.Providers tests/CmdPalDockPlus.Providers.Tests
git commit -m "feat: add generic window and process providers"
```

---

### Task 4: Implement ordered Smart grouping rules

**Files:**
- Create: `src/CmdPalDockPlus.Core/Rules/RuleCondition.cs`
- Create: `src/CmdPalDockPlus.Core/Rules/RuleAction.cs`
- Create: `src/CmdPalDockPlus.Core/Rules/DockRule.cs`
- Create: `src/CmdPalDockPlus.Core/Rules/RuleEvaluator.cs`
- Modify: `src/CmdPalDockPlus.Core/Profiles/AppProfile.cs`
- Modify: `src/CmdPalDockPlus.Core/Tiles/TileComposer.cs`
- Create: `tests/CmdPalDockPlus.Core.Tests/Rules/RuleEvaluatorTests.cs`
- Create: `tests/CmdPalDockPlus.Core.Tests/Tiles/SmartGroupingTests.cs`

**Interfaces:**
- Extends `GroupingMode` with `Smart`.
- Produces `RuleEvaluationResult` with terminal grouping result plus accumulated display/action overrides.

- [ ] **Step 1: Test condition operators**

```csharp
[Theory]
[InlineData(RuleOperator.Equals, "PowerToys", "PowerToys", true)]
[InlineData(RuleOperator.Contains, "PowerToys — Code", "PowerToys", true)]
[InlineData(RuleOperator.StartsWith, @"D:\Projects\Repo", @"D:\Projects", true)]
public void StringOperatorsEvaluate(RuleOperator op, string actual, string expected, bool match)
{
    RuleEvaluator.Matches(new RuleCondition("window.title", op, expected),
        new Dictionary<string, object?> { ["window.title"] = actual })
        .Should().Be(match);
}
```

Regex evaluation uses `RegexOptions.CultureInvariant` and a 100 ms timeout. Invalid regex is rejected at profile validation time.

- [ ] **Step 2: Implement rule actions**

```csharp
public abstract record RuleAction;
public sealed record GroupAction(string Key) : RuleAction;
public sealed record SeparateAction : RuleAction;
public sealed record HideAction : RuleAction;
public sealed record SetTitleTemplateAction(string Template) : RuleAction;
public sealed record SetSubtitleTemplateAction(string Template) : RuleAction;
public sealed record SetIconTemplateAction(string Template) : RuleAction;
```

- [ ] **Step 3: Test evaluation ordering**

First matching `GroupAction`, `SeparateAction`, or `HideAction` terminates grouping. Display actions in earlier matched rules persist. Later rules are not evaluated after a terminal grouping action.

- [ ] **Step 4: Integrate Smart TileIdentity**

Group key identity: `{profileId}:group:{normalized-rule-key}`.
Separate remains HWND-specific. Hidden windows do not contribute to window count of visible groups.

- [ ] **Step 5: Run Core tests and commit**

```bash
dotnet test tests/CmdPalDockPlus.Core.Tests
git add src/CmdPalDockPlus.Core/Rules src/CmdPalDockPlus.Core/Profiles src/CmdPalDockPlus.Core/Tiles tests/CmdPalDockPlus.Core.Tests
git commit -m "feat: add smart window grouping rules"
```

---

### Task 5: Build capability discovery into profile setup

**Files:**
- Create: `src/CmdPalDockPlus.Extension/Settings/CapabilityProbePage.cs`
- Create: `src/CmdPalDockPlus.Extension/Settings/FieldPickerPage.cs`
- Create: `src/CmdPalDockPlus.Extension/Settings/RuleEditorPage.cs`
- Modify: `src/CmdPalDockPlus.Extension/Settings/ProfileEditorPage.cs`
- Modify: `src/CmdPalDockPlus.Extension/Dock/DockCoordinator.cs`
- Create: `tests/CmdPalDockPlus.Core.Tests/Profiles/ProfileDependencyTests.cs`

**Interfaces:**
- Consumes provider `ProbeAsync` results.
- Produces user-visible catalog grouped by provider, template dependencies and rule dependencies.

- [ ] **Step 1: Test combined dependency extraction**

A profile with title `{vscode.workspace ?? window.title}` and rule on `process.cpu` must resolve exactly `vscode.workspace`, `window.title`, `process.cpu`.

- [ ] **Step 2: Implement capability probe page**

Render each provider section with:

```text
Field display name
Stable field id
Description
Current/example value
Update model: Event-driven | Sampled | Snapshot
```

Unsupported providers remain collapsed with a clear “not available for this target” state.

- [ ] **Step 3: Implement field insertion rather than a fixed dropdown**

Selecting a field inserts `{field.id}` at the current template field target. The raw template text remains editable for `??` and formatting.

- [ ] **Step 4: Implement Smart-rule editor controls**

Fields come from probe catalog; operator choices are filtered by `ProviderValueType`. String fields permit regex; boolean fields permit true/false; numbers permit equality and comparisons.

- [ ] **Step 5: Wire provider dependencies to DockCoordinator**

When profile templates/rules change, coordinator recalculates requested field set, disposes obsolete subscriptions and starts required subscriptions. Provider updates recompose only affected profile/target identities.

- [ ] **Step 6: Build/test and commit**

```bash
dotnet test CmdPalDockPlus.sln -c Debug -p:Platform=x64
git add src/CmdPalDockPlus.Extension/Settings src/CmdPalDockPlus.Extension/Dock tests/CmdPalDockPlus.Core.Tests/Profiles
git commit -m "feat: add capability-driven tile setup"
```

---

### Task 6: Add Visual Studio Code adapter

**Files:**
- Create: `src/CmdPalDockPlus.Adapters.VSCode/CmdPalDockPlus.Adapters.VSCode.csproj`
- Create: `src/CmdPalDockPlus.Adapters.VSCode/VSCodeProvider.cs`
- Create: `src/CmdPalDockPlus.Adapters.VSCode/VSCodeWindowParser.cs`
- Create: `tests/CmdPalDockPlus.Providers.Tests/Adapters/VSCodeWindowParserTests.cs`

**Interfaces:**
- Produces fields: `vscode.workspace`, `vscode.file`, `vscode.remote` when confidently derivable.
- Produces actions: `vscode.newWindow`, `vscode.openFolder`, and workspace-aware terminal/open actions only when executable/path information is available.

- [ ] **Step 1: Define parser fixtures from real VS Code title forms**

```csharp
[Theory]
[InlineData("DockItemControl.xaml - PowerToys - Visual Studio Code", "PowerToys", "DockItemControl.xaml")]
[InlineData("PowerToys - Visual Studio Code", "PowerToys", null)]
public void ParsesCommonTitles(string title, string workspace, string? file)
{
    VSCodeWindowParser.Parse(title).Should().Match<VSCodeWindowInfo>(x =>
        x.Workspace == workspace && x.File == file);
}
```

- [ ] **Step 2: Implement conservative fallback parsing**

Require recognized `Visual Studio Code` product suffix; never report workspace/file if title structure is ambiguous. `vscode.remote` is only populated when a recognized remote marker is present.

- [ ] **Step 3: Probe and watch via WindowProvider dependency**

VSCodeProvider listens to window-title changes; it does not create a second WinEvent hook.

- [ ] **Step 4: Register adapter and document capability caveat**

README states VS Code title-derived fields reflect what VS Code exposes in its title and may be absent depending on title-bar settings.

- [ ] **Step 5: Test and commit**

```bash
dotnet test tests/CmdPalDockPlus.Providers.Tests --filter VSCode
git add src/CmdPalDockPlus.Adapters.VSCode tests/CmdPalDockPlus.Providers.Tests/Adapters README.md
git commit -m "feat: add Visual Studio Code dock adapter"
```

---

### Task 7: Add browser and Windows Terminal adapters without brittle overreach

**Files:**
- Create: `src/CmdPalDockPlus.Adapters.Browsers/CmdPalDockPlus.Adapters.Browsers.csproj`
- Create: `src/CmdPalDockPlus.Adapters.Browsers/ChromiumWindowProvider.cs`
- Create: `src/CmdPalDockPlus.Adapters.Terminal/CmdPalDockPlus.Adapters.Terminal.csproj`
- Create: `src/CmdPalDockPlus.Adapters.Terminal/TerminalProvider.cs`
- Create: `tests/CmdPalDockPlus.Providers.Tests/Adapters/ChromiumWindowProviderTests.cs`
- Create: `tests/CmdPalDockPlus.Providers.Tests/Adapters/TerminalProviderTests.cs`

**Interfaces:**
- Browser actions: new window, private window, explicit configured profile launches when command-line/profile metadata is known.
- Terminal fields/actions: profile/title where reliably available; PowerShell/CMD/WSL launch actions.

- [ ] **Step 1: Test executable-family detection**

Recognize Edge/Chrome/Brave from executable filename and configured path, not window-title branding alone.

- [ ] **Step 2: Implement actions as explicit process launches**

Examples:

```text
msedge.exe --new-window
msedge.exe -inprivate
chrome.exe --new-window
chrome.exe --incognito
wt.exe -p "PowerShell"
wt.exe -p "Command Prompt"
```

Arguments are stored as argument arrays/escaped through `ProcessStartInfo.ArgumentList`; never concatenate untrusted window title text into a command line.

- [ ] **Step 3: Expose only reliable fields**

Do not claim active tab URL, browser profile or terminal working directory unless a supported/local source proves it. Probe simply omits fields that cannot be obtained reliably.

- [ ] **Step 4: Run adapter tests and commit**

```bash
dotnet test tests/CmdPalDockPlus.Providers.Tests --filter "Chromium|Terminal"
git add src/CmdPalDockPlus.Adapters.Browsers src/CmdPalDockPlus.Adapters.Terminal tests/CmdPalDockPlus.Providers.Tests/Adapters
git commit -m "feat: add browser and terminal dock adapters"
```

---

### Task 8: Add provider performance and settings regression tests

**Files:**
- Create: `tests/CmdPalDockPlus.Providers.Tests/Performance/ProviderActivationTests.cs`
- Create: `tests/CmdPalDockPlus.Providers.Tests/Performance/ProviderDeduplicationTests.cs`
- Modify: `README.md`

**Interfaces:**
- Verifies architectural constraints rather than adding new public API.

- [ ] **Step 1: Assert unused sampled providers remain idle**

Create ten profiles using only `window.title`; wait 5 seconds with fake clocks and assert process metric reader sample count is zero.

- [ ] **Step 2: Assert shared PID metrics are sampled once per interval**

Ten tiles for one process requesting `process.cpu` must cause one metrics sample per configured interval, not ten.

- [ ] **Step 3: Assert identical provider values cause no tile projection**

Inject 100 duplicate title events and assert `DockCoordinator` receives one changed value after coalescing.

- [ ] **Step 4: Complete README sections for capability discovery, Smart rules and app adapters**

Document exact examples for VS Code Smart separation and a sampled CPU subtitle, including the performance effect of choosing sampled fields.

- [ ] **Step 5: Run full suite and commit**

```bash
dotnet test CmdPalDockPlus.sln -c Release -p:Platform=x64
git add tests README.md
git commit -m "test: enforce provider activation and smart grouping behavior"
```

---

## Slice acceptance check

```text
[ ] Profile setup shows fields actually available from current providers.
[ ] Selecting only window.title starts no CPU/media sampling.
[ ] Smart rules can group, separate and hide windows.
[ ] Rule order is deterministic and validated.
[ ] VS Code exposes workspace/file when confidently discoverable.
[ ] Browser and Terminal adapters expose safe actions without pretending unavailable metadata exists.
[ ] Provider failure does not remove generic WindowProvider functionality.
[ ] Ten title-only profiles remain event-driven/idle except actual window events.
```
