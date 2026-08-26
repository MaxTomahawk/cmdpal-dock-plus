# CmdPal Dock Plus — Architecture Design

**Date:** 2026-08-26  
**Repository:** `MaxTomahawk/cmdpal-dock-plus`  
**Visibility:** Public  
**Status:** Design review

## 1. Goal

Build **CmdPal Dock Plus**, a PowerToys Command Palette Dock extension that turns ordinary dock shortcuts into configurable, live **App/Window Tiles**.

The project extends the Command Palette Dock rather than replacing Windows Explorer or implementing an independent taskbar. It adds:

- app launch/focus/minimize/restore behavior;
- grouped, separate, and rule-based window tiles;
- dynamic title/subtitle/icon content;
- discoverable per-app data fields and actions;
- app-specific data providers/adapters;
- per-window actions and context menus;
- window previews;
- recent/frequent destinations and richer app menus;
- custom badges, progress and attention states;
- optional native taskbar-state capture;
- third-party system tray mirroring;
- event-driven system controls such as volume, network and battery;
- settings entirely centered around configuring Dock tiles.

The project must be installable from **GitHub Releases**. Release binaries must be built by **GitHub Actions**, not manually uploaded local builds.

---

## 2. Non-goals

The following are explicitly out of scope:

- importing the user's existing native taskbar pins;
- replacing `explorer.exe`;
- hiding or destroying the Windows taskbar as part of the core extension;
- byte-for-byte cloning of Windows Jump Lists;
- free Dock resizing;
- hard dependencies on unsupported hooks for the basic Dock experience;
- polling the desktop continuously when Windows exposes an event-driven mechanism.

Unsupported native hooks may exist only in separately isolated optional components.

---

## 3. Product model

The central object is a **Dock Tile Profile**.

A profile identifies an application and determines:

1. which windows belong to it;
2. whether those windows are grouped or separated;
3. which data providers may inspect them;
4. which values are shown in the Dock;
5. which actions are available;
6. which rules may override grouping, display or behavior.

Example:

```text
Visual Studio Code profile

Application match:
    executable = Code.exe

Grouping:
    Smart

Title:
    {vscode.workspace ?? window.title}

Subtitle:
    {vscode.file} · {window.state}

Primary action:
    FocusWindow

Rules:
    workspace == "PowerToys"   -> Separate
    workspace == "Website"     -> Separate
    otherwise                  -> Group
```

A single application profile may therefore render one tile or many tiles.

---

## 4. User-visible Dock tile

A tile maps naturally onto the current Command Palette Dock item contract:

```text
┌──────────────────────────────┐
│ [icon] Title                 │
│        Subtitle              │
└──────────────────────────────┘
```

The extension controls these mutable values:

- `Icon`
- `Title`
- `Subtitle`
- primary `Command`
- `MoreCommands`

The Dock host remains responsible for layout, hover chrome, compact mode and placement.

### Display modes

Each profile supports:

- **Icon only**
- **Icon + title**
- **Icon + title + subtitle**
- **Automatic** — hide text when no configured dynamic value is available

The existing Command Palette Dock currently limits title/subtitle width, so templates must degrade gracefully with ellipsis.

---

## 5. Window grouping model

Every App Profile has one of three base modes.

### 5.1 Grouped

All matched windows become one Dock tile.

Default primary-click behavior:

- zero windows: launch application;
- one window: restore/focus it;
- multiple windows: restore/focus the most-recently-used window.

The tile's context menu exposes all windows.

### 5.2 Separate

Each eligible top-level application window becomes its own tile.

Example:

```text
[VS] PowerToys
[VS] Website
[VS] Notes
```

Each tile receives its own:

- title/subtitle;
- progress;
- badge;
- attention state;
- preview;
- actions.

### 5.3 Smart

Rules map windows into explicit groups.

Examples:

```text
VS Code
workspace = PowerToys       -> separate
workspace = HomeAssistant   -> separate
everything else             -> group "Other"

Edge
profile = Work              -> group "Edge — Work"
profile = Personal          -> group "Edge — Personal"
InPrivate                   -> separate

Explorer
path starts D:\Projects     -> separate
Downloads/Desktop           -> group
tool/dialog window          -> hide
```

The result of rule evaluation is a **Tile Identity**. Windows sharing the same identity render as one tile.

---

## 6. Rules engine

Rules operate on fields exposed by data providers.

