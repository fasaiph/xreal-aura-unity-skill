# Tracking: what works on the Aura

| Capability | Status on the tested unit | Use |
|---|---|---|
| Hand joints (XR Hands) | ✅ works, ~30 Hz | pinch, fingertip touch |
| Pinch | ✅ most reliable gesture | primary input |
| Whole-hand grip/wrap detection | ❌ unreliable, dropped | don't build on it |
| Environment depth | ✅ ~60 Hz, ~1 cm jitter | surfaces, occlusion |
| Depth raycasts | ✅ | finding surfaces |
| Plane detection | ⚠️ planes arrive with **no boundaries** | don't rely on outlines |
| Light estimation | ✅ | real-world lighting |
| Object tracking | ⚠️ only keyboard, mouse, laptop | no custom objects |
| Image tracking | ⚠️ via Google's extension: QR/ArUco/AprilTag only | markers only |
| Camera frames to app | ❓ untested | |
| Anchors / persistence | ❓ untested | |

## Hands and pinch

- Find a running `XRHandSubsystem` via `SubsystemManager.GetSubsystems` (re-check each frame
  until one is running).
- Pinch = distance between `ThumbTip` and `IndexTip`, with hysteresis: start < 2 cm, end > 4 cm
  so a shaky pinch doesn't flicker. Pinch point = midpoint. `templates/Runtime/HandPinch.cs`.
- Joint poses are in tracking space; transform with the XR Origin's Camera Offset.
- While holding something, allow ~0.4 s of tracking dropout before treating it as a release.
- Fingertip contact (`IndexTip`, `MiddleTip`) works for "touch/brush" interactions.
- Harmless log: `xrSetHandTrackingFrequencyHintMETA failed with XR_ERR...` (from MetaHandTrackingAim).
- Abstract `PinchSource` (`[DefaultExecutionOrder(-100)]`) lets hand, mouse (`MousePinch`, for
  Editor Play mode) and scripted sources (for automated recordings) swap freely.

## Planes

On the tested Aura (also after a reboot) plane counts arrived but every boundary request failed:
```
W/operator(): Failed to get scene plane.
E/native gg_scene_planes_client.cc:445] Service responded error: "Failed to get scene boundary for plane 721": Status(-8, EX_SERVICE_SPECIFIC): '-3: -1'
[ScenePlanesFMQ] Queue is empty or read failed -> Using cached bundle
```
So planes had no shape or colliders. Possibly unit/firmware-specific; re-test on new firmware,
but design with a depth fallback. Planes also don't follow a moved surface; they slowly re-detect.
Detecting a raised object (a book on a table) as its own plane or from a depth height map didn't work.

## Depth raycasts (the reliable surface finder)

```csharp
raycastManager.Raycast(ray, hits, TrackableType.Depth); // world-space rays only
```
The Android XR raycast subsystem supports world rays only (no viewport/screen rays), with types
`PlaneWithinPolygon | Depth`. `templates/Runtime/DepthSurfaces.cs` wraps it (`TryRaycast`,
`TryFindSurfaceBelow`). Needs `ARRaycastFeature` + `SCENE_UNDERSTANDING_FINE`.

Depth jitters ~1 cm. Re-sampling a resting object's support twice a second made it bounce.
Sample once on landing and lock it; ignore changes < 1.5 cm; only drop when the surface falls
away by > 8 cm. Through clear glass, depth randomly returns the rim, bottom or table behind it.

## Objects, images, markers

- `XR_ANDROID_trackables_object` labels only keyboard, mouse and laptop. No scanning/teaching.
- Google's Android XR Extensions for Unity: image tracking means QR and ArUco/AprilTag markers.
  (If the product shouldn't show visible markers, plan manual placement instead: e.g. the user
  pinch-taps points on the real object to mark it.)
- Camera: `dumpsys media.camera` lists 7 devices; 0 and 1 are exposed as normal back cameras,
  2 and 3 held by a system process. Whether an app can read frames is untested.
