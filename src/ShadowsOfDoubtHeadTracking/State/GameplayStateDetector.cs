// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;
using ShadowsOfDoubtHeadTracking.Camera;
using ShadowsOfDoubtHeadTracking.Core;
using UnityEngine;

namespace ShadowsOfDoubtHeadTracking.State;

/// <summary>
/// Decides whether the player is in first-person gameplay.
///
/// Shadows of Doubt runs the menu, the city and every in-game interface inside a
/// single scene called "Main", so the scene name alone cannot tell gameplay from
/// the main menu - it says "Main" while the title screen is up. The decision is
/// therefore made from the game's own state singletons, read through reflection
/// (see <see cref="GameFlagReader"/>) so the build keeps no compile-time
/// dependency on Assembly-CSharp.
///
/// Everything is polled rather than event-driven: IL2CPP cannot subscribe managed
/// delegates to Unity's scene and focus events.
/// </summary>
public sealed class GameplayStateDetector : IDisposable
{
    // Scene/focus polling is cheap but not free, and both change rarely. At 60Hz
    // this is ~100ms, far below anything a player can perceive on an alt-tab or a
    // scene transition.
    private const int PollEveryNFrames = 6;

    private CameraFinder? _cameraFinder;
    private int _pollFrameCounter;
    private bool _disposed;

    private GameState _currentState = GameState.Initializing;
    private string _currentSceneName = string.Empty;
    private DateTime _stateEnteredAt = DateTime.UtcNow;
    private int _stateTransitionCount;
    private bool _isApplicationFocused = true;

    // Menu / loading gates.
    private readonly GameFlagReader _mainMenuActive = new("MainMenuController", "mainMenuActive");
    private readonly GameFlagReader _startedGame = new("SessionData", "startedGame");
    private readonly GameFlagReader _sessionPlaying = new("SessionData", "play");
    private readonly GameFlagReader _cityLoading = new("CityConstructor", "loadingOperationActive");
    private readonly GameFlagReader _cityPreSim = new("CityConstructor", "preSimActive");

    // In-game interface gates: case board / map desktop, text entry, dialogue,
    // reading a document, and cutscenes all take first-person look away.
    private readonly GameFlagReader[] _interfaceGates =
    {
        new("InterfaceController", "desktopMode"),
        new("InterfaceController", "playerTextInputActive"),
        new("InteractionController", "dialogMode"),
        new("InteractionController", "readingMode"),
        new("CutSceneController", "cutSceneActive")
    };

    // The player itself: knocked out, riding a scripted transition, being
    // force-looked at a target, skipping time or auto-travelling are all states
    // where the game owns the view.
    private readonly GameFlagReader[] _playerGates =
    {
        new("Player", "transitionActive"),
        new("Player", "forceLookAtActive"),
        new("Player", "playerKOInProgress"),
        new("Player", "spendingTimeMode"),
        new("Player", "autoTravelActive")
    };

    private readonly GameFlagReader _playerExists = new("Player", "fpsMode");

    /// <summary>Current detected game state.</summary>
    public GameState CurrentState => _currentState;

    /// <summary>Name of the scene the detector last saw.</summary>
    public string CurrentScene => _currentSceneName;

    /// <summary>Whether the player is currently in first-person gameplay.</summary>
    public bool IsInGameplay => _currentState == GameState.Gameplay;

    /// <summary>Last polled value of <c>Application.isFocused</c>.</summary>
    public bool IsApplicationFocused => _isApplicationFocused;

    /// <summary>
    /// Fired when gameplay is entered (true) or left (false).
    /// </summary>
    public event Action<bool>? OnGameplayStateChanged;