### Conditions

Initial operators:

- equals / not equals
- contains / not contains
- starts with / ends with
- regex match
- exists / missing
- numeric greater / less
- boolean true / false

Initial generic fields include:

- `app.id`
- `app.name`
- `process.executable`
- `process.pid`
- `window.hwnd`
- `window.title`
- `window.class`
- `window.state`
- `window.isActive`
- `window.isMinimized`
- `window.monitor`
- `window.desktop`
- `window.count`

Provider-specific fields are automatically available when that provider supports the current app/window.

### Actions

A matching rule may:

- group;
- separate;
- hide;
- set group key;
- override icon template;
- override title template;
- override subtitle template;
- override click action;
- add/remove context actions.

Rules are evaluated in user-defined order. First terminal grouping action wins; display overrides may accumulate until that point.

---

## 7. Data provider system

The extension uses a provider model rather than hardcoded per-application UI.

```csharp
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

`ProbeAsync` returns:

- which fields are available;
- field names and descriptions;
- type information;
- example/current values;
- supported actions;
- whether updates are event-driven or sampled.

`WatchAsync` only monitors fields the user actually selected.

### Built-in providers

#### WindowProvider

Fields:

- title
- active state
- minimized/restored/maximized
- monitor
- virtual desktop when available
- window class
- window count
- MRU position

Primary mechanism:

- `EnumWindows` for reconciliation;
- `SetWinEventHook` for lifecycle/focus/name/state updates.

#### ProcessProvider

Fields:

- executable
- PID
- CPU
- working set / memory
- uptime

CPU/memory sampling is disabled unless selected in a template/rule.

Sampling intervals are configurable and coalesced per process.

#### MediaProvider

Uses supported Windows media-session APIs.

Fields may include:

- title
- artist
- album
- playback state
- source application

#### TaskbarStateProvider

Fields:

- progress value
- progress state
- overlay image
- overlay description

This provider is optional and isolated because generic capture requires native interception.

#### App-specific providers

Initial adapters targeted for implementation:

- Visual Studio Code
- Chromium-based browsers where reliable metadata is available
- Windows Terminal
- Explorer
- media applications via the generic media provider

App adapters may parse window titles only as a fallback. Stable supported interfaces or local app metadata are preferred.

---

## 8. Tile setup experience

The extension exposes a Command Palette settings page.

### New tile flow

1. **Choose application**
   - running application;
   - executable;
   - packaged application/AUMID.

2. **Choose matching scope**
   - all application windows;
   - matching title/class rules;
   - selected current window as a rule template.

3. **Probe capabilities**

   The setup page asks providers what the application/window currently exposes.

   Example:

   ```text
   Available data

   Window
   ✓ Window title          "PowerToys — Visual Studio Code"
   ✓ State                 Restored
   ✓ Active                Yes
   ✓ Monitor               DISPLAY1

   Process
   ✓ CPU                   1.7%
   ✓ Memory                842 MB

   Visual Studio Code
   ✓ Workspace             PowerToys
   ✓ Current file          DockItemControl.xaml
   ✓ Remote                Local

   Media
   — Not available
   ```

4. **Choose grouping**
   - Grouped
   - Separate
   - Smart

5. **Configure display**
   - icon
   - title
   - subtitle

6. **Configure actions**
   - primary
   - middle-click where supported by the host
   - context commands

7. **Add optional rules**

8. **Preview resulting tiles**

9. **Save**

Profiles are persisted as versioned JSON.

---

## 9. Template engine

Display values use a small safe template language.

Examples:

```text
{window.title}
{vscode.workspace ?? window.title}
{process.cpu:0.0}% · {process.memory:mb} MB
{media.artist} — {media.title}
```

Required functionality:

- field substitution;
- null fallback with `??`;
- simple formatting;
- literal text;
- no arbitrary scripting;
- no embedded C#/PowerShell/JavaScript.

Templates compile to a dependency set. Only referenced provider fields are monitored.

This is critical for performance: a tile that only renders `{window.title}` must not sample CPU, query media or start a taskbar-capture session.

---

## 10. Application and window actions

### Generic application actions

- Launch
- New instance
- Run as administrator
- Open file location
- App settings when a supported URI is available
- Restart application
- Close all application windows

### Generic window actions

- Focus
- Restore
- Minimize
- Maximize
- Close
- Move to monitor
- show all windows belonging to the tile

### App-specific actions

Adapters may add commands such as:

VS Code:

- New Window
- Open Folder
- Open recent workspace/project
- Open terminal in workspace

Browser:

- New Window
- New InPrivate/Incognito window
- new window for a configured profile

Terminal:

- PowerShell
- Command Prompt
- WSL distribution
- administrator shell

App-specific commands are exposed through `MoreCommands` and, where appropriate, nested list/content pages.

---

## 11. Jump-list replacement

CmdPal Dock Plus does **not** attempt to clone the complete native Jump List of another application.

Instead it provides a richer **Smart App Menu** containing:

- windows;
- Recent destinations when available through supported Windows APIs;
- Frequent destinations when available;
- app-specific actions;
- user-defined actions;
- generic application/window actions.

Existing native pinned Jump List destinations are not imported.

---

## 12. Window previews

Taskbar-style live previews use DWM thumbnails.

Architecture:

```text
Dock item hover
      |
      v
