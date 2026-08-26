#!/usr/bin/env pwsh
#Requires -Version 5.1
# Bump the vendored BepInEx 6 IL2CPP build to the latest published on
# builds.bepinex.dev. Manual: the dev runs this, reviews the diff, commits.
# The build task does not depend on it and CI never runs it.
#
# BepInEx 6 IL2CPP is bleeding edge and is only published on the BepInEx CI
# build server, never on GitHub releases, so Update-VendoredLoader's GitHub
# mode does not apply. We resolve the newest IL2CPP-win-x64 build off the
# project index and hand that URL to Update-VendoredLoader's DirectUrl mode,
# which owns the download, SHA-256, idempotency, LICENSE and README.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference    = 'SilentlyContinue'

$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir

Import-Module (Join-Path $projectDir 'cameraunlock-core/powershell/ModLoaderSetup.psm1') -Force

$out = Join-Path $projectDir 'vendor/bepinex'

Write-Host "Discovering latest BepInEx 6 IL2CPP build from builds.bepinex.dev..." -ForegroundColor Cyan
$idx = Invoke-WebRequest -Uri 'https://builds.bepinex.dev/projects/bepinex_be' -UseBasicParsing `
    -Headers @{ "User-Agent" = "CameraUnlock-HeadTracking" } -TimeoutSec 30
$hrefs = [regex]::Matches(
    $idx.Content,
    'href="(/projects/bepinex_be/(\d+)/BepInEx-Unity\.IL2CPP-win-x64-6\.0\.0-be\.\d+(?:%2B|\+)([a-f0-9]+)\.zip)"')
if (-not $hrefs.Count) { throw "Could not find any IL2CPP-win-x64 builds on builds.bepinex.dev" }
$best = $hrefs | ForEach-Object {
    [pscustomobject]@{
        Build  = [int]$_.Groups[2].Value
        Path   = $_.Groups[1].Value
        Commit = $_.Groups[3].Value
    }
} | Sort-Object -Property Build -Descending | Select-Object -First 1

$meta = Update-VendoredLoader `
    -Name 'bepinex' `
    -OutputDir $out `
    -OutputFileName 'BepInEx_UnityIL2CPP_x64.zip' `
    -DirectUrl "https://builds.bepinex.dev$($best.Path)" `
    -LicenseUrl 'https://raw.githubusercontent.com/BepInEx/BepInEx/master/LICENSE'

# DirectUrl mode has no tag or commit to report, so the module leaves both out
# of the README. Re-insert them from the build index, plus the on-disk name
# install.cmd hardcodes (the module records the upstream asset name instead).
$readmePath = Join-Path $out 'README.md'
$lines = (Get-Content -LiteralPath $readmePath -Raw).TrimEnd("`r", "`n") -split "`r?`n" |
    Where-Object { $_ -notmatch '^- (Vendored as|Tag|Commit):' }
$patched = foreach ($line in $lines) {
    $line
    if ($line -match '^- Asset:') {
        "- Vendored as: ``BepInEx_UnityIL2CPP_x64.zip``"
        "- Tag: ``6.0.0-be.$($best.Build)``"
        "- Commit: ``$($best.Commit)``"
    }
}
Set-Content -LiteralPath $readmePath -Value ($patched -join "`n") -Encoding UTF8

Write-Host ""
Write-Host "vendor/bepinex at build $($best.Build) (sha $($meta.Sha256.Substring(0,12))...). Review and commit." -ForegroundColor Green
