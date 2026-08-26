# Rich Window UX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add richer per-app/window menus, recent/frequent destinations, and live DWM hover previews through a minimal, versioned PowerToys compatibility patch while keeping the normal extension fully functional without that patch.

**Architecture:** Keep menu composition in the extension and DWM preview rendering in a dedicated Windows service. A tiny PowerToys patch exposes Dock hover identity through a compatibility bridge; it does not own profile logic or thumbnail rendering. Recent/Frequent data is treated as optional source data for the Smart App Menu, never as a requirement to clone Windows Jump Lists exactly.

**Tech Stack:** .NET 10, Win32/DWM (`DwmRegisterThumbnail`, `DwmUpdateThumbnailProperties`, `DwmUnregisterThumbnail`), shell destination-list APIs where publicly available, current PowerToys CmdPal source patch, xUnit/FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-08-26-cmdpal-dock-plus-design.md`

## Global Constraints

- Exact native Jump List cloning is not a goal.
- Existing pinned native Jump List items are not imported.
- Preview support is optional; absence/incompatibility of the PowerToys patch disables only hover previews.
- No global mouse-position polling/synthetic-hover workaround is accepted as the primary preview path.
- The compatibility patch must remain small and auditable against known PowerToys versions.

---

### Task 1: Build Recent/Frequent destination abstraction

**Files:**
- Create: `src/CmdPalDockPlus.Windows/Destinations/AppDestination.cs`
- Create: `src/CmdPalDockPlus.Windows/Destinations/IAppDestinationSource.cs`
- Create: `src/CmdPalDockPlus.Windows/Destinations/ShellDestinationSource.cs`
- Create: `tests/CmdPalDockPlus.Windows.Tests/Destinations/ShellDestinationSourceTests.cs`

**Interfaces:**
- Produces: `GetRecentAsync(AppIdentity, int limit, CancellationToken)` and `GetFrequentAsync(...)`.

- [ ] **Step 1: Define deterministic destination model and fake-source test**

```csharp
public sealed record AppDestination(
    string Id,
    string DisplayName,
    string? Path,
    string? Arguments,
    DestinationKind Kind);

