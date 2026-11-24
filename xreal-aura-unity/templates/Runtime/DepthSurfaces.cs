using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace AuraXR
{
    /// <summary>
    /// Finds real surfaces from the depth sensor, by ray casting against depth. Used when plane
    /// detection can't help: on the test Aura, the system plane service reports planes but fails
    /// to provide their outlines, so planes have no shape to stand on or measure.
    /// </summary>
    public class DepthSurfaces : MonoBehaviour
    {
        public static DepthSurfaces Instance { get; private set; }

        public ARRaycastManager raycastManager;

        static readonly List<ARRaycastHit> s_Hits = new();

        void Awake() => Instance = this;

        public bool Ready => raycastManager != null && raycastManager.enabled
            && raycastManager.subsystem != null && raycastManager.subsystem.running;

        /// <summary>The first real surface along <paramref name="ray"/>, within <paramref name="maxDistance"/>.</summary>
        public bool TryRaycast(Ray ray, float maxDistance, out Vector3 point)
        {
            point = default;
            if (!Ready || !raycastManager.Raycast(ray, s_Hits, TrackableType.Depth))
                return false;
            foreach (var hit in s_Hits)
            {
                if (hit.distance <= maxDistance)
                {
                    point = hit.pose.position;
                    return true;
                }
            }
            return false;
        }

        /// <summary>The height of the real surface straight below <paramref name="from"/>.</summary>
        public bool TryFindSurfaceBelow(Vector3 from, float maxDrop, out float y)
        {
            y = 0f;
            if (!TryRaycast(new Ray(from, Vector3.down), maxDrop, out var point))
                return false;
            y = point.y;
            return true;
        }
    }
}
