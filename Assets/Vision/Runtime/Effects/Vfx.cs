using System.Collections.Generic;
using UnityEngine;

namespace Vision.Effects
{
    /// <summary>
    /// The game's effects, drawn as a few dynamic meshes rebuilt each frame: particle bursts (camera-facing soft dots,
    /// with drag and gravity), expanding rings on the ground, and ribbons (trails). Each kind has a "masked" variant that,
    /// like characters, shows only inside the viewer's own light (splinters, glass, the scent trail), and a "senses"
    /// variant on the overlay layer that shows through the dark (breath rings, the Burst wave). Positions are world space.
    /// </summary>
    public sealed class Vfx : MonoBehaviour
    {
        public enum Blend { Additive, Alpha }

        /// <summary>The layer the senses overlay camera draws (and the main camera skips).</summary>
        public const int SensesLayer = 10;

        struct Particle
        {
            public Vector3 Pos, Vel;
            public Color Color;
            public float Size, Life, Age, Drag, Gravity;
        }

        struct Ring
        {
            public Vector3 Centre;
            public Color Color;
            public float R0, R1, Width, Life, Age;
        }

        sealed class Batch
        {
            public readonly List<Particle> Particles = new List<Particle>();
            public readonly List<Ring> Rings = new List<Ring>();
            // Ribbons queued this frame: their points copied into shared lists (no allocation per ribbon).
            public readonly List<Vector3> RibbonPts = new List<Vector3>();
            public readonly List<Color> RibbonCols = new List<Color>();
            public readonly List<(int start, int count, float width)> Ribbons = new List<(int, int, float)>();
            public readonly List<(Vector3 pos, float size, Color color)> Dots = new List<(Vector3, float, Color)>();
            public Mesh Mesh;
            public Material Material;
            public GameObject Go;
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<Vector2> U = new List<Vector2>();
            public readonly List<int> T = new List<int>();
        }

