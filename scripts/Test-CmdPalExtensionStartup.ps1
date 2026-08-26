param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [int]$StartupSeconds = 3
)

$ErrorActionPreference = 'Stop'
$resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
$extractDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("CmdPalDockPlus-startup-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $extractDirectory | Out-Null

try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($resolvedPackage, $extractDirectory)

    $exe = Join-Path $extractDirectory 'CmdPalDockPlus.Extension.exe'
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "CmdPal extension executable is missing from '$resolvedPackage'."
    }

    $process = Start-Process -FilePath $exe -ArgumentList '-RegisterProcessAsComServer' -WorkingDirectory $extractDirectory -PassThru
    try {
        Start-Sleep -Seconds $StartupSeconds
        if ($process.HasExited) {
            throw "CmdPal extension COM server exited during startup smoke test with code $($process.ExitCode)."
        }

        Write-Host "CmdPal extension startup smoke test passed: process $($process.Id) remained alive for $StartupSeconds seconds."
    }
    finally {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            $process.WaitForExit(5000) | Out-Null
        }
        $process.Dispose()
    }
}
finally {
    Remove-Item -LiteralPath $extractDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
