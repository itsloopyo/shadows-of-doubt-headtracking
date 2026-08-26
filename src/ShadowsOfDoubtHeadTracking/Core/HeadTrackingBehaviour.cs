// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using ShadowsOfDoubtHeadTracking.Camera;
using ShadowsOfDoubtHeadTracking.Configuration;
using ShadowsOfDoubtHeadTracking.Diagnostics;
using ShadowsOfDoubtHeadTracking.Input;
using ShadowsOfDoubtHeadTracking.State;
using ShadowsOfDoubtHeadTracking.Utilities;
using UnityEngine;

namespace ShadowsOfDoubtHeadTracking.Core;

/// <summary>
/// Drives head tracking for Shadows of Doubt.
///
/// Frame shape:
/// - Update (order 10000): hotkeys, state polling.
/// - FramePhaseProbe.Update / LateUpdate (order -10000): frame-phase sampling for
///   the rig probe.
/// - LateUpdate (order 10000): runs after the game's camera controller, reads the
///   clean camera pose, and writes the head-tracked view.
///
/// The camera transform's ROTATION is never modified, so
/// <c>camera.transform.forward</c> stays the mouse-controlled aim direction and
/// aim decoupling falls out for free.
///
/// The camera writes themselves, and the rules about when each may be taken back,
/// belong to <see cref="CameraViewWriter"/>.
/// </summary>
[DefaultExecutionOrder(10000)]
public class HeadTrackingBehaviour : MonoBehaviour
{
    // Rotation limits applied after the core processing pipeline,
    // preventing extreme head poses from flipping the camera.
    private const float MaxYaw = 90f;
    private const float MaxPitch = 60f;
    private const float MaxRoll = 45f;

    private OpenTrackReceiver? _receiver;
    private TrackingProcessor? _processor;
    private PositionProcessor? _positionProcessor;
    private PositionInterpolator? _positionInterpolator;
    private CameraFinder? _cameraFinder;
    private HotkeyHandler? _hotkeyHandler;
    private GameplayStateDetector? _stateDetector;
    private FieldOfViewOffset? _fieldOfView;

    private readonly CameraViewWriter _viewWriter = new();
    private readonly GameWindowPlacer _windowPlacer = new();

    private bool _positionEnabled = true;
    private bool _rotationEnabled = true;
    private bool _worldSpaceYaw = true;
    private bool _pauseOnLostFocus;

    private bool _initialized;
    private bool _wasInGameplay;
    private bool _loggedFirstPacket;

    private static HeadTrackingBehaviour? _instance;

    /// <summary>
    /// Current head tracking offset in degrees (yaw, pitch, roll).
    /// </summary>
    public static (float yaw, float pitch, float roll) CurrentOffset { get; private set; }

    /// <summary>
    /// Zeroes the position offset the mod owns, returning the camera to the pose
    /// the game set. See <see cref="CameraViewWriter.ClearLean"/>.
    /// </summary>
    internal static void ClearLean()
    {
        _instance?._viewWriter.ClearLean();
    }

    /// <summary>
    /// Hands the game a completely untracked camera for the duration of one game
    /// call. See <see cref="CameraViewWriter.EnterCleanScope"/>.
    /// </summary>
    internal static void EnterCleanCameraScope()
    {
        _instance?._viewWriter.EnterCleanScope();
    }

    /// <summary>
    /// Restores the tracked view suspended by <see cref="EnterCleanCameraScope"/>.
    /// </summary>
    internal static void ExitCleanCameraScope()
    {
        _instance?._viewWriter.ExitCleanScope();
    }

    /// <summary>
    /// Reads back the view matrix at the top of the next frame, before anything has
    /// touched it, and reports whether it is still the one we wrote. TEMPORARY.
    /// </summary>
    internal static void CheckViewMatrixSurvivedRender()
    {
        var writer = _instance?._viewWriter;
        if (writer == null || !writer.ViewMatrixApplied) return;

        var cam = writer.Camera;
        if (cam != null)
        {
            RigProbe.CheckMatrixSurvived(cam, TrackedView.RenderViewMatrix);
        }
    }

