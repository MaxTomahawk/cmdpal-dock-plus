# CmdPal Dock Plus

CmdPal Dock Plus turns the PowerToys Command Palette Dock into a smarter app/window surface instead of trying to clone every private Explorer taskbar behavior.

It provides:

- Smart application tiles that launch, focus, group, split, hide, and manage real top-level windows.
- Live title/subtitle templates with app-specific data for VS Code, browsers, terminals, Explorer, media sessions, and optional process metrics.
- A Smart App Menu with windows, Recent/Frequent destinations, app actions, media actions, and user-defined actions.
- A separate System status band for volume, network, and battery/power.
- A safe Windows 11 notification-area band based on UI Automation, without injecting code into Explorer.
- Optional live DWM hover previews through a small, version-pinned PowerToys compatibility patch.

## Status

`v0.1.0` targets Windows 11 and PowerToys **0.101.0 or newer**.

The core extension does **not** require a custom PowerToys build. The optional PowerToys patch is only required for live hover thumbnails because the public Command Palette extension SDK does not expose Dock pointer/hover events to extensions.

This release intentionally does **not** ship generic process injection for native `ITaskbarList3` progress/overlay interception.

## Requirements

- Windows 11.
- PowerToys 0.101.0 or newer with Command Palette/Dock available.
- x64 or ARM64 Windows.

For the optional hover-preview patch you also need a local PowerToys build environment matching the pinned upstream source revision in `powertoys/patches/upstream-commit.txt`.

## Install a GitHub release

A release contains:

- `CmdPalDockPlus-<version>.msixbundle` — x64 + ARM64 extension bundle.
- `Install-Unsigned.ps1` — installer for the unsigned GitHub development package.
- Individual x64 and ARM64 `.msix` files.
- `CmdPalDockPlus-PowerToysPatch-<version>.zip` — optional hover-preview compatibility patch.
- `SHA256SUMS.txt`.

The GitHub v0.1 channel uses Windows' unsigned MSIX development-install path. Put the `.msixbundle` and `Install-Unsigned.ps1` in the same directory, then run:

```powershell
PowerShell -ExecutionPolicy Bypass -File .\Install-Unsigned.ps1
```

The script requests elevation and installs the bundle with `Add-AppxPackage -AllowUnsigned`.

For a long-lived/public distribution channel, use a properly signed MSIX instead of the unsigned development package.

## First setup

After installation:

1. Make sure **CmdPal Dock Plus** is enabled as a Command Palette extension.
2. Open the Command Palette command **Configure CmdPal Dock Plus**.
3. Choose **Add from running application** for the easiest setup, or **Add manually**.
4. Give the profile a stable ID and display name.
5. Match by executable path, AUMID, or both.
6. Choose a grouping mode and title/subtitle templates.
7. Save the tile.

CmdPal Dock Plus exposes three Dock bands:

- **Smart applications**
- **System status**
- **Notification area**

Use the normal Command Palette Dock customization/settings UI to arrange the bands. Dock placement, compact size, always-on-top, and auto-hide remain native PowerToys Dock settings.

Profiles are stored at:

```text
%LOCALAPPDATA%\CmdPalDockPlus\profiles.json
```

Deleting that file resets the app-profile configuration. Close/restart the extension after a manual file edit.

## Smart application tile behavior

Primary click follows a taskbar-like but deliberately simplified rule:

- No matching window: launch the configured app.
- One or more matching windows: focus the tile's current primary/MRU window.

The context menu exposes, when applicable:

- Smart App Menu
- Focus a specific window
- Minimize / maximize / close a specific window
- New instance
- Open executable location
- Close all windows
- Live provider actions, such as media Play/Pause/Previous/Next
- User-defined actions

### Window matching

A profile can match either:

- executable name/path, or
- Windows Application User Model ID (AUMID).

The Windows backend reads both the process image path and, when Windows exposes it, the process AUMID. Matching is case-insensitive.

