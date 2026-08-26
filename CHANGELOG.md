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

### Changed

- Changed centring to be the tracker's job. Set it there, with OpenTrack's Center
  bind, the CENTER button in a phone app, or your headset's own centring, and the
  mod applies what the tracker sends. Two centres in series was the problem: when
  the view was off you could not tell which side was wrong, and switching trackers
  meant centring in both
- Changed rotation smoothing to two config keys, `LocalSmoothing` (default 0.0)
  and `RemoteSmoothing` (default 0.15), selected per connection from the packet
  source address. It was previously hardcoded to the internal baseline

### Fixed

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
