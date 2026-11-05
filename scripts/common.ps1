# Shared by the scripts in this folder (dot-sourced).

$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root "src\DirtSweeper\DirtSweeper.csproj"
# the real settings, used by the installed/dist copy (dev builds use bin\dev\settings.json)
$RealSettings = Join-Path $env:LOCALAPPDATA "DirtSweeper\settings.json"

# Release files in $OutDir: DirtSweeper-<v>.exe, DirtSweeper-<v>.zip (exe + README.txt + LICENSE.txt, for Nexus Mods
# and other sites that prefer archives) and SHA256SUMS.txt covering both. Used by build.ps1 and release.yml.
function New-ReleaseFiles([string]$Exe, [string]$Version, [string]$OutDir) {
    New-Item -ItemType Directory -Force $OutDir | Out-Null
    $exeName = "DirtSweeper-$Version.exe"
    $zipName = "DirtSweeper-$Version.zip"
    Copy-Item $Exe (Join-Path $OutDir $exeName) -Force

    $stage = Join-Path ([IO.Path]::GetTempPath()) "DirtSweeper-package-$Version"
    if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
    New-Item -ItemType Directory $stage | Out-Null
    Copy-Item $Exe (Join-Path $stage "DirtSweeper.exe")
    Copy-Item (Join-Path $Root "LICENSE") (Join-Path $stage "LICENSE.txt")
    $readme = (Get-Content (Join-Path $PSScriptRoot "package-readme.txt") -Raw).Replace("{version}", $Version)
    Set-Content (Join-Path $stage "README.txt") $readme -Encoding utf8
    $zip = Join-Path $OutDir $zipName
    if (Test-Path $zip) { Remove-Item $zip }
    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip
    Remove-Item -Recurse -Force $stage

    # one line per file; the in-app updater looks up the exe's line by name
    $sums = foreach ($f in $exeName, $zipName) { (Get-FileHash (Join-Path $OutDir $f) -Algorithm SHA256).Hash.ToLower() + "  " + $f }
    Set-Content (Join-Path $OutDir "SHA256SUMS.txt") $sums -Encoding ascii
}

# <Version> in the csproj is the single source of truth for the app version.
function Get-Version {
    $v = ([xml](Get-Content $Project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if (-not $v) { throw "No <Version> in $Project" }
    return $v
}

# Closes running Dirt Sweeper copies: all of them, or only the one at -Path. Returns the exe paths that were running.
function Stop-DirtSweeper([string]$Path) {
    $running = @(Get-Process DirtSweeper -ErrorAction SilentlyContinue |
        Where-Object { -not $Path -or $(try { $_.Path -ieq $Path } catch { $false }) })
    $paths = @($running | ForEach-Object { try { $_.Path } catch { $null } } | Where-Object { $_ })
    if ($running.Count -gt 0) {
        $running | Stop-Process -Force
        $running | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
        Write-Host "closed running Dirt Sweeper ($($paths -join ', '))" -ForegroundColor DarkGray
    }
    return $paths
}
