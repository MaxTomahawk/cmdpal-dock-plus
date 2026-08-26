# Release Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the completed feature slices into a reproducible, test-gated GitHub Actions build that publishes signed installable CmdPal Dock Plus releases with exact end-user installation, configuration, upgrade and troubleshooting documentation.

**Architecture:** CI builds/tests every managed and native component from locked dependencies. Tag builds produce architecture-specific extension MSIX packages, a bundle, native optional compatibility artifacts, checksums and symbols; release signing occurs only inside GitHub Actions using a long-lived code-signing certificate stored as repository/environment secrets. `README.md` is finalized against the actual shipped UI and artifacts, not aspirational controls.

**Tech Stack:** GitHub Actions Windows runners, .NET 10, Visual Studio/MSBuild C++ workload, Windows SDK `makeappx`/`signtool`, PowerShell 7, MSIX, SHA-256 checksums, GitHub Releases.

**Spec:** `docs/superpowers/specs/2026-08-26-cmdpal-dock-plus-design.md`

## Global Constraints

- Canonical release binaries must originate from GitHub Actions.
- No manually built local executable/MSIX is uploaded as an official release asset.
- Main extension ships x64 and ARM64.
- Native taskbar hook matrix accounts for x86, x64 and ARM64.
- Optional Explorer tray hook ships x64 and ARM64 matching Explorer architecture.
- Initial package baseline follows the current official PowerToys template and pins `Microsoft.CommandPalette.Extensions` `0.11.260520004`, `Microsoft.Windows.CsWin32` `0.3.183`, `Microsoft.Windows.CsWinRT` `2.2.0`, `Microsoft.Windows.SDK.BuildTools.MSIX` `1.7.20250829.1`, and `Shmuelie.WinRTServer` `2.1.1` unless implementation proves an explicit compatibility need to bump them.
- Supported baseline is PowerToys `0.101.0` or newer unless integration testing establishes a higher minimum before v1.0.0.
- Release signing private key is never committed to the repository.
- README documents every setting/control that actually ships.

---

### Task 1: Lock dependencies, analyzers and deterministic build settings

**Files:**
- Modify: `Directory.Build.props`
- Modify: `Directory.Packages.props`
- Create: `global.json`
- Create: `nuget.config`
- Create: `.editorconfig`
- Create: `scripts/verify-no-unpinned-packages.ps1`

**Interfaces:**
- Produces reproducible restore/build inputs shared by local and Actions builds.

- [ ] **Step 1: Pin current official template package versions**

`Directory.Packages.props` includes:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.CommandPalette.Extensions" Version="0.11.260520004" />
    <PackageVersion Include="Microsoft.Windows.CsWin32" Version="0.3.183" />
    <PackageVersion Include="Microsoft.Windows.CsWinRT" Version="2.2.0" />
    <PackageVersion Include="Microsoft.Windows.SDK.BuildTools.MSIX" Version="1.7.20250829.1" />
    <PackageVersion Include="Shmuelie.WinRTServer" Version="2.1.1" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.4" />
    <PackageVersion Include="FluentAssertions" Version="7.2.0" />
  </ItemGroup>
</Project>
```

If NuGet restore proves a test-package version unavailable, update to the nearest current stable version and commit that exact value before any implementation depends on it; do not use wildcard/floating versions.

- [ ] **Step 2: Enable deterministic/strict build defaults**

```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
  <Deterministic>true</Deterministic>
