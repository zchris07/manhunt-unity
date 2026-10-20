using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vision.Visibility;

namespace Vision.World
{
    /// <summary>
    /// A stretch of a ruin's wall as play sees it, along the wall (metres from its start): solid (it stops you, and
    /// stops sight unless it's a window), low (a fallen stretch knee-high or less: climbed over, seen over), or nothing
    /// (a doorway, an arch to walk through, a stretch fallen to the ground).
    /// </summary>
    public struct RuinSolid
    {
        public float X0, X1, Height;
        public bool Low;
        /// <summary>Sight passes (a window).</summary>
        public bool SeeThrough;
    }

    /// <summary>Works out a ruin wall's solid stretches from its plan.</summary>
    public static class RuinSolids
    {
        /// <summary>Under this a fallen stretch is climbed and seen over; under the floor of it, it's gone.</summary>
        public const float LowHeight = 0.9f, Gone = 0.25f;
        /// <summary>The colliders stand no taller than this (play is on the plane; it only has to stop a body).</summary>
        public const float MaxHeight = 2.3f;

        enum Kind { Gap, Low, Full, Window }

        /// <summary>The wall's stretches, from start to end: solid ones (windows split out, for sight) and low ones.</summary>
        public static List<RuinSolid> Of(RuinRun run)
        {
            const float step = 0.05f;
            float len = run.Length;
            Kind At(float x)
            {
                float top = run.TopAt(x);
                if (top < Gone) return Kind.Gap;
                foreach (RuinOpening o in run.Openings)
                    if (Mathf.Abs(x - o.Center) < o.Half)
                    {
                        // A doorway or an arch from the ground: a way through, whatever's left above it.
                        if (o.Passage) return Kind.Gap;
                        // A window still standing above its sill: seen through, not walked through.
                        if (top > o.Sill + 0.1f) return Kind.Window;
                    }
                return top < LowHeight ? Kind.Low : Kind.Full;
            }
            var result = new List<RuinSolid>();
            int n = Mathf.Max(1, Mathf.CeilToInt(len / step));
            float start = 0f, height = 0f;
            Kind cur = At(step * 0.5f);
            for (int i = 0; i <= n; i++)
            {
                float x = Mathf.Min(len, (i + 0.5f) * step);
                Kind k = i < n ? At(x) : Kind.Gap;
                if (i < n && k == cur)
                {
                    height = Mathf.Max(height, cur == Kind.Window ? MaxHeight : run.TopAt(x));
                    continue;
                }
                float end = Mathf.Min(len, i * step);
                if (cur != Kind.Gap && end - start > 0.02f)
                    result.Add(new RuinSolid { X0 = start, X1 = end, Height = Mathf.Clamp(cur == Kind.Window ? MaxHeight : Mathf.Max(height, run.TopAt((start + end) * 0.5f)), 0.3f, MaxHeight), Low = cur == Kind.Low, SeeThrough = cur == Kind.Window });
                start = end;
                cur = k;
                height = i < n ? run.TopAt(x) : 0f;
            }
            return result;
        }
    }

    /// <summary>
    /// Brick ruins dotted about the map, somewhere new each match: a dozen or so, from lone broken walls and corners to
    /// the shells of buildings (<see cref="RuinPlanner"/>), laid brick by brick (<see cref="RuinBricks"/>). Every map has
    /// a big building of hallways, rooms and nooks, a plain rectangle part fallen open and half a rectangle's frame, each
    /// with a stone floor laid inside its walls (<see cref="RuinFloors"/>; nothing grows through it), and perhaps a
    /// church's, a warehouse's or a house's shell besides. Out on the open ground and in the woods (never on a path, in the lake, at a site or a
    /// generator's work site, or crowding anything already standing; the trees grow round them), each on ground level
    /// enough for it to stand on one flat base. Their walls stop you and block sight, but you walk through their doors
    /// and arches, see through their windows, and climb and see over a stretch fallen to knee height; the machete
    /// strikes them as brick.
    /// </summary>
    public sealed partial class SandboxWorld
    {
        /// <summary>How many ruins stand on the default-sized map (more or fewer with its area).</summary>
        public const int RuinCount = 12;
        /// <summary>A ruin's wall: two bricks back to back.</summary>
        public const float RuinThickness = LowPolyModels.WallThickness;
        /// <summary>
        /// How uneven the ground under a ruin may be: it stands on one flat base at the lowest point, so on a slope its
        /// lowest courses are buried uphill (a building's shell may sit on more of one).
        /// </summary>
        public const float RuinMaxSlope = 0.6f, RuinMaxSlopeBuilding = 1.4f;

