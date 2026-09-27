// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System.IO;
using BepInEx.Configuration;

namespace ShadowsOfDoubtHeadTracking.Legacy;

/// <summary>
/// The plugin's BepInEx Bind calls as the published builds ran them, each definition's section,
/// key, type, description and acceptable values unchanged and its default taken from
/// <see cref="LegacyConfig"/>. Frozen for the life of the repo: it is how a player's .cfg is read,
/// whichever earlier build wrote it.
/// <para>
/// It writes nothing. BepInEx's ConfigFile read the .cfg in its constructor, before whoever calls
/// this held the file, so saving on set is turned off first and the file is read again before
/// anything is bound. A missing file reads as the defaults.
/// </para>
/// </summary>
internal static class LegacyConfigReader
{
    /// <summary>Reads <paramref name="config"/>'s file into a new <see cref="LegacyConfig"/>.</summary>
    /// <param name="found">Whether the file existed.</param>
    public static LegacyConfig Read(ConfigFile config, out bool found)
    {
        config.SaveOnConfigSet = false;
        found = File.Exists(config.ConfigFilePath);
        if (found)
        {
            config.Reload();
        }

        var read = new LegacyConfig();

        read.YawSensitivity = config.Bind(
            "Sensitivity",
            "YawSensitivity",
            read.YawSensitivity,
            new ConfigDescription(
                "Yaw (horizontal) rotation sensitivity multiplier. " +
                "Use negative values to invert axis.",
                new AcceptableValueRange<float>(-5f, 5f)
            )
        ).Value;

        read.PitchSensitivity = config.Bind(
            "Sensitivity",
            "PitchSensitivity",
            read.PitchSensitivity,
            new ConfigDescription(
                "Pitch (vertical) rotation sensitivity multiplier. " +
                "Use negative values to invert axis.",
                new AcceptableValueRange<float>(-5f, 5f)
            )
        ).Value;

        read.RollSensitivity = config.Bind(
            "Sensitivity",
            "RollSensitivity",
            read.RollSensitivity,
            new ConfigDescription(
                "Roll rotation sensitivity multiplier. " +
                "Use negative values to invert axis.",
                new AcceptableValueRange<float>(-5f, 5f)
            )
        ).Value;

        read.EnabledOnStartup = config.Bind(
            "General",
            "EnabledOnStartup",
            read.EnabledOnStartup,
            "Enable head tracking automatically when the game starts. " +
            "If false, press End to enable manually."
        ).Value;

        read.WorldSpaceYaw = config.Bind(
            "General",
            "WorldSpaceYaw",
            read.WorldSpaceYaw,
            "Yaw mode: true = horizon-locked yaw (default), false = camera-local. " +
            "Horizon-locked keeps 'up' constant when looking at extreme pitches; " +
            "camera-local follows the camera's current up-axis. " +
            "Toggle at runtime with Page Down or Ctrl+Shift+H."
        ).Value;

        read.InvertYaw = config.Bind(
            "CoordinateTransform",
            "InvertYaw",
            read.InvertYaw,
            "Invert the yaw (horizontal) axis. " +
            "Enable if turning your head right makes the camera turn left."
        ).Value;

        read.InvertPitch = config.Bind(
            "CoordinateTransform",
            "InvertPitch",
            read.InvertPitch,
            "Invert the pitch (vertical) axis. " +
            "This is enabled by default to convert OpenTrack coordinates to Unity. " +
            "Disable if looking up makes the camera look down."
        ).Value;

        read.InvertRoll = config.Bind(
            "CoordinateTransform",
            "InvertRoll",
            read.InvertRoll,
            "Invert the roll (tilt) axis. " +
            "Enable if tilting your head right makes the camera tilt left."
        ).Value;

        read.ToggleKey = config.Bind(
            "Hotkeys",
            "ToggleKey",
            read.ToggleKey,
            "Key to toggle head tracking on/off. " +
            "Press this key during gameplay to enable or disable head tracking."
        ).Value;

        read.CycleTrackingModeKey = config.Bind(
            "Hotkeys",
            "TogglePositionKey",
            read.CycleTrackingModeKey,
            "Key to cycle tracking mode: rotation+position -> rotation only -> position only -> back to start. " +
            "Ctrl+Shift+G is also bound for keyboards without a Page Up key."
        ).Value;

        read.YawModeKey = config.Bind(
            "Hotkeys",
            "YawModeKey",
            read.YawModeKey,
            "Key to toggle world-space (horizon-locked) vs camera-local yaw. " +
            "Ctrl+Shift+H is also bound for keyboards without a Page Down key."
        ).Value;

        read.PauseOnLostFocus = config.Bind(
            "Behavior",
            "PauseOnLostFocus",
            read.PauseOnLostFocus,
            "Pause head tracking when the game window loses focus. " +
            "This prevents unwanted camera movement when switching to other applications."
        ).Value;

        read.DiagnosticLogging = config.Bind(
            "Behavior",
            "DiagnosticLogging",
            read.DiagnosticLogging,
            "Write the camera rig, frame-phase and pose probes to HeadTracking.log. " +
            "Only turn this on when asked for a diagnostic log - it writes several " +
            "lines every few seconds."
        ).Value;

        read.FieldOfViewOffset = config.Bind(
            "Camera",
            "FieldOfViewOffset",
            read.FieldOfViewOffset,
            new ConfigDescription(
                "Degrees added to the field of view the game renders with, on top of the " +
                "Field of View slider in the game's own settings. 0 leaves the game's value " +
                "alone; raise it to see wider than that slider allows. The game's sprint kick " +
                "and interaction zoom still work, and the crosshair compensation follows the " +
                "change automatically.",
                new AcceptableValueRange<float>(-30f, 60f)
            )
        ).Value;

        read.PositionEnabled = config.Bind(
            "Position",
            "PositionEnabled",
            read.PositionEnabled,
            "Enable positional tracking (lean in/out/side-to-side)"
        ).Value;

        read.PositionSensitivityX = config.Bind(
            "Position",
            "PositionSensitivityX",
            read.PositionSensitivityX,
            new ConfigDescription(
                "Multiplier for lateral (left/right) position",
                new AcceptableValueRange<float>(0f, 3.0f)
            )
        ).Value;

        read.PositionSensitivityY = config.Bind(
            "Position",
            "PositionSensitivityY",
            read.PositionSensitivityY,
            new ConfigDescription(
                "Multiplier for vertical (up/down) position",
                new AcceptableValueRange<float>(0f, 3.0f)
            )
        ).Value;

        read.PositionSensitivityZ = config.Bind(
            "Position",
            "PositionSensitivityZ",
            read.PositionSensitivityZ,
            new ConfigDescription(
                "Multiplier for depth (forward/back) position",
                new AcceptableValueRange<float>(0f, 3.0f)
            )
        ).Value;

        read.PositionLimitX = config.Bind(
            "Position",
            "PositionLimitX",
            read.PositionLimitX,
            new ConfigDescription(
                "Maximum lateral displacement in meters",
                new AcceptableValueRange<float>(0.01f, 0.5f)
            )
        ).Value;

        read.PositionLimitY = config.Bind(
            "Position",
            "PositionLimitY",
            read.PositionLimitY,
            new ConfigDescription(
                "Maximum upward vertical displacement in meters",
                new AcceptableValueRange<float>(0.01f, 0.5f)
            )
        ).Value;

        read.PositionLimitYDown = config.Bind(
            "Position",
            "PositionLimitYDown",
            read.PositionLimitYDown,
            new ConfigDescription(
                "Maximum downward vertical displacement in meters",
                new AcceptableValueRange<float>(0f, 0.5f)
            )
        ).Value;

        read.PositionLimitZ = config.Bind(
            "Position",
            "PositionLimitZ",
            read.PositionLimitZ,
            new ConfigDescription(
                "Maximum forward displacement in meters",
                new AcceptableValueRange<float>(0.01f, 0.5f)
            )
        ).Value;

        read.PositionLimitZBack = config.Bind(
            "Position",
            "PositionLimitZBack",
            read.PositionLimitZBack,
            new ConfigDescription(
                "Maximum backward displacement in meters. Leaning back is restricted more tightly than leaning forward to stop the camera pulling into the player body.",
                new AcceptableValueRange<float>(0.01f, 0.5f)
            )
        ).Value;

        read.LocalSmoothing = config.Bind(
            "Smoothing",
            "LocalSmoothing",
            read.LocalSmoothing,
            new ConfigDescription(
                "Smoothing applied when the tracker runs on this machine (loopback). " +
                "0 = no smoothing, 1 = heavy. Covers rotation and position.",
                new AcceptableValueRange<float>(0f, 1f)
            )
        ).Value;

        read.RemoteSmoothing = config.Bind(
            "Smoothing",
            "RemoteSmoothing",
            read.RemoteSmoothing,
            new ConfigDescription(
                "Smoothing applied when the tracker is a remote device on the network. " +
                "0 = no smoothing, 1 = heavy. Covers rotation and position.",
                new AcceptableValueRange<float>(0f, 1f)
            )
        ).Value;

        return read;
    }
}
