using System.Collections.Generic;
using UnityEngine;
using Vision.Audio;
using Vision.Characters;
using Vision.Effects;
using Vision.Game;
using Vision.World;

namespace Vision.UI
{
    /// <summary>
    /// The match's effects and sounds in the world, after the original's look: Zach's machete (charge ring at his feet,
    /// the swing's smear, the slash), the lunge's speed lines, the Soundcloud Burst (a purple concave lens racing across
    /// the map through walls, with fading echoes), Penjamin's gas (soft yellow puffs rolling out along a narrow cone, blue
    /// for 50 Nic), the Hemp Battery's pulsing green disc, the Hemp Beam (a charging orb, then a white-green beam with an
    /// impact glow), guns (muzzle flash, pellet tracers), and the sounds of all of it, placed where it happens.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed partial class MatchEffects : MonoBehaviour
    {
        public SandboxWorld world;

        MatchHost host;

        struct Arc { public Vector2 At; public float Angle, Radius, ArcDeg; public bool Heavy, Back; public float Born; }
        struct Wave { public Vector2 At; public float Angle, Born; }
        struct Tracer { public Vector3 From, To; public Color Color; public float Born, Life, Width; }

        readonly List<Arc> arcs = new List<Arc>();
        readonly List<Wave> waves = new List<Wave>();
        readonly List<Tracer> tracers = new List<Tracer>();
        readonly HashSet<int> beamLoops = new HashSet<int>();
        readonly List<int> beamGone = new List<int>();
        readonly List<Vector3> pts = new List<Vector3>(64);
        readonly List<Color> cols = new List<Color>(64);
        readonly Dictionary<int, float> charging = new Dictionary<int, float>();

        AudioManager Audio => AudioManager.Instance;
        Vfx Fx => Vfx.Instance;
        float S => world != null ? world.transform.lossyScale.x : 1f;
        float Now => host != null && host.Sim != null ? host.Sim.Time : Time.time;

        Vector3 Ground(Vector2 at, float up = 0.04f)
        {
            float h = world.GroundHeight(at);
            return world.transform.TransformPoint(new Vector3(at.x, h + up, at.y));
        }

        /// <summary>A point in original units from an origin (design units) along an angle and sideways.</summary>
        Vector3 Along(Vector2 origin, float angle, float forwardUnits, float sideUnits, float up = 0.1f)
        {
            Vector2 d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), s = new Vector2(-d.y, d.x);
            return Ground(origin + d * Scale.D(forwardUnits) + s * Scale.D(sideUnits), up);
        }

        Vector2 Units(Vector2 design) => design / Scale.Unit;

        void Bind()
        {
            if (world == null) return;
            MatchHost h = MatchHost.For(world);
            if (h == host) return;
            if (host != null) host.EventRaised -= OnEvent;
            host = h;
            if (host != null) host.EventRaised += OnEvent;
        }

        void OnDestroy()
        {
            if (host != null) host.EventRaised -= OnEvent;
        }

        // ---------------------------------------------------------------- events

