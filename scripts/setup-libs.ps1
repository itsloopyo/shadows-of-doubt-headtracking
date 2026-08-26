#!/usr/bin/env pwsh
# Populate src/ShadowsOfDoubtHeadTracking/libs/ with the Unity / BepInEx /
# IL2CPP DLLs that CameraUnlock.Core.Unity's HintPath references resolve to.
#
# Sources are REPO FILES ONLY - never a game install. A contributor who owns
# Shadows of Doubt and a CI runner that does not must compile against byte-
# identical references, otherwise a member missing from the stub passes
# locally and fails on push.
#
# - BepInEx, IL2CPP, HarmonyX, UnityEngine.* modules: NuGet cache (matches
#   the PackageReferences in ShadowsOfDoubtHeadTracking.csproj).
#
# UnityEngine.UI (UGUI) is deliberately absent. It is not on NuGet and ships
# only inside a built Unity game, so there is no copy of it we may vendor.
# Directory.Build.targets drops the submodule's reference to it along with
# CrosshairUtility.cs, its only consumer, neither of which this mod uses.
#
# libs/ is wiped first so a local run reproduces the runner's empty-libs/
# start and a stale DLL cannot survive into the build.
#
# Run order: dotnet restore -> setup -> dotnet build. The pixi `build` task
# wires this. CI calls this same script through `pixi run package`.

$ErrorActionPreference = "Stop"

$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$libsPath    = Join-Path $projectRoot "src/ShadowsOfDoubtHeadTracking/libs"

New-Item -ItemType Directory -Path $libsPath -Force | Out-Null
Get-ChildItem $libsPath -Filter '*.dll' -File | Remove-Item -Force

$nugetRoot = (& dotnet nuget locals global-packages -l) -replace '^global-packages: ', ''
if (-not (Test-Path $nugetRoot)) {
    throw "NuGet global-packages root not found: $nugetRoot. Run 'dotnet restore' first."
}

$packageDlls = @(
    @{ Pkg = 'bepinex.unity.il2cpp/6.0.0-be.753';  Path = 'lib/net6.0/BepInEx.Unity.IL2CPP.dll' },
    @{ Pkg = 'bepinex.core/6.0.0-be.753';          Path = 'lib/netstandard2.0/BepInEx.Core.dll' },
    @{ Pkg = 'harmonyx/2.10.2';                    Path = 'lib/netstandard2.0/0Harmony.dll' },
    @{ Pkg = 'il2cppinterop.runtime/1.5.0-ci.620'; Path = 'lib/net6.0/Il2CppInterop.Runtime.dll' },
    @{ Pkg = 'il2cppinterop.referencelibs/1.0.0';  Path = 'lib/net6.0/Il2Cppmscorlib.dll' }
)

foreach ($entry in $packageDlls) {
    $src = Join-Path $nugetRoot ("$($entry.Pkg)/$($entry.Path)")
    if (-not (Test-Path $src)) {
        throw "Missing NuGet asset: $src. Did 'dotnet restore' complete?"
    }
    Copy-Item $src $libsPath -Force
}

$unityModulesDir = Join-Path $nugetRoot 'unityengine.modules/2021.3.45/lib/netstandard2.0'
if (-not (Test-Path $unityModulesDir)) {
    throw "UnityEngine.Modules NuGet package not found at $unityModulesDir."
}
Copy-Item (Join-Path $unityModulesDir '*.dll') $libsPath -Force

$dlls = Get-ChildItem $libsPath -Filter '*.dll'
Write-Host "Populated $($dlls.Count) DLLs in $libsPath" -ForegroundColor Green
$dlls | ForEach-Object { Write-Host "  $($_.Name)" -ForegroundColor Gray }
