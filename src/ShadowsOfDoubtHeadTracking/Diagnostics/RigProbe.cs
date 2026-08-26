// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using ShadowsOfDoubtHeadTracking.Core;
using ShadowsOfDoubtHeadTracking.Utilities;
using UnityEngine;

namespace ShadowsOfDoubtHeadTracking.Diagnostics;

/// <summary>
/// TEMPORARY reverse-engineering scaffolding. Answers what is still unknown about
/// how Shadows of Doubt drives its camera and HUD, then gets deleted:
///
/// 1. Which transforms in the camera rig does the game rewrite, and in which Unity
///    phase? (decides which ancestor can safely carry the position offset, and
///    whether our LateUpdate really runs after the game's camera controller)
/// 2. Is the camera we write the camera that renders the frame, and does the view
///    matrix we write survive to the next frame?
/// 3. Does the game move the crosshair itself?
/// 4. Does the game route world-to-screen and screen-to-ray through Unity's Camera
///    helpers at all?
///
/// Nothing here may throw into the tracking path. It runs after the camera has
/// already been written, so a fault in the scaffolding cannot stop the frame. Note
/// that generic <c>GetComponents&lt;T&gt;()</c> does not exist under IL2CPP interop
/// and throws MissingMethodException at runtime; do not reintroduce it here.
/// </summary>
internal static class RigProbe
{
    /// <summary>
    /// Off unless the user turns DiagnosticLogging on. The periodic reports below
    /// are several lines every few seconds, which is not what a normal session's
    /// log should be made of.
    /// </summary>
    internal static bool Enabled;

    private const int RigChainDepth = 4;
    private const int ReportIntervalFrames = 300;

    // Resolving a game type walks every loaded assembly and the IL2CPP interop ones
    // throw on GetTypes(), so the one-shot searches retry on an interval rather than
    // every frame.
    private const int SearchRetryFrames = 60;

    private const int ReticleSubtreeDepth = 2;

    // Tolerance for calling a read-back matrix element unchanged.
    private const float MatrixElementEpsilon = 1e-5f;

    internal const int PhaseFixedUpdate = 0;
    internal const int PhaseUpdate = 1;
    internal const int PhaseLateUpdatePre = 2;
    internal const int PhaseLateUpdatePost = 3;

    private static readonly string[] PhaseNames = { "fixed", "update", "late-pre", "late-post" };

    private static readonly Vector3[,] Positions = new Vector3[4, RigChainDepth];
    private static readonly Quaternion[,] Rotations = new Quaternion[4, RigChainDepth];
    private static readonly Vector2[] Reticle = new Vector2[4];

    private static readonly Transform?[] Chain = new Transform?[RigChainDepth];
    private static RectTransform? _crosshair;

    private static bool _dumpedRig;
    private static bool _dumpedReticle;
    private static bool _dumpedFieldOfView;
    private static bool _matrixSurvived;
    private static int _frameCounter;
    private static int _reticleSearchCounter;
    private static int _fieldOfViewSearchCounter;

    internal static int WorldToScreenCalls;
    internal static int ScreenToRayCalls;

    internal static void DumpRig(Transform cameraTransform, UnityEngine.Camera camera)
    {
        if (!Enabled || _dumpedRig) return;
        _dumpedRig = true;

        var log = HeadTrackingPlugin.Logger;
        log.LogInfo("RIG ==================== camera rig dump ====================");
        log.LogInfo($"RIG HDRP ShaderConfig.s_CameraRelativeRendering = {ReadCameraRelativeRendering()} " +
                    "(1 means the view matrix translation is discarded and the world camera " +
                    "position is taken from transform.position)");

        var t = cameraTransform;
        int depth = 0;
        while (t != null)
        {
            if (depth < RigChainDepth) Chain[depth] = t;

            log.LogInfo($"RIG [{depth}] {t.name} localPos={Fmt(t.localPosition)} " +
                        $"localEuler={Fmt(t.localEulerAngles)}");

            t = t.parent;
            depth++;
        }

        log.LogInfo($"RIG chain depth to root = {depth}");
        DumpCameraCensus(camera);
        DumpFirstPersonControllerRig();
    }

