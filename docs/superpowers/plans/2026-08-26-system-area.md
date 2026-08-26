# System Area Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a low-jank Dock system area with event-driven volume/network/battery tiles plus optional third-party notification-area mirroring through a minimal Explorer hook and local bridge.

**Architecture:** Shell-owned status indicators are rebuilt from supported Windows APIs. Third-party tray icons use a separate native hook loaded into Explorer, but that DLL only intercepts/serializes tray state; all caching, CmdPal projection, UI Automation fallback and policy live outside Explorer. The normal extension starts even when the tray bridge is missing or broken.

**Tech Stack:** .NET 10, Core Audio COM, Windows networking APIs, power setting notifications, C++20 Win32 hook DLL, named pipes, UI Automation only for reconciliation/fallback, in-memory icon streams, xUnit/FluentAssertions plus native unit/smoke tests.

**Spec:** `docs/superpowers/specs/2026-08-26-cmdpal-dock-plus-design.md`

## Global Constraints

- Do not reverse-engineer Windows 11 combined shell volume/network/battery controls.
- Third-party tray mirroring is optional.
- Explorer hook must contain no UI/profile/settings/network logic.
- Tray updates are event-driven when hook is healthy.
- No permanent 3-second UIA polling loop.
- Dynamic tray icon bytes remain in memory; no repeated `%TEMP%` PNG writes.
- Synthesized mouse input is fallback-only.

---

### Task 1: Define tray protocol and state reducer

**Files:**
- Create: `src/CmdPalDockPlus.Core/Tray/TrayIconKey.cs`
- Create: `src/CmdPalDockPlus.Core/Tray/TrayIconSnapshot.cs`
- Create: `src/CmdPalDockPlus.Core/Tray/TrayMessage.cs`
- Create: `src/CmdPalDockPlus.Core/Tray/TrayStateStore.cs`
- Create: `tests/CmdPalDockPlus.Core.Tests/Tray/TrayStateStoreTests.cs`
- Create: `tests/CmdPalDockPlus.Protocol.Tests/CmdPalDockPlus.Protocol.Tests.csproj`
- Create: `tests/CmdPalDockPlus.Protocol.Tests/TrayProtocolTests.cs`

**Interfaces:**
- Produces protocol v1 Add/Modify/Delete/Reset messages and a deduplicated immutable tray snapshot.

- [ ] **Step 1: Write reducer tests**

```csharp
[Fact]
public void ModifyWithIdenticalVisualStateDoesNotRaiseChanged()
{
    var store = new TrayStateStore();
    var icon = Fixtures.TrayIcon(ownerHwnd: 10, id: 2, tooltip: "Sync", pixelsHash: 123);
    store.Apply(new TrayAdded(1, icon));
    var changes = 0;
    store.Changed += (_, _) => changes++;

    store.Apply(new TrayModified(2, icon));

    changes.Should().Be(0);
}
```

Also test delete, Explorer reset, duplicate owner/id records and sequence ordering.

- [ ] **Step 2: Define bounded binary protocol**

Header:

```c
struct TrayMessageHeader {
    uint32_t magic;          // 'CDP1'
    uint16_t major;          // 1
    uint16_t type;           // add/modify/delete/reset
    uint32_t payloadLength;  // <= 1 MiB
    uint64_t sequence;
};
```

Payload uses explicit little-endian fixed-width integers plus UTF-8 length-prefixed strings. Icon dimensions are capped at 256x256 and RGBA payload length must equal `width * height * 4`.

- [ ] **Step 3: Implement parser validation tests**

Reject wrong magic/version, oversized payload, negative/impossible dimensions, truncated messages and stale sequence values.

- [ ] **Step 4: Run tests and commit**

```bash
dotnet test tests/CmdPalDockPlus.Core.Tests --filter Tray
dotnet test tests/CmdPalDockPlus.Protocol.Tests --filter Tray
git add src/CmdPalDockPlus.Core/Tray tests/CmdPalDockPlus.Core.Tests/Tray tests/CmdPalDockPlus.Protocol.Tests
git commit -m "feat: define tray state and protocol"
```

---

### Task 2: Build the minimal Explorer tray hook

**Files:**
- Create: `src/CmdPalDockPlus.SysTrayHook/CmdPalDockPlus.SysTrayHook.vcxproj`
- Create: `src/CmdPalDockPlus.SysTrayHook/HookEntry.cpp`
- Create: `src/CmdPalDockPlus.SysTrayHook/TrayWindowSubclass.cpp`
- Create: `src/CmdPalDockPlus.SysTrayHook/TrayWireParser.cpp`
- Create: `src/CmdPalDockPlus.SysTrayHook/IconPixels.cpp`
- Create: `src/CmdPalDockPlus.SysTrayHook/PipeWriter.cpp`
- Create: `src/CmdPalDockPlus.SysTrayHook/include/TrayProtocol.h`
- Create: `tests/native/SysTrayHookTests/SysTrayHookTests.vcxproj`
- Create: `tests/native/SysTrayHookTests/TrayWireParserTests.cpp`

