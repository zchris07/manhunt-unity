using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// The level's ground height, in design units (local space of the level root). Deterministic from a
    /// seed: four sine waves at random headings give rolling hills (slopes up to about 35°, about 5 m of
    /// relief) and a ridged term cuts long ditches. Sines rather than noise, so the steepest slope is
    /// bounded and predictable. Flat pads (cabin, spawn, campfires) blend in smoothly, and the ground
    /// flattens toward the arena walls. The ground mesh, prop placement, characters and camera all read it.
    /// </summary>
    public sealed class TerrainField
    {
        public struct Pad
        {
            public Rect Area;
            public float Height;
            public float Blend;
        }

        struct Wave
        {
            public Vector2 K;
            public float Phase, Amplitude;
        }

        readonly Wave[] hills;
        readonly Wave ditch;
        readonly List<Pad> pads = new List<Pad>();
        readonly float halfExtent, edgeBand;

        public IReadOnlyList<Pad> Pads => pads;
        PathNetwork paths;
        float pathFlat, pathBlend;

        /// <summary>Footpaths the ground is flattened across (null for none).</summary>
        public PathNetwork Paths => paths;

        /// <summary>
        /// Flattens the ground across each path to the path's own (smoothed) height: flat within
        /// <paramref name="flat"/> of the centre line, blending back to the hillside over <paramref name="blend"/>.
        /// The paths must have been routed over this field's heights without paths.
        /// </summary>
        public void SetPaths(PathNetwork network, float flat = 1.0f, float blend = 1.8f)
        {
            paths = network;
            pathFlat = flat;
            pathBlend = Mathf.Max(0.01f, blend);
        }
        public float HalfExtent => halfExtent;

        /// <param name="halfExtent">Half the arena size; the ground is flat from the walls inward over <paramref name="edgeBand"/>.</param>
        public TerrainField(int seed, float halfExtent, float edgeBand = 4f)
        {
            this.halfExtent = halfExtent;
            this.edgeBand = edgeBand;
            var rng = new System.Random(seed * 7919 + 17);
            float[] lengths = { 26f, 17f, 11f, 7f };
            float[] amplitudes = { 1.15f, 0.75f, 0.38f, 0.16f };
            hills = new Wave[lengths.Length];
            for (int i = 0; i < lengths.Length; i++) hills[i] = MakeWave(rng, lengths[i], amplitudes[i]);
            ditch = MakeWave(rng, 21f, 0.85f);
        }

        static Wave MakeWave(System.Random rng, float length, float amplitude)
        {
            float heading = (float)rng.NextDouble() * Mathf.PI * 2f;
            float k = Mathf.PI * 2f / length;
            return new Wave
            {
                K = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * k,
                Phase = (float)rng.NextDouble() * Mathf.PI * 2f,
                Amplitude = amplitude,
            };
        }

        /// <summary>Flattens <paramref name="area"/> to the natural height at its centre, blending over <paramref name="blend"/> units.</summary>
        public void AddPad(Rect area, float blend = 5f)
        {
            pads.Add(new Pad { Area = area, Height = Natural(area.center.x, area.center.y), Blend = Mathf.Max(0.01f, blend) });
        }

        public void AddPad(Vector2 centre, float radius, float blend = 4f) =>
            AddPad(new Rect(centre.x - radius, centre.y - radius, radius * 2f, radius * 2f), blend);

        /// <summary>Hills and ditches before pads and the wall band.</summary>
        public float Natural(float x, float z)
        {
            float h = 0f;
            for (int i = 0; i < hills.Length; i++)
                h += hills[i].Amplitude * Mathf.Sin(hills[i].K.x * x + hills[i].K.y * z + hills[i].Phase);
            float c = Mathf.Abs(Mathf.Cos(0.5f * (ditch.K.x * x + ditch.K.y * z + ditch.Phase)));
            float c2 = c * c;
            h -= ditch.Amplitude * c2 * c2 * c2;
            return h;
        }

        public float Height(float x, float z)
        {
            float h = Natural(x, z);
            for (int i = 0; i < pads.Count; i++)
            {
                Pad p = pads[i];
                float dx = Mathf.Max(p.Area.xMin - x, 0f, x - p.Area.xMax);
                float dz = Mathf.Max(p.Area.yMin - z, 0f, z - p.Area.yMax);
                float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Sqrt(dx * dx + dz * dz) / p.Blend);
                if (w > 0f) h = Mathf.Lerp(h, p.Height, w);
            }
            if (paths != null)
            {
                float d = paths.Distance(x, z, out float ph);
                if (d < pathFlat + pathBlend)
                    h = Mathf.Lerp(h, ph, 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - pathFlat) / pathBlend)));
            }
            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float inner = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfExtent - 1f, halfExtent - 1f - edgeBand, edge));
            return h * inner;
        }

        public Vector3 Normal(float x, float z)
        {
            const float e = 0.05f;
            float gx = (Height(x + e, z) - Height(x - e, z)) / (2f * e);
            float gz = (Height(x, z + e) - Height(x, z - e)) / (2f * e);
            return new Vector3(-gx, 1f, -gz).normalized;
        }

        public float SlopeDeg(float x, float z) => Vector3.Angle(Vector3.up, Normal(x, z));

        // ------------------------------------------------------------------ the active level

        static TerrainField active;
        static Transform activeRoot;

        /// <summary>The terrain of the level being played, and the (scaled) level root it lives under.</summary>
        public static TerrainField Active => activeRoot != null ? active : null;

        public static void SetActive(TerrainField field, Transform root)
        {
            active = field;
            activeRoot = root;
        }

        public static void ClearActive(Transform root)
        {
            if (activeRoot != root) return;
            active = null;
            activeRoot = null;
        }

        /// <summary>Ground height and normal in world space under a world point; false when there is no active terrain.</summary>
        public static bool TrySample(Vector3 world, out float height, out Vector3 normal)
        {
            TerrainField f = Active;
            if (f == null)
            {
                height = 0f;
                normal = Vector3.up;
                return false;
            }
            Vector3 local = activeRoot.InverseTransformPoint(world);
            float h = f.Height(local.x, local.z);
            height = activeRoot.TransformPoint(new Vector3(local.x, h, local.z)).y;
            normal = activeRoot.TransformDirection(f.Normal(local.x, local.z)).normalized;
            return true;
        }

        /// <summary>World-space ground height under a world point, or <paramref name="fallback"/> without terrain.</summary>
        public static float WorldHeight(Vector3 world, float fallback = 0f) =>
            TrySample(world, out float h, out _) ? h : fallback;
    }
}
