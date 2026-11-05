# npm start: runs the latest build (dist\DirtSweeper.exe), closing a dev copy if one is running.
# Builds it first if dist\ has no exe yet.

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")

$dist = Join-Path $Root "dist"
$exe = Join-Path $dist "DirtSweeper.exe"
if (-not (Test-Path $exe)) { & (Join-Path $PSScriptRoot "build.ps1") }
Stop-DirtSweeper | Out-Null
Start-Process $exe -WorkingDirectory $dist
Write-Host "started $exe" -ForegroundColor Green