</PropertyGroup>
```

Native projects use `/W4 /WX` for project-owned code; suppressions must be scoped and commented.

- [ ] **Step 3: Add unpinned-package verification**

PowerShell script scans all `*.csproj` for `PackageReference Version=` and fails unless the package is intentionally private/local. Central package versions are the only normal path.

- [ ] **Step 4: Run clean restore/build and commit**

```powershell
git clean -xfd
dotnet restore CmdPalDockPlus.sln --locked-mode:$false
dotnet build CmdPalDockPlus.sln -c Release -p:Platform=x64
pwsh scripts/verify-no-unpinned-packages.ps1
```

```bash
git add Directory.* global.json nuget.config .editorconfig scripts/verify-no-unpinned-packages.ps1
git commit -m "build: lock toolchain and package versions"
```

---

### Task 2: Add full pull-request/main CI workflow

**Files:**
- Create: `.github/workflows/ci.yml`
- Create: `scripts/build-managed.ps1`
- Create: `scripts/build-native.ps1`
- Create: `scripts/test.ps1`
- Create: `scripts/package-smoke.ps1`

**Interfaces:**
- Produces required CI jobs for managed build/tests, native matrix and package smoke tests.

- [ ] **Step 1: Create workflow triggers and concurrency**

```yaml
name: CI
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true
permissions:
  contents: read
```

- [ ] **Step 2: Add managed x64 test job**

Job uses `windows-2025`, checks out with submodules false, sets up .NET 10 from `global.json`, restores, builds Release x64 and runs all managed tests with TRX output.

Exact commands:

```powershell
dotnet restore CmdPalDockPlus.sln
dotnet build CmdPalDockPlus.sln -c Release -p:Platform=x64 --no-restore
dotnet test CmdPalDockPlus.sln -c Release -p:Platform=x64 --no-build --logger "trx;LogFileName=tests.trx"
```

- [ ] **Step 3: Add ARM64 compile/package job**

Build the extension with `-p:Platform=ARM64`. Tests that cannot execute ARM64 on x64 runner remain compile-time validation and are covered by x64 logic tests.

- [ ] **Step 4: Add native architecture matrix**

Matrix: `x86`, `x64`, `ARM64` for TaskbarHook; `x64`, `ARM64` for SysTrayHook. Build native unit tests where host architecture can execute them; always compile every matrix target.

- [ ] **Step 5: Add package smoke job**

Run `dotnet build ... -p:GenerateAppxPackageOnBuild=true` for x64/ARM64 and assert at least one `.msix` exists per architecture plus expected extension manifest entries.

- [ ] **Step 6: Upload only CI diagnostics, not release binaries**

Upload TRX/log artifacts with 7-day retention. CI artifacts are explicitly non-canonical.

- [ ] **Step 7: Commit**

```bash
git add .github/workflows/ci.yml scripts
git commit -m "ci: build and test managed native and MSIX targets"
```

---

### Task 3: Define version propagation and release artifact layout

**Files:**
- Create: `scripts/Get-Version.ps1`
- Create: `scripts/Set-PackageVersion.ps1`
- Create: `scripts/Collect-ReleaseArtifacts.ps1`
- Create: `docs/releasing/artifact-layout.md`

**Interfaces:**
- Tag `vX.Y.Z` maps to MSIX `X.Y.Z.0`.

- [ ] **Step 1: Test version parser with PowerShell/Pester or direct script assertions**

Accepted:

```text
v0.1.0 -> semver 0.1.0 -> MSIX 0.1.0.0
v1.2.3 -> semver 1.2.3 -> MSIX 1.2.3.0
```

Reject prerelease tags for initial release automation until explicit prerelease packaging is added.

- [ ] **Step 2: Implement manifest/project version injection**

`Set-PackageVersion.ps1 -Version 1.2.3.0` updates only the package identity version properties used by the extension package and verifies all occurrences match afterward.

- [ ] **Step 3: Define exact release staging tree**

```text
artifacts/release/
  CmdPalDockPlus-1.2.3-x64.msix
  CmdPalDockPlus-1.2.3-arm64.msix
  CmdPalDockPlus-1.2.3.msixbundle
  CmdPalDockPlus.Native-1.2.3.zip
  CmdPalDockPlus.PowerToysPatch-1.2.3.zip
  CmdPalDockPlus.cer
  SHA256SUMS.txt
  symbols/
