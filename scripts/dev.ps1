# npm run dev: builds the current code into bin\dev and runs it.
#   npm run dev               build + run (your real settings are copied in the first time)
#   npm run dev -- -Fresh     start the dev copy from default settings
#   npm run dev -- -NoRun     only build
# Dirt Sweeper is single-instance, so a running copy (dev or dist) is closed before launching; -NoRun leaves dist alone.

param([switch]$Fresh, [switch]$NoRun)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")

$out = Join-Path $Root "bin\dev"
$exe = Join-Path $out "DirtSweeper.exe"
Stop-DirtSweeper $exe | Out-Null   # the old dev exe is locked while it runs

dotnet build $Project -c Debug -nologo -v q -o $out | Out-Host
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$devSettings = Join-Path $out $SettingsName
$realSettings = Join-Path $Root "dist\$SettingsName"
if ($Fresh -and (Test-Path $devSettings)) { Remove-Item $devSettings }
elseif (-not (Test-Path $devSettings) -and (Test-Path $realSettings)) { Copy-Item $realSettings $devSettings }

Write-Host "dev build $(Get-Version) -> $out" -ForegroundColor Green
if (-not $NoRun) {
    Stop-DirtSweeper | Out-Null    # single-instance: a running dist copy would swallow the launch
    Start-Process $exe -WorkingDirectory $out
}
