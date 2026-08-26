param(
    [Parameter(Mandatory=$true)][string]$InputDirectory,
    [Parameter(Mandatory=$true)][string]$OutputPath
)

$packages = @(Get-ChildItem $InputDirectory -File | Where-Object Extension -eq '.msix')
if ($packages.Count -ne 2) {
    throw "Expected exactly two architecture MSIX files in '$InputDirectory', found $($packages.Count)."
}

$makeAppx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\makeappx\.exe$' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if (-not $makeAppx) { throw 'Windows SDK makeappx.exe was not found.' }

$parent = Split-Path $OutputPath -Parent
if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
& $makeAppx.FullName bundle /d $InputDirectory /p $OutputPath /o
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $OutputPath)) {
    throw "makeappx bundle failed with exit code $LASTEXITCODE"
}
Write-Host "Created $OutputPath"
