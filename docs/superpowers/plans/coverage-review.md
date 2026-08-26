# Implementation Plan Coverage Review

**Spec reviewed:** `docs/superpowers/specs/2026-08-26-cmdpal-dock-plus-design.md`  
**Review date:** 2026-08-26

This is the Superpowers writing-plans self-review: spec coverage, placeholder scan, and interface/type consistency across plans.

## Coverage matrix

| Approved design area | Implementation plan |
| --- | --- |
| CmdPal extension/package baseline | `2026-08-26-smart-dock-foundation.md` |
| Profile persistence/versioning | `2026-08-26-smart-dock-foundation.md` |
| Grouped windows | `2026-08-26-smart-dock-foundation.md` |
| Separate per-window icons/tiles | `2026-08-26-smart-dock-foundation.md` |
| Dynamic Title/Subtitle | `2026-08-26-smart-dock-foundation.md` |
| Safe template language | `2026-08-26-smart-dock-foundation.md` |
| Generic launch/focus/minimize/maximize/close | `2026-08-26-smart-dock-foundation.md` |
| Provider contracts/capability discovery | `2026-08-26-providers-and-smart-grouping.md` |
| Dependency-aware provider activation | `2026-08-26-providers-and-smart-grouping.md` |
| Window/Process provider | `2026-08-26-providers-and-smart-grouping.md` |
| Smart grouping/rules | `2026-08-26-providers-and-smart-grouping.md` |
| VS Code adapter | `2026-08-26-providers-and-smart-grouping.md` |
| Browser adapter/actions | `2026-08-26-providers-and-smart-grouping.md` |
| Windows Terminal adapter/actions | `2026-08-26-providers-and-smart-grouping.md` |
| Generic Media provider | `2026-08-26-app-data-attention-and-user-actions.md` |
| Explorer adapter/path rules | `2026-08-26-app-data-attention-and-user-actions.md` |
| Provider-level attention state | `2026-08-26-app-data-attention-and-user-actions.md` |
| User-defined safe actions | `2026-08-26-app-data-attention-and-user-actions.md` |
| Recent/Frequent destinations | `2026-08-26-rich-window-ux.md` |
| Smart App Menu | `2026-08-26-rich-window-ux.md` |
| Live DWM window previews | `2026-08-26-rich-window-ux.md` |
| Minimal CmdPal hover bridge/PowerToys patch | `2026-08-26-rich-window-ux.md` |
| Third-party tray protocol/state | `2026-08-26-system-area.md` |
| Minimal Explorer tray hook | `2026-08-26-system-area.md` |
| Event-driven tray bridge/recovery | `2026-08-26-system-area.md` |
| In-memory tray icon projection | `2026-08-26-system-area.md` |
| UIA fallback without 3-second polling | `2026-08-26-system-area.md` |
| Volume control/status | `2026-08-26-system-area.md` |
| Network status | `2026-08-26-system-area.md` |
| Battery/power status | `2026-08-26-system-area.md` |
| Optional native taskbar progress capture | `2026-08-26-native-taskbar-capture.md` |
| Optional native overlay capture | `2026-08-26-native-taskbar-capture.md` |
| Per-profile opt-in injection | `2026-08-26-native-taskbar-capture.md` |
| x86/x64/ARM64 capture architecture | `2026-08-26-native-taskbar-capture.md` |
| Native failure/security diagnostics | `2026-08-26-native-taskbar-capture.md` |
| PR/main CI | `2026-08-26-release-hardening.md` |
| x64/ARM64 extension MSIX | `2026-08-26-release-hardening.md` |
| MSIX bundle | `2026-08-26-release-hardening.md` |
| Actions-only signing | `2026-08-26-release-hardening.md` |
| GitHub Release publication | `2026-08-26-release-hardening.md` |
| Canonical end-user README | `2026-08-26-release-hardening.md` |
| Upgrade/uninstall/reset/troubleshooting docs | `2026-08-26-release-hardening.md` |
| Release verification matrix | `2026-08-26-release-hardening.md` |

## Explicit non-goals preserved

- Existing native taskbar pins are not imported.
- Exact native Jump List cloning is not planned.
- Free Dock resizing is not planned.
- Explorer is not replaced.
- Main extension never requires native hook components to start.
- System shell combined indicators are not scraped/reimplemented from private shell state.

## Placeholder scan

The plans intentionally contain no `TBD`, `TODO`, `implement later`, or `fill in details` implementation placeholders. Optional features are assigned concrete plans/tasks rather than deferred with an undefined action.

Where a value is validation-dependent (for example raising the minimum supported PowerToys version after integration testing), the plan defines the current baseline and the exact condition under which that concrete value may be changed.

## Cross-plan interface consistency

The following identifiers are intentionally shared across plans:

```text
AppProfile
ApplicationMatch
GroupingMode: Grouped | Separate | Smart
DisplayTemplate
WindowSnapshot
DockTarget
IDockDataProvider
ProviderProbeResult
DockDataChange
ProviderHost
TileIdentity
DockTileState
DockCoordinator
TaskbarStateStore
TrayStateStore
```

Dependency direction remains:

```text
Core
  ^
  |
Windows / Providers / Adapters
  ^
  |
Extension
```

Native hook projects communicate through versioned IPC and do not reference CmdPal extension assemblies.

## Review result

**PASS** — every material requirement in the approved architecture has an implementation owner and acceptance gate. No uncovered requirement remains before execution starts.
