// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;

namespace ShadowsOfDoubtHeadTracking.State;

/// <summary>
/// Represents the current game state for head tracking activation.
/// </summary>
public enum GameState
{
    /// <summary>Game is starting up or in initial loading.</summary>
    Initializing,

    /// <summary>Player is in the main menu.</summary>
    MainMenu,

    /// <summary>Game is loading (scene transition, save loading).</summary>
    Loading,

    /// <summary>Player is in active gameplay.</summary>
    Gameplay,

    /// <summary>Game is paused (ESC menu, inventory, etc), or in an interface.</summary>
    Paused
}

/// <summary>
/// Diagnostic information about game state detection.
/// </summary>
public readonly struct GameStateDiagnostics
{
    /// <summary>Current detected game state.</summary>
    public GameState CurrentState { get; init; }

    /// <summary>Current scene name.</summary>
    public string CurrentScene { get; init; }

    /// <summary>Whether the game window is focused.</summary>
    public bool IsWindowFocused { get; init; }

    /// <summary>Whether head tracking should be active based on state.</summary>
    public bool ShouldTrackingBeActive { get; init; }

    /// <summary>Number of state transitions since initialization.</summary>
    public int StateTransitionCount { get; init; }

    /// <summary>Time spent in current state.</summary>
    public TimeSpan TimeInCurrentState { get; init; }

    /// <summary>Reason for current active/inactive status.</summary>
    public string StatusReason { get; init; }

    public override string ToString()
    {
        return $"GameState={CurrentState}, Scene={CurrentScene}, Focused={IsWindowFocused}, " +
               $"Active={ShouldTrackingBeActive}, Transitions={StateTransitionCount}";
    }
}
