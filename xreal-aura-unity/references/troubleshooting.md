# Troubleshooting: symptom → cause → fix

| Symptom / message | Cause | Fix |
|---|---|---|
| App runs as a flat 2D Android window | OpenXR loader not enabled for Android (profile switch didn't do it) | XR Plug-in Management → Android → tick OpenXR + Android XR group |
| Everything looks black / lenses dark / no see-through; log `XR_ENVIRONMENT_BLEND_MODE_OPAQUE (Selected)` | OpenXR chose OPAQUE → Aura runs the app as VR | `SeeThroughFeature` (Additive), enabled in OpenXR features |
| Content too dim, real world dark through lenses | Lens dimming not at 0 | `ARCameraFeature.TryRequestGlobalDimmingLevel(0f)` with retries + on focus |
| Dark objects/outlines invisible or ghostly | Additive display: black = transparent | Brighter materials; ink in a deeper shade of own colour |
| Thin parts look yellow after brightening | Sepia/brown ink lines brightened | Ink from the object's own colour |
| `Can't add component because class 'SphereCollider' doesn't exist!` | Engine code stripping + `CreatePrimitive` at runtime | Grab built-in meshes in the Editor, serialize them |
| Hands never track | `HAND_TRACKING` not granted / not declared | Manifest hook + runtime request; `adb shell pm grant <pkg> android.permission.HAND_TRACKING` |
| Plane detection errors at startup | Managers started before permission | Keep managers disabled; enable after grant (`AuraPermissions`) |
| Planes exist but no shape: `Failed to get scene boundary for plane N` | System plane service on this firmware | Use depth raycasts (`DepthSurfaces`) |
| Resting objects bounce | Depth jitter ~1 cm, re-sampled | Sample once and lock; thresholds 1.5 cm / 8 cm |
| Everything occluded in the Editor | No depth in Editor but occlusion on | Only enable occlusion on device |
| `xrSetHandTrackingFrequencyHintMETA failed` | MetaHandTrackingAim hint unsupported | Harmless |
| `Cannot find extension ID for ANDROID_global_passthrough_dimming` | Warning | Harmless; dimming still works |
| `IsSystemExtensionEnabled requires a valid System ID` (Editor) | XR Editor noise | Ignore |
| `UnityException: No Android devices connected` | adb not connected | Pair/connect wireless adb |
| Build "Failed" but APK exists, no install; `ObjectDisposedException: SerializedProperty m_PlatformBuildProfile.m_BuildAppBundle has disappeared!` | Build Profiles window bug | Close the window; ⌘B, or install with adb |
| `error CS0246: ... 'CanvasControlScheme' could not be found` in `com.unity.ai.inference` | Auto-added Unity AI packages | Remove `com.unity.ai.assistant` / `com.unity.ai.inference` |
| After that: `error CS0006: Metadata file 'Packages/com.unity.ai.inference/.../SourceGenerators.dll' could not be found` | Stale Bee build plan + leftover `com.unity.dt.app-ui` in `ProjectSettings/EditorBuildSettings.asset` `m_configObjects` | Quit Unity, delete `Library/Bee/1300b0aP*` and that entry, reopen (edits while open get overwritten) |
| `Could not find file '.../Library/PlayerDataCache/Android/Data/boot.config'` next to `Data 2` | iCloud conflict copies | Clean `* <n>` copies + `Library/PlayerDataCache/Android`, `Library/Bee/artifacts/Android`; move project out of iCloud |
| `Error building Player: 8444 errors`, `TypeDB: Class '...' was already registered` | iCloud duplicated DLLs in `Library/ScriptAssemblies` | Same as above, then restart Unity |
| `MissingReferenceException` on cached assets after leaving Play | Statics survive Play mode (no domain reload) | `TryGetValue(...) && cached != null` |
| CS0618 `resizableWindow` / `FindFirstObjectByType` / `FindObjectsByType(SortMode)` deprecated | Unity 6.6 API changes | `resizeableActivity`, `FindAnyObjectByType`, `FindObjectsByType<T>()` |
| Edits from terminal not picked up | Unity not focused / screen locked | `open -a Unity`; check DLL timestamps and Editor.log |
| adb retries fail after a drop: `no device with transport id` / `device offline` | Serial/transport id change on reconnect | Re-resolve the device each command (`aura.sh`) |
| `adb connect` refused, device visible in mdns | Aura asleep or Wireless debugging off; stale port | Wake it, re-check Wireless debugging, `adb mdns services` again |
| `adb screencap` shows the ceiling/room, not the app | It captures the passthrough camera | Use Quick Settings screen recording, or ask the user |
| Recordings look poor for video | On-device capture quality | Render offline in the Editor with alpha and composite |