```

Native ZIP contains architecture-labeled optional DLL/controller assets, not duplicate extension package files.

- [ ] **Step 4: Generate deterministic checksum manifest**

Sort filenames ordinally and write lowercase SHA-256 + two spaces + filename.

- [ ] **Step 5: Commit**

```bash
git add scripts/Get-Version.ps1 scripts/Set-PackageVersion.ps1 scripts/Collect-ReleaseArtifacts.ps1 docs/releasing/artifact-layout.md
git commit -m "build: define release version and artifact layout"
```

---

### Task 4: Implement long-lived Actions-only MSIX signing

**Files:**
- Create: `scripts/Import-SigningCertificate.ps1`
- Create: `scripts/Sign-ReleaseArtifacts.ps1`
- Create: `docs/releasing/signing.md`
- Create: `.github/workflows/signing-self-test.yml`

**Interfaces:**
- Requires Actions secrets `MSIX_SIGNING_PFX_BASE64` and `MSIX_SIGNING_PFX_PASSWORD`.
- Produces signed MSIX/MSIXBundle and public `.cer`.

- [ ] **Step 1: Lock initial signing strategy**

Use one long-lived self-signed code-signing certificate with subject exactly matching the MSIX Publisher identity. The PFX private key exists only as a GitHub Actions secret. The public certificate is safe to publish and is attached as `CmdPalDockPlus.cer` so users can trust it in `CurrentUser\TrustedPeople`.

- [ ] **Step 2: Implement import script**

Decode base64 PFX to `$RUNNER_TEMP\cmdpal-dock-plus.pfx`, import to `Cert:\CurrentUser\My`, return thumbprint, and delete PFX file in `finally`.

- [ ] **Step 3: Implement signing script**

Locate Windows SDK `signtool.exe`; sign with SHA-256 file digest and certificate thumbprint. Verify every package immediately:

```powershell
& $signtool verify /pa /v $package
if ($LASTEXITCODE -ne 0) { throw "Signature verification failed: $package" }
```

- [ ] **Step 4: Export public certificate from imported cert**

Export `.cer` only, never PFX. Release workflow later publishes `.cer`.

- [ ] **Step 5: Add manual self-test workflow**

`workflow_dispatch` builds a tiny/package smoke artifact, imports secret cert, signs and verifies it. No release is created.

- [ ] **Step 6: Document maintainer-only one-time certificate setup**

`docs/releasing/signing.md` gives exact `New-SelfSignedCertificate`, `Export-PfxCertificate`, base64 conversion and GitHub secret names. It explicitly warns never to commit PFX/private key.

- [ ] **Step 7: Commit**

```bash
git add scripts/Import-SigningCertificate.ps1 scripts/Sign-ReleaseArtifacts.ps1 docs/releasing/signing.md .github/workflows/signing-self-test.yml
git commit -m "build: add Actions-only MSIX signing"
```

---

### Task 5: Build MSIX x64/ARM64 and bundle in release workflow

**Files:**
- Create: `.github/workflows/release.yml`
- Create: `scripts/Create-MsixBundle.ps1`

**Interfaces:**
- Trigger: pushed tag `v[0-9]+.[0-9]+.[0-9]+`.
- Produces signed release staging directory.

- [ ] **Step 1: Create restricted release workflow permissions**

```yaml
name: Release
on:
  push:
    tags: ['v*.*.*']
permissions:
  contents: write
