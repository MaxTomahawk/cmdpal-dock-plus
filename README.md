# CmdPal Dock Plus

CmdPal Dock Plus extends the PowerToys Command Palette Dock into a configurable app/window surface instead of trying to reproduce every private Explorer taskbar implementation detail.

It provides:

- Smart app/window tiles that launch, focus, group, split, hide, and manage top-level windows.
- Dynamic icon/title/subtitle templates driven by live app/window/process/provider data.
- Grouped, Separate, and Smart per-window modes.
- App-specific adapters for VS Code, browsers, Windows Terminal, Explorer, and Windows media sessions.
- Smart rules and custom actions configured per shortcut/profile.
- A Smart App Menu with windows, Recent/Frequent destinations, media actions, app actions, and user actions.
- A System band for volume, network, and battery/power.
- A safe Windows 11 notification-area band using UI Automation without injecting code into Explorer.
- Optional live DWM hover previews through a small version-pinned PowerToys compatibility patch.

## Status

The `v0.1.x` line targets Windows 11 and PowerToys **0.101.0 or newer**.

`v0.1.1` changes the default unsigned-package installer to `Install-Unsigned.cmd` so installation still works on machines where Group Policy requires PowerShell script files to be signed. The actual package-install command is unchanged: `Add-AppxPackage -AllowUnsigned`.

The core extension does **not** require a custom PowerToys build. The optional PowerToys patch is required only for automatic hover thumbnails because the public Command Palette extension SDK does not expose Dock pointer/hover events to extensions.

This release line intentionally does **not** ship generic third-party process injection for native `ITaskbarList3` progress/overlay interception.

## Requirements

- Windows 11.
- PowerToys 0.101.0 or newer with Command Palette/Dock available.
- x64 or ARM64 Windows.

For the optional hover-preview patch, you also need a PowerToys build environment matching the pinned source revision in `powertoys/patches/upstream-commit.txt`.

## Install a GitHub release

A release contains:

- `CmdPalDockPlus-<version>.msixbundle` — combined x64 + ARM64 extension bundle.
- `Install-Unsigned.cmd` — **recommended installer** for the unsigned GitHub package.
- `Install-Unsigned.ps1` — compatibility fallback for machines that allow local unsigned PowerShell scripts.
- Individual x64 and ARM64 `.msix` files.
- `CmdPalDockPlus-PowerToysPatch-<version>.zip` — optional hover-preview compatibility patch.
- `SHA256SUMS.txt`.

### Recommended installation

Download these two files into the same folder:

```text
CmdPalDockPlus-<version>.msixbundle
Install-Unsigned.cmd
```

Then double-click `Install-Unsigned.cmd`, or run it from a terminal:

```cmd
Install-Unsigned.cmd
```

The installer:

1. finds exactly one `CmdPalDockPlus-*.msixbundle` next to itself;
2. requests administrator elevation if necessary;
3. executes an inline Windows PowerShell command;
4. installs the bundle with:

```powershell
Add-AppxPackage -Path '<bundle>' -AllowUnsigned
```

The CMD bootstrapper deliberately does **not** execute an unsigned `.ps1` file, so a Group Policy that enforces signed PowerShell script files does not block this installation path.

You can also provide an explicit package path:

```cmd
Install-Unsigned.cmd "C:\path\to\CmdPalDockPlus-0.1.1.msixbundle"
```

### Direct installation without either installer file

Open an **Administrator** Windows PowerShell window in the folder containing the bundle and run:

```powershell
Add-AppxPackage -Path .\CmdPalDockPlus-0.1.1.msixbundle -AllowUnsigned
```

Or from PowerShell 7, request an elevated Windows PowerShell process without executing a script file:

```powershell
$pkg = (Resolve-Path .\CmdPalDockPlus-0.1.1.msixbundle).Path
Start-Process powershell.exe -Verb RunAs -Wait -ArgumentList "-NoProfile -Command `"Add-AppxPackage -Path '$pkg' -AllowUnsigned`""
```

### Why `Install-Unsigned.ps1` may be blocked

PowerShell execution-policy precedence is:

```text
MachinePolicy
UserPolicy
Process
LocalMachine / CurrentUser
```

A command such as:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\Install-Unsigned.ps1
```

sets the Process policy only. If Group Policy sets `MachinePolicy` or `UserPolicy` to require signed scripts, that higher-precedence policy still wins. `Install-Unsigned.cmd` avoids this issue by using an inline PowerShell command instead of loading an unsigned script file.

To inspect your machine:

```powershell
Get-ExecutionPolicy -List
```

### Unsigned development channel

The GitHub v0.1 release line uses Windows' unsigned MSIX development-install path. For a long-lived public distribution channel, a properly signed MSIX should replace this development route.

## First setup

After installation:

