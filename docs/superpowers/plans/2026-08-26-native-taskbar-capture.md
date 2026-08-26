# Native Taskbar Capture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add explicit per-profile capture of third-party `ITaskbarList3` progress and overlay-icon calls so Dock tiles can mirror native taskbar progress/overlay state without making invasive capture a dependency of the main extension.

**Architecture:** A user-mode controller targets only explicitly enabled app profiles, injects an architecture-matched native hook DLL, wraps/intercepts `CLSID_TaskbarList` activation, forwards calls to the real COM implementation, and emits bounded local IPC events. The managed `TaskbarStateProvider` reduces those events to per-HWND state and feeds existing tile composition. x86/x64/ARM64 hook binaries are separate from the x64/ARM64 extension package runtime.

**Tech Stack:** C++20 COM/Win32, `IActivationFilter`/COM activation interception where viable with a fallback hook path, named pipes, .NET 10 provider/reducer code, xUnit/FluentAssertions, native tests, Windows process architecture APIs.

**Spec:** `docs/superpowers/specs/2026-08-26-cmdpal-dock-plus-design.md`

## Global Constraints

- Native taskbar capture is disabled by default.
- Enablement is per App Profile, never global by default.
- Basic app/window Dock functionality cannot depend on capture.
- Capture forwards original `ITaskbarList3` behavior after observing calls.
- Overlay pixels are copied inside the target process before IPC.
- No unrelated process memory is collected.
- Hook failure removes captured state but must not destabilize the extension.
- Native hook matrix includes x86, x64 and ARM64 where the toolchain supports it.

---

### Task 1: Define capture protocol and managed state reducer

**Files:**
- Create: `src/CmdPalDockPlus.Core/Taskbar/TaskbarProgressState.cs`
- Create: `src/CmdPalDockPlus.Core/Taskbar/TaskbarOverlay.cs`
- Create: `src/CmdPalDockPlus.Core/Taskbar/TaskbarCaptureMessage.cs`
- Create: `src/CmdPalDockPlus.Core/Taskbar/TaskbarStateStore.cs`
- Create: `tests/CmdPalDockPlus.Core.Tests/Taskbar/TaskbarStateStoreTests.cs`
- Create: `tests/CmdPalDockPlus.Protocol.Tests/TaskbarCaptureProtocolTests.cs`

**Interfaces:**
- Produces per-HWND `TaskbarWindowState` with progress mode/value and copied overlay image.

- [ ] **Step 1: Write reducer tests**

```csharp
[Fact]
public void ProgressValueAndStateAreCombinedPerWindow()
{
    var store = new TaskbarStateStore();
    store.Apply(new ProgressStateChanged(1, (nint)0x10, TaskbarProgressMode.Normal));
    store.Apply(new ProgressValueChanged(2, (nint)0x10, 25, 100));

    store.Get((nint)0x10).Should().BeEquivalentTo(new TaskbarWindowState(
        TaskbarProgressMode.Normal, 25, 100, null));
}

[Fact]
public void NoProgressClearsValue()
{
    var store = new TaskbarStateStore();
    store.Apply(new ProgressValueChanged(1, (nint)0x10, 5, 10));
    store.Apply(new ProgressStateChanged(2, (nint)0x10, TaskbarProgressMode.None));
    store.Get((nint)0x10).Completed.Should().BeNull();
}
```

- [ ] **Step 2: Define protocol messages**

Protocol v1 message types:

```text
Hello(processId, architecture, protocolVersion)
SetProgressState(hwnd, state)
SetProgressValue(hwnd, completed, total)
SetOverlayIcon(hwnd, width, height, rgba, description)
ClearOverlayIcon(hwnd)
ProcessExiting(processId)
```

Maximum overlay dimensions: 256x256; maximum message: 1 MiB.

- [ ] **Step 3: Test malformed/ordering behavior**

Reject `total == 0` only at display-normalization time, not protocol parse time; preserve original numeric call for diagnostics. Reject stale sequence messages per process/session. `ProcessExiting` removes all HWND states owned by that source process.

- [ ] **Step 4: Run tests and commit**

```bash
dotnet test tests/CmdPalDockPlus.Core.Tests --filter TaskbarState
dotnet test tests/CmdPalDockPlus.Protocol.Tests --filter TaskbarCapture
git add src/CmdPalDockPlus.Core/Taskbar tests/CmdPalDockPlus.Core.Tests/Taskbar tests/CmdPalDockPlus.Protocol.Tests
git commit -m "feat: define taskbar capture state and protocol"
```

---

### Task 2: Implement native `ITaskbarList3` forwarding wrapper

