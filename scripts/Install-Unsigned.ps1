param(
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Administrator)) {
    $scriptPath = $MyInvocation.MyCommand.Path
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$scriptPath`"")
    if ($PackagePath) { $arguments += @('-PackagePath', "`"$PackagePath`"") }
    Start-Process powershell.exe -Verb RunAs -ArgumentList $arguments
    exit
}

if (-not $PackagePath) {
    $directory = Split-Path $MyInvocation.MyCommand.Path -Parent
    $candidate = @(Get-ChildItem $directory -File -Filter 'CmdPalDockPlus-*.msixbundle' | Sort-Object Name -Descending)
    if ($candidate.Count -ne 1) {
        throw "Expected exactly one CmdPalDockPlus .msixbundle next to this script; found $($candidate.Count)."
    }
    $PackagePath = $candidate[0].FullName
}

$resolved = (Resolve-Path $PackagePath).Path
Write-Host "Installing unsigned CmdPal Dock Plus package: $resolved"
Add-AppxPackage -Path $resolved -AllowUnsigned
Write-Host 'Installation completed. Restart PowerToys Command Palette if the extension is not detected immediately.'
