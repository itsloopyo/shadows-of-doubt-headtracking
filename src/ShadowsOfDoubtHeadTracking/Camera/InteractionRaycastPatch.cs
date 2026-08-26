// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using HarmonyLib;
using ShadowsOfDoubtHeadTracking.Core;

namespace ShadowsOfDoubtHeadTracking.Camera;

/// <summary>
/// Keeps the game's interaction raycast on the mouse-controlled aim rather than on
/// the player's head.
///
/// <c>InteractionController.InteractionRaycastCheck</c> casts through
/// <c>Camera.ScreenPointToRay</c>, which builds its ray from the camera's
/// <c>worldToCameraMatrix</c>. That matrix carries the head-tracked view for the
/// whole frame, so without this the interaction point tracks the head: the
/// on-screen marker frames whatever the head is pointed at, and the player has to
/// click where the marker went instead of the thing they are looking at.
///
/// The prefix hands the game a clean camera and the postfix puts the tracked view
/// back, so only this one call sees untracked state. Patching
/// <c>Camera.ScreenPointToRay</c> directly does not work under IL2CPP - it is an
/// engine internal call that native game code invokes without passing through the
/// managed wrapper Harmony patches, which is why
/// <see cref="CameraProjectionPatches"/> never fires. A game method is ordinary
/// IL2CPP-compiled code and patches normally.
/// </summary>
internal static class InteractionRaycastPatch
{
    private static bool _patchAttempted;
    private static bool _loggedFirstCall;

    internal static void ApplyIfPossible(Harmony harmony)
    {
        if (_patchAttempted) return;
        _patchAttempted = true;

        var type = AccessTools.TypeByName("InteractionController");
        if (type == null)
        {
            HeadTrackingPlugin.Logger.LogError(
                "InteractionController not found - the interaction point will follow your head " +
                "instead of your aim. Disable positional tracking with Page Up as a workaround.");
            return;
        }

        var method = AccessTools.Method(type, "InteractionRaycastCheck");
        if (method == null)
        {
            HeadTrackingPlugin.Logger.LogError(
                "InteractionController.InteractionRaycastCheck not found - the interaction point " +
                "will follow your head instead of your aim. Disable positional tracking with " +
                "Page Up as a workaround.");
            return;
        }

        harmony.Patch(method,
            prefix: new HarmonyMethod(typeof(InteractionRaycastPatch), nameof(Prefix)),
            postfix: new HarmonyMethod(typeof(InteractionRaycastPatch), nameof(Postfix)));

        HeadTrackingPlugin.Logger.LogInfo("Patched InteractionController.InteractionRaycastCheck");
    }

    public static void Prefix()
    {
        // Latched once. The Camera projection patches looked correct for months
        // while never running, so a patch that guards aim has to prove it fires.
        if (!_loggedFirstCall)
        {
            _loggedFirstCall = true;
            HeadTrackingPlugin.Logger.LogInfo("Interaction raycast decoupling active");
        }

        HeadTrackingBehaviour.EnterCleanCameraScope();
    }

    public static void Postfix()
    {
        HeadTrackingBehaviour.ExitCleanCameraScope();
    }
}