    /// <summary>
    /// Lists every live camera, so a stable view while the matrix is being written
    /// can be told apart from writing to a camera that is not the one rendering.
    /// </summary>
    private static void DumpCameraCensus(UnityEngine.Camera active)
    {
        var log = HeadTrackingPlugin.Logger;
        var all = UnityEngine.Camera.allCameras;
        log.LogInfo($"RIG camera count = {all.Length}, active = {active.name}");

        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i];
            if (c == null) continue;
            log.LogInfo($"RIG cam[{i}] {c.name} enabled={c.enabled} depth={c.depth} " +
                        $"type={c.cameraType} fov={c.fieldOfView:F1} " +
                        $"hasTarget={(c.targetTexture != null)} isActiveOne={(c == active)} " +
                        $"path={Path(c.transform)}");
        }
    }

    /// <summary>
    /// Reports the transforms the game's own first-person controller holds, so the
    /// offset transform can be chosen from the game's declared rig rather than from
    /// a guess about hierarchy depth.
    /// </summary>
    private static void DumpFirstPersonControllerRig()
    {
        var log = HeadTrackingPlugin.Logger;

        var playerType = AccessTools.TypeByName("Player");
        object? player = GameMembers.ReadSingleton(playerType);
        if (player == null)
        {
            log.LogWarning("RIG Player.Instance is null - cannot reach FirstPersonController");
            return;
        }

        LogTransformMember(playerType!, player, "camHeightParent");
        LogTransformMember(playerType!, player, "playerContainer");

        object? fps = GameMembers.ReadMember(playerType!, player, "fps");
        if (fps == null)
        {
            log.LogWarning("RIG Player.fps is null");
            return;
        }

        var fpcType = fps.GetType();
        LogTransformMember(fpcType, fps, "leanPivot");

        var camera = GameMembers.ReadMember(fpcType, fps, "m_Camera") as UnityEngine.Camera;
        log.LogInfo($"RIG FirstPersonController.m_Camera = {Path(camera != null ? camera.transform : null)}");
        log.LogInfo($"RIG FirstPersonController transform = {Path(((Component)fps).transform)}");
    }

    internal static void DumpReticle()
    {
        if (!Enabled || _dumpedReticle) return;

        // InterfaceControls does not exist until the HUD spawns.
        if (++_reticleSearchCounter % SearchRetryFrames != 0) return;

        var controlsType = AccessTools.TypeByName("InterfaceControls");
        object? controls = GameMembers.ReadSingleton(controlsType);
        if (controls == null) return;

        var container = GameMembers.ReadMember(controlsType!, controls, "reticleContainer") as RectTransform;
        if (container == null) return;

        _dumpedReticle = true;
        var log = HeadTrackingPlugin.Logger;
        log.LogInfo($"RETICLE container = {Path(container)} anchored={Fmt(container.anchoredPosition)} " +
                    $"sizeDelta={Fmt(container.sizeDelta)} children={container.childCount}");

        var canvas = container.GetComponentInParent<Canvas>();
        log.LogInfo($"RETICLE canvas = {Path(canvas != null ? canvas.transform : null)} " +
                    $"scaleFactor={(canvas != null ? canvas.scaleFactor : 0f):F3} " +
                    $"renderMode={(canvas != null ? canvas.renderMode.ToString() : "n/a")}");

        DumpReticleSubtree(container, 0);
    }

    private static void DumpReticleSubtree(Transform parent, int depth)
    {
        if (depth > ReticleSubtreeDepth) return;

        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            var rect = child.GetComponent<RectTransform>();

            var sb = new StringBuilder("RETICLE ");
            sb.Append(' ', depth * 2)
              .Append("- ").Append(child.name)
              .Append(" active=").Append(child.gameObject.activeSelf);
            if (rect != null)
            {
                sb.Append(" anchored=").Append(Fmt(rect.anchoredPosition))
                  .Append(" size=").Append(Fmt(rect.sizeDelta));
            }
            HeadTrackingPlugin.Logger.LogInfo(sb.ToString());

            if (child.name == "Crosshairs" && rect != null) _crosshair = rect;

            DumpReticleSubtree(child, depth + 1);
        }
    }

    /// <summary>
    /// Records the rig chain at one point in the Unity frame.
    /// </summary>
    internal static void Sample(int phase)
    {
        if (!Enabled) return;

        for (int i = 0; i < RigChainDepth; i++)
        {
            var t = Chain[i];
            if (t == null) continue;
            Positions[phase, i] = t.localPosition;
            Rotations[phase, i] = t.localRotation;
        }

        if (_crosshair != null)
        {
            Reticle[phase] = _crosshair.anchoredPosition;
        }
    }

    /// <summary>
    /// Compares the camera's current view matrix against the one we wrote last
    /// frame. Separates "the render pipeline ignored our matrix" from "something
    /// reset it before the frame drew".
    /// </summary>
    internal static void CheckMatrixSurvived(UnityEngine.Camera camera, Matrix4x4 written)
    {
        if (!Enabled) return;

        Matrix4x4 live = camera.worldToCameraMatrix;
        _matrixSurvived =
            Mathf.Abs(live.m00 - written.m00) < MatrixElementEpsilon &&
            Mathf.Abs(live.m02 - written.m02) < MatrixElementEpsilon &&
            Mathf.Abs(live.m11 - written.m11) < MatrixElementEpsilon &&
            Mathf.Abs(live.m20 - written.m20) < MatrixElementEpsilon &&
            Mathf.Abs(live.m22 - written.m22) < MatrixElementEpsilon;
    }

    /// <summary>
    /// Reports which transforms moved between which phases, which is what identifies
    /// the game's per-frame writes and where our own passes sit relative to them.
    /// </summary>
    internal static void Report()
    {
        if (!Enabled || ++_frameCounter % ReportIntervalFrames != 0) return;

        var log = HeadTrackingPlugin.Logger;
        for (int i = 0; i < RigChainDepth; i++)
        {
            var t = Chain[i];
            if (t == null) continue;

            var sb = new StringBuilder("PHASE [");
            sb.Append(i).Append("] ").Append(t.name).Append(' ');
            for (int p = 1; p < 4; p++)
            {
                sb.Append(PhaseNames[p - 1]).Append("->").Append(PhaseNames[p])
                  .Append(" dPos=").Append((Positions[p, i] - Positions[p - 1, i]).magnitude.ToString("F4"))
                  .Append(" dRot=").Append(Quaternion.Angle(Rotations[p, i], Rotations[p - 1, i]).ToString("F3"))
                  .Append(" | ");
            }
            log.LogInfo(sb.ToString());
        }

        if (_crosshair != null)
        {
            log.LogInfo($"PHASE crosshair fixed={Fmt(Reticle[0])} update={Fmt(Reticle[1])} " +
                        $"latePre={Fmt(Reticle[2])} latePost={Fmt(Reticle[3])}");
        }

        log.LogInfo($"PHASE projection-patch calls per {ReportIntervalFrames} frames: " +
                    $"WorldToScreen={WorldToScreenCalls} ScreenToRay={ScreenToRayCalls}");
        WorldToScreenCalls = 0;
        ScreenToRayCalls = 0;
    }

    /// <summary>
    /// Logs the head tracking result resolved into the clean camera's own frame,
    /// where the expected signs are unambiguous: fwd.x &gt; 0 is looking right,
    /// fwd.y &gt; 0 is looking up, up.x &gt; 0 is rolled right, and the offset is
    /// (right, up, forward) in metres.
    /// </summary>
    internal static void LogPose(Quaternion cleanRotation, Quaternion renderRotation, Vector3 worldOffset)
    {
        if (!Enabled || _frameCounter % ReportIntervalFrames != 0) return;

        Quaternion toLocal = Quaternion.Inverse(cleanRotation);
        Vector3 forward = toLocal * (renderRotation * Vector3.forward);
        Vector3 up = toLocal * (renderRotation * Vector3.up);
        Vector3 localOffset = toLocal * worldOffset;

        HeadTrackingPlugin.Logger.LogInfo(
            $"POSE fwd={Fmt(forward)} up={Fmt(up)} offsetLocal={Fmt(localOffset)} " +
            $"matrixSurvivedLastFrame={_matrixSurvived}");
    }

    /// <summary>
    /// Reports the game's own Field of View setting: the value, the default, and the
    /// bounds of the slider the settings menu drives it with. Answers what a mod-side
    /// field of view offset has to be measured against.
    /// </summary>
    internal static void DumpFieldOfViewSetting()
    {
        if (!Enabled || _dumpedFieldOfView) return;

        if (++_fieldOfViewSearchCounter % SearchRetryFrames != 0) return;

        var prefsType = AccessTools.TypeByName("PlayerPrefsController");
        if (prefsType == null)
        {
            _dumpedFieldOfView = true;
            HeadTrackingPlugin.Logger.LogWarning("FOV PlayerPrefsController type not found");
            return;
        }

        object? prefs = GameMembers.ReadSingleton(prefsType);
        if (prefs == null) return;

        object? settings = GameMembers.ReadMember(prefsType, prefs, "gameSettingControls");
        if (settings == null) return;

        _dumpedFieldOfView = true;

        var listType = settings.GetType();
        var countProperty = listType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
        var itemMethod = listType.GetMethod("get_Item", BindingFlags.Public | BindingFlags.Instance);
        if (countProperty == null || itemMethod == null)
        {
            HeadTrackingPlugin.Logger.LogWarning(
                $"FOV cannot enumerate {listType.FullName} - Count/get_Item missing");
            return;
        }
        var log = HeadTrackingPlugin.Logger;

        int count = (int)countProperty.GetGetMethod()!.Invoke(settings, null)!;
        for (int i = 0; i < count; i++)
        {
            object? setting = itemMethod.Invoke(settings, new object[] { i });
            if (setting == null) continue;

            var settingType = setting.GetType();
            if (GameMembers.ReadMember(settingType, setting, "identifier") as string != "fpsfov") continue;

            object? slider = GameMembers.ReadMember(settingType, setting, "slider");
            string bounds = "no slider";
            if (slider != null)
            {
                var sliderType = slider.GetType();
                bounds = $"min={GameMembers.ReadMember(sliderType, slider, "minValue")} " +
                         $"max={GameMembers.ReadMember(sliderType, slider, "maxValue")} " +
                         $"whole={GameMembers.ReadMember(sliderType, slider, "wholeNumbers")} " +
                         $"value={GameMembers.ReadMember(sliderType, slider, "value")}";
            }

            log.LogInfo($"FOV setting fpsfov: value={GameMembers.ReadMember(settingType, setting, "intValue")} " +
                        $"default={GameMembers.ReadMember(settingType, setting, "intDefault")} slider {bounds}");
            return;
        }

        log.LogWarning("FOV setting fpsfov not found in PlayerPrefsController.gameSettingControls");
    }

    private static void LogTransformMember(Type type, object instance, string name)
    {
        HeadTrackingPlugin.Logger.LogInfo(
            $"RIG {type.Name}.{name} = {Path(GameMembers.ReadMember(type, instance, name) as Transform)}");
    }

    private static int ReadCameraRelativeRendering()
    {
        var type = AccessTools.TypeByName("UnityEngine.Rendering.HighDefinition.ShaderConfig");
        if (type == null) return -1;

        object? value = type.GetProperty("s_CameraRelativeRendering", BindingFlags.Public | BindingFlags.Static)
            ?.GetGetMethod()?.Invoke(null, null);
        if (value is int i) return i;

        return type.GetField("s_CameraRelativeRendering", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null) is int fi ? fi : -1;
    }

    private static string Fmt(Vector3 v) => $"({v.x:F3},{v.y:F3},{v.z:F3})";

    private static string Fmt(Vector2 v) => $"({v.x:F1},{v.y:F1})";

    private static string Path(Transform? t)
    {
        return t == null ? "<null>" : TransformPath.GetFullPath(t);
    }
}