**Interfaces:**
- Consumes Explorer notification-area messages at `Shell_TrayWnd`.
- Produces validated tray protocol events to the local bridge.

- [ ] **Step 1: Unit-test parsing using captured synthetic structures**

```cpp
TEST(TrayWireParser, IgnoresIconBytesWhenNifIconIsNotSet)
{
    FakeShellTrayData data = MakeModify(/*flags=*/NIF_TIP, /*hIcon=*/FakeIconHandle());
    ParsedTrayEvent event{};
    ASSERT_TRUE(ParseTrayMessage(data.Bytes(), event));
    EXPECT_FALSE(event.hasIconPixels);
}
```

Add tests for Add/Modify/Delete, callback message, owner HWND, id/GUID and tooltip bounds.

- [ ] **Step 2: Implement Explorer-only injection entry**

Controller will install `WH_GETMESSAGE` on the `Shell_TrayWnd` thread to load the DLL. After startup the hook DLL subclasses `Shell_TrayWnd`; the injection hook is removed by the controller after pipe connection.

- [ ] **Step 3: Keep callback path minimal**

On relevant `WM_COPYDATA` notification-area messages:

```text
parse fixed fields
if NIF_ICON changed -> copy HICON to RGBA
write one bounded message to already-open pipe
forward to original window proc
```

No disk access. No settings. No async task framework inside Explorer.

- [ ] **Step 4: Copy icon pixels only when required**

Use `GetIconInfo`/DIB conversion only when `NIF_ICON` is set for Add/Modify. Alpha-normalize into BGRA/RGBA as chosen by protocol and free every GDI object deterministically.

- [ ] **Step 5: Run native tests and commit**

```powershell
msbuild tests/native/SysTrayHookTests/SysTrayHookTests.vcxproj /p:Configuration=Release /p:Platform=x64
```

Expected: native tests pass without loading Explorer.

```bash
git add src/CmdPalDockPlus.SysTrayHook tests/native/SysTrayHookTests
git commit -m "feat: add minimal Explorer tray hook"
```

---

### Task 3: Implement tray bridge/controller and Explorer recovery

**Files:**
- Create: `src/CmdPalDockPlus.SysTrayBridge/CmdPalDockPlus.SysTrayBridge.csproj`
- Create: `src/CmdPalDockPlus.SysTrayBridge/ExplorerTrayInjector.cs`
- Create: `src/CmdPalDockPlus.SysTrayBridge/TrayPipeServer.cs`
- Create: `src/CmdPalDockPlus.SysTrayBridge/TrayBridgeService.cs`
- Create: `src/CmdPalDockPlus.SysTrayBridge/ExplorerLifecycleWatcher.cs`
- Create: `tests/CmdPalDockPlus.Protocol.Tests/TrayBridgeRecoveryTests.cs`

**Interfaces:**
- Produces bridge health state + tray messages; owns hook injection/reconnection.

- [ ] **Step 1: Test pipe reconnect state machine**

States:

```text
Stopped -> Starting -> Connected
Connected -> ExplorerLost -> Starting
Starting -> Failed -> Backoff -> Starting
```

Backoff: `1s, 2s, 5s, 10s, 30s`, capped. Manual disable goes directly to `Stopped`.

- [ ] **Step 2: Implement Explorer process/thread discovery**

Find `Shell_TrayWnd`, get its owning thread/process, verify process image is `explorer.exe` in the current interactive session before injection.

- [ ] **Step 3: Inject and immediately remove the loader hook after connection**

Use `SetWindowsHookEx(WH_GETMESSAGE, ...)`, post `WM_NULL` to the shell-tray thread, await pipe handshake, then `UnhookWindowsHookEx` in the controller.

- [ ] **Step 4: Force tray state rebuild safely**

After hook connection or Explorer restart, broadcast `TaskbarCreated` once so third-party applications re-register their icons. Store handles Reset/Add sequences without duplicate UI.

- [ ] **Step 5: Run recovery tests and commit**

```bash
dotnet test tests/CmdPalDockPlus.Protocol.Tests --filter TrayBridgeRecovery
git add src/CmdPalDockPlus.SysTrayBridge tests/CmdPalDockPlus.Protocol.Tests
git commit -m "feat: control tray hook and recover after Explorer restarts"
```

---

### Task 4: Project tray icons into CmdPal with in-memory icons and native callbacks

