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
        public float halfExtent = 20f;
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
        static readonly Vector2[] Fires = { new Vector2(-1f, -9f), new Vector2(12f, -13f), new Vector2(-10f, 4f) };

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

        /// <summary>Hills and ditches with flat pads under the cabin, the spawn point and the campfires.</summary>
        public TerrainField CreateTerrain()
        {
            var f = new TerrainField(seed, halfExtent);
            f.AddPad(new Rect(Cabin.x - 1.5f, Cabin.y - 1.5f, Cabin.width + 3f, Cabin.height + 3f), 6f);
            f.AddPad(new Vector2(playerSpawn.x, playerSpawn.z), 1.5f, 4f);
            foreach (Vector2 fire in Fires) f.AddPad(fire, 1.6f, 3.5f);
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
            blockedSpots.Clear();
            Player = null;
            Wanderer = null;

            Terrain = CreateTerrain();
            TerrainField.SetActive(Terrain, transform);
            staticRoot = new GameObject("Static").transform;
            staticRoot.SetParent(transform, false);
            entityRoot = new GameObject("Entities").transform;
            entityRoot.SetParent(transform, false);

            BuildGround();
            BuildPerimeter();
            BuildCabin();
            BuildForest();
            BuildLights();
            BuildProps();
            BuildCrows();
            BuildWanderer();
            BuildPlayer();
        }

        // ---------------------------------------------------------------- ground

        /// <summary>Side of a ground chunk in design units (one mesh and collider each, for culling and fast cooking).</summary>
        public const float ChunkSize = 16f;

        void BuildGround()
        {
            float e = halfExtent + 4f;
            var ash = new Color(0.52f, 0.48f, 0.42f);
            var moss = new Color(0.30f, 0.34f, 0.25f);
            var mud = new Color(0.27f, 0.23f, 0.19f);
            var floor = new Color(0.33f, 0.25f, 0.18f);
            // The cabin floor (and a margin around it) stays regular so its planks line up with the walls.
            var flatZone = new Rect(Cabin.x - 0.5f, Cabin.y - 0.5f, Cabin.width + 1f, Cabin.height + 1f);

            // Cell size from the shared polygon budget (the player's facet size, relaxed for flat ground).
            var grid = new LowPolyMeshBuilder.TerrainGrid(e, PolyBudget.Edge(PolyBudget.Class.Ground),
                (x, z) => Terrain.Height(x, z), (x, z) => flatZone.Contains(new Vector2(x, z)) ? 0f : 1f, 0.28f, seed);
            Color GroundColor(float x, float z)
            {
                if (Cabin.Contains(new Vector2(x, z)))
                {
                    // Planks run along X, 0.5 m wide.
                    Color plank = floor * (Mathf.FloorToInt(z / 0.5f) % 2 == 0 ? 1f : 0.85f);
                    return Vary(plank, 0.03f);
                }
                float forest = Mathf.InverseLerp(-2f, -12f, x);
                float n1 = Mathf.PerlinNoise(x * 0.08f + 3.1f, z * 0.08f + 7.7f);
                float n2 = Mathf.PerlinNoise(x * 0.3f + 11f, z * 0.3f + 5f);
                Color c = Color.Lerp(ash, moss, Mathf.Clamp01(forest * 0.8f + (n1 - 0.5f) * 0.9f));
                c = Color.Lerp(c, mud, Mathf.SmoothStep(0f, 1f, (n2 - 0.55f) * 2.5f));
                return Vary(c, 0.06f);
            }

            int perChunk = Mathf.Max(1, Mathf.RoundToInt(ChunkSize / grid.Step));
            int chunks = Mathf.CeilToInt(grid.Cells / (float)perChunk);
            var builders = new LowPolyMeshBuilder[chunks, chunks];
            for (int cj = 0; cj < chunks; cj++)
            {
                for (int ci = 0; ci < chunks; ci++)
                {
                    builders[ci, cj] = new LowPolyMeshBuilder(rng);
                    builders[ci, cj].AddTerrainPatch(grid, ci * perChunk, Mathf.Min(grid.Cells, (ci + 1) * perChunk),
                        cj * perChunk, Mathf.Min(grid.Cells, (cj + 1) * perChunk), GroundColor);
                }
            }
            LowPolyMeshBuilder ChunkAt(Vector2 p)
            {
                int ci = Mathf.Clamp(Mathf.FloorToInt((p.x + e) / grid.Step / perChunk), 0, chunks - 1);
                int cj = Mathf.Clamp(Mathf.FloorToInt((p.y + e) / grid.Step / perChunk), 0, chunks - 1);
                return builders[ci, cj];
            }

            // Dead grass blades, pebbles and bone-pale debris, merged into the ground chunks.
            var grass = new Color(0.36f, 0.36f, 0.26f);
            for (int i = 0; i < 700; i++)
            {
                var p = new Vector2(Range(-halfExtent, halfExtent), Range(-halfExtent, halfExtent));
                if (flatZone.Contains(p)) continue;
                LowPolyMeshBuilder b = ChunkAt(p);
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
            for (int i = 0; i < 260; i++)
            {
                var p = new Vector2(Range(-halfExtent, halfExtent), Range(-halfExtent, halfExtent));
                if (flatZone.Contains(p)) continue;
                LowPolyMeshBuilder b = ChunkAt(p);
                float s = Range(0.05f, 0.14f);
                bool bone = rng.NextDouble() < 0.25;
                Vector3 radii = bone ? new Vector3(s * 1.8f, s * 0.4f, s * 0.5f) : new Vector3(s, s * 0.6f, s);
                Color c = bone ? new Color(0.72f, 0.69f, 0.62f) : LowPolyModels.Palette.Stone;
                b.AddBlob(new Vector3(p.x, Terrain.Height(p.x, p.y), p.y), radii, 0, 0.2f, _ => b.Jitter(c, 0.2f), true, Quaternion.Euler(0f, Range(0f, 180f), 0f));
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

        // ---------------------------------------------------------------- forest

        void BuildForest()
        {
            var trees = new List<Vector2>();
            int attempts = 0;
            while (trees.Count < 42 && attempts++ < 4000)
            {
                var p = new Vector2(Range(-halfExtent + 1.5f, -2.5f), Range(-halfExtent + 1.5f, halfExtent - 1.5f));
                if (Mathf.Abs(p.y + 2f) < 1.2f && p.x > -14f) continue;   // a path into the woods
                if (TooClose(trees, p, 2.3f)) continue;
                trees.Add(p);
            }
            // A few straggler trees around the clearing.
            Vector2[] stragglers = { new Vector2(6f, -6f), new Vector2(16f, -2f), new Vector2(16.5f, 15f), new Vector2(1f, 14f), new Vector2(-1f, 6f) };
            trees.AddRange(stragglers);

            foreach (Vector2 p in trees)
            {
                GameObject go = Prop(library != null ? library.trees : null, rng.Next(PropLibrary.TreeVariants), staticRoot,
                    () => PropFactory.CreateTree(LowPolyModels.DeadTree(rng), lowPolyMaterial));
                go.transform.SetLocalPositionAndRotation(Upright(p, PropFactory.TreeTrunkRadius), Quaternion.Euler(0f, Range(0f, 360f), 0f));
                blockedSpots.Add(p);
            }

            for (int i = 0; i < 14; i++)
            {
                var p = new Vector2(Range(-halfExtent + 2f, halfExtent - 2f), Range(-halfExtent + 2f, halfExtent - 2f));
                int variant = rng.Next(PropLibrary.RockRadii.Length);
                float radius = PropLibrary.RockRadii[variant];
                if (TooClose(blockedSpots, p, 2f) || Cabin.Overlaps(new Rect(p.x - 1.5f, p.y - 1.5f, 3f, 3f))) continue;
                if (Vector2.Distance(p, new Vector2(playerSpawn.x, playerSpawn.z)) < 3f) continue;
                GameObject go = Prop(library != null ? library.rocks : null, variant, staticRoot,
                    () => PropFactory.CreateRock(LowPolyModels.Rock(rng, radius), lowPolyMaterial, radius));
                Conform(go.transform, p, radius, Range(0f, 360f), 0.8f, 0.12f);
                blockedSpots.Add(p);
            }
        }

        // ---------------------------------------------------------------- lights and props

        void BuildLights()
        {
            foreach (Vector2 p in Fires)
            {
                GameObject go = Prop(library != null ? library.campfire : null, staticRoot,
                    () => PropFactory.CreateCampfire(LowPolyModels.Campfire(rng), glowMaterial));
                go.transform.SetLocalPositionAndRotation(Upright(p, 0.6f), Quaternion.identity);
                blockedSpots.Add(p);
            }

            Vector2[] lanterns = { new Vector2(6.2f, 2.3f), new Vector2(-7f, 13f), new Vector2(15f, -5f), new Vector2(15.5f, 16f), new Vector2(-15f, -15f), new Vector2(11.5f, 9.5f) };
            foreach (Vector2 p in lanterns)
            {
                GameObject go = Prop(library != null ? library.lantern : null, staticRoot,
                    () => PropFactory.CreateLantern(LowPolyModels.LanternPost(rng), glowMaterial));
                go.transform.SetLocalPositionAndRotation(Upright(p, 0.15f), Quaternion.Euler(0f, Range(0f, 360f), 0f));
                blockedSpots.Add(p);
            }
        }

        void BuildProps()
        {
            Vector2[] crates = { new Vector2(12f, 5f), new Vector2(11.9f, 5.9f), new Vector2(4.2f, 2.8f), new Vector2(14f, 2f) };
            foreach (Vector2 p in crates)
            {
                int variant = rng.Next(PropLibrary.CrateSizes.Length);
                float size = PropLibrary.CrateSizes[variant];
                GameObject go = Prop(library != null ? library.crates : null, variant, staticRoot,
                    () => PropFactory.CreateCrate(LowPolyModels.Crate(rng, size), lowPolyMaterial, size));
                Conform(go.transform, p, size * 0.5f, Range(-15f, 15f), 0.75f, 0.04f);
            }
        }

        // ---------------------------------------------------------------- entities

        void BuildCrows()
        {
            Vector2[] spots = { new Vector2(2f, -7f), new Vector2(4.5f, -9.5f), new Vector2(0.5f, -12f), new Vector2(6f, -3.5f), new Vector2(-3f, -6f), new Vector2(9f, -10f) };
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
