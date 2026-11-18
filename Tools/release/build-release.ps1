<#
Builds Builds\MusicInYourHands-release.apk, signed with the release keystore, ready to upload to a release
channel (the competition's "Competition" channel) on the Meta Horizon Developer Dashboard.

It asks for the keystore password, hands it to the Unity batch build through environment variables of this
process only, and clears them afterwards. The project's settings files don't change, and nothing about the
keystore goes into git. Close any Unity editor that has this project open first.

  powershell -ExecutionPolicy Bypass -File Tools\release\build-release.ps1
#>
param(
    [string]$Keystore = (Join-Path $env:USERPROFILE "keystores\musicinyourhands-release.keystore"),
    [string]$Alias = "musicinyourhands",
    [int]$VersionCode = 0
)
$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$version = (Select-String -Path (Join-Path $root "ProjectSettings\ProjectVersion.txt") -Pattern "m_EditorVersion: (.+)").Matches[0].Groups[1].Value.Trim()
$editor = "C:\Program Files\Unity\Hub\Editor\$version\Editor"
$unity = Join-Path $editor "Unity.exe"
$android = Join-Path $editor "Data\PlaybackEngines\AndroidPlayer"
$buildTools = Get-ChildItem (Join-Path $android "SDK\build-tools") -Directory | Sort-Object Name | Select-Object -Last 1
$apk = Join-Path $root "Builds\MusicInYourHands-release.apk"
$log = Join-Path $root "Builds\release-build.log"

if (-not (Test-Path $unity)) { throw "Unity $version not found at $unity." }
if (-not (Test-Path $Keystore)) { throw "No keystore at $Keystore. Make one with Tools\release\new-keystore.ps1." }

# A batch build can't open a project that an editor (or another batch run) has open.
$lock = Join-Path $root "Temp\UnityLockfile"
if (Test-Path $lock) {
    try { [System.IO.File]::Open($lock, 'Open', 'ReadWrite', 'None').Close() }
    catch { throw "Unity has $root open. Close that editor (or wait for its batch run) and try again." }
}

$secure = Read-Host "Keystore password for $(Split-Path $Keystore -Leaf)" -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $env:FTM_KEYSTORE = $Keystore
    $env:FTM_KEY_ALIAS = $Alias
    $env:FTM_KEYSTORE_PASS = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    if ($VersionCode -gt 0) { $env:FTM_VERSION_CODE = "$VersionCode" }
    if (Test-Path $apk) { Remove-Item $apk }

    Write-Host "Building (3 to 10 minutes). Log: $log" -ForegroundColor Cyan
    $p = Start-Process -FilePath $unity -Wait -PassThru -NoNewWindow -ArgumentList @(
        "-batchmode", "-projectPath", "`"$root`"", "-buildTarget", "Android",
        "-executeMethod", "MusicInYourHands.EditorTools.QuestBuild.BuildReleaseApk", "-logFile", "`"$log`"")
    $code = $p.ExitCode
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    foreach ($name in "FTM_KEYSTORE", "FTM_KEY_ALIAS", "FTM_KEYSTORE_PASS", "FTM_VERSION_CODE") {
        Remove-Item "Env:$name" -ErrorAction SilentlyContinue
    }
}

# Unity writes the passwords in plain text into its Gradle project (Library\Bee\...\launcher\build.gradle).
# Library is never committed, but don't leave them lying around: blank them out, whether or not it built.
Get-ChildItem (Join-Path $root "Library\Bee\Android") -Recurse -Filter build.gradle -ErrorAction SilentlyContinue |
    ForEach-Object {
        try {
            $text = [System.IO.File]::ReadAllText($_.FullName)
            $clean = [regex]::Replace($text, "(storePassword|keyPassword)\s+'(?:[^'\\]|\\.)*'", "`$1 ''")
            if ($clean -ne $text) { [System.IO.File]::WriteAllText($_.FullName, $clean) }
        } catch { }
    }

Select-String -Path $log -Pattern "\[Build\]" | ForEach-Object { $_.Line }
if ($code -ne 0 -or -not (Test-Path $apk)) {
    Write-Host "The build failed (exit $code). See $log" -ForegroundColor Red
    exit 1
}

$env:JAVA_HOME = Join-Path $android "OpenJDK"
$ErrorActionPreference = "Continue"   # apksigner warns on stderr; the checks below just print what it found
Write-Host ""
& (Join-Path $buildTools.FullName "apksigner.bat") verify --verbose --print-certs $apk |
    Select-String "Verified using|Signer #1 certificate DN|Signer #1 certificate SHA-256"
& (Join-Path $buildTools.FullName "aapt2.exe") dump badging $apk | Select-String "^package:"
Write-Host ("APK: {0} ({1:N0} MB)" -f $apk, ((Get-Item $apk).Length / 1MB)) -ForegroundColor Green
