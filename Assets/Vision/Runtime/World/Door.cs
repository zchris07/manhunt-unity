using UnityEngine;
using Vision.Visibility;

namespace Vision.World
{
    /// <summary>
    /// A door or window shutter. Closed, it is an occluder (and a door blocks movement). Toggling it
    /// switches its occluder, which bumps the occluder version so cached polygons are rebuilt.
    /// The panel swings on a hinge child for the visual.
    /// </summary>
    public sealed class Door : MonoBehaviour
    {
        public Occluder occluder;
        public Transform hinge;
        public Collider blocker;
        [Tooltip("Shutters let you see through when open but you still can't walk through the window.")]
        public bool blocksMovementWhenOpen;
        public float openAngle = 100f;
        public float swingSpeed = 360f;
        /// <summary>The closed panel's ends (design units on the level's ground plane), for the match rules.</summary>
        public Vector2 a, b;

        bool open;
        float angle;

        public bool IsOpen => open;

        public void Toggle() => SetOpen(!open);

        /// <summary>Smashed in (Zach's two swipes): the panel is gone and the doorway is open for good.</summary>
        public bool IsBroken { get; private set; }

        public void SetBroken(bool broken)
        {
            if (IsBroken == broken) return;
            IsBroken = broken;
            if (hinge != null) hinge.gameObject.SetActive(!broken);
            if (broken)
            {
                if (occluder != null) occluder.Blocking = false;
                if (blocker != null) blocker.enabled = false;
            }
            else SetOpen(open);
        }

        public void SetOpen(bool value)
        {
            open = value;
            if (occluder != null) occluder.Blocking = !open;
            if (blocker != null) blocker.enabled = !open || blocksMovementWhenOpen;
        }

        void Update()
        {
            float target = open ? openAngle : 0f;
            angle = Mathf.MoveTowards(angle, target, swingSpeed * Time.deltaTime);
            if (hinge != null) hinge.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
