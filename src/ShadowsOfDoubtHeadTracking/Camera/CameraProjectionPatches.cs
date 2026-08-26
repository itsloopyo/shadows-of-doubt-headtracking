// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using HarmonyLib;
using ShadowsOfDoubtHeadTracking.Core;
using ShadowsOfDoubtHeadTracking.Diagnostics;
using UnityEngine;

namespace ShadowsOfDoubtHeadTracking.Camera;

/// <summary>
/// Harmony patches that split Unity's camera projection helpers along the
/// look/aim boundary while head tracking is active.
///
/// World-to-screen goes through the head-tracked view, because those results
/// position UI over what the player can see. Screen-to-ray goes through the
/// clean camera pose, because those results drive interaction raycasts and
/// picking, which must behave exactly as they would unmodded - Unity would
/// otherwise derive the ray from the head-tracked <c>worldToCameraMatrix</c>
/// and drag the player's aim along with their head.
///
/// Calls on any other camera pass through to the original implementation.
/// </summary>
[HarmonyPatch(typeof(UnityEngine.Camera))]
public static class CameraProjectionPatches
{
    // --- WorldToScreenPoint: both overloads ---

    [HarmonyPatch(nameof(UnityEngine.Camera.WorldToScreenPoint), new[] { typeof(Vector3) })]
    [HarmonyPrefix]
    public static bool WorldToScreenPoint(UnityEngine.Camera __instance, Vector3 position, ref Vector3 __result)
    {
        if (!ShouldIntercept(__instance)) return true;
        ProjectToScreen(position, out __result);
        return false;
    }

    [HarmonyPatch(nameof(UnityEngine.Camera.WorldToScreenPoint),
        new[] { typeof(Vector3), typeof(UnityEngine.Camera.MonoOrStereoscopicEye) })]
    [HarmonyPrefix]
    public static bool WorldToScreenPointEye(UnityEngine.Camera __instance, Vector3 position, ref Vector3 __result)
    {
        if (!ShouldIntercept(__instance)) return true;
        ProjectToScreen(position, out __result);
        return false;
    }

    // --- WorldToViewportPoint: both overloads ---

    [HarmonyPatch(nameof(UnityEngine.Camera.WorldToViewportPoint), new[] { typeof(Vector3) })]
    [HarmonyPrefix]
    public static bool WorldToViewportPoint(UnityEngine.Camera __instance, Vector3 position, ref Vector3 __result)
    {
        if (!ShouldIntercept(__instance)) return true;
        ProjectToViewport(position, out __result);
        return false;
    }

    [HarmonyPatch(nameof(UnityEngine.Camera.WorldToViewportPoint),
        new[] { typeof(Vector3), typeof(UnityEngine.Camera.MonoOrStereoscopicEye) })]
    [HarmonyPrefix]
    public static bool WorldToViewportPointEye(UnityEngine.Camera __instance, Vector3 position, ref Vector3 __result)
    {
        if (!ShouldIntercept(__instance)) return true;
        ProjectToViewport(position, out __result);
        return false;
    }

    // --- ScreenPointToRay: both overloads ---

    [HarmonyPatch(nameof(UnityEngine.Camera.ScreenPointToRay), new[] { typeof(Vector3) })]
    [HarmonyPrefix]
    public static bool ScreenPointToRay(UnityEngine.Camera __instance, Vector3 pos, ref Ray __result)
    {
        if (!ShouldIntercept(__instance)) return true;
        ComputeCleanRay(
            pos.x * TrackedView.ActiveInvPixelWidth,
            pos.y * TrackedView.ActiveInvPixelHeight,
            out __result);
        return false;
    }

    [HarmonyPatch(nameof(UnityEngine.Camera.ScreenPointToRay),
        new[] { typeof(Vector3), typeof(UnityEngine.Camera.MonoOrStereoscopicEye) })]
    [HarmonyPrefix]
    public static bool ScreenPointToRayEye(UnityEngine.Camera __instance, Vector3 pos, ref Ray __result)
    {
        if (!ShouldIntercept(__instance)) return true;
        ComputeCleanRay(
            pos.x * TrackedView.ActiveInvPixelWidth,
            pos.y * TrackedView.ActiveInvPixelHeight,
            out __result);
        return false;
    }

