// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using CameraUnlock.Core.Config;
using ShadowsOfDoubtHeadTracking.Legacy;

namespace ShadowsOfDoubtHeadTracking.Configuration;

/// <summary>
/// Everything the mod reads from BepInEx\config\CameraUnlock.ini. Unity-free, so the test
/// project compiles it and holds the committed file to it.
///
/// There are deliberately no sensitivity, deadzone, response-curve or axis-inversion
/// settings here. The tracker owns pose shaping: OpenTrack, a phone app or a headset
/// each already have those controls, and configuring them once there is what makes a
/// single profile behave the same across every game. The axis signs and the position
/// multiplier this game needs are a fixed conversion applied in code, not a knob.
/// </summary>
internal sealed class ShadowsOfDoubtConfig : HeadTrackingConfigData
{
    /// <summary>The game's name as data/games.json spells it.</summary>
    public const string DisplayName = "Shadows of Doubt";

    public bool PauseOnLostFocus { get; set; } = true;

    public bool DiagnosticLogging { get; set; }

    public float FieldOfViewOffset { get; set; }

    public static ConfigTable<ShadowsOfDoubtConfig> Table()
    {
        return HeadTrackingConfigTable.Create<ShadowsOfDoubtConfig>(
                ConfigConcepts.EnableOnStartup,
                ConfigConcepts.WorldSpaceYaw,
                ConfigConcepts.RotationEnabled,
                ConfigConcepts.LocalSmoothing,
                ConfigConcepts.RemoteSmoothing,
                ConfigConcepts.PositionEnabled,
                ConfigConcepts.PositionLimitX,
                ConfigConcepts.PositionLimitY,
                ConfigConcepts.PositionLimitYDown,
                ConfigConcepts.PositionLimitZ,
                ConfigConcepts.PositionLimitZBack,
                ConfigConcepts.ToggleKey,
                ConfigConcepts.CycleTrackingModeKey,
                ConfigConcepts.YawModeKey)
            .Select(ConfigConcepts.WorldSpaceYaw).Writable()
            .Select(ConfigConcepts.RotationEnabled).Writable()
            .Select(ConfigConcepts.PositionEnabled).Writable()
            .Local("General", "PauseOnLostFocus", c => c.PauseOnLostFocus, (c, v) => c.PauseOnLostFocus = v,
                new BoolCodec(),
                "true: head tracking stops moving the view while the game window is not focused.")
            .Local("Camera", "FieldOfViewOffset", c => c.FieldOfViewOffset, (c, v) => c.FieldOfViewOffset = v,
                new FloatCodec(),
                "Degrees added to the field of view the game renders with, on top of the\n" +
                "Field of View slider in the game's own settings. 0 leaves the game's value alone.\n" +
                "The game's sprint kick and interaction zoom still work on top of it.")
            .Range(-30, 60)
            .Local("Diagnostics", "DiagnosticLogging", c => c.DiagnosticLogging, (c, v) => c.DiagnosticLogging = v,
                new BoolCodec(),
                "true: write the camera rig, frame-phase and pose probes to HeadTracking.log.\n" +
                "Verbose; turn it on when reporting a problem.");
    }

    /// <summary>
    /// The config owner's options for <paramref name="path"/>, importing the published builds'
    /// .cfg at <paramref name="legacyPath"/> while it is absent. The mod passes
    /// <see cref="DefaultsFile.PerUser"/>, and a test a scratch file.
    /// </summary>
    public static ConfigOwnerOptions<ShadowsOfDoubtConfig> Options(string path, string legacyPath, DefaultsFile defaults)
    {
        return new ConfigOwnerOptions<ShadowsOfDoubtConfig>
        {
            Path = path,
            Table = Table(),
            Import = LegacyConfigImport.Create(),
            LegacySourcePath = legacyPath,
            Header = new RenderHeader(DisplayName),
            Defaults = defaults,
        };
    }
}
