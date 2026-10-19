using System.Collections.Generic;
using UnityEngine;

namespace Vision.Effects
{
    /// <summary>
    /// The game's effects, drawn as a few dynamic meshes rebuilt each frame: particle bursts (camera-facing soft dots,
    /// with drag and gravity), expanding rings on the ground, and ribbons (trails). Each kind has a "masked" variant that,
    /// like characters, shows only inside the viewer's own light (splinters, glass, the scent trail), and a "senses"
    /// variant on the overlay layer that shows through the dark (the Burst wave). Positions are world space.
    /// </summary>
    // Last in the frame (after the camera and everything that draws effects), so this frame's effects show this frame.
    [DefaultExecutionOrder(150)]
    public sealed class Vfx : MonoBehaviour
    {
        public enum Blend { Additive, Alpha }

        /// <summary>The layer the senses overlay camera draws (and the main camera skips).</summary>
        public const int SensesLayer = 10;
        /// <summary>
        /// The layer the over-ground overlay camera draws (and the main camera skips): the laser and rounds in flight, over
        /// the ground and behind whatever stands nearer (see <see cref="SensesCamera"/> and Vision/FxOverGround).
        /// </summary>
        public const int OverGroundLayer = 11;

        struct Particle
        {
            public Vector3 Pos, Vel;
            public Color Color;
            public float Size, Life, Age, Drag, Gravity;
            /// <summary>Drawn as a short streak along its motion (sparks), and as a glow.</summary>
            public bool Streak, Glow;
            /// <summary>Grows to this many times its size over its life (smoke).</summary>
            public float Grow;
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
            public readonly List<(int start, int count, float width, bool soft)> Ribbons = new List<(int, int, float, bool)>();
            public readonly List<(Vector3 pos, float size, Color color, bool glow)> Dots = new List<(Vector3, float, Color, bool)>();
            // Laser sights queued this frame (their points in RibbonPts, one colour each).
            public readonly List<(int start, int count, float width, Color color, float seed)> Lasers = new List<(int, int, float, Color, float)>();
            /// <summary>Drawn by the over-ground camera: its ribbons face the camera.</summary>
            public bool Over;
            // Triangles for this frame (three vertices and colours each).
            public readonly List<Vector3> TriV = new List<Vector3>();
            public readonly List<Color> TriC = new List<Color>();
            public Mesh Mesh;
            public Material Material;
            public GameObject Go;
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<Vector2> U = new List<Vector2>();
            // The second uv: a laser's distance along it and its seed (zero for everything else).
            public readonly List<Vector2> U2 = new List<Vector2>();
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

        readonly Dictionary<(bool masked, bool senses, Blend blend, bool over), Batch> batches = new Dictionary<(bool, bool, Blend, bool), Batch>();

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

        /// <summary>
        /// The material of what's drawn over the ground (Vision/FxOverGround): over the ground always, behind anything
        /// else that's nearer, never masked by the light.
        /// </summary>
        public static Material MakeOverGroundMaterial(Blend blend)
        {
            Shader s = Shader.Find("Vision/FxOverGround");
            if (s == null) return null;
            var m = new Material(s) { name = $"Fx {blend} over ground", hideFlags = HideFlags.HideAndDontSave };
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", blend == Blend.Additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.renderQueue = 3000 + (blend == Blend.Additive ? 10 : 0);
            return m;
        }

        Batch Get(bool masked, bool senses, Blend blend, bool over = false)
        {
            if (over) masked = senses = false;
            var key = (masked && !senses, senses, blend, over);
            if (batches.TryGetValue(key, out Batch b)) return b;
            b = new Batch { Over = over };
            b.Go = new GameObject($"Fx {blend}{(over ? " over ground" : senses ? " senses" : masked ? " masked" : "")}");
            b.Go.transform.SetParent(transform, false);
            if (senses) b.Go.layer = SensesLayer;
            if (over) b.Go.layer = OverGroundLayer;
            b.Mesh = new Mesh { name = b.Go.name, hideFlags = HideFlags.DontSave };
            b.Mesh.MarkDynamic();
            b.Go.AddComponent<MeshFilter>().sharedMesh = b.Mesh;
            var r = b.Go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            b.Material = over ? MakeOverGroundMaterial(blend) : MakeMaterial(blend, masked && !senses);
            r.sharedMaterial = b.Material;
            batches[key] = b;
            return b;
        }

        // ---------------------------------------------------------------- the API

