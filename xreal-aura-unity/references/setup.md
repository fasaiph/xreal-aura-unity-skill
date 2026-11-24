# Project setup

## Versions that worked together (Nov 2026)

| | Version |
|---|---|
| Unity | 6000.6.9f1 (6.5+ recommended for Aura) |
| Android module | OpenJDK 17.0.18, NDK r27c, build-tools 36, platforms 34/36/37; APK target SDK 36 |
| `com.unity.xr.androidxr-openxr` | 1.5.1 |
| `com.unity.xr.openxr` | 1.19.0 |
| `com.unity.xr.management` | 4.6.1 |
| `com.unity.xr.hands` | 1.10.0 |
| `com.unity.xr.arfoundation` | 6.7.1 |
| `com.unity.xr.core-utils` | 2.6.0 |
| `com.unity.inputsystem` | 1.20.1 |
| URP | 17.6.0 |

Put these as dependencies of a local package (`templates/package.json`) so a fresh project only
needs one manifest line, e.g. `"com.yourcompany.auraxr": "file:../../com.yourcompany.auraxr"`
(keeping code outside the Unity project folder makes it easy to version and reuse).

Runtime asmdef references: `Unity.XR.Hands`, `Unity.InputSystem`, `Unity.XR.OpenXR`,
`Unity.XR.ARFoundation`, `Unity.XR.ARSubsystems`, `Unity.XR.AndroidOpenXR`.
Editor asmdef adds `Unity.XR.CoreUtils`, `Unity.XR.Management(.Editor)`, `Unity.XR.OpenXR.Editor`,
`Unity.RenderPipelines.Core.Runtime`, `Unity.RenderPipelines.Universal.Runtime`.

**Unity 6.6 may auto-add AI packages** (`com.unity.ai.assistant`, `com.unity.ai.inference` →
`com.unity.dt.app-ui`) that fail to compile (`CanvasControlScheme could not be found`). Remove
them; see troubleshooting for the stale-build-plan cleanup that follows.

Installing the Android module: the Hub's old `--headless` CLI is gone; use the Hub GUI
(Installs → ⚙ → Add modules → Android Build Support + OpenJDK + Android SDK & NDK Tools). Hub
stages downloads in `~/Library/Application Support/UnityHub/downloads/`.

## Creating the project

1. Universal 3D template, in a folder iCloud doesn't sync (`~/Developer/...` or `Name.nosync`).
   If git must live next to it in iCloud, keep the git database outside:
   `git init --separate-git-dir ~/Developer/git/<repo>.git`.
2. File → Build Profiles → **Android XR** → Switch Profile. This creates
   `Assets/Settings/Build Profiles/Android XR.asset` and a quality level "Android XR (Build
   Profile)" using the package's own URP asset (HDR off, MSAA off, render scale 1).
3. **Check the loader.** After the profile switch `XRGeneralSettingsPerBuildTarget` was still
   empty and the app launched as a flat 2D window. Project Settings → XR Plug-in Management →
   Android tab → tick **OpenXR** and the **Android XR** feature group.
4. Run **Aura → 1. Set Up Project** (`AuraProjectSetup.cs`), then Project Validation → Fix All.
   Fix All can look stuck; leftover items are warnings or "Edit"-only and don't block builds.

## Player settings

IL2CPP · ARM64 only · Vulkan only (`SetUseDefaultGraphicsAPIs(false)`) · Linear colour ·
`AndroidApplicationEntry.GameActivity` (launch activity
`com.unity3d.player.UnityPlayerGameActivity`) · `resizeableActivity = true` (the old
`resizableWindow` is deprecated, CS0618) · minSdk ≥ 24 (Fix All raises it to 26) · target auto.
Engine code stripping on is fine as long as nothing calls `GameObject.CreatePrimitive` at runtime.

## URP

HDR off and post-processing off (`postProcessData = null` on renderers): they cost GPU and
do nothing useful on an additive display. MSAA stays off in the Android XR URP asset.

## OpenXR features (Android tab)

Enabled in the working project:

- Android XR Support, Android XR build profile feature, OpenXR lifecycle (auto)
- Hand Tracking Subsystem (`HandTracking`), Meta Hand Tracking Aim (`MetaHandTrackingAim`),
  Hand Interaction Profile (`HandInteractionProfile`)
- Android XR: Session (`ARSessionFeature`), Camera/Passthrough (`ARCameraFeature`, needed for
  light estimation and dimming), Planes (`ARPlaneFeature`), Occlusion (`AROcclusionFeature`),
  Raycasts (`ARRaycastFeature`)
- Foveated Rendering, Composition Layers (default on)
- Your `SeeThroughFeature`
- Render mode: single-pass instanced / multiview

Never tried: Anchors, Bounding Boxes, Faces, Meshing, Display Utilities, Hand Mesh Data,
Performance Metrics.

Enabling from code (matches by type name, so it compiles across package versions):
```csharp
var features = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeatures();
var f = features.FirstOrDefault(x => x.GetType().Name == "ARPlaneFeature");
f.enabled = true; EditorUtility.SetDirty(f);
```
Check the loader first: `XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android).Manager.activeLoaders.Any(l => l is OpenXRLoader)`.

## Permissions

| Permission | Needed for |
|---|---|
| `android.permission.HAND_TRACKING` | XR Hands joints |
| `android.permission.SCENE_UNDERSTANDING_COARSE` | planes, **light estimation** |
| `android.permission.SCENE_UNDERSTANDING_FINE` | environment depth, occlusion, depth raycasts |
| `org.khronos.openxr.permission.OPENXR(_SYSTEM)` | granted automatically |

- Declare them: `HandTrackingPermission.cs` (an `IPostGenerateGradleAndroidProject`) adds missing
  `<uses-permission>` entries to the generated manifest.
- Request at runtime and enable dependent components after the grant: `AuraPermissions.cs`.
  Leave `ARPlaneManager`, `AROcclusionManager`, `ARShaderOcclusion`, `ARRaycastManager`
  disabled in the scene. In the Editor, keep occlusion off (no depth → everything reads hidden).
- If the user dismissed the prompt: `adb shell pm grant <pkg> android.permission.HAND_TRACKING`,
  then force-stop and relaunch.

## The rig (`Aura → 2. Add XR Rig`)

- `XROrigin`, tracking origin **Device**, `CameraYOffset = 0`, Camera Offset at local 0.
  World space = tracking space, which matters because AR Foundation's depth reprojection
  matrices are in tracking space. (Side effect: in the Editor the camera sits at y = 0.)
- Camera: `SolidColor` clear, `Color.clear` background, near clip 0.05. No skybox, flat ambient.
- `TrackedPoseDriver` (Input System) on `<XRHMD>/centerEyePosition` / `centerEyeRotation`.
- `ARSession`; `ARCameraManager` on the camera with light estimation requested.
- Hand joints come in tracking space: transform them through Camera Offset.
- Detected-plane prefab: `ARPlane + MeshFilter + MeshCollider + ARPlaneMeshVisualizer`, no
  renderer, so real surfaces are invisible colliders. Put interactables on "Ignore Raycast" so
  ground rays hit only real surfaces.

## Google's Android XR Extensions for Unity

Separate package; checked for image tracking and not installed. On Android XR it tracks only QR
codes and ArUco/AprilTag markers, not arbitrary images.
