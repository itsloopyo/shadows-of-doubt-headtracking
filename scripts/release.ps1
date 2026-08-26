#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Automated release workflow for Shadows of Doubt Head Tracking mod.

.DESCRIPTION
    This script:
    1. Updates version in csproj and plugin source
    2. Builds and updates prebuilt DLLs
    3. Commits all changes
    4. Creates and pushes a git tag to trigger CI release

.PARAMETER Version
    The version to release (e.g., "1.0.0", "1.2.3")

.EXAMPLE
    pixi run release 1.0.0

.NOTES
    Run via: pixi run release <version>
#>
param(
    [Parameter(Position=0)]
    [string]$Version = "",
    # Ship a release even when there are no user-facing commits since the
    # last tag (writes a maintenance changelog entry instead of aborting).
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$csprojPath = Join-Path $projectDir "src\ShadowsOfDoubtHeadTracking\ShadowsOfDoubtHeadTracking.csproj"

Import-Module (Join-Path $projectDir "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force

# Mirrors New-ChangelogFromCommits' insertion so a -Force maintenance entry
# lands in the same place with the same shape.
function Add-MaintenanceChangelogEntry {
    param([string]$Path, [string]$NewVersion)
    $date = Get-Date -Format 'yyyy-MM-dd'
    $entry = "## [$NewVersion] - $date`n`n### Changed`n`n- Maintenance release (no user-facing changes).`n`n"
    $changelog = Get-Content $Path -Raw
    if ($changelog -match '(?s)(# Changelog.*?)(## \[)') {
        $changelog = $changelog -replace '(?s)(# Changelog.*?\n\n)', "`$1$entry"
    } else {
        $changelog = $changelog -replace '(?s)(# Changelog.*?\n)', "`$1$entry"
    }
    $changelog = $changelog.TrimEnd() + "`n"
    Set-Content $Path $changelog -NoNewline
}

Write-Host "=== Shadows of Doubt Head Tracking Release ===" -ForegroundColor Cyan
Write-Host ""

$currentVersion = Get-CsprojVersion $csprojPath

# If no version provided, show current and exit
if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Host "Current version: " -NoNewline -ForegroundColor Yellow
    Write-Host $currentVersion -ForegroundColor White
    Write-Host ""
    Write-Host "Usage: " -NoNewline -ForegroundColor Yellow
    Write-Host "pixi run release <major|minor|patch|nightly|X.Y.Z>" -ForegroundColor White
    Write-Host ""
    Write-Host "Example: " -NoNewline -ForegroundColor Yellow
    Write-Host "pixi run release patch" -ForegroundColor White
    exit 0
}

if ($Version -eq 'nightly') {
    & (Join-Path $PSScriptRoot 'release-nightly.ps1')
    exit $LASTEXITCODE
}

# Resolve major/minor/patch into a concrete version (or accept literal X.Y.Z)
try {
    $Version = Resolve-ReleaseVersion -Argument $Version -CurrentVersion $currentVersion
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$tagName = "v$Version"

# Releases are semver-only; a prerelease suffix would produce a tag the
# release workflow and the launcher manifest cannot both agree on.
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "Error: Resolved version '$Version' is not a bare X.Y.Z semver" -ForegroundColor Red
    exit 1
}

# Check if we're on main branch
$currentBranch = git rev-parse --abbrev-ref HEAD
if ($currentBranch -ne "main") {
    Write-Host "Error: Must be on 'main' branch to release (currently on '$currentBranch')" -ForegroundColor Red
    exit 1
}

# Check for uncommitted changes (prebuilt/ is excluded since the release overwrites it)
$status = git status --porcelain -- ':!prebuilt/'
if ($status) {
    Write-Host "Error: Working directory has uncommitted changes" -ForegroundColor Red
    Write-Host $status -ForegroundColor Gray
    Write-Host "Please commit or stash changes before releasing" -ForegroundColor Yellow
    exit 1
}

# Check if tag already exists
$existingTag = git tag -l $tagName
if ($existingTag) {
    Write-Host "Error: Tag '$tagName' already exists" -ForegroundColor Red
    exit 1
}

Write-Host "Current version: $currentVersion" -ForegroundColor Gray
Write-Host "New version:     $Version" -ForegroundColor Green
Write-Host ""

# Step 1: generate CHANGELOG from commits since last tag. This is the gate
# that aborts when there are no user-facing commits, so run it BEFORE
# mutating any version files - a failure here then leaves a clean tree
# instead of stranding a half-applied version bump with no tag.
Write-Host "Generating CHANGELOG from commits..." -ForegroundColor Cyan
$changelogPath = Join-Path $projectDir "CHANGELOG.md"
$hasExistingTags = git tag -l 2>$null
if (-not $hasExistingTags) {
    # First release - write a basic changelog entry
    $date = Get-Date -Format 'yyyy-MM-dd'
    $firstEntry = "# Changelog`n`n## [$Version] - $date`n`nFirst release.`n"
    Set-Content $changelogPath $firstEntry
    Write-Host "  First release - wrote initial CHANGELOG entry" -ForegroundColor Gray
} else {
    try {
        $changelogArgs = @{
            ChangelogPath = $changelogPath
            Version = $Version
            ArtifactPaths = @(
                "src/ShadowsOfDoubtHeadTracking/",
                "cameraunlock-core",
                "scripts/install.cmd",
                "scripts/uninstall.cmd",
                "prebuilt/"
            )
        }
        New-ChangelogFromCommits @changelogArgs
    } catch {
        if (-not $Force) {
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "No user-facing changes to release. Re-run with -Force for a maintenance release." -ForegroundColor Yellow
            exit 1
        }
        Write-Host "No user-facing commits since last tag - writing maintenance entry (-Force)." -ForegroundColor Yellow
        Add-MaintenanceChangelogEntry -Path $changelogPath -NewVersion $Version
    }
}

# Step 2: Update version in csproj
Write-Host "Updating version to $Version..." -ForegroundColor Cyan
Set-CsprojVersion $csprojPath $Version

# Step 3: Update version in plugin source
$pluginPath = Join-Path $projectDir "src\ShadowsOfDoubtHeadTracking\Core\HeadTrackingPlugin.cs"
$pluginContent = Get-Content $pluginPath -Raw
$pluginContent = $pluginContent -replace 'PluginVersion = "[^"]+"', "PluginVersion = `"$Version`""
$pluginContent | Set-Content $pluginPath -NoNewline
Write-Host "  Updated HeadTrackingPlugin.cs" -ForegroundColor Gray

# Step 2b: Update MOD_VERSION in install.cmd CONFIG BLOCK so the state file
# written at install time records the correct version. Preserve CRLF line
# endings (.cmd files require CRLF on Windows).
$installCmdPath = Join-Path $projectDir "scripts\install.cmd"
if (Test-Path $installCmdPath) {
    $installCmdBytes = [System.IO.File]::ReadAllBytes($installCmdPath)
    $installCmdContent = [System.Text.Encoding]::UTF8.GetString($installCmdBytes)
    $updatedInstallCmd = $installCmdContent -replace 'set "MOD_VERSION=[^"]+"', "set `"MOD_VERSION=$Version`""
    if ($updatedInstallCmd -ne $installCmdContent) {
        [System.IO.File]::WriteAllText($installCmdPath, $updatedInstallCmd, [System.Text.UTF8Encoding]::new($false))
        Write-Host "  Updated install.cmd MOD_VERSION" -ForegroundColor Gray
    }
}

