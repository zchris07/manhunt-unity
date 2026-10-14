using System.Collections.Generic;
using UnityEngine;
using Vision.Characters;
using Vision.Player;
using Vision.Rendering;
using Vision.Visibility;

namespace Vision.World
{
    /// <summary>
    /// Builds the whole level from <see cref="seed"/>, laid out like the original 2D game's map (<see cref="MapLayout"/>):
    /// a 180 m square of hills and woods walled in, the central building's site with the gate yard north of it,
    /// the survivors' spawn in the south, clearings joined by footpaths, three cabins, the woods generators with
    /// their cover, a lake with a dock in one corner, fences, logs, campfires, tall-grass hiding patches, power
    /// lines, a graveyard, a playground and a hanging tree; then about 2,900 trees, rocks and plants, crows, the
    /// wanderer and the player.
    ///
    /// The layout is in design units (local space); the level root is scaled by <see cref="WorldScale"/>.
    /// The level is built when Play starts (and again by <see cref="Regenerate"/>); nothing of it is saved in the scene.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed partial class SandboxWorld : MonoBehaviour
    {
        public int seed = 1337;
        [Tooltip("Half the map's width in design units: the original's 6000 units at 3 cm each.")]
        /// <summary>The playable radius (design units): the map is a circle, its first boundary ring here.</summary>
        public float halfExtent = 90f;
        /// <summary>Where the world ends: the outermost boundary ring (MatchSim.Boundary), as far as anything is generated.</summary>
        public float WorldRadius => halfExtent + Game.Balance.Boundary.Outer;
        /// <summary>Half the side of the square the maps show (the whole circle of the world).</summary>
        public float MapExtent => WorldRadius;
        [Tooltip("Build the level when Play starts.")]
        public bool generateOnAwake = true;

        [Header("Props and materials")]
        [Tooltip("Saved prop prefabs. When empty, props are generated in memory instead.")]
        public PropLibrary library;
        public Material lowPolyMaterial;
        public Material entityMaterial;
        public Material glowMaterial;

        Material groundMaterial;

        /// <summary>
        /// The ground's material (and the fallen logs'): the low-poly one, marking its pixels as the ground (stencil 1),
        /// so the laser and rounds in flight draw over it whatever its hills do, and behind anything else nearer.
        /// </summary>
        public Material GroundMaterial
        {
            get
            {
                if (groundMaterial == null && lowPolyMaterial != null)
                {
                    groundMaterial = new Material(lowPolyMaterial) { name = "Ground", hideFlags = HideFlags.DontSave };
                    groundMaterial.SetFloat("_StencilRef", 1f);
                }
                return groundMaterial;
            }
        }

        [Header("Wiring")]
        public TopDownCamera cameraRig;
        public VisionMaskRenderer maskRenderer;

        [Header("Generated")]
        public PlayerController Player;
        public Wanderer Wanderer;
        public List<Door> Doors = new List<Door>();
        public List<Transform> Crows = new List<Transform>();
        /// <summary>Scarecrow stakes and the Four Notes, in build order.</summary>
        public readonly List<Transform> Stakes = new List<Transform>();
        public readonly List<Transform> Notes = new List<Transform>();
        public List<Pickup> Pickups = new List<Pickup>();
        public List<Transform> Generators = new List<Transform>();
        /// <summary>Hiding spots, pallets and windows in build order (their index is their id in the match).</summary>
        public readonly List<HidingSpot> HidingSpots = new List<HidingSpot>();
        public readonly List<Barricade> BarricadeList = new List<Barricade>();
        public readonly List<WindowPiece> Windows = new List<WindowPiece>();

        /// <summary>Layers: the terrain, and characters (sight and body checks ignore both).</summary>
        public const int GroundLayer = 8, CharacterLayer = 9;
        /// <summary>Fallen logs and trees: players step over them (vaulting), NPCs walk round them; sight and shots pass over.</summary>
        public const int LowLayer = 12;

        /// <summary>
        /// Fences (the split-rail fences and the graveyard's iron fence): players climb over them like a fallen log, at the
        /// vaulting pace, while NPCs walk round them. Unlike a log they stand upright: they don't block sight or shots (sight
        /// and shots pass through their rails), but they still cover a line drawn over the terrain, as a wall does.
        /// </summary>
        public const int VaultLayer = 13;
        /// <summary>
        /// The graveyard's fence: survivors (and NPCs) run into it; only Zach's body passes it (his controller leaves this
        /// layer out), climbing over it five times as slowly as a log.
        /// </summary>
        public const int GraveFenceLayer = 14;

        /// <summary>
        /// A thing players vault (design units, the level's plane): a fallen log or tree, or a fence. <see cref="Slow"/> is how
        /// much longer it takes to get over than a log (1, or 0 meaning 1; the graveyard's spiked fence 1.5).
        /// </summary>
        public struct LowObstacle { public Vector2 A, B; public float Half, Height, Slow; public bool Fence, HunterOnly; }
        public readonly List<LowObstacle> Lows = new List<LowObstacle>();

        /// <summary>
        /// How high a body crossing at <paramref name="p"/> is lifted (design units): a smooth bump over a fallen log or tree,
        /// or over the sill of a broken window, peaking on top of it; zero anywhere else. Only the drawing uses it.
        /// </summary>
        public float VaultLift(Vector2 p, float radius, bool[] brokenWindows)
        {
            float lift = 0f;
            foreach (LowObstacle o in Lows)
            {
                float d = Vision.Game.SimMap.SegmentDistance(p, o.A, o.B), reach = o.Half + radius;
                if (d < reach) lift = Mathf.Max(lift, o.Height * Mathf.Cos(d / reach * Mathf.PI * 0.5f));
            }
            if (brokenWindows != null)
                for (int i = 0; i < Windows.Count && i < brokenWindows.Length; i++)
                {
                    if (!brokenWindows[i]) continue;
                    float d = Vision.Game.SimMap.SegmentDistance(p, Windows[i].a, Windows[i].b), reach = 0.15f + radius;
                    if (d < reach) lift = Mathf.Max(lift, WindowVaultHeight * Mathf.Cos(d / reach * Mathf.PI * 0.5f));
                }
            return lift;
        }

        /// <summary>How high the body goes over a broken window's sill, and over a fallen log or tree (design units).</summary>
        public const float WindowVaultHeight = 0.55f, LogVaultHeight = 0.42f;
        /// <summary>How high the body goes over a split-rail fence and over the graveyard's iron fence (Zach only).</summary>
        public const float FenceVaultHeight = 0.8f, IronFenceVaultHeight = 1.1f;
        /// <summary>The graveyard's iron fence is built of panels at most this long (metres), each sat on the ground under it.</summary>
        public const float IronPanel = 2.5f;
        /// <summary>A fence's vault strip reaches this far either side of it (the level's units), as far as a log's.</summary>
        public const float FenceVaultHalf = 0.3f;

        /// <summary>
        /// Makes a fence vaultable: players pass through its collider (climbing over it), NPCs don't. A
        /// <paramref name="hunterOnly"/> one (the graveyard's) only Zach climbs.
        /// </summary>
        void MakeVaultable(GameObject go, BoxCollider box, float height, float slow, bool hunterOnly = false)
        {
            go.layer = hunterOnly ? GraveFenceLayer : VaultLayer;
            Transform t = box.transform;
            Vector3 c = box.center, e = new Vector3(box.size.x * 0.5f, 0f, 0f);
            Vector3 a = transform.InverseTransformPoint(t.TransformPoint(c - e)), b = transform.InverseTransformPoint(t.TransformPoint(c + e));
            Lows.Add(new LowObstacle { A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z), Half = FenceVaultHalf, Height = height, Slow = slow, Fence = true, HunterOnly = hunterOnly });
        }