    /// <summary>
    /// Initialize the behaviour with all required dependencies.
    /// Must be called after the component is added to a GameObject.
    /// </summary>
    public void Initialize(OpenTrackReceiver receiver, TrackingProcessor processor, PluginConfig config,
        PositionProcessor? positionProcessor = null, PositionInterpolator? positionInterpolator = null)
    {
        _receiver = receiver;
        _processor = processor;
        _positionProcessor = positionProcessor;
        _positionInterpolator = positionInterpolator;

        _pauseOnLostFocus = config.PauseOnLostFocus.Value;
        _worldSpaceYaw = config.WorldSpaceYaw.Value;
        _positionEnabled = config.PositionEnabled.Value;
        _fieldOfView = new FieldOfViewOffset(config.FieldOfViewOffset.Value);

        _cameraFinder = new CameraFinder();
        _cameraFinder.OnCameraChanged += OnCameraChanged;

        _hotkeyHandler = new HotkeyHandler(config);
        _hotkeyHandler.Initialize();

        _stateDetector = new GameplayStateDetector();
        _stateDetector.Initialize();
        _stateDetector.SetCameraFinder(_cameraFinder);
        _stateDetector.OnGameplayStateChanged += OnGameplayStateChanged;

        RigProbe.Enabled = config.DiagnosticLogging.Value;
        if (RigProbe.Enabled)
        {
            gameObject.AddComponent<FramePhaseProbe>();
        }

        _initialized = true;
        _instance = this;
        HeadTrackingPlugin.Logger.LogInfo("HeadTrackingBehaviour initialized");
        HeadTrackingPlugin.Logger.LogInfo(
            $"Initial game state: {_stateDetector.CurrentState} (scene '{_stateDetector.CurrentScene}')");
    }

    /// <summary>
    /// Handle camera reference changes (scene load, camera switch).
    /// </summary>
    private void OnCameraChanged(Transform? newCamera)
    {
        _viewWriter.ClearLean();
        _viewWriter.ResetView();
        _fieldOfView!.Restore(_viewWriter.Camera);

        if (newCamera == null)
        {
            HeadTrackingPlugin.Logger.LogInfo("Camera reference lost");
            DetachCamera();
            return;
        }

        _viewWriter.Attach(newCamera.GetComponent<UnityEngine.Camera>(), newCamera);
        TrackedView.ActiveCamera = _viewWriter.Camera;

        HeadTrackingPlugin.Logger.LogInfo(
            $"Head tracking camera: {TransformPath.GetFullPath(newCamera)}");

        ResetSmoothing();
    }

    /// <summary>
    /// Drops every reference to the camera the mod was driving and puts the state
    /// that depends on it back to its resting position.
    /// </summary>
    private void DetachCamera()
    {
        _viewWriter.Detach();
        TrackedView.ActiveCamera = null;
        TrackedView.HasActiveViewMatrix = false;
        CrosshairOffsetManager.Reset();
        ResetSmoothing();
    }

    /// <summary>
    /// Handle gameplay state transitions.
    /// </summary>
    private void OnGameplayStateChanged(bool inGameplay)
    {
        if (inGameplay == _wasInGameplay) return;
        _wasInGameplay = inGameplay;

        if (inGameplay)
        {
            HeadTrackingPlugin.Logger.LogInfo("Entered gameplay - head tracking active");
            _cameraFinder?.InvalidateCache();
        }
        else
        {
            HeadTrackingPlugin.Logger.LogInfo("Left gameplay - head tracking paused");
            _viewWriter.ClearLean();
            _processor?.Reset();
            ResetSmoothing();
        }
    }

    /// <summary>
    /// Unity Update callback - hotkeys and state polling.
    /// </summary>
    private void Update()
    {
        if (!_initialized) return;

        _windowPlacer.Poll();

        // Event-based scene/focus notification is not available in IL2CPP.
        _stateDetector!.Poll();

        _hotkeyHandler!.ProcessInput();

        RigProbe.DumpReticle();
        RigProbe.DumpFieldOfViewSetting();
    }

