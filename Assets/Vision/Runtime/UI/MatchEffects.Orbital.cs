using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vision.Effects;
using Vision.Game;
using Vision.Visibility;
using Vision.World;

namespace Vision.UI
{
    /// <summary>
    /// The K-pop orbital strike (<see cref="OrbitalStrike"/>), seen by everyone, light or dark, modelled on an engraving
    /// laser burning into a surface (the user's reference clip), only far bigger. All of it is drawn by the over-ground
    /// camera (after the darkness, keeping the scene's depth), and everything that touches the ground is draped over the
    /// terrain's own surface, vertex by vertex, so a slope never slices it off in a straight line.
    /// <list type="bullet">
    /// <item>Counting down: pink target rings draped over the ground closing in on the locked player; for its last second
    /// a hair-thin white line from the sky (the reference's start-up), and the laser's sound.</item>
    /// <item>The beam: a real solid cylinder of pure white plasma as wide as Zach (opaque, HDR emission caught by a bloom
    /// that's on only while a strike burns), plasma streaming down inside it, wrapped in a tight ionised corona that tears
    /// and flickers and fades to pink at its edge (Beer-Lambert falloff, see Vision/StrikeBeam); pulsing rings round it
    /// racing down and spreading over the ground; streaks of light racing up it; arcs crackling off it.</item>
    /// <item>Where it meets the ground: a molten white contact patch draped over the surface in a pink bloom, a dome of
    /// light over it (soft glows faded against the scene's depth), a dense low spray of white-hot streaks, molten
    /// droplets, specks of debris, and vapour curling up off it (curl-noise advection), lingering after it's gone.</item>
    /// <item>Behind it as it follows its target, the ground it touched burning bright, white-hot to orange to dull red,
    /// for a second, then charred and smoking a while (it stays for the match).</item>
    /// </list>
    /// </summary>
    public sealed partial class MatchEffects
    {
        static readonly Color StrikeGlow = new Color(0.95f, 0.14f, 0.6f, 1f);
        /// <summary>The scorch blots left behind (where, how wide, when), the most kept, and how often one's laid.</summary>
        public const int MaxScorch = 240;
        public const float ScorchEvery = 0.35f, ScorchSmoke = 8f;
        /// <summary>How long the ground the beam touched burns bright before it's only char and smoke.</summary>
        public const float ScorchBurn = 1f;
        /// <summary>How far into the laser's sound file its impact comes (s): it's started that long before the beam lands.</summary>
        public const float LaserImpactAt = Balance.Items.Orbital.LaserImpactAt;
        /// <summary>The start-up line shows for the countdown's last this many seconds; the beam swells and narrows this fast.</summary>
        public const float StartupLine = 0.9f, BeamSwell = 0.15f, BeamNarrow = 0.6f;
        /// <summary>
        /// The white cylinder's radius (local units): as wide as Zach's figure as it's drawn (about a third of the width the
        /// match gives his body to collide with), measured against him on screen.
        /// </summary>
        public static float CoreRadius => Scale.D(Balance.HunterWidth) * 0.5f * 0.32f;
        /// <summary>How long the vapour keeps rising off the spot once the beam's gone.</summary>
        public const float VapourAfter = 3f;

        readonly List<(Vector2 at, float r, int seed, float born)> scorch = new List<(Vector2, float, int, float)>();
        readonly Dictionary<int, Vector2> lastScorch = new Dictionary<int, Vector2>();
        readonly Dictionary<int, float> strikeEmit = new Dictionary<int, float>();
        readonly Dictionary<int, VisionLight> strikeLights = new Dictionary<int, VisionLight>();
        readonly HashSet<int> strikesSeen = new HashSet<int>();
        readonly HashSet<int> laserCued = new HashSet<int>();
        /// <summary>Shockwave rings spreading over the ground (where, when, how far).</summary>
        readonly List<(Vector2 at, float born, float reach)> shockwaves = new List<(Vector2, float, float)>();
        /// <summary>Where each strike was last, so its vapour keeps rising there a while after it's gone.</summary>
        readonly List<(Vector2 at, float ended)> afterVapour = new List<(Vector2, float)>();
        readonly Dictionary<int, Vector2> lastStrikePos = new Dictionary<int, Vector2>();
        readonly Vector3[] drapeRing = new Vector3[40], drapePrev = new Vector3[40];
        GameObject scorchObject;
        MatchSim scorchSim;
        bool scorchDirty;
        static Mesh beamMesh;
        static Material beamCore, beamCorona;
        MaterialPropertyBlock beamProps;
        Volume strikeBloom;
        float bloomWeight;

