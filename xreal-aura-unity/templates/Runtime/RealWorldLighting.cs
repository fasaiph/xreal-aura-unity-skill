using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.OpenXR.Features.Android;

namespace AuraXR
{
    /// <summary>
    /// Lights virtual content with the room's real light, from Android XR light estimation:
    /// the main light's direction, colour and brightness drive the scene's directional light,
    /// and the room's ambient light (spherical harmonics) replaces Unity's ambient. Also asks
    /// the Aura for zero lens dimming.
    /// </summary>
    public class RealWorldLighting : MonoBehaviour
    {
        public ARCameraManager cameraManager;
        public Light mainLight;

        [Tooltip("Overall brightness of virtual content relative to the estimate. Below 1 lets content sit back into the room instead of glowing over it.")]
        [Range(0f, 2f)] public float exposure = 0.65f;

        [Tooltip("How quickly lighting follows changes in the room (per second). Low values avoid flicker.")]
        public float smoothing = 3f;

        float m_Intensity = 1f;
        Color m_Color = Color.white;
        Quaternion m_Rotation;
        bool m_HasEstimate;

        void OnEnable()
        {
            if (cameraManager != null)
                cameraManager.frameReceived += OnFrame;
        }

        void OnDisable()
        {
            if (cameraManager != null)
                cameraManager.frameReceived -= OnFrame;
        }

        IEnumerator Start()
        {
            if (mainLight != null)
                m_Rotation = mainLight.transform.rotation;

            // The XR session takes a moment to come up, so retry for a few seconds.
            for (int i = 0; i < 10; i++)
            {
                if (!ARCameraFeature.TryRequestGlobalDimmingLevel(0f).IsError())
                    yield break;
                yield return new WaitForSeconds(0.5f);
            }
        }

        void OnApplicationFocus(bool focused)
        {
            // The system can change dimming while the app is in the background.
            if (focused)
                ARCameraFeature.TryRequestGlobalDimmingLevel(0f);
        }

        void OnFrame(ARCameraFrameEventArgs args)
        {
            var estimate = args.lightEstimation;

            if (estimate.mainLightDirection.HasValue)
                m_Rotation = Quaternion.LookRotation(estimate.mainLightDirection.Value);
            if (estimate.mainLightColor.HasValue)
                m_Color = estimate.mainLightColor.Value;
            if (estimate.averageMainLightBrightness.HasValue)
                m_Intensity = estimate.averageMainLightBrightness.Value;
            else if (estimate.averageBrightness.HasValue)
                m_Intensity = estimate.averageBrightness.Value;

            if (estimate.ambientSphericalHarmonics.HasValue)
            {
                var ambient = estimate.ambientSphericalHarmonics.Value;
                ambient *= exposure;
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientProbe = ambient;
            }

            m_HasEstimate = true;
        }

        void Update()
        {
            if (!m_HasEstimate || mainLight == null)
                return;

            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            var transform = mainLight.transform;
            transform.rotation = Quaternion.Slerp(transform.rotation, m_Rotation, t);
            mainLight.color = Color.Lerp(mainLight.color, m_Color, t);
            mainLight.intensity = Mathf.Lerp(mainLight.intensity, m_Intensity * exposure, t);
        }
    }
}