**Files:**
- Create: `src/CmdPalDockPlus.Extension/SystemArea/TrayDockBand.cs`
- Create: `src/CmdPalDockPlus.Extension/SystemArea/TrayCommandItem.cs`
- Create: `src/CmdPalDockPlus.Extension/SystemArea/InMemoryIconFactory.cs`
- Create: `src/CmdPalDockPlus.Windows/Tray/TrayIconInvoker.cs`
- Create: `tests/CmdPalDockPlus.Core.Tests/Tray/TrayProjectionTests.cs`

**Interfaces:**
- Consumes: `TrayStateStore` snapshots.
- Produces: third-party tray Dock items + left/right/middle callback actions.

- [ ] **Step 1: Test projection deduplication**

An unchanged pixel hash and tooltip must preserve the existing command item/icon object and emit no property change.

- [ ] **Step 2: Encode pixels in memory**

Convert RGBA to an in-memory PNG/BMP stream, expose through `IRandomAccessStreamReference`, then `IconData`. Never write icon files to disk.

- [ ] **Step 3: Invoke owner callback directly where available**

Replay the tray callback message to owner HWND with expected left/right/middle message codes. Use `AllowSetForegroundWindow` where needed before context interaction. Do not move the user's real cursor.

- [ ] **Step 4: Add Dock band modes**

Settings:

```text
Third-party tray: Disabled | Overflow button | Inline
Show hidden icons: true/false
```

Overflow button opens an `IListPage`; Inline returns one command item per eligible icon.

- [ ] **Step 5: Test/build and commit**

```bash
dotnet test tests/CmdPalDockPlus.Core.Tests --filter TrayProjection
git add src/CmdPalDockPlus.Extension/SystemArea src/CmdPalDockPlus.Windows/Tray tests/CmdPalDockPlus.Core.Tests/Tray
git commit -m "feat: mirror third-party tray icons in the dock"
```

---

### Task 5: Add event-driven UI Automation fallback/reconciliation

**Files:**
- Create: `src/CmdPalDockPlus.Windows/Tray/UiaTrayReconciler.cs`
- Create: `src/CmdPalDockPlus.Windows/Tray/ITrayUiaBackend.cs`
- Create: `tests/CmdPalDockPlus.Windows.Tests/Tray/UiaTrayReconcilerTests.cs`

**Interfaces:**
- Produces visibility/overflow reconciliation; does not own canonical icon add/modify/delete while hook is healthy.

- [ ] **Step 1: Test debounce behavior**

100 structure-change notifications within 100 ms produce one scan after a 150 ms debounce window.

- [ ] **Step 2: Subscribe to UIA structure changes**

Watch only the taskbar/notification-area subtree. Rescan visible buttons after structure events. A slow 60-second watchdog scan is permitted for recovery, not ordinary updates.

- [ ] **Step 3: Handle overflow limitations explicitly**

If the overflow flyout is closed, UIA cannot classify every hidden icon reliably. Keep hook-captured icons in store and mark visibility as `Unknown/Overflow` rather than deleting them.

- [ ] **Step 4: Add fallback invocation policy**

For UIA-only icons use InvokePattern when available. Synthesized click at UIA clickable point is allowed only when no owner callback path exists; log this in diagnostics as fallback invocation.

- [ ] **Step 5: Test and commit**

```bash
dotnet test tests/CmdPalDockPlus.Windows.Tests --filter UiaTray
git add src/CmdPalDockPlus.Windows/Tray tests/CmdPalDockPlus.Windows.Tests/Tray
git commit -m "feat: add event-driven tray UIA reconciliation"
```

---

### Task 6: Implement volume provider

**Files:**
- Create: `src/CmdPalDockPlus.Providers/System/VolumeProvider.cs`
- Create: `src/CmdPalDockPlus.Windows/Audio/AudioEndpointService.cs`
- Create: `tests/CmdPalDockPlus.Providers.Tests/System/VolumeProviderTests.cs`

**Interfaces:**
- Fields: `system.volume.percent`, `system.volume.muted`.
- Actions: `system.volume.toggleMute`, `system.volume.up`, `system.volume.down`.

- [ ] **Step 1: Test callback-driven changes**

Fake endpoint invokes a control-change callback; provider emits one percentage/mute change without any timer.

- [ ] **Step 2: Implement Core Audio notification registration**

Register endpoint volume callback on current default render endpoint. Handle default-device change by unregistering old endpoint and registering new endpoint.

- [ ] **Step 3: Implement actions with bounded increments**

Volume up/down defaults to 2 percentage points and clamps `[0, 1]`. Toggle mute flips current endpoint state.

- [ ] **Step 4: Test and commit**

