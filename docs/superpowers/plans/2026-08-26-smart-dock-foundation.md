# Smart Dock Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver an installable Command Palette extension that can create persistent app profiles, discover/focus application windows, render grouped or separate Dock tiles, and display live templated Title/Subtitle values.

**Architecture:** Start from the current official PowerToys Command Palette extension template and keep the CmdPal-facing project thin. Domain models, templates and tile composition live in `Core`; Win32 discovery/actions live in `Windows`; the extension adapts those services to `ICommandProvider3/4` and CmdPal settings. This slice deliberately excludes Smart rules, app-specific adapters, tray hooks and native taskbar capture.

**Tech Stack:** .NET 10, `net10.0-windows10.0.26100.0`, Windows minimum `10.0.19041.0`, Microsoft Command Palette extension interfaces/toolkit, CsWin32/CsWinRT where appropriate, System.Text.Json, xUnit, FluentAssertions, Windows x64/ARM64 MSIX.

**Spec:** `docs/superpowers/specs/2026-08-26-cmdpal-dock-plus-design.md`

## Global Constraints

- Repository is public and named `cmdpal-dock-plus`.
- The product enhances Command Palette Dock; it is not a standalone replacement taskbar.
- Existing native taskbar pins are not imported.
- Main extension must function with all optional native components absent.
- Dynamic text is first-class; templates never execute arbitrary script.
- Prefer event-driven updates over polling.
- Release binaries are built by GitHub Actions and distributed through GitHub Releases.
- `README.md` is the canonical installation/configuration manual.
- Extension baseline follows the current official CmdPal template: `net10.0-windows10.0.26100.0`, minimum Windows `10.0.19041.0`, x64/ARM64, single-project MSIX tooling.

---

## File map

```text
CmdPalDockPlus.sln
Directory.Build.props
Directory.Packages.props
src/CmdPalDockPlus.Extension/
src/CmdPalDockPlus.Core/
src/CmdPalDockPlus.Windows/
tests/CmdPalDockPlus.Core.Tests/
tests/CmdPalDockPlus.Windows.Tests/
README.md
```

`Core` has no dependency on CmdPal or user32. `Windows` may depend on `Core`. `Extension` may depend on both.

---

### Task 1: Bootstrap the official CmdPal extension shape

**Files:**
- Create: `CmdPalDockPlus.sln`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `src/CmdPalDockPlus.Extension/CmdPalDockPlus.Extension.csproj`
- Create: `src/CmdPalDockPlus.Extension/Package.appxmanifest`
- Create: `src/CmdPalDockPlus.Extension/app.manifest`
- Create: `src/CmdPalDockPlus.Extension/Program.cs`
- Create: `src/CmdPalDockPlus.Extension/CmdPalDockPlusExtension.cs`
- Create: `src/CmdPalDockPlus.Core/CmdPalDockPlus.Core.csproj`
- Create: `src/CmdPalDockPlus.Windows/CmdPalDockPlus.Windows.csproj`
- Create: `tests/CmdPalDockPlus.Core.Tests/CmdPalDockPlus.Core.Tests.csproj`
- Create: `tests/CmdPalDockPlus.Windows.Tests/CmdPalDockPlus.Windows.Tests.csproj`

**Interfaces:**
- Consumes: current Microsoft Command Palette extension package contract.
- Produces: buildable solution; `CmdPalDockPlusExtension : Extension`; dependency direction `Extension -> Windows -> Core`.

- [ ] **Step 1: Add a smoke test that loads the Core assembly**

```csharp
namespace CmdPalDockPlus.Core.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void CoreAssemblyLoads()
    {
        typeof(CmdPalDockPlus.Core.AppProfile).Assembly.GetName().Name
            .Should().Be("CmdPalDockPlus.Core");
    }
}
```

Run: `dotnet test tests/CmdPalDockPlus.Core.Tests/CmdPalDockPlus.Core.Tests.csproj`
Expected: FAIL because the project/types do not exist yet.

- [ ] **Step 2: Create the solution and projects using the current template platform values**

