# Changelog

## [Unreleased]

### Added

- Added the mod itself: head tracking for Shadows of Doubt over the OpenTrack
  UDP protocol, decoupled from aim, with 6DOF lean
- Added centring of the game window on its monitor at startup when it opens
  windowed. It used to land against the top-left of the desktop, which on a very
  wide monitor puts the view a long way off to one side of where your tracker is
  centred
- Added a startup log line for the UDP port and a one-shot `Tracker data received`
  line the first time packets arrive, so a "no head tracking" report is answerable
  from `BepInEx/LogOutput.log` alone
- The tracking mode you switch to with `Page Up` / `Ctrl+Shift+G` and the yaw
  mode you switch to with `Page Down` / `Ctrl+Shift+H` are saved to `CameraUnlock.ini`
  and come back at the next start. Turning tracking on or off with `End` is not saved
- A setting set to `default` in `CameraUnlock.ini` takes its value from `Defaults.ini`, which every head tracking mod that keeps its settings in `CameraUnlock.ini` reads. Head tracking mods that keep their settings in another file do not read it, and neither do earlier versions of this mod. Writing a value in place of `default` changes that setting for this game only. When the mod saves a setting that a hotkey changed in game, it writes the new value in place of `default`, so that setting no longer follows `Defaults.ini` in this game until you set it to `default` again.
- `Defaults.ini` is `%AppData%\CameraUnlock\Defaults.ini` on Windows; `$XDG_CONFIG_HOME/CameraUnlock/Defaults.ini` on Linux, or `~/.config/CameraUnlock/Defaults.ini` where `XDG_CONFIG_HOME` is not set, under Wine and Proton too; and `~/Library/Application Support/CameraUnlock/Defaults.ini` on macOS. The mod's log, where it writes one, names the file it read.
- When the mod starts and finds no `Defaults.ini`, it creates one holding the built-in values, unless Windows runs the game as a packaged app, or the game runs on Linux or macOS without Wine or Proton. The mod never changes `Defaults.ini` after that.

### Changed

- Changed centring to be the tracker's job. Set it there, with OpenTrack's Center
  bind, the CENTER button in a phone app, or your headset's own centring, and the
  mod applies what the tracker sends. Two centres in series was the problem: when
  the view was off you could not tell which side was wrong, and switching trackers
  meant centring in both
- Changed rotation smoothing to two config keys, `LocalSmoothing` (default 0.0)
  and `RemoteSmoothing` (default 0.15), selected per connection from the packet
  source address. It was previously hardcoded to the internal baseline
- Settings move to `BepInEx\config\CameraUnlock.ini`. Earlier versions of the mod kept these settings in `com.headtracking.shadowsofdoubt.cfg`, in the same folder. The first time this version starts and finds no `CameraUnlock.ini`, it reads your settings from `com.headtracking.shadowsofdoubt.cfg` and writes them into `CameraUnlock.ini`. It never changes `com.headtracking.shadowsofdoubt.cfg`, and does not read it again while `CameraUnlock.ini` exists.
- A setting that the defaults the README shows set to `default` is written as `default` when you never changed it from the default earlier versions used, because `com.headtracking.shadowsofdoubt.cfg` does not hold it or holds that default. It then follows `Defaults.ini`, so it takes the value `Defaults.ini` gives it, or the built-in value where `Defaults.ini` gives none, which can differ from the default earlier versions used. A setting you changed is written with the value imported for it, or as `default` where that value equals its default at that start.
- `RotationEnabled` and `PositionEnabled` are one setting here, the tracking mode, so both are written as `default` or neither is.
- The key that cycles the tracking mode is `CycleTrackingModeKey` in `CameraUnlock.ini`. Earlier versions called it `TogglePositionKey`.
- Comments, and keys the mod never read, are not carried over. Nor are these, where your old file had them:
  - A sensitivity, scale, deadzone, response curve or axis inversion you changed from its default. Set these in your tracker instead.
  - A hotkey set to Ctrl, Shift or Alt on its own. That key goes down before the key of any chord made with it, so the hotkey is left unbound, and it keeps its Ctrl+Shift chord where it has one.
