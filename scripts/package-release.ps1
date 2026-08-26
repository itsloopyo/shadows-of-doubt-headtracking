#!/usr/bin/env pwsh
#Requires -Version 5.1
# Custom packaging for Shadows of Doubt Head Tracking.
# BepInEx 6 IL2CPP variant: vendor zip is BepInEx_UnityIL2CPP_x64.zip rather
# than the regular BepInEx_win_x64.zip the shared bundler assumes. Mirrors
# peak-headtracking's pattern (stages vendor/bepinex into the ZIP root).

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir

Import-Module (Join-Path $projectDir "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force

$csprojPath = Join-Path $projectDir "src\ShadowsOfDoubtHeadTracking\ShadowsOfDoubtHeadTracking.csproj"
$version = Get-CsprojVersion $csprojPath

$buildOutputDir = Join-Path $projectDir "src\ShadowsOfDoubtHeadTracking\bin\Release\net6.0"
$scriptsDir = Join-Path $projectDir "scripts"
$releaseDir = Join-Path $projectDir "release"

$modDlls = @("ShadowsOfDoubtHeadTracking.dll", "CameraUnlock.Core.dll", "CameraUnlock.Core.Unity.dll")

Write-Host "=== Shadows of Doubt Head Tracking - Package Release ===" -ForegroundColor Magenta
Write-Host ""
Write-Host "Version: $version" -ForegroundColor Cyan
Write-Host ""

foreach ($dll in $modDlls) {
    $dllPath = Join-Path $buildOutputDir $dll
    if (-not (Test-Path $dllPath)) {
        throw "Required DLL not found: $dllPath"
    }
}

foreach ($script in @("install.cmd", "uninstall.cmd")) {
    $scriptPath = Join-Path $scriptsDir $script
    if (-not (Test-Path $scriptPath)) {
        throw "Required script not found: $scriptPath"
    }
}

if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
}

# Vendoring is install-time source of truth; refresh manually with `pixi run update-deps`.
$vendorBepDir = Join-Path $projectDir "vendor\bepinex"
$vendorBepZip = Join-Path $vendorBepDir "BepInEx_UnityIL2CPP_x64.zip"
if (-not (Test-Path $vendorBepZip)) {
    throw "Bundled BepInEx vendor zip missing: $vendorBepZip. Run 'pixi run update-deps' to refresh."
}

# --- GitHub Release ZIP (with installer) ---

Write-Host "--- GitHub Release ZIP ---" -ForegroundColor Yellow
Write-Host ""

$ghStagingDir = Join-Path $releaseDir "staging-github"
if (Test-Path $ghStagingDir) { Remove-Item -Recurse -Force $ghStagingDir }
New-Item -ItemType Directory -Path $ghStagingDir -Force | Out-Null

foreach ($script in @("install.cmd", "uninstall.cmd")) {
    Copy-Item (Join-Path $scriptsDir $script) -Destination $ghStagingDir -Force
    Write-Host "  $script" -ForegroundColor Green
}

# Launcher manifest: the contract lopari ingests to deploy this package.
# Stamp mod_info.version with the real release version as we stage it.
$manifestPath = Join-Path $projectDir "launcher-manifest.json"
if (-not (Test-Path $manifestPath)) {
    throw "Required launcher manifest not found: $manifestPath"
}
$manifestJson = Get-Content $manifestPath -Raw
$manifestJson = $manifestJson -replace '("version":\s*")\d+\.\d+\.\d+(")', "`${1}$version`${2}"
Set-Content -Path (Join-Path $ghStagingDir "launcher-manifest.json") -Value $manifestJson -NoNewline -Encoding UTF8
Write-Host "  launcher-manifest.json (version $version)" -ForegroundColor Green

$pluginsDir = Join-Path $ghStagingDir "plugins"
New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null

foreach ($dll in $modDlls) {
    Copy-Item (Join-Path $buildOutputDir $dll) -Destination $pluginsDir -Force
    Write-Host "  plugins/$dll" -ForegroundColor Green
}

# Bundle vendored BepInEx (LGPL-2.1, see THIRD-PARTY-NOTICES.md) as install-time source.
$ghVendorDir = Join-Path $ghStagingDir "vendor\bepinex"
New-Item -ItemType Directory -Path $ghVendorDir -Force | Out-Null
foreach ($vendorFile in @("BepInEx_UnityIL2CPP_x64.zip", "LICENSE", "README.md")) {
    $src = Join-Path $vendorBepDir $vendorFile
    if (-not (Test-Path $src)) {
        throw "Required vendor file missing: $src. Run 'pixi run update-deps' to refresh."
    }
    Copy-Item $src -Destination $ghVendorDir -Force
    Write-Host "  vendor/bepinex/$vendorFile" -ForegroundColor Green
}