    // --- ViewportPointToRay: both overloads ---

    [HarmonyPatch(nameof(UnityEngine.Camera.ViewportPointToRay), new[] { typeof(Vector3) })]
    [HarmonyPrefix]
    public static bool ViewportPointToRay(UnityEngine.Camera __instance, Vector3 pos, ref Ray __result)
    {
        if (!ShouldIntercept(__instance)) return true;
        ComputeCleanRay(pos.x, pos.y, out __result);
        return false;
    }

    [HarmonyPatch(nameof(UnityEngine.Camera.ViewportPointToRay),
        new[] { typeof(Vector3), typeof(UnityEngine.Camera.MonoOrStereoscopicEye) })]
    [HarmonyPrefix]
    public static bool ViewportPointToRayEye(UnityEngine.Camera __instance, Vector3 pos, ref Ray __result)
    {
        if (!ShouldIntercept(__instance)) return true;
        ComputeCleanRay(pos.x, pos.y, out __result);
        return false;
    }

    // --- Shared helpers ---

    private static bool ShouldIntercept(UnityEngine.Camera cam)
    {
        // The explicit `ActiveCamera != null` check is redundant: `cam` is the
        // Harmony __instance for a Camera method call, so `cam == ActiveCamera`
        // already encodes "ActiveCamera is the same live native object as cam".
        return ModState.Instance.IsEnabled
            && TrackedView.HasActiveViewMatrix
            && cam == TrackedView.ActiveCamera;
    }

    private static void ProjectToScreen(Vector3 worldPos, out Vector3 result)
    {
        RigProbe.WorldToScreenCalls++;
        Vector3 ndc = TrackedView.RenderVPMatrix.MultiplyPoint(worldPos);
        result = new Vector3(
            (ndc.x * 0.5f + 0.5f) * TrackedView.ActivePixelWidth,
            (ndc.y * 0.5f + 0.5f) * TrackedView.ActivePixelHeight,
            DistanceInFront(worldPos));
    }

    private static void ProjectToViewport(Vector3 worldPos, out Vector3 result)
    {
        RigProbe.WorldToScreenCalls++;
        Vector3 ndc = TrackedView.RenderVPMatrix.MultiplyPoint(worldPos);
        result = new Vector3(
            ndc.x * 0.5f + 0.5f,
            ndc.y * 0.5f + 0.5f,
            DistanceInFront(worldPos));
    }

    /// <summary>
    /// Unity reports the projected point's z as world-space distance in FRONT of
    /// the camera, positive when visible. Unity's view space looks down -Z, so the
    /// view-space z of a point in front is negative and has to be negated to match.
    /// Callers test <c>z &gt; 0</c> to decide whether to draw at all, so getting the
    /// sign wrong hides every world-anchored marker in the game.
    /// </summary>
    private static float DistanceInFront(Vector3 worldPos)
    {
        // Inlined dot product against the view matrix's third row. That row is
        // cached as scalar fields so we skip the 64-byte Matrix4x4 struct copy a
        // full matrix read would incur.
        float viewZ = TrackedView.RenderViewM20 * worldPos.x
                    + TrackedView.RenderViewM21 * worldPos.y
                    + TrackedView.RenderViewM22 * worldPos.z
                    + TrackedView.RenderViewM23;
        return -viewZ;
    }

    /// <summary>
    /// Rebuilds the ray Unity would have produced with no head tracking: origin at
    /// the camera's clean position, direction from its clean rotation. FOV trig and
    /// nearClipPlane are pre-computed once per frame so this hot path does no
    /// per-call trig or Camera property marshalling.
    /// </summary>
    private static void ComputeCleanRay(float viewportX, float viewportY, out Ray result)
    {
        RigProbe.ScreenToRayCalls++;

        Vector3 localDir = new Vector3(
            (viewportX * 2f - 1f) * TrackedView.ActiveTanHalfFovH,
            (viewportY * 2f - 1f) * TrackedView.ActiveTanHalfFovV,
            1f).normalized;

        Vector3 worldDir = TrackedView.CleanRotation * localDir;
        Vector3 origin = TrackedView.CleanPosition;

        result = new Ray(origin + worldDir * TrackedView.ActiveNearClipPlane, worldDir);
    }
}