    /// <summary>
    /// Unity LateUpdate callback - computes and applies head tracking.
    ///
    /// Execution order 10000 puts this after the game's camera controller, so the
    /// camera transform read here is the pose the game intends for this frame.
    /// </summary>
    private void LateUpdate()
    {
        if (!_initialized) return;

        if (_cameraFinder!.GetCamera() == null || !_viewWriter.HasCamera)
        {
            return;
        }

        ApplyFieldOfView();

        if (!ShouldApplyHeadTracking() || !ApplyHeadTracking())
        {
            Deactivate();
        }

        CrosshairOffsetManager.Update();

        RigProbe.Sample(RigProbe.PhaseLateUpdatePost);
        RigProbe.Report();
    }

    /// <summary>
    /// Leaves the camera exactly as the game set it for this frame.
    /// </summary>
    private void Deactivate()
    {
        _viewWriter.ClearLean();
        _viewWriter.ResetView();
        TrackedView.HasActiveViewMatrix = false;
        CurrentOffset = (0f, 0f, 0f);
    }

    /// <summary>
    /// Computes and applies head tracking for this frame. Returns false when there
    /// is no fresh tracking data, leaving the camera untouched.
    /// </summary>
    private bool ApplyHeadTracking()
    {
        var rawPose = _receiver!.GetLatestPose();
        if (!rawPose.IsDataFresh)
        {
            return false;
        }

        // Latched once. The receiver logs its first accepted packet; this line is
        // further down, and proves a fresh pose reached the camera, so a "no head
        // tracking" report is answerable from the log alone.
        if (!_loggedFirstPacket)
        {
            _loggedFirstPacket = true;
            HeadTrackingPlugin.Logger.LogInfo(
                $"First pose applied to the camera ({(_receiver.IsRemoteConnection ? "remote" : "local")} source)");
        }

        // Cached: every Time.* read is an IL2CPP native call.
        float dt = Time.deltaTime;

        // Locality picks LocalSmoothing vs RemoteSmoothing. Re-read every frame so
        // swapping a local tracker for a remote device switches parameter without
        // restarting the game.
        bool isRemoteConnection = _receiver.IsRemoteConnection;
        _processor!.IsRemoteConnection = isRemoteConnection;
        if (_positionProcessor != null)
        {
            _positionProcessor.IsRemoteConnection = isRemoteConnection;
        }

        // Core pipeline: center offset -> deadzone -> smoothing -> sensitivity.
        var processed = _processor.Process(rawPose, dt);
        CurrentOffset = (
            Mathf.Clamp(processed.Yaw, -MaxYaw, MaxYaw),
            Mathf.Clamp(processed.Pitch, -MaxPitch, MaxPitch),
            Mathf.Clamp(processed.Roll, -MaxRoll, MaxRoll));

        var cameraTransform = _viewWriter.Transform!;

        // Drop last frame's lean before reading the pose, so what we read is the
        // pose the game set rather than the one we leaned to.
        _viewWriter.ClearLean();

        // The camera's own world pose, after the game's camera controller has run.
        // This is what the game aims and raycasts with, and it is never modified.
        Quaternion cleanRotation = cameraTransform.rotation;
        Vector3 cleanPosition = cameraTransform.position;
        TrackedView.CleanRotation = cleanRotation;
        TrackedView.CleanPosition = cleanPosition;

        Quaternion renderRotation = ComputeRenderRotation(cleanRotation);
        Vector3 leanOffset = ComputeLeanOffset(cameraTransform, dt);
        TrackedView.RenderRotation = renderRotation;

        _viewWriter.Write(renderRotation, leanOffset);
        TrackedView.CaptureCameraParameters(_viewWriter.Camera!);

        // Set last: the projection-patch fast path gates on this flag and reads the
        // cache above, so setting it after guarantees patches never see a torn cache.
        TrackedView.HasActiveViewMatrix = true;

        RigProbe.LogPose(cleanRotation, renderRotation, TrackedView.RenderPosition - cleanPosition);

        // Dumped last, and from here rather than from the camera-changed callback:
        // Camera.main also resolves on the menu, so latching the dump there would
        // describe the menu's rig, and running it ahead of the camera write would
        // let a fault in the scaffolding stop the camera being written at all.
        RigProbe.DumpRig(cameraTransform, _viewWriter.Camera!);

        return true;
    }

