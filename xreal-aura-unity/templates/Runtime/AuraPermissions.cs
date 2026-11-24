using System.Collections.Generic;
using UnityEngine;

namespace AuraXR
{
    /// <summary>
    /// Asks for the Android XR permissions the app needs, and switches on the AR Foundation
    /// components that depend on them only once granted. Android XR logs errors if plane
    /// detection or depth start without permission.
    /// </summary>
    public class AuraPermissions : MonoBehaviour
    {
        const string k_HandTracking = "android.permission.HAND_TRACKING";
        const string k_SceneCoarse = "android.permission.SCENE_UNDERSTANDING_COARSE"; // planes, light estimation
        const string k_SceneFine = "android.permission.SCENE_UNDERSTANDING_FINE";     // depth occlusion

        [Tooltip("Enabled once coarse scene understanding is granted, e.g. the AR Plane Manager.")]
        public List<Behaviour> needsSceneCoarse = new();
        [Tooltip("Enabled once fine scene understanding is granted, e.g. the AR Occlusion Manager and AR Shader Occlusion.")]
        public List<Behaviour> needsSceneFine = new();

#if UNITY_ANDROID && !UNITY_EDITOR
        bool m_CoarseOn, m_FineOn;
#endif

        void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var missing = new List<string>();
            foreach (var permission in new[] { k_HandTracking, k_SceneCoarse, k_SceneFine })
            {
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission))
                    missing.Add(permission);
            }
            if (missing.Count > 0)
                UnityEngine.Android.Permission.RequestUserPermissions(missing.ToArray());
#else
            // No depth data in the Editor: leave occlusion off, or all virtual content would read as hidden.
            Enable(needsSceneCoarse);
#endif
        }

        void Update()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!m_CoarseOn && UnityEngine.Android.Permission.HasUserAuthorizedPermission(k_SceneCoarse))
            {
                Enable(needsSceneCoarse);
                m_CoarseOn = true;
            }
            if (!m_FineOn && UnityEngine.Android.Permission.HasUserAuthorizedPermission(k_SceneFine))
            {
                Enable(needsSceneFine);
                m_FineOn = true;
            }
#endif
        }

        static void Enable(List<Behaviour> behaviours)
        {
            foreach (var behaviour in behaviours)
            {
                if (behaviour != null)
                    behaviour.enabled = true;
            }
        }
    }
}
