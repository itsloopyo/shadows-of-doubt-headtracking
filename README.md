# Shadows of Doubt Head Tracking

![Shadows of Doubt running with this mod](https://raw.githubusercontent.com/itsloopyo/shadows-of-doubt-headtracking/main/assets/readme-clip.gif)

An unofficial head tracking mod for Shadows of Doubt that moves the view with your head while your mouse or controller keeps aiming, driven by a webcam, phone, or any OpenTrack compatible tracker, with no VR headset required.

> **Settings have moved.** The mod now keeps its settings in `BepInEx\config\CameraUnlock.ini`. If you ran an earlier build, the first start of this one reads your settings from `BepInEx\config\com.headtracking.shadowsofdoubt.cfg` and never changes that file. BepInEx's ConfigurationManager no longer lists these settings: edit `CameraUnlock.ini` with any text editor. See [Configuration](#configuration).

## Features

- **Decoupled look and aim** - head tracking moves the camera; aim stays on your mouse or controller
- **6DOF positional tracking** - lean and peek with head position, not just rotation
- **Works with any OpenTrack compatible tracker** - free options available for PC, iOS and Android

## Requirements

- [Shadows of Doubt](https://store.steampowered.com/app/986130/Shadows_of_Doubt/) on Steam
- [OpenTrack](https://github.com/opentrack/opentrack), or another source that sends OpenTrack UDP pose data to port 4242
- Windows 10 or 11 (64-bit)

## Installation

### Lopari

Download [Lopari](https://lopari.app), choose **Shadows of Doubt**, and click
**Play with head tracking**.

### Standalone Installer

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

The mod listens for OpenTrack pose data on UDP port `4242`, on every network
interface. One datagram is six little-endian 64-bit floats in the order
`x, y, z, yaw, pitch, roll`: position in centimetres, rotation in degrees, 48
bytes in total. Anything that sends that to that port drives the view.
OpenTrack's **UDP over network** output sends exactly this, and the steps below
set it up.

1. Install [OpenTrack](https://github.com/opentrack/opentrack/releases).
2. Pick a tracker under **Input**, using the notes below.
3. Set **Output** to **UDP over network**, host `127.0.0.1`, port `4242`.
4. Press **Start**. Tracking and the game can start in either order.

### Webcam

OpenTrack ships a `neuralnet tracker` input that reads a plain webcam. Select it
under **Input**, pick your camera in its settings, and use the output settings
above. How well it tracks depends on your camera and your lighting, so try it
before buying anything.

### Phone

A phone app can reach the mod directly, with no OpenTrack on the PC, if it sends
the datagram described above. Point it at this PC's IP address (run `ipconfig`
to find it) on port `4242`. Not every phone tracker speaks this protocol, so
check yours for an OpenTrack or UDP output option first. [Headcam](https://headcam.app)
sends it, and I wrote it so decent tracking is free for anyone who already owns
a phone.

Sending direct works when the app filters its own signal on the device. The
mod's smoothing is sized to take the edge off a clean signal rather than to
rescue a noisy one, so a raw feed sent direct will jitter. If it does, point the
app at OpenTrack's **UDP over network** *input* on some other port, say 5252,
and let OpenTrack's filters and curves clean it up before its output forwards to
`127.0.0.1:4242`.

Anything arriving from outside `127.0.0.0/8` counts as a remote connection and
is smoothed with `RemoteSmoothing` rather than `LocalSmoothing`. That includes a
tracker on this very PC that sends to the machine's own LAN address, because the
mod reads the source address and not the machine.

### Headset or other hardware

If your device has an OpenTrack input driver, select it under **Input** and use
the same output settings. OpenTrack's own **Input** list is the authority on
what it can read; the mod only ever sees what OpenTrack sends.

### Centring

Centring belongs to your tracker. The mod subtracts no centre of its own: it
applies the pose it receives exactly as it arrives, so a stream of zeros holds
the view where the game itself puts it. Press the centre control in your tracker
(OpenTrack's **Center** bind, or the CENTER button in Headcam) and the tracker
zeroes its own output, which leaves the view centred with the mod doing nothing.

That is why there is no centre hotkey here and nothing to re-centre in game. Two
centres in series would drift apart, because each side re-centres at moments the
other cannot see, and you would end up pressing twice to centre once. If the
view sits off to one side, centre it in the tracker.

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

The keys are the `ToggleKey`, `CycleTrackingModeKey` and `YawModeKey` lists in the
config file, and each can be rebound, chords included. The tracking mode and the
yaw mode you switch to are saved to `CameraUnlock.ini` and come back at the next
start. Turning tracking on or off is not saved: the mod starts with tracking on or
off as `EnableOnStartup` says.

## Configuration

<!-- cameraunlock:config -->
The mod reads its settings from `BepInEx\config\CameraUnlock.ini` in the game folder, and creates the file when it starts and finds none. Edit it with any text editor.

A setting set to `default` takes its value from `Defaults.ini`, which every head tracking mod that keeps its settings in `CameraUnlock.ini` reads. Head tracking mods that keep their settings in another file do not read it. Writing a value in place of `default` changes that setting for this game only. When the mod saves a setting that a hotkey changed in game, it writes the new value in place of `default`, so that setting no longer follows `Defaults.ini` in this game until you set it to `default` again.

`Defaults.ini` is `%AppData%\CameraUnlock\Defaults.ini` on Windows; `$XDG_CONFIG_HOME/CameraUnlock/Defaults.ini` on Linux, or `~/.config/CameraUnlock/Defaults.ini` where `XDG_CONFIG_HOME` is not set, under Wine and Proton too; and `~/Library/Application Support/CameraUnlock/Defaults.ini` on macOS. The mod's log, where it writes one, names the file it read.

When the mod starts and finds no `Defaults.ini`, it creates one holding the built-in values, unless Windows runs the game as a packaged app, or the game runs on Linux or macOS without Wine or Proton. The mod never changes `Defaults.ini` after that. Edit it with any text editor.

On Linux and macOS without Wine or Proton, this version reads its settings and saves none: it creates no `CameraUnlock.ini` and a change made in game lasts until the game closes.

BepInEx's ConfigurationManager does not list these settings.

The built-in value of each setting set to `default` below:

- `EnableOnStartup=true`
- `WorldSpaceYaw=true`
- `RotationEnabled=true`
- `LocalSmoothing=0.0`
- `RemoteSmoothing=0.15`
- `PositionEnabled=true`
- `PositionLimitX=0.3`
- `PositionLimitY=0.2`
- `PositionLimitYDown=0.2`
- `PositionLimitZ=0.4`
- `PositionLimitZBack=0.1`
- `ToggleKey=End, Ctrl+Shift+Y`
- `CycleTrackingModeKey=PageUp, Ctrl+Shift+G`
- `YawModeKey=PageDown, Ctrl+Shift+H`

With every setting at its default, the file reads:

```ini
; Shadows of Doubt head tracking settings.
; Comments start with ; and go on their own line. Text after a value is part of the value.
; Hotkeys are key names such as End, PageUp or Ctrl+Shift+Y. Separate several with commas; leave empty for none.
; A setting set to default takes its value from Defaults.ini, which every head tracking mod
; that keeps its settings in CameraUnlock.ini reads: %AppData%\CameraUnlock\Defaults.ini on
; Windows, $XDG_CONFIG_HOME/CameraUnlock/Defaults.ini (normally ~/.config/CameraUnlock) on
; Linux, under Wine and Proton too, and ~/Library/Application Support/CameraUnlock/Defaults.ini
; on macOS. The log names the file it read. Write a value instead of default to change that
; setting for this game only.

[CameraUnlock]
; Written by the mod. Leave this section in place.
ConfigFormat=1

[General]
; true: head tracking is on when the game starts. ToggleKey turns it on and off.
EnableOnStartup=default
; true: yaw turns around the world's up axis. false: around the camera's own up axis.
WorldSpaceYaw=default
; true: turning your head turns the view.
; Tracking mode at startup, with PositionEnabled. The mode hotkey changes both.
RotationEnabled=default
; true: head tracking stops moving the view while the game window is not focused.
PauseOnLostFocus=true

[Smoothing]
; Smoothing when the tracker runs on this PC. 0 is the least, 1 the most.
LocalSmoothing=default
; Smoothing when the tracker is another device on the network, such as a phone.
; 0 is the least, 1 the most.
RemoteSmoothing=default

[Position]
; true: moving your head moves the view.
; Tracking mode at startup, with RotationEnabled. The mode hotkey changes both.
PositionEnabled=default
; How far, in metres, leaning left or right can move the view.
PositionLimitX=default
; How far, in metres, raising your head can move the view.
PositionLimitY=default
; How far, in metres, lowering your head can move the view.
PositionLimitYDown=default
; How far, in metres, leaning forward can move the view.
PositionLimitZ=default
; How far, in metres, leaning back can move the view.
PositionLimitZBack=default

[Hotkeys]
; Turns head tracking on and off.
ToggleKey=default
; Changes the tracking mode: rotation and position, rotation only, position only.
CycleTrackingModeKey=default
; Switches yaw between the world's up axis and the camera's own (WorldSpaceYaw).
YawModeKey=default

[Camera]
; Degrees added to the field of view the game renders with, on top of the
; Field of View slider in the game's own settings. 0 leaves the game's value alone.
; The game's sprint kick and interaction zoom still work on top of it.
FieldOfViewOffset=0.0

[Diagnostics]
; true: write the camera rig, frame-phase and pose probes to HeadTracking.log.
; Verbose; turn it on when reporting a problem.
DiagnosticLogging=false
```
<!-- /cameraunlock:config -->

## Troubleshooting

The mod writes `HeadTracking.log` next to `Shadows of Doubt.exe`. It holds this
mod's lines only, and it starts fresh on every launch, so it is the first thing
to read and the thing to attach to a bug report. Setting `DiagnosticLogging=true`
adds the camera rig and pose probes to it.

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
- Lower the sensitivity in your tracker app if the camera overshoots. The mod has no sensitivity setting of its own.
- For wireless phone trackers on WiFi, prefer a wired PC and a 5GHz connection to reduce packet jitter.

**Camera moves in the wrong direction:**
- Invert that axis in your tracker app. The mod has no axis inversion setting of its own.
- For yaw drift at extreme up/down angles, switch yaw mode with `Page Down` (or `Ctrl+Shift+H`). World-locked is horizon-stable; camera-local follows the camera's current up-axis.

**View is off-center:**
- Center in your tracker app: OpenTrack's Center bind, the CENTER button in a phone app, or your headset's own centering.

**A config edit had no effect:**
- Make sure nothing follows the value on the line. Text after a value is part of the value, so a trailing comment makes the line unreadable. The mod's log names each line of `CameraUnlock.ini` it could not read.
- Settings are read at startup. Restart the game after editing the file.

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

- Discord: [Loop's Head Tracking Hangout](https://discord.com/invite/dxyZdyFNT9) - setup help, bug reports, and new-release announcements
- [Lopari](https://lopari.app) - free Windows launcher with one-click install and launch for the released head-tracking mods
- [Headcam](https://headcam.app) - free app that turns your iPhone or Android phone into the head tracker

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
