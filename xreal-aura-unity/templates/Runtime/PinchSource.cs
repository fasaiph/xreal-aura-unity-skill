using UnityEngine;

namespace AuraXR
{
    /// <summary>
    /// Anything that can pinch: a tracked hand on the glasses, or the mouse in the Editor.
    /// Runs before grabbers so they always read this frame's pinch.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public abstract class PinchSource : MonoBehaviour
    {
        public bool IsTracked { get; private set; }
        public bool IsPinching { get; private set; }
        public bool PinchStarted { get; private set; }
        public Vector3 PinchPoint { get; private set; }

        protected abstract void Tick();

        void Update() => Tick();

        protected void Report(bool tracked, bool pinching, Vector3 point)
        {
            pinching &= tracked;
            PinchStarted = pinching && !IsPinching;
            IsTracked = tracked;
            IsPinching = pinching;
            if (tracked)
                PinchPoint = point;
        }
    }
}