**Files:**
- Create: `src/CmdPalDockPlus.TaskbarHook/CmdPalDockPlus.TaskbarHook.vcxproj`
- Create: `src/CmdPalDockPlus.TaskbarHook/TaskbarList3Wrapper.h`
- Create: `src/CmdPalDockPlus.TaskbarHook/TaskbarList3Wrapper.cpp`
- Create: `src/CmdPalDockPlus.TaskbarHook/RealTaskbarFactory.cpp`
- Create: `src/CmdPalDockPlus.TaskbarHook/CapturePipeClient.cpp`
- Create: `src/CmdPalDockPlus.TaskbarHook/include/TaskbarCaptureProtocol.h`
- Create: `tests/native/TaskbarHookTests/TaskbarHookTests.vcxproj`
- Create: `tests/native/TaskbarHookTests/TaskbarList3WrapperTests.cpp`

**Interfaces:**
- Implements all `ITaskbarList3` methods and forwards them to a supplied real `ITaskbarList3` instance.
- Observes at minimum `SetProgressState`, `SetProgressValue`, `SetOverlayIcon`.

- [ ] **Step 1: Build a fake real `ITaskbarList3` and forwarding test**

```cpp
TEST(TaskbarList3Wrapper, SetProgressValueForwardsExactArguments)
{
    FakeTaskbarList3 real;
    RecordingCaptureSink sink;
    TaskbarList3Wrapper wrapper(&real, &sink);

    HWND hwnd = reinterpret_cast<HWND>(0x1234);
    ASSERT_HRESULT_SUCCEEDED(wrapper.SetProgressValue(hwnd, 7, 9));
    EXPECT_EQ(real.lastProgressHwnd, hwnd);
    EXPECT_EQ(real.lastCompleted, 7u);
    EXPECT_EQ(real.lastTotal, 9u);
    EXPECT_EQ(sink.progressValues.size(), 1u);
}
```

- [ ] **Step 2: Implement COM identity/lifetime correctly**

`QueryInterface` supports `IUnknown`, `ITaskbarList`, `ITaskbarList2`, `ITaskbarList3`; `AddRef/Release` use atomic refcount. Wrapper retains the real object exactly once and releases it in destructor.

- [ ] **Step 3: Forward every method before/after capture according to failure policy**

Capture failure must never make the app's original taskbar call fail. The wrapper returns the real object's HRESULT. IPC send is best-effort and bounded/non-blocking on the app UI thread.

- [ ] **Step 4: Test progress capture and no-pipe behavior**

When capture sink is disconnected, `SetProgressState/Value` still reaches fake real object and returns its HRESULT.

- [ ] **Step 5: Commit**

```bash
git add src/CmdPalDockPlus.TaskbarHook tests/native/TaskbarHookTests
git commit -m "feat: wrap and observe ITaskbarList3 calls"
```

---

### Task 3: Capture overlay icons inside the target process

**Files:**
- Create: `src/CmdPalDockPlus.TaskbarHook/IconCapture.cpp`
- Create: `src/CmdPalDockPlus.TaskbarHook/IconCapture.h`
- Modify: `src/CmdPalDockPlus.TaskbarHook/TaskbarList3Wrapper.cpp`
- Create: `tests/native/TaskbarHookTests/IconCaptureTests.cpp`

**Interfaces:**
- Produces copied RGBA bytes detached from target-process `HICON` lifetime.

- [ ] **Step 1: Test null icon clears overlay**

```cpp
TEST(TaskbarList3Wrapper, NullOverlayProducesClearMessage)
{
    FakeTaskbarList3 real;
    RecordingCaptureSink sink;
    TaskbarList3Wrapper wrapper(&real, &sink);
    wrapper.SetOverlayIcon(reinterpret_cast<HWND>(0x20), nullptr, L"");
    ASSERT_EQ(sink.clearOverlays.size(), 1u);
}
```

- [ ] **Step 2: Implement HICON copy**

Use `GetIconInfo`, determine dimensions from color/mask bitmaps, render/copy to a 32-bit DIB section, normalize alpha, serialize maximum 256x256. Delete `hbmColor`, `hbmMask` and DIB/GDI resources on every path.

- [ ] **Step 3: Copy description safely**

Convert `LPCWSTR` description to UTF-8 with a 4 KiB cap. Null description becomes empty string.

- [ ] **Step 4: Test resource cleanup/error fallback**

If pixel copy fails, forward `SetOverlayIcon` to the real taskbar unchanged and emit no malformed capture event.

- [ ] **Step 5: Commit**

```bash
git add src/CmdPalDockPlus.TaskbarHook tests/native/TaskbarHookTests/IconCaptureTests.cpp
git commit -m "feat: copy taskbar overlay icons for dock capture"
```

