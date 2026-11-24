# Display and rendering

## The additive display

The Aura's waveguides add light to the real world; they cannot block it.

- Black (0,0,0) is invisible. Dark colours are partly see-through, and each step darker is more
  transparent. Deep burgundy, navy, black ink lines: all disappear or look ghostly.
- "Solid" and "lit like the room" pull against each other: content only reads as opaque when it
  is much brighter than what's behind it. Brightness matched to a dim room looks translucent.
  True solidity would need per-pixel dimming; the Aura only dims the whole lens.
- Even at dimming 0, the lens passes less light than the naked eye. Sunlight washes content out.
- Subtle shading mostly vanishes. Toon/painted shading needed much stronger contrast and edge
  widths before it showed.

### Colour guidance that worked

- Outline/ink with a *deeper shade of the object's own colour* (e.g. base × 0.5–0.7), never black
  or sepia. Sepia/brown ink brightened up turned thin parts (stems) yellow.
- Keep materials matte (smoothness ~0.3) and emission very low (~0.04). High emission makes
  everything look neon.
- Fully saturated colours at full brightness glow like neon in a dim room. What sat well:
  - dim, lamp-lit room: overall brightness multiplier ~0.6 and light-estimate exposure 0.65.
  - bright room: brightness ~0.93 plus a mid-tone lift `color = pow(color, 1/1.25)` and higher
    saturation.
  Expose these as tunables; the right value depends on the room.
- Default lighting before the first estimate arrives: warm ~2700K — directional light
  (1, 0.84, 0.66) at 0.7, flat ambient (0.34, 0.30, 0.26).
- Material colours in shaders are linear (project is Linear colour space).

## Blend mode (the "everything is black" bug)

The runtime offers `OPAQUE`, `ADDITIVE`, `ALPHA_BLEND`; OpenXR picks the first, OPAQUE. The log
shows `XR_ENVIRONMENT_BLEND_MODE_OPAQUE (Selected)`. In OPAQUE the Aura treats the app as VR: it
turns off auto-transparency and dims the lenses.

`templates/Runtime/SeeThroughFeature.cs` fixes it:
- `OnSessionBegin` → `SetEnvironmentBlendMode(XrEnvironmentBlendMode.Additive)`.
- `OnEnvironmentBlendModeChange` → re-set Additive **only if the new mode is Opaque**, with a
  static re-entrancy guard. Android XR's `ARCameraFeature` asks for OPAQUE until its camera
  starts and then ALPHA_BLEND; ALPHA_BLEND is also see-through, so leave it. Without the guard
  the two features ping-pong.
- The `[OpenXRFeature(...)]` attribute goes inside `#if UNITY_EDITOR`; enable the feature in the
  Android OpenXR feature list.

## Lens dimming

`ARCameraFeature.TryRequestGlobalDimmingLevel(0f)` (`UnityEngine.XR.OpenXR.Features.Android`).
The session isn't ready at `Start`, so retry ~10× every 0.5 s until `!IsError()`, and again in
`OnApplicationFocus(true)` (the system may change it while backgrounded). Device log confirms
with `FramesHostService: setGlobalDimmingLevel: 0`. The warning
`Cannot find extension ID for ANDROID_global_passthrough_dimming` appears but dimming still works.
Also available: `TryGetSupportedDimmingLevels`, `TryGetGlobalDimmingLevel`.

## Real-world light estimation

Needs `ARSessionFeature`, `ARCameraFeature` and `SCENE_UNDERSTANDING_COARSE`. Set
`ARCameraManager.requestedLightEstimation = MainLightDirection | MainLightIntensity |
AmbientSphericalHarmonics | AmbientIntensity | AmbientColor`; on `frameReceived` drive the
directional light and set `RenderSettings.ambientMode = Skybox; ambientProbe = sh * exposure`,
smoothing over time. See `templates/Runtime/RealWorldLighting.cs`. Extensions present:
`XR_ANDROID_light_estimation`, `XR_ANDROID_light_estimation_cubemap`.

## Custom shaders (URP, single-pass instanced)

Stereo rendering is single-pass instanced/multiview, so every custom HLSL shader needs:
```hlsl
#pragma multi_compile_instancing
// in Attributes:  UNITY_VERTEX_INPUT_INSTANCE_ID
// in Varyings:    UNITY_VERTEX_OUTPUT_STEREO
// vertex:  UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
// fragment: UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
```
Include package shader files by `Packages/<package-name>/Runtime/Shaders/X.hlsl`. Android/Vulkan
variants compile only at build time, so check the build log for shader errors too.

## Depth occlusion (real objects hiding virtual ones)

- `AROcclusionManager.requestedEnvironmentDepthMode = Medium`,
  `environmentDepthTemporalSmoothingRequested = false`, plus `ARShaderOcclusion` with
  `occlusionShaderMode = None` so your shader does its own soft test using the globals it sets
  (`_EnvironmentDepthTexture` Texture2DArray, `_EnvironmentDepthProjectionMatrices[2]`,
  `_NdcLinearConversionParameters`, `_IsOcclusionOn`).
- `templates/Runtime/Shaders/AuraOcclusion.hlsl`: `RealWorldVisibility(positionWS)` returns
  0..1; multiply the final colour by it (black = see-through = hidden). Needs
  `#pragma multi_compile _ XR_LINEAR_DEPTH` and `#pragma multi_compile _ XR_HARD_OCCLUSION`;
  samples with `unity_StereoEyeIndex`. Soft tolerance 2 cm near, widening to 50 cm beyond 3.5 m.
  Math follows AR Foundation samples' `OcclusionComputation.hlsl`.
- Depth runs ~60 FPS (`XR_ANDROID_depth_texture`). Clear glass is nearly invisible to depth.

## Placement in the field of view

The FOV is narrow (~70° diagonal class). Content placed "around the room" was out of view.
What worked for tabletop content: ~45–60 cm in front, 10–40 cm below eye level, spread within
~40°.

## Performance notes

- Not measured precisely. Display default 72 Hz; hand tracking 30 Hz; depth 60 Hz.
- Merging many small objects into a few combined meshes (grouped by material) ran smoothly.
- Animate many objects in the vertex shader (e.g. a small global array via
  `Shader.SetGlobalVectorArray`) instead of moving transforms.

## Audio

`AudioSource.PlayClipAtPoint` works; `AudioSettings.dspTime` + `PlayScheduled` gives gap-free
music. The system attaches spatial audio requests to tracks.