# Step 2c: Update mod_info.version in the launcher manifest so
# launcher-manifest.json never drifts from the released package version.
$manifestPath = Join-Path $projectDir "launcher-manifest.json"
if (Test-Path $manifestPath) {
    $manifestContent = Get-Content $manifestPath -Raw
    # Only mod_info.version is semver-shaped; loader/strategy carry no semver.
    $updatedManifest = $manifestContent -replace '("version":\s*")\d+\.\d+\.\d+(")', "`${1}$Version`${2}"
    if ($updatedManifest -ne $manifestContent) {
        $updatedManifest | Set-Content $manifestPath -NoNewline
        Write-Host "  Updated launcher-manifest.json version" -ForegroundColor Gray
    }
}

# Step 2d: Keep the pixi workspace version in step with the canonical csproj
# version so `pixi task list` and the release never disagree.
$pixiTomlPath = Join-Path $projectDir "pixi.toml"
$pixiContent = Get-Content $pixiTomlPath -Raw
$updatedPixi = $pixiContent -replace '(?m)^version = "\d+\.\d+\.\d+"', "version = `"$Version`""
if ($updatedPixi -ne $pixiContent) {
    $updatedPixi | Set-Content $pixiTomlPath -NoNewline
    Write-Host "  Updated pixi.toml version" -ForegroundColor Gray
}

# Step 4: Build and update prebuilt DLLs
# `pixi run build`, never a bare `dotnet build`: the pixi chain is what runs
# restore -> setup-libs, so a release cut from a clean checkout compiles
# against the same references CI uses instead of whatever is stale in libs/.
Write-Host "Building release..." -ForegroundColor Cyan
Push-Location $projectDir
pixi run build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Pop-Location
    exit 1
}

$prebuiltDir = Join-Path $projectDir "prebuilt"
if (-not (Test-Path $prebuiltDir)) {
    New-Item -ItemType Directory -Path $prebuiltDir -Force | Out-Null
}
Copy-Item "src/ShadowsOfDoubtHeadTracking/bin/Release/net6.0/*.dll" $prebuiltDir -Force
Write-Host "  Updated prebuilt DLLs" -ForegroundColor Gray
Pop-Location

# Step 5: Commit
Write-Host "Committing changes..." -ForegroundColor Cyan
git add $csprojPath
git add $pluginPath
git add $installCmdPath
git add $manifestPath
git add $pixiTomlPath
git add "$projectDir/prebuilt"
git add $changelogPath
git commit -m "Release v$Version"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Commit failed!" -ForegroundColor Red
    exit 1
}

# Step 6: Create tag
Write-Host "Creating tag $tagName..." -ForegroundColor Cyan
git tag -a $tagName -m "Release $tagName"

# Step 7: Push
Write-Host "Pushing to GitHub..." -ForegroundColor Cyan
git push origin main
git push origin $tagName

Write-Host ""
Write-Host "Release $tagName initiated!" -ForegroundColor Green
Write-Host ""
Write-Host "The GitHub Actions release workflow will now:" -ForegroundColor Yellow
Write-Host "  - Package the prebuilt DLLs" -ForegroundColor White
Write-Host "  - Create GitHub release with artifacts" -ForegroundColor White
Write-Host ""
Write-Host "Watch progress at:" -ForegroundColor Yellow
Write-Host "  https://github.com/itsloopyo/shadows-of-doubt-headtracking/actions" -ForegroundColor Cyan
