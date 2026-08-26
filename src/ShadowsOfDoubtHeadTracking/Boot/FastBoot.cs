// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using UnityEngine.Rendering;

namespace ShadowsOfDoubtHeadTracking.Boot;

/// <summary>
/// Cuts startup latency for fast debug iteration. Only touches things the
/// Unity runtime exposes directly (no game-side interop required).
/// </summary>
internal static class FastBoot
{
    public static void Apply()
    {
        SplashScreen.Stop(SplashScreen.StopBehavior.StopImmediate);
    }
}
