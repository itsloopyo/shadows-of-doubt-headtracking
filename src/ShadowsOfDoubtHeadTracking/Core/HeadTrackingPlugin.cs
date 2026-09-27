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
using ShadowsOfDoubtHeadTracking.Legacy;
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
    private ModConfig? _config;
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

        ModConfig config = LoadConfig();
        _config = config;

        _receiver = new OpenTrackReceiver();
        _processor = BuildRotationProcessor(config);
        _positionProcessor = BuildPositionProcessor(config);
        _positionInterpolator = new PositionInterpolator();

        CreateBehaviour(_receiver, _processor, config, _positionProcessor, _positionInterpolator);

        StartReceiver(_receiver);

        ModState.Instance.IsEnabled = config.EnabledOnStartup;

        Logger.LogInfo($"{PluginName} v{PluginVersion} loaded - tracking is " +
                       $"{(ModState.Instance.IsEnabled ? "ENABLED" : "DISABLED")} on startup");
    }

    /// <summary>
    /// Reads the plugin's .cfg through the frozen reader, then saves it once, which is the write
    /// every published build's Bind calls made at each start.
    /// </summary>
    private ModConfig LoadConfig()
    {
        LegacyConfig legacy = LegacyConfigReader.Read(Config, out _);
        Config.Save();
        return new ModConfig
        {
            YawSensitivity = legacy.YawSensitivity,
            PitchSensitivity = legacy.PitchSensitivity,
            RollSensitivity = legacy.RollSensitivity,
            EnabledOnStartup = legacy.EnabledOnStartup,
            WorldSpaceYaw = legacy.WorldSpaceYaw,
            InvertYaw = legacy.InvertYaw,
            InvertPitch = legacy.InvertPitch,
            InvertRoll = legacy.InvertRoll,
            ToggleKey = legacy.ToggleKey,
            CycleTrackingModeKey = legacy.CycleTrackingModeKey,
            YawModeKey = legacy.YawModeKey,
            PauseOnLostFocus = legacy.PauseOnLostFocus,
            DiagnosticLogging = legacy.DiagnosticLogging,
            FieldOfViewOffset = legacy.FieldOfViewOffset,
            PositionEnabled = legacy.PositionEnabled,
            PositionSensitivityX = legacy.PositionSensitivityX,
            PositionSensitivityY = legacy.PositionSensitivityY,
            PositionSensitivityZ = legacy.PositionSensitivityZ,
            PositionLimitX = legacy.PositionLimitX,
            PositionLimitY = legacy.PositionLimitY,
            PositionLimitYDown = legacy.PositionLimitYDown,
            PositionLimitZ = legacy.PositionLimitZ,
            PositionLimitZBack = legacy.PositionLimitZBack,
            LocalSmoothing = legacy.LocalSmoothing,
            RemoteSmoothing = legacy.RemoteSmoothing,
        };
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

    private static TrackingProcessor BuildRotationProcessor(ModConfig config)
    {
        return new TrackingProcessor
        {
            // Selected per connection: loopback senders get LocalSmoothing, remote
            // network devices get RemoteSmoothing. Both cover rotation and position.
            LocalSmoothing = config.LocalSmoothing,
            RemoteSmoothing = config.RemoteSmoothing,
            Sensitivity = new SensitivitySettings(
                config.YawSensitivity,
                config.PitchSensitivity,
                config.RollSensitivity,
                invertYaw: config.InvertYaw,
                invertPitch: config.InvertPitch,
                invertRoll: config.InvertRoll
            ),
            Deadzone = DeadzoneSettings.None
        };
    }

    private static PositionProcessor BuildPositionProcessor(ModConfig config)
    {
        return new PositionProcessor
        {
            Settings = new PositionSettings(
                config.PositionSensitivityX,
                config.PositionSensitivityY,
                config.PositionSensitivityZ,
                config.PositionLimitX,
                config.PositionLimitY,
                config.PositionLimitYDown,
                config.PositionLimitZ,
                config.PositionLimitZBack,
                localSmoothing: config.LocalSmoothing,
                remoteSmoothing: config.RemoteSmoothing,
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
        ModConfig config, PositionProcessor positionProcessor, PositionInterpolator positionInterpolator)
    {
        ClassInjector.RegisterTypeInIl2Cpp<HeadTrackingBehaviour>();
        if (config.DiagnosticLogging)
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