    public void Initialize()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(GameplayStateDetector));
        }

        _stateEnteredAt = DateTime.UtcNow;
        _currentSceneName = SceneNames.GetCurrentSceneName();
        Evaluate();
    }

    /// <summary>
    /// Set camera finder reference for cache invalidation on scene change.
    /// </summary>
    public void SetCameraFinder(CameraFinder cameraFinder)
    {
        _cameraFinder = cameraFinder;
    }

    /// <summary>
    /// Polls scene name, window focus and the game's state singletons.
    /// Call once per frame from Update.
    /// </summary>
    public void Poll()
    {
        if (_disposed) return;
        if (++_pollFrameCounter < PollEveryNFrames) return;
        _pollFrameCounter = 0;

        _isApplicationFocused = Application.isFocused;

        string sceneName = SceneNames.GetCurrentSceneName();
        if (sceneName.Length > 0 && sceneName != _currentSceneName)
        {
            HeadTrackingPlugin.Logger.LogInfo($"Scene changed to '{sceneName}'");
            _currentSceneName = sceneName;
            _cameraFinder?.InvalidateCache();
        }

        Evaluate();
    }

    /// <summary>
    /// Get diagnostic information about current state.
    /// </summary>
    public GameStateDiagnostics GetDiagnostics()
    {
        return new GameStateDiagnostics
        {
            CurrentState = _currentState,
            CurrentScene = _currentSceneName,
            IsWindowFocused = _isApplicationFocused,
            ShouldTrackingBeActive = IsInGameplay && _isApplicationFocused,
            StateTransitionCount = _stateTransitionCount,
            TimeInCurrentState = DateTime.UtcNow - _stateEnteredAt,
            StatusReason = DescribeState()
        };
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private void Evaluate()
    {
        GameState newState = DetermineState();
        if (newState == _currentState) return;

        bool wasGameplay = _currentState == GameState.Gameplay;
        bool isGameplay = newState == GameState.Gameplay;

        HeadTrackingPlugin.Logger.LogInfo(
            $"Game state: {_currentState} -> {newState} (scene '{_currentSceneName}')");

        _currentState = newState;
        _stateEnteredAt = DateTime.UtcNow;
        _stateTransitionCount++;

        if (wasGameplay != isGameplay)
        {
            OnGameplayStateChanged?.Invoke(isGameplay);
        }
    }

    private GameState DetermineState()
    {
        // IsLoadingScene already covers the empty name a load reports.
        if (SceneNames.IsLoadingScene(_currentSceneName))
        {
            return GameState.Loading;
        }

        // A scene the game only shows before the city exists (controller detection,
        // splash). Not the menu proper, but definitely not gameplay either.
        if (SceneNames.IsPreGameScene(_currentSceneName))
        {
            return GameState.MainMenu;
        }

        if (_mainMenuActive.Read(fallback: false) || !_startedGame.Read(fallback: false))
        {
            return GameState.MainMenu;
        }

        if (_cityLoading.Read(fallback: false) || _cityPreSim.Read(fallback: false))
        {
            return GameState.Loading;
        }

        // The simulation stops for the pause menu, the notebook, sleeping and every
        // other modal the game puts up over the world.
        if (!_sessionPlaying.Read(fallback: true))
        {
            return GameState.Paused;
        }

        if (AnyActive(_interfaceGates))
        {
            return GameState.Paused;
        }

        // Catch-all for every interface the flags above do not name individually -
        // computer terminals, keypads, phone calls. The game releases the cursor
        // whenever it takes first-person look away, so a cursor that is not locked
        // means head tracking has nothing to drive. Only consulted while the window
        // is focused, because Unity releases the cursor on focus loss by itself and
        // that case is the caller's PauseOnLostFocus decision, not ours.
        if (_isApplicationFocused && Cursor.lockState != CursorLockMode.Locked)
        {
            return GameState.Paused;
        }

        // The game drives the camera itself through transitions (climbing through a
        // window, sitting down), forced look-ats, knockouts, time skips and auto-travel.
        if (AnyActive(_playerGates))
        {
            return GameState.Paused;
        }

        if (!_playerExists.InstanceExists())
        {
            return GameState.Loading;
        }

        return GameState.Gameplay;
    }

    /// <summary>
    /// Whether any gate reports itself active. An unresolved gate reads false, so a
    /// game update that renames a field degrades that one gate rather than the set.
    /// </summary>
    private static bool AnyActive(GameFlagReader[] gates)
    {
        for (int i = 0; i < gates.Length; i++)
        {
            if (gates[i].Read(fallback: false)) return true;
        }
        return false;
    }

    private string DescribeState()
    {
        if (!_isApplicationFocused) return "Game window not focused";

        return _currentState switch
        {
            GameState.Initializing => "Game initializing",
            GameState.MainMenu => "In main menu",
            GameState.Loading => "Loading in progress",
            GameState.Gameplay => "Active gameplay",
            GameState.Paused => "Paused or in an interface",
            _ => "Unknown state"
        };
    }
}
