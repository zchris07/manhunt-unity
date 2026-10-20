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
    /// ring closing in on the locked player again and again and a thin guide line from the sky (and, a second before it
    /// lands, the laser's own sound under the music). Then the beam, a column of plasma three times Zach's width: at its
    /// centre a cylinder of pure white energy as wide as Zach himself, going pink further out to its soft edges; pulsing
    /// rings round it racing down to the ground, as the Hemp Beam's do; the air round it ionised, crackling arcs jumping
    /// off it and charged motes spiralling round it. Where it meets the ground, a blinding white-hot spot, shockwave rings,
    /// arcs crawling over the ground, sparks, molten droplets, pink plasma, vapour, embers and clods thrown up; anyone in
    /// it burns where it touches them; and it lights everything round it. The ground it touches burns bright white-orange
    /// for a second, then lies charred, smoking for a while (it stays for the match).
    /// </summary>
    public sealed partial class MatchEffects
    {
        static readonly Color StrikeBody = new Color(1f, 0.25f, 0.7f, 1f), StrikeGlow = new Color(0.95f, 0.12f, 0.6f, 1f);
        /// <summary>The scorch blots left behind (where, how wide, when), the most kept, and how often one's laid.</summary>
        public const int MaxScorch = 240;
        public const float ScorchEvery = 0.35f, ScorchSmoke = 8f;

        readonly List<(Vector2 at, float r, int seed, float born)> scorch = new List<(Vector2, float, int, float)>();
        readonly Dictionary<int, Vector2> lastScorch = new Dictionary<int, Vector2>();
        readonly Dictionary<int, float> strikeEmit = new Dictionary<int, float>();
        readonly Dictionary<int, VisionLight> strikeLights = new Dictionary<int, VisionLight>();
        readonly HashSet<int> strikesSeen = new HashSet<int>();
        readonly HashSet<int> laserCued = new HashSet<int>();
        /// <summary>How long the ground the beam touched burns bright before it's only char and smoke.</summary>
        public const float ScorchBurn = 1f;
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
                    // A second to go: the laser's sound starts (its impact a second in, landing with the beam).
                    if (st.Countdown <= 1f && laserCued.Add(st.Id)) Audio.PlayCue("orbital_laser", Units(st.Pos));
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
                laserCued.Remove(id);
            }
            // The ground it touched burning bright for a second (white-hot, orange, dull red), then the char smoking a while.
            if (Fx != null)
                foreach (var (at, rr, seed, born) in scorch)
                {
                    float age = now - born;
                    if (age < ScorchBurn)
                    {
                        float k = age / ScorchBurn;
                        Color hot = k < 0.35f ? Color.Lerp(new Color(1f, 0.97f, 0.9f, 0.8f), new Color(1f, 0.62f, 0.25f, 0.75f), k / 0.35f)
                            : Color.Lerp(new Color(1f, 0.62f, 0.25f, 0.75f), new Color(0.75f, 0.12f, 0.04f, 0f), (k - 0.35f) / 0.65f);
                        Vector3 gp = Ground(at, 0.06f);
                        Fx.Glow(gp, rr * 1.5f * s * (1f + 0.08f * Mathf.Sin(now * 37f + seed)), hot, false, false);
                        Fx.Glow(gp, rr * 0.8f * s, new Color(hot.r, Mathf.Min(1f, hot.g + 0.2f), Mathf.Min(1f, hot.b + 0.2f), hot.a * 0.8f), false, false);
                        // Flames licking up off it.
                        if (Random.value < dt * 40f * (1f - k))
                            Fx.Emit(Ground(at + Random.insideUnitCircle * rr * 0.7f, 0.05f), (Vector3.up * Random.Range(0.8f, 2f) + Random.insideUnitSphere * 0.3f) * s, Random.Range(0.25f, 0.5f),
                                Color.Lerp(new Color(1f, 0.85f, 0.5f, 0.9f), new Color(1f, 0.4f, 0.12f, 0.8f), Random.value), Random.Range(0.08f, 0.16f) * s, -0.5f * s, 1.2f, false, true, 1.6f, false, false);
                        continue;
                    }
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
            // Dropping out of the sky over its first fifth of a second.
            float drop = Mathf.Clamp01(st.T / 0.2f);
            const float sky = 40f;
            Vector3 top = g + Vector3.up * sky * s, bottom = Vector3.Lerp(top, g, drop);
            // The end of its track: fading with it.
            float left = Balance.Items.Orbital.Duration - st.T, fade = Mathf.Clamp01(left / 1.5f);
            float flicker = (0.92f + 0.08f * Mathf.Sin(now * 53f + st.Id) * Mathf.Sin(now * 31f)) * Mathf.Lerp(0.3f, 1f, fade);
            float core = Scale.D(Balance.HunterWidth);   // the white cylinder: as wide as Zach himself
            void Layer(float width, Color c, float pulseAmt)
            {
                beamPts.Clear(); beamCols.Clear();
                const int n = 16;
                for (int i = 0; i <= n; i++)
                {
                    float t = i / (float)n;
                    beamPts.Add(Vector3.Lerp(top, bottom, t));
                    float pulse = 1f - pulseAmt + pulseAmt * Mathf.Sin(t * 22f + now * 34f);
                    Color col = c * flicker * pulse;
                    col.a = c.a * flicker * Mathf.Lerp(0.6f, 1f, t);
                    beamCols.Add(col);
                }
                Fx.Ribbon(beamPts, beamCols, width * s, false, false, Vfx.Blend.Additive, true);
            }
            // Pink out at the edges, hotter pink-white inward, pure white at the centre (stacked, so it burns out to white).
            float w = 2f * r;
            Layer(w * 1.35f, new Color(StrikeGlow.r, StrikeGlow.g, StrikeGlow.b, 0.16f), 0.15f);
            Layer(w, new Color(StrikeBody.r, StrikeBody.g, StrikeBody.b, 0.42f), 0.15f);
            Layer(w * 0.62f, new Color(1f, 0.55f, 0.85f, 0.55f), 0.1f);
            Layer(core * 1.25f, new Color(1f, 0.85f, 0.95f, 0.8f), 0.05f);
            Layer(core, new Color(1f, 1f, 1f, 1f), 0.03f);
            Layer(core * 0.8f, new Color(1f, 1f, 1f, 1f), 0f);
            // Pulsing rings round it racing down to the ground (as the Hemp Beam's), bunched near the bottom.
            const int ringCount = 7;
            for (int k = 0; k < ringCount; k++)
            {
                float u = Mathf.Repeat(k / (float)ringCount - now * 0.9f, 1f);
                float h = 9f * u * u;
                if (h > sky * drop) continue;
                float pulse = 1f + 0.14f * Mathf.Sin(now * 18f + k * 1.7f);
                float rad = r * 0.62f * pulse * Mathf.Lerp(1f, 0.85f, u);
                Vector3 c = g + Vector3.up * h * s;
                beamPts.Clear(); beamCols.Clear();
                const int seg = 24;
                float a0 = (1f - u) * 0.85f * flicker;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    beamPts.Add(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rad * s);
                    beamCols.Add(new Color(1f, 0.72f, 0.92f, a0));
                }
                Fx.Ribbon(beamPts, beamCols, 0.08f * s, false, false, Vfx.Blend.Additive, true);
            }
            // The air round it ionised: arcs jumping off the column, crackling (new ones every frame).
            for (int arc = 0; arc < 4; arc++)
            {
                if (Random.value < 0.35f) continue;
                float ang = Random.Range(0f, Mathf.PI * 2f), h0 = Random.Range(0.2f, 6f);
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 from = g + Vector3.up * h0 * s + dir * core * 0.5f * s;
                beamPts.Clear(); beamCols.Clear();
                float reach = Random.Range(r * 0.6f, r * 1.4f);
                const int jags = 6;
                for (int i = 0; i <= jags; i++)
                {
                    float t = i / (float)jags;
                    Vector3 jitter = i == 0 ? Vector3.zero : Random.insideUnitSphere * 0.12f * s;
                    beamPts.Add(from + dir * reach * t * s + Vector3.up * Random.Range(-0.25f, 0.25f) * t * s + jitter);
                    beamCols.Add(new Color(0.9f, 0.85f, 1f, (1f - t * 0.7f) * flicker));
                }
                Fx.Ribbon(beamPts, beamCols, 0.03f * s, false, false, Vfx.Blend.Additive, true);
            }
            if (drop < 1f) return;
            // The white-hot spot where it meets the ground, in a pink bloom.
            Fx.Glow(g, w * 1.8f * s * flicker, new Color(0.95f, 0.15f, 0.62f, 0.3f), false, false);
            Fx.Glow(g, w * 0.95f * s * flicker, new Color(1f, 0.45f, 0.8f, 0.45f), false, false);
            Fx.Glow(g, core * 1.6f * s, new Color(1f, 0.95f, 0.98f, 0.85f), false, false);
            Fx.Glow(g, core * 0.9f * s, new Color(1f, 1f, 1f, 1f), false, false);
            // Arcs crawling over the ground round it.
            if (Random.value < 0.6f)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                beamPts.Clear(); beamCols.Clear();
                for (int i = 0; i <= 5; i++)
                {
                    float t = i / 5f;
                    beamPts.Add(g + dir * Mathf.Lerp(core * 0.5f, r * 1.6f, t) * s + new Vector3(Random.Range(-0.1f, 0.1f), 0.02f, Random.Range(-0.1f, 0.1f)) * s);
                    beamCols.Add(new Color(0.92f, 0.85f, 1f, 0.9f * (1f - t)));
                }
                Fx.Ribbon(beamPts, beamCols, 0.035f * s, false, false, Vfx.Blend.Additive, true);
            }
            // Sparks, molten droplets, ionised motes, plasma, vapour, embers, clods and shockwaves, at a steady rate.
            strikeEmit.TryGetValue(st.Id, out float acc);
            acc += dt;
            int rings = 0;
            while (acc > 0.007f)
            {
                acc -= 0.007f;
                Vector2 c2 = Random.insideUnitCircle;
                Vector3 at = g + new Vector3(c2.x, 0f, c2.y) * r * 0.7f * s;
                Vector3 outward = c2.sqrMagnitude > 1e-6f ? new Vector3(c2.x, 0f, c2.y).normalized : Vector3.forward;
                float roll = Random.value;
                if (roll < 0.34f)
                    // Sparks: white and pink, flying out and falling.
                    Fx.Emit(at, (outward * Random.Range(3f, 8f) + Vector3.up * Random.Range(2f, 6f)) * s, Random.Range(0.35f, 0.8f),
                        Random.value < 0.55f ? new Color(1f, 0.97f, 1f, 1f) : new Color(1f, 0.55f, 0.85f, 1f), Random.Range(0.02f, 0.045f) * s, 9.8f * s, 0.3f, true, true, 1f, false, false);
                else if (roll < 0.44f)
                    // Molten droplets, heavy, orange-white.
                    Fx.Emit(at, (outward * Random.Range(1f, 3f) + Vector3.up * Random.Range(1.5f, 3.5f)) * s, Random.Range(0.5f, 0.9f),
                        new Color(1f, 0.75f, 0.4f, 1f), Random.Range(0.04f, 0.06f) * s, 12f * s, 0.2f, true, true, 1f, false, false);
                else if (roll < 0.56f)
                {
                    // Ionised motes spiralling up round the column.
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    var rim = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 from = g + rim * Random.Range(r * 0.6f, r * 1.2f) * s + Vector3.up * Random.Range(0f, 3f) * s;
                    Fx.Emit(from, (new Vector3(-rim.z, 0f, rim.x) * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(1f, 2.5f) - rim * 0.6f) * s, Random.Range(0.3f, 0.6f),
                        Random.value < 0.5f ? new Color(0.85f, 0.85f, 1f, 1f) : new Color(1f, 0.7f, 0.95f, 1f), Random.Range(0.02f, 0.035f) * s, 0f, 1.4f, true, true, 1f, false, false);
                }
                else if (roll < 0.68f)
                    // Pink plasma blobs.
                    Fx.Emit(at + Vector3.up * Random.Range(0f, 1.2f) * s, (Random.insideUnitSphere * 0.8f + Vector3.up * 0.7f) * s, Random.Range(0.3f, 0.6f), new Color(1f, 0.38f, 0.78f, 0.55f),
                        Random.Range(0.14f, 0.28f) * s, 0f, 1.6f, false, true, 1.8f, false, false);
                else if (roll < 0.86f)
                    // Vapour boiling up.
                    Fx.Emit(at, (Vector3.up * Random.Range(0.9f, 2f) + outward * 0.5f) * s, Random.Range(1f, 1.8f), new Color(0.88f, 0.78f, 0.86f, 0.2f),
                        Random.Range(0.28f, 0.5f) * s, -0.3f * s, 0.7f, false, false, 2.4f, false, false, Vfx.Blend.Alpha);
                else if (roll < 0.95f)
                    // Embers.
                    Fx.Emit(at, (outward * Random.Range(0.5f, 2f) + Vector3.up * Random.Range(1f, 3f)) * s, Random.Range(0.6f, 1.2f), new Color(1f, 0.5f, 0.2f, 1f), 0.03f * s, 4f * s, 0.6f, false, true, 1f, false, false);
                else
                    // Clods of earth thrown out.
                    Fx.Emit(at, (outward * Random.Range(2f, 4f) + Vector3.up * Random.Range(2f, 4f)) * s, Random.Range(0.6f, 1f), new Color(0.12f, 0.1f, 0.09f, 1f),
                        Random.Range(0.04f, 0.07f) * s, 12f * s, 0.4f, false, false, 1f, false, false, Vfx.Blend.Alpha);
                if (Random.value < 0.006f && rings++ == 0) Fx.RingAt(g, r * 0.4f * s, r * 2.4f * s, 0.45f, new Color(1f, 0.6f, 0.88f, 0.6f), 0.1f * s, false, false);
            }
            strikeEmit[st.Id] = acc;
            // Burning wherever it touches someone.
            MatchSim sim = host != null ? host.Sim : null;
            SimPlayer target = sim != null ? sim.Get(st.Target) : null;
            if (target != null && MatchSim.Strikeable(target) && Vector2.Distance(target.Pos, st.Pos) < r + target.RadiusD)
            {
                Vector3 body = Ground(target.Pos, 1.1f);
                Fx.Glow(body, 0.9f * s * flicker, new Color(1f, 0.5f, 0.8f, 0.35f), false, false);
                if (Random.value < dt * 30f)
                    Fx.Emit(body + Random.insideUnitSphere * 0.3f * s, (Random.insideUnitSphere * 1.5f + Vector3.up) * s, Random.Range(0.25f, 0.5f), new Color(1f, 0.85f, 0.95f, 1f), 0.035f * s, 6f * s, 0.4f, true, true, 1f, false, false);
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
            light.intensity = 0.75f * Mathf.Lerp(0.4f, 1f, fade) * Mathf.Clamp01(st.T / 0.2f);
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