    /// <summary>
    /// Applies the configured field of view offset, or hands the angle back to the
    /// game. Applied only in gameplay: the menu resolves the same Camera.main and
    /// has no business being widened.
    /// </summary>
    private void ApplyFieldOfView()
    {
        if (_fieldOfView!.IsZero || !_stateDetector!.IsInGameplay)
        {
            _fieldOfView.Restore(_viewWriter.Camera);
            return;
        }

        _fieldOfView.Apply(_viewWriter.Camera!);
    }

    /// <summary>
    /// Composes the rendered rotation from the game's clean camera rotation and the
    /// current head tracking offset. Honours rotation-disabled mode and the yaw
    /// mode: world-space yaw is horizon-locked, camera-local yaw follows the view's
    /// up axis.
    /// </summary>
    private Quaternion ComputeRenderRotation(Quaternion cleanRotation)
    {
        if (!_rotationEnabled)
        {
            return cleanRotation;
        }

        var (yaw, pitch, roll) = CurrentOffset;

        if (_worldSpaceYaw)
        {
            Quaternion worldYaw = Quaternion.AngleAxis(yaw, Vector3.up);
            Quaternion localPitchRoll = Quaternion.Euler(pitch, 0f, roll);
            return worldYaw * cleanRotation * localPitchRoll;
        }

        return cleanRotation * Quaternion.Euler(pitch, yaw, roll);
    }

    /// <summary>
    /// Runs the tracker's translation through the position pipeline and returns the
    /// lean as a local offset for the camera's own transform. Returns zero when
    /// positional tracking is off.
    /// </summary>
    private Vector3 ComputeLeanOffset(Transform cameraTransform, float dt)
    {
        if (!_positionEnabled || _positionProcessor == null || _positionInterpolator == null)
        {
            return Vector3.zero;
        }

        var rawPosition = _receiver!.GetLatestPosition();
        var interpolated = _positionInterpolator.Update(rawPosition, dt);
        var headRotation = QuaternionUtils.FromYawPitchRoll(
            CurrentOffset.yaw, CurrentOffset.pitch, CurrentOffset.roll);

        // Already box-clamped by the processor against the configured asymmetric
        // limits ([-LimitYDown, +LimitY], [-LimitZ, +LimitZBack]).
        Vec3 offset = _positionProcessor.Process(interpolated, headRotation, dt);

        // Negative z is the forward lean throughout the core pipeline and the
        // asymmetric clamp is built on that; Unity's +z is forward, so the flip
        // belongs here rather than in InvertZ, which would invert ahead of the clamp
        // and swap the budgets.
        Vector3 cameraLocal = new Vector3(offset.X, offset.Y, -offset.Z);

        // The offset is expressed along the camera's own axes; localPosition is in
        // the parent's space, so convert between the two rather than assuming the
        // camera sits at identity rotation under its parent.
        var parent = cameraTransform.parent;
        return parent == null
            ? cameraLocal
            : parent.InverseTransformVector(cameraTransform.TransformVector(cameraLocal));
    }

    /// <summary>
    /// Check all conditions required for head tracking to be applied.
    /// </summary>
    private bool ShouldApplyHeadTracking()
    {
        if (!ModState.Instance.IsEnabled)
        {
            return false;
        }

        var detector = _stateDetector!;
        if (!detector.IsInGameplay)
        {
            return false;
        }

        // The detector's cached focus value, polled every few frames, which matches
        // any reasonable pause-on-unfocus response time and avoids a per-frame
        // IL2CPP native property read.
        return !_pauseOnLostFocus || detector.IsApplicationFocused;
    }