1. Start or restart PowerToys.
2. Open **Command Palette**.
3. Verify **CmdPal Dock Plus** is enabled as an extension.
4. Run **Configure CmdPal Dock Plus**.
5. Choose **Add from running application** for the easiest setup, or **Add manually**.
6. Give the shortcut/profile a stable ID and display name.
7. Match the app by executable path, AUMID, or both.
8. Choose **Grouped**, **Separate**, or **Smart** window behavior.
9. Configure Title, Subtitle, and optional Icon templates.
10. Configure Smart rules and custom actions if wanted.
11. Save the profile.
12. Use normal Command Palette Dock customization to place the provided bands where you want them.

CmdPal Dock Plus exposes three Dock bands:

- **Smart applications**
- **System status**
- **Notification area**

Dock placement, compact/default size, always-on-top, and auto-hide remain native PowerToys Dock settings.

Profiles are stored at:

```text
%LOCALAPPDATA%\CmdPalDockPlus\profiles.json
```

Deleting this file resets configured app profiles. Restart the extension after manually editing or deleting it.

## Shortcut / app-profile behavior

Every configured shortcut is an **App Profile**. A profile determines:

- how the application is identified;
- which windows belong to it;
- whether windows are grouped or split;
- which live fields are requested;
- how Title, Subtitle, and Icon are rendered;
- which rules override grouping/presentation;
- which actions appear in the app/window menu.

Primary click behavior:

- no matching window → launch the configured app;
- one or more matching windows → focus the current primary/MRU window.

The context menu can expose:

- Smart App Menu;
- Focus a specific window;
- Minimize / maximize / close a specific window;
- New instance;
- Open executable location;
- Close all windows;
- live provider actions such as media Play/Pause/Previous/Next;
- user-defined actions.

## Window matching

A profile can match by:

- executable name/path;
- Windows Application User Model ID (AUMID);
- or both.

The Windows backend reads the process image path and, when Windows exposes it, the process AUMID. Matching is case-insensitive. If an AUMID is configured, it takes precedence for packaged-host scenarios where several apps can share a host executable.

AUMID is also used by the public Windows Recent/Frequent destination APIs in the Smart App Menu.

## Grouping modes

### Grouped

All matching windows become one tile. The primary/MRU window is focused by default; every represented window remains available in the menu.

### Separate

Each eligible top-level window becomes its own Dock tile and can show its own title, subtitle, icon, provider state, and actions.

### Smart

Rules can dynamically group, separate, hide, or restyle windows based on provider fields.

Typical examples:

- one VS Code tile per workspace;
- separate browser InPrivate windows;
- group browser windows by profile;
- separate Explorer project folders but group Downloads/Desktop;
- hide tool/dialog windows.

## Dynamic Title / Subtitle / Icon templates

Fields are referenced with braces:

```text
{window.title}
{vscode.workspace}
{browser.pageTitle}
{process.cpu}
```

Fallback chains use `??`:

```text
{vscode.workspace ?? window.title}
{media.title ?? window.title ?? app.name}
```

The setup page probes the selected/running application and shows fields that providers can expose.

Generic fields include:

| Field | Update model | Meaning |
|---|---|---|
| `app.name` | profile | Configured display name |
| `process.executable` | snapshot | Executable path/name |
| `process.aumid` | snapshot | Process AUMID when available |
| `process.pid` | event-driven | Process ID |
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

Application adapters add fields such as:

- VS Code: `vscode.workspace`, `vscode.file`, `vscode.remote`
- Browsers: `browser.pageTitle`, `browser.isPrivate`, `browser.product`
- Windows Terminal: `terminal.title`, `terminal.shell`
- Explorer: `explorer.locationName`
- Media: `media.title`, `media.artist`, `media.album`, `media.playbackState`, `media.sourceApp`

### Icon selection