        /// <summary>How many scorch blots lie about, and the strike lights up (tests).</summary>
        public int ScorchCount => scorch.Count;
        public int StrikeLights => strikeLights.Count;
        /// <summary>How strongly the strike's bloom is on (0 with no strike burning; tests).</summary>
        public float StrikeBloomWeight => bloomWeight;

        void OrbitalStrikes(MatchSim sim)
        {
            if (sim != scorchSim)
            {
                scorchSim = sim;
                scorch.Clear();
                lastScorch.Clear();
                shockwaves.Clear();
                afterVapour.Clear();
                scorchDirty = true;
            }
            float s = S, now = Time.time, dt = Time.deltaTime;
            float r = Scale.D(Balance.Items.Orbital.Radius);
            strikesSeen.Clear();
            bool burning = false;
            foreach (OrbitalStrike st in sim.Strikes)
            {
                strikesSeen.Add(st.Id);
                if (!st.Firing)
                {
                    // The laser's sound starts as it powers up, its impact (LaserImpactAt s into the file) landing with the beam.
                    if (st.Countdown <= LaserImpactAt && laserCued.Add(st.Id)) Audio.PlayCue("orbital_laser", Units(st.Pos));
                    StrikeCountdown(st, r, s, now);
                    continue;
                }
                burning = true;
                lastStrikePos[st.Id] = st.Pos;
                StrikeBeam(st, r, s, now, dt);
                StrikeLight(st, r);
                // The charred trail it leaves.
                if (!lastScorch.TryGetValue(st.Id, out Vector2 last) || (st.Pos - last).magnitude > ScorchEvery)
                {
                    lastScorch[st.Id] = st.Pos;
                    scorch.Add((st.Pos + Random.insideUnitCircle * r * 0.15f, r * Random.Range(0.45f, 0.62f), Random.Range(0, 100000), now));
                    while (scorch.Count > MaxScorch) scorch.RemoveAt(0);
                    scorchDirty = true;
                }
            }
            // Strikes over: their lights out, their vapour rising a while longer where they ended.
            List<int> gone = null;
            foreach (var kv in lastStrikePos) if (!strikesSeen.Contains(kv.Key)) (gone ??= new List<int>()).Add(kv.Key);
            if (gone != null)
                foreach (int id in gone)
                {
                    afterVapour.Add((lastStrikePos[id], now));
                    lastStrikePos.Remove(id);
                    if (strikeLights.TryGetValue(id, out VisionLight l) && l != null) { if (Application.isPlaying) Destroy(l.gameObject); else DestroyImmediate(l.gameObject); }
                    strikeLights.Remove(id);
                    lastScorch.Remove(id);
                    laserCued.Remove(id);
                    strikeEmit.Remove(id);
                    strikeEmit.Remove(-id);
                }
            StrikeBloom(burning, dt);
            Shockwaves(now, s);
            AfterVapour(r, s, now, dt);
            ScorchBurning(s, now, dt);
            if (scorchDirty) DrawScorch();
        }

        // ---------------------------------------------------------------- draped over the ground

        /// <summary>
        /// A glow draped over the ground (a disc of rings, every vertex on the terrain's own surface): <paramref name="centre"/>
        /// in the middle falling off smoothly to <paramref name="edge"/> (usually clear) at <paramref name="radius"/>.
        /// </summary>
        void DrapedDisc(Vector2 at, float radius, Color centre, Color edge, float lift = 0.03f, int rings = 4, int segs = 20, float wobble = 0f, float seed = 0f)
        {
            if (Fx == null || radius <= 0f || (centre.a <= 0.002f && edge.a <= 0.002f)) return;
            segs = Mathf.Min(segs, drapeRing.Length - 1);
            Vector3 mid = OnSurface(at, lift);
            Color prevCol = centre;
            for (int k = 1; k <= rings; k++)
            {
                float t = k / (float)rings;
                // (Falling off smoothly: a glow's profile, brightest in the middle.)
                Color col = Color.Lerp(centre, edge, t * t * (3f - 2f * t));
                for (int j = 0; j <= segs; j++)
                {
                    float a = j * Mathf.PI * 2f / segs;
                    float rr = radius * t * (1f + wobble * (Mathf.PerlinNoise(seed + (j % segs) * 0.37f, Time.time * 2.3f + k) - 0.5f));
                    drapeRing[j] = OnSurface(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr, lift);
                }
                for (int j = 0; j < segs; j++)
                {
                    if (k == 1) Fx.TriOver(mid, drapeRing[j], drapeRing[j + 1], centre, col, col);
                    else
                    {
                        Fx.TriOver(drapePrev[j], drapeRing[j], drapeRing[j + 1], prevCol, col, col);
                        Fx.TriOver(drapePrev[j], drapeRing[j + 1], drapePrev[j + 1], prevCol, col, prevCol);
                    }
                }
                System.Array.Copy(drapeRing, drapePrev, segs + 1);
                prevCol = col;
            }
        }

