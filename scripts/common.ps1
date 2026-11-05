# Shared by the scripts in this folder (dot-sourced).

$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root "src\DirtSweeper\DirtSweeper.csproj"
$SettingsName = "DirtSweeper.settings.json"

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
