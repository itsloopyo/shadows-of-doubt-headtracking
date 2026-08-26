// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using ShadowsOfDoubtHeadTracking.Input;
using ShadowsOfDoubtHeadTracking.State;

namespace ShadowsOfDoubtHeadTracking.Core;

/// <summary>
/// Diagnostic information about HeadTrackingBehaviour state.
/// </summary>
public readonly struct BehaviourDiagnostics
{
    public bool IsInitialized { get; init; }
    public bool IsEnabled { get; init; }
    public bool IsInGameplay { get; init; }
    public bool HasCamera { get; init; }
    public bool IsReceiverConnected { get; init; }
    public string? CameraPath { get; init; }
    public HotkeyDiagnostics? HotkeyDiagnostics { get; init; }
    public GameStateDiagnostics? GameStateDiagnostics { get; init; }

    /// <summary>
    /// Whether head tracking is currently being applied.
    /// </summary>
    public bool IsActive => IsInitialized && IsEnabled && IsInGameplay && HasCamera && IsReceiverConnected;

    public override string ToString()
    {
        return $"HeadTracking(Active={IsActive}, Init={IsInitialized}, Enabled={IsEnabled}, " +
               $"Gameplay={IsInGameplay}, Camera={HasCamera}, Connected={IsReceiverConnected})";
    }
}
