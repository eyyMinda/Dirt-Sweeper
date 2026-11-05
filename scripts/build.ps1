# npm run build: release build of the current code.
#   releases\<version>\DirtSweeper-<version>.exe                     (local, ignored by git)
#   releases\<version>\DirtSweeper-<version>.zip                     exe + README.txt + LICENSE.txt (Nexus Mods etc.)
#   releases\<version>\SHA256SUMS.txt                                both of the above
#   dist\DirtSweeper.exe                                             your everyday copy (settings: %LOCALAPPDATA%\DirtSweeper)
# The build is deterministic: the same commit gives a byte-identical exe here and in CI (checked for 0.1.2), so the exe
# matches the release's SHA256SUMS.txt. The zip's own hash differs per build (zips store file times).
#   npm run build -- -NoDist   leave dist\ alone
# If the dist copy was running it is restarted on the new build.

param([switch]$NoDist)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")

$version = Get-Version
$bin = Join-Path $Root "src\DirtSweeper\bin\out"
dotnet build $Project -c Release -nologo -v q -o $bin | Out-Host
if ($LASTEXITCODE -ne 0) { throw "build failed" }
$exe = Join-Path $bin "DirtSweeper.exe"

$rel = Join-Path $Root "releases\$version"
New-ReleaseFiles $exe $version $rel
Write-Host "release build $version -> $rel" -ForegroundColor Green
Get-ChildItem $rel | Format-Table Name, @{ Name = "KB"; Expression = { [math]::Round($_.Length / 1KB) } } -AutoSize | Out-Host

if (-not $NoDist) {
    $dist = Join-Path $Root "dist"
    $distExe = Join-Path $dist "DirtSweeper.exe"
    New-Item -ItemType Directory -Force $dist | Out-Null
    $wasRunning = @(Stop-DirtSweeper $distExe).Count -gt 0
    Copy-Item $exe $distExe -Force
    Write-Host "updated $distExe" -ForegroundColor Green
    if ($wasRunning) { Start-Process $distExe -WorkingDirectory $dist }
}