        /// <summary>Records a lying obstacle from its box collider, and makes it low: vaulted, seen and shot over.</summary>
        void MakeLow(GameObject go, BoxCollider box)
        {
            go.layer = LowLayer;
            // Drawn as the ground: the laser and rounds pass over it.
            foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true))
                if (r.sharedMaterial == lowPolyMaterial && lowPolyMaterial != null) r.sharedMaterial = GroundMaterial;
            var occ = go.GetComponent<Vision.Visibility.Occluder>();
            if (occ != null && !occ.low)
            {
                occ.low = true;
                if (occ.isActiveAndEnabled) occ.Refresh();
            }
            if (box == null) return;
            Transform t = box.transform;
            Vector3 c = box.center, e = new Vector3(box.size.x * 0.5f, 0f, 0f);
            Vector3 a = transform.InverseTransformPoint(t.TransformPoint(c - e)), b = transform.InverseTransformPoint(t.TransformPoint(c + e));
            float half = box.size.z * 0.5f * t.lossyScale.z / Mathf.Max(1e-4f, transform.lossyScale.x);
            Lows.Add(new LowObstacle { A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z), Half = half, Height = LogVaultHeight });
        }
        /// <summary>
        /// A fallen tree's root mound on the ground plane (the level's units): a thin ellipse centred on
        /// <see cref="C"/>, <see cref="Thin"/> along the trunk (<see cref="Along"/>) and <see cref="Wide"/> across it.
        /// </summary>
        public struct Mound
        {
            public Vector2 C, Along;
            public float Thin, Wide;

            /// <summary>How far out of the ellipse (grown by <paramref name="pad"/>) a point is: below 1 inside.</summary>
            public float Reach(Vector2 p, float pad)
            {
                Vector2 d = p - C, across = new Vector2(-Along.y, Along.x);
                float u = Vector2.Dot(d, Along) / (Thin + pad), v = Vector2.Dot(d, across) / (Wide + pad);
                return Mathf.Sqrt(u * u + v * v);
            }
        }

        /// <summary>
        /// The plate's footprint, as multiples of the tree's size: DeadTrees.Fallen stands a disc of soil and roots on edge
        /// at the trunk's base, 0.22 thick either side of -0.15 along the trunk and 1.15 across; where it meets the ground
        /// it's a thin ellipse a little wider than the disc is thick (the dirt banked at its foot).
        /// </summary>
        const float MoundAt = -0.15f, MoundThin = 0.27f, MoundWide = 1.1f, MoundTop = 2.4f, MoundBelow = 1f;

        static Mesh moundPrism;

        /// <summary>
        /// A rock's solid body: an upright round wall about it (its own collider, a sphere or squat capsule tilted with the
        /// slope, let a body walk up its curve onto the top, where the eye ended up inside the rock's outline and saw
        /// nothing else). The wall stands straight up from under the ground to above the rock's top, so there's no
        /// slope to climb and no edge to step onto; sight still stops at the rock's own outline.
        /// </summary>
        void RockWall(GameObject rock, Vector2 p, float radius)
        {
            foreach (Collider c in rock.GetComponents<Collider>()) c.enabled = false;
            Transform parent = rock.transform.parent != null ? rock.transform.parent : staticRoot;
            var wall = new GameObject("Rock Wall");
            wall.SetActive(false);
            wall.transform.SetParent(parent, false);
            const float below = 1f;
            float r = radius * 0.85f, top = Mathf.Max(2.6f, radius * 2.2f);
            wall.transform.position = transform.TransformPoint(new Vector3(p.x, H(p) - below, p.y));
            wall.transform.rotation = transform.rotation;
            float k = transform.lossyScale.x / Mathf.Max(1e-4f, parent.lossyScale.x);
            wall.transform.localScale = new Vector3(r, below + top, r) * k;
            wall.isStatic = rock.isStatic;
            var col = wall.AddComponent<MeshCollider>();
            col.sharedMesh = MoundPrism();
            col.convex = true;
            wall.SetActive(true);
        }

        /// <summary>An upright elliptic prism of unit radius, from the ground (y 0) up to y 1 (scaled to each mound).</summary>
        static Mesh MoundPrism()
        {
            if (moundPrism != null) return moundPrism;
            const int n = 16;
            var v = new Vector3[n * 2];
            var tris = new List<int>();
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                v[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v[i + n] = new Vector3(Mathf.Cos(a), 1f, Mathf.Sin(a));
                int j = (i + 1) % n;
                tris.AddRange(new[] { i, j, i + n, j, j + n, i + n });
            }
            for (int i = 1; i < n - 1; i++)
            {
                tris.AddRange(new[] { 0, i + 1, i });
                tris.AddRange(new[] { n, n + i, n + i + 1 });
            }
            moundPrism = new Mesh { name = "Root Mound Prism", vertices = v, triangles = tris.ToArray(), hideFlags = HideFlags.DontSave };
            moundPrism.RecalculateBounds();
            return moundPrism;
        }

        /// <summary>
        /// The torn-up root plate at a fallen tree's base: a solid wall that players, NPCs, thrown items and shots all run
        /// into and nobody vaults (unlike the log itself, which stays low: vaulted, seen and shot over). Its footprint is
        /// only where the disc meets the ground: a thin ellipse across the trunk's end. It stands straight up whatever the
        /// slope (not tilted with the tree, whose lean would turn a wall's side into a ramp the player's body walks up and
        /// over), with walls running from well under the ground to above the plate's top so there's no edge to step onto.
        /// Every vault strip is cut back out of it (<see cref="TrimLowsAtMounds"/>).
        /// </summary>
        void RootMound(GameObject tree, float s)
        {
            Transform t = tree.transform;
            float girth = t.localScale.x, tall = t.localScale.y;
            Vector3 baseW = t.TransformPoint(new Vector3(MoundAt * s, 0f, 0f));
            Vector3 l = transform.InverseTransformPoint(baseW);
            var c = new Vector2(l.x, l.z);
            Vector3 alongL = transform.InverseTransformDirection(t.right);
            var along = new Vector2(alongL.x, alongL.z);
            along = along.sqrMagnitude > 1e-6f ? along.normalized : Vector2.right;
            float thin = MoundThin * s * girth, wide = MoundWide * s * girth, below = MoundBelow * s, top = MoundTop * s * tall;

            var mound = new GameObject("Root Mound");
            mound.SetActive(false);
            Transform parent = t.parent != null ? t.parent : staticRoot;
            mound.transform.SetParent(parent, false);
            mound.transform.position = transform.TransformPoint(new Vector3(c.x, H(c) - below, c.y));
            mound.transform.rotation = transform.rotation * Quaternion.Euler(0f, -Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg, 0f);
            float k = transform.lossyScale.x / Mathf.Max(1e-4f, parent.lossyScale.x);
            mound.transform.localScale = new Vector3(thin, below + top, wide) * k;
            mound.isStatic = tree.isStatic;
            var col = mound.AddComponent<MeshCollider>();
            col.sharedMesh = MoundPrism();
            col.convex = true;
            var occ = mound.AddComponent<Vision.Visibility.Occluder>();
            occ.shape = Vision.Visibility.Occluder.Shape.Circle;
            occ.radius = 1f;
            occ.sides = 16;
            mound.SetActive(true);
            RootMounds.Add(new Mound { C = c, Along = along, Thin = thin, Wide = wide });
        }

        /// <summary>
        /// Cuts every vault strip (its own log's, and any other log or fallen tree lying into it) back out of every root
        /// mound, so nothing is ever vaulted into or over a mound. A strip running through a mound keeps its longer side.
        /// </summary>
        void TrimLowsAtMounds()
        {
            const float pad = 0.05f;
            foreach (Mound m in RootMounds)
            {
                Vector2 across = new Vector2(-m.Along.y, m.Along.x);
                // In the ellipse's own frame, scaled to a unit circle (lines stay lines, the same parts along them).
                Vector2 Unit(Vector2 p) => new Vector2(Vector2.Dot(p - m.C, m.Along) / (m.Thin + pad), Vector2.Dot(p - m.C, across) / (m.Wide + pad));
                for (int i = 0; i < Lows.Count; i++)
                {
                    LowObstacle o = Lows[i];
                    Vector2 a = Unit(o.A), b = Unit(o.B), ab = b - a;
                    float len2 = ab.sqrMagnitude;
                    if (len2 < 1e-10f) continue;
                    // Where the strip enters and leaves the ellipse (as parts of the strip, 0 at A to 1 at B).
                    float bq = Vector2.Dot(a, ab) / len2, cq = (a.sqrMagnitude - 1f) / len2, disc = bq * bq - cq;
                    if (disc <= 0f) continue;
                    float root = Mathf.Sqrt(disc);
                    float t0 = -bq - root, t1 = -bq + root;
                    if (t1 <= 0f || t0 >= 1f) continue;
                    t0 = Mathf.Clamp01(t0);
                    t1 = Mathf.Clamp01(t1);
                    // Keep whichever side lies outside and is longer; a strip wholly inside shrinks to a point at its end.
                    Vector2 AB = o.B - o.A;
                    if (t0 >= 1f - t1)
                    {
                        if (t0 > 1e-4f) o.B = o.A + AB * t0;
                        else o.A = o.B = m.C + (o.B - m.C).normalized * (m.Wide + pad);
                    }
                    else o.A = o.A + AB * t1;
                    Lows[i] = o;
                }
            }
        }

        /// <summary>The fallen trees' root mounds (thin ellipses on the ground, the level's units): solid, never vaulted.</summary>
        public readonly List<Mound> RootMounds = new List<Mound>();
        /// <summary>Which photo each of <see cref="Notes"/> shows (0-based, note-1 to note-6).</summary>
        public readonly List<int> NoteImages = new List<int>();

        /// <summary>For the painted map: each tree's spot, its crown radius and whether it is dead; each rock's spot and radius.</summary>
        public readonly List<(Vector2 p, float r, bool dead)> MapTrees = new List<(Vector2, float, bool)>();
        public readonly List<(Vector2 p, float r)> MapRocks = new List<(Vector2, float)>();
        /// <summary>Raised after <see cref="Generate"/> builds a level.</summary>
        public static event System.Action<SandboxWorld> Built;

        /// <summary>How a prefab becomes a scene object. Null means Object.Instantiate.</summary>
        public System.Func<GameObject, Transform, GameObject> placeHook;

        System.Random rng;
        Transform staticRoot, entityRoot;
        readonly PointGrid blocked = new PointGrid(4f);

        /// <summary>Where everything is on this map.</summary>
        public MapLayout Layout { get; private set; }

        /// <summary>The ground height of this level (design units, local space).</summary>
        public TerrainField Terrain { get; private set; }

        /// <summary>The player's start, design units.</summary>
        public Vector3 playerSpawn => Layout != null ? new Vector3(Layout.Spawn.x, 0f, Layout.Spawn.y) : Vector3.zero;

        /// <summary>Raised after every (re)generation.</summary>
        public event System.Action Generated;

        void Awake()
        {
            if (generateOnAwake) Generate();
        }

        void OnEnable()
        {
            if (Terrain == null)
            {
                Layout ??= new MapLayout(seed, halfExtent);
                Terrain = CreateTerrain(Layout);
            }
            TerrainField.SetActive(Terrain, transform);
            HumanoidAnimator.Ground = TerrainField.TrySample;
        }

        void OnDisable()
        {
            TerrainField.ClearActive(transform);
            if (TerrainField.Active == null) HumanoidAnimator.Ground = null;
        }

        /// <summary>Builds a fresh map from a new seed (random when none is given).</summary>
        public void Regenerate(int? newSeed = null)
        {
            seed = newSeed ?? new System.Random().Next(1, 1000000);
            Generate();
        }

        /// <summary>The upright rectangle round <paramref name="r"/> turned by <paramref name="yaw"/> degrees about its centre.</summary>
        static Rect Turned(Rect r, float yaw)
        {
            float a = yaw * Mathf.Deg2Rad, c = Mathf.Abs(Mathf.Cos(a)), s = Mathf.Abs(Mathf.Sin(a));
            float w = r.width * c + r.height * s, h = r.width * s + r.height * c;
            return new Rect(r.center.x - w * 0.5f, r.center.y - h * 0.5f, w, h);
        }

        /// <summary>
        /// Hills and ditches with flat ground under the building and its yard, the cabins, the clearings, the cover
        /// pieces and the special sites, the lake's basin, and footpaths routed between everything.
        /// </summary>
        public TerrainField CreateTerrain(MapLayout layout)
        {
            var f = new TerrainField(seed, WorldRadius);
            f.AddPad(Expand(layout.Building, 1.5f), 6f);
            f.AddPad(Expand(layout.Yard, 1f), 4f);
            foreach (MapLayout.Cabin c in layout.Cabins) f.AddPad(Expand(c.Area, 1.2f), 5f);
            for (int i = 0; i < layout.Clearings.Count; i++)
                f.AddPad(layout.Clearings[i].Centre, layout.Clearings[i].Radius * 0.45f, layout.Clearings[i].Radius * 0.6f);
            foreach (MapLayout.Kit k in layout.Kits) f.AddPad(k.Centre, 3.5f, 3f);
            foreach (Vector2 g in layout.WoodsGenerators) f.AddPad(g, 1.4f, 3f);
            // The graveyard is turned (a quarter turn or more, and a little), so its flat pad covers the whole turned plot:
            // the plain rectangle left its long sides and corners out on the hills, the fence sunk into them.
            f.AddPad(Expand(Turned(layout.Graveyard, layout.GraveyardYaw), 1f), 4f);
            f.AddPad(Expand(layout.Playground, 1f), 4f);
            f.AddPad(Expand(layout.Sushi, 1.5f), 4f);
            f.AddPad(Expand(layout.Chamber, 1.5f), 4f);
            f.AddPad(layout.HangingTree, 2.5f, 3f);
            f.SetLake(layout);

            var building = Expand(layout.Building, 1.4f);   // paths keep a path-width and a bit from the walls, windows and all
            var yard = Expand(layout.Yard, 0.2f);
            var cabins = new List<Rect>();
            foreach (MapLayout.Cabin c in layout.Cabins) cabins.Add(Expand(c.Area, 0.4f));
            bool Blocked(Vector2 p)
            {
                // The paths keep to the playable circle.
                if (p.magnitude > halfExtent - 3f) return true;
                if ((building.Contains(p) && !(layout.Plan != null && layout.Plan.InNotch(p, 1.4f))) || yard.Contains(p) || layout.LakeDepth(p) > -1.5f) return true;
                foreach (Rect r in cabins) if (r.Contains(p)) return true;
                if (layout.Graveyard.Contains(p) || layout.Playground.Contains(p)) return true;
                foreach (Vector2 pole in layout.PowerPoles) if ((pole - p).sqrMagnitude < 1.6f * 1.6f) return true;
                foreach (Vector2 g in layout.WoodsGenerators) if ((g - p).sqrMagnitude < 2.2f * 2.2f) return true;
                foreach (MapLayout.Kit k in layout.Kits) if ((k.Centre - p).sqrMagnitude < 3.2f * 3.2f) return true;
                foreach (MapLayout.Segment fence in layout.Fences)
                {
                    Vector2 ab = fence.B - fence.A;
                    float t = Mathf.Clamp01(Vector2.Dot(p - fence.A, ab) / ab.sqrMagnitude);
                    if (t > fence.GapStart && t < fence.GapEnd) continue;   // through the gap
                    if ((fence.A + ab * t - p).sqrMagnitude < 0.8f * 0.8f) return true;
                }
                // The logs lying in the clearings.
                foreach (MapLayout.Segment log in layout.Logs)
                {
                    Vector2 ab = log.B - log.A;
                    float t = Mathf.Clamp01(Vector2.Dot(p - log.A, ab) / ab.sqrMagnitude);
                    if ((log.A + ab * t - p).sqrMagnitude < 1.6f * 1.6f) return true;
                }
                return false;
            }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            // The original's paths (a spanning tree with a few loops) wandering through the woods, and a web of trails.
            var net = PathNetwork.Build(layout.PathPoints(), f.Height, halfExtent - 3f, Blocked, 1f, new PathNetwork.Web { Seed = unchecked(seed * 7919 + 61) });
            net.HalfWidth = 70f * MapLayout.Unit * 0.5f;
            f.SetPaths(net);
            long tPaths = sw.ElapsedMilliseconds;
            f.Bake();
            TerrainReport = $"paths {tPaths} ms, bake {sw.ElapsedMilliseconds - tPaths} ms";
            return f;
        }

        static Rect Expand(Rect r, float by) => new Rect(r.x - by, r.y - by, r.width + 2f * by, r.height + 2f * by);

        /// <summary>Clears and rebuilds the whole level under this object.</summary>
        [ContextMenu("Generate Level")]
        public void Generate()
        {
            Lows.Clear();
            RootMounds.Clear();
            Physics.IgnoreLayerCollision(CharacterLayer, LowLayer, true);
            Physics.IgnoreLayerCollision(CharacterLayer, VaultLayer, true);
            Physics.IgnoreLayerCollision(CharacterLayer, GraveFenceLayer, false);
            rng = new System.Random(seed);
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) { child.SetActive(false); Destroy(child); }
                else DestroyImmediate(child);
            }
            Doors.Clear();
            HidingSpots.Clear();
            BarricadeList.Clear();
            Windows.Clear();
            Stakes.Clear();
            Notes.Clear();
            NoteImages.Clear();
            Ambulance = null;
            Crows.Clear();
            Pickups.Clear();
            Places.Clear();
            Generators.Clear();
            MapTrees.Clear();
            MapRocks.Clear();
            blocked.Clear();
            Player = null;
            Wanderer = null;

            var timer = System.Diagnostics.Stopwatch.StartNew();
            var times = new System.Text.StringBuilder();
            void Step(string name, System.Action build)
            {
                long t0 = timer.ElapsedMilliseconds;
                build();
                times.Append($" {name} {timer.ElapsedMilliseconds - t0}");
            }
            Step("layout", () => Layout = new MapLayout(seed, halfExtent));
            Step("terrain", () => Terrain = CreateTerrain(Layout));
            TerrainField.SetActive(Terrain, transform);
            staticRoot = new GameObject("Static").transform;
            staticRoot.SetParent(transform, false);
            entityRoot = new GameObject("Entities").transform;
            entityRoot.SetParent(transform, false);

            Step("building", BuildBuilding);
            Step("cabins", BuildCabins);
            Step("kits", BuildKits);
            Step("fences", () => { BuildFences(); BuildLogs(); });
            Step("lake", BuildLake);
            Step("power", BuildPowerLine);
            Step("sites", () => { BuildGraveyard(); BuildPlayground(); BuildHangingTree(); BuildSushi(); BuildChamber(); });
            Step("lights", () => { BuildGenerators(); BuildLights(); });
            Step("stakes", () => { BuildStakes(); BuildAmbulance(); });
            Step("trees", BuildTrees);
            Step("rocks", BuildRocks);
            Step("supplies", () => { BuildPickups(); BuildNotes(); });
            Step("ground", BuildGround);   // last, so plants and grass grow around everything placed
            Step("entities", () => { BuildCrows(); BuildWanderer(); BuildPlayer(); });
            TrimLowsAtMounds();
            LastGenerationReport = $"seed {seed}: {timer.ElapsedMilliseconds} ms ({times.ToString().Trim()} ms; {TerrainReport}), {TreeCount} trees";
            Debug.Log($"[Vision] Generated {LastGenerationReport}");
            // The gas's noise volume takes a moment to make: do it with the level, not on the first cloud.
            if (Application.isPlaying) Vision.Effects.VolumetricGas.Prepare();
            Generated?.Invoke();
            Built?.Invoke(this);
        }

        // ---------------------------------------------------------------- land character (gradual, no borders)

        float Offset => (seed % 1000) * 0.37f;

        /// <summary>0 = living evergreens, 1 = dead trees.</summary>
        public float Deadness(float x, float z)
        {
            float r = Mathf.PerlinNoise(x * 0.03f + 17.3f + Offset, z * 0.03f + 4.1f);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.32f, r));
        }

        /// <summary>0 = open ground, 1 = thick woods: how densely trees grow.</summary>
        public float Woodland(float x, float z) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.68f, Mathf.PerlinNoise(x * 0.04f + 51.7f, z * 0.04f + 23.9f + Offset)));

        /// <summary>0 = dry, 1 = damp: grass, ferns and moss versus straw and dry scrub.</summary>
        public float Moisture(float x, float z) => Mathf.PerlinNoise(x * 0.05f + 7.1f, z * 0.05f + 2.3f);

        /// <summary>Somewhere a solid prop may stand: inside the walls, off the paths, the lake and every reserved site.</summary>
        bool Free(Vector2 p, float radius, float spacing)
        {
            // Anywhere in the world's circle: the woods go on into the dark past the playable ring.
            if (p.magnitude > WorldRadius - 1.5f - radius) return false;
            if (Layout.Blocked(p) || Layout.LakeDepth(p) > -2f - radius) return false;
            PathNetwork paths = Terrain.Paths;
            if (paths != null && Terrain.PathClearance(p.x, p.y) < radius + 0.4f) return false;
            return !blocked.AnyWithin(p, spacing + radius);
        }

        // ---------------------------------------------------------------- ground

        /// <summary>Side of a ground chunk in design units (one mesh and collider each, for culling and fast cooking).</summary>
        public const float ChunkSize = 16f;

        static class Ground
        {
            public static readonly Color LightEarth = new Color(0.47f, 0.40f, 0.31f);
            public static readonly Color DarkEarth = new Color(0.30f, 0.25f, 0.20f);
            public static readonly Color Clay = new Color(0.45f, 0.34f, 0.26f);
            public static readonly Color GreyEarth = new Color(0.39f, 0.36f, 0.32f);
            public static readonly Color Straw = new Color(0.47f, 0.44f, 0.33f);
            public static readonly Color Moss = new Color(0.33f, 0.35f, 0.26f);
            public static readonly Color Rocky = new Color(0.36f, 0.35f, 0.33f);
            public static readonly Color Mud = new Color(0.22f, 0.19f, 0.16f);
            public static readonly Color LakeBed = new Color(0.16f, 0.15f, 0.13f);
            public static readonly Color Path = new Color(0.46f, 0.38f, 0.28f);
            public static readonly Color Floor = new Color(0.33f, 0.25f, 0.18f);
            public static readonly Color Concrete = new Color(0.36f, 0.36f, 0.35f);
            public static readonly Color GraveGrass = new Color(0.27f, 0.30f, 0.23f);
        }

        bool InCabin(Vector2 p)
        {
            foreach (MapLayout.Cabin c in Layout.Cabins) if (c.Area.Contains(p)) return true;
            return false;
        }

        /// <summary>
        /// Ground colour: shades of earth blending slowly, a hint of moss where it is damp and straw where it is dry,
        /// trodden dirt in the clearings, rock grey on steep slopes, mud in the ditches and the lake bed, packed dirt
        /// on the paths, plank floors in the cabins, concrete in the building and its yard, darker grass in the graveyard.
        /// </summary>
        public Color GroundColor(float x, float z)
        {
            var p = new Vector2(x, z);
            if (InCabin(p)) return Ground.Floor * (Mathf.FloorToInt(z / 0.5f) % 2 == 0 ? 1f : 0.85f);
            if (Layout.Building.Contains(p) || Layout.Yard.Contains(p)) return Ground.Concrete * (0.94f + 0.08f * Mathf.PerlinNoise(x * 0.6f, z * 0.6f));
            float f1 = Mathf.PerlinNoise(x * 0.045f + 3.3f, z * 0.045f + 9.1f);
            float f2 = Mathf.PerlinNoise(x * 0.12f + 11f, z * 0.12f + 5f);
            float f3 = Mathf.PerlinNoise(x * 0.022f + 31f, z * 0.022f + 2f);
            float n3 = Mathf.PerlinNoise(x * 0.7f + 3f, z * 0.7f + 9f);
            float moist = Moisture(x, z), dead = Deadness(x, z);
            Color c = Color.Lerp(Ground.LightEarth, Ground.DarkEarth, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.75f, f1)));
            c = Color.Lerp(c, Ground.Clay, 0.55f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.8f, f3)));
            c = Color.Lerp(c, Ground.GreyEarth, 0.45f * dead);
            c = Color.Lerp(c, Ground.Moss, 0.4f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.8f, moist)) * (1f - dead));
            c = Color.Lerp(c, Ground.Straw, 0.35f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.25f, moist)));
            c = Color.Lerp(c, Ground.DarkEarth * 0.9f, 0.3f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.8f, f2)));
            foreach (MapLayout.Clearing cl in Layout.Clearings)
            {
                float d = (cl.Centre - p).magnitude / (cl.Radius * 0.75f);
                if (d < 1.4f) c = Color.Lerp(c, Ground.Path * 0.95f, 0.6f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.4f, 0.8f, d + (n3 - 0.5f) * 0.3f)));
            }
            if (Expand(Layout.Graveyard, 0.5f).Contains(p)) c = Color.Lerp(c, Ground.GraveGrass, 0.7f);
            c = Color.Lerp(c, Ground.Rocky, 0.8f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(27f, 35f, Terrain.SlopeDeg(x, z))));
            c = Color.Lerp(c, Ground.Mud, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1.1f, -1.8f, Terrain.Natural(x, z))));
            float lake = Layout.LakeDepth(p);
            if (lake > -3f) c = Color.Lerp(c, lake > 0f ? Ground.LakeBed : Ground.Mud, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-3f, 0.5f, lake)));
            if (Terrain.Paths != null && Terrain.PathAt(x, z, out int pi, out float along, out float pd, out float hw))
            {
                float edge = hw + 0.12f + (n3 - 0.5f) * 0.45f;
                float on = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge + 0.3f, edge - 0.25f, pd));
                if (on > 0f) c = Color.Lerp(c, PathColor(Terrain.Paths.Infos[pi], x, z, along, Mathf.Clamp01(pd / Mathf.Max(0.1f, hw)), c), on);
            }
            return c;
        }

        static readonly Color GravelA = new Color(0.47f, 0.45f, 0.41f), GravelB = new Color(0.36f, 0.35f, 0.33f);
        static readonly Color LeafA = new Color(0.46f, 0.27f, 0.12f), LeafB = new Color(0.55f, 0.40f, 0.16f), LeafDark = new Color(0.30f, 0.21f, 0.13f);
        static readonly Color ClayPath = new Color(0.55f, 0.40f, 0.29f), MudPath = new Color(0.27f, 0.22f, 0.17f), Puddle = new Color(0.14f, 0.15f, 0.17f);

        /// <summary>
        /// A path's surface at a point (u: 0 on the centre line, 1 at the edge): its look, blending slowly into its second
        /// look and back along its length; worn lighter down the middle where feet go, darker at the edges where it meets the
        /// ground; two faint ruts on the wider paths; damp, darker patches and the odd puddle; and the look's own grain (gravel
        /// specks, drifts of fallen leaves, moss creeping in from the edges, the clay's warm streaks, mud's wet tracks).
        /// </summary>
        Color PathColor(PathNetwork.Info info, float x, float z, float along, float u, Color ground)
        {
            Color Look(PathNetwork.Style s)
            {
                float fine = Mathf.PerlinNoise(x * 2.7f + 13f, z * 2.7f + 7f), mid = Mathf.PerlinNoise(x * 0.85f + info.Seed, z * 0.85f + 2f);
                switch (s)
                {
                    case PathNetwork.Style.Gravel:
                        return Color.Lerp(GravelA, GravelB, Mathf.SmoothStep(0f, 1f, fine)) * (0.95f + 0.1f * mid);
                    case PathNetwork.Style.Leafy:
                    {
                        Color earth = Color.Lerp(LeafDark, Ground.Path * 0.9f, 0.5f);
                        float drift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.7f, mid + u * 0.25f));
                        return Color.Lerp(earth, Color.Lerp(LeafA, LeafB, fine), drift);
                    }
                    case PathNetwork.Style.Mossy:
                    {
                        float moss = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.85f, mid * 0.6f + u * 0.6f));
                        return Color.Lerp(Ground.Path * 0.92f, Color.Lerp(Ground.Moss, new Color(0.30f, 0.38f, 0.22f), fine), moss);
                    }
                    case PathNetwork.Style.Clay:
                        return Color.Lerp(ClayPath, ClayPath * 0.86f, Mathf.SmoothStep(0f, 1f, Mathf.PerlinNoise(along * 0.6f + info.Seed, u * 3f)));
                    case PathNetwork.Style.Mud:
                    {
                        // Wet tracks along its length.
                        float track = Mathf.Exp(-Mathf.Pow((u - 0.45f) / 0.16f, 2f));
                        return Color.Lerp(MudPath * 1.15f, MudPath * 0.8f, track * (0.6f + 0.4f * fine));
                    }
                    default:
                        return Ground.Path * (0.9f + 0.14f * fine);
                }
            }
            // The look drifts into the path's second look and back, slowly, along its length.
            float mix = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.75f, Mathf.PerlinNoise(along * (info.Main ? 0.025f : 0.05f) + info.Seed, info.Seed * 0.3f)));
            Color c = Color.Lerp(Look(info.Look), Look(info.Second), mix);
            // Worn lighter down the middle, darker toward the edge.
            c *= 1.06f - 0.12f * u * u;
            // Two faint ruts on the main paths.
            if (info.Main) c *= 1f - 0.08f * Mathf.Exp(-Mathf.Pow((u - 0.42f) / 0.11f, 2f));
            // Damp patches, and the odd puddle in the lowest of them.
            float damp = Mathf.PerlinNoise(along * 0.21f + info.Seed * 2f, u * 1.5f + info.Seed);
            c = Color.Lerp(c, c * 0.78f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.8f, damp)));
            if (info.Look == PathNetwork.Style.Mud || info.Second == PathNetwork.Style.Mud || info.Main)
                c = Color.Lerp(c, Puddle, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 0.9f, damp)) * (1f - u));
            // The edge frays into the ground around it.
            return Color.Lerp(c, ground, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1.05f, u)) * 0.35f);
        }

        void BuildGround()
        {
            float e = WorldRadius + 3f;
            var cabinZones = new List<Rect>();
            foreach (MapLayout.Cabin c in Layout.Cabins) cabinZones.Add(Expand(c.Area, 0.5f));
            Rect buildingZone = Expand(Layout.Building, 0.5f), yardZone = Layout.Yard;
            bool Flat(Vector2 p)
            {
                if (buildingZone.Contains(p) || yardZone.Contains(p)) return true;
                foreach (Rect r in cabinZones) if (r.Contains(p)) return true;
                return false;
            }

            // Cell size from the shared polygon budget (the player's facet size, relaxed for the ground).
            var grid = new LowPolyMeshBuilder.TerrainGrid(e, PolyBudget.Edge(PolyBudget.Class.Ground),
                (x, z) => Terrain.Height(x, z), (x, z) => Flat(new Vector2(x, z)) ? 0f : 1f, 0.28f, seed);
            Color GroundColorVaried(float x, float z) => Vary(GroundColor(x, z), Flat(new Vector2(x, z)) ? 0.03f : 0.05f);
            // Kept: markings on the ground (the indicator rings) sit on these very facets.
            surface = grid;
            hasSurface = true;

            int perChunk = Mathf.Max(1, Mathf.RoundToInt(ChunkSize / grid.Step));
            int chunks = Mathf.CeilToInt(grid.Cells / (float)perChunk);
            var builders = new LowPolyMeshBuilder[chunks, chunks];
            for (int cj = 0; cj < chunks; cj++)
                for (int ci = 0; ci < chunks; ci++)
                {
                    builders[ci, cj] = new LowPolyMeshBuilder(rng);
                    // The ground ends with the world's circle.
                    builders[ci, cj].AddTerrainPatch(grid, ci * perChunk, Mathf.Min(grid.Cells, (ci + 1) * perChunk),
                        cj * perChunk, Mathf.Min(grid.Cells, (cj + 1) * perChunk), GroundColorVaried, c => c.x * c.x + c.z * c.z <= (WorldRadius + 1f) * (WorldRadius + 1f));
                }
            LowPolyMeshBuilder ChunkAt(Vector2 p)
            {
                int ci = Mathf.Clamp(Mathf.FloorToInt((p.x + e) / grid.Step / perChunk), 0, chunks - 1);
                int cj = Mathf.Clamp(Mathf.FloorToInt((p.y + e) / grid.Step / perChunk), 0, chunks - 1);
                return builders[ci, cj];
            }
            PathNetwork paths = Terrain.Paths;
            float OnPath(Vector2 p) => paths != null ? Terrain.PathClearance(p.x, p.y) : float.MaxValue;
            bool Bare(Vector2 p) => Flat(p) || Layout.LakeDepth(p) > -0.3f || Layout.Playground.Contains(p);
            float lim = WorldRadius - 0.8f;

            // Short grass blades (dead or green with the ground), pebbles and bone-pale debris, over the world's circle.
            int total = Mathf.RoundToInt(700f / 1600f * Mathf.PI * WorldRadius * WorldRadius);
            for (int i = 0; i < total; i++)
            {
                var p = new Vector2(Range(-lim, lim), Range(-lim, lim));
                if (p.magnitude > lim || Bare(p) || OnPath(p) < -0.2f) continue;
                LowPolyMeshBuilder b = ChunkAt(p);
                Color grass = Color.Lerp(GroundColor(p.x, p.y), new Color(0.36f, 0.36f, 0.26f), 0.5f) * 1.1f;
                int blades = 2 + rng.Next(4);
                for (int k = 0; k < blades; k++)
                {
                    float h = Range(0.15f, 0.38f);
                    float rx = p.x + Range(-0.15f, 0.15f), rz = p.y + Range(-0.15f, 0.15f);
                    var root = new Vector3(rx, Terrain.Height(rx, rz) - 0.01f, rz);
                    var tip = root + new Vector3(Range(-0.08f, 0.08f), h, Range(-0.08f, 0.08f));
                    b.AddCone(root, tip, 0.03f, 3, Vary(grass, 0.2f), 0.05f, false);
                }
            }
            for (int i = 0; i < total * 0.37f; i++)
            {
                var p = new Vector2(Range(-lim, lim), Range(-lim, lim));
                if (Flat(p)) continue;
                LowPolyMeshBuilder b = ChunkAt(p);
                float s = Range(0.05f, 0.14f);
                bool bone = rng.NextDouble() < 0.2;
                Vector3 radii = bone ? new Vector3(s * 1.8f, s * 0.4f, s * 0.5f) : new Vector3(s, s * 0.6f, s);
                Color c = bone ? new Color(0.72f, 0.69f, 0.62f) : LowPolyModels.Palette.Stone;
                b.AddBlob(new Vector3(p.x, Terrain.Height(p.x, p.y), p.y), radii, 0, 0.2f, _ => b.Jitter(c, 0.2f), true, Quaternion.Euler(0f, Range(0f, 180f), 0f));
            }

            // Litter along the paths, by their look: pebbles, fallen leaves, twigs, moss, puddles, and stones by the edges.
            if (paths != null)
                for (int pi = 0; pi < paths.Paths.Count; pi++)
                {
                    List<Vector2> path = paths.Paths[pi];
                    PathNetwork.Info info = paths.Infos[pi];
                    float along = 0f, next = Range(0.2f, 0.6f);
                    for (int k = 0; k < path.Count - 1; k++)
                    {
                        Vector2 a = path[k], d = path[k + 1] - a;
                        float len = d.magnitude;
                        if (len < 1e-4f) continue;
                        d /= len;
                        var side = new Vector2(-d.y, d.x);
                        while (next <= along + len)
                        {
                            float t = (next - along) / len, s = next;
                            next += Range(0.3f, 0.75f);
                            float hw = paths.HalfWidthAt(pi, s);
                            Vector2 p = a + d * (t * len);
                            if (Bare(p)) continue;
                            float mix = Mathf.PerlinNoise(s * (info.Main ? 0.025f : 0.05f) + info.Seed, info.Seed * 0.3f);
                            PathNetwork.Style look = mix > 0.57f ? info.Second : info.Look;
                            float roll = (float)rng.NextDouble();
                            LowPolyMeshBuilder b = ChunkAt(p);
                            Vector3 At(Vector2 q, float lift = 0f) => new Vector3(q.x, Terrain.Height(q.x, q.y) + lift, q.y);
                            if (info.Main && roll < 0.1f)
                            {
                                // A stone at the edge.
                                Vector2 q = p + side * ((rng.Next(2) == 0 ? 1f : -1f) * hw * Range(0.95f, 1.2f));
                                float r = Range(0.08f, 0.18f);
                                b.AddBlob(At(q), new Vector3(r * 1.3f, r * 0.6f, r), 0, 0.2f, _ => b.Jitter(LowPolyModels.Palette.Stone * 0.95f, 0.15f), true, Quaternion.Euler(0f, Range(0f, 180f), 0f));
                                continue;
                            }
                            Vector2 o = p + side * (Range(-0.85f, 0.85f) * hw);
                            switch (look)
                            {
                                case PathNetwork.Style.Gravel:
                                    for (int g = 0; g < 3; g++)
                                    {
                                        Vector2 q = o + new Vector2(Range(-0.2f, 0.2f), Range(-0.2f, 0.2f));
                                        float r = Range(0.025f, 0.05f);
                                        b.AddBlob(At(q), new Vector3(r, r * 0.6f, r), 0, 0.25f, _ => b.Jitter(Color.Lerp(GravelA, GravelB, (float)rng.NextDouble()), 0.12f), true);
                                    }
                                    break;
                                case PathNetwork.Style.Leafy:
                                    for (int g = 0; g < 3; g++)
                                    {
                                        Vector2 q = o + new Vector2(Range(-0.25f, 0.25f), Range(-0.25f, 0.25f));
                                        float ang = Range(0f, Mathf.PI * 2f), r = Range(0.06f, 0.1f);
                                        var f = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                                        var sd = new Vector3(-f.z, 0f, f.x) * 0.55f;
                                        Vector3 c0 = At(q, 0.012f);
                                        Color leaf = b.Jitter(Color.Lerp(LeafA, LeafB, (float)rng.NextDouble()), 0.15f);
                                        b.AddDoubleSided(c0 - f, c0 + sd, c0 + f, leaf);
                                        b.AddDoubleSided(c0 - f, c0 + f, c0 - sd, leaf * 0.9f);
                                    }
                                    break;
                                case PathNetwork.Style.Mossy:
                                    if (roll < 0.6f)
                                    {
                                        float r = Range(0.07f, 0.13f);
                                        b.AddBlob(At(o, -0.01f), new Vector3(r, r * 0.35f, r * 0.9f), 0, 0.3f, _ => b.Jitter(new Color(0.30f, 0.38f, 0.21f), 0.12f), true);
                                    }
                                    break;
                                case PathNetwork.Style.Mud:
                                    if (roll < 0.35f)
                                    {
                                        // A puddle: a flat dark pool lying in the ruts.
                                        float r = Range(0.18f, 0.4f);
                                        Vector3 c0 = At(o, 0.015f);
                                        int sides = 7;
                                        Color water = b.Jitter(Puddle, 0.05f);
                                        for (int ei = 0; ei < sides; ei++)
                                        {
                                            float a0 = ei * Mathf.PI * 2f / sides, a1 = (ei + 1) * Mathf.PI * 2f / sides;
                                            float r0 = r * (0.8f + 0.2f * Mathf.Sin(a0 * 3f + s)), r1 = r * (0.8f + 0.2f * Mathf.Sin(a1 * 3f + s));
                                            b.AddDoubleSided(c0, c0 + new Vector3(Mathf.Cos(a0) * r0 * 1.5f, 0f, Mathf.Sin(a0) * r0), c0 + new Vector3(Mathf.Cos(a1) * r1 * 1.5f, 0f, Mathf.Sin(a1) * r1), water);
                                        }
                                    }
                                    break;
                                default:
                                    if (roll < 0.35f)
                                    {
                                        // A twig.
                                        float ang = Range(0f, Mathf.PI), l = Range(0.15f, 0.32f);
                                        var f = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * l * 0.5f;
                                        Vector3 c0 = At(o, 0.02f);
                                        b.AddTube(new[] { c0 - f, c0 + f }, new[] { 0.014f, 0.01f }, 3, b.Jitter(new Color(0.30f, 0.23f, 0.16f), 0.1f), 0.05f);
                                    }
                                    else if (roll < 0.7f)
                                    {
                                        float r = Range(0.03f, 0.06f);
                                        b.AddBlob(At(o), new Vector3(r, r * 0.6f, r), 0, 0.25f, _ => b.Jitter(LowPolyModels.Palette.Stone, 0.15f), true);
                                    }
                                    break;
                            }
                        }
                        along += len;
                    }
                }

            // Plant life: shrubs, ferns, tall grass, reeds in ditches and by the lake, dead shrubs, flowers, mushrooms.
            int plants = Mathf.RoundToInt(260f / 1600f * Mathf.PI * WorldRadius * WorldRadius);
            for (int i = 0; i < plants; i++)
            {
                var p = new Vector2(Range(-lim, lim), Range(-lim, lim));
                if (p.magnitude > lim || Bare(p) || OnPath(p) < 0.2f || blocked.AnyWithin(p, 0.7f)) continue;
                float dead = Deadness(p.x, p.y), moist = Moisture(p.x, p.y), wood = Woodland(p.x, p.y);
                bool wet = Terrain.Natural(p.x, p.y) < -1.3f || Layout.LakeDepth(p) > -4f;
                float roll = (float)rng.NextDouble();
                LowPolyMeshBuilder b = ChunkAt(p);
                Vector3 n = Terrain.Normal(p.x, p.y);
                b.Transform = Matrix4x4.TRS(new Vector3(p.x, Terrain.Height(p.x, p.y) - 0.02f, p.y),
                    Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, n, 0.5f)) * Quaternion.Euler(0f, Range(0f, 360f), 0f),
                    Vector3.one * Range(0.8f, 1.25f));
                if (wet && roll < 0.6f) LowPolyModels.AddReeds(b, Range(0.9f, 1.4f));
                else if (roll < 0.35f * dead) LowPolyModels.AddDeadShrub(b, Range(0.5f, 0.9f));
                else if (roll < 0.5f) LowPolyModels.AddTallGrass(b, Range(0.45f, 0.85f), (float)rng.NextDouble() > moist);
                else if (roll < 0.62f + 0.12f * wood) LowPolyModels.AddBush(b, Range(0.4f, 0.7f), moist < 0.35f || dead > 0.6f);
                else if (roll < 0.78f && moist > 0.45f) LowPolyModels.AddFern(b, Range(0.45f, 0.65f));
                else if (roll < 0.9f && dead < 0.5f) LowPolyModels.AddFlowers(b);
                else LowPolyModels.AddMushrooms(b);
                b.Transform = null;
            }

            // Tall-grass hiding patches: dense, head-high grass (a hiding spot each).
            Transform hiding = Group("Tall Grass");
            foreach (MapLayout.Clearing g in Layout.GrassPatches)
            {
                int clumps = Mathf.RoundToInt(g.Radius * g.Radius * 3.2f);
                for (int k = 0; k < clumps; k++)
                {
                    float a = Range(0f, Mathf.PI * 2f), r = g.Radius * Mathf.Sqrt((float)rng.NextDouble());
                    var p = g.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    LowPolyMeshBuilder b = ChunkAt(p);
                    b.Transform = Matrix4x4.TRS(new Vector3(p.x, Terrain.Height(p.x, p.y) - 0.02f, p.y), Quaternion.Euler(0f, Range(0f, 360f), 0f), Vector3.one * Range(0.9f, 1.2f));
                    LowPolyModels.AddTallGrass(b, Range(1.15f, 1.6f) * (1f - 0.35f * r / g.Radius), Deadness(p.x, p.y) > 0.5f);
                    b.Transform = null;
                }
                var spot = new GameObject("Tall Grass Patch");
                spot.transform.SetParent(hiding, false);
                spot.transform.localPosition = new Vector3(g.Centre.x, H(g.Centre), g.Centre.y);
                var hs = spot.AddComponent<HidingSpot>();
                hs.kind = HidingSpot.Kind.Grass;
                hs.reach = g.Radius;
                hs.exit = Vector3.zero;
                HidingSpots.Add(hs);
            }

            for (int cj = 0; cj < chunks; cj++)
                for (int ci = 0; ci < chunks; ci++)
                {
                    var go = MakeStatic("Ground", builders[ci, cj].ToMesh("Ground"), Vector3.zero, Quaternion.identity, GroundMaterial);
                    go.layer = GroundLayer;
                    go.AddComponent<MeshCollider>().sharedMesh = grid.CollisionMesh(ci * perChunk, Mathf.Min(grid.Cells, (ci + 1) * perChunk),
                        cj * perChunk, Mathf.Min(grid.Cells, (cj + 1) * perChunk), "Ground Collider");
                    go.SetActive(true);
                }
        }

        // ---------------------------------------------------------------- placement on the terrain

        float H(Vector2 p) => Terrain.Height(p.x, p.y);

        /// <summary>Upright on the ground (trees, posts, walls): never tilted, sunk to the lowest point under the footprint.</summary>
        Vector3 Upright(Vector2 p, float radius)
        {
            float y = H(p);
            if (radius > 0f)
                for (int k = 0; k < 6; k++)
                {
                    float a = k * Mathf.PI / 3f;
                    y = Mathf.Min(y, H(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius));
                }
            return new Vector3(p.x, y - 0.03f, p.y);
        }

        /// <summary>Resting on the slope (rocks, crates, wrecks): tilted most of the way to the ground's average normal and sunk a little.</summary>
        void Conform(Transform t, Vector2 p, float radius, float yaw, float follow = 0.75f, float sink = 0.06f)
        {
            Vector3 n = Terrain.Normal(p.x, p.y);
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f + 0.4f;
                Vector2 q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * 0.7f;
                n += Terrain.Normal(q.x, q.y);
            }
            Quaternion tilt = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, n.normalized, follow));
            t.SetLocalPositionAndRotation(new Vector3(p.x, H(p) - sink * Mathf.Max(radius, 0.3f), p.y), tilt * Quaternion.Euler(0f, yaw, 0f));
        }

        Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(staticRoot, false);
            return t;
        }

        /// <summary>A plain static mesh object (no collider), placed upright at a point with a yaw.</summary>
        GameObject Piece(string name, Mesh mesh, Transform parent, Vector3 pos, float yaw, Material mat = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            PropFactory.NoShadows(go.AddComponent<MeshRenderer>()).sharedMaterial = mat != null ? mat : lowPolyMaterial;
            return go;
        }

        static BoxCollider AddBox(GameObject go, Vector3 centre, Vector3 size)
        {
            var c = go.AddComponent<BoxCollider>();
            c.center = centre;
            c.size = size;
            return c;
        }

        // ---------------------------------------------------------------- walls

        void StoneWall(Vector2 a, Vector2 b)
        {
            Mesh m = LowPolyModels.StoneWall(rng, Vector2.Distance(a, b), 1.2f, 0.6f);
            Wall("Stone Wall", a, b, 1.2f, 0.6f, m, true, true);
        }

        void PlankWall(Vector2 a, Vector2 b, float height = 2.4f, bool occludes = true)
        {
            if (Vector2.Distance(a, b) < 0.05f) return;
            Mesh m = LowPolyModels.PlankWall(rng, Vector2.Distance(a, b), height, 0.3f);
            Wall("Plank Wall", a, b, height, 0.3f, m, occludes, true);
        }

        GameObject Wall(string name, Vector2 a, Vector2 b, float height, float thickness, Mesh m, bool occludes, bool collides, float baseY = float.NaN)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            Vector2 mid = (a + b) * 0.5f;
            Quaternion rot = Quaternion.Euler(0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 0f);
            if (float.IsNaN(baseY)) baseY = Mathf.Min(H(a), Mathf.Min(H(mid), H(b))) - 0.03f;
            GameObject go = MakeStatic(name, m, new Vector3(mid.x, baseY, mid.y), rot, lowPolyMaterial);
            if (collides) AddBox(go, new Vector3(0f, height * 0.5f, 0f), new Vector3(length, height, thickness));
            if (occludes)
            {
                var occ = go.AddComponent<Occluder>();
                occ.shape = Occluder.Shape.Box;
                occ.size = new Vector2(length, thickness);
            }
            go.SetActive(true);
            return go;
        }

        /// <summary>
        /// A hinged panel filling a gap that starts at <paramref name="start"/> and runs along
        /// <paramref name="along"/>. Shutters sit on the sill (raised) and never unblock movement.
        /// </summary>
        void MakeDoor(Vector2 start, Vector2 along, float width, float height, bool shutter, float baseY, bool office = false)
        {
            var root = new GameObject(shutter ? "Window Shutter" : "Door");
            root.SetActive(false);
            root.transform.SetParent(staticRoot, false);
            root.transform.SetLocalPositionAndRotation(new Vector3(start.x, baseY, start.y),
                Quaternion.Euler(0f, -Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg, 0f));

            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root.transform, false);
            // The central building's are office steel doors in steel frames; cabins keep their plank doors.
            Mesh m = office ? LowPolyModels.MetalDoor(rng, width - 0.04f, height - 0.03f, 0.06f) : LowPolyModels.Panel(rng, width, height, 0.12f);
            if (office)
            {
                var frame = new GameObject("Frame");
                frame.transform.SetParent(root.transform, false);
                frame.AddComponent<MeshFilter>().sharedMesh = LowPolyModels.MetalDoorFrame(rng, width, height, 0.24f);
                PropFactory.NoShadows(frame.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;
            }
            var panel = new GameObject("Panel");
            panel.transform.SetParent(hinge, false);
            panel.transform.localPosition = new Vector3(width * 0.5f, shutter ? 0.8f : 0f, 0f);
            panel.AddComponent<MeshFilter>().sharedMesh = m;
            PropFactory.NoShadows(panel.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;

            var blocker = AddBox(root, new Vector3(width * 0.5f, 1f, 0f), new Vector3(width, 2f, 0.2f));
            var occ = root.AddComponent<Occluder>();
            occ.shape = Occluder.Shape.Box;
            occ.size = new Vector2(width, 0.12f);
            occ.offset = new Vector2(width * 0.5f, 0f);

            var door = root.AddComponent<Door>();
            door.occluder = occ;
            door.hinge = hinge;
            door.blocker = blocker;
            door.blocksMovementWhenOpen = shutter;
            door.a = start;
            door.b = start + along * width;
            Doors.Add(door);
            root.SetActive(true);
        }

        /// <summary>Half the width of the graveyard's gateway (the level's units): two leaves, each this wide.</summary>
        public const float GateHalfWidth = 1.1f;
        /// <summary>How much of the fence beside each gate pillar is a plain wall, never climbed, even by Zach (the level's units).</summary>
        public const float GateFenceSolid = 2f;

        /// <summary>
        /// The graveyard's gates: two ornate iron leaves filling the gateway from <paramref name="start"/> along
        /// <paramref name="along"/>, hinged at either end and swinging in toward the graves, chained and padlocked at the start.
        /// Iron bars: seen and shot through, but nobody walks through them shut.
        /// </summary>
        void MakeGate(Transform parent, Vector2 start, Vector2 along, float width, float baseY)
        {
            var root = new GameObject("Graveyard Gates");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            root.transform.SetLocalPositionAndRotation(new Vector3(start.x, baseY, start.y), Quaternion.Euler(0f, -Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg, 0f));
            float leaf = width * 0.5f;
            Transform Leaf(string name, Vector3 at)
            {
                var h = new GameObject(name).transform;
                h.SetParent(root.transform, false);
                h.localPosition = at;
                var panel = new GameObject("Leaf");
                panel.transform.SetParent(h, false);
                panel.AddComponent<MeshFilter>().sharedMesh = LowPolyModels.GateLeaf(rng, leaf - 0.01f);
                PropFactory.NoShadows(panel.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;
                return h;
            }
            Transform h1 = Leaf("Hinge", Vector3.zero), h2 = Leaf("Hinge 2", new Vector3(width, 0f, 0f));
            h2.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var lockGo = new GameObject("Lock");
            lockGo.transform.SetParent(root.transform, false);
            lockGo.transform.localPosition = new Vector3(leaf, 0f, 0f);
            lockGo.AddComponent<MeshFilter>().sharedMesh = LowPolyModels.GateLock(rng);
            PropFactory.NoShadows(lockGo.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;

            var blocker = AddBox(root, new Vector3(leaf, 1.1f, 0f), new Vector3(width, 2.2f, 0.15f));
            var door = root.AddComponent<Door>();
            door.gate = true;
            door.hinge = h1;
            door.hinge2 = h2;
            door.lockVisual = lockGo;
            door.blocker = blocker;
            // In toward the graves.
            door.openAngle = -100f;
            door.swingSpeed = 160f;
            door.a = start;
            door.b = start + along * width;
            Doors.Add(door);
            root.SetActive(true);
        }

        /// <summary>A window in a wall from <paramref name="a"/> to <paramref name="b"/>: a sill wall, glass that blocks movement but not sight.</summary>
        void Window(Vector2 a, Vector2 b, float baseY)
        {
            float width = Vector2.Distance(a, b);
            Mesh sill = LowPolyModels.PlankWall(rng, width, 0.9f, 0.3f);
            GameObject sillGo = Wall("Window Sill", a, b, 0.9f, 0.3f, sill, false, true, baseY);
            Vector2 d = (b - a).normalized;
            GameObject pane = Piece("Window", LowPolyModels.WindowPane(width), staticRoot, new Vector3(a.x, baseY, a.y), -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            BoxCollider glass = AddBox(pane, new Vector3(width * 0.5f, 1.5f, 0f), new Vector3(width, 1.2f, 0.1f));
            RegisterWindow(a, b, pane, glass, sillGo.GetComponent<Collider>(), null, null);
        }

        // ---------------------------------------------------------------- cabins

        /// <summary>
        /// The three cabins: plank walls, a door on one side and a window in the middle of each other side, a wardrobe
        /// against the wall facing the door, a bed in a corner and a bare bulb.
        /// </summary>
        void BuildCabins()
        {
            const float doorW = 1.3f, winW = 1.4f;
            foreach (MapLayout.Cabin cabin in Layout.Cabins)
            {
                Rect r = cabin.Area;
                float y = H(r.center);
                var corners = new[] { new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMin) };
                for (int side = 0; side < 4; side++)
                {
                    Vector2 a = corners[side], b = corners[(side + 1) % 4];
                    Vector2 mid = (a + b) * 0.5f, u = (b - a).normalized;
                    float half = side == cabin.DoorSide ? doorW * 0.5f : winW * 0.5f;
                    PlankWallAt(a, mid - u * half, y);
                    PlankWallAt(mid + u * half, b, y);
                    if (side == cabin.DoorSide) MakeDoor(mid - u * half, u, doorW, 2.2f, false, y);
                    else Window(mid - u * half, mid + u * half, y);
                }
                // Inside: wardrobe against the wall facing the door, bed in a far corner, a bulb.
                Vector2 inward = cabin.DoorSide switch { 0 => Vector2.down, 1 => Vector2.left, 2 => Vector2.up, _ => Vector2.right };
                Vector2 wardrobe = r.center - inward * (cabin.DoorSide % 2 == 0 ? r.height : r.width) * 0.5f + inward * 0.45f + new Vector2(inward.y, -inward.x) * 1.4f;
                float wYaw = Mathf.Atan2(inward.x, inward.y) * Mathf.Rad2Deg;
                GameObject w = Piece("Wardrobe", LowPolyModels.Wardrobe(rng), staticRoot, new Vector3(wardrobe.x, y, wardrobe.y), wYaw);
                AddBox(w, new Vector3(0f, 0.95f, 0f), new Vector3(1f, 1.9f, 0.55f));
                AddHiding(w, HidingSpot.Kind.Wardrobe, 1.1f, new Vector3(0f, 0f, 0.9f));
                Vector2 bedAt = new Vector2(r.center.x + (inward.x >= 0f ? -1f : 1f) * (r.width * 0.5f - 0.7f), r.center.y + (inward.y >= 0f ? -1f : 1f) * (r.height * 0.5f - 1.15f));
                GameObject bed = Piece("Bed", LowPolyModels.Bed(rng), staticRoot, new Vector3(bedAt.x, y, bedAt.y), 0f);
                AddBox(bed, new Vector3(0f, 0.3f, 0f), new Vector3(1f, 0.6f, 2f));
                AddHiding(bed, HidingSpot.Kind.Bed, 1.3f, new Vector3(bedAt.x < r.center.x ? 0.9f : -0.9f, 0f, 0f));
                Vector2 tableAt = r.center + new Vector2(inward.y, -inward.x) * -1.2f;
                Piece("Table", LowPolyModels.Table(rng), staticRoot, new Vector3(tableAt.x, y, tableAt.y), Range(-10f, 10f));
                Piece("Chair", LowPolyModels.Chair(rng, rng.NextDouble() < 0.4), staticRoot, new Vector3(tableAt.x + 0.7f, y, tableAt.y), Range(0f, 360f));
                Lamp(new Vector3(r.center.x, y, r.center.y), 2.4f);
                blocked.Add(r.center);
            }
        }

        void PlankWallAt(Vector2 a, Vector2 b, float baseY)
        {
            if (Vector2.Distance(a, b) < 0.05f) return;
            Mesh m = LowPolyModels.PlankWall(rng, Vector2.Distance(a, b), 2.4f, 0.3f);
            Wall("Plank Wall", a, b, 2.4f, 0.3f, m, true, true, baseY);
        }

        void AddHiding(GameObject go, HidingSpot.Kind kind, float reach, Vector3 exit)
        {
            var hs = go.AddComponent<HidingSpot>();
            hs.kind = kind;
            hs.reach = reach;
            hs.exit = exit;
            HidingSpots.Add(hs);
        }

        /// <summary>A window the match can smash (its glass, sill collider and any boards).</summary>
        void RegisterWindow(Vector2 a, Vector2 b, GameObject pane, Collider glass, Collider sill, GameObject boards, Occluder boardsOccluder)
        {
            var w = pane.AddComponent<WindowPiece>();
            w.a = a;
            w.b = b;
            w.pane = pane;
            w.glass = glass;
            w.sill = sill;
            w.boards = boards;
            w.boardsOccluder = boardsOccluder;
            Windows.Add(w);
        }

        /// <summary>
        /// A random stream for gameplay entities placed after the level (NPC spawns, stakes, notes): each feature has its own,
        /// so adding one never shifts what the level's shared stream places.
        /// </summary>
        public System.Random FeatureRng(int feature) => new System.Random(unchecked(seed * 7919 + feature * 104729));

        /// <summary>
        /// A bare bulb hanging from a ceiling <paramref name="ceiling"/> above <paramref name="floor"/>: a 9 m light, as the
        /// original's lamps. The light sits on the floor (its polygon is cast from there), the bulb hangs above it.
        /// </summary>
        GameObject Lamp(Vector3 floor, float ceiling, float intensity = 0.8f, float flicker = 0.1f)
        {
            var root = new GameObject("Lamp");
            root.SetActive(false);
            root.transform.SetParent(staticRoot, false);
            root.transform.localPosition = floor;
            Piece("Bulb", LowPolyModels.HangingBulb(rng), root.transform, Vector3.up * ceiling, 0f, glowMaterial);
            var light = root.AddComponent<VisionLight>();
            light.range = 300f * MapLayout.Unit;
            light.intensity = intensity;
            light.flickerAmount = flicker;
            light.height = ceiling - 0.7f;
            root.SetActive(true);
            return root;
        }

        // ---------------------------------------------------------------- the original's cover pieces

        static Vector2 Rot(Vector2 v, int turns)
        {
            for (int i = 0; i < (turns & 3); i++) v = new Vector2(-v.y, v.x);
            return v;
        }

        /// <summary>Kit coordinates (original units, y south) to map metres.</summary>
        static Vector2 KitPoint(MapLayout.Kit k, float x, float y) => k.Centre + Rot(new Vector2(x, -y) * MapLayout.Unit, k.Turns);

        void BuildKits()
        {
            foreach (MapLayout.Kit k in Layout.Kits)
            {
                float y = H(k.Centre);
                switch (k.Kind)
                {
                    case MapLayout.KitKind.LWall:
                        PlankWallAt(KitPoint(k, -130, -60), KitPoint(k, -35, -60), y);
                        PlankWallAt(KitPoint(k, 35, -60), KitPoint(k, 130, -60), y);
                        PlankWallAt(KitPoint(k, 130, -60), KitPoint(k, 130, 95), y);
                        break;
                    case MapLayout.KitKind.Shack:
                        PlankWallAt(KitPoint(k, -95, -65), KitPoint(k, -35, -65), y);
                        PlankWallAt(KitPoint(k, 35, -65), KitPoint(k, 95, -65), y);
                        PlankWallAt(KitPoint(k, 95, -65), KitPoint(k, 95, -30), y);
                        Window(KitPoint(k, 95, -30), KitPoint(k, 95, 30), y);
                        PlankWallAt(KitPoint(k, 95, 30), KitPoint(k, 95, 65), y);
                        PlankWallAt(KitPoint(k, 95, 65), KitPoint(k, 40, 65), y);
                        PlankWallAt(KitPoint(k, -40, 65), KitPoint(k, -95, 65), y);
                        PlankWallAt(KitPoint(k, -95, 65), KitPoint(k, -95, -65), y);
                        Vector2 hinge = KitPoint(k, 40, 65), end = KitPoint(k, -40, 65);
                        MakeDoor(hinge, (end - hinge).normalized, Vector2.Distance(hinge, end), 2.2f, false, y);
                        break;
                    default:
                        // A car wreck beside two boulders.
                        Vector2 car = KitPoint(k, -87.5f, 0f);
                        Vector2 along = (KitPoint(k, 1, 0) - KitPoint(k, 0, 0)).normalized;
                        int kind = rng.Next(PropLibrary.CarVariants);
                        GameObject wreck = Prop(library != null ? library.cars : null, kind, staticRoot,
                            () => PropFactory.CreateCar(LowPolyModels.Car(rng, kind), lowPolyMaterial, kind));
                        Conform(wreck.transform, car, 1.8f, Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg + Range(-8f, 8f), 0.9f, 0.05f);
                        blocked.Add(car);
                        PlaceRock(KitPoint(k, 115, 0), 38f * MapLayout.Unit);
                        PlaceRock(KitPoint(k, 150, 30), 26f * MapLayout.Unit);
                        break;
                }
                blocked.Add(k.Centre);
            }
        }

        void PlaceRock(Vector2 p, float wantRadius)
        {
            int variant = 0;
            for (int i = 1; i < PropLibrary.RockRadii.Length; i++)
                if (Mathf.Abs(PropLibrary.RockRadii[i] - wantRadius) < Mathf.Abs(PropLibrary.RockRadii[variant] - wantRadius)) variant = i;
            float radius = PropLibrary.RockRadii[variant];
            GameObject go = Prop(library != null ? library.rocks : null, variant, staticRoot,
                () => PropFactory.CreateRock(LowPolyModels.Rock(rng, radius), lowPolyMaterial, radius));
            Conform(go.transform, p, radius, Range(0f, 360f), 0.8f, 0.12f);
            RockWall(go, p, radius);
            blocked.Add(p);
        }

        // ---------------------------------------------------------------- fences and logs

        void BuildFences()
        {
            Transform parent = Group("Fences");
            foreach (MapLayout.Segment f in Layout.Fences)
            {
                foreach (var (t0, t1) in new[] { (0f, f.GapStart), (f.GapEnd, 1f) })
                {
                    Vector2 a = Vector2.Lerp(f.A, f.B, t0), b = Vector2.Lerp(f.A, f.B, t1);
                    float len = Vector2.Distance(a, b);
                    if (len < 0.5f) continue;
                    Vector2 d = (b - a) / len;
                    float y = Mathf.Min(H(a), H(b), H((a + b) * 0.5f));
                    GameObject go = Piece("Fence", LowPolyModels.Fence(rng, len), parent, new Vector3(a.x, y, a.y), -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                    // Climbed over at the vaulting pace.
                    MakeVaultable(go, AddBox(go, new Vector3(len * 0.5f, 0.7f, 0f), new Vector3(len, 1.4f, 0.2f)), FenceVaultHeight, 1f);
                }
            }
        }

        void BuildLogs()
        {
            Transform parent = Group("Logs");
            foreach (MapLayout.Segment l in Layout.Logs)
            {
                Vector2 mid = (l.A + l.B) * 0.5f, d = (l.B - l.A).normalized;
                float len = Vector2.Distance(l.A, l.B);
                GameObject go = Piece("Log", LowPolyModels.Log(rng, len), parent, Vector3.zero, 0f);
                Conform(go.transform, mid, len * 0.4f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 0.85f, 0.04f);
                BoxCollider box = AddBox(go, new Vector3(0f, 0.3f, 0f), new Vector3(len, 0.6f, 0.6f));
                // Seen over in the see-through shade, vaulted by players.
                var occ = go.AddComponent<Vision.Visibility.Occluder>();
                occ.shape = Vision.Visibility.Occluder.Shape.Box;
                occ.size = new Vector2(len * 0.95f, 0.5f);
                // MakeLow marks it low and re-registers it, so it is seen over from the first frame.
                MakeLow(go, box);
                blocked.Add(mid);
            }
        }

        // ---------------------------------------------------------------- the lake

        void BuildLake()
        {
            Transform parent = Group("Lake");
            var shore = new List<Vector2>();
            const int n = 56;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                shore.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (Layout.ShoreRadius(a) + 0.6f));
            }
            Piece("Lake Water", LowPolyModels.Water(shore), parent, new Vector3(Layout.LakeCentre.x, Terrain.LakeLevel, Layout.LakeCentre.y), 0f);
            Vector2 d = (Layout.DockEnd - Layout.DockStart).normalized;
            float len = Vector2.Distance(Layout.DockStart, Layout.DockEnd);
            float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            float deck = Mathf.Max(H(Layout.DockStart), Terrain.LakeLevel) + 0.35f - Terrain.LakeLevel;
            Piece("Dock", LowPolyModels.Dock(rng, len, Layout.DockHalfWidth, deck), parent, new Vector3(Layout.DockStart.x, Terrain.LakeLevel, Layout.DockStart.y), yaw);
            // A lantern at the end of the dock.
            Vector2 at = Layout.DockEnd - d * 0.6f + new Vector2(d.y, -d.x) * (Layout.DockHalfWidth - 0.25f);
            GameObject lantern = Prop(library != null ? library.lantern : null, parent,
                () => PropFactory.CreateLantern(LowPolyModels.LanternPost(rng), glowMaterial));
            lantern.transform.SetLocalPositionAndRotation(new Vector3(at.x, Terrain.LakeLevel + deck, at.y), Quaternion.Euler(0f, yaw + 90f, 0f));
        }

        // ---------------------------------------------------------------- power line

        void BuildPowerLine()
        {
            if (Layout.PowerPoles.Count < 2) return;
            Transform parent = Group("Power Line");
            var bases = new List<Vector3>();
            var yaws = new List<float>();
            for (int i = 0; i < Layout.PowerPoles.Count; i++)
            {
                Vector2 p = Layout.PowerPoles[i];
                Vector2 prev = Layout.PowerPoles[Mathf.Max(0, i - 1)], next = Layout.PowerPoles[Mathf.Min(Layout.PowerPoles.Count - 1, i + 1)];
                Vector2 dir = (next - prev).normalized;
                float yaw = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y)).eulerAngles.y;
                Vector3 at = Upright(p, 0.2f);
                GameObject pole = Piece("Power Pole", LowPolyModels.PowerPole(rng, i % 5 == 2), parent, at, yaw);
                var col = pole.AddComponent<CapsuleCollider>();
                col.radius = 0.18f;
                col.height = LowPolyModels.PowerPoleHeight;
                col.center = Vector3.up * LowPolyModels.PowerPoleHeight * 0.5f;
                var occ = pole.AddComponent<Occluder>();
                occ.shape = Occluder.Shape.Circle;
                occ.radius = 0.16f;
                occ.sides = 6;
                bases.Add(at);
                yaws.Add(yaw);
                blocked.Add(p);
            }
            Piece("Power Wires", LowPolyModels.PowerWires(bases, yaws), parent, Vector3.zero, 0f);
        }

        // ---------------------------------------------------------------- graveyard

        void BuildGraveyard()
        {
            Transform parent = Group("Graveyard");
            Rect g = Layout.Graveyard;
            Quaternion q = Quaternion.Euler(0f, Layout.GraveyardYaw, 0f);
            Vector2 Local(float x, float z)
            {
                Vector3 v = q * new Vector3(x, 0f, z);
                return g.center + new Vector2(v.x, v.z);
            }
            float hx = g.width * 0.5f, hz = g.height * 0.5f;
            // Iron fence round the plot; the only way in is the pair of gates between stone pillars in the middle of the
            // south side. Survivors can't climb the fence; Zach can, slowly.
            const float gateHalf = GateHalfWidth, pillar = 0.44f;
            float sideEnd = gateHalf + pillar;
            var sides = new[] { (new Vector2(-hx, -hz), new Vector2(-sideEnd, -hz)), (new Vector2(sideEnd, -hz), new Vector2(hx, -hz)),
                                (new Vector2(hx, -hz), new Vector2(hx, hz)), (new Vector2(hx, hz), new Vector2(-hx, hz)), (new Vector2(-hx, hz), new Vector2(-hx, -hz)) };
            float fh = LowPolyModels.IronFenceHeight;
            var pieces = new List<(float from, float to)>();
            for (int si = 0; si < sides.Length; si++)
            {
                var (a0, b0) = sides[si];
                Vector2 a = Local(a0.x, a0.y), b = Local(b0.x, b0.y);
                float len = Vector2.Distance(a, b);
                Vector2 d = (b - a) / len;
                // Each side in short panels, each set at the lowest of the ground under it (one rigid run the length of a
                // side, set at its lower end, sank into the ground wherever the ground rose along it). The panel next to
                // the gates keeps the solid stretch with a little climbable fence after it.
                bool gateAtEnd = si == 0, gateAtStart = si == 1;   // the west panel runs up to the gate, the east one away from it
                float solid = Mathf.Min(GateFenceSolid, len), gatePiece = si < 2 ? Mathf.Min(len, solid + IronPanel) : 0f;
                if (si < 2 && len - gatePiece < 0.6f) gatePiece = len;
                float runFrom = gateAtStart ? gatePiece : 0f, runTo = gateAtEnd ? len - gatePiece : len;
                pieces.Clear();
                if (gateAtStart) pieces.Add((0f, gatePiece));
                int n = runTo - runFrom > 0.05f ? Mathf.Max(1, Mathf.CeilToInt((runTo - runFrom) / IronPanel)) : 0;
                for (int k = 0; k < n; k++) pieces.Add((Mathf.Lerp(runFrom, runTo, k / (float)n), Mathf.Lerp(runFrom, runTo, (k + 1) / (float)n)));
                if (gateAtEnd) pieces.Add((len - gatePiece, len));
                foreach (var (from, to) in pieces)
                {
                    Vector2 pa = a + d * from, pb = a + d * to;
                    float plen = to - from;
                    float baseY = Mathf.Min(Mathf.Min(H(pa), H(pb)), H((pa + pb) * 0.5f)) - 0.05f;
                    GameObject f = Piece("Iron Fence", LowPolyModels.IronFence(rng, plen), parent, new Vector3(pa.x, baseY, pa.y), -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                    bool atGate = (gateAtStart && from < 1e-4f) || (gateAtEnd && to > len - 1e-4f);
                    if (!atGate)
                    {
                        MakeVaultable(f, AddBox(f, new Vector3(plen * 0.5f, fh * 0.5f, 0f), new Vector3(plen, fh, 0.12f)), IronFenceVaultHeight, 1f, true);
                        continue;
                    }
                    // Next to the pillar: a plain wall even to Zach, so brushing the fence as he goes for the gateway
                    // never starts a climb.
                    float s0 = Mathf.Min(solid, plen), keep = plen - s0, keepFrom = gateAtEnd ? 0f : s0;
                    var wall = new GameObject("Gate Fence Wall");
                    wall.transform.SetParent(f.transform, false);
                    AddBox(wall, new Vector3(gateAtEnd ? plen - s0 * 0.5f : s0 * 0.5f, fh * 0.5f, 0f), new Vector3(s0, fh, 0.12f));
                    if (keep > 0.3f)
                        MakeVaultable(f, AddBox(f, new Vector3(keepFrom + keep * 0.5f, fh * 0.5f, 0f), new Vector3(keep, fh, 0.12f)), IronFenceVaultHeight, 1f, true);
                    else f.layer = GraveFenceLayer;
                }
            }
            foreach (float s in new[] { -1f, 1f })
            {
                Vector2 pp = Local(s * (gateHalf + pillar * 0.5f), -hz);
                GameObject pl = Piece("Gate Pillar", LowPolyModels.GatePillar(rng, 2.45f), parent, new Vector3(pp.x, H(pp) - 0.05f, pp.y), Layout.GraveyardYaw);
                AddBox(pl, new Vector3(0f, 1.2f, 0f), new Vector3(pillar + 0.08f, 2.4f, pillar + 0.08f));
            }
            {
                Vector2 a = Local(-gateHalf, -hz), b = Local(gateHalf, -hz);
                MakeGate(parent, a, (b - a).normalized, gateHalf * 2f, Mathf.Min(H(a), H(b), H((a + b) * 0.5f)) - 0.03f);
            }
            // Rows of graves facing the gate, some missing, some crooked; the middle kept clear round the generator, with
            // an aisle to it from the gates.
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 7; col++)
                {
                    if (rng.NextDouble() < 0.18) continue;
                    float x = -hx + 1.6f + col * (g.width - 3.2f) / 6f + Range(-0.3f, 0.3f);
                    float z = hz - 1.6f - row * 2.9f + Range(-0.2f, 0.2f);
                    if ((Mathf.Abs(x) < 2.7f && Mathf.Abs(z) < 2.3f) || (Mathf.Abs(x) < 1.5f && z < 0f)) continue;
                    Vector2 p = Local(x, z);
                    int style = rng.Next(LowPolyModels.HeadstoneStyles);
                    GameObject stone = Piece("Headstone", LowPolyModels.Headstone(rng, style), parent, Vector3.zero, 0f);
                    stone.transform.SetLocalPositionAndRotation(new Vector3(p.x, H(p) - 0.02f, p.y), q * Quaternion.Euler(0f, 180f + Range(-6f, 6f), 0f));
                    AddBox(stone, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.9f, 0.3f));
                }
            // Dead trees in two corners.
            foreach (var (cx, cz, kind) in new[] { (-hx + 1.3f, hz - 1.2f, LowPolyModels.DeadTreeKind.Elm), (hx - 1.4f, -hz + 1.6f, LowPolyModels.DeadTreeKind.Oak) })
            {
                Vector2 p = Local(cx, cz);
                GameObject t = Prop(library != null ? library.trees : null, (int)kind * 3 + 2, parent,
                    () => PropFactory.CreateDeadTree(LowPolyModels.DeadTree(kind, 2), lowPolyMaterial, kind));
                t.transform.SetLocalPositionAndRotation(Upright(p, 0.4f), Quaternion.Euler(0f, Range(0f, 360f), 0f));
                blocked.Add(p);
            }
            blocked.Add(g.center);
        }

        // ---------------------------------------------------------------- playground

        void BuildPlayground()
        {
            Transform parent = Group("Playground");
            Rect r = Layout.Playground;
            float y = H(r.center);
            var pieces = new (LowPolyModels.PlayKind kind, Vector2 at, float yaw, Vector3 box)[]
            {
                (LowPolyModels.PlayKind.Swings, new Vector2(-3.8f, 2.6f), 0f, new Vector3(4.2f, 2.5f, 2f)),
                (LowPolyModels.PlayKind.Slide, new Vector2(3.4f, 2.8f), 180f, new Vector3(3.2f, 1.9f, 0.8f)),
                (LowPolyModels.PlayKind.Seesaw, new Vector2(-3.6f, -2.8f), 25f, new Vector3(3.2f, 0.6f, 0.4f)),
                (LowPolyModels.PlayKind.Roundabout, new Vector2(1.2f, -2.4f), 0f, new Vector3(2.6f, 0.5f, 2.6f)),
                (LowPolyModels.PlayKind.Climber, new Vector2(4.6f, -2.2f), 0f, new Vector3(2.8f, 1.5f, 2.8f)),
            };
            foreach (var (kind, at, yaw, box) in pieces)
            {
                Vector2 p = r.center + at;
                GameObject go = Piece(kind.ToString(), LowPolyModels.Playground(rng, kind), parent, new Vector3(p.x, y, p.y), yaw + Range(-6f, 6f));
                AddBox(go, new Vector3(0f, box.y * 0.5f, 0f), box);
                if (kind == LowPolyModels.PlayKind.Swings)
                {
                    // Two seats: one still, one creaking back and forth on its own; the third chain snapped.
                    foreach (var (x, broken, sway) in new[] { (-1.1f, false, true), (0f, true, false), (1.1f, false, false) })
                    {
                        GameObject seat = Piece("Swing", LowPolyModels.SwingSeat(rng, broken), go.transform, new Vector3(x, 2.4f, 0f), 0f);
                        seat.isStatic = false;
                        if (sway) seat.AddComponent<Sway>().Set(new Vector3(18f, 0f, 0f), 0.42f);
                    }
                }
            }
            // A sandbox frame, half buried.
            Vector2 sb = r.center + new Vector2(-0.6f, 0.4f);
            var frame = new LowPolyMeshBuilder(rng);
            foreach (var (o, s) in new[] { (new Vector3(-1.2f, 0f, -1.2f), new Vector3(2.4f, 0.22f, 0.15f)), (new Vector3(-1.2f, 0f, 1.05f), new Vector3(2.4f, 0.22f, 0.15f)),
                                         (new Vector3(-1.2f, 0f, -1.2f), new Vector3(0.15f, 0.22f, 2.4f)), (new Vector3(1.05f, 0f, -1.2f), new Vector3(0.15f, 0.22f, 2.4f)) })
                frame.AddBox(o, s, LowPolyModels.Site.Weathered, 0.1f);
            frame.AddBox(new Vector3(-1.05f, 0f, -1.05f), new Vector3(2.1f, 0.08f, 2.1f), new Color(0.55f, 0.50f, 0.40f), 0.08f);
            Piece("Sandbox", frame.ToMesh("Sandbox"), parent, new Vector3(sb.x, y - 0.02f, sb.y), 8f);
            blocked.Add(r.center);
        }

        // ---------------------------------------------------------------- the hanging tree

        void BuildHangingTree()
        {
            Transform parent = Group("Hanging Tree");
            Vector2 p = Layout.HangingTree;
            float yaw = Range(0f, 360f);
            GameObject tree = Piece("Hanging Tree", LowPolyModels.HangingOak(rng), parent, Upright(p, 0.5f), yaw);
            var col = tree.AddComponent<CapsuleCollider>();
            col.radius = 0.55f;
            col.height = 4f;
            col.center = Vector3.up * 2f;
            var occ = tree.AddComponent<Occluder>();
            occ.shape = Occluder.Shape.Circle;
            occ.radius = 0.55f;
            occ.sides = 8;
            // The rope hangs from the long limb; the figure turns slowly on it.
            Vector2 rope = LowPolyModels.HangingRope;
            var pivot = new GameObject("Rope").transform;
            pivot.SetParent(tree.transform, false);
            pivot.localPosition = new Vector3(rope.y, rope.x, 0f);
            pivot.gameObject.AddComponent<Sway>().Set(new Vector3(3f, 18f, 3f), 0.11f);
            const float ropeLength = 1.55f;
            Piece("Noose", LowPolyModels.Noose(ropeLength), pivot, Vector3.zero, 0f).isStatic = false;
            var figure = new GameObject("Hanged Figure");
            figure.transform.SetParent(pivot, false);
            // The mannequin's neck (about 1.53 up) meets the noose; the head tips forward.
            figure.transform.SetLocalPositionAndRotation(new Vector3(0f, -ropeLength - 1.5f, 0.03f), Quaternion.Euler(4f, 0f, 2f));
            figure.AddComponent<MeshFilter>().sharedMesh = library != null && library.player != null
                ? library.player.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh
                : MannequinBuilder.Build();
            PropFactory.NoShadows(figure.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;
            blocked.Add(p);
        }

        // ---------------------------------------------------------------- generators, lights

        void BuildGenerators()
        {
            foreach (Vector2 g in Layout.WoodsGenerators)
            {
                GameObject gen = Prop(library != null ? library.generator : null, staticRoot,
                    () => PropFactory.CreateGenerator(LowPolyModels.Generator(rng), lowPolyMaterial));
                bool graveyard = g == Layout.Graveyard.center;
                Conform(gen.transform, g, 0.7f, graveyard ? Layout.GraveyardYaw : Range(0f, 4f) * 90f, 0.85f, 0.03f);
                MakeObjective(gen);
                Generators.Add(gen.transform);
                blocked.Add(g);
            }
        }

        /// <summary>Makes a generator startable: hold Interact beside it; running, it shakes and glows.</summary>
        void MakeObjective(GameObject gen)
        {
            gen.isStatic = false;
            var glowGo = new GameObject("Running Light");
            glowGo.transform.SetParent(gen.transform, false);
            glowGo.transform.localPosition = new Vector3(0f, 0f, 0.75f);
            var glow = glowGo.AddComponent<VisionLight>();
            glow.range = 3.2f;
            glow.intensity = 0.5f;
            glow.flickerAmount = 0.12f;
            glow.height = 1.0f;
            glow.enabled = false;
            var objective = gen.AddComponent<GeneratorObjective>();
            objective.body = gen.transform;
            objective.glow = glow;
            BuildFloodLight(gen, objective);
        }

        /// <summary>The flood light's reach (design units), half its beam's width (degrees) and brightness.</summary>
        public const float FloodRange = 20f, FloodHalfAngle = 55f, FloodIntensity = 1f, FloodHeight = 2.8f;

        /// <summary>
        /// A flood light wired to a generator: a steel pole just behind it with a lamp head on top, a cable along the ground
        /// to the generator, and a wide cone of light aimed out across it. Dark until the generator runs, then on for good.
        /// </summary>
        void BuildFloodLight(GameObject gen, GeneratorObjective objective)
        {
            Vector3 gl = transform.InverseTransformPoint(gen.transform.position);
            Vector3 f3 = transform.InverseTransformDirection(gen.transform.forward);
            var f = new Vector2(f3.x, f3.z);
            f = f.sqrMagnitude > 1e-6f ? f.normalized : Vector2.up;
            var g = new Vector2(gl.x, gl.z);
            float baseY = gl.y - 0.05f;
            // The pole on clear ground beside the generator, on its side of any wall (behind it first, then round it).
            Physics.SyncTransforms();
            int solid = ~((1 << GroundLayer) | (1 << CharacterLayer) | (1 << 2) | (1 << LowLayer));
            float s = transform.lossyScale.x;
            Vector3 W(Vector2 p, float up) => transform.TransformPoint(new Vector3(p.x, baseY + up, p.y));
            Vector2 at = g - f * 1.7f;
            for (int k = 0; k < 8; k++)
            {
                float a = Mathf.Atan2(-f.y, -f.x) + k * Mathf.PI / 4f * (k % 2 == 0 ? 1f : -1f) * ((k + 1) / 2);
                Vector2 c = g + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.7f;
                if (Physics.CheckSphere(W(c, 1.2f), 0.35f * s, solid, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(W(g, 1.2f), W(c, 1.2f), solid, QueryTriggerInteraction.Ignore)) continue;
                at = c;
                break;
            }
            // Aimed the way with the longest clear run from the pole (over open ground, not into a wall).
            Vector2 aim = (g - at).normalized;
            float bestRun = -1f;
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector3 from = W(at, 1.5f), dir = transform.TransformDirection(new Vector3(d.x, 0f, d.y)).normalized;
                float run = Physics.Raycast(from, dir, out RaycastHit hit, FloodRange * s, solid, QueryTriggerInteraction.Ignore) ? hit.distance : FloodRange * s;
                // Prefer across the generator a little, when it's as open.
                run *= 1f + 0.15f * Mathf.Max(0f, Vector2.Dot(d, (g - at).normalized));
                if (run > bestRun) { bestRun = run; aim = d; }
            }
            f = aim;
            Quaternion yaw = Quaternion.Euler(0f, Mathf.Atan2(f.x, f.y) * Mathf.Rad2Deg, 0f);
            var steel = new Color(0.32f, 0.33f, 0.35f);

            var pole = new LowPolyMeshBuilder(rng);
            pole.AddBox(new Vector3(-0.2f, 0f, -0.2f), new Vector3(0.4f, 0.08f, 0.4f), new Color(0.25f, 0.25f, 0.26f), 0.02f);
            pole.AddBox(new Vector3(-0.06f, 0f, -0.06f), new Vector3(0.12f, FloodHeight, 0.12f), steel, 0.02f);
            pole.AddBox(new Vector3(-0.35f, FloodHeight - 0.12f, -0.05f), new Vector3(0.7f, 0.08f, 0.1f), steel, 0.02f);
            GameObject poleGo = MakeStatic("Flood Light", pole.ToMesh("Flood Light Pole"), new Vector3(at.x, baseY, at.y), yaw, lowPolyMaterial);
            AddBox(poleGo, new Vector3(0f, FloodHeight * 0.5f, 0f), new Vector3(0.2f, FloodHeight, 0.2f));
            poleGo.SetActive(true);

            // The lamp head: a wide box tipped down toward what it lights, its face lit when the generator runs.
            var head = new LowPolyMeshBuilder(rng);
            head.AddBox(new Vector3(-0.32f, -0.14f, -0.08f), new Vector3(0.64f, 0.28f, 0.16f), new Color(0.2f, 0.21f, 0.22f), 0.01f);
            head.AddBox(new Vector3(-0.28f, -0.11f, 0.08f), new Vector3(0.56f, 0.22f, 0.02f), new Color(0.95f, 0.93f, 0.82f), 0.005f);
            GameObject headGo = MakeStatic("Flood Lamp", head.ToMesh("Flood Lamp"), new Vector3(at.x, baseY + FloodHeight + 0.05f, at.y), yaw * Quaternion.Euler(28f, 0f, 0f), lowPolyMaterial);
            // Not static: its material changes as the generator starts.
            headGo.isStatic = false;
            headGo.SetActive(true);

            // The cable: a dark run along the ground from the pole's foot to the generator.
            var cable = new LowPolyMeshBuilder(rng);
            float len = Vector2.Distance(at, g);
            cable.AddBox(new Vector3(-0.025f, 0f, 0f), new Vector3(0.05f, 0.04f, len), new Color(0.08f, 0.08f, 0.08f), 0.005f);
            Vector2 toGen = (g - at).normalized;
            GameObject cableGo = MakeStatic("Flood Cable", cable.ToMesh("Flood Cable"), new Vector3(at.x, baseY + 0.06f, at.y),
                Quaternion.Euler(0f, Mathf.Atan2(toGen.x, toGen.y) * Mathf.Rad2Deg, 0f), lowPolyMaterial);
            cableGo.SetActive(true);

            var lightGo = new GameObject("Flood Light Beam");
            lightGo.transform.SetParent(poleGo.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            var flood = lightGo.AddComponent<VisionLight>();
            flood.range = FloodRange;
            flood.height = FloodHeight;
            flood.intensity = FloodIntensity;
            flood.flickerAmount = 0.02f;
            flood.coneHalfAngleDeg = FloodHalfAngle;
            flood.falloffStart = 0.65f;
            flood.enabled = false;
            objective.flood = flood;
            objective.floodLamp = headGo.GetComponent<MeshRenderer>();
            objective.lampOff = lowPolyMaterial;
            objective.lampOn = glowMaterial != null ? glowMaterial : lowPolyMaterial;
        }

        /// <summary>The most stakes a map has (the original's: survivors + 4, between 6 and 12).</summary>
        public const int MaxStakes = 12;

        /// <summary>
        /// The original's scarecrow stakes: three candidate spots in each clearing but the spawn's (at 0.55 of its radius),
        /// none near a generator (3.9 m), a cabin, a campfire, the building or the spawn (15 m), then spread out by always
        /// taking the candidate farthest from those already chosen. Up to <see cref="MaxStakes"/>; a match uses the first
        /// survivors + 4 of them (the best spread).
        /// </summary>
        void BuildStakes()
        {
            System.Random r = FeatureRng(3);
            float R(float a, float b) => a + (float)r.NextDouble() * (b - a);
            var candidates = new List<Vector2>();
            for (int i = 1; i < Layout.Clearings.Count; i++)
            {
                MapLayout.Clearing c = Layout.Clearings[i];
                // Spots round each clearing, nearer and further from its middle, so one with a generator or a fire in it
                // still has room for a stake.
                for (int k = 0; k < 6; k++)
                {
                    float a = R(0f, Mathf.PI * 2f);
                    candidates.Add(c.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * c.Radius * (k % 2 == 0 ? 0.55f : 0.8f));
                }
            }
            bool Ok(Vector2 p)
            {
                if (p.magnitude > halfExtent - 3f) return false;
                if (Layout.LakeDepth(p) > -1f || Layout.Yard.Contains(p) || Layout.Building.Contains(p)) return false;
                foreach (Transform g in Generators)
                {
                    Vector3 l = transform.InverseTransformPoint(g.position);
                    if (Vector2.Distance(new Vector2(l.x, l.z), p) < 130f * MapLayout.Unit) return false;
                }
                foreach (MapLayout.Cabin c in Layout.Cabins) if (Expand(c.Area, 40f * MapLayout.Unit).Contains(p)) return false;
                foreach (Vector2 f in Layout.Campfires) if (Vector2.Distance(f, p) < 90f * MapLayout.Unit) return false;
                if (Vector2.Distance(Layout.Spawn, p) < 500f * MapLayout.Unit) return false;
                return !blocked.AnyWithin(p, 1.0f);
            }
            var pool = candidates.FindAll(Ok);
            var chosen = new List<Vector2>();
            while (chosen.Count < MaxStakes && pool.Count > 0)
            {
                int bi = 0;
                float bd = -1f;
                for (int i = 0; i < pool.Count; i++)
                {
                    float d = (float)r.NextDouble();
                    if (chosen.Count > 0)
                    {
                        d = float.MaxValue;
                        foreach (Vector2 s in chosen) d = Mathf.Min(d, Vector2.Distance(s, pool[i]));
                    }
                    if (d > bd) { bd = d; bi = i; }
                }
                chosen.Add(pool[bi]);
                pool.RemoveAt(bi);
            }
            Transform parent = Group("Stakes");
            foreach (Vector2 p in chosen)
            {
                GameObject go = Piece("Stake", LowPolyModels.Stake(r), parent, Upright(p, 0.2f), R(0f, 360f));
                AddBox(go, new Vector3(0f, 1.2f, 0f), new Vector3(0.16f, 2.4f, 0.16f));
                Stakes.Add(go.transform);
                blocked.Add(p);
            }
        }

        void BuildLights()
        {
            foreach (Vector2 p in Layout.Campfires)
            {
                GameObject go = Prop(library != null ? library.campfire : null, staticRoot, CampfireFromScratch);
                go.transform.SetLocalPositionAndRotation(Upright(p, 0.6f), Quaternion.identity);
                blocked.Add(p);
            }

            // Lanterns along the paths every ~20 units, alternating sides.
            var lanterns = new PointGrid(8f);
            PathNetwork paths = Terrain.Paths;
            int count = 0;
            if (paths != null)
            {
                int side = 1;
                foreach (List<Vector2> path in paths.Paths)
                {
                    float walked = 10f;
                    for (int i = 0; i < path.Count - 1 && count < 40; i++)
                    {
                        Vector2 a = path[i], b = path[i + 1];
                        float seg = Vector2.Distance(a, b);
                        walked += seg;
                        if (walked < 20f || seg < 1e-3f) continue;
                        Vector2 dir = (b - a) / seg;
                        Vector2 p = a + new Vector2(-dir.y, dir.x) * (side * (paths.HalfWidth + 0.7f));
                        side = -side;
                        if (!Free(p, 0.15f, 1.5f) || lanterns.AnyWithin(p, 12f)) continue;
                        lanterns.Add(p);
                        GameObject go = Prop(library != null ? library.lantern : null, staticRoot,
                            () => PropFactory.CreateLantern(LowPolyModels.LanternPost(rng), glowMaterial));
                        go.transform.SetLocalPositionAndRotation(Upright(p, 0.15f), Quaternion.Euler(0f, Range(0f, 360f), 0f));
                        blocked.Add(p);
                        walked = 0f;
                        count++;
                    }
                }
            }

            // A burning barrel beside the first wreck.
            foreach (MapLayout.Kit k in Layout.Kits)
            {
                if (k.Kind != MapLayout.KitKind.Wreck) continue;
                Vector2 barrel = KitPoint(k, -87.5f, 95f);
                GameObject drum = Prop(library != null ? library.burningBarrel : null, staticRoot, BarrelFromScratch);
                drum.transform.SetLocalPositionAndRotation(Upright(barrel, 0.3f), Quaternion.Euler(0f, Range(0f, 360f), 0f));
                blocked.Add(barrel);
                break;
            }
        }

        GameObject CampfireFromScratch()
        {
            var flames = LowPolyModels.Flames(rng, 0.14f, 0.55f, 5);
            return PropFactory.CreateCampfire(LowPolyModels.Campfire(rng), glowMaterial, flames.ConvertAll(f => f.mesh).ToArray(),
                flames.ConvertAll(f => f.position).ToArray(), LowPolyModels.Ember(rng));
        }

        GameObject BarrelFromScratch()
        {
            var flames = LowPolyModels.Flames(rng, 0.12f, 0.45f, 4);
            return PropFactory.CreateBurningBarrel(LowPolyModels.BurningBarrel(rng), glowMaterial, flames.ConvertAll(f => f.mesh).ToArray(),
                flames.ConvertAll(f => f.position).ToArray(), LowPolyModels.Ember(rng));
        }

        // ---------------------------------------------------------------- trees and rocks

        /// <summary>Trees per square metre of open ground (as the 80 m map had), about 2,900 on the 180 m map.</summary>
        public const float TreeDensity = 0.09f;

        /// <summary>Trees: denser where the land is wooded, dead or evergreen by <see cref="Deadness"/>.</summary>
        void BuildTrees()
        {
            // The woods fill the world's circle, on into the dark past the playable ring.
            int target = Mathf.RoundToInt(TreeDensity * Mathf.PI * WorldRadius * WorldRadius);
            int placed = 0;
            for (int attempt = 0; attempt < target * 60 && placed < target; attempt++)
            {
                var p = new Vector2(Range(-WorldRadius, WorldRadius), Range(-WorldRadius, WorldRadius));
                float wood = Woodland(p.x, p.y);
                if ((float)rng.NextDouble() > 0.2f + 0.8f * wood) continue;
                if (!Free(p, PropFactory.TreeTrunkRadius, Mathf.Lerp(2.7f, 1.9f, wood))) continue;

                GameObject go;
                float dead = Deadness(p.x, p.y);
                bool isDead = (float)rng.NextDouble() < Mathf.Lerp(0.06f, 0.9f, dead);
                bool fallen = false;
                int fallenSize = 0;
                if (isDead)
                {
                    var kind = PickDeadTree();
                    // A fallen tree is 5-9 units long: keep it well clear of the paths.
                    if (kind == LowPolyModels.DeadTreeKind.Fallen && Terrain.Paths != null && Terrain.Paths.Distance(p) < 6f)
                        kind = LowPolyModels.DeadTreeKind.PineSnag;
                    int size = rng.Next(3);
                    fallen = kind == LowPolyModels.DeadTreeKind.Fallen;
                    fallenSize = size;
                    go = Prop(library != null ? library.trees : null, (int)kind * 3 + size, staticRoot,
                        () => PropFactory.CreateDeadTree(LowPolyModels.DeadTree(kind, size), lowPolyMaterial, kind));
                }
                else
                {
                    float moist = Moisture(p.x, p.y), roll = (float)rng.NextDouble();
                    int style = roll < 0.25f * dead * 2f ? 4
                        : roll < 0.62f && moist > 0.6f ? (roll < 0.4f ? 6 : 1)
                        : roll < 0.45f && moist < 0.42f ? 2
                        : roll < 0.6f && wood < 0.4f ? 3
                        : roll > 0.78f ? 5
                        : 0;
                    int variant = style * 3 + rng.Next(3);
                    go = Prop(library != null ? library.conifers : null, variant, staticRoot,
                        () => { Mesh m = LowPolyModels.Conifer(rng, style); return PropFactory.CreateTree(m, lowPolyMaterial, m.name); });
                }
                // Each tree gets its own height and girth: some shorter, some taller, some skinnier, some thicker.
                Vector2 hg = TreeScale(rng, isDead);
                go.transform.localScale = new Vector3(hg.y, hg.x, hg.y);
                // By its kind, not its name: the baked prop library's copies are named after their slot (DeadTree_33...).
                if (fallen)
                {
                    Conform(go.transform, p, 1.5f, Range(0f, 360f), 0.9f, 0.05f);
                    MakeLow(go, go.GetComponent<BoxCollider>());
                    RootMound(go, LowPolyModels.DeadTreeSizes[Mathf.Clamp(fallenSize, 0, LowPolyModels.DeadTreeSizes.Length - 1)]);
                }
                else
                    go.transform.SetLocalPositionAndRotation(Upright(p, PropFactory.TreeTrunkRadius * hg.y), Quaternion.Euler(0f, Range(0f, 360f), 0f));
                blocked.Add(p);
                MapTrees.Add((p, isDead ? 0.45f * hg.y : 1.1f * Mathf.Sqrt(hg.x * hg.y), isDead));
                placed++;
            }
            TreeCount = placed;
        }

        /// <summary>
        /// A tree's own height (x) and girth (y) multipliers, chosen independently. Evergreens spread over 0.62-1.45 (50%
        /// more than the shapes alone), dead trees a little less as their designs already vary; girth is kept within
        /// 0.55-1.1 of the height so no tree turns into a squat umbrella or a pole.
        /// </summary>
        public static Vector2 TreeScale(System.Random rng, bool dead)
        {
            float lo = dead ? 0.78f : 0.62f, hi = dead ? 1.25f : 1.45f;
            float h = Mathf.Lerp(lo, hi, (float)rng.NextDouble());
            float g = Mathf.Lerp(lo, hi, (float)rng.NextDouble());
            return new Vector2(h, Mathf.Clamp(g, h * 0.55f, h * 1.1f));
        }

        /// <summary>A dead tree design, weighted: snags, oaks and spruces common; the fallen tree and hollow trunk rarer.</summary>
        LowPolyModels.DeadTreeKind PickDeadTree()
        {
            float[] weight = { 1.2f, 1.4f, 0.9f, 1.2f, 0.6f, 0.7f, 0.9f, 0.8f, 0.9f, 0.6f, 0.45f, 0.45f, 1.2f };
            float total = 0f;
            foreach (float w in weight) total += w;
            float roll = (float)rng.NextDouble() * total;
            for (int i = 0; i < weight.Length; i++)
            {
                roll -= weight[i];
                if (roll <= 0f) return (LowPolyModels.DeadTreeKind)i;
            }
            return LowPolyModels.DeadTreeKind.PineSnag;
        }

        /// <summary>Timing of the terrain set-up (paths, baking).</summary>
        public string TerrainReport { get; private set; }

        /// <summary>Timing of the last generation, step by step.</summary>
        public string LastGenerationReport { get; private set; }

        /// <summary>How many trees the last generation placed.</summary>
        public int TreeCount { get; private set; }

        void BuildRocks()
        {
            int target = Mathf.RoundToInt(70f / 6400f * Mathf.PI * WorldRadius * WorldRadius);
            int placed = 0;
            for (int attempt = 0; attempt < target * 40 && placed < target; attempt++)
            {
                var p = new Vector2(Range(-WorldRadius, WorldRadius), Range(-WorldRadius, WorldRadius));
                // Small stones are common, boulders rare; steep ground and dead woods get more of them.
                float roll = (float)rng.NextDouble();
                int variant = roll < 0.45f ? rng.Next(3) : roll < 0.85f ? 3 + rng.Next(3) : 6 + rng.Next(2);
                float radius = PropLibrary.RockRadii[variant];
                float steep = Mathf.InverseLerp(10f, 30f, Terrain.SlopeDeg(p.x, p.y));
                if ((float)rng.NextDouble() > 0.3f + 0.45f * steep + 0.25f * Deadness(p.x, p.y)) continue;
                if (!Free(p, radius, 1.4f)) continue;
                GameObject go = Prop(library != null ? library.rocks : null, variant, staticRoot,
                    () => PropFactory.CreateRock(LowPolyModels.Rock(rng, radius), lowPolyMaterial, radius));
                Conform(go.transform, p, radius, Range(0f, 360f), 0.8f, 0.12f);
                RockWall(go, p, radius);
                blocked.Add(p);
                MapRocks.Add((p, radius));
                placed++;
            }
        }

        // ---------------------------------------------------------------- supplies

        /// <summary>
        /// The original's supplies at its counts (86 in all), placed by its rules: candidate spots in the building's
        /// rooms, in the cabins, around the clearings and beside the paths, shuffled, each item taking the first spot
        /// at least 4.8 m (160 units) from the others, or 1.8 m when the map runs short.
        /// </summary>
        /// <summary>Items keep this far inside the first (flicker) ring, and those beside paths this far from the path's edge.</summary>
        public const float PickupEdgeMargin = 10f, PathSpotNear = 2f, PathSpotFar = 12f, WildFromPaths = 20f;
        /// <summary>The share of all items that spawn out in the open woods, away from the paths.</summary>
        public const float WildShare = 0.25f;

        /// <summary>
        /// Supplies: a quarter of all the items lie out in the open woods, at least 20 m from any path, spread evenly over
        /// the map (each picked as the candidate farthest from those already placed). The rest go where the original puts
        /// them, the building's loot spots, the cabins and the clearings, and beside the paths, now anywhere 2-12 m from a
        /// path's edge instead of right on it. Nothing spawns within 10 m of the first boundary ring.
        /// </summary>
        void BuildPickups()
        {
            float limit = halfExtent - PickupEdgeMargin;
            bool Open(Vector2 p) => p.magnitude <= limit && !(Layout.LakeDepth(p) > -1f) && !Layout.Yard.Contains(p) && !Layout.Building.Contains(p) && !blocked.AnyWithin(p, 0.6f);
            var inside = new List<Vector2>();
            if (Layout.Plan != null) inside.AddRange(Layout.Plan.LootSpots);
            var outside = new List<Vector2>();
            foreach (MapLayout.Cabin c in Layout.Cabins)
            {
                outside.Add(c.Area.center + new Vector2(Range(-1.5f, 1.5f), 0.9f));
                outside.Add(new Vector2(c.Area.xMin + 1.2f, c.Area.yMax - 1.2f));
            }
            for (int i = 1; i < Layout.Clearings.Count; i++)
            {
                MapLayout.Clearing c = Layout.Clearings[i];
                for (int k = 0; k < 3; k++)
                {
                    float a = Range(0f, Mathf.PI * 2f);
                    outside.Add(c.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * c.Radius * Range(0.3f, 0.7f));
                }
            }
            PathNetwork paths = Terrain.Paths;
            // Beside the paths: 2-12 m out from the edge, either side.
            if (paths != null)
                foreach (List<Vector2> path in paths.Paths)
                {
                    if (path.Count < 4) continue;
                    for (int k = 0; k < 16; k++)
                    {
                        int s = rng.Next(1, path.Count - 2);
                        Vector2 a = path[s], b = path[s + 1], d = (b - a).normalized;
                        Vector2 on = Vector2.Lerp(a, b, Range(0f, 1f));
                        Vector2 at = on + new Vector2(-d.y, d.x) * (rng.Next(2) == 0 ? 1f : -1f) * (paths.HalfWidth * 1.4f + Range(PathSpotNear, PathSpotFar));
                        float edge = Terrain.PathClearance(at.x, at.y);
                        if (edge >= PathSpotNear && edge <= PathSpotFar + 1f) outside.Add(at);
                    }
                }
            var pool = new List<(Vector2 p, bool inside)>();
            foreach (Vector2 p in inside) pool.Add((p, true));
            foreach (Vector2 p in outside)
                if (Open(p)) pool.Add((p, false));
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            // Every item to place, shuffled; the first quarter go out into the woods.
            var all = new List<ItemType>();
            foreach (ItemType item in Items.All)
                for (int n = 0; n < Items.Info(item).mapCount; n++) all.Add(item);
            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (all[i], all[j]) = (all[j], all[i]);
            }
            int wild = Mathf.RoundToInt(all.Count * WildShare);

            Transform parent = Group("Supplies");
            var placed = new List<Vector2>();
            for (int n = 0; n < all.Count; n++)
            {
                ItemType item = all[n];
                Vector2 p;
                bool indoors = false;
                if (n < wild)
                {
                    // The best of a few random open spots far from the paths: the one farthest from everything placed.
                    float best = -1f;
                    p = Vector2.zero;
                    for (int tries = 0; tries < 60; tries++)
                    {
                        float ang = Range(0f, Mathf.PI * 2f), r = limit * Mathf.Sqrt(Range(0f, 1f));
                        var c = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                        if (!Open(c) || Terrain.PathClearance(c.x, c.y) < WildFromPaths) continue;
                        float near = float.MaxValue;
                        foreach (Vector2 q in placed) near = Mathf.Min(near, (q - c).sqrMagnitude);
                        if (near > best) { best = near; p = c; }
                    }
                    if (best < 0f) continue;
                }
                else
                {
                    int idx = -1;
                    foreach (float spacing in new[] { 160f * MapLayout.Unit, 60f * MapLayout.Unit })
                    {
                        idx = pool.FindIndex(c => placed.TrueForAll(q => (q - c.p).sqrMagnitude > spacing * spacing));
                        if (idx >= 0) break;
                    }
                    if (idx < 0) continue;
                    (p, indoors) = pool[idx];
                    pool.RemoveAt(idx);
                }
                placed.Add(p);
                var go = new GameObject($"Pickup {item}");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = LowPolyModels.Item(rng, item);
                PropFactory.NoShadows(go.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;
                var pickup = go.AddComponent<Pickup>();
                pickup.item = item;
                pickup.count = 1;
                if (indoors) go.transform.SetLocalPositionAndRotation(new Vector3(p.x, BuildingFloor + 0.015f, p.y), Quaternion.Euler(0f, Range(0f, 360f), 0f));
                else Conform(go.transform, p, 0.1f, Range(0f, 360f), 0.8f, 0f);
                go.transform.localScale = Vector3.one * 1.6f;
                Pickups.Add(pickup);
            }
        }

        // ---------------------------------------------------------------- entities

        void BuildCrows()
        {
            var spots = new List<Vector2>();
            for (int i = 0; i < 6; i++) spots.Add(Layout.Spawn + new Vector2(Range(-7f, 7f), Range(3f, 10f)));
            foreach (MapLayout.Kit k in Layout.Kits) if (spots.Count < 12) spots.Add(k.Centre + new Vector2(Range(-3f, 3f), Range(2.6f, 3.4f)));
            spots.Add(Layout.HangingTree + new Vector2(1.2f, 1.5f));
            foreach (Vector2 p in spots)
            {
                if (Layout.LakeDepth(p) > -0.5f) continue;
                GameObject go = Prop(library != null ? library.crows : null, rng.Next(PropLibrary.CrowVariants), entityRoot,
                    () => PropFactory.CreateCrow(LowPolyModels.Crow(rng), entityMaterial));
                Conform(go.transform, p, 0.15f, Range(0f, 360f), 0.6f, 0f);
                Crows.Add(go.transform);
            }
        }

        void BuildWanderer()
        {
            GameObject go = Prop(library != null ? library.wanderer : null, entityRoot,
                () => PropFactory.CreateWanderer(entityMaterial));
            Vector2 s = Layout.Spawn;
            var loop = new[] { s + new Vector2(5f, 4f), s + new Vector2(5f, 12f), s + new Vector2(-5f, 12f), s + new Vector2(-5f, 4f) };
            go.transform.localPosition = new Vector3(loop[0].x, H(loop[0]), loop[0].y);
            Wanderer = go.GetComponent<Wanderer>();
            Wanderer.waypoints = new Vector3[loop.Length];
            // Waypoints are world positions; the layout is in design units under the scaled root.
            for (int i = 0; i < loop.Length; i++) Wanderer.waypoints[i] = transform.TransformPoint(new Vector3(loop[i].x, 0f, loop[i].y));
        }

        void BuildPlayer()
        {
            GameObject go = Prop(library != null ? library.player : null, transform,
                () => PropFactory.CreatePlayer(lowPolyMaterial));
            go.transform.localPosition = new Vector3(Layout.Spawn.x, H(Layout.Spawn) + 0.05f, Layout.Spawn.y);

            Player = go.GetComponent<PlayerController>();
            Player.world = this;
            if (cameraRig != null)
            {
                cameraRig.target = go.transform;
                Player.viewCamera = cameraRig.GetComponent<Camera>();
                cameraRig.Snap();
            }
            if (maskRenderer != null) maskRenderer.viewer = go.GetComponent<VisionViewer>();
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>A saved prefab placed under <paramref name="parent"/>, or a generated one if there is none.</summary>
        GameObject Prop(GameObject prefab, Transform parent, System.Func<GameObject> generate)
        {
            if (prefab != null)
                return placeHook != null ? placeHook(prefab, parent) : Instantiate(prefab, parent);
            GameObject go = generate();
            go.transform.SetParent(parent, false);
            return go;
        }

        GameObject Prop(GameObject[] variants, int variant, Transform parent, System.Func<GameObject> generate) =>
            Prop(variants != null && variants.Length > variant ? variants[variant] : null, parent, generate);

        GameObject MakeStatic(string name, Mesh mesh, Vector3 pos, Quaternion rot, Material mat)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(staticRoot, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            PropFactory.NoShadows(go.AddComponent<MeshRenderer>()).sharedMaterial = mat;
            return go;
        }

        float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        Color Vary(Color c, float amount)
        {
            float k = 1f + Range(-amount, amount);
            return new Color(c.r * k, c.g * k, c.b * k);
        }
    }

    /// <summary>Slow pendulum motion about the object's own pivot (a creaking swing, a body turning on a rope).</summary>
    public sealed class Sway : MonoBehaviour
    {
        public Vector3 amplitude = new Vector3(10f, 0f, 0f);
        public float frequency = 0.4f;
        Quaternion rest;
        float phase;

        public void Set(Vector3 degrees, float hz)
        {
            amplitude = degrees;
            frequency = hz;
        }

        void Awake()
        {
            rest = transform.localRotation;
            phase = (transform.position.x * 0.37f + transform.position.z * 0.61f) % 6.28f;
        }

        void Update()
        {
            float t = Time.time * frequency * Mathf.PI * 2f + phase;
            transform.localRotation = rest * Quaternion.Euler(amplitude.x * Mathf.Sin(t), amplitude.y * Mathf.Sin(t * 0.37f + 1f), amplitude.z * Mathf.Sin(t * 0.83f + 2f));
        }
    }
}
