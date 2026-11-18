<#
Creates the release keystore that signs Music In Your Hands for the Meta Horizon Store.

Run it once, ever. Every later upload of the app must be signed with this same key, so back the file up
(and keep its password) somewhere safe: losing it means the app can't be updated. Never commit it.

keytool asks for the password itself, twice. Use at least 6 characters.

  powershell -ExecutionPolicy Bypass -File Tools\release\new-keystore.ps1
#>
param(
    [string]$Path = (Join-Path $env:USERPROFILE "keystores\musicinyourhands-release.keystore"),
    [string]$Alias = "musicinyourhands",
    [string]$Organisation = "MR Folks"
)
$ErrorActionPreference = "Stop"

if (Test-Path $Path) {
    Write-Host "A keystore already exists at $Path. Not touching it." -ForegroundColor Yellow
    exit 1
}

$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$version = (Select-String -Path (Join-Path $root "ProjectSettings\ProjectVersion.txt") -Pattern "m_EditorVersion: (.+)").Matches[0].Groups[1].Value.Trim()
$jdk = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin"
$keytool = Join-Path $jdk "keytool.exe"
if (-not (Test-Path $keytool)) { throw "keytool not found at $keytool (Unity $version with Android support)." }

New-Item -ItemType Directory -Force (Split-Path $Path) | Out-Null
Write-Host "Creating $Path (alias $Alias). keytool will ask for a password twice." -ForegroundColor Cyan
$ErrorActionPreference = "Continue"   # keytool reports progress on stderr; judge it by its exit code
& $keytool -genkeypair -v -keystore $Path -alias $Alias -keyalg RSA -keysize 2048 -validity 10000 `
    -dname "CN=$Organisation, O=$Organisation"
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $Path)) { throw "keytool failed; no keystore was written." }

Write-Host ""
Write-Host "Done: $Path" -ForegroundColor Green
Write-Host "Back this file up now, with its password. Every future upload needs the same key."
Write-Host "Next: powershell -ExecutionPolicy Bypass -File Tools\release\build-release.ps1"
