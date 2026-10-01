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
        public Material lowPolyMaterial;
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
            var b = new LowPolyMeshBuilder(rng);
            float e = halfExtent + 4f;
            var ash = new Color(0.52f, 0.48f, 0.42f);
            var moss = new Color(0.30f, 0.34f, 0.25f);
            var mud = new Color(0.27f, 0.23f, 0.19f);
            var floor = new Color(0.33f, 0.25f, 0.18f);
            // The cabin floor (and a margin around it) stays flat and regular so its planks line up with the walls.
            var flatZone = new Rect(Cabin.x - 0.5f, Cabin.y - 0.5f, Cabin.width + 1f, Cabin.height + 1f);

            b.AddFacetedGround(e, 0.7f,
                (x, z) => flatZone.Contains(new Vector2(x, z)) ? 0f : -0.05f * Mathf.PerlinNoise(x * 0.35f + 1f, z * 0.35f + 9f),
                (x, z) =>
                {
                    Color c;
                    if (Cabin.Contains(new Vector2(x, z)))
                    {
                        // Planks run along X, 0.5 m wide.
                        c = floor * (Mathf.FloorToInt(z / 0.5f) % 2 == 0 ? 1f : 0.85f);
                        return Vary(c, 0.03f);
                    }
                    float forest = Mathf.InverseLerp(-2f, -12f, x);
                    float n1 = Mathf.PerlinNoise(x * 0.08f + 3.1f, z * 0.08f + 7.7f);
                    float n2 = Mathf.PerlinNoise(x * 0.3f + 11f, z * 0.3f + 5f);
                    c = Color.Lerp(ash, moss, Mathf.Clamp01(forest * 0.8f + (n1 - 0.5f) * 0.9f));
                    c = Color.Lerp(c, mud, Mathf.SmoothStep(0f, 1f, (n2 - 0.55f) * 2.5f));
                    return Vary(c, 0.06f);
                },
                (x, z) => flatZone.Contains(new Vector2(x, z)) ? 0f : 1f, 0.28f, 0.035f);

            // Dead grass blades, pebbles and bone-pale debris, merged into the ground mesh.
            var grass = new Color(0.36f, 0.36f, 0.26f);
            for (int i = 0; i < 700; i++)
            {
                var p = new Vector2(Range(-halfExtent, halfExtent), Range(-halfExtent, halfExtent));
                if (flatZone.Contains(p)) continue;
                int blades = 2 + rng.Next(4);
                for (int k = 0; k < blades; k++)
                {
                    float h = Range(0.15f, 0.38f);
                    var root = new Vector3(p.x + Range(-0.15f, 0.15f), -0.01f, p.y + Range(-0.15f, 0.15f));
                    var tip = root + new Vector3(Range(-0.08f, 0.08f), h, Range(-0.08f, 0.08f));
                    b.AddCone(root, tip, 0.03f, 3, Vary(grass, 0.2f), 0.05f, false);
                }
            }
            for (int i = 0; i < 260; i++)
            {
                var p = new Vector2(Range(-halfExtent, halfExtent), Range(-halfExtent, halfExtent));
                if (flatZone.Contains(p)) continue;
                float s = Range(0.05f, 0.14f);
                bool bone = rng.NextDouble() < 0.25;
                Vector3 radii = bone ? new Vector3(s * 1.8f, s * 0.4f, s * 0.5f) : new Vector3(s, s * 0.6f, s);
                Color c = bone ? new Color(0.72f, 0.69f, 0.62f) : LowPolyModels.Palette.Stone;
                b.AddBlob(new Vector3(p.x, 0f, p.y), radii, 0, 0.2f, _ => b.Jitter(c, 0.2f), true, Quaternion.Euler(0f, Range(0f, 180f), 0f));
            }

            var go = MakeStatic("Ground", b.ToMesh("Ground"), Vector3.zero, Quaternion.identity, lowPolyMaterial);
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
            GameObject go = MakeStatic(name, m, new Vector3(mid.x, 0f, mid.y), rot, lowPolyMaterial);
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
            Mesh m = LowPolyModels.Panel(rng, width, height, 0.12f);
            var panel = new GameObject("Panel");
            panel.transform.SetParent(hinge, false);
            panel.transform.localPosition = new Vector3(width * 0.5f, shutter ? 0.8f : 0f, 0f);
            panel.AddComponent<MeshFilter>().sharedMesh = m;
            panel.AddComponent<MeshRenderer>().sharedMaterial = lowPolyMaterial;

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
                Mesh m = LowPolyModels.DeadTree(rng);
                GameObject go = MakeStatic("Dead Tree", m, new Vector3(p.x, 0f, p.y),
                    Quaternion.Euler(0f, Range(0f, 360f), 0f), lowPolyMaterial);
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
                Mesh m = LowPolyModels.Rock(rng, radius);
                GameObject go = MakeStatic("Rock", m, new Vector3(p.x, 0f, p.y),
                    Quaternion.Euler(0f, Range(0f, 360f), 0f), lowPolyMaterial);
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
                Mesh m = LowPolyModels.Campfire(rng);
                GameObject go = MakeStatic("Campfire", m, new Vector3(p.x, 0f, p.y), Quaternion.identity, glowMaterial);
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
                Mesh m = LowPolyModels.LanternPost(rng);
                GameObject go = MakeStatic("Lantern", m, new Vector3(p.x, 0f, p.y), Quaternion.Euler(0f, Range(0f, 360f), 0f), glowMaterial);
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
                Mesh m = LowPolyModels.Crate(rng, size);
                GameObject go = MakeStatic("Crate", m, new Vector3(p.x, 0f, p.y), Quaternion.Euler(0f, Range(-15f, 15f), 0f), lowPolyMaterial);
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
                Mesh m = LowPolyModels.Crow(rng);
                GameObject go = MakeEntity("Crow", m, new Vector3(p.x, 0f, p.y), Quaternion.Euler(0f, Range(0f, 360f), 0f));
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
            Mesh m = LowPolyModels.Humanoid(rng, LowPolyModels.Palette.Rags, LowPolyModels.Palette.PaleSkin);
            GameObject body = MakeEntity("Body", m, Vector3.zero, Quaternion.identity);
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

            Mesh m = LowPolyModels.Humanoid(rng, LowPolyModels.Palette.Coat, LowPolyModels.Palette.Skin);
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = m;
            body.AddComponent<MeshRenderer>().sharedMaterial = lowPolyMaterial;

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
