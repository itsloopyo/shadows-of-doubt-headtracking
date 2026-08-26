// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using ShadowsOfDoubtHeadTracking.Boot;
using ShadowsOfDoubtHeadTracking.Camera;
using ShadowsOfDoubtHeadTracking.Configuration;
using ShadowsOfDoubtHeadTracking.Diagnostics;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace ShadowsOfDoubtHeadTracking.Core;

/// <summary>
/// Main BepInEx IL2CPP plugin entry point for head tracking mod.
/// Initializes the OpenTrack receiver, camera handler, and Unity lifecycle hooks.
/// </summary>
[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class HeadTrackingPlugin : BasePlugin
{
    public const string PluginGuid = "com.headtracking.shadowsofdoubt";
    public const string PluginName = "Head Tracking";
    public const string PluginVersion = "0.0.0";

    internal static ManualLogSource Logger { get; private set; } = null!;

    // Static references to prevent IL2CPP garbage collection
    private static GameObject? _behaviourObject;
    private static HeadTrackingBehaviour? _behaviour;

    // Held for the lifetime of the plugin: BepInEx roots the plugin instance, so
    // these fields keep the managed graph alive under IL2CPP's GC.
    private OpenTrackReceiver? _receiver;
    private TrackingProcessor? _processor;
    private PositionProcessor? _positionProcessor;
    private PositionInterpolator? _positionInterpolator;
    private PluginConfig? _config;
    private Harmony? _harmony;
    private LogFile? _logFile;

    public override void Load()
    {
        Logger = Log;

        // Attached before the first line is logged, so the session's log file holds
        // the whole of Load() rather than starting part way through it.
        _logFile = LogFile.Attach(PluginName, $"{PluginName} v{PluginVersion}");

        FastBoot.Apply();

        ApplyHarmonyPatches();

        var config = new PluginConfig();
        config.Initialize(Config);
        _config = config;

        _receiver = new OpenTrackReceiver();
        _processor = BuildRotationProcessor(config);
        _positionProcessor = BuildPositionProcessor(config);
        _positionInterpolator = new PositionInterpolator();

        CreateBehaviour(_receiver, _processor, config, _positionProcessor, _positionInterpolator);

        StartReceiver(_receiver);

        ModState.Instance.IsEnabled = config.EnabledOnStartup.Value;

        Logger.LogInfo($"{PluginName} v{PluginVersion} loaded - tracking is " +
                       $"{(ModState.Instance.IsEnabled ? "ENABLED" : "DISABLED")} on startup");
    }

    private void ApplyHarmonyPatches()
    {
        _harmony = new Harmony(PluginGuid);
        try
        {
            _harmony.PatchAll(typeof(HeadTrackingPlugin).Assembly);
            Logger.LogInfo("Harmony patches applied for decoupled LOOK+AIM");
            InteractionRaycastPatch.ApplyIfPossible(_harmony);
        }
        catch (Exception ex)
        {
            // The whole exception, not just its Message: a patch failure names the
            // offending method in the stack and nowhere else.
            Logger.LogError($"Failed to apply Harmony patches: {ex}");
            Logger.LogWarning("Decoupled aiming may not work correctly");
        }
    }

    private static TrackingProcessor BuildRotationProcessor(PluginConfig config)
    {
        return new TrackingProcessor
        {
            // Selected per connection: loopback senders get LocalSmoothing, remote
            // network devices get RemoteSmoothing. Both cover rotation and position.
            LocalSmoothing = config.LocalSmoothing.Value,
            RemoteSmoothing = config.RemoteSmoothing.Value,
            Sensitivity = new SensitivitySettings(
                config.YawSensitivity.Value,
                config.PitchSensitivity.Value,
                config.RollSensitivity.Value,
                invertYaw: config.InvertYaw.Value,
                invertPitch: config.InvertPitch.Value,
                invertRoll: config.InvertRoll.Value
            ),
            Deadzone = DeadzoneSettings.None
        };
    }

    private static PositionProcessor BuildPositionProcessor(PluginConfig config)
    {
        return new PositionProcessor
        {
            Settings = new PositionSettings(
                config.PositionSensitivityX.Value,
                config.PositionSensitivityY.Value,
                config.PositionSensitivityZ.Value,
                config.PositionLimitX.Value,
                config.PositionLimitY.Value,
                config.PositionLimitYDown.Value,
                config.PositionLimitZ.Value,
                config.PositionLimitZBack.Value,
                localSmoothing: config.LocalSmoothing.Value,
                remoteSmoothing: config.RemoteSmoothing.Value,
                invertX: true, invertY: false, invertZ: false
            )
        };
    }

    /// <summary>
    /// Hosts the behaviour on a persistent GameObject. For IL2CPP: register the
    /// injected types, create the object, set the DontSave flag, then call
    /// DontDestroyOnLoad. The behaviour is held in a static field so IL2CPP's GC
    /// cannot collect it.
    /// </summary>
    private static void CreateBehaviour(OpenTrackReceiver receiver, TrackingProcessor processor,
        PluginConfig config, PositionProcessor positionProcessor, PositionInterpolator positionInterpolator)
    {
        ClassInjector.RegisterTypeInIl2Cpp<HeadTrackingBehaviour>();
        if (config.DiagnosticLogging.Value)
        {
            ClassInjector.RegisterTypeInIl2Cpp<FramePhaseProbe>();
        }

        _behaviourObject = new GameObject("HeadTrackingController");
        _behaviourObject.hideFlags = HideFlags.DontSave;
        UnityEngine.Object.DontDestroyOnLoad(_behaviourObject);

        _behaviour = _behaviourObject.AddComponent<HeadTrackingBehaviour>();
        _behaviour.Initialize(receiver, processor, config, positionProcessor, positionInterpolator);
    }

    private static void StartReceiver(OpenTrackReceiver receiver)
    {
        receiver.Log = msg => Logger.LogInfo(msg);
        if (receiver.Start(OpenTrackReceiver.DefaultPort))
        {
            Logger.LogInfo($"Listening for tracker data on UDP port {OpenTrackReceiver.DefaultPort}");
        }
    }

    public override bool Unload()
    {
        Logger.LogInfo($"Unloading {PluginName}...");

        _harmony?.UnpatchSelf();
        _receiver?.Dispose();

        _behaviour = null;
        if (_behaviourObject != null)
        {
            GameObject.Destroy(_behaviourObject);
            _behaviourObject = null;
        }

        Logger.LogInfo($"{PluginName} unloaded.");

        _logFile?.Dispose();
        _logFile = null;
        return true;
    }
}