`src/CmdPalDockPlus.Extension/CmdPalDockPlus.Extension.csproj` must begin with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <RootNamespace>CmdPalDockPlus.Extension</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <WindowsSdkPackageVersion>10.0.26100.68-preview</WindowsSdkPackageVersion>
    <TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.19041.0</TargetPlatformMinVersion>
    <SupportedOSPlatformVersion>10.0.19041.0</SupportedOSPlatformVersion>
    <RuntimeIdentifiers>win-x64;win-arm64</RuntimeIdentifiers>
    <PublishProfile>win-$(Platform).pubxml</PublishProfile>
    <EnableMsixTooling>true</EnableMsixTooling>
    <Nullable>enable</Nullable>
    <PublishSingleFile>true</PublishSingleFile>
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CommandPalette.Extensions" />
    <PackageReference Include="Microsoft.Windows.CsWinRT" />
    <PackageReference Include="Shmuelie.WinRTServer" />
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools.MSIX" PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\CmdPalDockPlus.Core\CmdPalDockPlus.Core.csproj" />
    <ProjectReference Include="..\CmdPalDockPlus.Windows\CmdPalDockPlus.Windows.csproj" />
  </ItemGroup>
</Project>
```

Use central package management in `Directory.Packages.props`; pin every package version there.

- [ ] **Step 3: Add the minimum domain type needed by the smoke test**

```csharp
namespace CmdPalDockPlus.Core;

