param(
    [Parameter(Mandatory)]
    [string]$PowerToysRoot
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$patch = Join-Path $repoRoot 'powertoys\patches\cmdpal-dock-hover.patch'
$pinnedCommit = (Get-Content (Join-Path $repoRoot 'powertoys\patches\upstream-commit.txt') -Raw).Trim()

if (-not (Test-Path (Join-Path $PowerToysRoot '.git'))) {
    throw "PowerToysRoot is not a Git checkout: $PowerToysRoot"
}

$currentCommit = (git -C $PowerToysRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to read PowerToys HEAD.'
}

if ($currentCommit -ne $pinnedCommit) {
    throw "PowerToys checkout is $currentCommit; CmdPal Dock Plus hover patch is pinned to $pinnedCommit."
}

git -C $PowerToysRoot apply --check $patch
if ($LASTEXITCODE -ne 0) {
    throw 'CmdPal Dock Plus hover patch no longer applies cleanly.'
}

Write-Host "Hover patch applies cleanly to PowerToys $pinnedCommit."