        public void OnEvent(GameEvent e)
        {
            Bind();
            MatchSim sim = host != null ? host.Sim : null;
            if (sim == null) return;
            if (NpcEvent(e)) return;
            SimPlayer by = sim.Get(e.A);
            switch (e.Kind)
            {
                case EventKind.Swing:
                    if (by == null) break;
                    if (e.F < 0f)
                    {
                        // The windup: the swish (heavier for a charged swing).
                        Audio.PlayCue(e.B >= 2 ? "swing_heavy" : "swing", Units(by.Pos));
                        charging.Remove(by.Id);
                    }
                    else if (e.G <= 0f)
                    {
                        float reach = Balance.Hunter.Attack.Range * (e.B >= 2 ? Balance.Hunter.Attack.ChargeRangeMul : 1f);
                        arcs.Add(new Arc { At = by.Pos, Angle = by.Facing, Radius = reach, ArcDeg = Balance.Hunter.Attack.ArcDeg * (e.B >= 2 ? Balance.Hunter.Attack.ChargeArcMul : 1f), Heavy = e.B >= 2, Back = e.Text == "back", Born = Now });
                        if (e.F > 0f) Audio.PlayCue("slash", Units(by.Pos));
                    }
                    break;
                case EventKind.Hit:
                    if (e.Text == "slash" || e.Text == "lunge" || e.Text == "punch") Audio.PlayCue("body_hit", Units(e.Pos), 0.8f);
                    break;
                case EventKind.Talk:
                    if (by == null) break;
                    if (e.Text == "burst") waves.Add(new Wave { At = e.Pos, Angle = e.F, Born = Now });
                    else if (e.Text == "slam") Audio.PlayCue("slam", Units(e.Pos));
                    else if (e.Text == "search") Audio.PlayCue("hide", Units(e.Pos));
                    else if (e.Text == "drink") Audio.PlayCue("drink", Units(e.Pos));
                    else if (e.Text == "eat") Audio.PlayCue("eat", Units(e.Pos));
                    break;
                case EventKind.Throw:
                    Audio.PlayCue("throw", Units(e.Pos));
                    break;
                case EventKind.Gas:
                    if (string.IsNullOrEmpty(e.Text))
                    {
                        // A gas trap bursting.
                        Audio.PlayCue("gas", Units(e.Pos));
                        Fx.Burst(Ground(e.Pos, 0.4f), 30, 2.4f * S, 0.8f, new Color(0.7f, 0.4f, 1f, 1f), 0.08f * S, true, Vfx.Blend.Additive, 0f, 2f, 0.5f, new Color(1f, 0.45f, 0.8f, 1f));
                    }
                    break;
                case EventKind.Item:
                    if (e.Text != null && e.Text.StartsWith("Picked up")) Audio.PlayCue("pickup");
                    break;
                case EventKind.Noise:
                    switch (e.Text)
                    {
                        case "glass": Audio.PlayCue(e.F >= 900f ? "bottle_break" : "glass", Units(e.Pos)); break;
                        case "book": Audio.PlayCue("book_hit", Units(e.Pos)); break;
                        case "piss": Audio.PlayCue("splash", Units(e.Pos)); break;
                        case "smash": Audio.PlayCue("splinter", Units(e.Pos)); break;
                        case "door_smash": Audio.PlayCue("smash", Units(e.Pos)); break;
                        case "gen_kick":
                        case "gen_explode": Audio.PlayCue("kick", Units(e.Pos)); break;
                    }
                    break;
                case EventKind.Shot:
                    Shot(e, sim);
                    break;
                case EventKind.Stun:
                    if (e.Text != "down" && by != null) Audio.PlayCue("stun", Units(by.Pos), 0.6f);
                    break;
                case EventKind.GenDone:
                    Audio.PlayCue("generator_done", Units(e.Pos));
                    break;
                case EventKind.GatePowered:
                case EventKind.GateOpen:
                    Audio.PlayCue("gate", Units(sim.Map.Lever));
                    break;
            }
        }

        /// <summary>
        /// Where a shot leaves the gun: the end of the barrel of the gun the shooter's model holds (a player or an NPC), or
        /// a little way out in front of them when the model isn't drawn.
        /// </summary>
        Vector3 MuzzleOf(GameEvent e, MatchSim sim)
        {
            if (e.A > 0)
            {
                SimPlayer p = sim.Get(e.A);
                GameObject go = p != null ? CharacterOf(p) : null;
                ActionLayer layer = go != null ? go.GetComponent<ActionLayer>() : null;
                if (layer != null && layer.Muzzle(out Vector3 m)) return m;
            }
            else if (e.A < 0)
            {
                var npcs = world.GetComponent<NpcViews>();
                if (npcs != null && npcs.Muzzle(-e.A, out Vector3 m)) return m;
            }
            return Along(e.Pos, e.F, Balance.SurvivorRadius * 2.2f, 0f, 1.1f);
        }

