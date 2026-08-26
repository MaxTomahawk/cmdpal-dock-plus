# Optional PowerToys hover compatibility patch

CmdPal Dock Plus works without modifying PowerToys. The patch in `patches/cmdpal-dock-hover.patch` only enables native Dock hover previews.

## Compatibility boundary

The patch is pinned to the exact PowerToys commit recorded in `patches/upstream-commit.txt`.

The PowerToys side knows only:

- the existing CmdPal Dock command id (`DockItemViewModel.Command.Id`),
- whether the pointer entered or left a Dock item,
- the item's screen-space rectangle.

It does **not** know anything about CmdPal Dock Plus profiles, HWND sets, DWM thumbnails, taskbar capture, tray mirroring, settings, or application-specific providers.

Events are sent as newline-delimited protocol-v1 JSON to the local per-user pipe `CmdPalDockPlus.hover.v1`. Pipe connection and writes happen off the UI thread. If CmdPal Dock Plus is not running, events are simply dropped and normal CmdPal Dock behavior is unchanged.

## Verify/apply

Check the patch against a PowerToys checkout at the pinned commit:

```powershell
pwsh scripts/verify-powertoys-patch.ps1 -PowerToysRoot C:\src\PowerToys
```

Then apply it with normal Git tooling and build PowerToys/Command Palette from that checkout. A non-matching upstream checkout is intentionally treated as incompatible rather than patched heuristically.
