param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'
$resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
$expectedExtensionName = 'com.microsoft.commandpalette'
$expectedExecutable = 'CmdPalDockPlus.Extension.exe'

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackage)
try {
    $entries = @($archive.Entries)
    $manifestEntry = $entries | Where-Object { $_.FullName -ieq 'AppxManifest.xml' } | Select-Object -First 1
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

    $ns = [System.Xml.XmlNamespaceManager]::new($manifest.NameTable)
    $ns.AddNamespace('appx', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $ns.AddNamespace('uap3', 'http://schemas.microsoft.com/appx/manifest/uap/windows10/3')
    $ns.AddNamespace('com', 'http://schemas.microsoft.com/appx/manifest/com/windows10')

    $appExtension = $manifest.SelectSingleNode("/appx:Package/appx:Applications/appx:Application/appx:Extensions/uap3:Extension[@Category='windows.appExtension']/uap3:AppExtension[@Name='$expectedExtensionName']", $ns)
    if (-not $appExtension) {
        throw "CmdPal AppExtension '$expectedExtensionName' is missing from packaged AppxManifest.xml."
    }

    # The custom CmdPalProvider property elements are unprefixed in the official
    # template, so they inherit the package's default foundation namespace.
    $createInstance = $appExtension.SelectSingleNode('uap3:Properties/appx:CmdPalProvider/appx:Activation/appx:CreateInstance', $ns)
    if (-not $createInstance) {
        throw 'CmdPalProvider/Activation/CreateInstance is missing from packaged AppxManifest.xml.'
    }
    $classId = [string]$createInstance.GetAttribute('ClassId')
    if ([string]::IsNullOrWhiteSpace($classId)) {
        throw 'CmdPal CreateInstance ClassId is empty.'
    }

    $commands = $appExtension.SelectSingleNode('uap3:Properties/appx:CmdPalProvider/appx:SupportedInterfaces/appx:Commands', $ns)
    if (-not $commands) {
        throw 'CmdPal SupportedInterfaces/Commands is missing.'
    }

    $comClass = $manifest.SelectSingleNode("/appx:Package/appx:Applications/appx:Application/appx:Extensions/com:Extension[@Category='windows.comServer']/com:ComServer/com:ExeServer/com:Class[@Id='$classId']", $ns)
    if (-not $comClass) {
        throw "No windows.comServer class matches CmdPal ClassId '$classId'."
    }

    $exeServer = $comClass.ParentNode
    $registeredExecutable = [string]$exeServer.GetAttribute('Executable')
    if (-not [string]::Equals($registeredExecutable, $expectedExecutable, [StringComparison]::OrdinalIgnoreCase)) {
        throw "CmdPal COM server executable mismatch. Expected '$expectedExecutable', actual '$registeredExecutable'."
    }

    foreach ($requiredFile in @($expectedExecutable, 'hostfxr.dll', 'hostpolicy.dll', 'coreclr.dll')) {
        if (-not ($entries | Where-Object { $_.FullName -ieq $requiredFile } | Select-Object -First 1)) {
            throw "Required self-contained extension payload '$requiredFile' is missing from '$resolvedPackage'."
        }
    }

    Write-Host "CmdPal package contract is valid: AppExtension=$expectedExtensionName; ClassId=$classId; EXE=$registeredExecutable; self-contained runtime present."
}
finally {
    $archive.Dispose()
}