        /// <summary>Every wall of every ruin (its two ends on the plane), for the map.</summary>
        public readonly List<(Vector2 a, Vector2 b)> MapRuins = new List<(Vector2, Vector2)>();
        /// <summary>Each ruin placed: its kind, where it stands, its yaw (radians) and its plan.</summary>
        public readonly List<(RuinArchetype kind, Vector2 at, float yaw, RuinPlan plan, GameObject root)> Ruins = new List<(RuinArchetype, Vector2, float, RuinPlan, GameObject)>();

        /// <summary>How the last placement went: ruins placed, and why buildings' shells were turned away where they were.</summary>
        public string RuinReport { get; private set; }
        readonly Dictionary<string, int> ruinRejects = new Dictionary<string, int>();

        static Vector2 Turn(Vector2 p, float yaw)
        {
            float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            return new Vector2(p.x * c - p.y * s, p.x * s + p.y * c);
        }

        /// <summary>A building's shell sited with the map's layout: its plan, its seed, where it stands, its yaw, its reach.</summary>
        struct RuinSite { public RuinPlan Plan; public int Seed; public Vector2 At; public float Yaw, Radius; }
        readonly List<RuinSite> ruinSites = new List<RuinSite>();

        /// <summary>The shells every map has (sited first, the biggest first), each with a stone floor.</summary>
        public static readonly RuinArchetype[] RequiredRuins = { RuinArchetype.Complex, RuinArchetype.Enclosure, RuinArchetype.HalfFrame };

        /// <summary>Each floored ruin: where it stands, its yaw, its plan and its reach (for what grows, and footsteps).</summary>
        readonly List<(Vector2 at, float yaw, RuinPlan plan, float radius)> ruinFloors = new List<(Vector2, float, RuinPlan, float)>();

        /// <summary>Whether a point is on a ruin's stone floor (within <paramref name="pad"/> of it).</summary>
        public bool OnRuinFloor(Vector2 p, float pad = 0f)
        {
            foreach (var (at, yaw, plan, radius) in ruinFloors)
            {
                Vector2 d = p - at;
                if (d.sqrMagnitude > (radius + pad) * (radius + pad)) continue;
                if (plan.OnFloor(Turn(d, -yaw), pad)) return true;
            }
            return false;
        }

        /// <summary>The shells sited for a layout (testing: whether every map gets its three), without building anything.</summary>
        public List<RuinArchetype> SitedRuinKinds(MapLayout layout)
        {
            SiteRuins(layout);
            return ruinSites.Select(s => s.Plan.Archetype).ToList();
        }

        int RuinTarget => Mathf.Max(1, Mathf.RoundToInt(RuinCount * halfExtent * halfExtent / (90f * 90f)));