AUMID is also useful for the public Windows Recent/Frequent destination APIs used by the Smart App Menu.

## Grouping modes

### Grouped

All matching windows become one tile. The primary/MRU window is focused by default; all windows remain available in the menu.

### Separate

Each window becomes its own tile.

### Smart

Rules inspect live fields and can group, separate, hide, or override presentation.

## Title and subtitle templates

Fields are referenced with braces:

```text
{window.title}
{vscode.workspace}
{browser.pageTitle}
{process.cpu}
```

Fallbacks use `??`:

```text
{vscode.workspace ?? window.title}
{media.title ?? window.title ?? app.name}
```

The editor shows the fields available for the selected/running application.

Generic fields include:

| Field | Update model | Meaning |
|---|---|---|
| `app.name` | profile | Configured display name |
| `process.executable` | snapshot | Executable path/name |
| `process.aumid` | snapshot | Process AUMID when available |
| `process.pid` | event-driven | Process id |
| `process.cpu` | sampled/2s | CPU percentage; sampling starts only when referenced |
| `process.memory` | sampled/2s | Working set; sampling starts only when referenced |
| `process.uptime` | sampled/2s | Process uptime; sampling starts only when referenced |
| `window.title` | event-driven | Window title |
| `window.state` | event-driven | Restored/minimized/maximized |
| `window.isActive` | event-driven | Foreground state |
| `window.isMinimized` | event-driven | Minimized state |
| `window.monitor` | event-driven | Monitor device name |
| `window.class` | snapshot/event | Win32 window class |
| `window.count` | event-driven | Number of windows represented by a tile |

Application-specific adapters add fields such as:

- VS Code: `vscode.workspace`, `vscode.file`, `vscode.remote`
- Browsers: `browser.pageTitle`, `browser.isPrivate`, `browser.product`
- Windows Terminal: `terminal.title`, `terminal.shell`
- Explorer: `explorer.locationName`
- Media sessions: `media.title`, `media.artist`, `media.album`, `media.playbackState`, `media.sourceApp`

## Smart rules JSON

The profile editor accepts an ordered JSON array.

Example:

```json
[
  {
    "id": "private-browser-window",
    "when": [
      { "field": "browser.isPrivate", "op": "true" }
    ],
    "then": [
      { "action": "title", "template": "Private · {browser.pageTitle}" },
      { "action": "separate" }
    ]
  },
  {
    "id": "group-vscode-workspace",
    "when": [
      { "field": "vscode.workspace", "op": "exists" }
    ],
    "then": [
      { "action": "subtitle", "template": "{vscode.workspace}" },
      { "action": "group", "key": "{vscode.workspace}" }
    ]
  }
]
```

Supported condition operators:

```text
equals
notEquals
contains
notContains
startsWith
endsWith
regex
exists
missing
greaterThan
lessThan
true
false
```

Supported actions:

```text
group      -> requires key
separate
hide
title      -> requires template
subtitle   -> requires template
icon       -> requires template
```

Rules are evaluated in order. Inside a matching rule, `group`, `separate`, and `hide` are terminal grouping decisions, so put title/subtitle/icon overrides **before** the terminal grouping action when both are required.

Regex rules use a short timeout and invalid regex input is rejected when the profile is saved.

## User actions JSON

Custom actions are also configured as a JSON array.

Process example:

```json
[
  {
    "id": "terminal-here",
    "name": "Terminal here",
    "kind": "process",
    "target": "wt.exe",
    "arguments": "-d C:\\src\\my-project",
    "workingDirectory": "C:\\src\\my-project"
  }
]
```

URI example:

```json
[
  {
    "id": "project-board",
    "name": "Open project board",
    "kind": "uri",
    "target": "https://example.com/project",
    "arguments": null,
    "workingDirectory": null
  }
]
```

Supported kinds:

