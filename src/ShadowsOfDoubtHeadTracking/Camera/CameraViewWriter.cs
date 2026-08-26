// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using UnityEngine;

namespace ShadowsOfDoubtHeadTracking.Camera;

/// <summary>
/// Owns everything the mod writes onto the camera, and the rules about when each
/// write may be taken back.
///
/// Rotation rides <c>worldToCameraMatrix</c> so the camera transform's rotation
/// stays the game's aim direction. The position offset additionally moves the
/// camera's own <c>localPosition</c>, because HDRP's camera-relative rendering
/// takes the world camera position from the transform rather than from the view
/// matrix's translation column.
///
/// The two writes have DIFFERENT LIFETIMES and must not be reset together:
///
/// - The lean is an absolute write, cleared and re-applied within a single
///   LateUpdate so the pose read back is the game's own rather than last frame's
///   leaned one.
/// - The view matrix must stay applied for the whole frame, because it is what
///   the frame renders with. Clearing it early wipes the head rotation before the
///   camera draws, and the symptom is subtle: the reticle still moves correctly,
///   because reticle compensation projects through the cached
///   <see cref="TrackedView.RenderVPMatrix"/>, while the rendered view sits
///   perfectly still.
/// </summary>
internal sealed class CameraViewWriter
{
    private UnityEngine.Camera? _camera;
    private Transform? _transform;

    // The lean, written straight onto the camera's own localPosition as an ABSOLUTE
    // value rather than as a delta on an ancestor.
    //
    // The game leaves this at exactly zero - it drives the rig through the pivots
    // above the camera - so owning it outright removes every failure mode the delta
    // approach had: nothing accumulates, nothing is corrupted when the game writes
    // an absolute of its own, and there is no ancestor to resolve and get stale when
    // CameraController.SetupFPS() reparents the camera mid-session.
    private Vector3 _appliedLeanOffset;
    private bool _leanApplied;

    // Whether the camera currently carries our view matrix. Tracked separately from
    // the position offset: the offset is zero whenever the player's head is centred,
    // and tying the matrix reset to it left the rendered rotation latched on the
    // camera after tracking stopped.
    private bool _viewMatrixApplied;

    private bool _leanSuspended;
    private bool _viewMatrixSuspended;

    internal UnityEngine.Camera? Camera => _camera;

    internal Transform? Transform => _transform;

    /// <summary>Whether a live camera and its transform are both attached.</summary>
    internal bool HasCamera => _camera != null && _transform != null;

    /// <summary>Whether the camera currently carries our view matrix.</summary>
    internal bool ViewMatrixApplied => _viewMatrixApplied;

    internal void Attach(UnityEngine.Camera camera, Transform transform)
    {
        _camera = camera;
        _transform = transform;
    }

    internal void Detach()
    {
        _camera = null;
        _transform = null;
    }

    /// <summary>
    /// Zeroes the lean, returning the camera to the pose the game set. An absolute
    /// write, so it is correct no matter what happened in between.
    ///
    /// Called once per LateUpdate before the clean pose is read, and on every path
    /// where the mod stops driving the camera: the camera reference changes,
    /// gameplay is left, positional tracking is switched off, tracking is
    /// deactivated, or the behaviour is destroyed.
    ///
    /// The per-frame clear is never observed by game code: the behaviour runs at
    /// execution order 10000, after everything else, and re-applies the lean in the
    /// same LateUpdate, so the lean is in place for the whole of the rest of the
    /// frame. That is what keeps the transform the renderer reads and the view
    /// matrix the game's world-to-screen projection reads in agreement.
    /// </summary>
    internal void ClearLean()
    {
        if (!_leanApplied) return;

        if (_transform != null)
        {
            _transform.localPosition = Vector3.zero;
        }
        _appliedLeanOffset = Vector3.zero;
        _leanApplied = false;
    }

    /// <summary>
    /// Writes the head-tracked view onto the camera and publishes it to
    /// <see cref="TrackedView"/>.
    /// </summary>
    internal void Write(Quaternion renderRotation, Vector3 leanOffset)
    {
        var camera = _camera!;
        var transform = _transform!;

        transform.localPosition = leanOffset;
        _appliedLeanOffset = leanOffset;
        _leanApplied = leanOffset != Vector3.zero;

        // Read back where the camera actually ended up rather than assuming the write
        // landed where we wanted. HDRP renders with camera-relative rendering on, so
        // it takes the camera position from the transform and discards the view
        // matrix translation. A matrix built from an assumed position can therefore
        // disagree with what is drawn, and everything that projects through the
        // matrix - the game's world-anchored markers - then slides off its target by
        // exactly the size of that disagreement. Deriving the matrix from the
        // transform makes the two agree by construction.
        Vector3 renderPosition = transform.position;

        Matrix4x4 view = TrackedView.BuildViewMatrix(renderRotation, renderPosition);
        camera.worldToCameraMatrix = view;
        _viewMatrixApplied = true;
        TrackedView.PublishView(view, renderPosition);
    }

    /// <summary>
    /// Hands the camera back to Unity, so it derives its view from the transform
    /// again. Called when tracking stops for any reason - disabled, paused, tracker
    /// data gone stale, camera swapped, mod torn down.
    /// </summary>
    internal void ResetView()
    {
        if (!_viewMatrixApplied) return;
        _viewMatrixApplied = false;

        if (_camera != null)
        {
            _camera.ResetWorldToCameraMatrix();
        }
    }

    /// <summary>
    /// Hands the game a completely untracked camera for the duration of one game
    /// call, then <see cref="ExitCleanScope"/> puts the tracked view back.
    ///
    /// This is what actually decouples aim in this title. The game's interaction
    /// raycast goes through <c>Camera.ScreenPointToRay</c>, which derives its ray
    /// from <c>worldToCameraMatrix</c> - the matrix we overwrite with the
    /// head-tracked view. Leaving it applied while the game raycasts drags the
    /// interaction point around with the player's head, so the marker frames
    /// whatever the head is pointing at rather than what the mouse is aiming at.
    ///
    /// Patching <c>Camera.ScreenPointToRay</c> itself does not work here: it is an
    /// engine internal call, and native game code invokes it directly rather than
    /// through the managed wrapper Harmony can reach. Suspending the tracking
    /// around the game's own method is the interception point that does work.
    /// </summary>
    internal void EnterCleanScope()
    {
        _leanSuspended = _leanApplied;
        if (_leanSuspended && _transform != null)
        {
            _transform.localPosition = Vector3.zero;
        }

        _viewMatrixSuspended = _viewMatrixApplied;
        if (_viewMatrixSuspended && _camera != null)
        {
            _camera.ResetWorldToCameraMatrix();
        }
    }

    /// <summary>
    /// Restores the tracked view suspended by <see cref="EnterCleanScope"/>, so
    /// rendering and the game's own world-to-screen projection keep seeing the
    /// head-tracked camera.
    /// </summary>
    internal void ExitCleanScope()
    {
        if (_leanSuspended)
        {
            _leanSuspended = false;
            if (_transform != null)
            {
                _transform.localPosition = _appliedLeanOffset;
            }
        }

        if (_viewMatrixSuspended)
        {
            _viewMatrixSuspended = false;
            if (_camera != null)
            {
                _camera.worldToCameraMatrix = TrackedView.RenderViewMatrix;
            }
        }
    }
}