public sealed record AppProfile(string Id, string DisplayName, ApplicationMatch Application);
public sealed record ApplicationMatch(string? ExecutablePath, string? Aumid);
```

- [ ] **Step 4: Run restore/build/test**

Run:

```powershell
dotnet restore CmdPalDockPlus.sln
dotnet build CmdPalDockPlus.sln -c Debug -p:Platform=x64
dotnet test CmdPalDockPlus.sln -c Debug -p:Platform=x64
```

Expected: all commands succeed.

- [ ] **Step 5: Commit**

```bash
git add CmdPalDockPlus.sln Directory.* src tests
git commit -m "build: bootstrap CmdPal Dock Plus solution"
```

---

### Task 2: Define profile persistence and versioned configuration

**Files:**
- Create: `src/CmdPalDockPlus.Core/Profiles/AppProfile.cs`
- Create: `src/CmdPalDockPlus.Core/Profiles/ProfileDocument.cs`
- Create: `src/CmdPalDockPlus.Core/Profiles/ProfileStore.cs`
- Create: `src/CmdPalDockPlus.Core/Profiles/IProfileStore.cs`
- Create: `tests/CmdPalDockPlus.Core.Tests/Profiles/ProfileStoreTests.cs`

**Interfaces:**
- Consumes: `ApplicationMatch`.
- Produces: `IProfileStore.LoadAsync`, `SaveAsync`; schema version `1`; grouping enum `Grouped|Separate` for this slice.

- [ ] **Step 1: Write persistence tests**

```csharp
public sealed class ProfileStoreTests
{
    [Fact]
    public async Task RoundTripPreservesProfile()
    {
        using var dir = new TempDirectory();
        var store = new ProfileStore(Path.Combine(dir.Path, "profiles.json"));
        var expected = new AppProfile(
            "vscode", "Visual Studio Code",
            new ApplicationMatch(@"C:\Program Files\Microsoft VS Code\Code.exe", null),
            GroupingMode.Separate,
            new DisplayTemplate("{window.title}", "{window.state}"));

        await store.SaveAsync(new ProfileDocument(1, [expected]), default);
        var actual = await store.LoadAsync(default);

        actual.Profiles.Should().ContainSingle().Which.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task MissingFileReturnsEmptyVersionOneDocument()
    {
        using var dir = new TempDirectory();
        var store = new ProfileStore(Path.Combine(dir.Path, "missing.json"));
        (await store.LoadAsync(default)).Should().BeEquivalentTo(new ProfileDocument(1, []));
    }
}
```

- [ ] **Step 2: Run the tests and confirm failure**

Run: `dotnet test tests/CmdPalDockPlus.Core.Tests --filter ProfileStoreTests`
Expected: FAIL because persistence types are missing.

- [ ] **Step 3: Implement immutable profile models**

```csharp
public enum GroupingMode { Grouped, Separate }

public sealed record DisplayTemplate(string Title, string Subtitle);

public sealed record AppProfile(
    string Id,
    string DisplayName,
    ApplicationMatch Application,
    GroupingMode Grouping,
    DisplayTemplate Display);

public sealed record ProfileDocument(int SchemaVersion, IReadOnlyList<AppProfile> Profiles);
```

Implement `ProfileStore` with `System.Text.Json`, atomic save via `path.tmp` followed by `File.Move(..., overwrite: true)`, and `JsonSerializerOptions` using camelCase plus string enums.

- [ ] **Step 4: Run profile tests**

Run: `dotnet test tests/CmdPalDockPlus.Core.Tests --filter ProfileStoreTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CmdPalDockPlus.Core/Profiles tests/CmdPalDockPlus.Core.Tests/Profiles
git commit -m "feat: persist versioned app profiles"
```

---

### Task 3: Implement the safe template engine

**Files:**
- Create: `src/CmdPalDockPlus.Core/Templates/TemplateCompiler.cs`
- Create: `src/CmdPalDockPlus.Core/Templates/CompiledTemplate.cs`
- Create: `src/CmdPalDockPlus.Core/Templates/TemplateEvaluationContext.cs`
- Create: `tests/CmdPalDockPlus.Core.Tests/Templates/TemplateCompilerTests.cs`

**Interfaces:**
- Consumes: dictionary-like provider values by stable field id.
- Produces: `CompiledTemplate.Dependencies` and `Evaluate(IReadOnlyDictionary<string, object?> values)`.

- [ ] **Step 1: Write parser/evaluation tests**

```csharp
[Theory]
[InlineData("{window.title}", "Editor", "Editor")]
[InlineData("prefix {window.title}", "Editor", "prefix Editor")]
public void EvaluatesFieldSubstitution(string template, string value, string expected)
{
    var compiled = TemplateCompiler.Compile(template);
    compiled.Evaluate(new Dictionary<string, object?> { ["window.title"] = value })
        .Should().Be(expected);
}

[Fact]
public void NullCoalesceUsesFallback()
{
    var compiled = TemplateCompiler.Compile("{vscode.workspace ?? window.title}");
    compiled.Dependencies.Should().BeEquivalentTo(["vscode.workspace", "window.title"]);
    compiled.Evaluate(new Dictionary<string, object?>
    {
        ["vscode.workspace"] = null,
        ["window.title"] = "PowerToys"
    }).Should().Be("PowerToys");
}

[Fact]
public void RejectsExecutableSyntax()
{
    var act = () => TemplateCompiler.Compile("{System.Diagnostics.Process.Start('cmd')}");
    act.Should().Throw<TemplateParseException>();
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test tests/CmdPalDockPlus.Core.Tests --filter TemplateCompilerTests`
Expected: FAIL with missing `TemplateCompiler`.

- [ ] **Step 3: Implement a deliberately small grammar**

Accepted token grammar:

```text
Template      := (Literal | Expression)*
Expression    := "{" Field ("??" Field)? (":" Format)? "}"
Field         := Identifier ("." Identifier)+
Identifier    := [A-Za-z_][A-Za-z0-9_]*
Format        := [A-Za-z0-9._-]+
```

No method calls, brackets, quotes, operators other than `??`, or nested expressions.

Implement `CompiledTemplate` as immutable segments and collect referenced fields during compilation.

- [ ] **Step 4: Add numeric format tests and implementation**

```csharp
[Fact]
public void FormatsNumericValue()
{
    var t = TemplateCompiler.Compile("{process.cpu:0.0}%");
    t.Evaluate(new Dictionary<string, object?> { ["process.cpu"] = 1.74 })
        .Should().Be("1.7%");
}
```

Support .NET numeric format strings after validating `Format` against the restricted grammar.

- [ ] **Step 5: Run all Core tests and commit**

```bash
dotnet test tests/CmdPalDockPlus.Core.Tests
git add src/CmdPalDockPlus.Core/Templates tests/CmdPalDockPlus.Core.Tests/Templates
git commit -m "feat: add safe dock text templates"
```

---

### Task 4: Track top-level windows and execute generic window actions

**Files:**
- Create: `src/CmdPalDockPlus.Windows/WindowSnapshot.cs`
- Create: `src/CmdPalDockPlus.Windows/IWindowBackend.cs`
- Create: `src/CmdPalDockPlus.Windows/Win32WindowBackend.cs`
- Create: `src/CmdPalDockPlus.Windows/WindowTracker.cs`
- Create: `src/CmdPalDockPlus.Windows/WindowActivator.cs`
- Create: `tests/CmdPalDockPlus.Windows.Tests/Fakes/FakeWindowBackend.cs`
- Create: `tests/CmdPalDockPlus.Windows.Tests/WindowTrackerTests.cs`
- Create: `tests/CmdPalDockPlus.Windows.Tests/WindowActivatorTests.cs`

**Interfaces:**
- Produces: `WindowSnapshot`, `IWindowTracker.Snapshot`, `IWindowTracker.Changed`, and action methods `FocusAsync`, `MinimizeAsync`, `MaximizeAsync`, `CloseAsync`.

- [ ] **Step 1: Test reconciliation without Win32**

```csharp
[Fact]
public async Task ReconcilePublishesOnlyRealChanges()
{
    var backend = new FakeWindowBackend(new WindowSnapshot((nint)42, 100, "Code.exe", "One", "Chrome_WidgetWin_1", WindowState.Restored, true, "DISPLAY1"));
    await using var tracker = new WindowTracker(backend);
    var changes = new List<WindowSetChanged>();
    tracker.Changed += (_, e) => changes.Add(e);

    await tracker.ReconcileAsync(default);
    await tracker.ReconcileAsync(default);

    tracker.Snapshot.Should().ContainSingle();
    changes.Should().HaveCount(1);
}
```

- [ ] **Step 2: Implement `IWindowBackend` and tracker**

```csharp
public interface IWindowBackend
{
    ValueTask<IReadOnlyList<WindowSnapshot>> EnumerateAsync(CancellationToken ct);
    ValueTask FocusAsync(nint hwnd, CancellationToken ct);
    ValueTask ShowAsync(nint hwnd, WindowShowCommand command, CancellationToken ct);
    ValueTask CloseAsync(nint hwnd, CancellationToken ct);
}
```

`Win32WindowBackend.EnumerateAsync` uses `EnumWindows`, filters invisible/tool/owned windows that should not be normal application tiles, reads PID/executable/title/class/state/monitor, and never blocks on an unresponsive target window for title retrieval.

- [ ] **Step 3: Add event-driven refresh**

Register `SetWinEventHook` for create/destroy/show/hide/name-change/minimize/foreground events. Event callbacks enqueue a single coalesced reconciliation on a background channel; they do not enumerate windows on the WinEvent callback thread.

Test with the fake backend by calling `tracker.RequestReconcile()` ten times and asserting one effective reconciliation after debounce.

- [ ] **Step 4: Implement actions and tests**

`FocusAsync` must restore minimized windows before foreground activation. `CloseAsync` sends a normal close request rather than terminating the process.

Run: `dotnet test tests/CmdPalDockPlus.Windows.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CmdPalDockPlus.Windows tests/CmdPalDockPlus.Windows.Tests
git commit -m "feat: track and control application windows"
```

---

### Task 5: Compose grouped/separate tile state

**Files:**
- Create: `src/CmdPalDockPlus.Core/Tiles/DockTileState.cs`
- Create: `src/CmdPalDockPlus.Core/Tiles/TileIdentity.cs`
- Create: `src/CmdPalDockPlus.Core/Tiles/TileComposer.cs`
- Create: `tests/CmdPalDockPlus.Core.Tests/Tiles/TileComposerTests.cs`

**Interfaces:**
- Consumes: `AppProfile`, window snapshots mapped into generic field dictionaries, compiled title/subtitle templates.
- Produces: deterministic `IReadOnlyList<DockTileState> Compose(AppProfile profile, IReadOnlyList<TileWindow> windows)`.

- [ ] **Step 1: Write grouped/separate tests**

```csharp
[Fact]
public void GroupedProfileProducesOneTileForThreeWindows()
{
    var profile = Fixtures.Profile(GroupingMode.Grouped);
    var tiles = _composer.Compose(profile, Fixtures.Windows("One", "Two", "Three"));
    tiles.Should().ContainSingle();
    tiles[0].Windows.Should().HaveCount(3);
}

[Fact]
public void SeparateProfileProducesStableTilePerWindow()
{
    var profile = Fixtures.Profile(GroupingMode.Separate);
    var tiles = _composer.Compose(profile, Fixtures.Windows("One", "Two"));
    tiles.Select(x => x.Identity.Value).Should().Equal("vscode:hwnd:1", "vscode:hwnd:2");
}
```

- [ ] **Step 2: Implement deterministic identity and MRU selection**

Grouped identity: `{profileId}:group:default`.
Separate identity: `{profileId}:hwnd:{hwnd-invariant-hex}`.

Grouped display evaluation uses the active window, else most-recently-used window, else the first deterministic HWND.

- [ ] **Step 3: Test zero-window pinned tile behavior**

A configured profile remains visible with zero windows and produces a launchable tile using profile display name and app icon fallback.

- [ ] **Step 4: Run Core tests and commit**

```bash
dotnet test tests/CmdPalDockPlus.Core.Tests
git add src/CmdPalDockPlus.Core/Tiles tests/CmdPalDockPlus.Core.Tests/Tiles
git commit -m "feat: compose grouped and separate dock tiles"
```

---

### Task 6: Adapt live tiles to Command Palette Dock

**Files:**
- Create: `src/CmdPalDockPlus.Extension/Dock/CmdPalDockPlusCommandsProvider.cs`
- Create: `src/CmdPalDockPlus.Extension/Dock/DockTileCommandItem.cs`
- Create: `src/CmdPalDockPlus.Extension/Dock/DockTileCommand.cs`
- Create: `src/CmdPalDockPlus.Extension/Dock/WindowContextCommands.cs`
- Create: `src/CmdPalDockPlus.Extension/Dock/DockCoordinator.cs`
- Create: `tests/CmdPalDockPlus.Core.Tests/Tiles/DockCoordinatorStateTests.cs`

**Interfaces:**
- Consumes: `IProfileStore`, `IWindowTracker`, `TileComposer`.
- Produces: `ICommandProvider3.GetDockBands()`, `ICommandProvider4.GetCommandItem(string id)` and mutable CmdPal `CommandItem` properties.

- [ ] **Step 1: Test stable command ids in a CmdPal-independent state projection**

```csharp
[Fact]
public void TileCommandIdIsStableAcrossTitleChanges()
{
    var first = DockCommandId.ForTile(new TileIdentity("vscode:hwnd:1"));
    var second = DockCommandId.ForTile(new TileIdentity("vscode:hwnd:1"));
    first.Should().Be(second).And.Be("tile:vscode:hwnd:1");
}
```

- [ ] **Step 2: Implement provider and dock bands**

`GetDockBands()` returns one `WrappedDockItem` for configured app tiles in profile order. Every returned item has a non-empty stable id. `GetCommandItem(id)` resolves nested tile ids so users can pin individual tiles where the SDK allows it.

- [ ] **Step 3: Implement primary click behavior**

```text
0 windows -> launch configured executable/AUMID
1 window  -> restore/focus
N windows -> restore/focus active/MRU window
```

`MoreCommands` for a tile contains each current window plus generic actions: New instance, Minimize, Maximize, Close, Close all, Open file location when available.

- [ ] **Step 4: Make Title/Subtitle/Icon updates incremental**

`DockCoordinator` compares new `DockTileState` with last state; mutate only changed `Icon`, `Title`, `Subtitle` or `MoreCommands`. Coalesce rapid window-title changes to at most one UI projection per 50 ms.

- [ ] **Step 5: Build x64 package smoke test and commit**

Run:

```powershell
dotnet build src/CmdPalDockPlus.Extension/CmdPalDockPlus.Extension.csproj -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true
```

Expected: build completes and emits an MSIX package directory.

Commit:

```bash
git add src/CmdPalDockPlus.Extension tests/CmdPalDockPlus.Core.Tests/Tiles
git commit -m "feat: render live app and window dock tiles"
```

---

### Task 7: Add the first profile setup/settings flow and foundation README

**Files:**
- Create: `src/CmdPalDockPlus.Extension/Settings/DockPlusSettings.cs`
- Create: `src/CmdPalDockPlus.Extension/Settings/SettingsPage.cs`
- Create: `src/CmdPalDockPlus.Extension/Settings/ProfileEditorPage.cs`
- Create: `src/CmdPalDockPlus.Extension/Settings/ApplicationPickerPage.cs`
- Create: `src/CmdPalDockPlus.Extension/Settings/TemplateFieldsPage.cs`
- Modify: `README.md`
- Create: `tests/CmdPalDockPlus.Core.Tests/Profiles/ProfileValidationTests.cs`

**Interfaces:**
- Consumes: profile store and window snapshot list.
- Produces: create/edit/delete/reorder profile workflow and persisted valid profiles.

- [ ] **Step 1: Define and test validation**

```csharp
[Fact]
public void ProfileRequiresExecutableOrAumid()
{
    var profile = Fixtures.Profile(application: new ApplicationMatch(null, null));
    ProfileValidator.Validate(profile).Errors.Should().Contain("application.target.required");
}
```

Also test duplicate profile ids, invalid templates, and missing display name.

- [ ] **Step 2: Implement the settings landing page**

The page lists profiles with commands: Add app, Edit, Move up/down, Delete. `Add app` offers currently running apps first and an executable-path text field fallback. Saving writes through `IProfileStore` and triggers `DockCoordinator.ReloadProfilesAsync()`.

- [ ] **Step 3: Implement display/grouping controls for foundation scope**

Controls:

```text
Grouping: Grouped | Separate
Title template: text field
Subtitle template: text field
Preview: evaluated against selected/running window
Primary click: automatic (foundation default)
```

The field picker initially exposes generic foundation fields: `app.name`, `process.executable`, `window.title`, `window.state`, `window.isActive`, `window.monitor`, `window.count`.

- [ ] **Step 4: Write the foundation README sections**

`README.md` must already explain, with exact UI paths:

1. prerequisites;
2. installing the release MSIX once release workflow exists;
3. enabling Command Palette and Dock;
4. finding CmdPal Dock Plus settings;
5. creating a first app tile;
6. Grouped vs Separate;
7. Title/Subtitle template examples;
8. generic click/context behavior;
9. where configuration is stored;
10. how to reset it.

Mark later-plan features as “planned/not present in this slice” rather than documenting controls that do not yet exist.

- [ ] **Step 5: Run full managed test/build suite and commit**

```powershell
dotnet test CmdPalDockPlus.sln -c Release -p:Platform=x64
dotnet build CmdPalDockPlus.sln -c Release -p:Platform=ARM64
```

Expected: PASS.

```bash
git add src/CmdPalDockPlus.Extension/Settings README.md tests/CmdPalDockPlus.Core.Tests/Profiles
git commit -m "feat: add dock tile profile setup"
```

---

## Slice acceptance check

Before moving to the providers/smart-grouping plan, verify all of the following on a Windows desktop:

```text
[ ] Extension installs and is discovered by Command Palette.
[ ] A Code.exe profile can be created without editing JSON.
[ ] Zero running windows shows a launchable tile.
[ ] One running window is focused/restored on click.
[ ] Multiple grouped windows focus the active/MRU window.
[ ] Separate mode renders one tile per eligible window.
[ ] Window title changes update the configured Title/Subtitle live.
[ ] Closing/opening windows updates Dock tiles without restarting CmdPal.
[ ] Context menu can focus and close an individual window.
[ ] All managed tests pass for x64; ARM64 solution build succeeds.
```
