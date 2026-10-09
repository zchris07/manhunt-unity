using System.Collections.Generic;
using UnityEngine;
using Vision.Visibility;

namespace Vision.World
{
    /// <summary>
    /// A generator the survivor starts by holding Interact beside it, as in the original: 70 seconds of work from
    /// nothing to running. Progress is kept when you let go. Running, it shakes and throws a small warm light.
    /// </summary>
    [ExecuteAlways]
    public sealed class GeneratorObjective : MonoBehaviour
    {
        /// <summary>The original's repair time for one survivor.</summary>
        public const float RepairTime = 70f;

        public float progress;
        public Transform body;
        public VisionLight glow;

        public bool Running => progress >= 1f;

        public static readonly List<GeneratorObjective> All = new List<GeneratorObjective>();

        public static int RunningCount
        {
            get
            {
                int n = 0;
                foreach (GeneratorObjective g in All) if (g.Running) n++;
                return n;
            }
        }

        public static bool AllRunning => All.Count > 0 && RunningCount == All.Count;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        Vector3 rest;
        bool restSet;

        void Start()
        {
            if (glow != null) glow.enabled = Running;
        }

        /// <summary>Works on it for <paramref name="dt"/> seconds; true the moment it starts.</summary>
        public bool Repair(float dt)
        {
            if (Running) return false;
            progress = Mathf.Min(1f, progress + dt / RepairTime);
            if (!Running) return false;
            if (glow != null) glow.enabled = true;
            return true;
        }

        /// <summary>The match's state for this generator (progress 0-1; repaired: running).</summary>
        public void SetState(float value, bool repaired)
        {
            progress = repaired ? 1f : Mathf.Min(value, 0.999f);
            if (glow != null) glow.enabled = repaired;
        }

        void Update()
        {
            if (!Running || body == null) return;
            if (!restSet)
            {
                rest = body.localPosition;
                restSet = true;
            }
            float t = Time.time * 38f;
            body.localPosition = rest + new Vector3(Mathf.Sin(t) * 0.006f, Mathf.Abs(Mathf.Sin(t * 1.3f)) * 0.008f, Mathf.Cos(t * 0.9f) * 0.006f);
        }
    }
}
