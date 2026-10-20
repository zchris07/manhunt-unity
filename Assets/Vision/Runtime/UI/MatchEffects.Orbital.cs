using System.Collections.Generic;
using UnityEngine;
using Vision.Effects;
using Vision.Game;
using Vision.Visibility;
using Vision.World;

namespace Vision.UI
{
    /// <summary>
    /// The K-pop orbital strike (<see cref="OrbitalStrike"/>), seen by everyone, light or dark. Counting down: a pink target
    /// ring closing in on the locked player again and again and a thin guide line from the sky. Then the beam, a wide pink
    /// counterpart of the Hemp Beam three times Zach's width: it drops out of the sky in a quarter second, a soft wide glow,
    /// a hot pink body and a white-pink core pulsing down it, plasma threads twisting round it; where it meets the ground a
    /// blinding hot spot and shockwave rings, sparks spraying out and falling, pink plasma blobs, vapour boiling up, embers;
    /// anyone in it burns where it touches them; and it lights everything round it. Behind it as it follows its target, a
    /// charred trail across the ground, smoking for a while (it stays for the match).
    /// </summary>
    public sealed partial class MatchEffects
    {
        static readonly Color StrikeCore = new Color(1f, 0.78f, 0.93f, 1f), StrikeBody = new Color(1f, 0.25f, 0.7f, 1f), StrikeGlow = new Color(0.95f, 0.12f, 0.6f, 1f);
        /// <summary>The scorch blots left behind (where, how wide, when), the most kept, and how often one's laid.</summary>
        public const int MaxScorch = 240;
        public const float ScorchEvery = 0.35f, ScorchSmoke = 8f;

        readonly List<(Vector2 at, float r, int seed, float born)> scorch = new List<(Vector2, float, int, float)>();
        readonly Dictionary<int, Vector2> lastScorch = new Dictionary<int, Vector2>();
        readonly Dictionary<int, float> strikeEmit = new Dictionary<int, float>();
        readonly Dictionary<int, VisionLight> strikeLights = new Dictionary<int, VisionLight>();
        readonly HashSet<int> strikesSeen = new HashSet<int>();
        GameObject scorchObject;
        MatchSim scorchSim;
        bool scorchDirty;

        /// <summary>How many scorch blots lie about, and the strike lights up (tests).</summary>
        public int ScorchCount => scorch.Count;
        public int StrikeLights => strikeLights.Count;

