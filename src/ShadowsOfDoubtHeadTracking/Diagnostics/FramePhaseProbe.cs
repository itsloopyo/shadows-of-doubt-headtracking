// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using ShadowsOfDoubtHeadTracking.Core;
using UnityEngine;

namespace ShadowsOfDoubtHeadTracking.Diagnostics;

/// <summary>
/// Samples the camera rig at each phase of the Unity frame for
/// <see cref="RigProbe"/>, and checks whether the view matrix written last frame
/// survived being rendered.
///
/// It deliberately does NOT strip the position offset off the camera before game
/// code. The offset has to stay applied for the whole frame so that the transform
/// the renderer reads and the view matrix the game's world-to-screen projection
/// reads agree with each other; removing it for part of the frame is what made
/// world-anchored markers slide off their targets. The one place the game must
/// not see it is its interaction raycast, which gets an explicitly clean camera
/// through <see cref="HeadTrackingBehaviour.EnterCleanCameraScope"/>.
///
/// Uses only static method calls - no managed type parameters that would cause
/// IL2CPP ClassInjector registration failure.
/// </summary>
[DefaultExecutionOrder(-10000)]
internal class FramePhaseProbe : MonoBehaviour
{
    private void FixedUpdate()
    {
        RigProbe.Sample(RigProbe.PhaseFixedUpdate);
    }

    private void Update()
    {
        HeadTrackingBehaviour.CheckViewMatrixSurvivedRender();
        RigProbe.Sample(RigProbe.PhaseUpdate);
    }

    private void LateUpdate()
    {
        RigProbe.Sample(RigProbe.PhaseLateUpdatePre);
    }
}
