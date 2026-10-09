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

        MapLayout lake;
        float lakeLevel;

        /// <summary>The water surface height (design units) when there is a lake.</summary>
        public float LakeLevel => lakeLevel;
        public MapLayout LakeLayout => lake;

        /// <summary>
        /// Carves the layout's lake: the shore flattens to the water level over a few metres, and the bed falls away
        /// under the water to about a metre deep.
        /// </summary>
        public void SetLake(MapLayout layout)
        {
            lake = layout;
            lakeLevel = Natural(layout.LakeCentre.x, layout.LakeCentre.y) - 0.6f;
        }

        /// <summary>Whether a point is standing in the lake's water (wading), not on the dock.</summary>
        public bool InWater(float x, float z)
        {
            if (lake == null) return false;
            var p = new Vector2(x, z);
            if (lake.LakeDepth(p) <= 0.4f) return false;
            Vector2 a = lake.DockStart, ab = lake.DockEnd - lake.DockStart;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude > lake.DockHalfWidth;
        }

        /// <summary>Whether a world point is in the active level's lake.</summary>
        public static bool InWaterAt(Vector3 world)
        {
            TerrainField f = Active;
            if (f == null) return false;
            Vector3 local = activeRoot.InverseTransformPoint(world);
            return f.InWater(local.x, local.z);
        }

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

        // A baked copy of the finished field on a fine grid: building a 180 m level asks for millions of heights,
        // and the full function (pads, paths, lake) is far too slow for that.
        float[] bakedHeight, bakedPath, bakedAlong;
        int[] bakedWhich;
        float bakeStep, bakeMin;
        int bakeN;

        public bool IsBaked => bakedHeight != null;

        /// <summary>
        /// Samples the finished terrain (after its pads, lake and paths are set) and the distance to the nearest path onto
        /// a <paramref name="step"/> grid; from then on <see cref="Height"/> and <see cref="PathDistance"/> read the grid.
        /// </summary>
        public void Bake(float step = 0.25f)
        {
            float ext = halfExtent + 6f;
            int n = Mathf.CeilToInt(2f * ext / step) + 1;
            var h = new float[n * n];
            var d = new float[n * n];
            float min = -ext;
            // Paths first, drawn into the grid in one pass; the heights then read the path terms from it.
            var ph = new float[n * n];
            var which = new int[n * n];
            var along = new float[n * n];
            if (paths != null) paths.Rasterize(min, step, n, pathFlat + pathBlend + 6f, 1000f, d, ph, which, along);
            else for (int k = 0; k < d.Length; k++) { d[k] = 1000f; which[k] = -1; }
            System.Threading.Tasks.Parallel.For(0, n, j =>
            {
                float z = min + j * step;
                for (int i = 0; i < n; i++)
                {
                    int k = j * n + i;
                    h[k] = HeightCore(min + i * step, z, d[k], ph[k]);
                }
            });
            bakeStep = step;
            bakeMin = min;
            bakeN = n;
            bakedHeight = h;
            bakedPath = d;
            bakedWhich = which;
            bakedAlong = along;
        }

        float Sample(float[] grid, float x, float z)
        {
            float fx = Mathf.Clamp((x - bakeMin) / bakeStep, 0f, bakeN - 1.001f);
            float fz = Mathf.Clamp((z - bakeMin) / bakeStep, 0f, bakeN - 1.001f);
            int i = (int)fx, j = (int)fz;
            float tx = fx - i, tz = fz - j;
            int k = j * bakeN + i;
            float a = grid[k] + (grid[k + 1] - grid[k]) * tx;
            float b = grid[k + bakeN] + (grid[k + bakeN + 1] - grid[k + bakeN]) * tx;
            return a + (b - a) * tz;
        }

        /// <summary>
        /// The nearest path at a point: which one (-1 none), how far along it, how far from its centre line, and its
        /// half-width there.
        /// </summary>
        public bool PathAt(float x, float z, out int path, out float along, out float distance, out float halfWidth)
        {
            path = -1;
            along = 0f;
            halfWidth = paths != null ? paths.HalfWidth : 0f;
            distance = PathDistance(x, z);
            if (paths == null || bakedWhich == null || distance > 50f) return false;
            int i = Mathf.Clamp(Mathf.RoundToInt((x - bakeMin) / bakeStep), 0, bakeN - 1);
            int j = Mathf.Clamp(Mathf.RoundToInt((z - bakeMin) / bakeStep), 0, bakeN - 1);
            path = bakedWhich[j * bakeN + i];
            if (path < 0) return false;
            along = bakedAlong[j * bakeN + i];
            halfWidth = paths.HalfWidthAt(path, along);
            return true;
        }

        /// <summary>How far a point is outside the nearest path's edge (negative on the path), with each path's own width.</summary>
        public float PathClearance(float x, float z)
        {
            if (paths == null) return 1000f;
            PathAt(x, z, out _, out _, out float d, out float hw);
            return d - hw;
        }

        /// <summary>Distance to the nearest path centre line (large when there is none).</summary>
        public float PathDistance(float x, float z)
        {
            if (bakedPath != null) return Sample(bakedPath, x, z);
            return paths != null ? paths.Distance(x, z, out _) : 1000f;
        }

        public float Height(float x, float z) => bakedHeight != null ? Sample(bakedHeight, x, z) : HeightExact(x, z);

        /// <summary>The terrain function itself (hills, pads, paths, lake, wall band), unbaked.</summary>
        public float HeightExact(float x, float z)
        {
            float d = 1000f, ph = 0f;
            if (paths != null) d = paths.Distance(x, z, out ph);
            return HeightCore(x, z, d, ph);
        }

        /// <summary>The terrain at a point given the distance to the nearest path and that path's height there.</summary>
        float HeightCore(float x, float z, float pathDistance, float pathHeight)
        {
            float h = Natural(x, z);
            for (int i = 0; i < pads.Count; i++)
            {
                Pad p = pads[i];
                // Two-argument Max: the three-argument overload allocates an array on every call.
                float dx = Mathf.Max(Mathf.Max(p.Area.xMin - x, x - p.Area.xMax), 0f);
                float dz = Mathf.Max(Mathf.Max(p.Area.yMin - z, z - p.Area.yMax), 0f);
                float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Sqrt(dx * dx + dz * dz) / p.Blend);
                if (w > 0f) h = Mathf.Lerp(h, p.Height, w);
            }
            if (paths != null && pathDistance < pathFlat + pathBlend)
                h = Mathf.Lerp(h, pathHeight, 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((pathDistance - pathFlat) / pathBlend)));
            if (lake != null)
            {
                float depth = lake.LakeDepth(new Vector2(x, z));
                if (depth > -5f)
                {
                    h = Mathf.Lerp(h, lakeLevel + 0.15f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-5f, 0f, depth)));
                    if (depth > 0f) h -= 1.1f * Mathf.SmoothStep(0f, 1f, depth / 3.5f);
                }
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
