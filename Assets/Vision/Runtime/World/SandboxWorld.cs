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
        public float halfExtent = 90f;
        [Tooltip("Build the level when Play starts.")]
        public bool generateOnAwake = true;

        [Header("Props and materials")]
        [Tooltip("Saved prop prefabs. When empty, props are generated in memory instead.")]
        public PropLibrary library;
        public Material lowPolyMaterial;
        public Material entityMaterial;
        public Material glowMaterial;

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

        /// <summary>
        /// Hills and ditches with flat ground under the building and its yard, the cabins, the clearings, the cover
        /// pieces and the special sites, the lake's basin, and footpaths routed between everything.
        /// </summary>
        public TerrainField CreateTerrain(MapLayout layout)
        {
            var f = new TerrainField(seed, halfExtent);
            f.AddPad(Expand(layout.Building, 1.5f), 6f);
            f.AddPad(Expand(layout.Yard, 1f), 4f);
            foreach (MapLayout.Cabin c in layout.Cabins) f.AddPad(Expand(c.Area, 1.2f), 5f);
            for (int i = 0; i < layout.Clearings.Count; i++)
                f.AddPad(layout.Clearings[i].Centre, layout.Clearings[i].Radius * 0.45f, layout.Clearings[i].Radius * 0.6f);
            foreach (MapLayout.Kit k in layout.Kits) f.AddPad(k.Centre, 3.5f, 3f);
            foreach (Vector2 g in layout.WoodsGenerators) f.AddPad(g, 1.4f, 3f);
            f.AddPad(Expand(layout.Graveyard, 1f), 4f);
            f.AddPad(Expand(layout.Playground, 1f), 4f);
            f.AddPad(layout.HangingTree, 2.5f, 3f);
            f.SetLake(layout);

            var building = Expand(layout.Building, 1.4f);   // paths keep a path-width and a bit from the walls, windows and all
            var yard = Expand(layout.Yard, 0.2f);
            var cabins = new List<Rect>();
            foreach (MapLayout.Cabin c in layout.Cabins) cabins.Add(Expand(c.Area, 0.4f));
            bool Blocked(Vector2 p)
            {
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
                return false;
            }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var net = PathNetwork.Build(layout.PathPoints(), f.Height, halfExtent - 3f, Blocked);
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
            Ambulance = null;
            Crows.Clear();
            Pickups.Clear();
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

            Step("walls", BuildPerimeter);
            Step("building", BuildBuilding);
            Step("cabins", BuildCabins);
            Step("kits", BuildKits);
            Step("fences", () => { BuildFences(); BuildLogs(); });
            Step("lake", BuildLake);
            Step("power", BuildPowerLine);
            Step("sites", () => { BuildGraveyard(); BuildPlayground(); BuildHangingTree(); });
            Step("lights", () => { BuildGenerators(); BuildLights(); });
            Step("stakes", () => { BuildStakes(); BuildAmbulance(); });
            Step("trees", BuildTrees);
            Step("rocks", BuildRocks);
            Step("supplies", () => { BuildPickups(); BuildNotes(); });
            Step("ground", BuildGround);   // last, so plants and grass grow around everything placed
            Step("entities", () => { BuildCrows(); BuildWanderer(); BuildPlayer(); });
            LastGenerationReport = $"seed {seed}: {timer.ElapsedMilliseconds} ms ({times.ToString().Trim()} ms; {TerrainReport}), {TreeCount} trees";
            Debug.Log($"[Vision] Generated {LastGenerationReport}");
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
            float lim = halfExtent - 1.5f - radius;
            if (Mathf.Abs(p.x) > lim || Mathf.Abs(p.y) > lim) return false;
            if (Layout.Blocked(p) || Layout.LakeDepth(p) > -2f - radius) return false;
            PathNetwork paths = Terrain.Paths;
            if (paths != null && Terrain.PathDistance(p.x, p.y) < paths.HalfWidth + radius + 0.4f) return false;
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
            PathNetwork paths = Terrain.Paths;
            if (paths != null)
            {
                float d = Terrain.PathDistance(x, z);
                float edge = paths.HalfWidth + 0.15f + (n3 - 0.5f) * 0.5f;
                c = Color.Lerp(c, Ground.Path * (0.92f + 0.16f * n3), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge + 0.25f, edge - 0.25f, d)));
            }
            return c;
        }

        void BuildGround()
        {
            float e = halfExtent + 4f;
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

            int perChunk = Mathf.Max(1, Mathf.RoundToInt(ChunkSize / grid.Step));
            int chunks = Mathf.CeilToInt(grid.Cells / (float)perChunk);
            var builders = new LowPolyMeshBuilder[chunks, chunks];
            for (int cj = 0; cj < chunks; cj++)
                for (int ci = 0; ci < chunks; ci++)
                {
                    builders[ci, cj] = new LowPolyMeshBuilder(rng);
                    builders[ci, cj].AddTerrainPatch(grid, ci * perChunk, Mathf.Min(grid.Cells, (ci + 1) * perChunk),
                        cj * perChunk, Mathf.Min(grid.Cells, (cj + 1) * perChunk), GroundColorVaried);
                }
            LowPolyMeshBuilder ChunkAt(Vector2 p)
            {
                int ci = Mathf.Clamp(Mathf.FloorToInt((p.x + e) / grid.Step / perChunk), 0, chunks - 1);
                int cj = Mathf.Clamp(Mathf.FloorToInt((p.y + e) / grid.Step / perChunk), 0, chunks - 1);
                return builders[ci, cj];
            }
            PathNetwork paths = Terrain.Paths;
            float OnPath(Vector2 p) => paths != null ? Terrain.PathDistance(p.x, p.y) - paths.HalfWidth : float.MaxValue;
            bool Bare(Vector2 p) => Flat(p) || Layout.LakeDepth(p) > -0.3f || Layout.Playground.Contains(p);
            float lim = halfExtent - 0.8f;

            // Short grass blades (dead or green with the ground), pebbles and bone-pale debris.
            int total = Mathf.RoundToInt(700f * (halfExtent * halfExtent) / 400f);
            for (int i = 0; i < total; i++)
            {
                var p = new Vector2(Range(-lim, lim), Range(-lim, lim));
                if (Bare(p) || OnPath(p) < -0.2f) continue;
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

            // Plant life: shrubs, ferns, tall grass, reeds in ditches and by the lake, dead shrubs, flowers, mushrooms.
            int plants = Mathf.RoundToInt(260f * (halfExtent * halfExtent) / 400f);
            for (int i = 0; i < plants; i++)
            {
                var p = new Vector2(Range(-lim, lim), Range(-lim, lim));
                if (Bare(p) || OnPath(p) < 0.2f || blocked.AnyWithin(p, 0.7f)) continue;
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
                    var go = MakeStatic("Ground", builders[ci, cj].ToMesh("Ground"), Vector3.zero, Quaternion.identity, lowPolyMaterial);
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

        void BuildPerimeter()
        {
            float h = halfExtent;
            const float seg = 4f;
            for (float t = -h; t < h - 0.01f; t += seg)
            {
                StoneWall(new Vector2(t, -h), new Vector2(t + seg, -h));
                StoneWall(new Vector2(t, h), new Vector2(t + seg, h));
                StoneWall(new Vector2(-h, t), new Vector2(-h, t + seg));
                StoneWall(new Vector2(h, t), new Vector2(h, t + seg));
            }
        }

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
        void MakeDoor(Vector2 start, Vector2 along, float width, float height, bool shutter, float baseY)
        {
            var root = new GameObject(shutter ? "Window Shutter" : "Door");
            root.SetActive(false);
            root.transform.SetParent(staticRoot, false);
            root.transform.SetLocalPositionAndRotation(new Vector3(start.x, baseY, start.y),
                Quaternion.Euler(0f, -Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg, 0f));

            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root.transform, false);
            Mesh m = LowPolyModels.Panel(rng, width, height, 0.12f);
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
                    AddBox(go, new Vector3(len * 0.5f, 0.7f, 0f), new Vector3(len, 1.4f, 0.2f));
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
                AddBox(go, new Vector3(0f, 0.3f, 0f), new Vector3(len, 0.6f, 0.6f));
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
            // Iron fence round the plot, a gate gap in the middle of the south side.
            var sides = new[] { (new Vector2(-hx, -hz), new Vector2(-1f, -hz)), (new Vector2(1f, -hz), new Vector2(hx, -hz)),
                                (new Vector2(hx, -hz), new Vector2(hx, hz)), (new Vector2(hx, hz), new Vector2(-hx, hz)), (new Vector2(-hx, hz), new Vector2(-hx, -hz)) };
            foreach (var (a0, b0) in sides)
            {
                Vector2 a = Local(a0.x, a0.y), b = Local(b0.x, b0.y);
                float len = Vector2.Distance(a, b);
                Vector2 d = (b - a) / len;
                GameObject f = Piece("Iron Fence", LowPolyModels.IronFence(rng, len), parent, new Vector3(a.x, Mathf.Min(H(a), H(b)) - 0.05f, a.y), -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                AddBox(f, new Vector3(len * 0.5f, 0.7f, 0f), new Vector3(len, 1.4f, 0.12f));
            }
            // Rows of graves facing the gate, some missing, some crooked.
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 7; col++)
                {
                    if (rng.NextDouble() < 0.18) continue;
                    float x = -hx + 1.6f + col * (g.width - 3.2f) / 6f + Range(-0.3f, 0.3f);
                    float z = hz - 1.6f - row * 2.9f + Range(-0.2f, 0.2f);
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
                Conform(gen.transform, g, 0.7f, Range(0f, 4f) * 90f, 0.85f, 0.03f);
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
                for (int k = 0; k < 3; k++)
                {
                    float a = R(0f, Mathf.PI * 2f);
                    candidates.Add(c.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * c.Radius * 0.55f);
                }
            }
            bool Ok(Vector2 p)
            {
                if (Mathf.Abs(p.x) > halfExtent - 3f || Mathf.Abs(p.y) > halfExtent - 3f) return false;
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
            int target = Mathf.RoundToInt(TreeDensity * (2f * halfExtent) * (2f * halfExtent));
            int placed = 0;
            for (int attempt = 0; attempt < target * 60 && placed < target; attempt++)
            {
                var p = new Vector2(Range(-halfExtent, halfExtent), Range(-halfExtent, halfExtent));
                float wood = Woodland(p.x, p.y);
                if ((float)rng.NextDouble() > 0.2f + 0.8f * wood) continue;
                if (!Free(p, PropFactory.TreeTrunkRadius, Mathf.Lerp(2.7f, 1.9f, wood))) continue;

                GameObject go;
                float dead = Deadness(p.x, p.y);
                bool isDead = (float)rng.NextDouble() < Mathf.Lerp(0.06f, 0.9f, dead);
                if (isDead)
                {
                    var kind = PickDeadTree();
                    // A fallen tree is 5-9 units long: keep it well clear of the paths.
                    if (kind == LowPolyModels.DeadTreeKind.Fallen && Terrain.Paths != null && Terrain.Paths.Distance(p) < 6f)
                        kind = LowPolyModels.DeadTreeKind.PineSnag;
                    int size = rng.Next(3);
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
                if (go.name.StartsWith("Fallen"))
                    Conform(go.transform, p, 1.5f, Range(0f, 360f), 0.9f, 0.05f);
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
            float[] weight = { 1.2f, 1.4f, 0.9f, 1.2f, 0.6f, 0.7f, 0.9f, 0.8f, 0.9f, 0.6f, 0.45f, 0.45f };
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
            int target = Mathf.RoundToInt(70f * (halfExtent * halfExtent) / 1600f);
            int placed = 0;
            for (int attempt = 0; attempt < target * 40 && placed < target; attempt++)
            {
                var p = new Vector2(Range(-halfExtent, halfExtent), Range(-halfExtent, halfExtent));
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
        void BuildPickups()
        {
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
            if (paths != null)
                foreach (List<Vector2> path in paths.Paths)
                {
                    if (path.Count < 4) continue;
                    for (int k = 0; k < 11; k++)
                    {
                        int s = rng.Next(1, path.Count - 2);
                        Vector2 a = path[s], b = path[s + 1], d = (b - a).normalized;
                        Vector2 at = Vector2.Lerp(a, b, Range(0f, 1f)) + new Vector2(-d.y, d.x) * (rng.Next(2) == 0 ? 1f : -1f) * (42f * MapLayout.Unit + paths.HalfWidth * 0.5f);
                        outside.Add(at);
                    }
                }
            var pool = new List<(Vector2 p, bool inside)>();
            foreach (Vector2 p in inside) pool.Add((p, true));
            foreach (Vector2 p in outside)
            {
                if (Mathf.Abs(p.x) > halfExtent - 2f || Mathf.Abs(p.y) > halfExtent - 2f) continue;
                if (Layout.LakeDepth(p) > -1f || Layout.Yard.Contains(p) || Layout.Building.Contains(p) || blocked.AnyWithin(p, 0.6f)) continue;
                pool.Add((p, false));
            }
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            Transform parent = Group("Supplies");
            var placed = new List<Vector2>();
            foreach (ItemType item in Items.All)
                for (int n = 0; n < Items.Info(item).mapCount; n++)
                {
                    int idx = -1;
                    foreach (float spacing in new[] { 160f * MapLayout.Unit, 60f * MapLayout.Unit })
                    {
                        idx = pool.FindIndex(c => placed.TrueForAll(q => (q - c.p).sqrMagnitude > spacing * spacing));
                        if (idx >= 0) break;
                    }
                    if (idx < 0) break;
                    (Vector2 p, bool indoors) = pool[idx];
                    pool.RemoveAt(idx);
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
