// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;
using CameraUnlock.Core.Input;
using CameraUnlock.Core.Unity.Extensions;
using ShadowsOfDoubtHeadTracking.Configuration;
using ShadowsOfDoubtHeadTracking.Core;

namespace ShadowsOfDoubtHeadTracking.Input;

/// <summary>
/// Fires the mod's hotkey actions from the key lists in CameraUnlock.ini: ToggleKey,
/// CycleTrackingModeKey and YawModeKey. Every binding in a list is an ordinary item, the
/// Ctrl+Shift chords included, so the defaults (End, Page Up and Page Down, each with its
/// chord) are rebindable like any other key.
///
/// Hotkeys work regardless of gameplay state, allowing users to toggle
/// tracking even in menus.
/// </summary>
public sealed class HotkeyHandler
{
    private readonly string _toggleKeyName;
    private readonly KeyBinding[] _toggle;
    private readonly KeyBinding[] _cycleTrackingMode;
    private readonly KeyBinding[] _yawMode;

    private int _toggleCount;
    private DateTime _lastToggleTime = DateTime.MinValue;

    internal HotkeyHandler(ShadowsOfDoubtConfig config)
    {
        _toggleKeyName = config.ToggleKeyName;
        _toggle = Parse("ToggleKey", config.ToggleKeyName);
        _cycleTrackingMode = Parse("CycleTrackingModeKey", config.CycleTrackingModeKeyName);
        _yawMode = Parse("YawModeKey", config.YawModeKeyName);

        HeadTrackingPlugin.Logger.LogInfo(
            $"Hotkeys: [{config.ToggleKeyName}] toggle, [{config.CycleTrackingModeKeyName}] cycle tracking mode, " +
            $"[{config.YawModeKeyName}] yaw mode");
    }

    /// <summary>
    /// Process hotkey input. Call from Update() every frame.
    /// Hotkeys work regardless of gameplay state to allow toggling in menus.
    /// </summary>
    public void ProcessInput()
    {
        if (KeyBindingInput.IsTriggered(_toggle))
        {
            FireToggle();
        }

        if (KeyBindingInput.IsTriggered(_cycleTrackingMode))
        {
            HeadTrackingBehaviour.CycleTrackingMode();
        }

        if (KeyBindingInput.IsTriggered(_yawMode))
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

    // The table's hotkey codec has read every list the file holds, and the legacy import writes
    // only key lists, so a list that does not parse is a bug.
    private static KeyBinding[] Parse(string key, string text)
    {
        if (!KeyBindings.TryParse(text, out KeyBinding[] bindings, out string? error))
            throw new InvalidOperationException($"[Hotkeys] {key}={text}: {error}");
        return bindings;
    }

    /// <summary>
    /// Get diagnostic information about hotkey handler state.
    /// </summary>
    public HotkeyDiagnostics GetDiagnostics()
    {
        return new HotkeyDiagnostics
        {
            IsInitialized = true,
            ToggleKey = _toggleKeyName,
            ToggleCount = _toggleCount,
            LastToggleTime = _lastToggleTime,
            IsTrackingEnabled = ModState.Instance.IsEnabled
        };
    }
}
