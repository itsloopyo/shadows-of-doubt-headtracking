# Changelog

## [Unreleased]

### Added

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

## [1.0.4] - 2026-05-01

### Added

- add Invoke-FetchLatestLoader and Refresh-VendoredLoader helpers

### Changed

- Hello World
- Switch from manual DLL references to NuGet packages
- Health check: config-driven position settings, fix CHANGELOG, add THIRD-PARTY-NOTICES
- 6DOF head tracking with decoupled look+aim and interaction fixes
- Add receive timestamp getter and fix crosshair projection model
- Fix changelog generation crash when repo has no tags
- Change TrackerPivotForward default from 0.15f to 0.01f
- Include THIRD-PARTY-NOTICES in release ZIPs
- Fix branch redirect in InjectCallBeforeReturn
- Fix SRP callback registration with IL-emitted DynamicMethod wrappers
- Fix position interpolator sample timing and decay curve
- Add Xbox/Microsoft Store path detection for Firewatch
- Remove smoothing threshold bypass so minimum smoothing always applies
- Use spherical coordinate projection for reticle and add frame interpolation floor to smoothing
- Rename GitHub release zip to -installer suffix
- Add SmoothedEulerState and PositionApplicator shared utilities
- Reduce default Z position limit from 0.20m to 0.10m to prevent camera clipping through the player model
- Add asymmetric Z position limit (limit_z_back) to prevent backward camera clipping
- Fix swapped Z clamp bounds in PositionProcessor
- Add neck pivot overloads, per-axis Euler smoothing, and hotkey config entries
- Remove isRemoteConnection parameter, apply baseline smoothing unconditionally
- Clarify crosshair projection comment
- Add projection distance parameter to CalculateAimScreenOffset
- Add NET35 compatibility to ViewMatrixModifier for pre-Unity 2017.1 targets
- Add Screencheat and Zeepkist to game path detection configs
- Halve frame interpolation speed to smooth tracker sample boundaries
- Add SubnauticaBelowZero and SonsOfTheForest game path configs
- Add CalculateScreenOffsetFromWorldPoint for parallax-correct reticle positioning
- Remove NeckModel from position processing pipeline
- Refactor PoseInterpolator and PositionInterpolator for clarity
- Switch release to prebuilt DLLs and reusable workflow
- Add automatic port retry to OpenTrackReceiver
- Rate-limit scene/focus polling in GameplayStateDetector
- Vendor BepInEx 6 IL2CPP and align with shared install-time doctrine
- Add prediction-error correction to interpolators for smooth high-FPS output
- Port linear interpolation and quaternion SLERP smoothing from C# core
- Add gui_marker_compensation.h for RE Engine GUI world-anchor tracking
- Add REFramework utilities module (cameraunlock_reframework)
- Add velocity extrapolation to interpolators for smooth high-refresh output
- Gate UnityEngine.InputLegacyModule reference on file existence
- Fix batch paren-poisoning in install.cmd template
- Move game detection to data-driven games.json
- Fix install.cmd/uninstall.cmd templates for dev-tree use
- Unify installer CLI across BepInEx/MelonLoader/Cecil/ASI/REFramework/shim
- Make vendored loaders the install-time source of truth
- Add Step-SemanticVersion and Resolve-ReleaseVersion helpers
- Add camera discovery module (RTTI vtable + float classifier)
- Add AGENTS.md with shared code-quality and library API rules
- Expand submodule pointer commits in generated changelogs
- Fix /y flag detection and bundle vendored BepInEx in installers
- Use WriteAllBytes for .cmd output to avoid Defender race
- Add DX11 overlay header for crosshair rendering
- Adopt unified launcher contract and add yaw/cycle-mode hotkeys

## [1.0.3] - 2026-02-26

### Changed
- Updated smoothing note to mention filtered signal for direct phone tracking.

## [1.0.1] - 2026-02-26

### Fixed
- Fixed package install to generate a real install.ps1 with game detection.

## [1.0.0] - 2026-02-24

### Added
- Initial release of Shadows of Doubt Head Tracking mod.
- Head tracking support via OpenTrack UDP protocol.
- Switched from manual DLL references to NuGet packages.
