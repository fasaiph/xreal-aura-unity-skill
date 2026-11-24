using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace AuraXR
{
    /// <summary>
    /// Editor stand-in for a hand, so the app can be tested in Play mode without the glasses.
    /// Left mouse = pinch, scroll while holding = push/pull, right-drag = look around.
    /// Switches itself off whenever an XR device is active.
    /// </summary>
    public class MousePinch : PinchSource
    {
        public Camera viewer;
        public float defaultDepth = 0.5f;
        public float lookSpeed = 0.15f;

        float m_Depth;
        float m_Yaw, m_Pitch;

        void Start()
        {
            m_Depth = defaultDepth;
            if (viewer != null)
            {
                var euler = viewer.transform.eulerAngles;
                m_Yaw = euler.y;
                m_Pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            }
        }

        protected override void Tick()
        {
            var mouse = Mouse.current;
            if (XRSettings.isDeviceActive || mouse == null || viewer == null)
            {
                Report(false, false, default);
                return;
            }

            if (mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                m_Yaw += delta.x * lookSpeed;
                m_Pitch = Mathf.Clamp(m_Pitch - delta.y * lookSpeed, -80f, 80f);
                viewer.transform.rotation = Quaternion.Euler(m_Pitch, m_Yaw, 0f);
            }

            var ray = viewer.ScreenPointToRay(mouse.position.ReadValue());
            bool pinching = mouse.leftButton.isPressed;
            if (pinching)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    m_Depth = Mathf.Clamp(m_Depth + Mathf.Sign(scroll) * 0.03f, 0.15f, 3f);
            }
            else
            {
                m_Depth = DepthUnderMouse(ray) ?? defaultDepth;
            }

            Report(true, pinching, ray.GetPoint(m_Depth));
        }

        // Put the cursor at the depth of whatever collider the mouse is over, like reaching out to it.
        static float? DepthUnderMouse(Ray ray) =>
            Physics.Raycast(ray, out var hit, 5f) ? hit.distance : null;
    }
}
