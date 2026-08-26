# Shadows of Doubt Head Tracking - Installation Guide

## Requirements

- **Shadows of Doubt** (Steam)
- **Windows 10/11** (64-bit)
- **[OpenTrack](https://github.com/opentrack/opentrack)** or an OpenTrack-compatible head tracking app (smartphone, webcam, or dedicated hardware)

## Installation

### Automatic Installation (Recommended)

1. Extract all files from the zip to a folder
2. Double-click **install.cmd**
3. The installer will:
   - Find your Shadows of Doubt installation automatically
   - Install BepInEx 6 IL2CPP if not present
   - Deploy mod DLLs to BepInEx/plugins

**Note:** Shadows of Doubt uses IL2CPP, so BepInEx needs to generate interop assemblies on first launch. The installer will prompt you to run the game once if needed.

If the installer can't find your game, see [Custom Game Path](#custom-game-path) below.

### Manual Installation

1. Download [BepInEx 6.x IL2CPP win_x64](https://builds.bepinex.dev/projects/bepinex_be) (Unity.IL2CPP_win_x64) and extract to your game folder:
   - Default: `C:\Program Files (x86)\Steam\steamapps\common\Shadows of Doubt`
2. Launch the game once to generate interop assemblies (may take a few minutes), then close it
3. Copy these files to `BepInEx/plugins/`:
   - `ShadowsOfDoubtHeadTracking.dll`
   - `CameraUnlock.Core.dll`
   - `CameraUnlock.Core.Unity.dll`

### Custom Game Path

If your game is installed in a non-standard location, set the `SHADOWS_OF_DOUBT_PATH` environment variable or pass the path directly:

```
install.cmd "D:\Games\Shadows of Doubt"
```

## Setting Up OpenTrack

1. Download and install [OpenTrack](https://github.com/opentrack/opentrack/releases)
2. Configure your tracker (Input):
   - For webcam: Select "neuralnet tracker"
   - For phone app: Select "UDP over network"
3. Configure output:
   - Select **UDP over network**
   - Host: `127.0.0.1`
   - Port: `4242`
4. Click **Start** to begin tracking
5. Launch Shadows of Doubt

### Phone App Setup

This mod includes built-in smoothing to handle network jitter, so if your tracking app already provides a filtered signal, you can send directly from your phone to the mod on port 4242 without needing OpenTrack on PC.

1. Install an OpenTrack-compatible head tracking app from your phone's app store
2. Configure your phone app to send to your PC's IP address on port 4242 (run `ipconfig` to find it, e.g. `192.168.1.100`)
3. Set the protocol to OpenTrack/UDP
4. Start tracking

If you want curve mapping, a visual preview, or extra filtering, route through OpenTrack instead. Since the mod already listens on 4242, OpenTrack's input needs a different port:

1. In OpenTrack, set Input to UDP over network on port 5252 (or any port other than 4242)
2. Set Output to UDP over network at `127.0.0.1:4242`
3. In your phone app, send to your PC's IP on port 5252
4. Open port 5252 in your firewall for incoming UDP traffic

## Controls

| Key | Action |
|-----|--------|
| **End** | Toggle head tracking on/off |
| **Page Up** | Toggle positional tracking on/off |

## Verifying Installation

1. Start OpenTrack and enable tracking
2. Launch Shadows of Doubt
3. Once in-game, move your head - the camera should follow
4. If the view sits off-centre, centre it in your tracker app: OpenTrack's Center bind, the CENTER button in a phone app, or your headset's own centring. The mod applies what the tracker sends and keeps no centre of its own.

The mod writes `HeadTracking.log` next to `Shadows of Doubt.exe`, starting fresh
on every launch. A working session opens with:
```
Head Tracking v0.0.0 - session started 2026-01-01 12:00:00
[12:00:00] [Info] Head Tracking v0.0.0 loaded - tracking is ENABLED on startup
[12:00:07] [Info] Listening for tracker data on UDP port 4242
[12:00:31] [Info] Head tracking camera: Map/Player/.../Main Camera
```
BepInEx's own `BepInEx/LogOutput.log` covers everything before the mod loads.

## Configuration

The mod creates a config file at `BepInEx/config/com.headtracking.shadowsofdoubt.cfg` on first run.

A comment has to sit on its own line. BepInEx splits each line at the first `=`
and takes everything after it as the value, so a trailing `# note` becomes part
of the value, the conversion fails, and the entry silently keeps its default -
the only trace is a line in `BepInEx/LogOutput.log`. Put explanations above the
key, never after it.

```ini
[General]
# Start with tracking enabled
EnabledOnStartup = true
# true = horizon-locked yaw (default), false = camera-local
WorldSpaceYaw = true

[Behavior]
# Pause tracking when alt-tabbed
PauseOnLostFocus = true
# Write the camera rig, frame-phase and pose probes to HeadTracking.log.
# Only for producing a diagnostic log - it writes several lines every few seconds.
DiagnosticLogging = false

[Hotkeys]
ToggleKey = End
TogglePositionKey = PageUp
# Toggle world-locked vs camera-local yaw
YawModeKey = PageDown

[Sensitivity]
# Horizontal rotation (-5.0 to 5.0)
YawSensitivity = 1.0
# Vertical rotation (-5.0 to 5.0)
PitchSensitivity = 1.0
# Tilt (-5.0 to 5.0)
RollSensitivity = 1.0

[CoordinateTransform]
InvertYaw = false
# Default matches OpenTrack to Unity
InvertPitch = true
InvertRoll = false

[Position]
# Enable lean/positional tracking
PositionEnabled = true
# Lateral sensitivity (0.0-3.0)
PositionSensitivityX = 2.0
# Vertical sensitivity (0.0-3.0)
PositionSensitivityY = 2.0
# Depth sensitivity (0.0-3.0)
PositionSensitivityZ = 2.0
# Max lateral offset in meters
PositionLimitX = 0.30
# Max upward offset in meters
PositionLimitY = 0.15
# Max downward offset in meters
PositionLimitYDown = 0.05
# Max forward offset in meters
PositionLimitZ = 0.40
# Max backward offset in meters
PositionLimitZBack = 0.10

[Smoothing]
# Tracker on this machine (loopback), 0 = none, 1 = heavy
LocalSmoothing = 0.0
# Tracker on a remote network device, 0 = none, 1 = heavy
RemoteSmoothing = 0.15
```

## Troubleshooting

### "BepInEx interop assemblies not generated"

Run the game once after installing BepInEx to generate the required assemblies.

### Mod not loading

1. Ensure BepInEx 6.x IL2CPP is installed (not BepInEx 5.x)
2. Verify DLLs are in `BepInEx/plugins/` (not a subfolder)
3. Check `BepInEx/LogOutput.log` for errors

### Camera not responding

1. Read `HeadTracking.log` next to the game exe - a `First tracker packet accepted from ...` line means packets are arriving
2. Verify OpenTrack is running and tracking is active
3. Check UDP output is set to `127.0.0.1:4242`
4. Press **End** to make sure tracking is enabled
5. If the view sits off-centre, centre it in your tracker app (OpenTrack Center bind, phone-app CENTER button, or your headset). The mod has no centre of its own.

### Wrong movement direction

- Adjust `InvertYaw`, `InvertPitch`, or `InvertRoll` in the config file
- Or use negative sensitivity values

### A config edit had no effect

- Make sure nothing follows the value on the line. A trailing `# comment` is read as part of the value, the entry falls back to its default, and the game gives no sign of it. `BepInEx/LogOutput.log` records the failed conversion.

### Game crashes on startup

1. Remove the mod files from `BepInEx/plugins/`
2. If game still crashes, reinstall BepInEx
3. Report the issue with `HeadTracking.log` (next to the game exe) and `BepInEx/LogOutput.log`

## Uninstalling

### Automatic

Run `uninstall.cmd` from the release folder.

### Manual

#### Remove Mod Only (Keep BepInEx)

Delete from `BepInEx/plugins/`:
- `ShadowsOfDoubtHeadTracking.dll`
- `CameraUnlock.Core.dll`
- `CameraUnlock.Core.Unity.dll`

#### Complete Removal

Delete these from the game folder:
- `BepInEx/` folder
- `doorstop_config.ini`
- `winhttp.dll`

## License

MIT License - see [LICENSE](LICENSE), which ships in every release ZIP. Copyright (c) 2026 itsloopyo.

Third-party components bundled with the mod retain their own licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