        /// <summary>
        /// Sites the buildings' shells with the layout, before the ground is shaped (ground level enough for a whole building
        /// is rare in these hills): the three every map has (<see cref="RequiredRuins"/>), biggest first, and half the time
        /// a church's, a warehouse's or a house's besides, each well clear of everything the layout holds (sites, the lake,
        /// fences, logs, power poles, the spawn, the generators, each other; the spacing eased off as the tries run on, so
        /// the three always find somewhere). The ground is levelled under each, the paths go round them, and nothing else
        /// is placed on them.
        /// </summary>
        void SiteRuins(MapLayout layout)
        {
            ruinSites.Clear();
            System.Random r = FeatureRng(44);
            float R(float lo, float hi) => lo + (float)r.NextDouble() * (hi - lo);
            var kinds = RequiredRuins.Select(k => (RuinArchetype?)k).ToList();
            if (r.NextDouble() < 0.5) kinds.Add(null);
            float lim = halfExtent - 8f;
            float SegDist(Vector2 p, Vector2 a, Vector2 b)
            {
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                return (a + ab * t - p).magnitude;
            }
            foreach (RuinArchetype? kind in kinds)
                for (int attempt = 0; attempt < 900; attempt++)
                {
                    // (Easing off the spacing as the tries run on.)
                    float ease = attempt < 300 ? 1f : attempt < 600 ? 0.65f : 0.35f;
                    var at = new Vector2(R(-lim, lim), R(-lim, lim));
                    float yaw = R(0f, Mathf.PI * 2f);
                    int planSeed = r.Next();
                    RuinPlan plan = kind.HasValue ? RuinPlanner.Generate(new System.Random(planSeed), kind.Value) : RuinPlanner.Generate(new System.Random(planSeed), RuinTier.Structure);
                    Rect b = plan.Bounds;
                    float radius = 0f;
                    foreach (Vector2 c in new[] { new Vector2(b.xMin, b.yMin), new Vector2(b.xMax, b.yMin), new Vector2(b.xMax, b.yMax), new Vector2(b.xMin, b.yMax) })
                        radius = Mathf.Max(radius, c.magnitude);
                    radius += 0.8f;
                    bool ok = at.magnitude + radius < lim && (at - layout.Spawn).magnitude > radius + 10f * ease;
                    foreach (Vector2 g in layout.WoodsGenerators) ok &= (at - g).magnitude > radius + 12f * ease;
                    foreach (Vector2 pole in layout.PowerPoles) ok &= (at - pole).magnitude > radius + 2.5f;
                    foreach (MapLayout.Segment f in layout.Fences) ok &= SegDist(at, f.A, f.B) > radius + 1f + 1.5f * ease;
                    foreach (MapLayout.Segment l in layout.Logs) ok &= SegDist(at, l.A, l.B) > radius + 1f + 1.5f * ease;
                    foreach (RuinSite s in ruinSites) ok &= (at - s.At).magnitude > radius + s.Radius + 1.5f + 2.5f * ease;
                    for (float a = 0f; ok && a < Mathf.PI * 2f; a += Mathf.PI / 8f)
                        for (float k = 0f; ok && k <= 1.001f; k += 0.25f)
                        {
                            Vector2 p = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius + 0.5f + 1f * ease) * k;
                            if (layout.Blocked(p) || layout.LakeDepth(p) > -3f) ok = false;
                        }
                    if (!ok) continue;
                    ruinSites.Add(new RuinSite { Plan = plan, Seed = planSeed, At = at, Yaw = yaw, Radius = radius });
                    layout.KeepCircles.Add((at, radius + 1f));
                    break;
                }
        }

