using System.Collections.Generic;
using UnityEngine;
using Vision.Player;
using Vision.Rendering;
using Vision.Visibility;

namespace Vision.World
{
    /// <summary>
    /// Builds the vision sandbox diorama procedurally on Awake (deterministic from <see cref="seed"/>):
    /// ground, a walled arena, a cabin with a door, a window shutter and an inner partition, a dead
    /// forest with rocks, campfires and lanterns (more than the 6-light cap), crows, one wandering
    /// figure and the player. Then wires the player into the camera and mask renderer.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SandboxWorld : MonoBehaviour
    {
        public int seed = 1337;
        public float halfExtent = 20f;

        [Header("Materials")]
        public Material voxelMaterial;
        public Material entityMaterial;
        public Material glowMaterial;

        [Header("Wiring")]
        public TopDownCamera cameraRig;
        public VisionMaskRenderer maskRenderer;
        public Vector3 playerSpawn = new Vector3(0f, 0f, -5f);

        public PlayerController Player { get; private set; }
        public Wanderer Wanderer { get; private set; }
        public readonly List<Door> Doors = new List<Door>();
        public readonly List<Transform> Crows = new List<Transform>();

        System.Random rng;
        Transform staticRoot, entityRoot;
        readonly List<Vector2> blockedSpots = new List<Vector2>();

        // Cabin footprint (world X,Z).
        static readonly Rect Cabin = new Rect(5f, 4f, 8f, 7f);

        void Awake()
        {
            rng = new System.Random(seed);
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

        void BuildGround()
        {
            var b = new FlatMeshBuilder();
            const float tile = 0.25f;
            float e = halfExtent + 4f;
            var ash = new Color(0.52f, 0.48f, 0.42f);
            var moss = new Color(0.30f, 0.34f, 0.25f);
            var mud = new Color(0.27f, 0.23f, 0.19f);
            var floor = new Color(0.33f, 0.25f, 0.18f);
            for (float z = -e; z < e; z += tile)
            {
                for (float x = -e; x < e; x += tile)
                {
                    float cx = x + tile * 0.5f, cz = z + tile * 0.5f;
                    Color c;
                    if (Cabin.Contains(new Vector2(cx, cz)))
                    {
                        // Planks run along X, 0.5 m wide.
                        c = Vary(floor * (Mathf.FloorToInt(cz / 0.5f) % 2 == 0 ? 1f : 0.85f), 0.03f);
                    }
                    else
                    {
                        float forest = Mathf.InverseLerp(-2f, -12f, cx);
                        float n1 = Mathf.PerlinNoise(cx * 0.08f + 3.1f, cz * 0.08f + 7.7f);
                        float n2 = Mathf.PerlinNoise(cx * 0.3f + 11f, cz * 0.3f + 5f);
                        c = Color.Lerp(ash, moss, Mathf.Clamp01(forest * 0.8f + (n1 - 0.5f) * 0.9f));
                        c = Color.Lerp(c, mud, Mathf.SmoothStep(0f, 1f, (n2 - 0.55f) * 2.5f));
                        c = Vary(c, 0.035f);
                    }
                    b.AddQuad(new Vector3(x, 0f, z), new Vector3(x, 0f, z + tile), new Vector3(x + tile, 0f, z + tile),
                              new Vector3(x + tile, 0f, z), Vector3.up, c);
                }
            }

            // Dead grass tufts, pebbles and bone-pale debris, merged into the ground mesh.
            var grass = new Color(0.36f, 0.36f, 0.26f);
            for (int i = 0; i < 900; i++)
            {
                var p = new Vector2(Range(-halfExtent, halfExtent), Range(-halfExtent, halfExtent));
                if (Cabin.Contains(p)) continue;
                int blades = 2 + rng.Next(4);
                for (int k = 0; k < blades; k++)
                {
                    float h = Range(0.12f, 0.35f);
                    var min = new Vector3(p.x + Range(-0.15f, 0.15f), 0f, p.y + Range(-0.15f, 0.15f));
                    b.AddBox(min, new Vector3(0.04f, h, 0.04f), Vary(grass, 0.15f));
                }
            }
            for (int i = 0; i < 300; i++)
            {
                var p = new Vector2(Range(-halfExtent, halfExtent), Range(-halfExtent, halfExtent));
                if (Cabin.Contains(p)) continue;
                float s = Range(0.05f, 0.14f);
                bool bone = rng.NextDouble() < 0.25;
                Color c = bone ? new Color(0.72f, 0.69f, 0.62f) : Vary(VoxelModels.Palette.Stone, 0.2f);
                Vector3 size = bone ? new Vector3(s * 3f, s * 0.4f, s * 0.6f) : new Vector3(s, s * 0.6f, s);
                b.AddBox(new Vector3(p.x, 0f, p.y), size, c);
            }

            var go = MakeStatic("Ground", b.ToMesh("Ground"), Vector3.zero, Quaternion.identity, voxelMaterial);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, -0.5f, 0f);
            col.size = new Vector3(e * 2f, 1f, e * 2f);
            go.SetActive(true);
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
            VoxelModels.Model m = VoxelModels.StoneWall(rng, Vector2.Distance(a, b), 1.2f, 0.6f);
            Wall("Stone Wall", a, b, 1.2f, 0.6f, m, true, true);
        }

        void PlankWall(Vector2 a, Vector2 b, float height = 2.4f, bool occludes = true)
        {
            VoxelModels.Model m = VoxelModels.PlankWall(rng, Vector2.Distance(a, b), height, 0.3f);
            Wall("Plank Wall", a, b, height, 0.3f, m, occludes, true);
        }

        GameObject Wall(string name, Vector2 a, Vector2 b, float height, float thickness, VoxelModels.Model m, bool occludes, bool collides)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            Vector2 mid = (a + b) * 0.5f;
            Quaternion rot = Quaternion.Euler(0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 0f);
            GameObject go = MakeStatic(name, BuildMesh(m, name), new Vector3(mid.x, 0f, mid.y), rot, voxelMaterial);
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
            root.transform.SetPositionAndRotation(new Vector3(start.x, 0f, start.y),
                Quaternion.Euler(0f, -Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg, 0f));

            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root.transform, false);
            VoxelModels.Model m = VoxelModels.Panel(rng, width, height, 0.12f);
            var panel = new GameObject("Panel");
            panel.transform.SetParent(hinge, false);
            panel.transform.localPosition = new Vector3(width * 0.5f, shutter ? 0.8f : 0f, 0f);
            panel.AddComponent<MeshFilter>().sharedMesh = BuildMesh(m, "Panel");
            panel.AddComponent<MeshRenderer>().sharedMaterial = voxelMaterial;

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
                VoxelModels.Model m = VoxelModels.DeadTree(rng);
                GameObject go = MakeStatic("Dead Tree", BuildMesh(m, "Dead Tree"), new Vector3(p.x, 0f, p.y),
                    Quaternion.Euler(0f, Range(0f, 360f), 0f), voxelMaterial);
                var col = go.AddComponent<CapsuleCollider>();
                col.radius = 0.32f;
                col.height = 4f;
                col.center = new Vector3(0f, 2f, 0f);
                var occ = go.AddComponent<Occluder>();
                occ.shape = Occluder.Shape.Circle;
                occ.radius = 0.32f;
                occ.sides = 8;
                go.SetActive(true);
                blockedSpots.Add(p);
            }

            for (int i = 0; i < 14; i++)
            {
                var p = new Vector2(Range(-halfExtent + 2f, halfExtent - 2f), Range(-halfExtent + 2f, halfExtent - 2f));
                if (TooClose(blockedSpots, p, 2f) || Cabin.Overlaps(new Rect(p.x - 1.5f, p.y - 1.5f, 3f, 3f))) continue;
                if (Vector2.Distance(p, new Vector2(playerSpawn.x, playerSpawn.z)) < 3f) continue;
                float radius = Range(0.4f, 0.9f);
                VoxelModels.Model m = VoxelModels.Rock(rng, radius);
                GameObject go = MakeStatic("Rock", BuildMesh(m, "Rock"), new Vector3(p.x, 0f, p.y),
                    Quaternion.Euler(0f, Range(0f, 360f), 0f), voxelMaterial);
                var col = go.AddComponent<CapsuleCollider>();
                col.radius = radius * 0.85f;
                col.height = 2f;
                col.center = new Vector3(0f, 0.5f, 0f);
                var occ = go.AddComponent<Occluder>();
                occ.shape = Occluder.Shape.Circle;
                occ.radius = radius * 0.85f;
                occ.sides = 9;
                go.SetActive(true);
                blockedSpots.Add(p);
            }
        }

        // ---------------------------------------------------------------- lights and props

        void BuildLights()
        {
            Vector2[] fires = { new Vector2(-1f, -9f), new Vector2(12f, -13f), new Vector2(-10f, 4f) };
            foreach (Vector2 p in fires)
            {
                VoxelModels.Model m = VoxelModels.Campfire(rng);
                GameObject go = MakeStatic("Campfire", BuildMesh(m, "Campfire"), new Vector3(p.x, 0f, p.y), Quaternion.identity, glowMaterial);
                var light = go.AddComponent<VisionLight>();
                light.range = 6.5f;
                light.flickerAmount = 0.25f;
                light.flickerSpeed = 5f;
                go.SetActive(true);
                blockedSpots.Add(p);
            }

            Vector2[] lanterns = { new Vector2(6.2f, 2.3f), new Vector2(-7f, 13f), new Vector2(15f, -5f), new Vector2(15.5f, 16f), new Vector2(-15f, -15f), new Vector2(11.5f, 9.5f) };
            foreach (Vector2 p in lanterns)
            {
                VoxelModels.Model m = VoxelModels.LanternPost(rng);
                GameObject go = MakeStatic("Lantern", BuildMesh(m, "Lantern"), new Vector3(p.x, 0f, p.y), Quaternion.Euler(0f, Range(0f, 360f), 0f), glowMaterial);
                var light = go.AddComponent<VisionLight>();
                light.range = 4.5f;
                light.intensity = 0.85f;
                light.flickerAmount = 0.08f;
                var col = go.AddComponent<CapsuleCollider>();
                col.radius = 0.15f;
                col.height = 2f;
                col.center = new Vector3(0f, 1f, 0f);
                go.SetActive(true);
                blockedSpots.Add(p);
            }
        }

        void BuildProps()
        {
            Vector2[] crates = { new Vector2(12f, 5f), new Vector2(11.9f, 5.9f), new Vector2(4.2f, 2.8f), new Vector2(14f, 2f) };
            foreach (Vector2 p in crates)
            {
                float size = Range(0.7f, 0.9f);
                VoxelModels.Model m = VoxelModels.Crate(rng, size);
                GameObject go = MakeStatic("Crate", BuildMesh(m, "Crate"), new Vector3(p.x, 0f, p.y), Quaternion.Euler(0f, Range(-15f, 15f), 0f), voxelMaterial);
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, size * 0.5f, 0f);
                col.size = Vector3.one * size;
                var occ = go.AddComponent<Occluder>();
                occ.size = new Vector2(size, size);
                go.SetActive(true);
            }
        }

        // ---------------------------------------------------------------- entities

        void BuildCrows()
        {
            Vector2[] spots = { new Vector2(2f, -7f), new Vector2(4.5f, -9.5f), new Vector2(0.5f, -12f), new Vector2(6f, -3.5f), new Vector2(-3f, -6f), new Vector2(9f, -10f) };
            foreach (Vector2 p in spots)
            {
                VoxelModels.Model m = VoxelModels.Crow(rng);
                GameObject go = MakeEntity("Crow", BuildMesh(m, "Crow"), new Vector3(p.x, 0f, p.y), Quaternion.Euler(0f, Range(0f, 360f), 0f));
                go.SetActive(true);
                Crows.Add(go.transform);
            }
        }

        void BuildWanderer()
        {
            var root = new GameObject("Wanderer");
            root.SetActive(false);
            root.transform.SetParent(entityRoot, false);
            root.transform.position = new Vector3(3f, 0f, -3f);
            VoxelModels.Model m = VoxelModels.Humanoid(rng, VoxelModels.Palette.Rags, VoxelModels.Palette.PaleSkin);
            GameObject body = MakeEntity("Body", BuildMesh(m, "Wanderer"), Vector3.zero, Quaternion.identity);
            body.transform.SetParent(root.transform, false);
            body.SetActive(true);
            Wanderer = root.AddComponent<Wanderer>();
            Wanderer.body = body.transform;
            Wanderer.waypoints = new[] { new Vector3(3f, 0f, -3f), new Vector3(3f, 0f, -12f), new Vector3(-4f, 0f, -12f), new Vector3(-4f, 0f, -3f) };
            root.SetActive(true);
        }

        void BuildPlayer()
        {
            var root = new GameObject("Player");
            root.SetActive(false);
            root.transform.position = playerSpawn + Vector3.up * 0.05f;
            var cc = root.AddComponent<CharacterController>();
            cc.radius = 0.3f;
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.skinWidth = 0.03f;

            VoxelModels.Model m = VoxelModels.Humanoid(rng, VoxelModels.Palette.Coat, VoxelModels.Palette.Skin);
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = BuildMesh(m, "Player");
            body.AddComponent<MeshRenderer>().sharedMaterial = voxelMaterial;

            var viewer = root.AddComponent<VisionViewer>();
            Player = root.AddComponent<PlayerController>();
            Player.viewer = viewer;
            Player.body = body.transform;
            Player.world = this;
            if (cameraRig != null)
            {
                cameraRig.target = root.transform;
                Player.viewCamera = cameraRig.GetComponent<Camera>();
            }
            if (maskRenderer != null) maskRenderer.viewer = viewer;
            root.SetActive(true);
        }

        // ---------------------------------------------------------------- helpers

        GameObject MakeStatic(string name, Mesh mesh, Vector3 pos, Quaternion rot, Material mat)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(staticRoot, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        GameObject MakeEntity(string name, Mesh mesh, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(entityRoot, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = entityMaterial;
            // A shadow would give away an entity standing in the dark.
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static Mesh BuildMesh(VoxelModels.Model m, string name)
        {
            var b = new FlatMeshBuilder();
            b.AddVoxels(m.Grid, m.VoxelSize, m.Pivot);
            return b.ToMesh(name);
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
