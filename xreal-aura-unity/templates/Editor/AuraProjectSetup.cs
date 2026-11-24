using System.Linq;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.OpenXR;

namespace AuraXR.Editor
{
    /// <summary>
    /// "Aura/1. Set Up Project": player settings, OpenXR features and URP settings for the XREAL
    /// Aura (Android XR). "Aura/2. Add XR Rig": an XR Origin with a see-through camera, AR session,
    /// light estimation, and permission-gated planes, depth ray casts and occlusion.
    /// Needs URP; the asmdef must reference Unity.RenderPipelines.Universal.Runtime.
    /// </summary>
    static class AuraProjectSetup
    {
        // Change these per project.
        const string k_ProductName = "My Aura App";
        const string k_Company = "Your Company";
        const string k_AppId = "com.yourcompany.myauraapp";

        // OpenXR features, by type name so this compiles across package versions.
        static readonly string[] k_Features =
        {
            "HandTracking", "MetaHandTrackingAim", "HandInteractionProfile", // XR Hands joints + pinch/aim
            "ARSessionFeature", "ARCameraFeature",  // AR session, light estimation, lens dimming
            "ARPlaneFeature", "ARRaycastFeature",   // planes; depth ray casts
            "AROcclusionFeature",                   // environment depth
            "SeeThroughFeature",                    // keeps the app out of OPAQUE (VR) blend mode
        };

        [MenuItem("Aura/1. Set Up Project", priority = 1)]
        static void SetUpProject()
        {
            var report = new StringBuilder();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            var android = NamedBuildTarget.Android;
            PlayerSettings.productName = k_ProductName;
            PlayerSettings.companyName = k_Company;
            PlayerSettings.SetApplicationIdentifier(android, k_AppId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            if ((int)PlayerSettings.Android.minSdkVersion < 24)
                PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)24;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            PlayerSettings.Android.resizeableActivity = true;
            report.AppendLine("✓ Player: IL2CPP, ARM64, Vulkan, linear, GameActivity, resizable, min API 24+");

            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            bool openXR = general != null && general.Manager != null && general.Manager.activeLoaders.Any(l => l is OpenXRLoader);
            if (!openXR)
            {
                report.AppendLine("✗ OpenXR isn't on for Android. File → Build Profiles → Android XR → Switch Profile " +
                    "(or XR Plug-in Management → Android → tick OpenXR + Android XR feature group), then run this again.");
            }
            else
            {
                var features = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeatures();
                foreach (var name in k_Features)
                {
                    var feature = features.FirstOrDefault(f => f.GetType().Name == name);
                    if (feature == null)
                    {
                        report.AppendLine($"✗ OpenXR feature {name} not found");
                        continue;
                    }
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                    report.AppendLine($"✓ OpenXR feature: {name}");
                }
            }

            // HDR and post-processing cost a lot on the puck and do nothing for an additive display.
            var pipelines = Enumerable.Range(0, QualitySettings.count)
                .Select(QualitySettings.GetRenderPipelineAssetAt)
                .Append(GraphicsSettings.defaultRenderPipeline)
                .OfType<UniversalRenderPipelineAsset>()
                .Distinct();
            foreach (var pipeline in pipelines)
            {
                pipeline.supportsHDR = false;
                EditorUtility.SetDirty(pipeline);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (renderer == null || renderer.postProcessData == null)
                    continue;
                renderer.postProcessData = null;
                EditorUtility.SetDirty(renderer);
            }
            report.AppendLine("✓ URP: HDR off, post-processing off");

            AssetDatabase.SaveAssets();
            Debug.Log("[Aura] Setup\n" + report);
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Project Validation");
        }

        [MenuItem("Aura/2. Add XR Rig", priority = 2)]
        static void AddRig()
        {
            // Black is see-through on the Aura: no skybox, transparent clear.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.30f, 0.26f); // warm room until the estimate arrives

            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.84f, 0.66f);
            light.intensity = 0.7f;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);

            var origin = new GameObject("XR Origin").AddComponent<XROrigin>();
            var cameraOffset = new GameObject("Camera Offset").transform;
            cameraOffset.SetParent(origin.transform, false);
            // World space = tracking space: AR Foundation's depth matrices are in tracking space.
            cameraOffset.localPosition = Vector3.zero;

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(cameraOffset, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.nearClipPlane = 0.05f;
            cameraObject.AddComponent<AudioListener>();

            var pose = cameraObject.AddComponent<TrackedPoseDriver>();
            pose.positionInput = new InputActionProperty(new InputAction("Head Position", InputActionType.Value,
                "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            pose.rotationInput = new InputActionProperty(new InputAction("Head Rotation", InputActionType.Value,
                "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));

            origin.Camera = camera;
            origin.CameraFloorOffsetObject = cameraOffset.gameObject;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = 0f;

            new GameObject("AR Session").AddComponent<ARSession>();
            var cameraManager = cameraObject.AddComponent<ARCameraManager>();
            cameraManager.requestedLightEstimation = LightEstimation.MainLightDirection | LightEstimation.MainLightIntensity |
                LightEstimation.AmbientSphericalHarmonics | LightEstimation.AmbientIntensity | LightEstimation.AmbientColor;
            var lighting = light.gameObject.AddComponent<RealWorldLighting>();
            lighting.cameraManager = cameraManager;
            lighting.mainLight = light;

            // Everything below needs scene-understanding permission; AuraPermissions enables it once granted.
            var planes = origin.gameObject.AddComponent<ARPlaneManager>();
            planes.requestedDetectionMode = UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Horizontal;
            planes.enabled = false;
            var raycasts = origin.gameObject.AddComponent<ARRaycastManager>();
            raycasts.enabled = false;
            origin.gameObject.AddComponent<DepthSurfaces>().raycastManager = raycasts;
            var occlusion = cameraObject.AddComponent<AROcclusionManager>();
            occlusion.requestedEnvironmentDepthMode = UnityEngine.XR.ARSubsystems.EnvironmentDepthMode.Medium;
            occlusion.enabled = false;

            var permissions = new GameObject("Permissions").AddComponent<AuraPermissions>();
            permissions.needsSceneCoarse.Add(planes);
            permissions.needsSceneFine.Add(raycasts);
            permissions.needsSceneFine.Add(occlusion);

            foreach (var handedness in new[] { UnityEngine.XR.Hands.Handedness.Left, UnityEngine.XR.Hands.Handedness.Right })
            {
                var hand = new GameObject($"{handedness} Hand").AddComponent<HandPinch>();
                hand.handedness = handedness;
                hand.trackingSpace = cameraOffset;
            }
            new GameObject("Editor Mouse").AddComponent<MousePinch>().viewer = camera;

            EditorSceneManager.MarkSceneDirty(origin.gameObject.scene);
            Debug.Log("[Aura] Added the XR rig. Save the scene and add it to the build profile.");
        }
    }
}
