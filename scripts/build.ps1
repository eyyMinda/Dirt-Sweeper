# npm run build: release build of the current code.
#   releases\<version>\DirtSweeper-<version>.exe + SHA256SUMS.txt   (local, ignored by git)
#   dist\DirtSweeper.exe                                             (your everyday copy; settings live next to it)
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
New-Item -ItemType Directory -Force $rel | Out-Null
$name = "DirtSweeper-$version.exe"
Copy-Item $exe (Join-Path $rel $name) -Force
$hash = (Get-FileHash (Join-Path $rel $name) -Algorithm SHA256).Hash.ToLower()
Set-Content (Join-Path $rel "SHA256SUMS.txt") "$hash  $name" -Encoding ascii
Write-Host "release build $version -> $rel" -ForegroundColor Green

if (-not $NoDist) {
    $dist = Join-Path $Root "dist"
    $distExe = Join-Path $dist "DirtSweeper.exe"
    New-Item -ItemType Directory -Force $dist | Out-Null
    $wasRunning = @(Stop-DirtSweeper $distExe).Count -gt 0
    Copy-Item $exe $distExe -Force
    Write-Host "updated $distExe" -ForegroundColor Green
    if ($wasRunning) { Start-Process $distExe -WorkingDirectory $dist }
}
