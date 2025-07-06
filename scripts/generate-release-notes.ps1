if (-not (Test-Path CHANGELOG.md))
{
    Write-Error "CHANGELOG.md file not found."
    exit 1
}

$all = Get-Content CHANGELOG.md

$versionPattern = [regex]::Escape($env:VPK_PACK_VERSION)
$possiblePatterns = @("##\s*\[?v?$versionPattern\]?[^\n]*")

$start = $null
foreach ($pattern in $possiblePatterns)
{
    $match = $all | Select-String -Pattern $pattern | Select-Object -First 1
    if ($match)
    {
        $start = $match.LineNumber
        break
    }
}

if (-not $start)
{
    Write-Error "Version $env:VPK_PACK_VERSION not found in CHANGELOG.md"
    exit 1
}

$rest = $all[($start - 1)..($all.Length - 1)]
$next = ($rest | Select-String -Pattern "^##\s" | Select-Object -Skip 1 -First 1).LineNumber

if ($next)
{
    $notes = $rest[0..($next - 2)]
}
else
{
    # until end of file
    $notes = $rest
}

# Clean extra blank lines (at most 1)
$cleaned = @()
$blankCount = 0
foreach ($line in $notes)
{
    if ($line.Trim() -eq "")
    {
        $blankCount += 1
        if ($blankCount -le 1)
        {
            $cleaned += ""
        }
    }
    else
    {
        $blankCount = 0
        $cleaned += $line.TrimEnd()
    }
}

$notesPath = $Env:RELEASE_NOTES_FILE
$cleaned | Set-Content -Path $notesPath -Encoding utf8
"NOTES=$notesPath" | Out-File -FilePath $Env:GITHUB_ENV -Encoding utf8