```

No `pull_request_target`; secrets never run on PR code.

- [ ] **Step 2: Validate tag and set package version**

Checkout exact tag SHA; reject dirty/generated source modifications except versionized build output. Call `Get-Version.ps1` then `Set-PackageVersion.ps1`.

- [ ] **Step 3: Run the same tests before packaging**

Release job must run managed tests and native build/test gates, not assume `main` CI was green.

- [ ] **Step 4: Build architecture MSIX files**

```powershell
dotnet build src/CmdPalDockPlus.Extension/CmdPalDockPlus.Extension.csproj -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageDir="$env:RUNNER_TEMP\AppPackages\x64\"
dotnet build src/CmdPalDockPlus.Extension/CmdPalDockPlus.Extension.csproj -c Release -p:Platform=ARM64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageDir="$env:RUNNER_TEMP\AppPackages\arm64\"
```

- [ ] **Step 5: Create MSIX bundle**

`Create-MsixBundle.ps1` finds exactly one extension MSIX per architecture, creates mapping file, runs Windows SDK `makeappx bundle`, and fails on missing/extra ambiguous package.

- [ ] **Step 6: Sign architecture packages and bundle**

Import Actions certificate, sign and verify all three package assets. Package Publisher must equal certificate subject; script verifies this before signing.

- [ ] **Step 7: Collect optional native components and patch package**

Native ZIP includes built controller/hook DLLs with architecture folders. PowerToys patch ZIP includes patch, pinned upstream commit, patch verification script and compatibility README.

- [ ] **Step 8: Generate checksums and commit workflow**

```bash
git add .github/workflows/release.yml scripts/Create-MsixBundle.ps1
git commit -m "ci: build signed MSIX releases from version tags"
```

---

### Task 6: Publish GitHub Release only after verification

**Files:**
- Modify: `.github/workflows/release.yml`
- Create: `scripts/Generate-ReleaseNotes.ps1`
- Create: `.github/release.yml`

**Interfaces:**
- Produces GitHub Release for exact tag with all staged assets.

- [ ] **Step 1: Add pre-publish assertions**

Release job fails unless staging contains exactly required core assets:

```text
x64 MSIX
ARM64 MSIX
MSIX bundle
public CER
native ZIP
PowerToys patch ZIP
SHA256SUMS.txt
```

Symbols may be optional but if generated must be inside symbols archive.

- [ ] **Step 2: Verify checksums before upload**

Re-read `SHA256SUMS.txt` and independently hash every listed file; fail on mismatch.

- [ ] **Step 3: Generate release notes with install warning**

Top section contains:

```text
Requires PowerToys 0.101.0 or newer.
Install CmdPalDockPlus.cer to Current User > Trusted People before installing the MSIX bundle for this self-signed GitHub release channel.
Native tray/taskbar capture and the PowerToys hover patch are optional.
```

- [ ] **Step 4: Create release using GitHub CLI**

```powershell
gh release create $env:GITHUB_REF_NAME `
  artifacts/release/* `
  --repo $env:GITHUB_REPOSITORY `
  --title "CmdPal Dock Plus $version" `
  --notes-file release-notes.md `
  --verify-tag
```