```bash
dotnet test tests/CmdPalDockPlus.Providers.Tests --filter VolumeProvider
git add src/CmdPalDockPlus.Providers/System src/CmdPalDockPlus.Windows/Audio tests/CmdPalDockPlus.Providers.Tests/System
git commit -m "feat: add event-driven volume dock provider"
```

---

### Task 7: Implement network and battery/power providers

**Files:**
- Create: `src/CmdPalDockPlus.Providers/System/NetworkProvider.cs`
- Create: `src/CmdPalDockPlus.Providers/System/PowerProvider.cs`
- Create: `src/CmdPalDockPlus.Windows/System/NetworkStatusService.cs`
- Create: `src/CmdPalDockPlus.Windows/System/PowerStatusService.cs`
- Create: `tests/CmdPalDockPlus.Providers.Tests/System/NetworkProviderTests.cs`
- Create: `tests/CmdPalDockPlus.Providers.Tests/System/PowerProviderTests.cs`

**Interfaces:**
- Network fields: `system.network.connected`, `system.network.profile` when available. Action: open Network settings.
- Power fields: `system.power.batteryPercent`, `system.power.source`, `system.power.charging` when available.

- [ ] **Step 1: Test event-driven network state**

Fake `NetworkStatusChanged` event updates provider. No timer is created.

- [ ] **Step 2: Implement network service**

Use Windows network-status events to refresh the current connectivity/profile snapshot. Opening settings uses `ms-settings:network`/supported URI via ShellExecute.

- [ ] **Step 3: Test power notification updates**

Feed AC/DC source and battery percentage messages; provider emits only changed fields.

- [ ] **Step 4: Implement power-setting notifications**

Register `GUID_ACDC_POWER_SOURCE` and `GUID_BATTERY_PERCENTAGE_REMAINING`; surface messages through a hidden message-only/native host window owned by the extension helper. No periodic battery polling.

- [ ] **Step 5: Test and commit**

```bash
dotnet test tests/CmdPalDockPlus.Providers.Tests --filter "NetworkProvider|PowerProvider"
git add src/CmdPalDockPlus.Providers/System src/CmdPalDockPlus.Windows/System tests/CmdPalDockPlus.Providers.Tests/System
git commit -m "feat: add network and power dock providers"
```

---

### Task 8: Add system-area settings, diagnostics and performance verification

**Files:**
- Create: `src/CmdPalDockPlus.Extension/SystemArea/SystemAreaSettingsPage.cs`
- Create: `src/CmdPalDockPlus.Extension/Diagnostics/NativeBridgeDiagnosticsPage.cs`
- Create: `tests/CmdPalDockPlus.Core.Tests/Tray/TrayPerformanceTests.cs`
- Create: `docs/testing/system-area-checklist.md`
- Modify: `README.md`

**Interfaces:**
- Settings expose independent enablement of Volume, Network, Battery and third-party Tray.

- [ ] **Step 1: Add settings controls**

Users can order system tiles and choose icon-only vs title/subtitle templates from their provider fields. Tray bridge has an explicit enable toggle and health status.

- [ ] **Step 2: Add diagnostics**

Show Explorer PID, bridge state, protocol version, last connect time, icon count, UIA fallback status and last error without dumping tray tooltip/window-title user content by default.

- [ ] **Step 3: Add performance regression test**

Apply 10,000 identical Modify messages to one tray icon; state store should raise one changed event after initial add and allocate no image encoding work when pixel hash is unchanged.

- [ ] **Step 4: Complete README setup/security sections**

Explain supported system APIs, optional Explorer hook, why shell combined indicators are not scraped, and exactly how to enable/disable tray mirroring.

- [ ] **Step 5: Run test suites and commit**

```bash
dotnet test CmdPalDockPlus.sln -c Release -p:Platform=x64
git add src/CmdPalDockPlus.Extension/SystemArea src/CmdPalDockPlus.Extension/Diagnostics tests docs/testing/system-area-checklist.md README.md
git commit -m "feat: complete dock system area"
```

---

## Slice acceptance check

```text
[ ] Volume changes update via callbacks, not polling.
[ ] Network and power changes are event-driven.
[ ] Third-party tray add/modify/delete appears live when bridge is enabled.
[ ] Animated/changing tray icons cause no repeated disk writes.
[ ] Explorer hook contains only interception/parsing/icon-copy/pipe work.
[ ] UIA uses structure events plus slow watchdog, not 3-second polling.
[ ] Explorer restart reconnects and rebuilds tray state.
[ ] Disabling/breaking tray bridge leaves volume/network/battery and app tiles functional.
[ ] Right/middle tray actions prefer owner callbacks over synthesized pointer input.
```