CmdPal host hover bridge
      |
      v
ThumbnailPreviewService
      |
      +--> DwmRegisterThumbnail(source HWND)
      +--> DwmUpdateThumbnailProperties(...)
      |
      v
owned preview window
```

The current extension API does not expose Dock-item pointer enter/leave events. Therefore exact hover previews require a **small PowerToys Command Palette host patch**.

The patch must remain minimal:

- expose or route Dock item hover identity;
- leave preview/window logic in CmdPal Dock Plus code where possible;
- never fork unrelated PowerToys behavior.

The project will keep the patch as a clearly separated compatibility component.

If the patch is not installed, all normal tiles still work and windows remain accessible via click/context commands. The basic extension must never depend on the patched host.

---

## 13. Progress and overlay badges

### 13.1 Dock-owned state

Any provider can supply:

- `progress.current`
- `progress.total`
- `progress.state`
- badge text/image
- attention state

The extension can represent these through dynamic icon composition and text.

### 13.2 Native taskbar state capture

Generic third-party applications normally send taskbar progress/overlay state through `ITaskbarList3`.

There is no supported system-wide getter/subscription API for another process.

Therefore the concrete implementation is:

```text
Target application
      |
      | ITaskbarList3 calls
      v
TaskbarCapture hook/wrapper
      |
      +--> original taskbar COM object
      |
      +--> local IPC
                 |
                 v
        TaskbarStateProvider
                 |
                 v
             Dock tile
```

The capture module is:

- optional;
- disabled by default;
- architecture-specific;
- isolated from the main extension;
- never required for normal application/window tracking.

The capture layer records at least:

- `SetProgressState`
- `SetProgressValue`
- `SetOverlayIcon`

It copies overlay pixels inside the target process before sending them through IPC.

### Capture policy

Injection is never global by default.

Users explicitly enable native taskbar capture per App Profile. Only matching processes are targeted.

This limits compatibility and security impact.

---

## 14. Attention state

CmdPal Dock Plus defines its own provider-level attention model:

```text
None
Informational
Attention
Urgent
```

Sources may include:

- app adapter events;
- workflow state;
- captured native signals when available;
- generic window heuristics.

Exact reproduction of every Windows taskbar flashing case is not a hard dependency.

The Dock renders attention through a dynamic icon/badge/text state. A future host patch may add a native animation state, but the data model does not depend on it.

---

## 15. Third-party system tray

Third-party notification-area icons are implemented as a separate optional component:

```text
explorer.exe
    |
    | notification-area messages
    v
CmdPalDockPlus.SysTrayHook.dll
    |
    | named pipe
    v
CmdPalDockPlus.SysTrayBridge
    |
    v