[Fact]
public async Task ResultsAreDeduplicatedByCanonicalIdentity()
{
    var source = new FakeDestinationSource([
        new("a", "Repo", @"D:\Repo", null, DestinationKind.Recent),
        new("a", "Repo", @"D:\Repo", null, DestinationKind.Recent)]);

    (await DestinationDeduplicator.ReadAsync(source, Fixtures.AppIdentity(), 10, default))
        .Should().ContainSingle();
}
```

- [ ] **Step 2: Implement public-shell retrieval only**

Use public destination/document-list APIs for Recent/Frequent associated with an AppUserModelID when available. Return an empty collection when the app has no resolvable AUMID/document list rather than scraping Explorer internals.

- [ ] **Step 3: Add path/action validation**

Destinations are display/open targets only. Validate filesystem targets before exposing an “Open” action; preserve URI targets when the shell item resolves to a URI.

- [ ] **Step 4: Test graceful unsupported behavior and commit**

```bash
dotnet test tests/CmdPalDockPlus.Windows.Tests --filter Destination
git add src/CmdPalDockPlus.Windows/Destinations tests/CmdPalDockPlus.Windows.Tests/Destinations
git commit -m "feat: read recent and frequent app destinations"
```

---

### Task 2: Compose Smart App Menu pages

**Files:**
- Create: `src/CmdPalDockPlus.Extension/Menus/SmartAppMenuPage.cs`
- Create: `src/CmdPalDockPlus.Extension/Menus/WindowMenuSection.cs`
- Create: `src/CmdPalDockPlus.Extension/Menus/DestinationMenuSection.cs`
- Create: `src/CmdPalDockPlus.Extension/Menus/AppActionMenuSection.cs`
- Create: `tests/CmdPalDockPlus.Core.Tests/Menus/SmartAppMenuModelTests.cs`

**Interfaces:**
- Consumes: current tile windows, destination source, provider actions, generic actions.
- Produces ordered sections: Windows, Recent, Frequent, App actions, User actions.

- [ ] **Step 1: Write menu ordering tests**

```csharp
[Fact]
public void SmartMenuKeepsStableSectionOrder()
{
    var model = SmartAppMenuModel.Compose(
        windows: Fixtures.Windows("A", "B"),
        recent: [Fixtures.Destination("r")],
        frequent: [Fixtures.Destination("f")],
        actions: [Fixtures.Action("new-window")]);

    model.Sections.Select(x => x.Id).Should()
        .Equal("windows", "recent", "frequent", "actions");
}
```

- [ ] **Step 2: Implement menu-page mapping**

Window entries expose Focus, Restore/Minimize, Maximize, Close. Destination entries expose Open. App/provider actions preserve provider-provided display names/icons and stable ids.

- [ ] **Step 3: Add empty-section suppression**

Do not render Recent/Frequent headers when those lists are empty. Never replace a working window/action menu with an error page if destination retrieval fails.

- [ ] **Step 4: Integrate with tile `MoreCommands` and commit**

```bash
dotnet test tests/CmdPalDockPlus.Core.Tests --filter SmartAppMenu
git add src/CmdPalDockPlus.Extension/Menus tests/CmdPalDockPlus.Core.Tests/Menus
git commit -m "feat: add smart app and window menus"
```

---

### Task 3: Implement host-independent DWM preview service

**Files:**
- Create: `src/CmdPalDockPlus.Windows/Previews/IThumbnailPreviewService.cs`
- Create: `src/CmdPalDockPlus.Windows/Previews/DwmThumbnailPreviewService.cs`
- Create: `src/CmdPalDockPlus.Windows/Previews/PreviewLayout.cs`
- Create: `tests/CmdPalDockPlus.Windows.Tests/Previews/PreviewLayoutTests.cs`

**Interfaces:**
- Produces: `ShowAsync(PreviewRequest)`, `UpdateAsync(PreviewRequest)`, `HideAsync()`.

- [ ] **Step 1: Test multi-window preview layout without DWM**

```csharp
[Theory]
[InlineData(1, 320, 220)]
[InlineData(2, 640, 220)]
[InlineData(4, 640, 440)]
public void LayoutUsesBoundedGrid(int count, int maxWidth, int maxHeight)
{
    var layout = PreviewLayout.Calculate(count, new Size(maxWidth, maxHeight));
    layout.Cells.Should().HaveCount(count);
    layout.Bounds.Width.Should().BeLessThanOrEqualTo(maxWidth);
    layout.Bounds.Height.Should().BeLessThanOrEqualTo(maxHeight);
}
```

- [ ] **Step 2: Implement an owned top-level preview window**

The preview destination must be a top-level window owned by CmdPal Dock Plus. Register one DWM thumbnail per source top-level HWND and unregister every thumbnail on hide/dispose/source destruction.

- [ ] **Step 3: Implement live property updates**

Use `DwmUpdateThumbnailProperties` to set source/destination rectangles, opacity and visibility. Do not capture screenshots on a timer.

- [ ] **Step 4: Add click targets**

Each cell maps to one source HWND. Left click focuses/restores; a close affordance requests normal window close. Preview actions call existing `WindowActivator` methods.

- [ ] **Step 5: Run layout/unit tests and commit**

```bash
dotnet test tests/CmdPalDockPlus.Windows.Tests --filter Preview
git add src/CmdPalDockPlus.Windows/Previews tests/CmdPalDockPlus.Windows.Tests/Previews
git commit -m "feat: render live DWM window previews"
```

---

### Task 4: Define a stable hover bridge protocol

**Files:**
- Create: `src/CmdPalDockPlus.Core/Compatibility/HoverEvent.cs`
- Create: `src/CmdPalDockPlus.Extension/Compatibility/IHoverEventSource.cs`
- Create: `src/CmdPalDockPlus.Extension/Compatibility/NamedPipeHoverEventSource.cs`
- Create: `tests/CmdPalDockPlus.Core.Tests/Compatibility/HoverEventProtocolTests.cs`
- Create: `powertoys/README.md`

**Interfaces:**
- Produces protocol messages `Enter(tileCommandId, anchorRect)` and `Leave(tileCommandId)`.

- [ ] **Step 1: Define protocol test vectors**

```json
{"version":1,"kind":"enter","commandId":"tile:vscode:hwnd:2A","x":100,"y":1040,"width":48,"height":40}
{"version":1,"kind":"leave","commandId":"tile:vscode:hwnd:2A"}
```

Tests must reject unknown major version, missing command id, negative width/height and payloads over 8 KiB.

- [ ] **Step 2: Implement local named-pipe source**

Pipe is per-user-session; accept only local clients. Parse complete newline-delimited UTF-8 JSON messages with a hard 8 KiB message cap.

- [ ] **Step 3: Document compatibility boundary**

`powertoys/README.md` states that the upstream host patch knows only stable Dock command ids and item rectangles; it does not know profiles, windows, DWM or native hooks.

- [ ] **Step 4: Test and commit**

```bash
dotnet test tests/CmdPalDockPlus.Core.Tests --filter HoverEvent
git add src/CmdPalDockPlus.Core/Compatibility src/CmdPalDockPlus.Extension/Compatibility tests/CmdPalDockPlus.Core.Tests/Compatibility powertoys/README.md
git commit -m "feat: define dock hover compatibility bridge"
```

---

### Task 5: Create the minimal PowerToys Dock hover patch

**Files:**
- Create: `powertoys/patches/README.md`
- Create: `powertoys/patches/cmdpal-dock-hover.patch`
- Create: `powertoys/patches/upstream-commit.txt`
- Create: `scripts/verify-powertoys-patch.ps1`

**Interfaces:**
- Consumes: PowerToys `DockItemControl` pointer enter/exit and rendered command id/anchor bounds.
- Produces: `Enter/Leave` messages to the local bridge.

- [ ] **Step 1: Pin the upstream base commit**

Write the exact PowerToys commit used for the patch to `upstream-commit.txt`. The implementation worker must fetch that commit before generating the patch; do not patch “whatever main is” during a release.

- [ ] **Step 2: Patch only the Dock hover path**

The patch adds a tiny `DockHoverBridge` service to CmdPal UI and calls it from existing `PointerEntered`/`PointerExited` handlers. It sends only command id + screen-space bounds. It must not reference CmdPal Dock Plus assemblies.

- [ ] **Step 3: Add patch verification script**

```powershell
param([Parameter(Mandatory)][string]$PowerToysRoot)
$patch = Join-Path $PSScriptRoot '..\powertoys\patches\cmdpal-dock-hover.patch'
git -C $PowerToysRoot apply --check $patch
if ($LASTEXITCODE -ne 0) { throw 'CmdPal Dock Plus hover patch no longer applies cleanly.' }
```

- [ ] **Step 4: Verify against the pinned checkout and commit**

Run: `pwsh scripts/verify-powertoys-patch.ps1 -PowerToysRoot C:\src\PowerToys`
Expected: succeeds against the pinned commit and fails clearly against incompatible source.

```bash
git add powertoys scripts/verify-powertoys-patch.ps1
git commit -m "feat: add minimal CmdPal dock hover patch"
```

---

### Task 6: Connect hover events to tile previews

**Files:**
- Create: `src/CmdPalDockPlus.Extension/Previews/HoverPreviewCoordinator.cs`
- Modify: `src/CmdPalDockPlus.Extension/Dock/DockCoordinator.cs`
- Create: `tests/CmdPalDockPlus.Core.Tests/Previews/HoverPreviewStateMachineTests.cs`

**Interfaces:**
- Consumes: hover events and current `DockTileState` lookup by stable command id.
- Produces: bounded show/hide behavior with no orphan preview.

- [ ] **Step 1: Test state transitions**

```csharp
[Fact]
public void EnterAThenEnterBReplacesPreviewTarget()
{
    var state = new HoverPreviewStateMachine();
    state.Apply(HoverEvent.Enter("tile:a", Fixtures.Rect()));
    state.Apply(HoverEvent.Enter("tile:b", Fixtures.Rect()));
    state.CurrentCommandId.Should().Be("tile:b");
}
```

Also test duplicate Enter, Leave for stale id, tile deletion while hovered, and bridge disconnect.

- [ ] **Step 2: Add hover delay and close delay**

Default open delay 250 ms; hide after 150 ms unless pointer enters preview window. Use cancellable timers/tasks, not DispatcherTimer accumulation per tile.

- [ ] **Step 3: Resolve preview windows at display time**

Lookup tile identity after delay so closed/new windows are reflected. One window shows one DWM thumbnail; grouped tile shows bounded grid.

- [ ] **Step 4: Disable cleanly when bridge is unavailable**

No warning loop. Settings diagnostics show `Hover previews: compatibility bridge not connected`.

- [ ] **Step 5: Run tests and commit**

```bash
dotnet test tests/CmdPalDockPlus.Core.Tests --filter HoverPreview
git add src/CmdPalDockPlus.Extension/Previews src/CmdPalDockPlus.Extension/Dock tests/CmdPalDockPlus.Core.Tests/Previews
git commit -m "feat: connect dock hover to live previews"
```

---

### Task 7: Document and manually verify rich window UX

**Files:**
- Modify: `README.md`
- Modify: `powertoys/README.md`
- Create: `docs/testing/rich-window-ux-checklist.md`

**Interfaces:**
- Documentation/release checklist only.

- [ ] **Step 1: Add README Smart App Menu section**

Show exact examples for Windows, Recent/Frequent and app-specific actions. Explicitly say native pinned/custom Jump List categories are not imported.

- [ ] **Step 2: Add optional preview installation section**

Explain stock extension behavior first, then the compatible PowerToys build/patch path. Include exact supported PowerToys commit/version once implementation pins it.

- [ ] **Step 3: Write manual preview checklist**

Verify one/multiple windows, minimized source, closing source during preview, switching hovered tiles, multiple monitors, Dock top/bottom/side orientations if supported, auto-hide, light/dark, CmdPal restart and patch absence.

- [ ] **Step 4: Commit**

```bash
git add README.md powertoys/README.md docs/testing/rich-window-ux-checklist.md
git commit -m "docs: document smart menus and hover previews"
```

---

## Slice acceptance check

```text
[ ] Right-click/MoreCommands exposes current windows and app actions.
[ ] Recent/Frequent appears only when public shell data exists.
[ ] No native pinned Jump List data is claimed/imported.
[ ] DWM preview remains live without screenshot polling.
[ ] One grouped tile can preview multiple current windows.
[ ] Hover bridge patch touches only the CmdPal Dock hover path and local bridge code.
[ ] Without the patch, every non-preview feature still works.
[ ] Patch applicability is verifiable against a pinned PowerToys base.
```
