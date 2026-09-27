// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using CameraUnlock.Core.Config;
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
    private ConfigOwner<ShadowsOfDoubtConfig>? _configOwner;
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

        ShadowsOfDoubtConfig config = LoadConfig();

        _receiver = new OpenTrackReceiver();
        _processor = BuildRotationProcessor(config);
        _positionProcessor = BuildPositionProcessor(config);
        _positionInterpolator = new PositionInterpolator();

        CreateBehaviour(_receiver, _processor, config, _positionProcessor, _positionInterpolator, SaveConfig);

        StartReceiver(_receiver);

        ModState.Instance.IsEnabled = config.EnableOnStartup;

        Logger.LogInfo($"{PluginName} v{PluginVersion} loaded - tracking is " +
                       $"{(ModState.Instance.IsEnabled ? "ENABLED" : "DISABLED")} on startup");
    }

    /// <summary>
    /// The settings live in BepInEx\config\CameraUnlock.ini, read and written by core's config
    /// owner, with rows set to default following the player's Defaults.ini. Nothing is bound on
    /// the plugin's Config, so ConfigurationManager does not list them. While CameraUnlock.ini is
    /// absent the owner imports the plugin's .cfg, the file every earlier build read, through the
    /// frozen reader on a ConfigFile of its own, and never writes that file.
    ///
    /// The mod has nothing on screen to show a message with, so the owner's messages for the
    /// player go to the log beside its other lines.
    /// </summary>
    private ShadowsOfDoubtConfig LoadConfig()
    {
        ConfigOwnerOptions<ShadowsOfDoubtConfig> options =
            ShadowsOfDoubtConfig.Options(ConfigPath, Config.ConfigFilePath, DefaultsFile.PerUser());
        options.StatusSink = message => Logger.LogWarning(message);
        _configOwner = new ConfigOwner<ShadowsOfDoubtConfig>(options);

        ConfigLoadResult<ShadowsOfDoubtConfig> loaded = _configOwner.Load();

        // The owner writes each diagnostic as "<path>: <description>" among lines that only
        // report what it did, so the complaints are picked out by their text.
        var complaints = new HashSet<string>();
        foreach (CanonicalDiagnostic diagnostic in loaded.Diagnostics)
        {
            complaints.Add(ConfigPath + ": " + diagnostic.Describe());
        }
        bool usable = loaded.Status == ConfigLoadStatus.Canonical
                      || loaded.Status == ConfigLoadStatus.Migrated
                      || loaded.Status == ConfigLoadStatus.Created;
        foreach (string line in loaded.Log)
        {
            if (usable && !complaints.Contains(line)) Logger.LogInfo(line);
            else Logger.LogWarning(line);
        }
        Logger.LogInfo($"Config {ConfigPath}: {loaded.Status}");
        return loaded.Config;
    }

    private static string ConfigPath => Path.Combine(Paths.ConfigPath, "CameraUnlock.ini");

    /// <summary>
    /// Called after the new value is already applied. A save that fails is logged and the
    /// session keeps the new value.
    /// </summary>
    private void SaveConfig(Action<ShadowsOfDoubtConfig> change)
    {
        ConfigSaveResult saved = _configOwner!.Save(change);
        if (saved.Status == ConfigSaveStatus.Saved)
        {
            // A row that held default and now holds a value, so it stops following
            // Defaults.ini in this game.
            foreach (string line in saved.Log) Logger.LogInfo(line);
            return;
        }
        foreach (string line in saved.Log) Logger.LogWarning(line);
        Logger.LogWarning($"{ConfigPath}: {saved.Status}: {saved.Reason} The change applies to this session only.");
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

    // The published builds shipped pitch inverted and every position axis at 2.0, as settings.
    // They are this engine's conversion from the tracker's axes, so they stay, in code.
    private const float PositionMultiplier = 2.0f;

    /// <summary>
    /// The rotation pipeline applies no sensitivity and no deadzone. Pitch is inverted, which is
    /// the conversion from OpenTrack's pitch sign to Unity's.
    /// </summary>
    private static TrackingProcessor BuildRotationProcessor(ShadowsOfDoubtConfig config)
    {
        return new TrackingProcessor
        {
            // Selected per connection: loopback senders get LocalSmoothing, remote
            // network devices get RemoteSmoothing. Both cover rotation and position.
            LocalSmoothing = config.LocalSmoothing,
            RemoteSmoothing = config.RemoteSmoothing,
            Sensitivity = new SensitivitySettings(1f, 1f, 1f, invertYaw: false, invertPitch: true, invertRoll: false),
            Deadzone = DeadzoneSettings.None
        };
    }

    private static PositionProcessor BuildPositionProcessor(ShadowsOfDoubtConfig config)
    {
        return new PositionProcessor
        {
            Settings = new PositionSettings(
                PositionMultiplier,
                PositionMultiplier,
                PositionMultiplier,
                config.Position.LimitX,
                config.Position.LimitY,
                config.Position.LimitYDown,
                config.Position.LimitZ,
                config.Position.LimitZBack,
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
        ShadowsOfDoubtConfig config, PositionProcessor positionProcessor, PositionInterpolator positionInterpolator,
        Action<Action<ShadowsOfDoubtConfig>> saveConfig)
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
        _behaviour.Initialize(receiver, processor, config, saveConfig, positionProcessor, positionInterpolator);
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
