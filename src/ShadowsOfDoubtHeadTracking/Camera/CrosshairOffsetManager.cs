// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;
using ShadowsOfDoubtHeadTracking.Core;
using ShadowsOfDoubtHeadTracking.Utilities;
using UnityEngine;

namespace ShadowsOfDoubtHeadTracking.Camera;

/// <summary>
/// Keeps the game's crosshair glued to where the player is actually aiming.
///
/// The camera transform is never rotated by this mod, so the game keeps firing,
/// raycasting and interacting along the mouse-controlled direction. What changes
/// is where that direction lands on screen once the view is head-tracked, so the
/// crosshair is moved to the projection of the aim point through the rendered
/// view. Deriving it from the same matrix the frame is drawn with means it cannot
/// drift out of phase with the camera on combined yaw/pitch/roll poses.
/// </summary>
public static class CrosshairOffsetManager
{
    private const float MaxRaycastDistance = 1000f;
    private const float MinRaycastDistance = 0.5f;
    private const float DefaultAimDistance = 25f;
    private const float DistanceSmoothingRate = 15f;

    // Keeps a clamped crosshair inside the viewport rather than half off it.
    private const float EdgeMarginPixels = 24f;

    // The HUD singleton does not exist on the menu, and the search reflects into
    // Assembly-CSharp, so retry on an interval rather than every frame.
    private const int SearchRetryFrames = 60;

    // Floor for the dominant screen axis, so a direction that is almost exactly
    // along the view axis cannot divide by zero.
    private const float MinDominantAxis = 1e-4f;

    private static RectTransform? _crosshairs;
    private static Canvas? _canvas;
    private static Vector2 _originalPosition;
    private static bool _searched;
    private static int _searchCounter;
    private static float _aimDistance = DefaultAimDistance;
    private static bool _atOriginal = true;

    public static void Update()
    {
        if (!_searched)
        {
            FindCrosshairs();
        }

        if (_crosshairs == null)
        {
            return;
        }

        if (!ModState.Instance.IsEnabled || !TrackedView.HasActiveViewMatrix)
        {
            ParkAtOriginal();
            return;
        }

        Vector3 aimOrigin = TrackedView.CleanPosition;
        Vector3 aimDirection = TrackedView.CleanRotation * Vector3.forward;

        if (Physics.Raycast(aimOrigin, aimDirection, out RaycastHit hit, MaxRaycastDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && hit.distance >= MinRaycastDistance)
        {
            float t = 1f - Mathf.Exp(-DistanceSmoothingRate * Time.deltaTime);
            _aimDistance = Mathf.Lerp(_aimDistance, hit.distance, t);
        }

        Vector3 aimPoint = aimOrigin + aimDirection * _aimDistance;

        if (!TrackedView.TryProjectToScreen(aimPoint, out Vector2 screenPoint))
        {
            screenPoint = OffScreenEdgePoint(aimDirection);
        }

        screenPoint.x = Mathf.Clamp(screenPoint.x, EdgeMarginPixels,
            TrackedView.ActivePixelWidth - EdgeMarginPixels);
        screenPoint.y = Mathf.Clamp(screenPoint.y, EdgeMarginPixels,
            TrackedView.ActivePixelHeight - EdgeMarginPixels);

        Vector2 pixelOffset = new Vector2(
            screenPoint.x - TrackedView.ActivePixelWidth * 0.5f,
            screenPoint.y - TrackedView.ActivePixelHeight * 0.5f);

        // anchoredPosition lives in canvas units; scaleFactor converts from pixels.
        float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        _crosshairs.anchoredPosition = _originalPosition + pixelOffset / scale;
        _atOriginal = false;
    }

    /// <summary>
    /// Places the crosshair a full screen away in the direction the aim point
    /// actually lies, for the frames where a hard head turn puts it behind the
    /// rendered view and there is no projection to use. The caller's clamp then
    /// pins it to the matching edge. Parking it on a fixed edge instead sends the
    /// crosshair the wrong way whenever the player turns the other way.
    /// </summary>
    private static Vector2 OffScreenEdgePoint(Vector3 aimDirection)
    {
        Vector3 viewDirection = Quaternion.Inverse(TrackedView.RenderRotation) * aimDirection;
        float dominant = Mathf.Max(Mathf.Abs(viewDirection.x), Mathf.Abs(viewDirection.y));
        if (dominant < MinDominantAxis)
        {
            dominant = MinDominantAxis;
        }

        return new Vector2(
            TrackedView.ActivePixelWidth * (0.5f + viewDirection.x / dominant),
            TrackedView.ActivePixelHeight * (0.5f + viewDirection.y / dominant));
    }

    private static void ParkAtOriginal()
    {
        // The crosshair can be destroyed with the HUD by a scene reload, which
        // Unity's overloaded null check catches.
        if (_atOriginal || _crosshairs == null) return;
        _crosshairs.anchoredPosition = _originalPosition;
        _atOriginal = true;
    }

    private static void FindCrosshairs()
    {
        if (++_searchCounter % SearchRetryFrames != 0) return;

        _searched = true;

        // InterfaceControls lives in Assembly-CSharp; reached purely by reflection so
        // the build keeps no compile-time dependency on the game.
        var controlsType = Type.GetType("InterfaceControls, Assembly-CSharp");
        if (controlsType == null)
        {
            HeadTrackingPlugin.Logger.LogWarning(
                "InterfaceControls not found - the crosshair will stay centred while head tracking is on");
            return;
        }

        object? controls = GameMembers.ReadSingleton(controlsType);
        if (controls == null) { _searched = false; return; }

        var container = GameMembers.ReadMember(controlsType, controls, "reticleContainer") as RectTransform;
        if (container == null) { _searched = false; return; }

        for (int i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            if (child.name != "Crosshairs") continue;

            _crosshairs = child.GetComponent<RectTransform>();
            if (_crosshairs == null) break;

            _originalPosition = _crosshairs.anchoredPosition;
            _canvas = _crosshairs.GetComponentInParent<Canvas>();
            HeadTrackingPlugin.Logger.LogInfo(
                $"Crosshair found (canvas scale {(_canvas != null ? _canvas.scaleFactor : 1f):F2})");
            return;
        }

        HeadTrackingPlugin.Logger.LogWarning(
            "Crosshair not found under reticleContainer - it will stay centred while head tracking is on");
    }

    public static void Reset()
    {
        ParkAtOriginal();

        _crosshairs = null;
        _canvas = null;
        _searched = false;
        _atOriginal = true;
        _aimDistance = DefaultAimDistance;
    }
}