- `process` — starts the target directly without shell execution.
- `uri` — opens an absolute URI through Windows shell handling.
- `shell` — explicit advanced shell-execute action.

`shell` is intentionally opt-in. Treat profile files as executable configuration: do not import actions from untrusted sources.

## Smart App Menu and Jump List replacement

CmdPal Dock Plus does not attempt to clone every private native Jump List category. Instead, the Smart App Menu combines public/reliable sources:

- Current windows
- Windows Recent destinations when an AUMID is available
- Windows Frequent destinations when an AUMID is available
- New instance
- Open file location
- Close all windows
- Live provider actions
- Custom actions

This gives a larger, app-aware menu while avoiding dependence on private pinned/custom Jump List internals.

## Media integration

If Windows exposes exactly one matching Global System Media Transport Controls session for the tile's app, the tile can expose:

- Play / Pause
- Previous
- Next

Media metadata is event-driven; it does not require a polling loop.

## System status band

The System band contains supported Windows status controls:

### Volume

- Live volume percentage and mute state.
- Primary click toggles mute.
- Context actions: volume -5%, volume +5%, Sound settings.
- Uses Core Audio endpoint-change notifications rather than polling.

If Core Audio initialization fails, the volume item is omitted without taking down the extension.

### Network

- Live connectivity/profile state.
- Click opens Windows Network settings.
- Uses Windows network-status notifications.

### Battery / power

- Live battery percentage and charging/power state.
- Click opens Power & battery settings.
- Uses Windows power-status events.

## Notification area / system tray

The safe v0.1 implementation uses Windows UI Automation on Windows 11. It deliberately does **not** inject a DLL into `explorer.exe`.

Behavior:

- Visible third-party notification icons are enumerated from the Windows taskbar accessibility tree.
- Default activation uses UIA `InvokePattern` when the icon supports it.
- **Hidden icons…** invokes Windows' own overflow chevron.
- While the Windows overflow panel is open, its third-party icons can be reflected into the Dock band.
- Shell-owned combined indicators such as volume/network/battery are excluded because CmdPal Dock Plus provides those as supported System-band controls instead.

Performance model:

- UIA structure/property events trigger refreshes.
- Events are debounced instead of causing a full rebuild per individual accessibility event.
- A slow recovery/watchdog scan is retained for Explorer/UIA recovery.
- There is no permanent multi-second polling loop for normal updates.

Current safe-build limitations:

- UIA does not expose the original notification icon bitmap reliably, so v0.1 uses a generic tray glyph rather than scraping Explorer internals.
- No synthetic cursor movement is used.
- Right/middle-click emulation is therefore not provided in the safe build.
- Hidden/overflow icons are discoverable while Windows' overflow flyout is open; Windows does not publicly expose the complete hidden-icon collection through the same non-disruptive UIA path while it is closed.

## Live hover previews

The repository includes an optional PowerToys patch:

```text
powertoys/patches/cmdpal-dock-hover.patch
```

The matching upstream PowerToys commit is pinned in:

```text
powertoys/patches/upstream-commit.txt
```

The patch only bridges internal Dock hover enter/exit events to CmdPal Dock Plus. The extension owns the preview window and uses DWM thumbnails (`DwmRegisterThumbnail` / `DwmUpdateThumbnailProperties`) for live window content.

The CI and release workflows validate the patch with `git apply --check` against the pinned PowerToys commit.

See [`powertoys/README.md`](powertoys/README.md) for build/apply instructions.

Without the patch, all normal app/window tiles, context menus, status controls, tray UIA behavior, templates, rules, and actions still work; only automatic hover previews are absent.

## Native taskbar progress, overlay badges, and attention flashing

The core state/protocol model exists for future capture adapters, but v0.1 does not ship generic taskbar API interception.

Why:

