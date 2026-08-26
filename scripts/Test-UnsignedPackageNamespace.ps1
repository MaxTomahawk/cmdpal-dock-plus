param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'
$requiredOid = 'OID.2.25.311729368913984317654407730594956997722=1'
$resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackage)
try {
    $manifestEntry = $archive.Entries | Where-Object { $_.FullName -ieq 'AppxManifest.xml' } | Select-Object -First 1
    if (-not $manifestEntry) {
        throw "AppxManifest.xml was not found in package '$resolvedPackage'."
    }

    $reader = [System.IO.StreamReader]::new($manifestEntry.Open())
    try {
        [xml]$manifest = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }

    $namespaceManager = [System.Xml.XmlNamespaceManager]::new($manifest.NameTable)
    $namespaceManager.AddNamespace('appx', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $identity = $manifest.SelectSingleNode('/appx:Package/appx:Identity', $namespaceManager)
    if (-not $identity) {
        throw "Package Identity was not found in AppxManifest.xml from '$resolvedPackage'."
    }

    $publisher = [string]$identity.GetAttribute('Publisher')
    if ([string]::IsNullOrWhiteSpace($publisher)) {
        throw "Package publisher is empty in '$resolvedPackage'."
    }

    $publisherParts = @($publisher -split ',' | ForEach-Object { $_.Trim() })
    if ($publisherParts -notcontains $requiredOid) {
        throw "Unsigned MSIX publisher is outside the Windows unsigned namespace. Expected '$requiredOid' in Publisher, actual Publisher='$publisher'."
    }

    Write-Host "Unsigned MSIX publisher namespace is valid: $publisher"
}
finally {
    $archive.Dispose()
}
