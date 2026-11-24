using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.NativeTypes;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace AuraXR
{
    /// <summary>
    /// Keeps the app out of OPAQUE blend mode. OpenXR otherwise picks the runtime's first mode,
    /// OPAQUE, and the Aura treats the app as VR: it turns off auto-transparency and dims the
    /// lenses. ADDITIVE and ALPHA_BLEND (which AR Foundation's camera uses for light estimation)
    /// both keep the real world visible, so only OPAQUE is corrected.
    /// </summary>
#if UNITY_EDITOR
    [OpenXRFeature(UiName = "Aura: See-Through (Additive)",
        BuildTargetGroups = new[] { BuildTargetGroup.Android },
        Company = "Your Company",
        Desc = "Selects the ADDITIVE environment blend mode so the real world stays undimmed.",
        Version = "0.1.0",
        Category = FeatureCategory.Feature,
        FeatureId = FeatureId)]
#endif
    public class SeeThroughFeature : OpenXRFeature
    {
        public const string FeatureId = "com.yourcompany.aura.seethrough";

        protected override void OnSessionBegin(ulong xrSession) =>
            SetEnvironmentBlendMode(XrEnvironmentBlendMode.Additive);

        // Android XR's camera feature asks for OPAQUE until its camera starts, then ALPHA_BLEND.
        // The guard stops the two features re-triggering each other within one callback.
        static bool s_Correcting;

        protected override void OnEnvironmentBlendModeChange(XrEnvironmentBlendMode mode)
        {
            if (mode != XrEnvironmentBlendMode.Opaque || s_Correcting)
                return;
            s_Correcting = true;
            SetEnvironmentBlendMode(XrEnvironmentBlendMode.Additive);
            s_Correcting = false;
        }
    }
}