        /// <summary>A ring lying over the ground (every vertex on the terrain's surface, <paramref name="lift"/> above it), soft both sides.</summary>
        void DrapedRing(Vector2 at, float radius, float width, Color col, float lift = 0.04f, int segs = 32)
        {
            if (Fx == null || radius <= 0f || col.a <= 0.002f) return;
            Color clear = new Color(col.r, col.g, col.b, 0f);
            float r0 = Mathf.Max(0f, radius - width * 0.5f), r1 = radius + width * 0.5f;
            Vector3 pi = default, pm = default, po = default;
            for (int j = 0; j <= segs; j++)
            {
                float a = j * Mathf.PI * 2f / segs;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector3 vi = OnSurface(at + d * r0, lift), vm = OnSurface(at + d * radius, lift), vo = OnSurface(at + d * r1, lift);
                if (j > 0)
                {
                    Fx.TriOver(pi, vi, vm, clear, clear, col); Fx.TriOver(pi, vm, pm, clear, col, col);
                    Fx.TriOver(pm, vm, vo, col, col, clear); Fx.TriOver(pm, vo, po, col, clear, clear);
                }
                pi = vi; pm = vm; po = vo;
            }
        }

        // ---------------------------------------------------------------- counting down

        void StrikeCountdown(OrbitalStrike st, float r, float s, float now)
        {
            if (Fx == null) return;
            float k = 1f - st.Countdown / Balance.Items.Orbital.Countdown;
            // Target rings closing in on them, faster as it nears, draped over the ground.
            for (int i = 0; i < 3; i++)
            {
                float u = Mathf.Repeat(now * Mathf.Lerp(0.9f, 2.4f, k) + i / 3f, 1f);
                DrapedRing(st.Pos, r * Mathf.Lerp(2.2f, 0.3f, u), 0.12f, new Color(1f, 0.35f, 0.72f, 0.7f * (1f - u) * (0.4f + 0.6f * k)));
            }
            DrapedDisc(st.Pos, r * 0.9f, new Color(1f, 0.3f, 0.7f, (0.15f + 0.25f * k) * (0.8f + 0.2f * Mathf.Sin(now * 12f))), new Color(1f, 0.3f, 0.7f, 0f));
            // Its last second: the start-up, a hair-thin white line from the sky to the spot (dropping in first).
            if (st.Countdown <= StartupLine)
            {
                float into = (StartupLine - st.Countdown) / StartupLine;
                float drop = Mathf.Clamp01(into / 0.3f);
                DrawBeam(st, 0.025f, drop, 1f, 0.9f, 0.4f);
                if (drop >= 1f) Fx.Glow(OnSurface(st.Pos, 0.15f), 0.35f * s, new Color(1f, 0.9f, 0.97f, 0.9f), false, false, Vfx.Blend.Additive, true, true);
            }
        }

        // ---------------------------------------------------------------- the beam

