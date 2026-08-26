// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;
using UnityEngine.SceneManagement;

namespace ShadowsOfDoubtHeadTracking.State;

/// <summary>
/// Scene-name helpers for Shadows of Doubt.
///
/// The game ships two scenes that matter here: "ControllerDetect", shown once at
/// boot, and "Main", which hosts the title screen, the generated city and every
/// interface. "Main" therefore says nothing about whether the player is in
/// gameplay - that decision belongs to <see cref="GameplayStateDetector"/>, which
/// reads the game's own state singletons. These helpers only rule out the scenes
/// where there is no city at all.
/// </summary>
public static class SceneNames
{
    /// <summary>The scene that hosts both the title screen and gameplay.</summary>
    public const string MainScene = "Main";

    /// <summary>
    /// Get the current scene name. Returns an empty string while scene data is
    /// unavailable, which happens during a load.
    /// </summary>
    public static string GetCurrentSceneName()
    {
        if (SceneManager.sceneCount <= 0)
        {
            return string.Empty;
        }

        var scene = SceneManager.GetSceneAt(0);
        return scene.name ?? string.Empty;
    }

    /// <summary>
    /// Whether the scene name means a load is in progress.
    /// </summary>
    public static bool IsLoadingScene(string sceneName)
    {
        return sceneName.Length == 0
            || sceneName.IndexOf("load", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Whether the scene runs before the game proper: the boot-time controller
    /// detection screen, or any scene other than <see cref="MainScene"/>. Unknown
    /// scenes count as pre-game so an unrecognised name never enables tracking.
    /// </summary>
    public static bool IsPreGameScene(string sceneName)
    {
        return !string.Equals(sceneName, MainScene, StringComparison.OrdinalIgnoreCase);
    }
}