        /// <summary>A gun going off: the flash at the muzzle, tracers from the barrel for each pellet or the round, and its sound.</summary>
        void Shot(GameEvent e, MatchSim sim)
        {
            var item = (Vision.Player.ItemType)e.B;
            bool golden = (item != Vision.Player.ItemType.Pistol && e.G > 0f) || (e.Text != null && e.Text.Contains("gold"));
            Vector3 muzzle = MuzzleOf(e, sim);
            Color flash = golden ? new Color(1f, 0.76f, 0.23f, 1f) : new Color(1f, 0.82f, 0.23f, 1f);
            Fx.Burst(muzzle, 10, 3f * S, 0.12f, flash, 0.09f * S, false, Vfx.Blend.Additive, 0f, 8f, 0.2f);
            switch (item)
            {
                case Vision.Player.ItemType.Pistol:
                    AddTracer(muzzle, e.Pos, e.F, e.G > 0f ? e.G / Scale.Unit : 900f, new Color(1f, 0.9f, 0.6f, 0.9f), 0.04f);
                    break;
                case Vision.Player.ItemType.Sniper:
                    AddTracer(muzzle, e.Pos, e.F, 6000f, new Color(1f, 0.95f, 0.82f, 1f), 0.05f, 0.35f);
                    break;
                default:
                    // Pellets: "hit|angle*1000:distance,..." (original units).
                    string list = e.Text != null && e.Text.Contains("|") ? e.Text.Substring(e.Text.IndexOf('|') + 1) : "";
                    foreach (string p in list.Split(','))
                    {
                        int c = p.IndexOf(':');
                        if (c <= 0) continue;
                        if (!float.TryParse(p.Substring(0, c), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ang)) continue;
                        if (!float.TryParse(p.Substring(c + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dist)) continue;
                        AddTracer(muzzle, e.Pos, ang / 1000f, dist, golden ? new Color(1f, 0.85f, 0.4f, 0.8f) : new Color(1f, 0.76f, 0.23f, 0.7f), 0.025f);
                        Fx.Burst(Along(e.Pos, ang / 1000f, dist, 0f, 1.0f), 3, 1.4f * S, 0.3f, golden ? new Color(1f, 0.85f, 0.42f, 1f) : new Color(1f, 0.76f, 0.23f, 1f), 0.04f * S);
                    }
                    break;
            }
        }

        /// <summary>A tracer from the barrel to where the round stopped (<paramref name="units"/> from the shooter's centre along <paramref name="angle"/>).</summary>
        void AddTracer(Vector3 muzzle, Vector2 from, float angle, float units, Color c, float width, float life = 0.14f)
        {
            tracers.Add(new Tracer { From = muzzle, To = Along(from, angle, units, 0f, 1.0f), Color = c, Born = Time.time, Life = life, Width = width });
        }

        // ---------------------------------------------------------------- per frame

        void LateUpdate() => Draw();

        /// <summary>Draws this frame's effects (also called by tests).</summary>
        public void Draw()
        {
            Bind();
            MatchSim sim = host != null ? host.Sim : null;
            if (sim == null || world == null) return;
            foreach (SimPlayer p in sim.Order)
            {
                // Anyone Thomas armed can fire the Hemp Beam too.
                if (p.BeamT > 0f && p.Health != Health.Eliminated) PlayerBeam(p);
                if (p.Role != Role.Hunter || p.Health == Health.Eliminated) continue;
                ChargeRing(p);
                LungeLines(p);
                HempDisc(p);
            }
            DrawArcs();
            DrawWaves(sim);
            DrawVapes(sim);
            DrawTracers();
            DrawNpcs(sim);
            DoorSounds(sim);
            BeamSounds(sim);
            ReloadSounds(sim);
        }

        /// <summary>The machete's charge: a ring at his feet, dark red filling to bright red, pulsing once it's heavy.</summary>
        void ChargeRing(SimPlayer p)
        {
            if (p.ChargeT < 0f)
            {
                charging.Remove(p.Id);
                return;
            }
            if (!charging.ContainsKey(p.Id))
            {
                charging[p.Id] = Time.time;
            }
            float k = Mathf.Clamp01(p.ChargeT / Balance.Hunter.Attack.ChargeMax);
            bool heavy = p.ChargeT >= Balance.Hunter.Attack.HeavyAt;
            Color c = Color.Lerp(new Color(0x6a / 255f, 0x20 / 255f, 0x20 / 255f, 0.8f), new Color(0xb0 / 255f, 0x18 / 255f, 0x18 / 255f, 0.95f), k);
            float pulse = heavy ? 0.5f + 0.5f * Mathf.Sin(Time.time * 18f) : 0f;
            float r = Scale.D(Balance.HunterRadius * 2.2f) * S * (1f + 0.08f * pulse);
            Fx.Circle(Ground(p.Pos, 0.05f), r, 0.05f * S * (1f + pulse), c, true);
            // The filled part sweeps round with the charge.
            Fx.Arc(Ground(p.Pos, 0.05f), r * 0.82f, r * 0.92f, Mathf.PI / 2f, k * Mathf.PI * 2f, new Color(c.r, c.g, c.b, c.a * 0.7f), true);
        }

        /// <summary>Speed lines streaming behind Zach while he lunges.</summary>
        void LungeLines(SimPlayer p)
        {
            if (p.Move.LungeT <= 0f) return;
            float a = p.Move.LungeAng;
            for (int i = -1; i <= 1; i++)
            {
                pts.Clear(); cols.Clear();
                float side = i * Balance.HunterRadius * 0.9f;
                pts.Add(Along(p.Pos, a, -Balance.HunterRadius * 1.2f, side, 0.9f));
                pts.Add(Along(p.Pos, a, -Balance.HunterRadius * 6f, side, 0.9f));
                cols.Add(new Color(1f, 1f, 1f, 0.35f)); cols.Add(new Color(1f, 1f, 1f, 0f));
                Fx.Ribbon(pts, cols, 0.03f * S, true);
            }
        }

        /// <summary>The Hemp Battery: a green disc pulsing under him while it's on.</summary>
        void HempDisc(SimPlayer p)
        {
            if (!p.HempOn) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            float r = Scale.D(Balance.HunterRadius * (2.6f + 0.4f * pulse)) * S;
            Fx.Disc(Ground(p.Pos, 0.05f), r, new Color(0x6d / 255f, 1f, 0x6a / 255f, 0.07f + 0.06f * pulse), true);
            Fx.Circle(Ground(p.Pos, 0.05f), r, 0.04f * S, new Color(0x6d / 255f, 1f, 0x6a / 255f, 0.5f), true);
        }

        readonly Dictionary<int, ActionKind> lastAction = new Dictionary<int, ActionKind>();

        /// <summary>A trap being set.</summary>
        void ReloadSounds(MatchSim sim)
        {
            foreach (SimPlayer p in sim.Order)
            {
                lastAction.TryGetValue(p.Id, out ActionKind a);
                if (p.Action == ActionKind.Plant && a != ActionKind.Plant) Audio.PlayCue("trap", Units(p.Pos));
                lastAction[p.Id] = p.Action;
            }
        }

        bool[] lastDoors;

        /// <summary>Doors creak open and bang shut wherever they are and whoever moved them (a player or an NPC).</summary>
        void DoorSounds(MatchSim sim)
        {
            if (lastDoors == null || lastDoors.Length != sim.Doors.Length)
            {
                lastDoors = (bool[])sim.Doors.Clone();
                return;
            }
            for (int i = 0; i < lastDoors.Length; i++)
            {
                if (lastDoors[i] == sim.Doors[i]) continue;
                lastDoors[i] = sim.Doors[i];
                if (sim.DoorBroken[i]) continue;
                Vector2 at = (sim.Map.Doors[i].A + sim.Map.Doors[i].B) * 0.5f;
                Audio.PlayCue(sim.Doors[i] ? "door_open" : "door_close", Units(at));
            }
        }

        void BeamSounds(MatchSim sim)
        {
            beamGone.Clear();
            foreach (int id in beamLoops) beamGone.Add(id);
            foreach (SimPlayer p in sim.Order)
            {
                if (p.BeamT <= 0f) continue;
                beamGone.Remove(p.Id);
                beamLoops.Add(p.Id);
                Audio.Loop("beam" + p.Id, "repulsor", Units(p.Pos), Balance.Hunter.Beam.AudioVolume, Balance.Hunter.Beam.AudioNear, Balance.Hunter.Beam.AudioRadius, Balance.Hunter.Beam.AudioCurve, true);
            }
            foreach (int id in beamGone)
            {
                Audio.Loop("beam" + id, null);
                beamLoops.Remove(id);
            }
        }

        /// <summary>The swing's smear: a ten-slice arc in front of him, pale for a light swing, dark blood-red for a heavy one.</summary>
        void DrawArcs()
        {
            float now = Now;
            for (int i = arcs.Count - 1; i >= 0; i--)
            {
                Arc a = arcs[i];
                float k = (now - a.Born) / 0.25f;
                if (k >= 1f) { arcs.RemoveAt(i); continue; }
                Color c = a.Heavy ? new Color(0x5a / 255f, 0x08 / 255f, 0x08 / 255f, 0.75f * (1f - k)) : new Color(0xb8 / 255f, 0xb0 / 255f, 0xa0 / 255f, 0.55f * (1f - k));
                float half = a.ArcDeg * 0.5f * Mathf.Deg2Rad;
                float r = Scale.D(a.Radius) * S;
                // Sweeps across over the first part of its life.
                float sweep = Mathf.Clamp01(k * 2.5f);
                // A forehand sweeps from his right to his left, a backhand back the other way.
                float start = a.Back ? a.Angle + half - half * 2f * sweep : a.Angle - half;
                Fx.Arc(Ground(a.At, 0.9f), r * 0.55f, r, start, half * 2f * sweep, c, true, Vfx.Blend.Alpha);
            }
        }

        /// <summary>
        /// The Soundcloud Burst: a sound wave racing straight on through walls, purple and translucent. Its crest follows
        /// the concave lens the rules hit with: a thin bright leading edge, a soft violet body, and pressure bands rippling
        /// behind it like compressions in the air, the whole front trembling along its length and thinning out at its
        /// tips. Behind it a faint smear of where it just was, gone in a tenth of a second.
        /// </summary>
        void DrawWaves(MatchSim sim)
        {
            float now = Now;
            float half = Balance.Hunter.Burst.Width / 2f, speed = Balance.Hunter.Burst.Speed, T = Balance.Hunter.Burst.Thickness;
            const int N = 32;
            for (int w = waves.Count - 1; w >= 0; w--)
            {
                Wave wv = waves[w];
                float age = now - wv.Born;
                float front = speed * age;
                if (front - T > Balance.World.Size * 1.5f) { waves.RemoveAt(w); continue; }
                float born = Mathf.Clamp01(age / 0.08f);
                void Front(float back, float widthUnits, Color c, float tremble, float freq, float phase)
                {
                    pts.Clear(); cols.Clear();
                    for (int i = 0; i <= N; i++)
                    {
                        float sUnits = -half + Balance.Hunter.Burst.Width * i / N;
                        float u = sUnits / half;
                        // The crest shivers along its length, two frequencies beating against each other.
                        float shake = (Mathf.Sin(u * freq + now * 38f + phase) + 0.45f * Mathf.Sin(u * freq * 2.3f - now * 61f + phase * 1.7f)) * tremble;
                        pts.Add(Along(wv.At, wv.Angle, front - back + MatchRules.BurstSag(sUnits) + shake, sUnits, 0.35f));
                        float tip = 1f - Mathf.Pow(Mathf.Abs(u), 6f);
                        Color col = c;
                        col.a *= tip * born;
                        cols.Add(col);
                    }
                    Fx.Ribbon(pts, cols, Scale.D(widthUnits) * S, false, true, Vfx.Blend.Additive, true);
                }
                // The faint smear behind: the front where it was over the last tenth of a second.
                for (int k = 5; k >= 1; k--)
                    Front(T * 0.5f + speed * 0.018f * k, T * 1.3f, new Color(0.42f, 0.18f, 0.85f, 0.055f * (1f - k / 6f)), 3f, 9f, k);
                // Pressure bands rippling behind the crest.
                for (int b = 3; b >= 1; b--)
                    Front(T * (0.85f + b * 0.8f), T * 0.32f, new Color(0.55f, 0.28f, 0.95f, 0.22f / b), 2.5f + b, 11f, b * 2.1f);
                // The body, the crest, and its bright leading edge.
                Front(T * 0.45f, T * 1.7f, new Color(0.45f, 0.18f, 0.88f, 0.16f), 2f, 8f, 0f);
                Front(0f, T * 0.6f, new Color(0.6f, 0.32f, 1f, 0.36f), 1.6f, 13f, 0.5f);
                Front(-T * 0.18f, T * 0.14f, new Color(0.88f, 0.76f, 1f, 0.62f), 1.2f, 17f, 1f);
            }
        }

        /// <summary>
        /// Penjamin's gas, as the original builds it: soft puffs billowing out along a narrow cone, each swelling as the
        /// front reaches it and drifting on, bigger and thinner the farther out; it rolls out in 0.6 s, hangs, then fades.
        /// It shows above the dark: Zach sees the gas run to the edge of his screen.
        /// </summary>
        void DrawVapes(MatchSim sim)
        {
            float now = sim.Time;
            float halfAng = Balance.Hunter.Vape.HalfAngleDeg * Mathf.Deg2Rad;
            float grow = Balance.Hunter.Vape.GrowTime, linger = Balance.Hunter.Vape.LingerTime, fadeT = Balance.Hunter.Vape.FadeTime;
            foreach (VapeCloud v in sim.Vapes)
            {
                float age = now - v.T0;
                float rUnits = Scale.ToUnits(v.Range);
                float k = Mathf.Min(1f, age / grow);
                float ext = rUnits * (1f - (1f - k) * (1f - k));
                float fade = Mathf.Clamp01((grow + linger + fadeT - age) / fadeT);
                int n = Mathf.CeilToInt(rUnits / 16f);
                for (int i = 0; i < n; i++)
                {
                    float h1 = Mathf.Sin(i * 12.9898f + v.Id * 1.3f) * 43758.5453f, r1 = h1 - Mathf.Floor(h1);
                    float h2 = Mathf.Sin(i * 78.233f + v.Id * 0.7f) * 12345.6789f, r2 = h2 - Mathf.Floor(h2);
                    float u = Mathf.Pow((i + r1 * 0.8f) / n, 0.9f);
                    float bas = u * rUnits;
                    if (bas > ext) continue;
                    float reached = grow * (1f - Mathf.Sqrt(Mathf.Max(0f, 1f - bas / rUnits)));
                    float since = Mathf.Max(0f, age - reached);
                    float g = 1f - Mathf.Exp(-since * 5f);
                    float d = bas + since * (14f + 18f * u);
                    float width = Mathf.Tan(halfAng) * Mathf.Max(d, 30f);
                    float side = (r2 * 2f - 1f) * width * 0.7f + Mathf.Sin(age * (0.8f + r1) + i * 1.9f) * width * 0.22f;
                    float falloff = 1f - 0.7f * u;
                    float size = (14f + width * 0.9f) * 0.5f * (0.55f + 0.45f * g) * (1f + since * 0.05f);
                    Color body = v.Nic ? (i % 4 == 0 ? Hex(0xa6dcff) : i % 3 == 0 ? Hex(0x4a9ae0) : Hex(0x72bcf0)) : (i % 4 == 0 ? Hex(0xf2e6a0) : i % 3 == 0 ? Hex(0xc8b040) : Hex(0xdccb5a));
                    body.a = 0.34f * falloff * fade * (0.4f + 0.6f * g);
                    Vector3 pos = Along(v.Origin, v.Angle, d, side, 1.0f);
                    Fx.Dot(pos, Scale.D(size) * S, body, false, true, Vfx.Blend.Alpha);
                    if (i % 2 == 0)
                    {
                        Color glow = v.Nic ? Hex(0xb8e6ff) : Hex(0xfff0a0);
                        glow.a = 0.16f * falloff * fade * g;
                        Fx.Dot(pos, Scale.D(size * 0.7f) * S, glow, false, true, Vfx.Blend.Additive);
                    }
                }
            }
        }

        void DrawTracers()
        {
            float now = Time.time;
            for (int i = tracers.Count - 1; i >= 0; i--)
            {
                Tracer t = tracers[i];
                float k = (now - t.Born) / t.Life;
                if (k >= 1f) { tracers.RemoveAt(i); continue; }
                pts.Clear(); cols.Clear();
                pts.Add(t.From); pts.Add(t.To);
                Color c = t.Color;
                c.a *= 1f - k;
                cols.Add(c); cols.Add(c);
                Fx.Ribbon(pts, cols, t.Width * S, false);
            }
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        public int ArcCount => arcs.Count;
        public int WaveCount => waves.Count;
        public int TracerCount => tracers.Count;
    }
}
