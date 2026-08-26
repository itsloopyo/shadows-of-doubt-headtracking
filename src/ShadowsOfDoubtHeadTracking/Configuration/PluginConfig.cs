// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using BepInEx.Configuration;
using UnityEngine;

namespace ShadowsOfDoubtHeadTracking.Configuration;

/// <summary>
/// BepInEx configuration bindings for head tracking settings.
/// Settings are saved to BepInEx/config/com.headtracking.shadowsofdoubt.cfg
/// </summary>
public sealed class PluginConfig
{
    /// <summary>Yaw (horizontal) rotation sensitivity multiplier.</summary>
    public ConfigEntry<float> YawSensitivity { get; private set; } = null!;

    /// <summary>Pitch (vertical) rotation sensitivity multiplier.</summary>
    public ConfigEntry<float> PitchSensitivity { get; private set; } = null!;

    /// <summary>Roll rotation sensitivity multiplier.</summary>
    public ConfigEntry<float> RollSensitivity { get; private set; } = null!;

    /// <summary>Enable head tracking when game starts.</summary>
    public ConfigEntry<bool> EnabledOnStartup { get; private set; } = null!;

    /// <summary>Invert yaw axis (for coordinate transformation).</summary>
    public ConfigEntry<bool> InvertYaw { get; private set; } = null!;

    /// <summary>Invert pitch axis (for coordinate transformation).</summary>
    public ConfigEntry<bool> InvertPitch { get; private set; } = null!;

    /// <summary>Invert roll axis (for coordinate transformation).</summary>
    public ConfigEntry<bool> InvertRoll { get; private set; } = null!;

    /// <summary>Hotkey for toggling head tracking on/off.</summary>
    public ConfigEntry<KeyCode> ToggleKey { get; private set; } = null!;

    /// <summary>Hotkey for cycling the tracking mode.</summary>
    public ConfigEntry<KeyCode> CycleTrackingModeKey { get; private set; } = null!;

    /// <summary>Hotkey for toggling world-space vs camera-local yaw.</summary>
    public ConfigEntry<KeyCode> YawModeKey { get; private set; } = null!;

    /// <summary>
    /// True = horizon-locked yaw (default). Yaw rotates around the world up-axis.
    /// False = camera-local yaw. Yaw rotates around the camera's current up-axis.
    /// </summary>
    public ConfigEntry<bool> WorldSpaceYaw { get; private set; } = null!;

    /// <summary>Pause tracking when game window loses focus.</summary>
    public ConfigEntry<bool> PauseOnLostFocus { get; private set; } = null!;

    /// <summary>Write the per-frame camera rig and pose probes to the log.</summary>
    public ConfigEntry<bool> DiagnosticLogging { get; private set; } = null!;

    /// <summary>Degrees added to the field of view the game renders with.</summary>
    public ConfigEntry<float> FieldOfViewOffset { get; private set; } = null!;

    // Position settings
    /// <summary>Whether positional tracking (lean) is enabled.</summary>
    public ConfigEntry<bool> PositionEnabled { get; private set; } = null!;
    /// <summary>Lateral position sensitivity multiplier.</summary>
    public ConfigEntry<float> PositionSensitivityX { get; private set; } = null!;
    /// <summary>Vertical position sensitivity multiplier.</summary>
    public ConfigEntry<float> PositionSensitivityY { get; private set; } = null!;
    /// <summary>Depth position sensitivity multiplier.</summary>
    public ConfigEntry<float> PositionSensitivityZ { get; private set; } = null!;
    /// <summary>Maximum lateral displacement in meters.</summary>
    public ConfigEntry<float> PositionLimitX { get; private set; } = null!;
    /// <summary>Maximum upward vertical displacement in meters.</summary>
    public ConfigEntry<float> PositionLimitY { get; private set; } = null!;
    /// <summary>Maximum downward vertical displacement in meters.</summary>
    public ConfigEntry<float> PositionLimitYDown { get; private set; } = null!;
    /// <summary>Maximum forward displacement in meters.</summary>
    public ConfigEntry<float> PositionLimitZ { get; private set; } = null!;
    /// <summary>Maximum backward displacement in meters.</summary>
    public ConfigEntry<float> PositionLimitZBack { get; private set; } = null!;
    /// <summary>Smoothing for a tracker running on this machine (loopback). Covers rotation and position.</summary>
    public ConfigEntry<float> LocalSmoothing { get; private set; } = null!;
    /// <summary>Smoothing for a tracker on a remote network device. Covers rotation and position.</summary>
    public ConfigEntry<float> RemoteSmoothing { get; private set; } = null!;
    /// <summary>
    /// Initialize and bind all configuration entries.
    /// Must be called during plugin Load().
    /// </summary>
    public void Initialize(ConfigFile config)
    {
        BindSensitivity(config);
        BindGeneral(config);
        BindCoordinateTransform(config);
        BindHotkeys(config);
        BindBehavior(config);
        BindCamera(config);
        BindPosition(config);
    }