        void BuildRuins()
        {
            MapRuins.Clear();
            Ruins.Clear();
            ruinFloors.Clear();
            ruinRejects.Clear();
            int structureTries = ruinSites.Count;
            Transform parent = Group("Brick Ruins");
            System.Random r = FeatureRng(43);
            float R(float lo, float hi) => lo + (float)r.NextDouble() * (hi - lo);
            int target = RuinTarget;
            int structures = 0, placed = 0;
            float lim = halfExtent - 6f;
            // The buildings' shells, on the level ground kept for them.
            foreach (RuinSite s in ruinSites)
            {
                float lo = float.MaxValue;
                foreach (RuinRun run in s.Plan.Runs)
                    for (float k = 0f; k <= 1.001f; k += 0.1f) lo = Mathf.Min(lo, H(s.At + Turn(Vector2.Lerp(run.A, run.B, k), s.Yaw)));
                PlaceRuin(s.Plan, s.At, s.Yaw, lo - 0.03f, s.Seed, parent, true);
                structures++;
                placed++;
            }
            // Then broken walls and corners (five to three) wherever they fit.
            bool Try(RuinTier tier)
            {
                // The same draws every attempt, so a rejected one never changes what comes after.
                var at = new Vector2(R(-lim, lim), R(-lim, lim));
                float yaw = R(0f, Mathf.PI * 2f);
                int planSeed = r.Next();
                RuinPlan plan = RuinPlanner.Generate(new System.Random(planSeed), tier);
                if (!ClearForRuin(plan, at, yaw, lim, out float baseY)) return false;
                PlaceRuin(plan, at, yaw, baseY, planSeed, parent, true);
                placed++;
                return true;
            }
            for (int attempt = 0; attempt < target * 80 && placed < target; attempt++)
                Try(R(0f, 1f) < 0.625f ? RuinTier.Fragment : RuinTier.Corner);
            RuinReport = $"{placed} ruins ({structures} buildings from {structureTries} tries; turned away for {string.Join(", ", ruinRejects.Select(kv => kv.Key + " " + kv.Value))})";
        }

