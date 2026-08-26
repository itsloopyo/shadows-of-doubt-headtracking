# Shadows of Doubt Head Tracking

![Shadows of Doubt running with this mod](https://raw.githubusercontent.com/itsloopyo/shadows-of-doubt-headtracking/main/assets/readme-clip.gif)

An unofficial mod that adds [OpenTrack](https://github.com/opentrack/opentrack) compatible 6dof head tracking to [Shadows of Doubt](https://store.steampowered.com/app/986130/Shadows_of_Doubt/), your head moves the camera while your mouse keeps controlling aim, no VR headset required.

> [!CAUTION]
> **Early dev build**
>
> This mod has not been comprehensively tested. It may be buggy.

## Features

- **Decoupled look and aim** - head tracking moves the camera; aim stays on your mouse or controller
- **6DOF positional tracking** - lean and peek with head position, not just rotation

## Requirements

- [Shadows of Doubt](https://store.steampowered.com/app/986130/Shadows_of_Doubt/) on Steam
- [OpenTrack](https://github.com/opentrack/opentrack) or any OpenTrack-compatible head tracking source (webcam, smartphone app, or dedicated hardware)
- Windows 10 or 11 (64-bit)

## Installation

1. Download the installer ZIP from the [Releases page](https://github.com/itsloopyo/shadows-of-doubt-headtracking/releases).
2. Extract the ZIP anywhere.
3. Double-click `install.cmd`. On a clean install, BepInEx 6 IL2CPP must run the game once to generate interop assemblies; the installer prompts you to launch the game and then re-run `install.cmd` to deploy the mod DLLs.
4. Configure OpenTrack to output UDP to `127.0.0.1:4242`.
5. Launch the game.

If the installer can't find your game, point it at the install root explicitly:

- Set the `SHADOWS_OF_DOUBT_PATH` environment variable to the game folder, or
- Pass the path as a positional argument: `install.cmd "D:\Games\Shadows of Doubt"`

### Manual Installation

For users who prefer placing files by hand:

1. Download [BepInEx 6.x IL2CPP win_x64](https://builds.bepinex.dev/projects/bepinex_be) (the `Unity.IL2CPP_win_x64` build) and extract the contents to your game folder (default: `C:\Program Files (x86)\Steam\steamapps\common\Shadows of Doubt`).
2. Launch the game once and let BepInEx generate interop assemblies, then close it.
3. Copy the mod DLLs from the Nexus ZIP (or the installer ZIP's `plugins/` folder) into `BepInEx/plugins/`:
   - `ShadowsOfDoubtHeadTracking.dll`
   - `CameraUnlock.Core.dll`
   - `CameraUnlock.Core.Unity.dll`

## Setting Up OpenTrack

1. Download and install [OpenTrack](https://github.com/opentrack/opentrack/releases).
2. Configure your tracker as input.
3. Set output to **UDP over network**.
4. Host: `127.0.0.1`, Port: `4242`.
5. Start tracking before launching the game.

### VR Headset Setup

1. Connect the headset to your PC with Air Link (Quest) or [Virtual Desktop](https://www.vrdesktop.net/).
2. Start [SteamVR](https://store.steampowered.com/app/250820/SteamVR/)
3. In OpenTrack, set the input to **SteamVR**.
4. Set output to **UDP over network** at `127.0.0.1:4242`.
5. Start tracking before launching the game.
6. Center the headset in SteamVR or OpenTrack 

### Webcam Setup

No special hardware required. OpenTrack's built-in **neuralnet tracker** uses any webcam for 6DOF face tracking, with no markers and no IR hardware.

1. In OpenTrack, set the input to **neuralnet tracker**.
2. Select your webcam in the tracker settings.
3. Set output to **UDP over network** at `127.0.0.1:4242`.
4. Start tracking before launching the game.
5. Center in OpenTrack with its Center hotkey while you are looking straight at the screen.

### Phone App Setup

The mod takes the OpenTrack UDP protocol on port `4242` and nothing else, so a phone app works here if it can send that protocol, either itself or through a companion app on the PC. For one that can, what decides how you wire it up is how much filtering it does before the packet leaves the phone. An app that filters on-device can send straight to the game. A raw or lightly filtered feed will jitter if you send it direct, because the mod's smoothing is sized to take the edge off a clean signal rather than to rescue a noisy one, and an app like that should go through OpenTrack so its filters and curves can clean the feed up first.

Try direct first, hold your head still, and watch the view. If it drifts or shakes, route it through OpenTrack.

I made [Headcam](https://headcam.app) so decent tracking would be free for anybody with a phone. It filters on-device, so it can send direct. Any app that filters enough noise works the same way.

**Sending direct:**

1. Install an OpenTrack-compatible head tracking app on your phone.
2. Configure the app to send to your PC's IP on port `4242` (run `ipconfig` to find your PC's IP).
3. Set the protocol to OpenTrack/UDP.

**Relaying through OpenTrack:**

1. Set OpenTrack's input to **UDP over network** on a different port, e.g. `5252`.
2. Point the phone app at that port.
3. Set OpenTrack's output to **UDP over network** at `127.0.0.1:4242`.
4. Allow incoming UDP on the input port through your firewall.

Port 4242 is bound on every interface, which is what lets a phone on your WiFi reach it. Any device that can route to your PC on that port can move your view, so keep the firewall rule scoped to your own network rather than opening 4242 to the internet. Nothing is ever sent out: the mod only receives.

Which smoothing value applies is decided by the sender's address, not by which machine it runs on. Only `127.x.x.x` counts as local, so a phone on WiFi is a remote connection and gets `RemoteSmoothing`. So does OpenTrack running on this same PC if you point it at your LAN address instead of `127.0.0.1`, because the classifier sees a transport and not a machine.

## Controls

Two equivalent binding sets. Use whichever your keyboard has.

| Action              | Nav-cluster | Chord           |
|---------------------|-------------|-----------------|
| Toggle tracking     | `End`       | `Ctrl+Shift+Y`  |
| Cycle tracking mode | `Page Up`   | `Ctrl+Shift+G`  |
| Toggle yaw mode     | `Page Down` | `Ctrl+Shift+H`  |

`Page Up` / `Ctrl+Shift+G` cycles through tracking modes:

1. Normal head-tracked gameplay (rotation + position).
2. Position disabled, rotation only.
3. Rotation disabled, position only.
4. Back to normal.

## Configuration

The mod creates a config file at `BepInEx/config/com.headtracking.shadowsofdoubt.cfg` on first run. Edit it to customize:

A comment has to sit on its own line. BepInEx splits each line at the first `=`
and takes everything after it as the value, so a trailing `# note` becomes part
of the value, the conversion fails, and the entry silently keeps its default.
The only trace is a line in `BepInEx/LogOutput.log`. Put explanations above the
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
# Only for producing a diagnostic log; it writes several lines every few seconds.
DiagnosticLogging = false

[Camera]
# Degrees added to the game's own Field of View slider (-30 to 60)
FieldOfViewOffset = 0.0

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

Both smoothing values cover rotation and position; the mod picks one per
connection from the packet source address.

`FieldOfViewOffset` widens the view past what the game's own Field of View
slider allows. It is added to whatever the game renders with, so the sprint kick
and the interaction zoom still work, and the crosshair compensation follows it
automatically.

## Troubleshooting

The mod writes `HeadTracking.log` next to `Shadows of Doubt.exe`. It holds this
mod's lines only, and it starts fresh on every launch, so it is the first thing
to read and the thing to attach to a bug report. Setting `DiagnosticLogging =
true` adds the camera rig and pose probes to it.

**Mod not loading:**
- Ensure BepInEx 6.x IL2CPP is installed (not BepInEx 5.x).
- Run the game once after BepInEx installation so interop assemblies generate, then re-run `install.cmd`.
- Verify the mod DLLs sit directly in `BepInEx/plugins/`, not in a subfolder.
- Check `BepInEx/LogOutput.log` for errors.

**No tracking response:**
- Read `HeadTracking.log`. A `First tracker packet accepted from ...` line means packets are arriving and the problem is further down; no such line means they are not.
- Verify OpenTrack (or your phone app) is running and outputting data.
- Confirm the UDP port matches (default `4242`).
- Press `End` (or `Ctrl+Shift+Y`) to enable tracking.
- Check that your firewall isn't blocking UDP on port `4242`.

**Jittery or unstable tracking:**
- Raise `RemoteSmoothing` toward `0.30` for a phone or other network tracker, or `LocalSmoothing` for a tracker running on this PC. Write the value with nothing after it on the line.
- Drop the per-axis sensitivity values closer to `1.0` if the camera overshoots.
- For wireless phone trackers on WiFi, prefer a wired PC and a 5GHz connection to reduce packet jitter.

**Camera moves in the wrong direction:**
- Toggle `InvertYaw`, `InvertPitch`, or `InvertRoll` in the config file (or use a negative sensitivity value).
- For yaw drift at extreme up/down angles, switch yaw mode with `Page Down` (or `Ctrl+Shift+H`). World-locked is horizon-stable; camera-local follows the camera's current up-axis.

**View is off-center:**
- Center in your tracker app: OpenTrack's Center bind, the CENTER button in a phone app, or your headset's own centering.

**A config edit had no effect:**
- Make sure nothing follows the value on the line. A trailing `# comment` is read as part of the value, the entry falls back to its default, and the game gives no sign of it. `BepInEx/LogOutput.log` records the failed conversion.

## Updating

Download the new release and run `install.cmd` again. Your config is preserved.

## Uninstalling

Run `uninstall.cmd` from the release folder. This removes the mod DLLs. BepInEx is only removed if the installer originally put it there. To force-remove BepInEx anyway:

```powershell
uninstall.cmd /force
```

## Building from Source

Prerequisites:

- [.NET SDK](https://dotnet.microsoft.com/download) 6.0+
- [pixi](https://pixi.sh) task runner

```powershell
git clone --recurse-submodules https://github.com/itsloopyo/shadows-of-doubt-headtracking.git
cd shadows-of-doubt-headtracking
pixi run build      # Release build
pixi run install    # Build and deploy to the game directory
pixi run package    # Create release ZIPs
```

## Community & Support

- [Discord](https://discord.com/invite/dxyZdyFNT9) - setup help, bug reports, and new-release announcements
- [Lopari](https://lopari.app) - free Windows launcher with one-click install and launch of head-tracking mods
- [Headcam](https://headcam.app) - free app that turns your phone into a head tracker

## License

MIT License - see [LICENSE](LICENSE) for details. Copyright (c) 2026 itsloopyo.

Third-party components bundled or referenced by this mod retain their own licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Credits

- [ColePowered Games](https://colepowered.com/) for Shadows of Doubt
- [BepInEx](https://github.com/BepInEx/BepInEx) (Unity IL2CPP modding framework)
- [HarmonyX](https://github.com/BepInEx/HarmonyX) (runtime patching)
- [OpenTrack](https://github.com/opentrack/opentrack) (head tracking software)

## Disclaimer

This mod is not affiliated with, endorsed by, or supported by ColePowered Games. "Shadows of Doubt" is a trademark of ColePowered Games. Use at your own risk; no warranty is provided.
