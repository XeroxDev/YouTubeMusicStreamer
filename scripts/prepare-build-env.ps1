[xml]$xml = Get-Content "YouTubeMusicStreamer/YouTubeMusicStreamer.csproj"
$pg = $xml.Project.PropertyGroup | Where-Object { $_.VersionMajor -and $_.VersionMinor -and $_.VersionPatch } | Select-Object -First 1
$base = "$( $pg.VersionMajor ).$( $pg.VersionMinor ).$( $pg.VersionPatch )"
$envPath = ".env"

function Add-GitHubEnvValue([string]$name, [string]$value) {
    if (-not $Env:GITHUB_ENV) {
        return
    }

    "$name=$value" | Out-File -FilePath $Env:GITHUB_ENV -Encoding utf8 -Append
}

function Get-ExistingEnvValue([string]$key) {
    if (-not (Test-Path $envPath)) {
        return $null
    }

    $line = Get-Content $envPath | Where-Object { $_ -match "^$key=" } | Select-Object -First 1
    if (-not $line) {
        return $null
    }

    return ($line -replace "^$key=", "")
}

$gitCommit = if ([string]::IsNullOrWhiteSpace($Env:GITHUB_SHA)) {
    $existingGitCommit = Get-ExistingEnvValue "GIT_COMMIT"
    if ([string]::IsNullOrWhiteSpace($existingGitCommit)) { "local" } else { $existingGitCommit }
} else {
    $Env:GITHUB_SHA
}

$twitchClientId = if ([string]::IsNullOrWhiteSpace($Env:TWITCH_CLIENT_ID)) {
    $existingTwitchClientId = Get-ExistingEnvValue "TWITCH_CLIENT_ID"
    if ([string]::IsNullOrWhiteSpace($existingTwitchClientId)) { "local-build" } else { $existingTwitchClientId }
} else {
    $Env:TWITCH_CLIENT_ID
}

Set-Content -Path $envPath -Value "GIT_COMMIT=$gitCommit"
Add-Content -Path $envPath -Value "TWITCH_CLIENT_ID=$twitchClientId"

if (-not $Env:GITHUB_ENV) {
    Write-Host "GITHUB_ENV is not set; skipping GitHub Actions env export."
    return
}

Add-GitHubEnvValue "VERSION" $base

$vpkVersion = if ([string]::IsNullOrWhiteSpace($Env:VERSION_SUFFIX)) {
    $base
} else {
    "$base$($Env:VERSION_SUFFIX)"
}

if ([string]::IsNullOrWhiteSpace($Env:OS) -or
    [string]::IsNullOrWhiteSpace($Env:ARCH) -or
    [string]::IsNullOrWhiteSpace($Env:APP_NAME) -or
    [string]::IsNullOrWhiteSpace($Env:DOTNET_VERSION)) {
    Write-Host "OS/ARCH/APP_NAME/DOTNET_VERSION env vars are incomplete; skipping derived VPK env export."
    return
}

$channel = "$($Env:OS)-$($Env:ARCH)"
$repository = if ([string]::IsNullOrWhiteSpace($Env:GITHUB_REPOSITORY)) { $null } else { "https://github.com/$($Env:GITHUB_REPOSITORY)" }
$targetCommitish = if ([string]::IsNullOrWhiteSpace($Env:GITHUB_SHA)) { $gitCommit } else { $Env:GITHUB_SHA }

$derivedValues = [ordered]@{
    VPK_CHANNEL = $channel
    VPK_RUNTIME = $channel
    VPK_PACK_DIR = "./publish/$channel"
    VPK_FRAMEWORK = "webview2,net$($Env:DOTNET_VERSION)-$($Env:ARCH)-desktop"
    VPK_ICON = "./$($Env:APP_NAME)/wwwroot/favicon.ico"
    VPK_SPLASH_IMAGE = "./$($Env:APP_NAME)/Resources/Splash/splash.png"
    VPK_PACK_VERSION = $vpkVersion
    VPK_RELEASE_NAME = "v$base"
    VPK_TAG = "v$base"
    VPK_TARGET_COMMITISH = $targetCommitish
    VPK_PACK_ID = $Env:APP_NAME
    VPK_MAIN_EXE = "$($Env:APP_NAME).exe"
}

if ($repository) {
    $derivedValues["VPK_REPO_URL"] = $repository
}

foreach ($entry in $derivedValues.GetEnumerator()) {
    Add-GitHubEnvValue $entry.Key $entry.Value
}