If no icon override is configured, the tile uses the matched executable path as its icon source. Command Palette extracts the application icon from the executable. A Smart rule may override the icon with a rendered icon template.

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
group      -> requires key; key may be a template
separate
hide
title      -> requires template
subtitle   -> requires template
icon       -> requires template
```

Rules are evaluated in order. Inside a matching rule, `group`, `separate`, and `hide` are terminal grouping decisions, so put presentation overrides before the terminal grouping action when both are needed.

Rule templates and regex expressions are validated when the profile is saved. Regex execution has a timeout.

## User actions JSON

Custom actions are configured as an ordered JSON array.

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

- `process` — starts the target directly without shell execution;
- `uri` — opens an absolute URI through Windows shell handling;
- `shell` — explicit advanced shell-execute action.

`shell` is intentionally opt-in. Treat imported profile files as executable configuration; do not import actions from untrusted sources.

## Smart App Menu and Jump List replacement

CmdPal Dock Plus does not attempt to clone every private native Jump List category. The Smart App Menu combines supported/reliable sources:

- current windows;
- Windows Recent destinations when an AUMID is available;
- Windows Frequent destinations when an AUMID is available;
- New instance;
- Open file location;
- Close all windows;
- live provider actions;
- custom actions.

Native pinned/custom Jump List categories are not imported.

## Media integration

When Windows exposes exactly one matching Global System Media Transport Controls session for the tile's app, the tile can expose:

- Play / Pause;
- Previous;
- Next.

Media metadata is event-driven rather than polled.

## System status band

### Volume

- Live volume percentage and mute state.
- Primary click toggles mute.
- Context actions: -5%, +5%, Sound settings.
- Uses Core Audio endpoint notifications rather than polling.

If Core Audio initialization fails, only the volume item is omitted.

### Network

- Live connectivity/profile state.
- Click opens Windows Network settings.
- Uses Windows network-status notifications.

### Battery / power

- Live battery percentage and charging/power state.
- Click opens Power & battery settings.
- Uses Windows power-status events.

## Notification area / system tray

The safe v0.1 implementation uses Windows UI Automation on Windows 11 and deliberately does **not** inject a DLL into `explorer.exe`.

Behavior:

- visible third-party notification icons are enumerated from the Windows taskbar accessibility tree;
- default activation uses UIA `InvokePattern` when available;
- **Hidden icons…** invokes Windows' own overflow control;
- while Windows' overflow panel is open, its third-party icons can be reflected into the Dock band;
- shell-owned combined volume/network/battery indicators are excluded because CmdPal Dock Plus provides them through the System band.

Performance model:

- UIA structure/property events trigger refreshes;
- events are debounced;
- a slow watchdog scan exists only for Explorer/UIA recovery;
- there is no permanent multi-second polling loop for normal updates.

Safe-build limitations:

- UIA does not reliably expose original notification icon pixels, so v0.1 uses a generic tray glyph instead of scraping Explorer internals;
- no synthetic cursor movement;
- no right/middle-click emulation;
- hidden/overflow icons are discoverable while Windows' overflow flyout is open.

## Live hover previews

The repository includes an optional PowerToys patch:

```text
powertoys/patches/cmdpal-dock-hover.patch
```

The matching PowerToys revision is pinned in:

```text
powertoys/patches/upstream-commit.txt
```

The patch only bridges Dock hover enter/exit events and stable command IDs to CmdPal Dock Plus. The extension owns the preview window and uses DWM thumbnails (`DwmRegisterThumbnail` / `DwmUpdateThumbnailProperties`) for live content.

CI and release workflows validate the patch with `git apply --check` against the pinned upstream revision.

See [`powertoys/README.md`](powertoys/README.md) for apply/build instructions.

Without the patch, all normal app/window tiles, context menus, status controls, tray UIA behavior, templates, rules, and actions continue working; only automatic hover previews are absent.

## Native taskbar progress, overlay badges, and attention flashing

The state/protocol model exists for future capture adapters, but the v0.1 release line does **not** ship generic taskbar API interception.

Reason:

- `ITaskbarList3` exposes setters, not a supported global observer API;
- exact generic interception requires code in the target process or an unsupported Shell-side technique;
- generic injection adds architecture, security-product, anti-cheat, sandbox, and crash-surface costs that are not acceptable for the default release.

Current release position:

- native app progress interception: **not shipped**;
- native `SetOverlayIcon` interception: **not shipped**;
- exact Explorer attention-flash mirroring: **not shipped**;
- app/provider-defined titles, state, actions, and future custom badges: supported by the normal extension architecture.

## Build from source

Use Windows with the .NET 10 SDK and Windows/MSIX build tooling.

Run tests:

```powershell
dotnet test tests/CmdPalDockPlus.Core.Tests/CmdPalDockPlus.Core.Tests.csproj -c Release
dotnet test tests/CmdPalDockPlus.Windows.Tests/CmdPalDockPlus.Windows.Tests.csproj -c Release
dotnet test tests/CmdPalDockPlus.Providers.Tests/CmdPalDockPlus.Providers.Tests.csproj -c Release
```

Validate the installer bootstrap without installing anything:

```cmd
scripts\Install-Unsigned.cmd --help
```

Build x64:

```powershell
dotnet restore src/CmdPalDockPlus.Extension/CmdPalDockPlus.Extension.csproj -p:Platform=x64
dotnet build src/CmdPalDockPlus.Extension/CmdPalDockPlus.Extension.csproj -c Release -p:Platform=x64 --no-restore
```

Build ARM64 by replacing `x64` with `ARM64`.

## Release process

Tags matching `vX.Y.Z` trigger `.github/workflows/release.yml`.

The workflow:

1. validates/parses the tag;
2. sets the MSIX version to `X.Y.Z.0`;
3. validates `Install-Unsigned.cmd --help`;
4. runs all managed tests;
5. validates the optional PowerToys patch against the pinned upstream revision;
6. builds unsigned x64 and ARM64 MSIX packages;
7. creates the `.msixbundle`;
8. includes both installer bootstraps and the PowerToys patch package;
9. generates and verifies SHA-256 checksums;
10. creates the GitHub Release.

No signing secret is required for this unsigned development channel.

## Security / reliability choices

- No DLL injection into Explorer for tray capture.
- No generic injection into third-party apps for taskbar progress/overlay capture.
- No synthetic cursor movement for tray context menus.
- Regex execution has a timeout.
- Custom actions are explicit profile configuration and validated before execution.
- System status components fail independently where possible.
- The optional PowerToys hover patch modifies PowerToys source but does not inject into arbitrary applications.

## License

MIT. See [`LICENSE`](LICENSE).
