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
    /// on the ground (blinking once armed), clouds of galaxy gas (purple, pink and blue, with twinkling stars), and items
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
                return;
            }
            float t = Time.time, s = S;

            // Thrown: spinning end over end along the flight, lifted in a shallow arc.
            foreach (ThrownItem b in sim.Thrown)
            {
                if (!thrown.TryGetValue(b.Id, out View v))
                {
                    PropKind kind = b.Item == ItemType.Book ? PropKind.Book : b.Item == ItemType.Piss ? PropKind.Jar : PropKind.Bottle;
                    thrown[b.Id] = v = Make("Thrown " + b.Item, PropModels.Get(kind), 1.4f);
                }
                v.Seen = true;
                float arc = Mathf.Sin(Mathf.Clamp01(Scale.ToUnits(b.Travelled) / 900f) * Mathf.PI) * 0.6f;
                v.Go.transform.position = Ground(b.Pos, 1.2f + arc);
                Vector3 dir = new Vector3(b.Dir.x, 0f, b.Dir.y);
                v.Go.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(t * 720f + b.Id * 40f, 0f, 0f);
            }
            Sweep(thrown);

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

            // Galaxy gas: billowing purple, pink and blue, with stars twinkling in it.
            float life = Balance.Items.Trap.GasTime;
            foreach (GasCloud g in sim.Gases)
            {
                float fade = Mathf.Clamp01((life - g.Age) / 1.2f) * Mathf.Clamp01(g.Age / 0.3f);
                float radius = Scale.D(Balance.Items.Trap.GasRadius) * s * (0.6f + 0.4f * Mathf.Sqrt(Mathf.Min(1f, g.Age / 0.8f)));
                for (int i = 0; i < 24; i++)
                {
                    float h1 = Mathf.Sin(i * 12.9898f + g.Id) * 43758.5453f, r1 = h1 - Mathf.Floor(h1);
                    float h2 = Mathf.Sin(i * 78.233f + g.Id) * 12345.6789f, r2 = h2 - Mathf.Floor(h2);
                    float a = r1 * Mathf.PI * 2f + g.Age * (0.2f + r2 * 0.3f);
                    float d = Mathf.Sqrt(r2) * radius * 0.8f;
                    Vector3 pos = Ground(g.Pos, 0.4f * s) + new Vector3(Mathf.Cos(a) * d, Mathf.Sin(g.Age * 1.3f + i) * 0.1f * s, Mathf.Sin(a) * d);
                    Color c = i % 3 == 0 ? new Color(0.62f, 0.32f, 0.92f) : i % 3 == 1 ? new Color(0.95f, 0.42f, 0.78f) : new Color(0.38f, 0.52f, 0.98f);
                    c.a = 0.11f * fade;
                    Vfx.Instance.Dot(pos, radius * (0.18f + 0.14f * r1), c, true, false, Vfx.Blend.Alpha);
                }
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