Dock items
```

The design is based on the proven `SysTrayCmdPal` technique.

### Hook responsibilities

The DLL loaded into Explorer must do only:

- intercept relevant notification-area messages;
- parse the minimum required data;
- copy icon pixels when `NIF_ICON` changes;
- forward add/modify/delete events through local IPC.

It must not contain:

- UI;
- profile logic;
- CmdPal objects;
- settings;
- web/network access;
- heavyweight logging;
- application discovery.

### Runtime behavior

The main path is event-driven.

No 3-second permanent UI Automation polling loop is used when the hook is healthy.

Icon pixels are kept in memory and surfaced to CmdPal through stream-backed `IconData`; changing tray icons are **not** continuously written to `%TEMP%`.

UI Automation is fallback/reconciliation only.

### Interactions

Where notification callback information is available, left/right/middle actions are replayed through the owning application's callback rather than moving the real mouse.

Synthesized input is fallback-only.

---

## 16. Windows system controls

Windows 11 shell-owned combined indicators are not reverse-engineered.

CmdPal Dock Plus provides equivalent first-class controls through supported APIs.

Initial system providers:

### Volume

Event-driven Core Audio endpoint notifications.

Fields/actions:

- volume percentage
- muted state
- mute toggle
- volume up/down

### Network

Event-driven Windows networking status notifications.

Fields/actions:

- connectivity state
- connection/profile display where supported
- open network settings

### Battery/power

Power notification APIs.

Fields:

- battery percentage
- AC/battery state
- charging state where available

### Notifications

Use the Command Palette Dock/Windows Notification Center integration exposed by current PowerToys rather than recreating Notification Center.

---

## 17. Runtime component boundaries

```text
CmdPalDockPlus.Extension
│
├── Profiles
│   ├── profile persistence
│   ├── settings pages
│   └── migrations
│
├── Tiles
│   ├── TileManager
│   ├── GroupingEngine
│   ├── RuleEngine
│   └── TemplateEngine
│
├── Windows
│   ├── WindowTracker
│   ├── WindowActivator
│   └── WindowCommands
│
├── Providers
│   ├── ProviderHost
│   ├── WindowProvider
│   ├── ProcessProvider
│   ├── MediaProvider
│   ├── System providers
│   └── app adapters
│
└── Native clients
    ├── SysTray client
    └── TaskbarCapture client

CmdPalDockPlus.SysTrayBridge
└── SysTrayHook.dll

CmdPalDockPlus.TaskbarCapture
├── injector/controller
└── architecture-specific hook DLLs

PowerToys compatibility patch
└── Dock hover -> preview integration
```

The main extension must start and remain functional if either optional native component fails.

---

## 18. State and IPC

Local native components communicate with the extension through versioned named-pipe protocols.

Messages are explicit envelopes:

```text
ProtocolVersion
MessageType
SourceProcessId
Sequence
Payload
```

The protocol rejects:

- unknown future major versions;
- oversized messages;
- invalid window/process ownership;
- malformed icon dimensions;
- stale sequence updates where ordering matters.

Native bridges send state changes, not arbitrary executable commands.

---

## 19. Performance requirements

The design favors events over polling.

### Required behavior

- Window changes use WinEvent hooks plus periodic low-frequency reconciliation.
- Provider watches are reference-counted by requested fields.
- CPU/memory are sampled only when used by a rule/template.
- Tray contents are event-driven after initialization.
- Icon pixel encoding stays in memory.
- UI updates are deduplicated/coalesced.
- Repeated identical provider values do not trigger CmdPal property changes.
- App-specific adapters must define their cost and refresh model.
- Optional hooks do no expensive work on Explorer/application UI threads.

### Performance budgets

Initial engineering targets on an idle system with 10 configured profiles:

- main extension idle CPU: effectively near zero, target `<0.2%` averaged over 60 seconds;
- no continuous disk writes for dynamic icon state;
- no UIA whole-tree scan more often than necessary after an event;
- tray hook work proportional to actual notification-area changes;
- provider exceptions must not create retry loops faster than one second.

These are engineering targets, not guarantees across all hardware.

---

## 20. Failure isolation

### Provider failure

A failing provider is disabled for the affected target after bounded retries. Generic WindowProvider functionality remains.

### App-specific adapter failure

Fall back to generic fields/actions.

### SysTray hook failure

Hide/disable third-party tray band and offer diagnostic information. Do not restart Explorer automatically.

### Taskbar capture failure

Remove captured progress/overlay state and continue with normal tiles.

### PowerToys hover patch absent/incompatible

Disable hover preview feature only.

### Explorer restart

Reconnect the tray bridge, rebuild tray state and continue without restarting CmdPal where possible.

---

## 21. Configuration persistence

Profiles use human-readable versioned JSON under the extension's application data directory.

Conceptual schema:

```json
{
  "schemaVersion": 1,
  "profiles": [
    {
      "id": "vscode",
      "application": {
        "executable": "Code.exe"
      },
      "grouping": {
        "mode": "smart",
        "rules": []
      },
      "display": {
        "title": "{vscode.workspace ?? window.title}",
        "subtitle": "{vscode.file}",
        "icon": "{app.icon}"
      },
      "nativeCapture": {
        "taskbarState": false
      }
    }
  ]
}
```

Migrations are explicit and tested. Unknown properties are preserved where practical to avoid destroying configuration created by newer builds.

---

## 22. Repository structure

```text
cmdpal-dock-plus/
├── README.md
├── LICENSE
├── CmdPalDockPlus.sln
├── Directory.Build.props
├── docs/
│   └── superpowers/
│       ├── specs/
│       │   └── 2026-08-26-cmdpal-dock-plus-design.md
│       └── plans/
├── src/
│   ├── CmdPalDockPlus.Extension/
│   ├── CmdPalDockPlus.Core/
│   ├── CmdPalDockPlus.Windows/
│   ├── CmdPalDockPlus.Providers/
│   ├── CmdPalDockPlus.Adapters.VSCode/
│   ├── CmdPalDockPlus.Adapters.Browsers/
│   ├── CmdPalDockPlus.Adapters.Terminal/
│   ├── CmdPalDockPlus.SysTrayBridge/
│   ├── CmdPalDockPlus.SysTrayHook/
│   ├── CmdPalDockPlus.TaskbarCapture/
│   └── CmdPalDockPlus.TaskbarHook/
├── tests/
│   ├── CmdPalDockPlus.Core.Tests/
│   ├── CmdPalDockPlus.Windows.Tests/
│   ├── CmdPalDockPlus.Providers.Tests/
│   └── CmdPalDockPlus.Protocol.Tests/
├── powertoys/
│   ├── patches/
│   └── README.md
├── scripts/
└── .github/
    └── workflows/
        ├── ci.yml
        └── release.yml