        static Vfx instance;
        public static Vfx Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindAnyObjectByType<Vfx>();
                if (instance == null) instance = new GameObject("Vfx").AddComponent<Vfx>();
                return instance;
            }
        }

        readonly Dictionary<(bool masked, bool senses, Blend blend), Batch> batches = new Dictionary<(bool, bool, Blend), Batch>();

        public int ParticleCount
        {
            get
            {
                int n = 0;
                foreach (Batch b in batches.Values) n += b.Particles.Count;
                return n;
            }
        }

        public int RingCount
        {
            get
            {
                int n = 0;
                foreach (Batch b in batches.Values) n += b.Rings.Count;
                return n;
            }
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            foreach (Batch b in batches.Values)
            {
                if (b.Mesh != null) Destroy(b.Mesh);
                if (b.Material != null) Destroy(b.Material);
            }
        }

        public static Material MakeMaterial(Blend blend, bool masked)
        {
            Shader s = Shader.Find("Vision/Fx");
            if (s == null) return null;
            var m = new Material(s) { name = $"Fx {blend}{(masked ? " masked" : "")}", hideFlags = HideFlags.HideAndDontSave };
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", blend == Blend.Additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (masked)
            {
                m.EnableKeyword("_VISION_MASKED");
                m.SetFloat("_Masked", 1f);
            }
            m.renderQueue = 3000 + (blend == Blend.Additive ? 10 : 0);
            return m;
        }

        Batch Get(bool masked, bool senses, Blend blend)
        {
            var key = (masked && !senses, senses, blend);
            if (batches.TryGetValue(key, out Batch b)) return b;
            b = new Batch();
            b.Go = new GameObject($"Fx {blend}{(senses ? " senses" : masked ? " masked" : "")}");
            b.Go.transform.SetParent(transform, false);
            if (senses) b.Go.layer = SensesLayer;
            b.Mesh = new Mesh { name = b.Go.name, hideFlags = HideFlags.DontSave };
            b.Mesh.MarkDynamic();
            b.Go.AddComponent<MeshFilter>().sharedMesh = b.Mesh;
            var r = b.Go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            b.Material = MakeMaterial(blend, masked && !senses);
            r.sharedMaterial = b.Material;
            batches[key] = b;
            return b;
        }

        // ---------------------------------------------------------------- the API

        /// <summary>A burst of particles from a point, flying out up to <paramref name="speed"/> world units a second.</summary>
        public void Burst(Vector3 at, int count, float speed, float life, Color color, float size = 0.08f, bool masked = true, Blend blend = Blend.Additive,
            float gravity = 0f, float drag = 2.5f, float upward = 0.3f, Color? color2 = null)
        {
            Batch b = Get(masked, false, blend);
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * upward + (1f - upward) * dir.y * 0.3f;
                var p = new Particle
                {
                    Pos = at,
                    Vel = dir.normalized * speed * Random.Range(0.35f, 1f),
                    Color = color2.HasValue ? Color.Lerp(color, color2.Value, Random.value) : color,
                    Size = size * Random.Range(0.6f, 1.3f),
                    Life = life * Random.Range(0.6f, 1.1f),
                    Drag = drag,
                    Gravity = gravity,
                };
                b.Particles.Add(p);
            }
            if (b.Particles.Count > 3000) b.Particles.RemoveRange(0, b.Particles.Count - 3000);
        }

        /// <summary>A ring on the ground growing from r0 to r1 over its life, fading out.</summary>
        public void RingAt(Vector3 centre, float r0, float r1, float life, Color color, float width = 0.06f, bool senses = false, bool masked = false, Blend blend = Blend.Additive)
        {
            Get(masked, senses, blend).Rings.Add(new Ring { Centre = centre, R0 = r0, R1 = r1, Life = life, Color = color, Width = width });
        }

        /// <summary>A ribbon through points (one frame only: call each frame while it shows).</summary>
        public void Ribbon(IReadOnlyList<Vector3> pts, IReadOnlyList<Color> cols, float width, bool masked = true, bool senses = false, Blend blend = Blend.Additive)
        {
            if (pts == null || pts.Count < 2) return;
            Batch b = Get(masked, senses, blend);
            int start = b.RibbonPts.Count;
            for (int i = 0; i < pts.Count; i++)
            {
                b.RibbonPts.Add(pts[i]);
                b.RibbonCols.Add(cols != null && i < cols.Count ? cols[i] : Color.white);
            }
            b.Ribbons.Add((start, pts.Count, width));
        }

        /// <summary>A soft dot for this frame only (call each frame while it shows).</summary>
        public void Dot(Vector3 at, float size, Color color, bool masked = true, bool senses = false, Blend blend = Blend.Additive)
        {
            Get(masked, senses, blend).Dots.Add((at, size, color));
        }

        public void Clear()
        {
            foreach (Batch b in batches.Values)
            {
                b.Dots.Clear();
                b.Particles.Clear();
                b.Rings.Clear();
                b.Ribbons.Clear();
                b.RibbonPts.Clear();
                b.RibbonCols.Clear();
            }
        }

        // ---------------------------------------------------------------- per frame

        void LateUpdate() => Step(Time.deltaTime, Camera.main);

        public void Step(float dt, Camera cam)
        {
            Vector3 right = cam != null ? cam.transform.right : Vector3.right, up = cam != null ? cam.transform.up : Vector3.up;
            foreach (Batch b in batches.Values)
            {
                b.V.Clear(); b.C.Clear(); b.U.Clear(); b.T.Clear();
                for (int i = b.Particles.Count - 1; i >= 0; i--)
                {
                    Particle p = b.Particles[i];
                    p.Age += dt;
                    if (p.Age >= p.Life) { b.Particles.RemoveAt(i); continue; }
                    p.Vel *= Mathf.Exp(-p.Drag * dt);
                    p.Vel.y -= p.Gravity * dt;
                    p.Pos += p.Vel * dt;
                    b.Particles[i] = p;
                    float k = p.Age / p.Life;
                    Color c = p.Color;
                    c.a *= 1f - k * k;
                    Quad(b, p.Pos, right * p.Size, up * p.Size, c);
                }
                for (int i = b.Rings.Count - 1; i >= 0; i--)
                {
                    Ring r = b.Rings[i];
                    r.Age += dt;
                    if (r.Age >= r.Life) { b.Rings.RemoveAt(i); continue; }
                    b.Rings[i] = r;
                    float k = r.Age / r.Life;
                    float rad = Mathf.Lerp(r.R0, r.R1, 1f - (1f - k) * (1f - k));
                    Color c = r.Color;
                    c.a *= 1f - k;
                    RingMesh(b, r.Centre, rad, r.Width, c);
                }
                foreach (var (start, count, width) in b.Ribbons) RibbonMesh(b, b.RibbonPts, b.RibbonCols, start, count, width);
                b.Ribbons.Clear();
                b.RibbonPts.Clear();
                b.RibbonCols.Clear();
                foreach (var (pos, size, color) in b.Dots) Quad(b, pos, right * size, up * size, color);
                b.Dots.Clear();
                b.Mesh.Clear();
                if (b.V.Count == 0) continue;
                b.Mesh.indexFormat = b.V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                b.Mesh.SetVertices(b.V);
                b.Mesh.SetColors(b.C);
                b.Mesh.SetUVs(0, b.U);
                b.Mesh.SetTriangles(b.T, 0);
                b.Mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            }
        }

        static void Quad(Batch b, Vector3 c, Vector3 r, Vector3 u, Color col)
        {
            int i = b.V.Count;
            b.V.Add(c - r - u); b.V.Add(c + r - u); b.V.Add(c + r + u); b.V.Add(c - r + u);
            for (int k = 0; k < 4; k++) b.C.Add(col);
            // uv.x 2-3 marks a soft round dot in the shader.
            b.U.Add(new Vector2(2f, 0f)); b.U.Add(new Vector2(3f, 0f)); b.U.Add(new Vector2(3f, 1f)); b.U.Add(new Vector2(2f, 1f));
            b.T.Add(i); b.T.Add(i + 2); b.T.Add(i + 1); b.T.Add(i); b.T.Add(i + 3); b.T.Add(i + 2);
        }

        static void RingMesh(Batch b, Vector3 c, float r, float w, Color col)
        {
            const int n = 32;
            int start = b.V.Count;
            for (int k = 0; k <= n; k++)
            {
                float a = k * Mathf.PI * 2f / n;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                b.V.Add(c + d * (r - w * 0.5f));
                b.V.Add(c + d * (r + w * 0.5f));
                Color inner = col;
                inner.a *= 0.6f;
                b.C.Add(inner); b.C.Add(col);
                b.U.Add(Vector2.zero); b.U.Add(Vector2.zero);
            }
            for (int k = 0; k < n; k++)
            {
                int i = start + k * 2;
                b.T.Add(i); b.T.Add(i + 1); b.T.Add(i + 3);
                b.T.Add(i); b.T.Add(i + 3); b.T.Add(i + 2);
            }
        }

        static void RibbonMesh(Batch b, List<Vector3> pts, List<Color> cols, int first, int n, float w)
        {
            int start = b.V.Count;
            for (int k = 0; k < n; k++)
            {
                Vector3 a = pts[first + Mathf.Max(0, k - 1)], c = pts[first + Mathf.Min(n - 1, k + 1)];
                Vector3 along = (c - a).normalized;
                // Flat on the ground, sideways to the path.
                Vector3 side = Vector3.Cross(Vector3.up, along).normalized * (w * 0.5f);
                b.V.Add(pts[first + k] - side);
                b.V.Add(pts[first + k] + side);
                Color col = cols[first + k];
                b.C.Add(col); b.C.Add(col);
                b.U.Add(Vector2.zero); b.U.Add(Vector2.zero);
            }
            for (int k = 0; k < n - 1; k++)
            {
                int i = start + k * 2;
                b.T.Add(i); b.T.Add(i + 1); b.T.Add(i + 3);
                b.T.Add(i); b.T.Add(i + 3); b.T.Add(i + 2);
            }
        }
    }
}
