// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using ShadowsOfDoubtHeadTracking.Core;
using UnityEngine;

namespace ShadowsOfDoubtHeadTracking.Camera;

/// <summary>
/// Widens or narrows the field of view by a fixed offset, on top of whatever the
/// game set this frame.
///
/// The game's own settings carry a Field of View slider, so what this adds is the
/// range past its cap. It is an offset rather than an absolute so the game keeps
/// its sprint kick and its interaction zoom - both write an absolute angle every
/// frame they are animating, and the offset simply rides on top.
/// </summary>
internal sealed class FieldOfViewOffset
{
    // The base comes from the game and the offset from the user, so the sum is
    // clamped to angles a perspective camera can actually render.
    private const float MinFieldOfView = 20f;
    private const float MaxFieldOfView = 170f;

    private readonly float _offsetDegrees;

    private float _gameFieldOfView;
    private float _writtenFieldOfView;
    private bool _applied;
    private bool _logged;

    internal FieldOfViewOffset(float offsetDegrees)
    {
        _offsetDegrees = offsetDegrees;
    }

    /// <summary>Whether the user asked for no change at all.</summary>
    internal bool IsZero => _offsetDegrees == 0f;

    /// <summary>
    /// Adds the offset to the angle the game set this frame.
    ///
    /// Call before the view matrix is cached, so the crosshair projection and the
    /// rendered frame come from the same frustum.
    /// </summary>
    internal void Apply(UnityEngine.Camera camera)
    {
        // Re-read the game's angle every frame, and only believe it when it is not
        // the one we wrote last frame. Adding the offset to our own previous write
        // would compound it frame after frame.
        float current = camera.fieldOfView;
        if (!_applied || current != _writtenFieldOfView)
        {
            _gameFieldOfView = current;
        }

        float applied = Mathf.Clamp(
            _gameFieldOfView + _offsetDegrees, MinFieldOfView, MaxFieldOfView);
        camera.fieldOfView = applied;
        _writtenFieldOfView = applied;
        _applied = true;

        if (!_logged)
        {
            _logged = true;
            HeadTrackingPlugin.Logger.LogInfo(
                $"Field of view: game {_gameFieldOfView:F1} deg + offset {_offsetDegrees:F1} " +
                $"= {applied:F1} deg vertical");
        }
    }

    /// <summary>
    /// Hands the field of view back to the game. Skipped when something else has
    /// written the camera since, because that write is newer than ours and the value
    /// we remember is stale.
    /// </summary>
    internal void Restore(UnityEngine.Camera? camera)
    {
        if (!_applied) return;
        _applied = false;

        if (camera != null && camera.fieldOfView == _writtenFieldOfView)
        {
            camera.fieldOfView = _gameFieldOfView;
        }
    }
}