        /// <summary>Whether a ruin fits there (its walls and the floor inside it), and the height of its flat base.</summary>
        bool ClearForRuin(RuinPlan plan, Vector2 at, float yaw, float lim, out float baseY)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            baseY = 0f;
            bool No(string why)
            {
                if (plan.Structure) ruinRejects[why] = ruinRejects.TryGetValue(why, out int n) ? n + 1 : 1;
                return false;
            }
            bool Ok(Vector2 local, float radius, float spacing)
            {
                Vector2 p = at + Turn(local, yaw);
                if (p.magnitude > lim) return No("edge");
                if (Layout.Blocked(p)) return No("site");
                if (Layout.LakeDepth(p) > -2f - radius) return No("lake");
                if (Terrain.Paths != null && Terrain.PathClearance(p.x, p.y) < radius + 0.4f) return No("path");
                if (!Free(p, radius, spacing)) return No("crowded");
                if ((p - Layout.Spawn).magnitude < 8f) return No("spawn");
                foreach (Vector2 g in Layout.WoodsGenerators) if ((p - g).magnitude < 11f) return No("generator");
                float h = H(p);
                lo = Mathf.Min(lo, h);
                hi = Mathf.Max(hi, h);
                return hi - lo <= (plan.Structure ? RuinMaxSlopeBuilding : RuinMaxSlope) || No("slope");
            }
            foreach (RuinRun run in plan.Runs)
            {
                float len = run.Length;
                int n = Mathf.Max(2, Mathf.CeilToInt(len / 0.6f));
                for (int i = 0; i <= n; i++)
                    if (!Ok(Vector2.Lerp(run.A, run.B, i / (float)n), 0.5f, 1.4f)) return false;
            }
            if (plan.Outline.Count > 2)
            {
                Rect b = plan.Bounds;
                for (float x = b.xMin + 0.5f; x < b.xMax; x += 1f)
                    for (float y = b.yMin + 0.5f; y < b.yMax; y += 1f)
                        if (plan.Inside(new Vector2(x, y)) && !Ok(new Vector2(x, y), 0.4f, 0.6f)) return false;
            }
            baseY = lo - 0.03f;
            return true;
        }

        /// <summary>
        /// Builds a ruin from its plan at a spot (its base at <paramref name="baseY"/>): its brick meshes (in chunks), a
        /// collider and a sight-blocker for each solid stretch of each wall (low ones climbed and seen over), none over
        /// its doors, arches and windows. Registered, it's on the map and keeps everything else clear of its walls.
        /// </summary>
        public GameObject PlaceRuin(RuinPlan plan, Vector2 at, float yaw, float baseY, int seed, Transform parent, bool register)
        {
            var root = new GameObject($"Brick Ruin ({plan.Archetype})");
            root.SetActive(false);
            root.transform.SetParent(parent != null ? parent : staticRoot, false);
            root.transform.SetLocalPositionAndRotation(new Vector3(at.x, baseY, at.y), Quaternion.Euler(0f, -yaw * Mathf.Rad2Deg, 0f));
            root.isStatic = true;

            // The stone floor's top over the base (the ground there is level, the base a little under it).
            float floorTop = plan.Floored ? H(at) - baseY + RuinFloors.StoneTop : 0f;
            RuinBricks.Chunks chunks = RuinBricks.Build(plan, new System.Random(seed + 1), floorTop);
            foreach (LowPolyMeshBuilder b in chunks.Builders)
            {
                if (b.TriangleCount == 0) continue;
                var go = new GameObject("Brick Wall Ruin");
                go.transform.SetParent(root.transform, false);
                go.isStatic = true;
                go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Brick Wall Ruin");
                PropFactory.NoShadows(go.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;
            }
            if (plan.Floored)
            {
                var floor = new GameObject("Stone Floor Ruin");
                floor.transform.SetParent(root.transform, false);
                floor.isStatic = true;
                floor.AddComponent<MeshFilter>().sharedMesh = RuinFloors.Build(plan, new System.Random(seed + 2), H(at) - baseY, out _);
                PropFactory.NoShadows(floor.AddComponent<MeshRenderer>()).sharedMaterial = StoneMaterial;
            }

            float t = RuinThickness;
            var lows = new List<(GameObject go, BoxCollider box)>();
            foreach (RuinRun run in plan.Runs)
            {
                Vector2 d = run.Dir;
                Quaternion rot = Quaternion.Euler(0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 0f);
                foreach (RuinSolid s in RuinSolids.Of(run))
                {
                    var go = new GameObject(s.Low ? "Brick Wall Ruin Low" : "Brick Wall Ruin Collider");
                    go.transform.SetParent(root.transform, false);
                    go.transform.SetLocalPositionAndRotation(new Vector3(run.A.x, 0f, run.A.y), rot);
                    go.isStatic = true;
                    float xm = (s.X0 + s.X1) * 0.5f, l = s.X1 - s.X0;
                    var box = go.AddComponent<BoxCollider>();
                    box.center = new Vector3(xm, s.Height * 0.5f, 0f);
                    box.size = new Vector3(l, s.Height, t);
                    if (!s.SeeThrough)
                    {
                        var occ = go.AddComponent<Occluder>();
                        occ.shape = Occluder.Shape.Box;
                        occ.size = new Vector2(l, t);
                        occ.offset = new Vector2(xm, 0f);
                    }
                    if (s.Low) lows.Add((go, box));
                }
                if (register)
                {
                    Vector2 a = at + Turn(run.A, yaw), b = at + Turn(run.B, yaw);
                    int n = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.6f));
                    for (int i = 0; i <= n; i++) blocked.Add(Vector2.Lerp(a, b, i / (float)n));
                    MapRuins.Add((a, b));
                }
            }
            root.SetActive(true);
            // Knee-high stretches: climbed over and seen over, like a fallen log.
            foreach (var (go, box) in lows) MakeLow(go, box);
            if (register)
            {
                Ruins.Add((plan.Archetype, at, yaw, plan, root));
                if (plan.Floored)
                {
                    Rect b = plan.Bounds;
                    float reach = Mathf.Max(Mathf.Max(new Vector2(b.xMin, b.yMin).magnitude, new Vector2(b.xMax, b.yMin).magnitude), Mathf.Max(new Vector2(b.xMax, b.yMax).magnitude, new Vector2(b.xMin, b.yMax).magnitude));
                    ruinFloors.Add((at, yaw, plan, reach));
                }
            }
            return root;
        }
    }
}