        static Mesh BeamMesh()
        {
            if (beamMesh != null) return beamMesh;
            const int segs = 24;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int j = 0; j <= segs; j++)
            {
                float a = j * Mathf.PI * 2f / segs;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int y = 0; y <= 1; y++)
                {
                    v.Add(d + Vector3.up * y);
                    n.Add(d);
                    uv.Add(new Vector2(j / (float)segs, y));
                }
            }
            // (Wound so the outside faces out: the corona draws only its near side.)
            for (int j = 0; j < segs; j++)
            {
                int i = j * 2;
                t.Add(i); t.Add(i + 1); t.Add(i + 3);
                t.Add(i); t.Add(i + 3); t.Add(i + 2);
            }
            beamMesh = new Mesh { name = "Strike Beam", hideFlags = HideFlags.DontSave };
            beamMesh.SetVertices(v); beamMesh.SetNormals(n); beamMesh.SetUVs(0, uv); beamMesh.SetTriangles(t, 0);
            beamMesh.RecalculateBounds();
            return beamMesh;
        }

        static void BeamMaterials()
        {
            if (beamCore != null) return;
            Shader sh = Shader.Find("Vision/StrikeBeam");
            if (sh == null) return;
            beamCore = new Material(sh) { name = "Strike Core", hideFlags = HideFlags.DontSave, renderQueue = 2450 };
            beamCore.SetFloat("_SrcBlend", (float)BlendMode.One);
            beamCore.SetFloat("_DstBlend", (float)BlendMode.Zero);
            beamCore.SetFloat("_ZWrite", 1f);
            beamCorona = new Material(sh) { name = "Strike Corona", hideFlags = HideFlags.DontSave, renderQueue = 3020 };
            beamCorona.SetFloat("_SrcBlend", (float)BlendMode.One);
            beamCorona.SetFloat("_DstBlend", (float)BlendMode.One);
            beamCorona.SetFloat("_ZWrite", 0f);
            beamCorona.EnableKeyword("_STRIKE_CORONA");
            beamCorona.SetFloat("_Corona", 1f);
        }

        /// <summary>
        /// The beam's cylinder: <paramref name="radius"/> (local units), dropped <paramref name="drop"/> of the way from the
        /// sky to the ground, its core's HDR strength <paramref name="intensity"/>, its corona this much wider again at
        /// <paramref name="coronaStrength"/>.
        /// </summary>
        void DrawBeam(OrbitalStrike st, float radius, float drop, float intensity, float coronaWidth, float coronaStrength)
        {
            BeamMaterials();
            if (beamCore == null || radius <= 0f || drop <= 0f) return;
            if (beamProps == null) beamProps = new MaterialPropertyBlock();
            const float sky = 40f;
            float ground = world.SurfaceHeight(st.Pos) - 0.2f;
            float top = ground + sky, bottom = Mathf.Lerp(top, ground, drop);
            float sc = world.transform.lossyScale.x;
            Vector3 foot = world.transform.TransformPoint(new Vector3(st.Pos.x, bottom, st.Pos.y));
            float h = Mathf.Max(0.01f, (top - bottom) * sc);
            beamProps.Clear();
            beamProps.SetFloat("_Seed", st.Id * 0.137f);
            beamProps.SetFloat("_Intensity", 3f * intensity);
            Graphics.DrawMesh(BeamMesh(), Matrix4x4.TRS(foot, Quaternion.identity, new Vector3(radius * sc, h, radius * sc)), beamCore, Vfx.OverGroundLayer, null, 0, beamProps, false, false);
            beamProps.SetFloat("_Intensity", 1.6f * coronaStrength);
            float cr = radius * (1f + coronaWidth);
            Graphics.DrawMesh(BeamMesh(), Matrix4x4.TRS(foot, Quaternion.identity, new Vector3(cr * sc, h, cr * sc)), beamCorona, Vfx.OverGroundLayer, null, 0, beamProps, false, false);
        }

        void StrikeBeam(OrbitalStrike st, float r, float s, float now, float dt)
        {
            if (Fx == null) return;
            float core = CoreRadius;   // the white cylinder: as wide as Zach himself
            float left = Balance.Items.Orbital.Duration - st.T;
            // Swelling from the start-up line to its full width as it lands, narrowing back to a line as it ends.
            float swell = Mathf.SmoothStep(0f, 1f, st.T / BeamSwell) * Mathf.SmoothStep(0f, 1f, left / BeamNarrow);
            float width = Mathf.Lerp(0.025f, core, swell);
            float flicker = 0.93f + 0.07f * Mathf.Sin(now * 53f + st.Id) * Mathf.Sin(now * 31f);
            DrawBeam(st, width * (0.97f + 0.03f * flicker), 1f, 1f + swell, 0.75f, 0.45f + 0.45f * swell);
            if (swell < 0.05f) return;
            Vector3 g = OnSurface(st.Pos, 0.05f);
            Vector2 at = st.Pos;

            // The contact patch: molten white, draped over the ground, in a pink bloom spreading over it.
            DrapedDisc(at, core * 1.15f * swell, new Color(1f, 1f, 1f, 1f), new Color(1f, 0.75f, 0.9f, 0f), 0.04f, 3, 20, 0.25f, st.Id);
            DrapedDisc(at, r * 2.2f * swell, new Color(1f, 0.6f, 0.88f, 0.55f * flicker), new Color(StrikeGlow.r, StrikeGlow.g, StrikeGlow.b, 0f), 0.035f, 5, 24, 0.12f, st.Id + 7f);
            DrapedDisc(at, r * 4f * swell, new Color(1f, 0.45f, 0.8f, 0.09f), new Color(StrikeGlow.r, StrikeGlow.g, StrikeGlow.b, 0f), 0.03f, 4, 24);
            // The dome of light over it (soft glows, faded against the scene's depth so no slope cuts them off).
            Fx.Glow(g + Vector3.up * core * 0.8f * s, core * 6f * s * flicker, new Color(1f, 0.96f, 0.99f, 0.9f * swell), false, false, Vfx.Blend.Additive, true, true);
            Fx.Glow(g + Vector3.up * core * 1.2f * s, r * 2.4f * s * flicker, new Color(1f, 0.6f, 0.88f, 0.28f * swell), false, false, Vfx.Blend.Additive, true, true);

            // Pulsing rings round it racing down to the ground (as the Hemp Beam's), each lying over the terrain below it,
            // spreading out over the ground as they reach it.
            const int ringCount = 6;
            for (int k = 0; k < ringCount; k++)
            {
                float u = Mathf.Repeat(k / (float)ringCount - now * 1.1f, 1f);
                float hgt = 7f * u * u;
                float pulse = 1f + 0.12f * Mathf.Sin(now * 18f + k * 1.7f);
                float rad = Mathf.Lerp(r * 1.05f, core * 1.6f, Mathf.Sqrt(u)) * pulse;
                DrapedRing(at, rad, 0.16f, new Color(1f, 0.72f, 0.92f, (1f - u) * 0.75f * swell), hgt + 0.04f, 28);
            }
            // Shockwaves spreading over the ground now and then.
            strikeEmit.TryGetValue(-st.Id, out float wave);
            wave -= dt;
            if (wave <= 0f) { shockwaves.Add((at, now, r * Random.Range(2.2f, 3f))); wave = Random.Range(0.35f, 0.7f); }
            strikeEmit[-st.Id] = wave;

            // Arcs crackling off the column (midpoint-displaced, Reed and Wyvill 1994), new ones every frame.
            for (int arc = 0; arc < 3; arc++)
            {
                if (Random.value < 0.45f) continue;
                float ang = Random.Range(0f, Mathf.PI * 2f), h0 = Random.Range(0.3f, 5f);
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 from = g + (Vector3.up * h0 + dir * width) * s;
                Vector3 to = from + (dir * Random.Range(r * 0.6f, r * 1.3f) + Vector3.up * Random.Range(-0.4f, 0.6f)) * s;
                Arc(from, to, 0.25f * s, new Color(1f, 0.85f, 0.97f, 0.9f * swell));
            }
            // Arcs crawling over the ground from the contact patch.
            if (Random.value < 0.5f)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                var d2 = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                beamPts.Clear(); beamCols.Clear();
                for (int i = 0; i <= 6; i++)
                {
                    float t = i / 6f;
                    Vector2 p = at + d2 * Mathf.Lerp(core, r * 1.8f, t) + Random.insideUnitCircle * 0.08f;
                    beamPts.Add(OnSurface(p, 0.05f));
                    beamCols.Add(new Color(1f, 0.85f, 0.97f, 0.85f * (1f - t) * swell));
                }
                Fx.Ribbon(beamPts, beamCols, 0.035f * s, false, false, Vfx.Blend.Additive, true, true);
            }

            // The particles, at a steady rate (the reference: dense spray low over the ground, streaks racing up the
            // beam, molten droplets, ionised motes, specks of debris and vapour curling up).
            strikeEmit.TryGetValue(st.Id, out float acc);
            acc += dt * swell;
            while (acc > 0.004f)
            {
                acc -= 0.004f;
                float roll = Random.value;
                float ang = Random.Range(0f, Mathf.PI * 2f);
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                if (roll < 0.42f)
                {
                    // The spray: white-hot streaks thrown out low over the ground from the contact patch, falling.
                    float elev = Random.Range(0.08f, 0.6f);
                    Vector3 v = (dir * Mathf.Cos(elev) + Vector3.up * Mathf.Sin(elev)) * Random.Range(3f, 9f);
                    Fx.Emit(g + dir * core * 0.8f * s, v * s, Random.Range(0.3f, 0.75f), Random.value < 0.6f ? new Color(1f, 0.97f, 1f, 1f) : new Color(1f, 0.6f, 0.88f, 1f),
                        Random.Range(0.03f, 0.05f) * s, 9.8f * s, 0.4f, true, true, 1f, false, false, Vfx.Blend.Additive, true, true);
                }
                else if (roll < 0.66f)
                {
                    // Streaks racing up the beam, close round it.
                    Vector3 from = g + (dir * width * Random.Range(1f, 1.8f) + Vector3.up * Random.Range(0f, 6f)) * s;
                    Fx.Emit(from, (Vector3.up * Random.Range(8f, 15f) + dir * Random.Range(0.3f, 1.2f)) * s, Random.Range(0.2f, 0.4f),
                        Random.value < 0.5f ? new Color(1f, 0.92f, 0.98f, 0.95f) : new Color(1f, 0.55f, 0.85f, 0.9f), Random.Range(0.025f, 0.04f) * s, 0f, 0.6f, true, true, 1f,
                        false, false, Vfx.Blend.Additive, true, true);
                }
                else if (roll < 0.74f)
                    // Molten droplets, heavy, glowing.
                    Fx.Emit(g + dir * core * 0.6f * s, (dir * Random.Range(1f, 3f) + Vector3.up * Random.Range(1.5f, 3.5f)) * s, Random.Range(0.5f, 0.9f),
                        new Color(1f, 0.8f, 0.6f, 1f), Random.Range(0.03f, 0.05f) * s, 12f * s, 0.2f, true, true, 1f, false, false, Vfx.Blend.Additive, true, true);
                else if (roll < 0.84f)
                {
                    // Ionised motes spiralling round the column.
                    Vector3 from = g + (dir * Random.Range(r * 0.5f, r * 1.1f) + Vector3.up * Random.Range(0.2f, 4f)) * s;
                    Fx.Emit(from, (new Vector3(-dir.z, 0f, dir.x) * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(1f, 2.5f) - dir * 0.6f) * s, Random.Range(0.3f, 0.6f),
                        new Color(1f, 0.75f, 0.95f, 1f), Random.Range(0.02f, 0.03f) * s, 0f, 1.4f, false, true, 1f, false, false, Vfx.Blend.Additive, true, true);
                }
                else if (roll < 0.92f)
                    // Specks of debris knocked out of the ground, dark, falling back.
                    Fx.Emit(g + dir * core * s, (dir * Random.Range(1.5f, 4f) + Vector3.up * Random.Range(1.5f, 4f)) * s, Random.Range(0.5f, 0.9f), new Color(0.1f, 0.09f, 0.09f, 1f),
                        Random.Range(0.025f, 0.045f) * s, 12f * s, 0.4f, false, false, 1f, false, false, Vfx.Blend.Alpha, true, true);
                else if (roll < 0.97f)
                    Vapour(at + new Vector2(dir.x, dir.z) * Random.Range(core, r * 1.2f), s, 1f);
                else
                    // A white flare in the dome now and then.
                    Fx.Emit(g + (dir * Random.Range(0f, core) + Vector3.up * Random.Range(0.1f, 0.8f)) * s, Random.insideUnitSphere * 0.5f * s, Random.Range(0.08f, 0.16f),
                        new Color(1f, 0.95f, 1f, 0.8f), core * Random.Range(1.5f, 2.5f) * s, 0f, 1f, false, true, 1f, false, false, Vfx.Blend.Additive, true, true);
            }
            strikeEmit[st.Id] = acc;

            // Burning wherever it touches someone.
            MatchSim sim = host != null ? host.Sim : null;
            SimPlayer target = sim != null ? sim.Get(st.Target) : null;
            if (target != null && MatchSim.Strikeable(target) && Vector2.Distance(target.Pos, st.Pos) < r + target.RadiusD)
            {
                Vector3 body = Ground(target.Pos, 1.1f);
                Fx.Glow(body, 0.9f * s * flicker, new Color(1f, 0.6f, 0.85f, 0.35f), false, false, Vfx.Blend.Additive, true, true);
                if (Random.value < dt * 30f)
                    Fx.Emit(body + Random.insideUnitSphere * 0.3f * s, (Random.insideUnitSphere * 1.5f + Vector3.up) * s, Random.Range(0.25f, 0.5f), new Color(1f, 0.85f, 0.95f, 1f), 0.035f * s, 6f * s, 0.4f,
                        true, true, 1f, false, false, Vfx.Blend.Additive, true, true);
            }
        }

        /// <summary>A crackling arc between two points: midpoint displacement (Reed and Wyvill 1994, "Visual Simulation of Lightning").</summary>
        void Arc(Vector3 from, Vector3 to, float jag, Color col)
        {
            beamPts.Clear(); beamCols.Clear();
            const int n = 8;
            Vector3 d = to - from;
            Vector3 side = Vector3.Cross(d, Vector3.up).normalized;
            float off = 0f;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                off = i > 0 && i < n ? Mathf.Clamp(off + Random.Range(-1f, 1f) * jag * 0.6f, -jag, jag) : 0f;
                beamPts.Add(from + d * t + side * off + Vector3.up * Random.Range(-0.3f, 0.3f) * jag);
                beamCols.Add(new Color(col.r, col.g, col.b, col.a * (1f - t * 0.6f)));
            }
            Fx.Ribbon(beamPts, beamCols, 0.03f * S, false, false, Vfx.Blend.Additive, true, true);
        }

        /// <summary>A wisp of vapour rising off the ground at a spot, curling as it goes (curl-noise advection), swelling and thinning.</summary>
        void Vapour(Vector2 at, float s, float strength)
        {
            Vector3 p = OnSurface(at, 0.15f);
            Fx.Emit(p, (Vector3.up * Random.Range(0.5f, 1.1f) + Random.insideUnitSphere * 0.15f) * s, Random.Range(2f, 3.4f), new Color(0.66f, 0.64f, 0.66f, 0.16f * strength),
                Random.Range(0.14f, 0.28f) * s, -0.08f * s, 0.5f, false, false, 3f, false, false, Vfx.Blend.Alpha, true, true, 0.9f * s);
        }

        /// <summary>Shockwave rings spreading out over the ground, fading.</summary>
        void Shockwaves(float now, float s)
        {
            for (int i = shockwaves.Count - 1; i >= 0; i--)
            {
                var (at, born, reach) = shockwaves[i];
                float k = (now - born) / 0.5f;
                if (k >= 1f) { shockwaves.RemoveAt(i); continue; }
                float e = 1f - (1f - k) * (1f - k);
                DrapedRing(at, Mathf.Lerp(reach * 0.15f, reach, e), Mathf.Lerp(0.12f, 0.35f, e), new Color(1f, 0.65f, 0.9f, 0.6f * (1f - k)), 0.05f, 36);
            }
        }

        /// <summary>Vapour still rising off the spot a while after the beam's gone (the reference's last three seconds).</summary>
        void AfterVapour(float r, float s, float now, float dt)
        {
            if (Fx == null) return;
            for (int i = afterVapour.Count - 1; i >= 0; i--)
            {
                var (at, ended) = afterVapour[i];
                float k = (now - ended) / VapourAfter;
                if (k >= 1f) { afterVapour.RemoveAt(i); continue; }
                if (Random.value < dt * 14f * (1f - k)) Vapour(at + Random.insideUnitCircle * r * 0.8f, s, 1f - k * 0.5f);
            }
        }

        /// <summary>The ground it touched burning bright for a second (draped over it), then the char smoking a while.</summary>
        void ScorchBurning(float s, float now, float dt)
        {
            if (Fx == null) return;
            foreach (var (at, rr, seed, born) in scorch)
            {
                float age = now - born;
                if (age < ScorchBurn)
                {
                    float k = age / ScorchBurn;
                    Color hot = k < 0.35f ? Color.Lerp(new Color(1f, 0.97f, 0.9f, 0.85f), new Color(1f, 0.62f, 0.25f, 0.8f), k / 0.35f)
                        : Color.Lerp(new Color(1f, 0.62f, 0.25f, 0.8f), new Color(0.75f, 0.12f, 0.04f, 0f), (k - 0.35f) / 0.65f);
                    DrapedDisc(at, rr * 1.4f, hot, new Color(hot.r, hot.g * 0.5f, hot.b * 0.5f, 0f), 0.045f, 3, 14, 0.3f, seed);
                    // Flames licking up off it.
                    if (Random.value < dt * 40f * (1f - k))
                        Fx.Emit(OnSurface(at + Random.insideUnitCircle * rr * 0.7f, 0.05f), (Vector3.up * Random.Range(0.8f, 2f) + Random.insideUnitSphere * 0.3f) * s, Random.Range(0.25f, 0.5f),
                            Color.Lerp(new Color(1f, 0.85f, 0.5f, 0.9f), new Color(1f, 0.4f, 0.12f, 0.8f), Random.value), Random.Range(0.08f, 0.16f) * s, -0.5f * s, 1.2f, false, true, 1.6f,
                            false, false, Vfx.Blend.Additive, true, true);
                    continue;
                }
                if (age > ScorchSmoke || Random.value > dt * 3f * (1f - age / ScorchSmoke)) continue;
                Vector3 p = OnSurface(at + Random.insideUnitCircle * rr * 0.6f, 0.05f);
                Fx.Emit(p, (Vector3.up * Random.Range(0.25f, 0.6f) + Random.insideUnitSphere * 0.1f) * s, Random.Range(1.6f, 2.8f), new Color(0.16f, 0.14f, 0.14f, 0.32f),
                    Random.Range(0.18f, 0.32f) * s, -0.05f * s, 0.6f, false, false, 2.6f, false, false, Vfx.Blend.Alpha, true, true, 0.6f * s);
                if (age < 2.5f && Random.value < 0.3f)
                    Fx.Emit(p, Vector3.up * Random.Range(0.3f, 0.9f) * s, Random.Range(0.4f, 0.8f), new Color(1f, 0.45f, 0.2f, 0.9f), 0.025f * s, -0.2f * s, 0.8f, false, true, 1f,
                        false, false, Vfx.Blend.Additive, true, true);
            }
        }

        /// <summary>
        /// A bloom that's on only while a strike burns (a global volume, weighted up and down with it), its threshold above
        /// anything in the scene but the beam's HDR core, so it costs nothing the rest of the time.
        /// </summary>
        void StrikeBloom(bool burning, float dt)
        {
            bloomWeight = Mathf.MoveTowards(bloomWeight, burning ? 1f : 0f, dt * 4f);
            if (strikeBloom == null)
            {
                if (bloomWeight <= 0f) return;
                var go = new GameObject("Strike Bloom");
                go.transform.SetParent(transform, false);
                strikeBloom = go.AddComponent<Volume>();
                strikeBloom.isGlobal = true;
                strikeBloom.priority = 50f;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.hideFlags = HideFlags.DontSave;
                Bloom bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(1.25f);
                bloom.intensity.Override(1.1f);
                bloom.scatter.Override(0.6f);
                bloom.tint.Override(new Color(1f, 0.93f, 0.97f));
                bloom.highQualityFiltering.Override(false);
                strikeBloom.sharedProfile = profile;
            }
            strikeBloom.weight = bloomWeight;
            strikeBloom.enabled = bloomWeight > 0f;
        }

        /// <summary>The beam's own light on everything round it (and the player): strong, white-pink.</summary>
        void StrikeLight(OrbitalStrike st, float r)
        {
            if (!strikeLights.TryGetValue(st.Id, out VisionLight light) || light == null)
            {
                var go = new GameObject("Orbital Strike Light");
                go.transform.SetParent(world.transform, false);
                light = go.AddComponent<VisionLight>();
                light.isStatic = false;
                light.range = r * 6f;
                light.height = 3f;
                light.flickerAmount = 0.1f;
                light.falloffStart = 0.35f;
                strikeLights[st.Id] = light;
            }
            light.transform.position = Ground(st.Pos, 0.05f);
            float left = Balance.Items.Orbital.Duration - st.T;
            float swell = Mathf.Clamp01(st.T / BeamSwell) * Mathf.Clamp01(left / BeamNarrow);
            light.intensity = Mathf.Lerp(0.2f, 1f, swell);
        }

        /// <summary>Lays every scorch blot into one mesh over the ground: charred black, a brown-black ring, ragged.</summary>
        void DrawScorch()
        {
            scorchDirty = false;
            if (world == null) return;
            if (scorchObject == null)
            {
                scorchObject = new GameObject("Scorch Trail");
                scorchObject.transform.SetParent(world.transform, false);
                scorchObject.AddComponent<MeshFilter>();
                var mr = scorchObject.AddComponent<MeshRenderer>();
                mr.sharedMaterial = world.lowPolyMaterial;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            var b = new LowPolyMeshBuilder(new System.Random(11));
            foreach (var (at, radius, seed, born) in scorch)
            {
                var rng = new System.Random(seed);
                const int n = 9;
                Vector3 V(Vector2 q, float lift) => new Vector3(q.x, world.SurfaceHeight(q) + lift, q.y);
                Vector3 mid = V(at, 0.016f), under = mid + Vector3.down;
                Color ring = new Color(0.13f, 0.1f, 0.09f), core = new Color(0.05f, 0.045f, 0.045f);
                var rim = new Vector3[n];
                var inner = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n, rr = radius * (0.75f + 0.45f * (float)rng.NextDouble());
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    rim[i] = V(at + d * rr, 0.015f);
                    inner[i] = V(at + d * rr * 0.55f, 0.0165f);
                }
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    b.AddTriangleOutward(mid, inner[i], inner[j], core, under);
                    b.AddTriangleOutward(inner[i], rim[i], rim[j], ring, under);
                    b.AddTriangleOutward(inner[i], rim[j], inner[j], ring, under);
                }
            }
            MeshFilter mf = scorchObject.GetComponent<MeshFilter>();
            if (mf.sharedMesh != null)
            {
                if (Application.isPlaying) Destroy(mf.sharedMesh);
                else DestroyImmediate(mf.sharedMesh);
            }
            mf.sharedMesh = b.ToMesh("Scorch Trail");
        }
    }
}
