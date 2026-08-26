param(
    [Parameter(Mandatory=$true)][ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$Version,
    [string]$Manifest = 'src/CmdPalDockPlus.Extension/Package.appxmanifest'
)

[xml]$xml = Get-Content $Manifest -Raw
$identity = $xml.Package.Identity
if (-not $identity) { throw "Package Identity missing from $Manifest" }
$identity.Version = $Version
$settings = [System.Xml.XmlWriterSettings]::new()
$settings.Indent = $true
$settings.Encoding = [System.Text.UTF8Encoding]::new($false)
$writer = [System.Xml.XmlWriter]::Create((Resolve-Path $Manifest), $settings)
try { $xml.Save($writer) } finally { $writer.Dispose() }

[xml]$verify = Get-Content $Manifest -Raw
if ($verify.Package.Identity.Version -ne $Version) { throw 'Package version update verification failed.' }
Write-Host "Package version set to $Version"
