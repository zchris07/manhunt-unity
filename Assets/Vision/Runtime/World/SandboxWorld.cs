using System.Collections.Generic;
using UnityEngine;
using Vision.Player;
using Vision.Rendering;
using Vision.Visibility;

namespace Vision.World
{
    /// <summary>
    /// Builds the vision sandbox diorama (deterministic from <see cref="seed"/>): ground, a walled
    /// arena, a cabin with a door, a window shutter and an inner partition, a dead forest with rocks,
    /// campfires and lanterns (more than the 6-light cap), crows, one wandering figure and the player,
    /// then wires the player into the camera and mask renderer.
    ///
    /// The layout is in design units (local space). Scale this object to resize the world; see
    /// <see cref="WorldScale"/>.
    ///
    /// <see cref="Generate"/> works in the Editor as well as in Play mode. The level baker calls it
    /// to lay the level out in the scene and save its meshes as assets; the saved scene then has
    /// <see cref="generateOnAwake"/> off, and everything is ordinary, editable scene content.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SandboxWorld : MonoBehaviour
    {
        public int seed = 1337;
        public float halfExtent = 40f;
        [Tooltip("Build the level when Play starts. Off for a baked level that is already in the scene.")]
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
        public Vector3 playerSpawn = new Vector3(0f, 0f, -5f);

        [Header("Generated (serialized so a baked scene keeps them)")]
        public PlayerController Player;
        public Wanderer Wanderer;
        public List<Door> Doors = new List<Door>();
        public List<Transform> Crows = new List<Transform>();
        public List<Pickup> Pickups = new List<Pickup>();

        /// <summary>
        /// How a prefab becomes a scene object. Null means Object.Instantiate; the level baker sets it to
        /// PrefabUtility.InstantiatePrefab so baked objects stay linked to their prefabs.
        /// </summary>
        public System.Func<GameObject, Transform, GameObject> placeHook;

        System.Random rng;
        Transform staticRoot, entityRoot;
        readonly List<Vector2> blockedSpots = new List<Vector2>();

        // Cabin footprint (world X,Z).
        static readonly Rect Cabin = new Rect(5f, 4f, 8f, 7f);
        static readonly Vector2[] Fires = { new Vector2(-1f, -9f), new Vector2(12f, -13f), new Vector2(-10f, 4f), new Vector2(-27f, 21f), new Vector2(25f, 27f) };
        /// <summary>Where the car wrecks lie (sedan, van, pickup, sedan); paths lead to each.</summary>
        static readonly Vector2[] WreckSites = { new Vector2(-21f, -25f), new Vector2(29f, -7f), new Vector2(-7f, 31f), new Vector2(22f, 14f) };
        static readonly Vector2 GeneratorSite = new Vector2(15.5f, 8.5f);
        static readonly Vector2 CabinDoor = new Vector2(3.6f, 7.2f);
        /// <summary>Open ground around the spawn point and the wanderer's loop: no trees or rocks.</summary>
        static readonly Rect SpawnClearing = new Rect(-5.5f, -13.5f, 10f, 12f);

        /// <summary>The ground height of this level (design units, local space). Rebuilt from the seed when needed.</summary>
        public TerrainField Terrain { get; private set; }

        void Awake()
        {
            if (generateOnAwake) Generate();
        }

        void OnEnable()
        {
            Terrain ??= CreateTerrain();
            TerrainField.SetActive(Terrain, transform);
            Characters.HumanoidAnimator.Ground = TerrainField.TrySample;
        }

        void OnDisable()
        {
            TerrainField.ClearActive(transform);
            if (TerrainField.Active == null) Characters.HumanoidAnimator.Ground = null;
        }

        /// <summary>
        /// Hills and ditches with flat pads under the cabin, the spawn point, the campfires, the wrecks and the
        /// generator, and footpaths routed between them (the ground is flattened across each path).
        /// </summary>
        public TerrainField CreateTerrain()
        {
            var f = new TerrainField(seed, halfExtent);
            f.AddPad(new Rect(Cabin.x - 1.5f, Cabin.y - 1.5f, Cabin.width + 3f, Cabin.height + 3f), 6f);
            var spawn = new Vector2(playerSpawn.x, playerSpawn.z);
            f.AddPad(spawn, 1.5f, 4f);
            foreach (Vector2 fire in Fires) f.AddPad(fire, 1.6f, 3.5f);
            foreach (Vector2 wreck in WreckSites) f.AddPad(wreck, 2.6f, 5f);
            f.AddPad(GeneratorSite, 1.2f, 3f);

            var points = new List<Vector2> { spawn, CabinDoor + Vector2.left * 1.2f, GeneratorSite + Vector2.left * 1.5f };
            points.AddRange(Fires);
            foreach (Vector2 wreck in WreckSites) points.Add(wreck + Vector2.right * 3.2f);
            var cabinBlock = new Rect(Cabin.x - 0.6f, Cabin.y - 0.6f, Cabin.width + 1.2f, Cabin.height + 1.2f);
            f.SetPaths(PathNetwork.Build(points, f.Height, halfExtent - 3f, p => cabinBlock.Contains(p)));
            return f;
        }

        /// <summary>Clears and rebuilds the whole level under this object.</summary>
        [ContextMenu("Generate Level")]
        public void Generate()
        {
            rng = new System.Random(seed);
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            Doors.Clear();
            Crows.Clear();
            Pickups.Clear();
            blockedSpots.Clear();
            Player = null;
            Wanderer = null;

            Terrain = CreateTerrain();
            TerrainField.SetActive(Terrain, transform);
            staticRoot = new GameObject("Static").transform;
            staticRoot.SetParent(transform, false);
            entityRoot = new GameObject("Entities").transform;
            entityRoot.SetParent(transform, false);

            BuildPerimeter();
            BuildCabin();
            BuildTrees();
            BuildRocks();
            BuildLights();
            BuildProps();
            BuildPickups();
            BuildGround();   // last, so plants grow around everything placed
            BuildCrows();
            BuildWanderer();
            BuildPlayer();
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
            public static readonly Color Path = new Color(0.46f, 0.38f, 0.28f);
            public static readonly Color Floor = new Color(0.33f, 0.25f, 0.18f);
        }

        /// <summary>
        /// Ground colour: shades of earth (light and dark soil, a little clay, greyer where the trees are dead)
        /// blending slowly into each other, with a hint of moss where it is damp and straw where it is dry,
        /// rock grey on steep slopes, mud in the ditches and packed dirt on the paths (with ragged edges).
        /// </summary>
        public Color GroundColor(float x, float z)
        {
            if (Cabin.Contains(new Vector2(x, z)))
                return Ground.Floor * (Mathf.FloorToInt(z / 0.5f) % 2 == 0 ? 1f : 0.85f);   // planks along X, 0.5 m wide
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
            c = Color.Lerp(c, Ground.Rocky, 0.8f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(27f, 35f, Terrain.SlopeDeg(x, z))));
            c = Color.Lerp(c, Ground.Mud, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1.1f, -1.8f, Terrain.Natural(x, z))));
            PathNetwork paths = Terrain.Paths;
            if (paths != null)
            {
                float d = paths.Distance(x, z, out _);
                float edge = paths.HalfWidth + 0.15f + (n3 - 0.5f) * 0.5f;
                c = Color.Lerp(c, Ground.Path * (0.92f + 0.16f * n3), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge + 0.25f, edge - 0.25f, d)));
            }
            return c;
        }

        void BuildGround()
        {
            float e = halfExtent + 4f;
            // The cabin floor (and a margin around it) stays regular so its planks line up with the walls.
            var flatZone = new Rect(Cabin.x - 0.5f, Cabin.y - 0.5f, Cabin.width + 1f, Cabin.height + 1f);

            // Cell size from the shared polygon budget (the player's facet size, relaxed for flat ground).
            var grid = new LowPolyMeshBuilder.TerrainGrid(e, PolyBudget.Edge(PolyBudget.Class.Ground),
                (x, z) => Terrain.Height(x, z), (x, z) => flatZone.Contains(new Vector2(x, z)) ? 0f : 1f, 0.28f, seed);
            Color GroundColorVaried(float x, float z) => Vary(GroundColor(x, z), Cabin.Contains(new Vector2(x, z)) ? 0.03f : 0.05f);

            int perChunk = Mathf.Max(1, Mathf.RoundToInt(ChunkSize / grid.Step));
            int chunks = Mathf.CeilToInt(grid.Cells / (float)perChunk);
            var builders = new LowPolyMeshBuilder[chunks, chunks];
            for (int cj = 0; cj < chunks; cj++)
            {
                for (int ci = 0; ci < chunks; ci++)
                {
                    builders[ci, cj] = new LowPolyMeshBuilder(rng);
                    builders[ci, cj].AddTerrainPatch(grid, ci * perChunk, Mathf.Min(grid.Cells, (ci + 1) * perChunk),
                        cj * perChunk, Mathf.Min(grid.Cells, (cj + 1) * perChunk), GroundColorVaried);
                }
            }
            LowPolyMeshBuilder ChunkAt(Vector2 p)
            {
                int ci = Mathf.Clamp(Mathf.FloorToInt((p.x + e) / grid.Step / perChunk), 0, chunks - 1);
                int cj = Mathf.Clamp(Mathf.FloorToInt((p.y + e) / grid.Step / perChunk), 0, chunks - 1);
                return builders[ci, cj];
            }
            PathNetwork paths = Terrain.Paths;
            float OnPath(Vector2 p) => paths != null ? paths.Distance(p) - paths.HalfWidth : float.MaxValue;
            float lim = halfExtent - 0.8f;

            // Short grass blades (dead or green with the ground), pebbles and bone-pale debris.
            int total = Mathf.RoundToInt(700f * (halfExtent * halfExtent) / 400f);
            for (int i = 0; i < total; i++)
            {
                var p = new Vector2(Range(-lim, lim), Range(-lim, lim));
                if (flatZone.Contains(p) || OnPath(p) < -0.2f) continue;
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
                if (flatZone.Contains(p)) continue;
                LowPolyMeshBuilder b = ChunkAt(p);
                float s = Range(0.05f, 0.14f);
                bool bone = rng.NextDouble() < 0.2;
                Vector3 radii = bone ? new Vector3(s * 1.8f, s * 0.4f, s * 0.5f) : new Vector3(s, s * 0.6f, s);
                Color c = bone ? new Color(0.72f, 0.69f, 0.62f) : LowPolyModels.Palette.Stone;
                b.AddBlob(new Vector3(p.x, Terrain.Height(p.x, p.y), p.y), radii, 0, 0.2f, _ => b.Jitter(c, 0.2f), true, Quaternion.Euler(0f, Range(0f, 180f), 0f));
            }

            // Plant life: shrubs, ferns, tall grass, reeds in the ditches, dead shrubs, flowers, mushrooms.
            int plants = Mathf.RoundToInt(260f * (halfExtent * halfExtent) / 400f);
            for (int i = 0; i < plants; i++)
            {
                var p = new Vector2(Range(-lim, lim), Range(-lim, lim));
                if (flatZone.Contains(p) || OnPath(p) < 0.2f || TooClose(blockedSpots, p, 0.7f)) continue;
                float dead = Deadness(p.x, p.y), moist = Moisture(p.x, p.y), wood = Woodland(p.x, p.y);
                bool ditch = Terrain.Natural(p.x, p.y) < -1.3f;
                float roll = (float)rng.NextDouble();
                LowPolyMeshBuilder b = ChunkAt(p);
                Vector3 n = Terrain.Normal(p.x, p.y);
                b.Transform = Matrix4x4.TRS(new Vector3(p.x, Terrain.Height(p.x, p.y) - 0.02f, p.y),
                    Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, n, 0.5f)) * Quaternion.Euler(0f, Range(0f, 360f), 0f),
                    Vector3.one * Range(0.8f, 1.25f));
                // What grows here shifts gradually with how dead, damp and wooded the land is.
                if (ditch && roll < 0.6f) LowPolyModels.AddReeds(b, Range(0.9f, 1.4f));
                else if (roll < 0.35f * dead) LowPolyModels.AddDeadShrub(b, Range(0.5f, 0.9f));
                else if (roll < 0.5f) LowPolyModels.AddTallGrass(b, Range(0.45f, 0.85f), (float)rng.NextDouble() > moist);
                else if (roll < 0.62f + 0.12f * wood) LowPolyModels.AddBush(b, Range(0.4f, 0.7f), moist < 0.35f || dead > 0.6f);
                else if (roll < 0.78f && moist > 0.45f) LowPolyModels.AddFern(b, Range(0.45f, 0.65f));
                else if (roll < 0.9f && dead < 0.5f) LowPolyModels.AddFlowers(b);
                else LowPolyModels.AddMushrooms(b);
                b.Transform = null;
            }

            for (int cj = 0; cj < chunks; cj++)
            {
                for (int ci = 0; ci < chunks; ci++)
                {
                    var go = MakeStatic("Ground", builders[ci, cj].ToMesh("Ground"), Vector3.zero, Quaternion.identity, lowPolyMaterial);
                    go.AddComponent<MeshCollider>().sharedMesh = grid.CollisionMesh(ci * perChunk, Mathf.Min(grid.Cells, (ci + 1) * perChunk),
                        cj * perChunk, Mathf.Min(grid.Cells, (cj + 1) * perChunk), "Ground Collider");
                    go.SetActive(true);
                }
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
            Mesh m = LowPolyModels.PlankWall(rng, Vector2.Distance(a, b), height, 0.3f);
            Wall("Plank Wall", a, b, height, 0.3f, m, occludes, true);
        }

        GameObject Wall(string name, Vector2 a, Vector2 b, float height, float thickness, Mesh m, bool occludes, bool collides)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            Vector2 mid = (a + b) * 0.5f;
            Quaternion rot = Quaternion.Euler(0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 0f);
            float baseY = Mathf.Min(H(a), Mathf.Min(H(mid), H(b))) - 0.03f;
            GameObject go = MakeStatic(name, m, new Vector3(mid.x, baseY, mid.y), rot, lowPolyMaterial);
            if (collides)
            {
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, height * 0.5f, 0f);
                col.size = new Vector3(length, height, thickness);
            }
            if (occludes)
            {
                var occ = go.AddComponent<Occluder>();
                occ.shape = Occluder.Shape.Box;
                occ.size = new Vector2(length, thickness);
            }
            go.SetActive(true);
            return go;
        }

        // ---------------------------------------------------------------- cabin

        void BuildCabin()
        {
            float x0 = Cabin.xMin, x1 = Cabin.xMax, z0 = Cabin.yMin, z1 = Cabin.yMax;
            const float doorZ0 = 6.6f, doorWidth = 1.2f;
            const float winX0 = 7.5f, winWidth = 1.6f;

            // West wall with a doorway.
            PlankWall(new Vector2(x0, z0), new Vector2(x0, doorZ0));
            PlankWall(new Vector2(x0, doorZ0 + doorWidth), new Vector2(x0, z1));
            MakeDoor(new Vector2(x0, doorZ0), new Vector2(0f, 1f), doorWidth, 2.2f, false);

            // South wall with a shuttered window: a sill that blocks movement but not sight.
            PlankWall(new Vector2(x0, z0), new Vector2(winX0, z0));
            PlankWall(new Vector2(winX0 + winWidth, z0), new Vector2(x1, z0));
            PlankWall(new Vector2(winX0, z0), new Vector2(winX0 + winWidth, z0), 0.8f, false);
            MakeDoor(new Vector2(winX0, z0), new Vector2(1f, 0f), winWidth, 1.2f, true);

            PlankWall(new Vector2(x1, z0), new Vector2(x1, z1));
            PlankWall(new Vector2(x0, z1), new Vector2(x1, z1));

            // Inner partition for corners to wrap around.
            PlankWall(new Vector2(9.5f, z1), new Vector2(9.5f, 8.2f));
            blockedSpots.Add(Cabin.center);
        }

        /// <summary>
        /// A hinged panel filling a gap that starts at <paramref name="start"/> and runs along
        /// <paramref name="along"/>. Shutters sit on the sill (raised) and never unblock movement.
        /// </summary>
        void MakeDoor(Vector2 start, Vector2 along, float width, float height, bool shutter)
        {
            var root = new GameObject(shutter ? "Window Shutter" : "Door");
            root.SetActive(false);
            root.transform.SetParent(staticRoot, false);
            root.transform.SetLocalPositionAndRotation(new Vector3(start.x, H(start), start.y),
                Quaternion.Euler(0f, -Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg, 0f));

            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root.transform, false);
            Mesh m = LowPolyModels.Panel(rng, width, height, 0.12f);
            var panel = new GameObject("Panel");
            panel.transform.SetParent(hinge, false);
            panel.transform.localPosition = new Vector3(width * 0.5f, shutter ? 0.8f : 0f, 0f);
            panel.AddComponent<MeshFilter>().sharedMesh = m;
            PropFactory.NoShadows(panel.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;

            var blocker = root.AddComponent<BoxCollider>();
            blocker.center = new Vector3(width * 0.5f, 1f, 0f);
            blocker.size = new Vector3(width, 2f, 0.2f);

            var occ = root.AddComponent<Occluder>();
            occ.shape = Occluder.Shape.Box;
            occ.size = new Vector2(width, 0.12f);
            occ.offset = new Vector2(width * 0.5f, 0f);

            var door = root.AddComponent<Door>();
            door.occluder = occ;
            door.hinge = hinge;
            door.blocker = blocker;
            door.blocksMovementWhenOpen = shutter;
            Doors.Add(door);
            root.SetActive(true);
        }

        // ---------------------------------------------------------------- land character (gradual, no borders)

        float Offset => (seed % 1000) * 0.37f;

        /// <summary>0 = living evergreens, 1 = dead trees. The old arena's west side leans dead.</summary>
        public float Deadness(float x, float z)
        {
            float r = Mathf.PerlinNoise(x * 0.03f + 17.3f + Offset, z * 0.03f + 4.1f);
            if (Mathf.Abs(z) < 20f && x < 0f) r -= 0.25f * Mathf.InverseLerp(-2f, -10f, x);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.32f, r));
        }

        /// <summary>0 = open ground, 1 = thick woods: how densely trees grow.</summary>
        public float Woodland(float x, float z) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.68f, Mathf.PerlinNoise(x * 0.04f + 51.7f, z * 0.04f + 23.9f + Offset)));

        /// <summary>0 = dry, 1 = damp: grass, ferns and moss versus straw and dry scrub.</summary>
        public float Moisture(float x, float z) => Mathf.PerlinNoise(x * 0.05f + 7.1f, z * 0.05f + 2.3f);

        /// <summary>Somewhere a solid prop may stand: inside the walls, off the paths and pads, away from other props.</summary>
        bool Free(Vector2 p, float radius, float spacing)
        {
            float lim = halfExtent - 1.5f - radius;
            if (Mathf.Abs(p.x) > lim || Mathf.Abs(p.y) > lim) return false;
            var cabinZone = new Rect(Cabin.x - 2f - radius, Cabin.y - 2f - radius, Cabin.width + 4f + 2f * radius, Cabin.height + 4f + 2f * radius);
            if (cabinZone.Contains(p) || SpawnClearing.Contains(p)) return false;
            PathNetwork paths = Terrain.Paths;
            if (paths != null && paths.Distance(p) < paths.HalfWidth + radius + 0.4f) return false;
            foreach (Vector2 f in Fires) if ((f - p).sqrMagnitude < (2.8f + radius) * (2.8f + radius)) return false;
            foreach (Vector2 w in WreckSites) if ((w - p).sqrMagnitude < (4.2f + radius) * (4.2f + radius)) return false;
            if ((GeneratorSite - p).sqrMagnitude < (2f + radius) * (2f + radius)) return false;
            return !TooClose(blockedSpots, p, spacing + radius);
        }

        // ---------------------------------------------------------------- trees and rocks

        /// <summary>About 520 trees: denser where the land is wooded, dead or evergreen by <see cref="Deadness"/>.</summary>
        void BuildTrees()
        {
            int placed = 0;
            for (int attempt = 0; attempt < 90000 && placed < 520; attempt++)
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
                    // Spruce where it is damp, pine where it is dry, young firs at the edges of the woods, dying pines
                    // where the dead forest begins, ragged spiky spruces scattered through, black spruce in the wet.
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
                blockedSpots.Add(p);
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

        /// <summary>How many trees the last generation placed.</summary>
        public int TreeCount { get; private set; }

        void BuildRocks()
        {
            int placed = 0;
            for (int attempt = 0; attempt < 3000 && placed < 70; attempt++)
            {
                var p = new Vector2(Range(-halfExtent, halfExtent), Range(-halfExtent, halfExtent));
                // Small stones are common, boulders rare; steep ground gets more of them.
                float roll = (float)rng.NextDouble();
                int variant = roll < 0.45f ? rng.Next(3) : roll < 0.85f ? 3 + rng.Next(3) : 6 + rng.Next(2);
                float radius = PropLibrary.RockRadii[variant];
                float steep = Mathf.InverseLerp(10f, 30f, Terrain.SlopeDeg(p.x, p.y));
                if ((float)rng.NextDouble() > 0.3f + 0.45f * steep + 0.25f * Deadness(p.x, p.y)) continue;
                if (!Free(p, radius, 1.4f)) continue;
                if (Vector2.Distance(p, new Vector2(playerSpawn.x, playerSpawn.z)) < 3f) continue;
                GameObject go = Prop(library != null ? library.rocks : null, variant, staticRoot,
                    () => PropFactory.CreateRock(LowPolyModels.Rock(rng, radius), lowPolyMaterial, radius));
                Conform(go.transform, p, radius, Range(0f, 360f), 0.8f, 0.12f);
                blockedSpots.Add(p);
                placed++;
            }
        }

        // ---------------------------------------------------------------- lights and props

        void BuildLights()
        {
            foreach (Vector2 p in Fires)
            {
                GameObject go = Prop(library != null ? library.campfire : null, staticRoot, CampfireFromScratch);
                go.transform.SetLocalPositionAndRotation(Upright(p, 0.6f), Quaternion.identity);
                blockedSpots.Add(p);
            }

            // A lantern by the cabin, then lanterns along the paths every ~15 units, alternating sides.
            var lanterns = new List<Vector2> { new Vector2(6.2f, 2.3f) };
            PathNetwork paths = Terrain.Paths;
            if (paths != null)
            {
                int side = 1;
                foreach (List<Vector2> path in paths.Paths)
                {
                    float walked = 7f;
                    for (int i = 0; i < path.Count - 1 && lanterns.Count < 14; i++)
                    {
                        Vector2 a = path[i], b = path[i + 1];
                        float seg = Vector2.Distance(a, b);
                        walked += seg;
                        if (walked < 15f || seg < 1e-3f) continue;
                        Vector2 dir = (b - a) / seg;
                        Vector2 p = a + new Vector2(-dir.y, dir.x) * (side * (paths.HalfWidth + 0.7f));
                        side = -side;
                        if (!Free(p, 0.15f, 1.5f) || TooClose(lanterns, p, 8f)) continue;
                        lanterns.Add(p);
                        walked = 0f;
                    }
                }
            }
            foreach (Vector2 p in lanterns)
            {
                GameObject go = Prop(library != null ? library.lantern : null, staticRoot,
                    () => PropFactory.CreateLantern(LowPolyModels.LanternPost(rng), glowMaterial));
                go.transform.SetLocalPositionAndRotation(Upright(p, 0.15f), Quaternion.Euler(0f, Range(0f, 360f), 0f));
                blockedSpots.Add(p);
            }

            // A burning barrel by the second wreck.
            Vector2 barrel = WreckSites[1] + new Vector2(2.6f, 2.2f);
            GameObject drum = Prop(library != null ? library.burningBarrel : null, staticRoot, BarrelFromScratch);
            drum.transform.SetLocalPositionAndRotation(Upright(barrel, 0.3f), Quaternion.Euler(0f, Range(0f, 360f), 0f));
            blockedSpots.Add(barrel);
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

        void BuildProps()
        {
            var crates = new List<Vector2> { new Vector2(12f, 5f), new Vector2(11.9f, 5.9f), new Vector2(4.2f, 2.8f), new Vector2(14f, 2f) };
            crates.Add(WreckSites[0] + new Vector2(-2.6f, 1.2f));
            crates.Add(WreckSites[2] + new Vector2(2.4f, -1.4f));
            foreach (Vector2 p in crates)
            {
                int variant = rng.Next(PropLibrary.CrateSizes.Length);
                float size = PropLibrary.CrateSizes[variant];
                GameObject go = Prop(library != null ? library.crates : null, variant, staticRoot,
                    () => PropFactory.CreateCrate(LowPolyModels.Crate(rng, size), lowPolyMaterial, size));
                Conform(go.transform, p, size * 0.5f, Range(-15f, 15f), 0.75f, 0.04f);
                blockedSpots.Add(p);
            }

            // Car wrecks, roughly along the nearest path, abandoned at an angle.
            for (int i = 0; i < WreckSites.Length; i++)
            {
                Vector2 p = WreckSites[i];
                int kind = i % PropLibrary.CarVariants;
                float yaw = Range(0f, 360f);
                PathNetwork paths = Terrain.Paths;
                if (paths != null)
                {
                    Vector2 along = PathDirection(paths, p);
                    if (along != Vector2.zero) yaw = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg + Range(-30f, 30f);
                }
                GameObject go = Prop(library != null ? library.cars : null, kind, staticRoot,
                    () => PropFactory.CreateCar(LowPolyModels.Car(rng, kind), lowPolyMaterial, kind));
                Conform(go.transform, p, LowPolyModels.CarSize(kind).z * 0.4f, yaw, 0.9f, 0.05f);
                blockedSpots.Add(p);
            }

            GameObject gen = Prop(library != null ? library.generator : null, staticRoot,
                () => PropFactory.CreateGenerator(LowPolyModels.Generator(rng), lowPolyMaterial));
            Conform(gen.transform, GeneratorSite, 0.7f, Range(-20f, 20f) + 90f, 0.85f, 0.03f);
            blockedSpots.Add(GeneratorSite);
        }

        /// <summary>Supplies by the camps, the wrecks, the generator and in the cabin.</summary>
        void BuildPickups()
        {
            var spots = new List<Vector2> { new Vector2(10.5f, 9.6f), new Vector2(6.2f, 5.0f), GeneratorSite + new Vector2(0.2f, -1.4f) };
            foreach (Vector2 f in Fires) spots.Add(f + new Vector2(Range(-1.6f, 1.6f), Range(1.2f, 1.6f) * (rng.NextDouble() < 0.5 ? -1f : 1f)));
            foreach (Vector2 w in WreckSites)
            {
                spots.Add(w + new Vector2(Range(-2.4f, -1.6f), Range(-1.5f, 1.5f)));
                spots.Add(w + new Vector2(Range(1.6f, 2.4f), Range(-1.5f, 1.5f)));
            }
            Transform parent = new GameObject("Supplies").transform;
            parent.SetParent(staticRoot, false);
            foreach (Vector2 p in spots)
            {
                double roll = rng.NextDouble();
                var item = roll < 0.4 ? Vision.Player.ItemType.Bandage : roll < 0.75 ? Vision.Player.ItemType.Water : Vision.Player.ItemType.CannedFood;
                var go = new GameObject($"Pickup {item}");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = LowPolyModels.Item(rng, item);
                PropFactory.NoShadows(go.AddComponent<MeshRenderer>()).sharedMaterial = lowPolyMaterial;
                var pickup = go.AddComponent<Pickup>();
                pickup.item = item;
                pickup.count = item == Vision.Player.ItemType.Bandage ? 1 : 1 + rng.Next(2);
                Conform(go.transform, p, 0.1f, Range(0f, 360f), 0.8f, 0f);
                go.transform.localScale = Vector3.one * 1.6f;
                Pickups.Add(pickup);
            }
        }

        /// <summary>Direction of the path segment nearest to a point (zero when no path is near).</summary>
        static Vector2 PathDirection(PathNetwork paths, Vector2 p)
        {
            float best = float.MaxValue;
            Vector2 dir = Vector2.zero;
            foreach (List<Vector2> path in paths.Paths)
                for (int i = 0; i < path.Count - 1; i++)
                {
                    Vector2 mid = (path[i] + path[i + 1]) * 0.5f;
                    float d = (mid - p).sqrMagnitude;
                    if (d < best && (path[i + 1] - path[i]).sqrMagnitude > 1e-6f)
                    {
                        best = d;
                        dir = (path[i + 1] - path[i]).normalized;
                    }
                }
            return best < 100f ? dir : Vector2.zero;
        }

        // ---------------------------------------------------------------- entities

        void BuildCrows()
        {
            var spots = new List<Vector2> { new Vector2(2f, -7f), new Vector2(4.5f, -9.5f), new Vector2(0.5f, -12f), new Vector2(6f, -3.5f), new Vector2(-3f, -6f), new Vector2(9f, -10f) };
            for (int i = 0; i < 4; i++) spots.Add(WreckSites[i % WreckSites.Length] + new Vector2(Range(-3f, 3f), Range(2.6f, 3.4f) * (i % 2 == 0 ? 1f : -1f)));
            foreach (Vector2 p in spots)
            {
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
            go.transform.localPosition = new Vector3(3f, H(new Vector2(3f, -3f)), -3f);
            Wanderer = go.GetComponent<Wanderer>();
            Wanderer.waypoints = new[] { new Vector3(3f, 0f, -3f), new Vector3(3f, 0f, -12f), new Vector3(-4f, 0f, -12f), new Vector3(-4f, 0f, -3f) };
            // Waypoints are world positions; the layout is in design units under the scaled root.
            for (int i = 0; i < Wanderer.waypoints.Length; i++) Wanderer.waypoints[i] = transform.TransformPoint(Wanderer.waypoints[i]);
        }

        void BuildPlayer()
        {
            GameObject go = Prop(library != null ? library.player : null, transform,
                () => PropFactory.CreatePlayer(lowPolyMaterial));
            go.transform.localPosition = new Vector3(playerSpawn.x, H(new Vector2(playerSpawn.x, playerSpawn.z)) + 0.05f, playerSpawn.z);

            Player = go.GetComponent<PlayerController>();
            Player.world = this;
            if (cameraRig != null)
            {
                cameraRig.target = go.transform;
                Player.viewCamera = cameraRig.GetComponent<Camera>();
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

        /// <summary>A generated mesh object in the static root: walls and the ground, which are saved as mesh assets.</summary>
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

        static bool TooClose(List<Vector2> points, Vector2 p, float minDistance)
        {
            for (int i = 0; i < points.Count; i++)
                if ((points[i] - p).sqrMagnitude < minDistance * minDistance) return true;
            return false;
        }
    }
}
