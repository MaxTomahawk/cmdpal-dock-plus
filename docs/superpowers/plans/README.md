# CmdPal Dock Plus Implementation Plans

This directory contains the execution plans for the approved architecture in `docs/superpowers/specs/2026-08-26-cmdpal-dock-plus-design.md`.

## Required execution order

1. `2026-08-26-smart-dock-foundation.md`
   - Buildable CmdPal extension and MSIX shape
   - profile persistence
   - safe templates
   - window tracking/actions
   - Grouped/Separate tiles
   - first settings flow

2. `2026-08-26-providers-and-smart-grouping.md`
   - provider contracts/host
   - Window/Process providers
   - capability discovery
   - Smart rules
   - VS Code/browser/Terminal adapters

3. `2026-08-26-app-data-attention-and-user-actions.md`
   - Media provider
   - Explorer adapter
   - source-independent attention state
   - safe user-defined actions

4. `2026-08-26-rich-window-ux.md`
   - Recent/Frequent destinations
   - Smart App Menu
   - live DWM thumbnails
   - minimal version-pinned PowerToys Dock hover bridge

5. `2026-08-26-system-area.md`
   - event-driven Volume/Network/Battery
   - third-party tray protocol/store
   - minimal Explorer tray hook
   - bridge/recovery/UIA fallback
   - in-memory tray icons

6. `2026-08-26-native-taskbar-capture.md`
   - per-profile opt-in capture
   - architecture-matched `ITaskbarList3` wrapper/hook
   - progress and overlay state
   - capture diagnostics/security

7. `2026-08-26-release-hardening.md`
   - locked dependency/toolchain
   - PR/main CI
   - x64/ARM64 MSIX + bundle
   - native x86/x64/ARM64 matrix
   - Actions-only signing
   - GitHub Releases
   - final canonical README/manual

## Execution policy

Each plan is implemented task-by-task with TDD and a commit after every independently reviewable task. Do not start a later plan merely because some files can be scaffolded early: later plans assume the acceptance check of their prerequisite plan is green.

The main extension must remain usable after every slice. Optional native features are never allowed to become startup dependencies.

## Review gates

At minimum after each plan:

```text
1. Run the plan's automated test/build commands.
2. Run its acceptance checklist.
3. Review the diff against the architecture spec.
4. Confirm no planned feature was silently downgraded or claimed without evidence.
5. Only then begin the next plan.
```

For release hardening, the release-dry-run and manual Windows matrix are additional hard gates before the first stable version tag.