    /// <summary>
    /// Advances the tracking-mode cycle one step. Three states:
    ///   0: rotation + position (normal)
    ///   1: rotation only       (position disabled)
    ///   2: position only       (rotation disabled)
    /// State 2 wraps back to 0. Called from HotkeyHandler.
    /// </summary>
    public static void CycleTrackingMode()
    {
        var inst = _instance;
        if (inst == null) return;

        if (inst._rotationEnabled && inst._positionEnabled)
        {
            inst._positionEnabled = false;
        }
        else if (inst._rotationEnabled)
        {
            inst._rotationEnabled = false;
            inst._positionEnabled = true;
        }
        else
        {
            inst._rotationEnabled = true;
            inst._positionEnabled = true;
        }

        // Drop stale velocity/interpolation state so re-entry doesn't replay it.
        if (!inst._positionEnabled)
        {
            inst._viewWriter.ClearLean();
            inst._positionProcessor?.Reset();
            inst._positionInterpolator?.Reset();
        }

        HeadTrackingPlugin.Logger.LogInfo(
            $"Tracking mode: rotation={(inst._rotationEnabled ? "on" : "off")}, position={(inst._positionEnabled ? "on" : "off")}");
    }

    /// <summary>
    /// Toggles between world-space (horizon-locked) and camera-local yaw.
    /// Called from HotkeyHandler.
    /// </summary>
    public static void ToggleYawMode()
    {
        var inst = _instance;
        if (inst == null) return;

        inst._worldSpaceYaw = !inst._worldSpaceYaw;
        HeadTrackingPlugin.Logger.LogInfo(
            $"Yaw mode: {(inst._worldSpaceYaw ? "world-space (horizon-locked)" : "camera-local")}");
    }

    private void ResetSmoothing()
    {
        _processor?.ResetSmoothing();
        _positionProcessor?.Reset();
        _positionInterpolator?.Reset();
    }

    /// <summary>
    /// Unity OnDestroy callback - cleanup resources.
    /// </summary>
    private void OnDestroy()
    {
        _viewWriter.ClearLean();
        _viewWriter.ResetView();
        _fieldOfView?.Restore(_viewWriter.Camera);
        DetachCamera();

        if (_cameraFinder != null)
        {
            _cameraFinder.OnCameraChanged -= OnCameraChanged;
            _cameraFinder.Dispose();
        }

        if (_stateDetector != null)
        {
            _stateDetector.OnGameplayStateChanged -= OnGameplayStateChanged;
            _stateDetector.Dispose();
        }

        // Receiver disposal is handled by the plugin's Unload method.

        // Reference-equality guard: don't clobber a fresh _instance written by a
        // replacement Initialize() that ran before this teardown.
        if (ReferenceEquals(_instance, this))
        {
            _instance = null;
        }

        _initialized = false;
        HeadTrackingPlugin.Logger.LogInfo("HeadTrackingBehaviour destroyed");
    }

    /// <summary>
    /// Get diagnostic information about the current state.
    /// </summary>
    public BehaviourDiagnostics GetDiagnostics()
    {
        var cameraTransform = _viewWriter.Transform;
        return new BehaviourDiagnostics
        {
            IsInitialized = _initialized,
            IsEnabled = ModState.Instance.IsEnabled,
            IsInGameplay = _stateDetector?.IsInGameplay ?? false,
            HasCamera = _cameraFinder?.HasCamera ?? false,
            IsReceiverConnected = _receiver?.IsReceiving ?? false,
            CameraPath = cameraTransform != null ? TransformPath.GetFullPath(cameraTransform) : null,
            HotkeyDiagnostics = _hotkeyHandler?.GetDiagnostics(),
            GameStateDiagnostics = _stateDetector?.GetDiagnostics()
        };
    }
}