---

### Task 4: Intercept `CLSID_TaskbarList` activation inside target processes

**Files:**
- Create: `src/CmdPalDockPlus.TaskbarHook/ActivationFilter.cpp`
- Create: `src/CmdPalDockPlus.TaskbarHook/ActivationFilter.h`
- Create: `src/CmdPalDockPlus.TaskbarHook/HookBootstrap.cpp`
- Create: `src/CmdPalDockPlus.TaskbarHook/HookBootstrap.h`
- Create: `tests/native/TaskbarHookTests/ActivationFilterTests.cpp`

**Interfaces:**
- Primary path: process-wide COM activation filter for `CLSID_TaskbarList` returning wrapper/factory behavior.
- Fallback path is implemented only if primary path cannot cover required activation behavior on supported Windows builds.

- [ ] **Step 1: Test filter discrimination**

Activation requests for any CLSID other than `CLSID_TaskbarList` pass through untouched. Taskbar activation produces a wrapped `ITaskbarList3` whose real implementation is created without recursively re-entering the filter.

- [ ] **Step 2: Implement bootstrap registration before normal app use where possible**

Injected DLL initialization queues bootstrap work off `DllMain`; do not call COM-heavy code under loader lock. Bootstrap initializes the pipe and registers the process-wide activation filter.

- [ ] **Step 3: Guard recursion**

Use thread-local recursion guard around creation of the real TaskbarList object. If recursion is detected, fail open to real COM activation rather than deadlock.

- [ ] **Step 4: Record unsupported/pre-existing interface condition**

If the app obtained `ITaskbarList3` before injection, capture cannot retroactively observe that instance. Controller diagnostics must mark the process `injected-late/coverage-unknown`; do not falsely report full capture coverage.

- [ ] **Step 5: Run native tests and commit**

```bash
git add src/CmdPalDockPlus.TaskbarHook tests/native/TaskbarHookTests/ActivationFilterTests.cpp
git commit -m "feat: intercept taskbar COM activation in target apps"
```

---

### Task 5: Build per-profile capture controller and architecture selection

**Files:**
- Create: `src/CmdPalDockPlus.TaskbarCapture/CmdPalDockPlus.TaskbarCapture.csproj`
- Create: `src/CmdPalDockPlus.TaskbarCapture/TaskbarCaptureController.cs`
- Create: `src/CmdPalDockPlus.TaskbarCapture/TargetProcessMatcher.cs`
- Create: `src/CmdPalDockPlus.TaskbarCapture/ProcessArchitectureResolver.cs`
- Create: `src/CmdPalDockPlus.TaskbarCapture/NativeInjector.cs`
- Create: `src/CmdPalDockPlus.TaskbarCapture/CapturePipeServer.cs`
- Create: `tests/CmdPalDockPlus.Protocol.Tests/TaskbarCaptureControllerTests.cs`

**Interfaces:**
- Consumes: profiles with `NativeCapture.TaskbarState == true` and live process/window inventory.
- Produces: injection sessions + managed protocol stream.

- [ ] **Step 1: Test explicit opt-in matching**

```csharp
[Fact]
public void DisabledProfileNeverTargetsProcess()
{
    var profile = Fixtures.Profile(nativeTaskbarCapture: false);
    TargetProcessMatcher.ShouldCapture(profile, Fixtures.CodeProcess()).Should().BeFalse();
}
```

Also test executable path mismatch and child/helper process mismatch.

- [ ] **Step 2: Resolve target architecture**

Use supported process-machine APIs to classify `X86`, `X64`, `Arm64`; choose `CmdPalDockPlus.TaskbarHook.x86.dll`, `.x64.dll`, `.arm64.dll`. Unsupported architecture records a non-fatal health error.

- [ ] **Step 3: Implement injection lifecycle**

On newly matched process, inject once and await pipe `Hello` handshake. On process exit, dispose session and remove its state. Never repeatedly inject the same PID after successful handshake.

- [ ] **Step 4: Bound retries**

Failed injection retries at most three times per process with `1s`, `5s`, `30s` delays; access-denied/protected-process failures become terminal for that PID.

- [ ] **Step 5: Test controller with fake injector and commit**

```bash
dotnet test tests/CmdPalDockPlus.Protocol.Tests --filter TaskbarCaptureController
git add src/CmdPalDockPlus.TaskbarCapture tests/CmdPalDockPlus.Protocol.Tests
git commit -m "feat: control opt-in taskbar capture sessions"
```

---

### Task 6: Expose captured state through `TaskbarStateProvider`

