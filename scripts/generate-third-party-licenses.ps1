[CmdletBinding()]
param(
    [string]$ProjectPath = "YouTubeMusicStreamer/YouTubeMusicStreamer.csproj",
    [string]$GeneratedRawDir = "YouTubeMusicStreamer/Resources/Raw/Generated",
    [switch]$NoBuild,
    [switch]$DisableNetworkFallback
)

$ErrorActionPreference = "Stop"

$projectFullPath = Resolve-Path $ProjectPath
$generatedRawFullPath = Join-Path (Get-Location) $GeneratedRawDir
$licenseOverridePath = Join-Path ([System.IO.Path]::GetDirectoryName($projectFullPath)) "license-override.json"
$scriptRoot = Split-Path -Parent $PSCommandPath
$generatorProjectPath = Join-Path $scriptRoot "ThirdPartyLicenseGenerator\ThirdPartyLicenseGenerator.csproj"
$allowNetworkFallbackValue = if ($DisableNetworkFallback.IsPresent) { "false" } else { "true" }
$noBuildArg = if ($NoBuild.IsPresent) { "--no-build" } else { "" }

dotnet restore $projectFullPath

dotnet run --project $generatorProjectPath $noBuildArg -- `
    --project $projectFullPath `
    --generatedRawDir $generatedRawFullPath `
    --override $licenseOverridePath `
    --networkFallback $allowNetworkFallbackValue