```

Projects may be consolidated if implementation proves a boundary adds ceremony without isolation value, but native hooks remain separate binaries.

---

## 23. Testing strategy

### Unit tests

Required for:

- template parser/evaluator;
- rule engine;
- grouping identity;
- profile serialization/migration;
- provider dependency resolution;
- IPC serialization/validation;
- tray state store;
- taskbar state reducer.

### Windows integration tests

On Windows GitHub Actions runners where practical:

- fake windows and WinEvent tracking;
- activate/restore behavior;
- icon conversion;
- named-pipe reconnection;
- provider cancellation;
- architecture/package build validation.

### Manual hardware/desktop verification

Release checklist includes:

- multi-monitor;
- Explorer restart;
- light/dark theme;
- PowerToys Dock compact/default;
- auto-hide;
- grouped/separate/smart profiles;
- multiple VS Code windows;
- multiple browser windows;
- dynamic title changes;
- tray icon add/modify/delete;
- animated tray icon;
- volume/network/battery changes;
- missing optional hooks;
- native taskbar progress capture on explicitly enabled test apps.

Manual release verification is documented; binaries themselves still originate from GitHub Actions.

---

## 24. CI and release design

### `ci.yml`

Triggers:

- pull requests;
- pushes to `main`.

Jobs:

- restore;
- format/static checks;
- build managed projects;
- build native architecture matrix;
- unit tests;
- protocol tests;
- packaging smoke test.

### `release.yml`

Trigger:

- version tag `v*`.

Responsibilities:

1. checkout exact tag;
2. restore locked dependencies;
3. build Release artifacts;
4. build required architecture-specific native binaries;
5. run tests;
6. package the Command Palette extension;
7. create checksums;
8. publish GitHub Release;
9. attach installable package(s), symbols where useful, checksums and any certificate material required by the chosen MSIX signing strategy.

No locally built release binary is accepted as the canonical release.

---

## 25. README contract

`README.md` is not a developer stub. It is the primary end-user manual.

It must contain exact instructions for:

1. prerequisites and supported PowerToys/Command Palette version;
2. downloading a release;
3. installing/trusting the package when necessary;
4. confirming the extension is loaded;
5. adding CmdPal Dock Plus bands/items to the Dock;
6. creating a first app shortcut;
7. choosing grouped vs separate vs smart windows;
8. selecting live fields;
9. constructing title/subtitle templates;
10. writing grouping/display rules;
11. configuring primary/context actions;
12. enabling app-specific adapters;
13. enabling third-party tray mirroring;
14. configuring volume/network/battery controls;
15. enabling optional native taskbar progress/overlay capture per app;
16. installing the optional PowerToys hover-preview compatibility build/patch if required;
17. upgrading;
18. uninstalling;
19. resetting configuration;
20. diagnostics/troubleshooting;
21. privacy/security implications of optional process/Explorer hooks.

Every setting visible in the extension must either be documented directly or link to a dedicated section in the repository documentation.

---

## 26. Security model

The default installation does **not** inject into arbitrary processes.

Optional native features use explicit opt-in.

### SysTray

Explorer injection is required for the robust tray path and is clearly disclosed.

### Taskbar capture

Enabled per App Profile only.

The capture hook:

- forwards taskbar metadata only;
- does not expose a generic remote execution mechanism;
- validates the receiving pipe identity/session;
- avoids collecting unrelated process memory;
- unloads cleanly where possible.

### Templates

No arbitrary scripting.

### Logs

Do not log window titles, media titles, paths or other user content at verbose level by default.

---

## 27. Compatibility strategy

The main extension targets the public Command Palette extension contract.

Features requiring host internals are separated by compatibility version.

The PowerToys hover-preview patch is maintained as a small patch set against known PowerToys versions. CI must fail clearly when it no longer applies.

Native tray/taskbar hooks are treated as optional compatibility layers and are never allowed to block startup of the main extension.

---

## 28. Delivery slices

Implementation is split into independently working slices.

### Slice 1 — Smart Dock foundation

- extension/package;
- profile persistence;
- application/window discovery;
- grouped/separate tiles;
- launch/focus/minimize/restore/close;
- dynamic title/subtitle;
- template engine;
- settings;
- README installation and basic setup.

### Slice 2 — Smart grouping and providers

- rule engine;
- provider capability discovery;
- WindowProvider;
- ProcessProvider;
- app-specific adapter framework;
- initial VS Code/browser/terminal adapters.

### Slice 3 — Rich window UX

- recent/frequent destinations;
- richer context menus;
- DWM preview service;
- PowerToys hover compatibility patch/build.

### Slice 4 — System area

- volume/network/battery providers;
- third-party tray bridge/hook;
- event-driven icon/state updates.

### Slice 5 — Native taskbar-state capture

- per-profile opt-in capture controller;
- taskbar hook/wrapper;
- progress;
- overlay;
- dynamic Dock representation;
- architecture and failure-isolation tests.

### Slice 6 — Release hardening

- full CI matrix;
- tag-driven release;
- release artifacts;
- checksums/signing flow;
- end-user README completion;
- upgrade/uninstall/troubleshooting docs.

Every slice must leave the repository in a buildable/testable state.

---

## 29. Acceptance criteria

The project is considered functionally complete for the initial release when:

1. a user can install the GitHub Actions-produced release without compiling source;
2. the extension appears in Command Palette;
3. a user can create an application tile from the extension settings;
4. grouped and separate window modes work;
5. Smart rules can group/separate/hide windows;
6. title and subtitle can use discovered dynamic fields;
7. changing window/app data updates the Dock without restarting the extension;
8. VS Code demonstrates at least one useful app-specific field;
9. generic window context actions work;
10. app menus expose recent/frequent data where supported;
11. system controls are event-driven;
12. third-party tray mirroring works when its optional component is enabled;
13. tray activity does not continuously write icons to disk;
14. progress/overlay capture is explicitly opt-in and isolated;
15. the basic extension works when every optional native component is disabled or broken;
16. hover previews work with the documented compatible PowerToys host component;
17. CI builds/tests on every PR;
18. a version tag creates a GitHub Release with installable artifacts;
19. README documents every installation/setup/configuration path required by the project.

---

## 30. Decisions locked by this design

- Public repository name: **`cmdpal-dock-plus`**
- This is a **Command Palette Dock enhancement**, not a standalone replacement taskbar.
- Existing native taskbar pins are not imported.
- Dock tiles are profile-driven and provider-backed.
- Dynamic text is a first-class feature.
- Grouped/separate/smart per-window behavior is a first-class feature.
- App-specific adapters are supported.
- Templates are declarative, not executable scripts.
- Performance-sensitive providers are activated only when their fields are requested.
- System tray and native taskbar capture are optional native components.
- Explorer shell-owned system controls are implemented using supported APIs rather than shell scraping.
- Window hover previews use a minimal PowerToys compatibility patch rather than a janky global-mouse workaround.
- Releases are produced by GitHub Actions and distributed through GitHub Releases.
- README is the canonical installation and configuration guide.
