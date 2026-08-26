// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using UnityEngine;

namespace ShadowsOfDoubtHeadTracking.Camera;

/// <summary>
/// The head-tracked view the current frame is drawn with, published for the
/// consumers that run outside the tracking behaviour's own LateUpdate: the
/// Harmony projection prefixes, which fire many times per frame from game UI and
/// marker code, and the crosshair compensation.
///
/// Plain static fields rather than auto-properties so reads are direct memory
/// loads in IL2CPP, not method-call getters.
/// </summary>
internal static class TrackedView
{
    /// <summary>The camera currently receiving head tracking, or null.</summary>
    internal static UnityEngine.Camera? ActiveCamera;

    /// <summary>Whether the view state below is valid this frame.</summary>
    internal static bool HasActiveViewMatrix;

    /// <summary>Head-tracked view matrix (rotation and position offset).</summary>
    internal static Matrix4x4 RenderViewMatrix;

    /// <summary>Pre-computed <c>projectionMatrix * RenderViewMatrix</c>.</summary>
    internal static Matrix4x4 RenderVPMatrix;

    // Row 2 of RenderViewMatrix, pre-extracted. The projection patches only need
    // this row (to compute view-space z) and copying the full 64-byte struct out
    // of the field on every call is pure overhead.
    internal static float RenderViewM20;
    internal static float RenderViewM21;
    internal static float RenderViewM22;
    internal static float RenderViewM23;

    /// <summary>The camera's clean world rotation: the direction the player is aiming.</summary>
    internal static Quaternion CleanRotation;

    /// <summary>The camera's clean world position: where aim rays originate.</summary>
    internal static Vector3 CleanPosition;

    /// <summary>Final head-tracked world rotation of the rendered view.</summary>
    internal static Quaternion RenderRotation;

    /// <summary>Head-tracked world position of the rendered view.</summary>
    internal static Vector3 RenderPosition;

    /// <summary>Cached <c>nearClipPlane</c>.</summary>
    internal static float ActiveNearClipPlane;

    /// <summary>Pre-computed tan(verticalFov / 2).</summary>
    internal static float ActiveTanHalfFovV;

    /// <summary>Pre-computed tan(horizontalFov / 2).</summary>
    internal static float ActiveTanHalfFovH;

    /// <summary>Cached <c>pixelWidth</c>.</summary>
    internal static float ActivePixelWidth;

    /// <summary>Cached <c>pixelHeight</c>.</summary>
    internal static float ActivePixelHeight;

    /// <summary>Pre-computed <c>1f / pixelWidth</c>.</summary>
    internal static float ActiveInvPixelWidth;

    /// <summary>Pre-computed <c>1f / pixelHeight</c>.</summary>
    internal static float ActiveInvPixelHeight;

    /// <summary>
    /// Records the matrix just written onto the camera, along with the world
    /// position the camera actually ended up at.
    /// </summary>
    internal static void PublishView(Matrix4x4 view, Vector3 renderPosition)
    {
        RenderPosition = renderPosition;
        RenderViewMatrix = view;

        RenderViewM20 = view.m20;
        RenderViewM21 = view.m21;
        RenderViewM22 = view.m22;
        RenderViewM23 = view.m23;
    }

    /// <summary>
    /// Caches the frustum and viewport values the projection patches consume, so
    /// their hot path does no per-call trig or Camera property marshalling.
    ///
    /// Does NOT raise <see cref="HasActiveViewMatrix"/>: the patches gate on that
    /// flag and read this cache, so the caller sets it afterwards to guarantee
    /// they never see a torn cache.
    /// </summary>
    internal static void CaptureCameraParameters(UnityEngine.Camera camera)
    {
        // The projection matrix, not fieldOfView + aspect, is the field of view the
        // frame is drawn with. Under HDRP the two can disagree: a physical camera
        // derives its angle from focal length and sensor size, and the gate fit
        // decides which axis the angle applies to, so reconstructing the horizontal
        // extent as fieldOfView * aspect projects the crosshair through a frustum the
        // renderer never used. Reading the matrix back also picks up the field of
        // view offset for free.
        Matrix4x4 projection = camera.projectionMatrix;
        RenderVPMatrix = projection * RenderViewMatrix;

        ActiveTanHalfFovH = 1f / projection.m00;
        ActiveTanHalfFovV = 1f / projection.m11;
        ActiveNearClipPlane = camera.nearClipPlane;

        // Each pixelWidth/pixelHeight read is an IL2CPP native property call and
        // the projection patches consume them on every invocation.
        float pw = camera.pixelWidth;
        float ph = camera.pixelHeight;
        ActivePixelWidth = pw;
        ActivePixelHeight = ph;
        ActiveInvPixelWidth = pw > 0f ? 1f / pw : 0f;
        ActiveInvPixelHeight = ph > 0f ? 1f / ph : 0f;
    }

    /// <summary>
    /// Builds a Unity view matrix from a world rotation and position. Unity's
    /// <c>worldToCameraMatrix</c> looks down -Z, so the third row is negated.
    /// </summary>
    internal static Matrix4x4 BuildViewMatrix(Quaternion rotation, Vector3 position)
    {
        Matrix4x4 m = Matrix4x4.Rotate(Quaternion.Inverse(rotation)) * Matrix4x4.Translate(-position);
        m.m20 = -m.m20;
        m.m21 = -m.m21;
        m.m22 = -m.m22;
        m.m23 = -m.m23;
        return m;
    }

    // Points at or behind the rendered view's plane have no meaningful screen
    // position; the reciprocal would blow up as w approaches zero.
    private const float MinClipW = 0.0001f;

    /// <summary>
    /// Projects a world point through the head-tracked view. Returns false when the
    /// point is behind the rendered camera.
    /// </summary>
    internal static bool TryProjectToScreen(Vector3 worldPoint, out Vector2 screenPoint)
    {
        Vector4 clip = RenderVPMatrix * new Vector4(worldPoint.x, worldPoint.y, worldPoint.z, 1f);
        if (clip.w <= MinClipW)
        {
            screenPoint = default;
            return false;
        }

        float invW = 1f / clip.w;
        screenPoint = new Vector2(
            (clip.x * invW * 0.5f + 0.5f) * ActivePixelWidth,
            (clip.y * invW * 0.5f + 0.5f) * ActivePixelHeight);
        return true;
    }
}
