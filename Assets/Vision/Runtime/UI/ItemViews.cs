using System.Collections.Generic;
using UnityEngine;
using Vision.Characters;
using Vision.Effects;
using Vision.Game;
using Vision.Player;
using Vision.World;

namespace Vision.UI
{
    /// <summary>
    /// The items the match has out in the world: thrown bottles, books and jars spinning through the air, gas traps set
    /// on the ground (blinking once armed), clouds of galaxy gas (swirling purple, pink and blue wisps, with twinkling stars), and items
    /// dropped on the ground. Like characters, they show only inside the viewer's own light.
    /// </summary>
    public sealed class ItemViews : MonoBehaviour
    {
        public SandboxWorld world;

        sealed class View
        {
            public GameObject Go;
            public MeshFilter Filter;
            public int Key;
            public bool Seen;
        }

        readonly Dictionary<int, View> thrown = new Dictionary<int, View>(), traps = new Dictionary<int, View>(), drops = new Dictionary<int, View>();
        readonly List<int> stale = new List<int>();
        readonly Dictionary<(ItemType, bool), Mesh> groundMeshes = new Dictionary<(ItemType, bool), Mesh>();
        Transform root;
        Material material;

        public int ThrownCount => thrown.Count;
        /// <summary>Each thrown item's flying height (the level's units), fixed when it left the hand.</summary>
        readonly Dictionary<int, float> levels = new Dictionary<int, float>();
        readonly List<int> levelGone = new List<int>();
        /// <summary>Each thrown item's drawn height (the level's units): its level, lifted where the ground rises toward it.</summary>
        readonly Dictionary<int, float> heights = new Dictionary<int, float>();
        /// <summary>How high above the ground a thrown item leaves the hand, and the least it ever shows over the ground.</summary>
        const float Lift = 1.2f, MinClear = 0.9f;

        /// <summary>The height it must show at to clear the ground here and a little ahead along its flight.</summary>
        float Clearance(Vector2 at, Vector2 dir)
        {
            float best = world.SurfaceHeight(at);
            Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.zero;
            for (int i = 1; i <= 4; i++) best = Mathf.Max(best, world.SurfaceHeight(at + d * (i * 0.75f)));
            return best + MinClear;
        }

        /// <summary>Where a thrown item is drawn, in the level's units (tests).</summary>
        public Vector3 ThrownLocal(int id) => thrown.TryGetValue(id, out View v) ? world.transform.InverseTransformPoint(v.Go.transform.position) : Vector3.zero;

        /// <summary>
        /// A rise of the ground would hide this point from the camera and nothing upright stands in front of it: it's drawn
        /// over the terrain then. (Behind a wall, tree or prop it stays in the scene, where they hide it.)
        /// </summary>
        static bool OverHill(Vector3 p)
        {
            Camera cam = Camera.main;
            return Vfx.UnderGround(cam, p) && !Vfx.Covered(cam, p);
        }
        public int TrapCount => traps.Count;
        public int DropCount => drops.Count;

        float S => world.transform.lossyScale.x;

        Vector3 Ground(Vector2 at, float up)
        {
            float h = world.GroundHeight(at);
            return world.transform.TransformPoint(new Vector3(at.x, h + up, at.y));
        }

        Mesh GroundMesh(ItemType item, bool golden)
        {
            if (groundMeshes.TryGetValue((item, golden), out Mesh m) && m != null) return m;
            m = LowPolyModels.Item(new System.Random(31 + (int)item), item, golden);
            m.hideFlags = HideFlags.DontSave;
            groundMeshes[(item, golden)] = m;
            return m;
        }

        View Make(string name, Mesh mesh, float scale)
        {
            if (root == null)
            {
                root = new GameObject("Items In Play").transform;
                root.SetParent(transform, false);
            }
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.layer = SandboxWorld.CharacterLayer;
            var f = go.AddComponent<MeshFilter>();
            f.sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.localScale = Vector3.one * scale;
            return new View { Go = go, Filter = f };
        }

        void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        void Sweep(Dictionary<int, View> views)
        {
            stale.Clear();
            foreach (var kv in views) if (!kv.Value.Seen) stale.Add(kv.Key);
            foreach (int id in stale)
            {
                Kill(views[id].Go);
                views.Remove(id);
            }
            foreach (View v in views.Values) v.Seen = false;
        }

        void LateUpdate() => Sync(Time.deltaTime);

