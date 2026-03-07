$ErrorActionPreference = 'Stop'

function Fail([string]$message) {
    Write-Host $message -ForegroundColor Red
    exit 1
}

$repoRoot = (& git rev-parse --show-toplevel).Trim()
if ([string]::IsNullOrWhiteSpace($repoRoot)) {
    Fail "Commit blocked: could not resolve repository root."
}

$versionFile = Join-Path $repoRoot "top_speed_net/TopSpeed.Shared/Protocol/VersionInfo.cs"
$infoFile = Join-Path $repoRoot "info.json"

if (-not (Test-Path -LiteralPath $versionFile)) {
    Fail "Commit blocked: missing file '$versionFile'."
}

if (-not (Test-Path -LiteralPath $infoFile)) {
    Fail "Commit blocked: missing file '$infoFile'."
}

$source = Get-Content -LiteralPath $versionFile -Raw

function Get-ConstantValue([string]$name) {
    $pattern = "public\s+const\s+\w+\s+$name\s*=\s*(\d+)\s*;"
    $match = [System.Text.RegularExpressions.Regex]::Match($source, $pattern)
    if (-not $match.Success) {
        Fail "Commit blocked: could not read '$name' from ProtocolVersionInfo."
    }

    return [int]$match.Groups[1].Value
}

$year = Get-ConstantValue "CurrentYear"
$month = Get-ConstantValue "CurrentMonth"
$day = Get-ConstantValue "CurrentDay"
$revision = Get-ConstantValue "CurrentRevision"
$expectedVersion = "$year.$month.$day.$revision"

try {
    $info = Get-Content -LiteralPath $infoFile -Raw | ConvertFrom-Json
} catch {
    Fail "Commit blocked: info.json is not valid JSON. Details: $($_.Exception.Message)"
}

$actualVersion = $info.version
if ([string]::IsNullOrWhiteSpace($actualVersion)) {
    Fail "Commit blocked: info.json is missing the 'version' key."
}

if ($actualVersion -ne $expectedVersion) {
    $message = @"
Commit blocked: client version mismatch.
- ProtocolVersionInfo current version: $expectedVersion
- info.json version: $actualVersion

Fix: update info.json 'version' to '$expectedVersion' and commit again.
"@
    Fail $message
}

exit 0
