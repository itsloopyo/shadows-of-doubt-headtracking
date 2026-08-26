// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;
using ShadowsOfDoubtHeadTracking.Core;
using UnityEngine;

namespace ShadowsOfDoubtHeadTracking.Utilities;

/// <summary>
/// Places the game's render window once at startup, then does nothing for the
/// rest of the session.
/// </summary>
internal sealed class GameWindowPlacer
{
    // Shadows of Doubt launches behind other windows. Wait ~2s after init so the
    // real Unity render window is up - MainWindowHandle can briefly point at a
    // splash/loader handle, and bringing that one forward locks out the retry.
    private const int StartupDelayFrames = 120;
    private const int MaxPollFrames = 600;

    private bool _placed;
    private int _frameCount;

    /// <summary>Call once per frame from Update.</summary>
    internal void Poll()
    {
        if (_placed) return;

        _frameCount++;
        if (_frameCount < StartupDelayFrames) return;

        IntPtr window = WindowForegrounder.FindRenderWindow();
        if (window == IntPtr.Zero)
        {
            // Giving up silently would leave the search running for the whole
            // session and leave a user staring at a window that never came
            // forward with nothing in the log to explain it.
            if (_frameCount >= StartupDelayFrames + MaxPollFrames)
            {
                _placed = true;
                HeadTrackingPlugin.Logger.LogWarning(
                    "Render window not found - leaving the game window where it launched");
            }
            return;
        }

        // Latched before the placement is attempted, not after it succeeds. The
        // placement calls throw on an unexpected Win32 failure, and this runs first
        // in HeadTrackingBehaviour.Update - so a throw here with the flag still
        // clear would skip hotkeys and game-state detection for that frame and then
        // do it again on every frame after, leaving a player unable to even toggle
        // the mod off. Latching first keeps the failure loud but one-shot.
        _placed = true;

        // A windowed launch lands against the top-left of the desktop, which on a
        // wide monitor puts the view well off to one side of where the tracker is
        // centred. Fullscreen (exclusive or borderless) already fills the monitor.
        bool centered = !Screen.fullScreen;
        if (centered)
        {
            WindowForegrounder.CenterOnMonitor(window);
        }

        WindowForegrounder.BringToFront(window);

        HeadTrackingPlugin.Logger.LogInfo(centered
            ? "Centred the game window on its monitor and brought it to the foreground"
            : "Brought game window to foreground");
    }
}
