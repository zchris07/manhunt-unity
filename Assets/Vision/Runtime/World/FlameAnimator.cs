using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// Makes a fire's flame tongues lick and sway (each stretches and leans about its base, driven by its own
    /// noise), and loops a few embers rising from the fire, shrinking as they go.
    /// </summary>
    public sealed class FlameAnimator : MonoBehaviour
    {
        public Transform[] flames;
        public Transform[] embers;
        [Tooltip("How fast the flames move.")]
        public float speed = 1f;
        [Tooltip("How high the embers rise, in design units.")]
        public float emberRise = 1.1f;

        Vector3[] flameScale;
        Quaternion[] flameRotation;
        Vector3[] emberStart;
        Vector3 emberScale = Vector3.one;
        float seed;

        void Awake()
        {
            seed = Mathf.Repeat(transform.position.x * 0.731f + transform.position.z * 0.377f, 97f);
            flameScale = new Vector3[flames.Length];
            flameRotation = new Quaternion[flames.Length];
            for (int i = 0; i < flames.Length; i++)
            {
                flameScale[i] = flames[i].localScale;
                flameRotation[i] = flames[i].localRotation;
            }
            emberStart = new Vector3[embers.Length];
            for (int i = 0; i < embers.Length; i++) emberStart[i] = embers[i].localPosition;
            if (embers.Length > 0) emberScale = embers[0].localScale;
        }

        void Update() => Pose(Time.time);

        /// <summary>The pose at time <paramref name="t"/> (seconds); deterministic, so it can be tested and captured.</summary>
        public void Pose(float t)
        {
            if (flameScale == null) Awake();
            float s = t * speed;
            for (int i = 0; i < flames.Length; i++)
            {
                float a = Mathf.PerlinNoise(s * 3.1f + i * 1.7f, seed) * 2f - 1f;
                float b = Mathf.PerlinNoise(seed + i * 2.3f, s * 2.4f) * 2f - 1f;
                float c = Mathf.PerlinNoise(s * 5.3f + i, seed + 11f) * 2f - 1f;
                flames[i].localScale = Vector3.Scale(flameScale[i], new Vector3(1f - 0.12f * a, 1f + 0.32f * a + 0.12f * c, 1f - 0.12f * a));
                flames[i].localRotation = flameRotation[i] * Quaternion.Euler(b * 11f, 0f, a * 11f);
            }
            for (int i = 0; i < embers.Length; i++)
            {
                float phase = Mathf.Repeat(s * 0.55f + i / (float)embers.Length + seed * 0.13f, 1f);
                var drift = new Vector3(Mathf.Sin(s * 2f + i * 3f), 0f, Mathf.Cos(s * 1.7f + i * 2f)) * (0.12f * phase);
                embers[i].localPosition = emberStart[i] + Vector3.up * (emberRise * phase) + drift;
                embers[i].localScale = emberScale * (1f - phase);
            }
        }
    }
}
