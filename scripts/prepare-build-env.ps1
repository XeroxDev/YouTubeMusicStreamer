[xml]$xml = Get-Content "YouTubeMusicStreamer/YouTubeMusicStreamer.csproj"
$pg = $xml.Project.PropertyGroup | Where-Object { $_.VersionMajor -and $_.VersionMinor -and $_.VersionPatch } | Select-Object -First 1
$base = "$( $pg.VersionMajor ).$( $pg.VersionMinor ).$( $pg.VersionPatch )"
"VERSION=$base" | Out-File -FilePath $Env:GITHUB_ENV -Encoding utf8

Set-Content -Path .env -Value "GIT_COMMIT=$Env:GITHUB_SHA"
Add-Content  -Path .env -Value "TWITCH_CLIENT_ID=$Env:TWITCH_CLIENT_ID"