        /// <summary>A burst of particles from a point, flying out up to <paramref name="speed"/> world units a second.</summary>
        public void Burst(Vector3 at, int count, float speed, float life, Color color, float size = 0.08f, bool masked = true, Blend blend = Blend.Additive,
            float gravity = 0f, float drag = 2.5f, float upward = 0.3f, Color? color2 = null, bool senses = false, bool over = false)
        {
            Batch b = Get(masked && !senses, senses, blend, over);
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

        /// <summary>
        /// One particle with its own velocity: a spark (a glowing streak along its motion, falling with gravity), an
        /// ember, a smoke puff that swells as it rises (<paramref name="grow"/>), a chip of debris.
        /// </summary>
        public void Emit(Vector3 at, Vector3 velocity, float life, Color color, float size, float gravity = 0f, float drag = 1f, bool streak = false,
            bool glow = false, float grow = 1f, bool masked = true, bool senses = false, Blend blend = Blend.Additive, bool over = false)
        {
            Batch b = Get(masked, senses, blend, over);
            b.Particles.Add(new Particle { Pos = at, Vel = velocity, Color = color, Size = size, Life = life, Drag = drag, Gravity = gravity, Streak = streak, Glow = glow, Grow = grow });
            if (b.Particles.Count > 3000) b.Particles.RemoveRange(0, b.Particles.Count - 3000);
        }

        /// <summary>A ring on the ground growing from r0 to r1 over its life, fading out.</summary>
        public void RingAt(Vector3 centre, float r0, float r1, float life, Color color, float width = 0.06f, bool senses = false, bool masked = false, Blend blend = Blend.Additive)
        {
            Get(masked, senses, blend).Rings.Add(new Ring { Centre = centre, R0 = r0, R1 = r1, Life = life, Color = color, Width = width });
        }

        /// <summary>A ribbon through points (one frame only: call each frame while it shows); soft ones fade to both edges.</summary>
        public void Ribbon(IReadOnlyList<Vector3> pts, IReadOnlyList<Color> cols, float width, bool masked = true, bool senses = false, Blend blend = Blend.Additive, bool soft = false,
            bool over = false)
        {
            if (pts == null || pts.Count < 2) return;
            Batch b = Get(masked, senses, blend, over);
            int start = b.RibbonPts.Count;
            for (int i = 0; i < pts.Count; i++)
            {
                b.RibbonPts.Add(pts[i]);
                b.RibbonCols.Add(cols != null && i < cols.Count ? cols[i] : Color.white);
            }
            b.Ribbons.Add((start, pts.Count, width, soft));
        }

        /// <summary>A soft dot for this frame only (call each frame while it shows).</summary>
        public void Dot(Vector3 at, float size, Color color, bool masked = true, bool senses = false, Blend blend = Blend.Additive, bool over = false)
        {
            Get(masked, senses, blend, over).Dots.Add((at, size, color, false));
        }

        /// <summary>A soft glow for this frame only: bright in the middle, falling off smoothly (an orb, a flare, a hot spot).</summary>
        public void Glow(Vector3 at, float size, Color color, bool masked = false, bool senses = true, Blend blend = Blend.Additive, bool over = false)
        {
            Get(masked, senses, blend, over).Dots.Add((at, size, color, true));
        }

        /// <summary>
        /// A laser sight through points for this frame, drawn over the ground and behind whatever stands nearer, the way
        /// Noita draws one (Vision/FxOverGround's laser mode): <paramref name="width"/> must hold a few pixels of haze
        /// either side of its one-pixel core (<see cref="ScreenPixels"/>). <paramref name="seed"/> keeps each beam's static
        /// its own.
        /// </summary>
        public void Laser(IReadOnlyList<Vector3> pts, float width, Color color, float seed = 0f)
        {
            if (pts == null || pts.Count < 2) return;
            Batch b = Get(false, false, Blend.Additive, true);
            int start = b.RibbonPts.Count;
            // (Colours kept alongside, so the ribbons sharing these lists stay lined up.)
            for (int i = 0; i < pts.Count; i++) { b.RibbonPts.Add(pts[i]); b.RibbonCols.Add(color); }
            b.Lasers.Add((start, pts.Count, width, color, seed));
        }

        /// <summary>World units across <paramref name="pixels"/> of the screen's art pixels (a few real pixels each, as Noita's are).</summary>
        public static float ScreenPixels(Camera cam, float pixels)
        {
            if (cam == null) return 0.02f * pixels;
            float h = Mathf.Max(1f, cam.pixelHeight);
            float art = Mathf.Max(1f, Mathf.Floor(h / 540f));
            float perPx = cam.orthographic ? 2f * cam.orthographicSize / h : 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 10f / h;
            return pixels * art * perPx;
        }

        /// <summary>A flat triangle for this frame (a blood splatter is built of these).</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color color, bool masked = true, bool senses = false, Blend blend = Blend.Additive)
        {
            Batch bt = Get(masked, senses, blend);
            bt.TriV.Add(a); bt.TriV.Add(b); bt.TriV.Add(c);
            bt.TriC.Add(color); bt.TriC.Add(color); bt.TriC.Add(color);
        }

        /// <summary>A flat four-cornered shape for this frame (the Burst's lens is built of these).</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, bool masked = true, bool senses = false, Blend blend = Blend.Additive)
        {
            Batch bt = Get(masked, senses, blend);
            bt.TriV.Add(a); bt.TriV.Add(b); bt.TriV.Add(c);
            bt.TriV.Add(a); bt.TriV.Add(c); bt.TriV.Add(d);
            for (int i = 0; i < 6; i++) bt.TriC.Add(color);
        }

        /// <summary>
        /// A band between two lines of points for this frame, shaded from colour to colour along both and across (a blade's
        /// trail, a strand of something), as triangles.
        /// </summary>
        public void Strip(IReadOnlyList<Vector3> a, IReadOnlyList<Vector3> b, IReadOnlyList<Color> ca, IReadOnlyList<Color> cb, bool masked = true, bool senses = false, Blend blend = Blend.Additive)
        {
            int n = Mathf.Min(a.Count, b.Count);
            if (n < 2) return;
            Batch bt = Get(masked, senses, blend);
            for (int i = 0; i < n - 1; i++)
            {
                if (ca[i].a <= 0.001f && cb[i].a <= 0.001f && ca[i + 1].a <= 0.001f && cb[i + 1].a <= 0.001f) continue;
                bt.TriV.Add(a[i]); bt.TriV.Add(b[i]); bt.TriV.Add(b[i + 1]);
                bt.TriC.Add(ca[i]); bt.TriC.Add(cb[i]); bt.TriC.Add(cb[i + 1]);
                bt.TriV.Add(a[i]); bt.TriV.Add(b[i + 1]); bt.TriV.Add(a[i + 1]);
                bt.TriC.Add(ca[i]); bt.TriC.Add(cb[i + 1]); bt.TriC.Add(ca[i + 1]);
            }
        }

        /// <summary>A band on the ground between two radii, from an angle (radians, on the ground plane) for a sweep.</summary>
        public void Arc(Vector3 centre, float r0, float r1, float start, float sweep, Color color, bool masked = true, Blend blend = Blend.Additive, bool senses = false)
        {
            if (Mathf.Abs(sweep) < 1e-4f) return;
            int n = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(sweep) / (Mathf.PI / 16f)));
            for (int i = 0; i < n; i++)
            {
                float a0 = start + sweep * i / n, a1 = start + sweep * (i + 1) / n;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Quad(centre + d0 * r0, centre + d0 * r1, centre + d1 * r1, centre + d1 * r0, color, masked, senses, blend);
            }
        }

