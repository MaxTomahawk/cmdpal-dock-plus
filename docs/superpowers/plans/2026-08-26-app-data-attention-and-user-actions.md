# App Data, Attention, and User Actions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete slice 2 coverage with generic media metadata, a conservative Explorer adapter, a first-class attention state, and safe user-defined actions that participate in the same provider/profile/menu model as built-in actions.

**Architecture:** Media and Explorer are normal `IDockDataProvider` implementations. Attention is a typed state in Core that providers may raise without relying on exact native taskbar flashing semantics. User actions are profile data, validated and executed through a small safe launcher abstraction; they never evaluate scripts or interpolate untrusted provider values into a shell command.

**Tech Stack:** .NET 10, Windows Global/System Media Transport Controls session APIs, Win32/Explorer window metadata where publicly obtainable, existing ProviderHost/TileComposer/Smart App Menu, System.Text.Json, xUnit/FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-08-26-cmdpal-dock-plus-design.md`

## Global Constraints

- App-specific/provider failure falls back to generic WindowProvider behavior.
- Media updates are event-driven where the Windows media-session API exposes events.
- Explorer paths are exposed only when reliably obtainable; no shell-private memory scraping.
- Exact Windows taskbar flash mirroring is not required.
- Attention state must be useful even without native taskbar capture.
- User-defined actions may launch executable/URI targets but may not contain arbitrary PowerShell/C#/JavaScript.

---

### Task 1: Implement generic MediaProvider

**Files:**
- Create: `src/CmdPalDockPlus.Providers/Media/MediaProvider.cs`
- Create: `src/CmdPalDockPlus.Windows/Media/MediaSessionService.cs`
- Create: `src/CmdPalDockPlus.Windows/Media/MediaSessionSnapshot.cs`
- Create: `tests/CmdPalDockPlus.Providers.Tests/Media/MediaProviderTests.cs`

**Interfaces:**
- Fields: `media.title`, `media.artist`, `media.album`, `media.playbackState`, `media.sourceApp`.
- Actions: play/pause, previous, next only when the active session reports support.

- [ ] **Step 1: Write probe tests**

```csharp
[Fact]
public async Task ProbeOmitsMediaFieldsWhenNoSessionMatchesApp()
{
    var service = new FakeMediaSessionService([]);
    var provider = new MediaProvider(service);
    var result = await provider.ProbeAsync(Fixtures.App("Code.exe"), Fixtures.Window(), default);
    result.Supported.Should().BeFalse();
    result.Fields.Should().BeEmpty();
}
```

- [ ] **Step 2: Implement app/session correlation**

Correlate media session source application id/executable with the target app identity. If correlation is ambiguous, do not attach the session to that app profile.

- [ ] **Step 3: Subscribe to media-session events**

Listen for current-session change, playback-info change and media-properties change. Re-read only the changed/current session and emit deduplicated provider changes. No one-second metadata polling.

- [ ] **Step 4: Add supported transport actions**

Expose Play/Pause/Next/Previous only when `MediaSessionSnapshot` reports the control is enabled. Invocation calls the Windows media-session command APIs directly.

- [ ] **Step 5: Test and commit**

```bash
dotnet test tests/CmdPalDockPlus.Providers.Tests --filter MediaProvider
git add src/CmdPalDockPlus.Providers/Media src/CmdPalDockPlus.Windows/Media tests/CmdPalDockPlus.Providers.Tests/Media
git commit -m "feat: add event-driven media dock provider"
```

---

### Task 2: Add conservative Explorer adapter

**Files:**
- Create: `src/CmdPalDockPlus.Adapters.Explorer/CmdPalDockPlus.Adapters.Explorer.csproj`
- Create: `src/CmdPalDockPlus.Adapters.Explorer/ExplorerProvider.cs`
- Create: `src/CmdPalDockPlus.Adapters.Explorer/ExplorerLocationResolver.cs`
- Create: `tests/CmdPalDockPlus.Providers.Tests/Adapters/ExplorerProviderTests.cs`

**Interfaces:**
- Fields when reliably available: `explorer.path`, `explorer.locationName`.
- Actions: open new Explorer window, open target path, open parent path when available.

- [ ] **Step 1: Test unsupported windows**

Explorer tool/dialog windows without a resolvable folder must report no `explorer.path` rather than returning title text as a fake path.

- [ ] **Step 2: Implement location resolution through supported Shell automation/interfaces**

Resolve Explorer top-level window to a shell/browser location by HWND where supported. Convert file-system shell items to canonical filesystem paths; non-filesystem locations expose display name and leave path null.

- [ ] **Step 3: Reuse WindowProvider lifecycle**

ExplorerProvider refreshes location on relevant navigation/title/window events; it does not install a second general global window hook.

- [ ] **Step 4: Add Smart-rule examples**

README/config docs include:

```text
explorer.path startsWith D:\Projects -> Separate
explorer.path equals %USERPROFILE%\Downloads -> Group "Downloads"
```

Environment variables are expanded at profile save/validation time, not on every render.

- [ ] **Step 5: Test and commit**

```bash
dotnet test tests/CmdPalDockPlus.Providers.Tests --filter ExplorerProvider
git add src/CmdPalDockPlus.Adapters.Explorer tests/CmdPalDockPlus.Providers.Tests/Adapters README.md
git commit -m "feat: add Explorer dock adapter"
```

---

### Task 3: Define provider-level attention state

**Files:**
- Create: `src/CmdPalDockPlus.Core/Attention/AttentionLevel.cs`
- Create: `src/CmdPalDockPlus.Core/Attention/AttentionState.cs`
- Create: `src/CmdPalDockPlus.Core/Attention/AttentionReducer.cs`
- Modify: `src/CmdPalDockPlus.Core/Tiles/DockTileState.cs`
- Create: `tests/CmdPalDockPlus.Core.Tests/Attention/AttentionReducerTests.cs`

**Interfaces:**
- Levels: `None`, `Informational`, `Attention`, `Urgent`.
- Produces deterministic aggregation for grouped tiles.

- [ ] **Step 1: Write aggregation tests**

```csharp
[Theory]
[InlineData(AttentionLevel.None, AttentionLevel.Attention, AttentionLevel.Attention)]
[InlineData(AttentionLevel.Urgent, AttentionLevel.Attention, AttentionLevel.Urgent)]
public void GroupUsesHighestLevel(AttentionLevel a, AttentionLevel b, AttentionLevel expected)
{
    AttentionReducer.Combine([new(a, "a"), new(b, "b")]).Level.Should().Be(expected);
}
```

- [ ] **Step 2: Add field exposure**

ProviderHost exposes normalized fields:

```text
attention.level
attention.reason
attention.isActive
```

so templates/rules can use attention state without knowing its source.

- [ ] **Step 3: Define generic attention sources**

Initial generic source may mark `Attention` when an app adapter explicitly requests attention or a managed workflow state says input is required. Do not infer “urgent” from ordinary foreground changes. Native/taskbar attention capture may contribute later but is not required.

- [ ] **Step 4: Compose visual fallback**

`DynamicIconComposer` may add a small attention dot/ring and templates can render reason/level. Do not require a new CmdPal host animation API.

- [ ] **Step 5: Test and commit**

```bash
dotnet test tests/CmdPalDockPlus.Core.Tests --filter Attention
git add src/CmdPalDockPlus.Core/Attention src/CmdPalDockPlus.Core/Tiles tests/CmdPalDockPlus.Core.Tests/Attention
git commit -m "feat: add dock attention state model"
```

---

### Task 4: Add safe user-defined actions to profiles

**Files:**
- Create: `src/CmdPalDockPlus.Core/Actions/UserAction.cs`
- Create: `src/CmdPalDockPlus.Core/Actions/UserActionValidator.cs`
- Create: `src/CmdPalDockPlus.Windows/Actions/UserActionExecutor.cs`
- Modify: `src/CmdPalDockPlus.Core/Profiles/AppProfile.cs`
- Modify: `src/CmdPalDockPlus.Extension/Settings/ProfileEditorPage.cs`
- Modify: `src/CmdPalDockPlus.Extension/Menus/SmartAppMenuPage.cs`
- Create: `tests/CmdPalDockPlus.Core.Tests/Actions/UserActionValidatorTests.cs`

**Interfaces:**
- Action kinds: `LaunchExecutable`, `OpenUri`, `OpenPath`.

- [ ] **Step 1: Write validation tests**

```csharp
[Fact]
public void ExecutableActionRejectsShellCommandString()
{
    var action = new UserAction("bad", "Bad", UserActionKind.LaunchExecutable,
        "cmd.exe /c whoami", []);
    UserActionValidator.Validate(action).IsValid.Should().BeFalse();
}