**Files:**
- Create: `src/CmdPalDockPlus.Providers/Taskbar/TaskbarStateProvider.cs`
- Create: `src/CmdPalDockPlus.Extension/Dock/DynamicIconComposer.cs`
- Modify: `src/CmdPalDockPlus.Extension/Dock/DockCoordinator.cs`
- Modify: `src/CmdPalDockPlus.Extension/Settings/ProfileEditorPage.cs`
- Create: `tests/CmdPalDockPlus.Providers.Tests/Taskbar/TaskbarStateProviderTests.cs`

**Interfaces:**
- Fields: `taskbar.progress.current`, `taskbar.progress.total`, `taskbar.progress.percent`, `taskbar.progressState`, `taskbar.overlay`, `taskbar.overlayDescription`.

- [ ] **Step 1: Test grouped/separate aggregation policy**

Separate tile uses exact HWND state. Grouped tile display policy is deterministic: active/MRU window's progress/overlay wins; if it has no state, use most-recent state-bearing window. Do not average unrelated per-window progress values.

- [ ] **Step 2: Implement provider watches from state store events**

No sampling timer. Filter events by requested HWNDs/fields.

- [ ] **Step 3: Add explicit profile toggle**

Settings text:

```text
Capture native taskbar progress/overlay for this app
[off by default]
Requires injecting a small compatibility hook into matching app processes.
```

Turning it off disposes matching capture sessions and clears captured values.

- [ ] **Step 4: Compose overlay/progress representation**

Use existing app icon as base. Overlay image is composed at lower-right with bounded size. Progress can appear through title/subtitle template fields immediately; if the current CmdPal Dock host cannot draw a native progress ring/bar, icon composition is the visual fallback rather than a hidden unsupported API.

- [ ] **Step 5: Test and commit**

```bash
dotnet test tests/CmdPalDockPlus.Providers.Tests --filter TaskbarStateProvider
git add src/CmdPalDockPlus.Providers/Taskbar src/CmdPalDockPlus.Extension tests/CmdPalDockPlus.Providers.Tests/Taskbar
git commit -m "feat: expose captured taskbar state to dock tiles"
```

---

### Task 7: Add security, diagnostics and compatibility validation

**Files:**
- Create: `src/CmdPalDockPlus.Extension/Diagnostics/TaskbarCaptureDiagnosticsPage.cs`
- Create: `docs/security/native-taskbar-capture.md`
- Create: `docs/testing/native-taskbar-capture-checklist.md`
- Modify: `README.md`

**Interfaces:**
- Diagnostics only; no new capture mechanism.

- [ ] **Step 1: Add diagnostics per process**

Show PID, executable name, architecture, injection status, handshake time, capture coverage (`active`, `injected-late`, `access-denied`, `unsupported`) and last protocol sequence. Do not log arbitrary process memory or window-title content by default.

- [ ] **Step 2: Document threat/compatibility model**

Explain that code is injected only into explicitly enabled matching applications, why protected/anti-cheat/sandboxed apps may reject it, and that disabling the option restores normal no-injection operation.

- [ ] **Step 3: Write manual fixture app for end-to-end testing**

Create `tests/fixtures/TaskbarFixtureApp/` with buttons that call:

```text
SetProgressState(Normal/Paused/Error/Indeterminate/NoProgress)
SetProgressValue(0..100)
SetOverlayIcon(test HICON / null)
```

The fixture is a test executable only and is not packaged for end users.

- [ ] **Step 4: Verify architecture matrix**

Run fixture/capture for x64, x86 on x64 Windows, and ARM64 where runner/hardware is available. Document unavailable architecture as manual release hardware requirement rather than claiming it was tested.

- [ ] **Step 5: Commit**

```bash
git add src/CmdPalDockPlus.Extension/Diagnostics docs README.md tests/fixtures/TaskbarFixtureApp
git commit -m "docs: harden native taskbar capture diagnostics and security"
```

---

## Slice acceptance check

```text
[ ] Capture is off for every new profile by default.
[ ] Enabling one app never injects unrelated processes.
[ ] SetProgressState and SetProgressValue reach the Dock and still reach the real taskbar implementation.
[ ] SetOverlayIcon pixels are copied before target HICON lifetime ends.
[ ] Null overlay clears captured overlay.
[ ] Separate-window mode maps state by HWND.
[ ] Grouped mode uses a documented active/MRU aggregation rule.
[ ] Injection/IPC failure does not break app launch/window tiles or the target app's normal taskbar calls.
[ ] Protected/unsupported processes fail closed with diagnostics, not retry storms.
[ ] x86/x64/ARM64 hook artifacts are accounted for in build/release plans.
```