        /// <summary>A thin ring on the ground for this frame.</summary>
        public void Circle(Vector3 centre, float r, float width, Color color, bool masked = true, bool senses = false, Blend blend = Blend.Additive)
        {
            Arc(centre, r - width * 0.5f, r + width * 0.5f, 0f, Mathf.PI * 2f, color, masked, blend, senses);
        }

        /// <summary>A filled disc on the ground for this frame.</summary>
        public void Disc(Vector3 centre, float r, Color color, bool masked = true, bool senses = false, Blend blend = Blend.Additive)
        {
            Arc(centre, 0f, r, 0f, Mathf.PI * 2f, color, masked, blend, senses);
        }

        public void Clear()
        {
            foreach (Batch b in batches.Values)
            {
                b.TriV.Clear();
                b.TriC.Clear();
                b.Dots.Clear();
                b.Lasers.Clear();
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
            Vector3 fwd = cam != null ? cam.transform.forward : Vector3.down;
            foreach (Batch b in batches.Values)
            {
                b.V.Clear(); b.C.Clear(); b.U.Clear(); b.U2.Clear(); b.T.Clear();
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
                    float size = p.Size * (p.Grow != 1f && p.Grow > 0f ? Mathf.Lerp(1f, p.Grow, k) : 1f);
                    if (p.Streak && p.Vel.sqrMagnitude > 1e-4f)
                    {
                        // A spark: stretched along its motion on screen, a short glowing streak.
                        Vector3 v = p.Vel * 0.035f;
                        Vector3 side = Vector3.Cross(v, cam != null ? cam.transform.forward : Vector3.up).normalized * size * 0.5f;
                        StreakQuad(b, p.Pos, v, side, c);
                    }
                    else Quad(b, p.Pos, right * size, up * size, c, p.Glow);
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
                Vector3? facing = b.Over ? fwd : (Vector3?)null;
                foreach (var (start, count, width, soft) in b.Ribbons) RibbonMesh(b, b.RibbonPts, b.RibbonCols, start, count, width, soft, facing);
                b.Ribbons.Clear();
                foreach (var (start, count, width, color, seed) in b.Lasers) LaserMesh(b, b.RibbonPts, start, count, width, color, seed, fwd);
                b.Lasers.Clear();
                b.RibbonPts.Clear();
                b.RibbonCols.Clear();
                foreach (var (pos, size, color, glow) in b.Dots) Quad(b, pos, right * size, up * size, color, glow);
                b.Dots.Clear();
                for (int i = 0; i < b.TriV.Count; i++)
                {
                    b.T.Add(b.V.Count);
                    b.V.Add(b.TriV[i]);
                    b.C.Add(b.TriC[i]);
                    b.U.Add(Vector2.zero);
                }
                b.TriV.Clear();
                b.TriC.Clear();
                b.Mesh.Clear();
                if (b.V.Count == 0) continue;
                b.Mesh.indexFormat = b.V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                b.Mesh.SetVertices(b.V);
                b.Mesh.SetColors(b.C);
                b.Mesh.SetUVs(0, b.U);
                if (b.U2.Count > 0)
                {
                    while (b.U2.Count < b.V.Count) b.U2.Add(Vector2.zero);
                    b.Mesh.SetUVs(1, b.U2);
                }
                b.Mesh.SetTriangles(b.T, 0);
                b.Mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            }
        }

        static void Quad(Batch b, Vector3 c, Vector3 r, Vector3 u, Color col, bool glow = false)
        {
            int i = b.V.Count;
            b.V.Add(c - r - u); b.V.Add(c + r - u); b.V.Add(c + r + u); b.V.Add(c - r + u);
            for (int k = 0; k < 4; k++) b.C.Add(col);
            // uv.x 2-3 marks a soft round dot in the shader, 6-7 a glow.
            float x0 = glow ? 6f : 2f;
            b.U.Add(new Vector2(x0, 0f)); b.U.Add(new Vector2(x0 + 1f, 0f)); b.U.Add(new Vector2(x0 + 1f, 1f)); b.U.Add(new Vector2(x0, 1f));
            b.T.Add(i); b.T.Add(i + 2); b.T.Add(i + 1); b.T.Add(i); b.T.Add(i + 3); b.T.Add(i + 2);
        }

        /// <summary>Layers that are solid: everything but the ground, characters, raycast-ignored things and effects.</summary>
        public static int SolidMask = ~((1 << 8) | (1 << 9) | (1 << 2) | (1 << SensesLayer));
        /// <summary>Things standing upright: the solid layers less what lies low on the ground (fallen logs and trees, layer 12).</summary>
        public static int UprightMask => SolidMask & ~(1 << 12);

        /// <summary>
        /// Something standing upright (a wall, tree, rock or prop) is between the camera and <paramref name="p"/>; never the
        /// ground, nor a fallen log. Effects drawn over the terrain (the laser, rounds, thrown items) can sit under a rise in
        /// the ground, where a ray from them would pass up through the hill and catch the buried feet of the trees and
        /// rocks on it. So the point is first lifted, along its own line of sight (the same spot on screen), to
        /// <paramref name="floorY"/> (a world height just over the ground there), and only what stands in front of it
        /// from there covers it.
        /// </summary>
        public static bool Covered(Camera cam, Vector3 p, float floorY = float.NegativeInfinity)
        {
            if (cam == null) return false;
            Vector3 v = cam.WorldToViewportPoint(p);
            if (v.x < -0.1f || v.x > 1.1f || v.y < -0.1f || v.y > 1.1f) return false;
            Vector3 back = cam.orthographic ? -cam.transform.forward : (cam.transform.position - p).normalized;
            if (p.y < floorY && back.y > 1e-3f) p += back * ((floorY - p.y) / back.y);
            return Physics.Raycast(p + back * 0.05f, back, 400f, UprightMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// The ground (a rise of the terrain) is between the camera and <paramref name="p"/>, or <paramref name="p"/> is down
        /// in it (a ray from inside the ground passes out through its surface from behind, which rays don't see, so the
        /// ground above the point is checked as well).
        /// </summary>
        public static bool UnderGround(Camera cam, Vector3 p)
        {
            if (cam == null) return false;
            if (Physics.Raycast(p + Vector3.up * 500f, Vector3.down, out RaycastHit top, 500f, 1 << 8, QueryTriggerInteraction.Ignore) && top.point.y > p.y)
                return true;
            Vector3 back = cam.orthographic ? -cam.transform.forward : (cam.transform.position - p).normalized;
            return Physics.Raycast(p + back * 0.05f, back, 400f, 1 << 8, QueryTriggerInteraction.Ignore);
        }
        /// <summary>Diagnostics (the perf run's -perfNoSmoke): draw no gas.</summary>
        public static bool SkipSmoke;

        /// <summary>A streak from p back along -v, soft across (the soft-ribbon shading), bright at the head.</summary>
        static void StreakQuad(Batch b, Vector3 p, Vector3 v, Vector3 side, Color col)
        {
            int i = b.V.Count;
            Color tail = col;
            tail.a *= 0.15f;
            b.V.Add(p - v - side); b.V.Add(p - v + side); b.V.Add(p + side); b.V.Add(p - side);
            b.C.Add(tail); b.C.Add(tail); b.C.Add(col); b.C.Add(col);
            b.U.Add(new Vector2(4f, 0f)); b.U.Add(new Vector2(4f, 1f)); b.U.Add(new Vector2(4f, 1f)); b.U.Add(new Vector2(4f, 0f));
            b.T.Add(i); b.T.Add(i + 1); b.T.Add(i + 2); b.T.Add(i); b.T.Add(i + 2); b.T.Add(i + 3);
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

        static void RibbonMesh(Batch b, List<Vector3> pts, List<Color> cols, int first, int n, float w, bool soft = false, Vector3? facing = null)
        {
            int start = b.V.Count;
            for (int k = 0; k < n; k++)
            {
                Vector3 a = pts[first + Mathf.Max(0, k - 1)], c = pts[first + Mathf.Min(n - 1, k + 1)];
                Vector3 along = (c - a).normalized;
                // Flat on the ground, sideways to the path (or, drawn over the ground, facing the camera: as wide whichever
                // way it runs on screen).
                Vector3 side = Vector3.Cross(facing ?? Vector3.up, along).normalized * (w * 0.5f);
                b.V.Add(pts[first + k] - side);
                b.V.Add(pts[first + k] + side);
                Color col = cols[first + k];
                b.C.Add(col); b.C.Add(col);
                if (soft) { b.U.Add(new Vector2(4f, 0f)); b.U.Add(new Vector2(4f, 1f)); }
                else { b.U.Add(Vector2.zero); b.U.Add(Vector2.zero); }
            }
            for (int k = 0; k < n - 1; k++)
            {
                int i = start + k * 2;
                b.T.Add(i); b.T.Add(i + 1); b.T.Add(i + 3);
                b.T.Add(i); b.T.Add(i + 3); b.T.Add(i + 2);
            }
        }

        /// <summary>A laser sight's ribbon, facing the camera: uv.x 8 marks it, uv.y runs across, uv2 is (distance along, seed).</summary>
        static void LaserMesh(Batch b, List<Vector3> pts, int first, int n, float w, Color col, float seed, Vector3 facing)
        {
            while (b.U2.Count < b.V.Count) b.U2.Add(Vector2.zero);
            int start = b.V.Count;
            float along = 0f;
            for (int k = 0; k < n; k++)
            {
                if (k > 0) along += Vector3.Distance(pts[first + k - 1], pts[first + k]);
                Vector3 a = pts[first + Mathf.Max(0, k - 1)], c = pts[first + Mathf.Min(n - 1, k + 1)];
                Vector3 side = Vector3.Cross(facing, (c - a).normalized).normalized * (w * 0.5f);
                b.V.Add(pts[first + k] - side);
                b.V.Add(pts[first + k] + side);
                b.C.Add(col); b.C.Add(col);
                b.U.Add(new Vector2(8f, 0f)); b.U.Add(new Vector2(8f, 1f));
                b.U2.Add(new Vector2(along, seed)); b.U2.Add(new Vector2(along, seed));
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