- `ITaskbarList3` exposes setters such as progress and overlay operations, not a supported global observer API.
- Exact interception requires code inside the calling process or an unsupported Shell-side reverse-engineered path.
- Shipping generic per-process injection creates architecture, security-product, anti-cheat, sandbox, and crash-surface costs that are disproportionate for the base Dock extension.

So the v0.1 release position is:

- Native app progress interception: **not shipped**.
- Native `SetOverlayIcon` interception: **not shipped**.
- Exact Explorer attention-flash mirroring: **not shipped**.
- App/provider-defined titles, state, actions and future custom badges: supported by the normal extension architecture.

## Architecture

```text
CmdPalDockPlus.Extension
    |
    +-- DockCoordinator
    |     +-- WindowTracker / WindowActivator / AppLauncher
    |     +-- TileComposer
    |     +-- ProviderHost
    |
    +-- Smart applications band
    +-- System status band
    +-- Notification area band (UIA)
    +-- Configuration pages
    +-- HoverPreviewCoordinator (optional PowerToys hover bridge)

CmdPalDockPlus.Core
    +-- profiles
    +-- templates
    +-- rules
    +-- tile composition
    +-- taskbar-state protocol/reducer

CmdPalDockPlus.Windows
    +-- Win32 window enumeration/events
    +-- executable + AUMID identity
    +-- focus/minimize/maximize/close
    +-- Recent/Frequent destinations
    +-- DWM thumbnails
    +-- media session service
    +-- Core Audio / network / power status

CmdPalDockPlus.Providers
    +-- VS Code
    +-- browser
    +-- terminal
    +-- Explorer
    +-- media
    +-- opt-in process metrics
```

The normal runtime is event-driven. The only recurring sampling loop is process metrics, and it starts only when a profile actually references CPU/memory/uptime fields. CPU metrics use a 2-second sample cadence.

## Build from source

Use Windows with the .NET 10 SDK and Visual Studio/Build Tools support required by the Windows/MSIX project.

Run the managed test suites:

```powershell
dotnet test tests/CmdPalDockPlus.Core.Tests/CmdPalDockPlus.Core.Tests.csproj -c Release
dotnet test tests/CmdPalDockPlus.Windows.Tests/CmdPalDockPlus.Windows.Tests.csproj -c Release
dotnet test tests/CmdPalDockPlus.Providers.Tests/CmdPalDockPlus.Providers.Tests.csproj -c Release
```

Build x64:

```powershell
dotnet restore src/CmdPalDockPlus.Extension/CmdPalDockPlus.Extension.csproj -p:Platform=x64
dotnet build src/CmdPalDockPlus.Extension/CmdPalDockPlus.Extension.csproj -c Release -p:Platform=x64 --no-restore
```

Build ARM64 by replacing `x64` with `ARM64`.

The CI workflow additionally builds unsigned MSIX smoke packages for both architectures and validates the pinned PowerToys patch.

## Release process

Tags matching `vX.Y.Z` trigger `.github/workflows/release.yml`.

The workflow:

1. validates/parses the tag;
2. sets the MSIX version to `X.Y.Z.0`;
3. runs all managed tests;
4. validates the optional PowerToys patch against the pinned upstream revision;
5. builds unsigned x64 and ARM64 MSIX packages;
6. creates the `.msixbundle`;
7. packages the PowerToys patch docs;
8. generates and verifies SHA-256 checksums;
9. creates the GitHub Release.

No signing secret is required for the unsigned development release channel.

## Security / reliability choices

CmdPal Dock Plus deliberately keeps invasive mechanisms out of the default release:

- No DLL injection into Explorer for tray capture.
- No generic injection into third-party applications for taskbar progress/overlay capture.
- No synthetic cursor movement for notification-area context menus.
- Regex execution has a timeout.
- Custom actions are explicit profile configuration and are validated before execution.
- System status components fail independently where possible.

The optional PowerToys hover patch modifies PowerToys source, but it does not inject into arbitrary application processes.

## License

MIT. See [`LICENSE`](LICENSE).