    private void BindSensitivity(ConfigFile config)
    {
        YawSensitivity = config.Bind(
            "Sensitivity",
            "YawSensitivity",
            1.0f,
            new ConfigDescription(
                "Yaw (horizontal) rotation sensitivity multiplier. " +
                "Use negative values to invert axis.",
                new AcceptableValueRange<float>(-5f, 5f)
            )
        );

        PitchSensitivity = config.Bind(
            "Sensitivity",
            "PitchSensitivity",
            1.0f,
            new ConfigDescription(
                "Pitch (vertical) rotation sensitivity multiplier. " +
                "Use negative values to invert axis.",
                new AcceptableValueRange<float>(-5f, 5f)
            )
        );

        RollSensitivity = config.Bind(
            "Sensitivity",
            "RollSensitivity",
            1.0f,
            new ConfigDescription(
                "Roll rotation sensitivity multiplier. " +
                "Use negative values to invert axis.",
                new AcceptableValueRange<float>(-5f, 5f)
            )
        );
    }

    private void BindGeneral(ConfigFile config)
    {
        EnabledOnStartup = config.Bind(
            "General",
            "EnabledOnStartup",
            true,
            "Enable head tracking automatically when the game starts. " +
            "If false, press End to enable manually."
        );

        WorldSpaceYaw = config.Bind(
            "General",
            "WorldSpaceYaw",
            true,
            "Yaw mode: true = horizon-locked yaw (default), false = camera-local. " +
            "Horizon-locked keeps 'up' constant when looking at extreme pitches; " +
            "camera-local follows the camera's current up-axis. " +
            "Toggle at runtime with Page Down or Ctrl+Shift+H."
        );
    }

    private void BindCoordinateTransform(ConfigFile config)
    {
        InvertYaw = config.Bind(
            "CoordinateTransform",
            "InvertYaw",
            false,
            "Invert the yaw (horizontal) axis. " +
            "Enable if turning your head right makes the camera turn left."
        );

        InvertPitch = config.Bind(
            "CoordinateTransform",
            "InvertPitch",
            true,
            "Invert the pitch (vertical) axis. " +
            "This is enabled by default to convert OpenTrack coordinates to Unity. " +
            "Disable if looking up makes the camera look down."
        );

        InvertRoll = config.Bind(
            "CoordinateTransform",
            "InvertRoll",
            false,
            "Invert the roll (tilt) axis. " +
            "Enable if tilting your head right makes the camera tilt left."
        );
    }

    private void BindHotkeys(ConfigFile config)
    {
        ToggleKey = config.Bind(
            "Hotkeys",
            "ToggleKey",
            KeyCode.End,
            "Key to toggle head tracking on/off. " +
            "Press this key during gameplay to enable or disable head tracking."
        );

        // The bound entry keeps its original name so an existing user config still
        // resolves; only the property was renamed to match what the key now does.
        CycleTrackingModeKey = config.Bind(
            "Hotkeys",
            "TogglePositionKey",
            KeyCode.PageUp,
            "Key to cycle tracking mode: rotation+position -> rotation only -> position only -> back to start. " +
            "Ctrl+Shift+G is also bound for keyboards without a Page Up key."
        );

        YawModeKey = config.Bind(
            "Hotkeys",
            "YawModeKey",
            KeyCode.PageDown,
            "Key to toggle world-space (horizon-locked) vs camera-local yaw. " +
            "Ctrl+Shift+H is also bound for keyboards without a Page Down key."
        );
    }

