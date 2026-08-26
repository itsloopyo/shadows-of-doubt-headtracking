// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;
using CameraUnlock.Core.Unity.Extensions;
using ShadowsOfDoubtHeadTracking.Configuration;
using ShadowsOfDoubtHeadTracking.Core;
using UnityEngine;

namespace ShadowsOfDoubtHeadTracking.Input;

/// <summary>
/// Handles hotkey input for head tracking actions.
///
/// Two equivalent binding sets are registered simultaneously:
/// - Nav-cluster: End (toggle), Page Up (cycle tracking mode),
///   Page Down (toggle yaw mode)
/// - Chord (Y/G/H cluster): Ctrl+Shift+Y, Ctrl+Shift+G, Ctrl+Shift+H
///
/// Either binding fires the same handler. Edge detection is handled by
/// Input.GetKeyDown for the primary key of each set, so holding the modifier
/// keys does not double-fire the action.
///
/// Hotkeys work regardless of gameplay state, allowing users to toggle
/// tracking even in menus.
/// </summary>
public sealed class HotkeyHandler
{
    private readonly PluginConfig _config;

    private bool _initialized;
    private int _toggleCount;
    private DateTime _lastToggleTime;

    // Cached KeyCodes to avoid BepInEx ConfigEntry<T>.Value getter overhead per frame
    private KeyCode _cachedToggleKey;
    private KeyCode _cachedCycleModeKey;
    private KeyCode _cachedYawModeKey;

    public HotkeyHandler(PluginConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Initialize hotkey handler.
    /// </summary>
    public void Initialize()
    {
        if (_initialized)
        {
            HeadTrackingPlugin.Logger.LogWarning("HotkeyHandler already initialized");
            return;
        }

        _toggleCount = 0;
        _lastToggleTime = DateTime.MinValue;

        // Cache KeyCodes to avoid BepInEx ConfigEntry<T>.Value getter overhead per frame
        _cachedToggleKey = _config.ToggleKey.Value;
        _cachedCycleModeKey = _config.CycleTrackingModeKey.Value;
        _cachedYawModeKey = _config.YawModeKey.Value;

        HeadTrackingPlugin.Logger.LogInfo(
            $"Hotkeys initialized: nav-cluster {_cachedToggleKey}/{_cachedCycleModeKey}/{_cachedYawModeKey} (Toggle/CycleMode/YawMode); chords Ctrl+Shift+Y/G/H");

        _initialized = true;
    }

    /// <summary>
    /// Process hotkey input. Call from Update() every frame.
    /// Hotkeys work regardless of gameplay state to allow toggling in menus.
    /// </summary>
    public void ProcessInput()
    {
        if (!_initialized)
        {
            return;
        }

        if (ChordHotkeys.IsActionPressed(_cachedToggleKey, ChordHotkeys.ToggleLetter))
        {
            FireToggle();
        }

        if (ChordHotkeys.IsActionPressed(_cachedCycleModeKey, ChordHotkeys.PositionLetter))
        {
            HeadTrackingBehaviour.CycleTrackingMode();
        }

        if (ChordHotkeys.IsActionPressed(_cachedYawModeKey, ChordHotkeys.FourthToggleLetter))
        {
            HeadTrackingBehaviour.ToggleYawMode();
        }
    }

    private void FireToggle()
    {
        _toggleCount++;
        _lastToggleTime = DateTime.UtcNow;

        ModState.Instance.Toggle();
        bool newState = ModState.Instance.IsEnabled;

        HeadTrackingPlugin.Logger.LogInfo(
            $"Head tracking {(newState ? "ENABLED" : "DISABLED")}");
    }

    /// <summary>
    /// Get diagnostic information about hotkey handler state.
    /// </summary>
    public HotkeyDiagnostics GetDiagnostics()
    {
        return new HotkeyDiagnostics
        {
            IsInitialized = _initialized,
            ToggleKey = _cachedToggleKey,
            ToggleCount = _toggleCount,
            LastToggleTime = _lastToggleTime,
            IsTrackingEnabled = ModState.Instance.IsEnabled
        };
    }
}
