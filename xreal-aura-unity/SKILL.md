---
name: xreal-aura-unity
description: Build, deploy and debug Unity apps for the XREAL Aura (Android XR, OpenXR, optical see-through glasses with the "Android XR Puck"). Use whenever the user is making or fixing anything for the XREAL Aura or Android XR in Unity: project setup, OpenXR features, hand tracking/pinch, planes, depth, occlusion, light estimation, passthrough/see-through or dimming, colours looking dark or transparent, adb over wireless to the Aura, installing APKs, logcat, pulling screen recordings, or driving the Unity Editor from the terminal. Contains field-tested device behaviour that differs from what the docs imply.
---

# XREAL Aura × Unity (Android XR)

Field notes from building a shipped-to-device Unity app on the XREAL Aura (Nov 2026, Unity
6000.6.9f1, Android XR OpenXR 1.5.1, Aura firmware of that month). Where the docs and the
device disagree, these notes follow the device. Things never tested are listed as such in
the reference files, so you can tell "known" from "unknown".

## What the Aura is (and why it changes how you build)

- **Optical see-through, additive display.** The lenses can only *add* light. Black renders as
  fully transparent, dark colours look see-through, nothing can be darker than the room.
  Design every visual around this: no black outlines, no dark materials, bright and saturated
  wins. Hiding something = drawing it black.
- **Android XR + OpenXR.** Unity talks to it through `com.unity.xr.androidxr-openxr` and AR
  Foundation. Device reports as model "Android XR Puck", Android 14 / API 34, arm64, Vulkan,
  Adreno, 72 Hz.
- **OpenXR defaults to OPAQUE blend mode, which makes the Aura treat the app as VR** (lenses
  dim, auto-transparency off: "everything is black"). Every project needs the
  `SeeThroughFeature` in `templates/` to force ADDITIVE.
- **Develop over wireless adb.** The puck's USB ports did not give adb on a Mac.

## Non-negotiable rules

1. **Enable the OpenXR loader for Android by hand.** Switching to the Android XR build profile
   did *not* do it. Symptom: the app runs as a flat 2D window. Project Settings → XR Plug-in
   Management → Android tab → tick OpenXR + Android XR feature group.
2. **Ship `SeeThroughFeature`** (enabled in the OpenXR feature list) and request dimming 0 via
   `RealWorldLighting` / `ARCameraFeature.TryRequestGlobalDimmingLevel(0f)` with retries.
3. **Declare and request permissions, and gate AR managers on them.** `HAND_TRACKING`,
   `SCENE_UNDERSTANDING_COARSE` (planes, light estimation), `SCENE_UNDERSTANDING_FINE`
   (depth, occlusion, depth raycasts). Keep those managers disabled in the scene and enable
   them after the grant (`AuraPermissions` does this).
4. **Don't rely on plane boundaries.** On the tested unit planes arrived without outlines.
   Use depth raycasts (`TrackableType.Depth`) for surfaces (`DepthSurfaces`).
5. **Never create primitives at runtime.** Engine code stripping removes `SphereCollider` etc.
   (`Can't add component because class 'SphereCollider' doesn't exist!`). Grab built-in meshes
   in the Editor and serialize them.
6. **Keep the Unity project out of iCloud-synced folders** (Desktop/Documents on many Macs).
   iCloud's " 2" conflict copies corrupt Library and break builds. Name the folder `*.nosync`
   or put it under `~/Developer`.
7. **Unity only recompiles/reimports while it's the frontmost app** and the screen is
   unlocked. After editing files from a terminal, `open -a Unity` (activate Finder first if
   Unity is already front) and confirm in `<Project>/Logs/Editor.log`.

## Starting a new project

1. Unity 6.5+ (6000.6 tested) with Android Build Support + OpenJDK + SDK & NDK. Universal 3D
   template. Create it outside iCloud.
2. Copy `templates/` into the project as a local package (e.g. `Packages/com.yourcompany.auraxr/`
   or a sibling folder referenced by `"file:../..."` in `manifest.json`). Rename the package,
   namespace, app id and the `SeeThroughFeature` FeatureId. Its `package.json` pulls the
   tested package versions.
3. File → Build Profiles → Android XR → Switch Profile, then tick OpenXR for Android (rule 1).
4. Menu **Aura → 1. Set Up Project** (player settings, OpenXR features, URP), then Project
   Validation → Fix All (leftover yellow warnings are fine).
5. Menu **Aura → 2. Add XR Rig** in your scene: XR Origin with a clear-black camera, AR
   session, light estimation, permission-gated planes/depth/occlusion, two `HandPinch`es and an
   Editor `MousePinch`.
6. Pair wireless adb once, then build: `scripts/aura.sh install build/<app>.apk`.

Read `references/setup.md` for the exact settings, feature list and template contents.

## Reference map (read the one you need)

| File | Read when |
|---|---|
| `references/setup.md` | creating/configuring a project, packages, OpenXR features, player settings, manifest, rig |
| `references/display-and-rendering.md` | anything visual: colours, brightness, blend mode, dimming, shaders, stereo, occlusion, lighting, placement in FOV |
| `references/tracking.md` | hands, pinch, gestures, planes, depth, object/image tracking, camera, what does and doesn't work |
| `references/device-and-deploy.md` | adb pairing/connecting, install, launch, logcat, permissions over adb, screen recordings |
| `references/editor-workflow.md` | driving Unity from a terminal, trigger files, compile checks, Editor testing without glasses, offline video rendering with alpha |
| `references/troubleshooting.md` | an error message or symptom: look it up before debugging from scratch |

## Bundled files

- `templates/` — drop-in Unity package (namespace `AuraXR`):
  `SeeThroughFeature`, `AuraPermissions`, `HandPinch`/`MousePinch`/`PinchSource`,
  `DepthSurfaces`, `RealWorldLighting`, `Shaders/AuraOcclusion.hlsl`; Editor:
  `AuraProjectSetup` (menus), `HandTrackingPermission` (manifest), `EditorTriggers`
  (terminal → Editor: build/play/stop/refresh), `ICloudDuplicateCleaner`.
- `scripts/aura.sh` — `status | connect | install <apk> [pkg] | launch <pkg> | log [tag] |
  recordings | pull [dir] | clear-recordings`. Looks the device up fresh on every call (its
  adb name changes on reconnect). Needs the sandbox disabled in Claude Code.

## Working style that paid off

- Add a periodic status line to the app log (every 5–10 s, with a unique prefix) — logcat is
  the only window into what the device sees.
- `adb exec-out screencap` captures the passthrough camera, not your render; ask the user
  what they see, or have them screen-record from Quick Settings and pull the file.
- For marketing/video footage, on-device screen recordings look poor. Render the content
  offline in the Editor with alpha and composite it over real footage
  (`references/editor-workflow.md`).
- Confirm before deleting anything on the device; pull, verify sizes, then remove.
