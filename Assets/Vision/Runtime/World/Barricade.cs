using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// A pallet standing beside a doorway, as in the original: drop it and it falls across the gap and blocks it.
    /// The pallet turns on a hinge at the doorway's edge, from upright to lying across.
    /// </summary>
    public sealed class Barricade : MonoBehaviour
    {
        public Transform hinge;
        public Collider blocker;
        public float fallSpeed = 420f;
        /// <summary>Upright and lying-across angles about the hinge's z axis.</summary>
        public float upAngle = 90f, downAngle = 6f;
        /// <summary>The gap it falls across (design units on the level's ground plane).</summary>
        public Vector2 a, b;

        public static readonly List<Barricade> All = new List<Barricade>();

        public bool IsDown { get; private set; }
        float angle;

        void Awake() => angle = upAngle;
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public bool IsBroken { get; private set; }

        /// <summary>The match's state for this pallet: 0 up, 1 down across the gap, 2 smashed.</summary>
        public void SetState(int state)
        {
            if (state >= 1) Drop();
            if (state == 2 && !IsBroken)
            {
                IsBroken = true;
                if (blocker != null) blocker.enabled = false;
                if (hinge != null) hinge.gameObject.SetActive(false);
            }
        }

        public void Drop()
        {
            if (IsDown) return;
            IsDown = true;
            if (blocker != null) blocker.enabled = true;
        }

        /// <summary>Where a player stands to drop it: the middle of the gap.</summary>
        public Vector3 Centre => blocker != null ? blocker.bounds.center : transform.position;

        void Update()
        {
            float target = IsDown ? downAngle : upAngle;
            if (Mathf.Approximately(angle, target)) return;
            angle = Mathf.MoveTowards(angle, target, fallSpeed * Time.deltaTime);
            if (hinge != null) hinge.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
