using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// The building's exit gate: a roll-up door in the north wall with a lever beside it. Closed, it blocks movement and
    /// sight. Once every generator runs, holding Interact at the lever for 20 seconds (the original's gate time) opens
    /// it: it rolls up into its housing.
    /// </summary>
    public sealed class ExitGate : MonoBehaviour
    {
        public Transform door;
        public Transform leverHandle;
        public Collider blocker;
        public Vision.Visibility.Occluder occluder;
        public float height = 2.4f;
        public float rollSpeed = 0.8f;
        /// <summary>The lever box (where the survivor stands to pull it).</summary>
        public Transform lever;

        /// <summary>Seconds of pulling the lever to open the gate, as the original.</summary>
        public const float OpenTime = 20f;

        /// <summary>0 to 1 while the lever is being pulled.</summary>
        public float LeverProgress { get; private set; }

        /// <summary>The lever works once every generator is running.</summary>
        public static bool Powered => GeneratorObjective.AllRunning;

        /// <summary>Pulls the lever for <paramref name="dt"/> seconds; true the moment the gate opens.</summary>
        public bool PullLever(float dt)
        {
            if (IsOpen || !Powered) return false;
            LeverProgress = Mathf.Min(1f, LeverProgress + dt / OpenTime);
            if (leverHandle != null) leverHandle.localRotation = Quaternion.Euler(LeverProgress * 110f, 0f, 0f);
            if (LeverProgress < 1f) return false;
            Open();
            return true;
        }

        public bool IsOpen { get; private set; }
        float raised;

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            LeverProgress = 1f;
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
        }
    }
}