- An older version of the mod reads `com.headtracking.shadowsofdoubt.cfg` and never reads `CameraUnlock.ini`, so a setting you change after updating is not in `com.headtracking.shadowsofdoubt.cfg`.
- Deleting only `CameraUnlock.ini` makes the next start read `com.headtracking.shadowsofdoubt.cfg` again. To go back to the defaults, replace everything in `CameraUnlock.ini` with the defaults the README shows. Every setting they set to `default` then follows `Defaults.ini`.
- BepInEx's ConfigurationManager no longer lists these settings. Edit `BepInEx\config\CameraUnlock.ini` with any text editor.
- Hotkeys are written as key names, and each hotkey lists every key that triggers it, the Ctrl+Shift chord included: `ToggleKey=End, Ctrl+Shift+Y`.
- A hotkey bound to a plain key no longer fires while Ctrl and Shift are both held, so Ctrl+Shift with that key reaches only a binding that names the chord.
- On Linux and macOS without Wine or Proton, this version reads its settings and saves none: it creates no `CameraUnlock.ini`, reads your settings from `com.headtracking.shadowsofdoubt.cfg` again at every start while there is no `CameraUnlock.ini`, and a change made in game lasts until the game closes.

### Fixed

- Fixed the vertical lean limits being smaller than in the other head tracking mods. Earlier versions shipped 0.15 m up and 0.05 m down; `PositionLimitY` and `PositionLimitYDown` now match the other mods at 0.20 m up and 0.20 m down, unless you changed them. Where you never changed them they are written as `default` and follow `Defaults.ini`, whose built-in value is 0.20 m. To keep the old travel in this game, write `PositionLimitY=0.15` and `PositionLimitYDown=0.05` in `CameraUnlock.ini`.
- Fixed the camera freezing at the last head-tracked rotation when you disabled
  head tracking, entered a menu or lost tracker data, whenever positional tracking
  was off. Handing the camera back to Unity is now tracked separately from the lean
  offset, which is zero whenever your head is centred, so it happens whenever
  tracking stops rather than only when you happened to be leaning
- Fixed world-anchored markers sliding off their targets while you lean. The lean
  now stays applied for the whole frame, instead of being stripped at the top of
  LateUpdate and re-applied once the game's camera controller had run, so the
  transform the renderer reads and the view matrix the game's world-to-screen
  projection reads agree with each other
- Fixed the interaction point following your head. The game's interaction raycast
  goes through `Camera.ScreenPointToRay`, which reads the head-tracked view matrix,
  so the on-screen marker framed whatever your head was pointed at and you had to
  click the marker rather than the thing you were looking at. The game's own raycast
  method now runs against a clean camera. The Harmony patches on Unity's Camera
  helpers that were supposed to prevent this never ran: those are engine internal
  calls, which native game code invokes without passing through the managed wrapper
  Harmony can reach
- Fixed leaning not moving the view, and world-anchored markers not staying on the
  thing they mark. The transform that carries the lean was resolved once, when the
  camera first appeared on the menu, but the game reparents that same camera into
  the first-person rig afterwards, so the lean was being written to a transform that
  no longer fed the camera. It is now resolved from the camera's current parent chain
  every frame
- Fixed the crosshair parking on the right screen edge no matter which way you
  turned when a hard head turn puts the aim point behind the rendered view. It now
  parks on the correct edge

### Removed

- Removed recentring entirely: the `Home` / `Ctrl+Shift+T` hotkey, the `RecenterKey`
  and `AutoRecenterOnGameplayEntry` config keys, and the mod's own centre
- Removed the `PositionSmoothing` key. Position now uses the same
  connection-selected value as rotation
- Removed the hidden 0.15 baseline smoothing floor, so local trackers get
  zero-latency tracking by default
- The sensitivity, scale, deadzone, response curve and axis inversion settings. Set these in your tracker app instead.
- With these settings at their shipped defaults the camera moves as it did before.
