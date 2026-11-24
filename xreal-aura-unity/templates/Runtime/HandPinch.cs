using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace AuraXR
{
    /// <summary>
    /// Pinch from XR Hands joint data: the midpoint between thumb tip and index tip.
    /// </summary>
    public class HandPinch : PinchSource
    {
        public Handedness handedness = Handedness.Right;

        [Tooltip("The XR Origin's Camera Offset. Hand joints are reported in this space.")]
        public Transform trackingSpace;

        [Tooltip("Thumb-to-index gap (m) that starts a pinch.")]
        public float pinchStart = 0.02f;

        [Tooltip("Gap (m) that ends it. Larger than Pinch Start so a shaky pinch doesn't flicker.")]
        public float pinchEnd = 0.04f;

        static readonly List<XRHandSubsystem> s_Subsystems = new();
        XRHandSubsystem m_Subsystem;

        protected override void Tick()
        {
            if (m_Subsystem == null || !m_Subsystem.running)
            {
                m_Subsystem = null;
                SubsystemManager.GetSubsystems(s_Subsystems);
                foreach (var subsystem in s_Subsystems)
                {
                    if (subsystem.running)
                    {
                        m_Subsystem = subsystem;
                        break;
                    }
                }

                if (m_Subsystem == null)
                {
                    Report(false, false, default);
                    return;
                }
            }

            var hand = handedness == Handedness.Left ? m_Subsystem.leftHand : m_Subsystem.rightHand;
            if (!hand.isTracked ||
                !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb) ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index))
            {
                Report(false, false, default);
                return;
            }

            var thumbTip = ToWorld(thumb.position);
            var indexTip = ToWorld(index.position);
            float gap = Vector3.Distance(thumbTip, indexTip);
            bool pinching = IsPinching ? gap < pinchEnd : gap < pinchStart;
            Report(true, pinching, (thumbTip + indexTip) * 0.5f);
        }

        Vector3 ToWorld(Vector3 trackingPosition) =>
            trackingSpace != null ? trackingSpace.TransformPoint(trackingPosition) : trackingPosition;
    }
}
