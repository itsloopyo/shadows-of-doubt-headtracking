// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using UnityEngine;

namespace ShadowsOfDoubtHeadTracking.Configuration;

/// <summary>The settings the plugin runs on, read once at startup.</summary>
internal sealed class ModConfig
{
    public float YawSensitivity { get; set; }
    public float PitchSensitivity { get; set; }
    public float RollSensitivity { get; set; }
    public bool EnabledOnStartup { get; set; }
    public bool WorldSpaceYaw { get; set; }
    public bool InvertYaw { get; set; }
    public bool InvertPitch { get; set; }
    public bool InvertRoll { get; set; }
    public KeyCode ToggleKey { get; set; }
    public KeyCode CycleTrackingModeKey { get; set; }
    public KeyCode YawModeKey { get; set; }
    public bool PauseOnLostFocus { get; set; }
    public bool DiagnosticLogging { get; set; }
    public float FieldOfViewOffset { get; set; }
    public bool PositionEnabled { get; set; }
    public float PositionSensitivityX { get; set; }
    public float PositionSensitivityY { get; set; }
    public float PositionSensitivityZ { get; set; }
    public float PositionLimitX { get; set; }
    public float PositionLimitY { get; set; }
    public float PositionLimitYDown { get; set; }
    public float PositionLimitZ { get; set; }
    public float PositionLimitZBack { get; set; }
    public float LocalSmoothing { get; set; }
    public float RemoteSmoothing { get; set; }
}