        public void Sync(float dt)
        {
            if (world == null) return;
            MatchHost host = MatchHost.For(world);
            MatchSim sim = host != null ? host.Sim : null;
            if (material == null) material = world.entityMaterial;
            if (sim == null)
            {
                Sweep(thrown); Sweep(traps); Sweep(drops);
                levels.Clear();
                heights.Clear();
                return;
            }
            float t = Time.time, s = S;

            // Thrown: spinning end over end, flying level at the height it left the hand whatever the ground does below, until
            // it hits something. It never looks to fly into the ground: where the ground ahead rises toward it, it's lifted
            // smoothly to clear the rise (looking a little ahead, so it climbs before it gets there) and settles back to its
            // level once past. Where a hill nearer the camera would cover it, it's drawn over the terrain (on the senses
            // overlay) so no hill ever hides it; walls, trees and props still do.
            foreach (ThrownItem b in sim.Thrown)
            {
                if (!thrown.TryGetValue(b.Id, out View v))
                {
                    PropKind kind = b.Item == ItemType.Book ? PropKind.Book : b.Item == ItemType.Piss ? PropKind.Jar : PropKind.Bottle;
                    thrown[b.Id] = v = Make("Thrown " + b.Item, PropModels.Get(kind), 1.4f);
                }
                v.Seen = true;
                if (!levels.TryGetValue(b.Id, out float level)) levels[b.Id] = level = world.SurfaceHeight(b.Pos) + Lift;
                float clear = Clearance(b.Pos, b.Dir);
                float want = Mathf.Max(level, clear);
                if (!heights.TryGetValue(b.Id, out float h)) h = want;
                // Up quickly to clear a rise, back down gently to the level; never below the ground right under it.
                h = want > h ? Mathf.MoveTowards(h, want, dt * 40f) : Mathf.MoveTowards(h, want, dt * 6f);
                h = Mathf.Max(h, world.SurfaceHeight(b.Pos) + MinClear);
                heights[b.Id] = h;
                v.Go.transform.position = world.transform.TransformPoint(new Vector3(b.Pos.x, h, b.Pos.y));
                int layer = OverHill(v.Go.transform.position) ? Vfx.SensesLayer : SandboxWorld.CharacterLayer;
                if (v.Go.layer != layer) foreach (Transform c in v.Go.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = layer;
                Vector3 dir = new Vector3(b.Dir.x, 0f, b.Dir.y);
                v.Go.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(t * 720f + b.Id * 40f, 0f, 0f);
            }
            Sweep(thrown);
            if (levels.Count > thrown.Count + 8)
            {
                levelGone.Clear();
                foreach (int id in levels.Keys) if (!thrown.ContainsKey(id)) levelGone.Add(id);
                foreach (int id in levelGone) { levels.Remove(id); heights.Remove(id); }
            }

            // Traps: the canister on the ground; once armed a light blinks on it.
            foreach (Trap tr in sim.Traps)
            {
                if (!traps.TryGetValue(tr.Id, out View v))
                {
                    traps[tr.Id] = v = Make("Gas Trap", GroundMesh(ItemType.Trap, false), 1.6f);
                    v.Go.transform.position = Ground(tr.Pos, 0f);
                    v.Go.transform.rotation = Quaternion.Euler(0f, tr.Id * 47f % 360f, 0f);
                }
                v.Seen = true;
                if (tr.ArmT <= 0f && Mathf.Repeat(t * 1.5f, 1f) < 0.2f)
                    Vfx.Instance.Dot(Ground(tr.Pos, 0.26f * s), 0.06f * s, new Color(0.75f, 0.4f, 1f, 0.9f), true);
            }
            Sweep(traps);

            // Dropped items lie where they fell.
            foreach (DropItem d in sim.Drops)
            {
                if (!drops.TryGetValue(d.Id, out View v))
                {
                    drops[d.Id] = v = Make("Dropped " + d.Item, GroundMesh(d.Item, d.Golden), 1.6f);
                    v.Go.transform.position = Ground(d.Pos, 0.01f);
                    v.Go.transform.rotation = Quaternion.Euler(0f, d.Id * 71f % 360f, 0f);
                }
                v.Seen = true;
            }
            Sweep(drops);

            // Galaxy gas: a dense, billowing cloud of smoke sitting on the ground (Cold War's smoke, in purple, pink and blue),
            // spreading out as the trap bursts and thinning away at the end, with stars twinkling in it.
            float life = Balance.Items.Trap.GasTime;
            foreach (GasCloud g in sim.Gases)
            {
                float fade = Mathf.Clamp01((life - g.Age) / 1.2f) * Mathf.Clamp01(g.Age / 0.3f);
                float radiusD = Scale.D(Balance.Items.Trap.GasRadius) * (0.6f + 0.4f * Mathf.Sqrt(Mathf.Min(1f, g.Age / 0.8f)));
                float radius = radiusD * s;
                var look = new VolumetricGas.Look
                {
                    TintA = new Color(0.62f, 0.42f, 0.68f),
                    TintB = new Color(0.3f, 0.34f, 0.62f),
                    // 30% less opaque than at first.
                    Opacity = fade * 0.7f,
                    Extinction = 1.1f / s,
                    NoiseScale = 0.5f / s,
                    Erosion = 0.8f,
                    Drift = new Vector3(0.12f, 0.08f, 0.1f) * s,
                    Seed = g.Id,
                    Clumps = 0.8f,
                };
                VolumetricGas.Blob(Ground(g.Pos, 0f), radius, radius * 0.95f, look, true, false);
                for (int i = 0; i < 10; i++)
                {
                    float h = Mathf.Sin(i * 43.17f + g.Id * 3.1f) * 9631.7f, r = h - Mathf.Floor(h);
                    float a = r * Mathf.PI * 2f + i, d = (0.2f + 0.7f * ((i * 0.37f) % 1f)) * radius;
                    float tw = 0.5f + 0.5f * Mathf.Sin(t * 6f + i * 2.3f);
                    Vfx.Instance.Dot(Ground(g.Pos, 0.6f * s) + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), 0.035f * s, new Color(1f, 1f, 1f, 0.8f * tw * fade), true);
                }
            }
        }

        void OnDestroy()
        {
            foreach (var d in new[] { thrown, traps, drops })
                foreach (View v in d.Values) Kill(v.Go);
            foreach (Mesh m in groundMeshes.Values) Kill(m);
        }
    }
}