    private void BindBehavior(ConfigFile config)
    {
        PauseOnLostFocus = config.Bind(
            "Behavior",
            "PauseOnLostFocus",
            true,
            "Pause head tracking when the game window loses focus. " +
            "This prevents unwanted camera movement when switching to other applications."
        );

        DiagnosticLogging = config.Bind(
            "Behavior",
            "DiagnosticLogging",
            false,
            "Write the camera rig, frame-phase and pose probes to HeadTracking.log. " +
            "Only turn this on when asked for a diagnostic log - it writes several " +
            "lines every few seconds."
        );
    }

    private void BindCamera(ConfigFile config)
    {
        FieldOfViewOffset = config.Bind(
            "Camera",
            "FieldOfViewOffset",
            0.0f,
            new ConfigDescription(
                "Degrees added to the field of view the game renders with, on top of the " +
                "Field of View slider in the game's own settings. 0 leaves the game's value " +
                "alone; raise it to see wider than that slider allows. The game's sprint kick " +
                "and interaction zoom still work, and the crosshair compensation follows the " +
                "change automatically.",
                new AcceptableValueRange<float>(-30f, 60f)
            )
        );
    }

    private void BindPosition(ConfigFile config)
    {
        PositionEnabled = config.Bind(
            "Position",
            "PositionEnabled",
            true,
            "Enable positional tracking (lean in/out/side-to-side)"
        );

        PositionSensitivityX = config.Bind(
            "Position",
            "PositionSensitivityX",
            2.0f,
            new ConfigDescription(
                "Multiplier for lateral (left/right) position",
                new AcceptableValueRange<float>(0f, 3.0f)
            )
        );

        PositionSensitivityY = config.Bind(
            "Position",
            "PositionSensitivityY",
            2.0f,
            new ConfigDescription(
                "Multiplier for vertical (up/down) position",
                new AcceptableValueRange<float>(0f, 3.0f)
            )
        );

        PositionSensitivityZ = config.Bind(
            "Position",
            "PositionSensitivityZ",
            2.0f,
            new ConfigDescription(
                "Multiplier for depth (forward/back) position",
                new AcceptableValueRange<float>(0f, 3.0f)
            )
        );

        PositionLimitX = config.Bind(
            "Position",
            "PositionLimitX",
            0.30f,
            new ConfigDescription(
                "Maximum lateral displacement in meters",
                new AcceptableValueRange<float>(0.01f, 0.5f)
            )
        );

        PositionLimitY = config.Bind(
            "Position",
            "PositionLimitY",
            0.15f,
            new ConfigDescription(
                "Maximum upward vertical displacement in meters",
                new AcceptableValueRange<float>(0.01f, 0.5f)
            )
        );

        PositionLimitYDown = config.Bind(
            "Position",
            "PositionLimitYDown",
            0.05f,
            new ConfigDescription(
                "Maximum downward vertical displacement in meters",
                new AcceptableValueRange<float>(0f, 0.5f)
            )
        );

        PositionLimitZ = config.Bind(
            "Position",
            "PositionLimitZ",
            0.40f,
            new ConfigDescription(
                "Maximum forward displacement in meters",
                new AcceptableValueRange<float>(0.01f, 0.5f)
            )
        );

        PositionLimitZBack = config.Bind(
            "Position",
            "PositionLimitZBack",
            0.10f,
            new ConfigDescription(
                "Maximum backward displacement in meters. Leaning back is restricted more tightly than leaning forward to stop the camera pulling into the player body.",
                new AcceptableValueRange<float>(0.01f, 0.5f)
            )
        );

        LocalSmoothing = config.Bind(
            "Smoothing",
            "LocalSmoothing",
            0.0f,
            new ConfigDescription(
                "Smoothing applied when the tracker runs on this machine (loopback). " +
                "0 = no smoothing, 1 = heavy. Covers rotation and position.",
                new AcceptableValueRange<float>(0f, 1f)
            )
        );

        RemoteSmoothing = config.Bind(
            "Smoothing",
            "RemoteSmoothing",
            0.15f,
            new ConfigDescription(
                "Smoothing applied when the tracker is a remote device on the network. " +
                "0 = no smoothing, 1 = heavy. Covers rotation and position.",
                new AcceptableValueRange<float>(0f, 1f)
            )
        );
    }
}
