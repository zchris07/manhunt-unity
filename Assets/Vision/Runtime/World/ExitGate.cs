using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// The building's exit gate: a roll-up door in the north wall with a lever beside it. Closed, it blocks movement and
    /// sight; <see cref="Open"/> rolls it up into its housing and throws the lever.
    /// </summary>
    public sealed class ExitGate : MonoBehaviour
    {
        public Transform door;
        public Transform leverHandle;
        public Collider blocker;
        public Vision.Visibility.Occluder occluder;
        public float height = 2.4f;
        public float rollSpeed = 0.8f;

        public bool IsOpen { get; private set; }
        float raised;

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            if (blocker != null) blocker.enabled = false;
            if (occluder != null) occluder.Blocking = false;
        }

        void Update()
        {
            if (!IsOpen || raised >= 1f) return;
            raised = Mathf.MoveTowards(raised, 1f, rollSpeed * Time.deltaTime / Mathf.Max(0.1f, height) * 2f);
            if (door != null)
            {
                door.localScale = new Vector3(1f, Mathf.Max(0.04f, 1f - raised), 1f);
                door.localPosition = new Vector3(door.localPosition.x, height * raised, door.localPosition.z);
            }
            if (leverHandle != null) leverHandle.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 120f, Mathf.Min(1f, raised * 4f)), 0f, 0f);
        }
    }
}
