// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;

namespace ShadowsOfDoubtHeadTracking.Input;

/// <summary>
/// Diagnostic information about hotkey handler state.
/// </summary>
public readonly struct HotkeyDiagnostics
{
    public bool IsInitialized { get; init; }
    public string ToggleKey { get; init; }
    public int ToggleCount { get; init; }
    public DateTime LastToggleTime { get; init; }
    public bool IsTrackingEnabled { get; init; }

    public override string ToString()
    {
        return $"Hotkeys(Toggle={ToggleKey}, Toggles={ToggleCount}, Enabled={IsTrackingEnabled})";
    }
}
