param(
    [Parameter(Mandatory=$true)][string]$Tag
)

$match = [regex]::Match($Tag, '^v(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$')
if (-not $match.Success) {
    throw "Release tag must match vMAJOR.MINOR.PATCH; got '$Tag'."
}

$semver = "$($match.Groups['major'].Value).$($match.Groups['minor'].Value).$($match.Groups['patch'].Value)"
[pscustomobject]@{
    SemVer = $semver
    MsixVersion = "$semver.0"
}
