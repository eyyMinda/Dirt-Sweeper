# npm run release: tags the current version and pushes the tag; GitHub Actions (release.yml) then builds the exe
# and publishes the GitHub Release with SHA256SUMS.txt and the CHANGELOG.md section as notes.
# Checks first: on main (or release/*), clean tree, in sync with origin, changelog section present, tests pass.

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")
Set-Location $Root

$version = Get-Version
$tag = "v$version"
$branch = (git rev-parse --abbrev-ref HEAD).Trim()

if ($branch -ne "main" -and $branch -notlike "release/*") { throw "Release from main or a release/* branch (on $branch). Merge the PR first." }
if (git status --porcelain) { throw "Working tree isn't clean; commit first (/commit)." }
if (git tag --list $tag) { throw "$tag already exists. Bump <Version> with /commit first." }
if (-not (Select-String -Path CHANGELOG.md -Pattern "^## \[$([regex]::Escape($version))\]" -Quiet)) { throw "CHANGELOG.md has no section for $version." }

git fetch origin $branch --quiet
if ((git rev-parse HEAD) -ne (git rev-parse "origin/$branch")) { throw "$branch isn't in sync with origin/$branch; push or pull first." }

dotnet test (Join-Path $Root "tests\DirtSweeper.Tests") -c Release -nologo | Out-Host
if ($LASTEXITCODE -ne 0) { throw "tests failed" }

$answer = Read-Host "Tag $tag on $branch and push it (publishes the GitHub release)? [y/N]"
if ($answer -notmatch '^(y|yes)$') { Write-Host "cancelled"; exit 0 }

git tag $tag
git push origin $tag
Write-Host "pushed $tag; follow the release at https://github.com/eyyMinda/Dirt-Sweeper/actions" -ForegroundColor Green
