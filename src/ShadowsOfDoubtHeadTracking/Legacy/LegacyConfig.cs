// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using UnityEngine;

namespace ShadowsOfDoubtHeadTracking.Legacy;

/// <summary>
/// The settings every published build read from
/// BepInEx\config\com.headtracking.shadowsofdoubt.cfg, with their defaults. Frozen: a later
/// change to the runtime settings must never change what an old .cfg, or one missing a key,
/// reads as.
/// </summary>
internal sealed class LegacyConfig
{
    public float YawSensitivity = 1.0f;
    public float PitchSensitivity = 1.0f;
    public float RollSensitivity = 1.0f;

    public bool EnabledOnStartup = true;
    public bool WorldSpaceYaw = true;

    public bool InvertYaw = false;
    public bool InvertPitch = true;
    public bool InvertRoll = false;

    public KeyCode ToggleKey = KeyCode.End;
    public KeyCode CycleTrackingModeKey = KeyCode.PageUp;
    public KeyCode YawModeKey = KeyCode.PageDown;

    public bool PauseOnLostFocus = true;
    public bool DiagnosticLogging = false;

    public float FieldOfViewOffset = 0.0f;

    public bool PositionEnabled = true;
    public float PositionSensitivityX = 2.0f;
    public float PositionSensitivityY = 2.0f;
    public float PositionSensitivityZ = 2.0f;
    public float PositionLimitX = 0.30f;
    public float PositionLimitY = 0.15f;
    public float PositionLimitYDown = 0.05f;
    public float PositionLimitZ = 0.40f;
    public float PositionLimitZBack = 0.10f;

    public float LocalSmoothing = 0.0f;
    public float RemoteSmoothing = 0.15f;
}