`GH_TOKEN` is `${{ github.token }}`. No draft release if tests/signature/checksum verification failed.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/release.yml .github/release.yml scripts/Generate-ReleaseNotes.ps1
git commit -m "ci: publish verified GitHub releases"
```

---

### Task 7: Finalize README as exact end-user manual

**Files:**
- Modify: `README.md`
- Create: `docs/configuration/templates.md`
- Create: `docs/configuration/smart-rules.md`
- Create: `docs/configuration/app-adapters.md`
- Create: `docs/configuration/system-area.md`
- Create: `docs/configuration/native-capture.md`
- Create: `docs/troubleshooting.md`

**Interfaces:**
- Documentation must reflect exact shipped settings labels and release filenames.

- [ ] **Step 1: Write prerequisites and verified version floor**

README states Windows 10 19041+ at package level but identifies Windows 11 as the primary tested desktop target if that is what manual matrix confirms. State `PowerToys >= 0.101.0` unless release testing raised the minimum.

- [ ] **Step 2: Write exact GitHub Release install steps**

For self-signed release channel:

```text
1. Download CmdPalDockPlus.cer and CmdPalDockPlus-X.Y.Z.msixbundle from the same release.
2. Double-click/import the certificate for Current User.
3. Choose Trusted People as the certificate store.
4. Verify certificate subject/fingerprint against the value printed in the release notes.
5. Install the .msixbundle.
6. Open PowerToys > Command Palette and reload extensions if needed.
7. Open Command Palette Dock settings and add CmdPal Dock Plus items/bands.
```

Include PowerShell alternative using `Import-Certificate` and `Add-AppxPackage` with exact filenames.

- [ ] **Step 3: Document first shortcut end-to-end**

Show selecting an app, probing capabilities, Grouped/Separate/Smart choices, selecting Title/Subtitle fields, template syntax and primary/context actions.

- [ ] **Step 4: Document per-window separation examples**

Include VS Code workspace Smart rules and explain stable fallback if adapter fields are absent.

- [ ] **Step 5: Document system area and optional native features**

Clearly separate supported system controls from Explorer tray hook, taskbar capture injection and PowerToys hover compatibility patch. Each optional component has enable/disable/uninstall steps and security implications.

- [ ] **Step 6: Document upgrade/uninstall/reset**

Upgrade installs newer signed bundle over existing package. Uninstall through Windows Installed apps or `Remove-AppxPackage`; config path/reset action is exact from implementation. State whether user config survives uninstall and how to delete it manually.

- [ ] **Step 7: Document troubleshooting by observable symptom**

At minimum: extension missing, certificate trust error, app tile not matching windows, adapter fields missing, preview bridge disconnected, Explorer tray bridge unavailable, native taskbar capture access denied, ARM64/x64 architecture mismatch.

- [ ] **Step 8: Cross-check every settings label and commit**

Open the built settings UI and compare every switch/dropdown/text field against README/docs. Remove documentation for any non-shipped setting.

```bash
git add README.md docs/configuration docs/troubleshooting.md
git commit -m "docs: complete installation and configuration manual"
```

---

### Task 8: Add release verification matrix and first-release gate

**Files:**
- Create: `docs/testing/release-checklist.md`
- Create: `scripts/Verify-ReleaseAsset.ps1`
- Modify: `.github/workflows/release.yml`

**Interfaces:**
- Produces objective release gate and post-download artifact verifier.

- [ ] **Step 1: Write manual matrix**

Required before first stable release:

```text
Windows 11 x64: required
Windows 11 ARM64: required before claiming ARM64 runtime verified
Multi-monitor: required
Dock auto-hide: required
Dock compact/default: required
Explorer restart with tray enabled: required
Tray disabled: required
Taskbar capture disabled: required
Taskbar fixture capture x64: required
x86 capture on x64 Windows: required
PowerToys hover compatibility build: required
Stock PowerToys without hover patch: required
```

- [ ] **Step 2: Implement downloaded-release verifier**

`Verify-ReleaseAsset.ps1` validates SHA256SUMS, Authenticode/MSIX signature and expected package identity/publisher against downloaded assets.

- [ ] **Step 3: Add post-build install smoke test where runner permits**

Create a fresh Windows user-context test step that imports only the public test/release cert into `CurrentUser\TrustedPeople`, runs `Add-AppxPackage`, verifies package registration, then removes package and certificate. If hosted runner policy blocks interactive CmdPal discovery, package registration remains automated and full CmdPal discovery stays in manual matrix.

- [ ] **Step 4: Tag-release dry run without publication**

Add `workflow_dispatch` input `publish=false`; run entire release workflow through staging/signing/checksums while skipping `gh release create`. This is the required rehearsal before first tag.

- [ ] **Step 5: Commit**

```bash
git add docs/testing/release-checklist.md scripts/Verify-ReleaseAsset.ps1 .github/workflows/release.yml
git commit -m "test: gate releases on signed reproducible artifacts"
```

---

## Release acceptance check

```text
[ ] PR/main CI builds and tests managed code.
[ ] Native taskbar hooks compile x86/x64/ARM64.
[ ] Tray hook compiles x64/ARM64.
[ ] Release tag rebuilds/tests everything from exact tag.
[ ] x64 and ARM64 MSIX packages are produced and bundled.
[ ] Signing happens only in Actions using repository/environment secrets.
[ ] Public certificate is released; private PFX is never an artifact.
[ ] Signatures and SHA256 checksums are verified before publication.
[ ] GitHub Release contains installable bundle plus optional native/patch assets.
[ ] README explains exact certificate/MSIX install, Dock setup, shortcut setup, dynamic fields, Smart rules, per-window separation, tray/system controls, optional capture, upgrade, uninstall and troubleshooting.
[ ] Manual release matrix is complete before claiming first stable release.
```