[Fact]
public void ExecutableArgumentsRemainSeparate()
{
    var action = new UserAction("terminal", "Open shell", UserActionKind.LaunchExecutable,
        @"C:\Windows\System32\wt.exe", ["-p", "PowerShell"]);
    UserActionValidator.Validate(action).IsValid.Should().BeTrue();
}
```

- [ ] **Step 2: Implement execution without shell-string concatenation**

Executable actions use `ProcessStartInfo.FileName` plus `ArgumentList`. URI/path actions validate scheme/path then use ShellExecute only for the target itself. Provider/template text is not interpolated into command arguments in v1.

- [ ] **Step 3: Add settings editor**

Per profile: Add action -> Name -> Kind -> Target -> zero or more argument fields. Reorder/delete supported. Validation happens before Save.

- [ ] **Step 4: Add actions to Smart App Menu**

User actions appear in a `Custom` section after provider app actions. Stable command id: `profile:{profileId}:action:{actionId}`.

- [ ] **Step 5: Test and commit**

```bash
dotnet test tests/CmdPalDockPlus.Core.Tests --filter UserAction
git add src/CmdPalDockPlus.Core/Actions src/CmdPalDockPlus.Windows/Actions src/CmdPalDockPlus.Core/Profiles src/CmdPalDockPlus.Extension tests/CmdPalDockPlus.Core.Tests/Actions
git commit -m "feat: add safe user-defined dock actions"
```

---

### Task 5: Complete provider capability and README examples

**Files:**
- Modify: `src/CmdPalDockPlus.Extension/Settings/CapabilityProbePage.cs`
- Modify: `README.md`
- Modify: `docs/configuration/app-adapters.md` when created by release hardening plan
- Create: `docs/testing/app-data-checklist.md`

**Interfaces:**
- Documentation/settings integration only.

- [ ] **Step 1: Include Media and Explorer in capability probe**

Probe page shows current value and UpdateModel just like every other provider. Attention normalized fields appear as a Core/Attention section when supported.

- [ ] **Step 2: Add real examples**

README demonstrates:

```text
Spotify-like media tile:
Title    {media.artist}
Subtitle {media.title} · {media.playbackState}

Explorer Smart rule:
explorer.path startsWith D:\Projects -> Separate

Attention subtitle fallback:
{attention.reason ?? window.title}
```

- [ ] **Step 3: Add manual tests**

Checklist covers media session starting/stopping, Explorer navigation changing path, adapter unavailable fallback, attention state clearing, custom executable/URI action validation.

- [ ] **Step 4: Run all managed tests and commit**

```bash
dotnet test CmdPalDockPlus.sln -c Release -p:Platform=x64
git add src/CmdPalDockPlus.Extension/Settings README.md docs
git commit -m "docs: complete app data and attention setup"
```

---

## Slice acceptance check

```text
[ ] Media metadata updates from Windows media-session events.
[ ] Media controls only appear when the session supports them.
[ ] Explorer exposes real location/path when available and nothing fake when unavailable.
[ ] Smart rules can use explorer.path.
[ ] Attention has a source-independent typed model and grouped aggregation.
[ ] Attention can render through existing icon/text capabilities without a host patch.
[ ] User actions are profile-configurable and do not execute arbitrary script strings.
[ ] All four capabilities appear in the same setup/capability model as other providers.
```
