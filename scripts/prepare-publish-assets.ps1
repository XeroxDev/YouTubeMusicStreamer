[CmdletBinding()]
param(
    [switch]$DisableNetworkFallback
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $PSCommandPath
$generatorProjectPath = Join-Path $scriptRoot "ThirdPartyLicenseGenerator\ThirdPartyLicenseGenerator.csproj"
$licenseScriptPath = Join-Path $scriptRoot "generate-third-party-licenses.ps1"
$networkArg = if ($DisableNetworkFallback.IsPresent) { "-DisableNetworkFallback" } else { $null }

dotnet build $generatorProjectPath

if ($networkArg) {
    & $licenseScriptPath -NoBuild -DisableNetworkFallback
}
else {
    & $licenseScriptPath -NoBuild
}