        void OrbitalStrikes(MatchSim sim)
        {
            if (sim != scorchSim)
            {
                scorchSim = sim;
                scorch.Clear();
                lastScorch.Clear();
                scorchDirty = true;
            }
            float s = S, now = Time.time, dt = Time.deltaTime;
            float r = Scale.D(Balance.Items.Orbital.Radius);
            strikesSeen.Clear();
            foreach (OrbitalStrike st in sim.Strikes)
            {
                strikesSeen.Add(st.Id);
                Vector3 g = Ground(st.Pos, 0.05f);
                if (!st.Firing)
                {
                    // Locking on: rings closing in on them, a faint line down from the sky.
                    float k = 1f - st.Countdown / Balance.Items.Orbital.Countdown;
                    strikeEmit.TryGetValue(st.Id, out float ring);
                    ring += dt;
                    if (ring > Mathf.Lerp(0.55f, 0.18f, k) && Fx != null)
                    {
                        ring = 0f;
                        Fx.RingAt(g, r * 1.8f * s, r * 0.25f * s, 0.5f, new Color(1f, 0.35f, 0.72f, 0.75f), 0.07f * s, false, false);
                    }
                    strikeEmit[st.Id] = ring;
                    if (Fx != null)
                    {
                        Fx.Ribbon(new[] { g + Vector3.up * 30f * s, g }, new[] { new Color(1f, 0.4f, 0.75f, 0f), new Color(1f, 0.4f, 0.75f, 0.35f * k) }, 0.06f * s, false, false, Vfx.Blend.Additive, true);
                        Fx.Glow(g, r * 0.9f * s * (0.8f + 0.2f * Mathf.Sin(now * 12f)), new Color(1f, 0.3f, 0.7f, 0.2f + 0.25f * k), false, false);
                    }
                    continue;
                }
                StrikeBeam(st, g, r, s, now, dt);
                StrikeLight(st, g, r);
                // The charred trail it leaves.
                if (!lastScorch.TryGetValue(st.Id, out Vector2 last) || (st.Pos - last).magnitude > ScorchEvery)
                {
                    lastScorch[st.Id] = st.Pos;
                    scorch.Add((st.Pos + Random.insideUnitCircle * r * 0.15f, r * Random.Range(0.45f, 0.62f), Random.Range(0, 100000), now));
                    while (scorch.Count > MaxScorch) scorch.RemoveAt(0);
                    scorchDirty = true;
                }
            }
            // Strikes over: their lights out.
            var gone = new List<int>();
            foreach (var kv in strikeLights) if (!strikesSeen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (int id in gone)
            {
                if (strikeLights[id] != null) { if (Application.isPlaying) Destroy(strikeLights[id].gameObject); else DestroyImmediate(strikeLights[id].gameObject); }
                strikeLights.Remove(id);
                lastScorch.Remove(id);
            }
            // The trail smoking for a while after.
            if (Fx != null)
                foreach (var (at, rr, seed, born) in scorch)
                {
                    float age = now - born;
                    if (age > ScorchSmoke || Random.value > dt * 3f * (1f - age / ScorchSmoke)) continue;
                    Vector3 p = Ground(at + Random.insideUnitCircle * rr * 0.6f, 0.05f);
                    Fx.Emit(p, (Vector3.up * Random.Range(0.25f, 0.6f) + Random.insideUnitSphere * 0.1f) * s, Random.Range(1.6f, 2.8f), new Color(0.16f, 0.14f, 0.14f, 0.32f),
                        Random.Range(0.18f, 0.32f) * s, -0.05f * s, 0.6f, false, false, 2.6f, false, false, Vfx.Blend.Alpha);
                    if (age < 2.5f && Random.value < 0.3f)
                        Fx.Emit(p, Vector3.up * Random.Range(0.3f, 0.9f) * s, Random.Range(0.4f, 0.8f), new Color(1f, 0.45f, 0.2f, 0.9f), 0.025f * s, -0.2f * s, 0.8f, false, true, 1f, false, false);
                }
            if (scorchDirty) DrawScorch();
        }

        void StrikeBeam(OrbitalStrike st, Vector3 g, float r, float s, float now, float dt)
        {
            if (Fx == null) return;
            // Dropping out of the sky over its first quarter second.
            float drop = Mathf.Clamp01(st.T / 0.25f);
            Vector3 sky = g + Vector3.up * 40f * s;
            Vector3 top = sky, bottom = Vector3.Lerp(sky, g, drop);
            // The end of its track: fading with it.
            float left = Balance.Items.Orbital.Duration - st.T, fade = Mathf.Clamp01(left / 1.5f);
            float flicker = (0.88f + 0.12f * Mathf.Sin(now * 47f + st.Id) * Mathf.Sin(now * 29f)) * Mathf.Lerp(0.35f, 1f, fade);
            void Layer(float width, Color c)
            {
                beamPts.Clear(); beamCols.Clear();
                const int n = 16;
                for (int i = 0; i <= n; i++)
                {
                    float t = i / (float)n;
                    beamPts.Add(Vector3.Lerp(top, bottom, t));
                    float pulse = 0.82f + 0.18f * Mathf.Sin(t * 22f + now * 34f);
                    Color col = c * flicker * pulse;
                    col.a = c.a * flicker * Mathf.Lerp(0.55f, 1f, t);
                    beamCols.Add(col);
                }
                Fx.Ribbon(beamPts, beamCols, width * s, false, false, Vfx.Blend.Additive, true);
            }
            float w = 2f * r;
            Layer(w * 1.3f, new Color(StrikeGlow.r, StrikeGlow.g, StrikeGlow.b, 0.18f));
            Layer(w * 0.8f, new Color(StrikeBody.r, StrikeBody.g, StrikeBody.b, 0.5f));
            Layer(w * 0.32f, new Color(StrikeCore.r, StrikeCore.g, StrikeCore.b, 0.6f));
            // Plasma threads twisting round it, racing down.
            for (int strand = 0; strand < 3; strand++)
            {
                beamPts.Clear(); beamCols.Clear();
                const int n = 40;
                for (int i = 0; i <= n; i++)
                {
                    float t = i / (float)n, h = Mathf.Lerp(40f, 0f, t) * drop;
                    float ph = h / 1.6f * Mathf.PI * 2f + now * 9f + strand * Mathf.PI * 2f / 3f;
                    beamPts.Add(Vector3.Lerp(top, bottom, t) + new Vector3(Mathf.Cos(ph), 0f, Mathf.Sin(ph)) * r * 0.55f * s);
                    beamCols.Add(new Color(1f, 0.6f, 0.88f, 0.42f * t * flicker));
                }
                Fx.Ribbon(beamPts, beamCols, 0.07f * s, false, false, Vfx.Blend.Additive, true);
            }
            if (drop < 1f) return;
            // The hot spot where it meets the ground.
            Fx.Glow(g, w * 1.7f * s * flicker, new Color(0.95f, 0.15f, 0.62f, 0.3f), false, false);
            Fx.Glow(g, w * 0.9f * s * flicker, new Color(1f, 0.38f, 0.78f, 0.4f), false, false);
            Fx.Glow(g, w * 0.35f * s, new Color(1f, 0.8f, 0.94f, 0.45f), false, false);
            // Sparks, plasma, vapour, embers and shockwaves, at a steady rate.
            strikeEmit.TryGetValue(st.Id, out float acc);
            acc += dt;
            int rings = 0;
            while (acc > 0.008f)
            {
                acc -= 0.008f;
                Vector2 c2 = Random.insideUnitCircle;
                Vector3 at = g + new Vector3(c2.x, 0f, c2.y) * r * 0.75f * s;
                Vector3 outward = new Vector3(c2.x, 0f, c2.y).normalized;
                float roll = Random.value;
                if (roll < 0.45f)
                    Fx.Emit(at, (outward * Random.Range(2f, 6f) + Vector3.up * Random.Range(1.5f, 5f)) * s, Random.Range(0.35f, 0.8f),
                        Random.value < 0.5f ? new Color(1f, 0.85f, 0.95f, 1f) : new Color(1f, 0.55f, 0.82f, 1f), Random.Range(0.025f, 0.05f) * s, 9.8f * s, 0.3f, true, true, 1f, false, false);
                else if (roll < 0.62f)
                    Fx.Emit(at + Vector3.up * Random.Range(0f, 1.2f) * s, (Random.insideUnitSphere * 0.8f + Vector3.up * 0.6f) * s, Random.Range(0.35f, 0.7f), new Color(1f, 0.35f, 0.75f, 0.55f),
                        Random.Range(0.12f, 0.25f) * s, 0f, 1.6f, false, true, 1.8f, false, false);
                else if (roll < 0.85f)
                    Fx.Emit(at, (Vector3.up * Random.Range(0.8f, 1.8f) + outward * 0.4f) * s, Random.Range(1f, 1.8f), new Color(0.85f, 0.72f, 0.8f, 0.22f),
                        Random.Range(0.25f, 0.45f) * s, -0.3f * s, 0.7f, false, false, 2.4f, false, false, Vfx.Blend.Alpha);
                else
                    Fx.Emit(at, (outward * Random.Range(0.5f, 2f) + Vector3.up * Random.Range(1f, 3f)) * s, Random.Range(0.6f, 1.2f), new Color(1f, 0.5f, 0.2f, 1f), 0.03f * s, 4f * s, 0.6f, false, true, 1f, false, false);
                if (Random.value < 0.006f && rings++ == 0) Fx.RingAt(g, r * 0.4f * s, r * 2.2f * s, 0.45f, new Color(1f, 0.5f, 0.82f, 0.6f), 0.1f * s, false, false);
            }
            strikeEmit[st.Id] = acc;
            // Burning wherever it touches someone.
            MatchSim sim = host != null ? host.Sim : null;
            SimPlayer target = sim != null ? sim.Get(st.Target) : null;
            if (target != null && MatchSim.Strikeable(target) && Vector2.Distance(target.Pos, st.Pos) < r + target.RadiusD)
            {
                Vector3 body = Ground(target.Pos, 1.1f);
                Fx.Glow(body, 0.9f * s * flicker, new Color(1f, 0.4f, 0.75f, 0.35f), false, false);
                if (Random.value < dt * 30f)
                    Fx.Emit(body + Random.insideUnitSphere * 0.3f * s, (Random.insideUnitSphere * 1.5f + Vector3.up) * s, Random.Range(0.25f, 0.5f), new Color(1f, 0.75f, 0.9f, 1f), 0.035f * s, 6f * s, 0.4f, true, true, 1f, false, false);
            }
        }

        /// <summary>The beam's own light on everything round it.</summary>
        void StrikeLight(OrbitalStrike st, Vector3 g, float r)
        {
            if (!strikeLights.TryGetValue(st.Id, out VisionLight light) || light == null)
            {
                var go = new GameObject("Orbital Strike Light");
                go.transform.SetParent(world.transform, false);
                light = go.AddComponent<VisionLight>();
                light.isStatic = false;
                light.range = r * 4.5f;
                light.height = 1.5f;
                light.flickerAmount = 0.12f;
                strikeLights[st.Id] = light;
            }
            light.transform.position = g;
            float fade = Mathf.Clamp01((Balance.Items.Orbital.Duration - st.T) / 1.5f);
            light.intensity = 0.65f * Mathf.Lerp(0.4f, 1f, fade) * Mathf.Clamp01(st.T / 0.25f);
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
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