# Bundle the shared detection bundle for install.cmd's shim.
Copy-SharedBundle -StagingDir $ghStagingDir -CoreRoot (Join-Path $projectDir 'cameraunlock-core')

$docFiles = @("README.md", "LICENSE", "CHANGELOG.md", "THIRD-PARTY-NOTICES.md")
foreach ($doc in $docFiles) {
    $docPath = Join-Path $projectDir $doc
    if (-not (Test-Path $docPath)) {
        throw "Required document not found: $doc. Every published ZIP is a binary distribution and must carry it."
    }
    Copy-Item $docPath -Destination $ghStagingDir -Force
    Write-Host "  $doc" -ForegroundColor Green
}

$ghZipName = "ShadowsOfDoubtHeadTracking-v$version-installer.zip"
$ghZipPath = Join-Path $releaseDir $ghZipName
if (Test-Path $ghZipPath) { Remove-Item $ghZipPath -Force }

Write-Host ""
Write-Host "Creating GitHub ZIP..." -ForegroundColor Cyan

Push-Location $ghStagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $ghZipPath -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force $ghStagingDir

$ghZipSize = (Get-Item $ghZipPath).Length / 1KB
Write-Host ("  $ghZipPath ({0:N1} KB)" -f $ghZipSize) -ForegroundColor Green

# --- Nexus Mods ZIP (extract-to-game-folder) ---

Write-Host ""
Write-Host "--- Nexus Mods ZIP ---" -ForegroundColor Yellow
Write-Host ""

$nexusStagingDir = Join-Path $releaseDir "staging-nexus"
if (Test-Path $nexusStagingDir) { Remove-Item -Recurse -Force $nexusStagingDir }

# Mirror game directory structure: BepInEx/plugins/
# Users extract to game root, DLLs land in <game>/BepInEx/plugins/
$nexusPluginsDir = Join-Path $nexusStagingDir "BepInEx\plugins"
New-Item -ItemType Directory -Path $nexusPluginsDir -Force | Out-Null

foreach ($dll in $modDlls) {
    Copy-Item (Join-Path $buildOutputDir $dll) -Destination $nexusPluginsDir -Force
    Write-Host "  BepInEx/plugins/$dll" -ForegroundColor Green
}

$nexusZipName = "ShadowsOfDoubtHeadTracking-v$version-nexus.zip"
$nexusZipPath = Join-Path $releaseDir $nexusZipName
if (Test-Path $nexusZipPath) { Remove-Item $nexusZipPath -Force }

Write-Host ""
Write-Host "Creating Nexus ZIP..." -ForegroundColor Cyan

# The Nexus ZIP is a binary distribution too: the licences of everything
# compiled into or bundled with the payload require their notices to travel
# with it, so LICENSE and THIRD-PARTY-NOTICES.md ship at its root.
foreach ($noticeDoc in @('LICENSE', 'THIRD-PARTY-NOTICES.md', 'README.md')) {
    $noticeSrc = Join-Path $projectDir $noticeDoc
    if (-not (Test-Path $noticeSrc)) {
        throw "Required notice file not found: $noticeDoc. Every published ZIP is a binary distribution and must carry it."
    }
    Copy-Item $noticeSrc -Destination $nexusStagingDir -Force
    Write-Host "  $noticeDoc" -ForegroundColor Green
}
Push-Location $nexusStagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $nexusZipPath -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force $nexusStagingDir

$nexusZipSize = (Get-Item $nexusZipPath).Length / 1KB
Write-Host ("  $nexusZipPath ({0:N1} KB)" -f $nexusZipSize) -ForegroundColor Green

# --- Summary ---

Write-Host ""
Write-Host "=== Package Complete ===" -ForegroundColor Magenta
Write-Host ""
Write-Host ("GitHub Release: $ghZipPath ({0:N1} KB)" -f $ghZipSize) -ForegroundColor Green
Write-Host ("Nexus Mods:     $nexusZipPath ({0:N1} KB)" -f $nexusZipSize) -ForegroundColor Green

# Output both zip paths for CI capture (one per line)
Write-Output $ghZipPath
Write-Output $nexusZipPath
