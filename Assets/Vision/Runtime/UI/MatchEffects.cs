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
    /// The match's effects and sounds in the world, after the original's look: Zach's machete (the swipe line off the
    /// blade's tip out to its reach, the slash), the lunge's speed lines, the Soundcloud Burst (a purple concave lens
    /// racing across the map through walls, with fading echoes), Penjamin's gas (yellow wisps streaming out along a narrow
    /// cone, blue for 50 Nic), the Hemp Beam (a charging orb, then a light green beam with an
    /// impact glow), guns (muzzle flash, rounds in flight), and the sounds of all of it, placed where it happens.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed partial class MatchEffects : MonoBehaviour
    {
        public SandboxWorld world;

        MatchHost host;

        /// <summary>A wave on its way out: Zach's Soundcloud Burst.</summary>
        struct Wave { public Vector2 At; public float Angle, Born; }

        readonly List<Vector3> arcGrid = new List<Vector3>(2048);
        readonly List<Wave> waves = new List<Wave>();
        readonly HashSet<int> beamLoops = new HashSet<int>();
        readonly List<int> beamGone = new List<int>();
        readonly List<Vector3> pts = new List<Vector3>(64);
        readonly List<Color> cols = new List<Color>(64);

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
                        if (e.B >= 2) Audio.PlayCue("swing_heavy_low", Units(by.Pos));
                    }
                    else if (e.G <= 0f && e.F >= 2f)
                    {
                        // It cut someone (a survivor or an NPC, never a door, window or barricade): the slash into flesh,
                        // and a charged cut flings blood off the tip of the blade.
                        Audio.PlayCue("slash", Units(by.Pos));
                        if (e.B >= 2) BladeDroplets(by.Id);
                    }
                    // The strike: wherever the blade meets something solid in its arc, sparks, chips and dust fly off it.
                    if (e.F >= 0f && e.G <= 0f) MacheteImpact(by, e.B >= 2);
                    break;
                case EventKind.Eliminated:
                    // Sacrificed (staked to the end, or gone into the woods): out of the match for good.
                    if (e.Text == "stake" || e.Text == "woods") Audio.PlayCue("sacrificed");
                    break;
                case EventKind.Escaped:
                    // Someone through an exit: the victory jingle, for everyone.
                    Audio.PlayCue("victory_royale");
                    break;
                case EventKind.Hit:
                    if (e.Text == "slash" || e.Text == "lunge" || e.Text == "punch") Audio.PlayCue("body_hit", Units(e.Pos), 0.8f);
                    break;
                case EventKind.Talk:
                    if (by == null) break;
                    if (e.Text == "burst") waves.Add(new Wave { At = e.Pos, Angle = e.F, Born = Now });
                    else if (e.Text == "possess")
                    {
                        // A ghost takes a body: a deep whoosh and a pale ring spreading from it as it starts to rise.
                        Audio.PlayCue("possess", Units(e.Pos));
                        Fx.RingAt(Ground(e.Pos, 0.05f), 0.2f * S, 1.6f * S, 1.4f, new Color(0.82f, 0.9f, 1f, 0.8f), 0.05f * S, false, true);
                    }
                    else if (e.Text == "reanimated")
                    {
                        // One with it: a flash of pale light where it stands, and the rush of it.
                        Audio.PlayCue("reanimate", Units(e.Pos));
                        Fx.RingAt(Ground(e.Pos, 0.05f), 0.3f * S, 2.4f * S, 0.9f, new Color(0.9f, 0.95f, 1f, 0.9f), 0.08f * S, false, true);
                        Fx.Burst(Ground(e.Pos, 1f), 26, 2.2f * S, 0.7f, new Color(0.88f, 0.94f, 1f, 1f), 0.07f * S, true, Vfx.Blend.Additive, 0f, 2f, 0.4f, new Color(0.6f, 0.75f, 1f, 1f));
                    }
                    else if (e.Text == "ghost_out")
                    {
                        // Knocked out of a body: the ghost flung back out of it in a burst of pale light.
                        Audio.PlayCue("ghost_out", Units(e.Pos));
                        Fx.Burst(Ground(e.Pos, 1f), 22, 2.8f * S, 0.6f, new Color(0.9f, 0.95f, 1f, 1f), 0.07f * S, true, Vfx.Blend.Additive, 0f, 2f, 0.4f, new Color(0.6f, 0.75f, 1f, 1f));
                        Fx.RingAt(Ground(e.Pos, 0.05f), 0.2f * S, 1.8f * S, 0.6f, new Color(0.9f, 0.95f, 1f, 0.8f), 0.06f * S, false, true);
                    }
                    else if (e.Text == "slam") Audio.PlayCue("slam", Units(e.Pos));
                    else if (e.Text == "drink") Audio.PlayCue("drink", Units(e.Pos));
                    else if (e.Text == "eat") Audio.PlayCue("eat", Units(e.Pos));
                    else if (e.Text == "pick_hit") Audio.PlayCue("pick_hit", Units(e.Pos));
                    else if (e.Text == "pick_miss") Audio.PlayCue("pick_miss", Units(e.Pos));
                    else if (e.Text == "pick_reset") { Audio.PlayCue("pick_miss", Units(e.Pos)); Audio.PlayCue("pick_reset", Units(e.Pos)); }
                    else if (e.Text == "gates") Audio.PlayCue("gate_swing", Units(e.Pos));
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
                        // A bottle broken on someone: the window's glass breaking, softer.
                        case "glass_body": Audio.PlayCue("glass", Units(e.Pos), SoundBank.BottleOnBodyVolume); break;
                        case "book": Audio.PlayCue("book_hit", Units(e.Pos)); break;
                        case "piss": Audio.PlayCue("splash", Units(e.Pos)); break;
                        case "smash": Audio.PlayCue("splinter", Units(e.Pos)); break;
                        case "door_hit": DoorStruck(e.Pos, sim, false); break;
                        case "door_smash": DoorStruck(e.Pos, sim, true); break;
                        case "gate_hit": Audio.PlayCue("metal_light", Units(e.Pos)); break;
                        case "gate_smash": Audio.PlayCue("metal_light", Units(e.Pos)); Audio.PlayCue("slam", Units(e.Pos)); break;
                        case "flesh": Audio.PlayCue("bullet_flesh", Units(e.Pos)); break;
                        case "gate_unlock": Audio.PlayCue("gate_unlock", Units(e.Pos)); Audio.PlayCue("gate_swing", Units(e.Pos)); break;
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
                    // And the victory jingle, for everyone.
                    Audio.PlayCue("victory_royale");
                    break;
                case EventKind.Jarvis:
                    // JARVIS coming on: psionic waves out from whoever's running it.
                    if (by != null) JarvisWaves(by.Pos);
                    break;
                case EventKind.Explosion:
                    // Chacko, Soham or Njaaron blown apart.
                    BloodyExplosion(e.Pos);
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
        /// <summary>
        /// Notes lying in the viewer's light glint now and then: a soft pale glow pulsing up and away over the paper, each
        /// on its own beat, so a note on the ground catches the eye.
        /// </summary>
        void NoteGlints()
        {
            if (world == null || world.Notes.Count == 0) return;
            Camera cam = Camera.main;
            float t = Time.time, s = S;
            for (int i = 0; i < world.Notes.Count; i++)
            {
                Transform n = world.Notes[i];
                if (n == null) continue;
                if (cam != null)
                {
                    Vector3 v = cam.WorldToViewportPoint(n.position);
                    if (v.x < -0.1f || v.x > 1.1f || v.y < -0.1f || v.y > 1.1f) continue;
                }
                float beat = Mathf.Repeat(t / 2.6f + i * 0.37f, 1f);
                float k = Mathf.Sin(Mathf.Clamp01(beat / 0.35f) * Mathf.PI);
                Vector3 at = n.position + Vector3.up * 0.05f * s;
                Fx.Glow(at, (0.55f + 0.25f * k) * s, new Color(1f, 0.96f, 0.82f, 0.18f + 0.32f * k), true, false);
            }
        }

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

        /// <summary>A gun going off: the flash at the muzzle, and a round flying from the barrel for each pellet or bullet.</summary>
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
                    Fire(muzzle, e.Pos, e.F, e.G > 0f ? e.G / Scale.Unit : 900f, PistolRound, 0);
                    Audio.PlayCue("pistol_shot", Units(e.Pos));
                    break;
                case Vision.Player.ItemType.Sniper:
                    SniperRound(muzzle, e.Pos, e.F);
                    Audio.PlayCue("sniper_shot", Units(e.Pos));
                    break;
                default:
                    Audio.PlayCue("shotgun_shot", Units(e.Pos));
                    // Pellets: "hit|angle*1000:distance,..." (original units).
                    string list = e.Text != null && e.Text.Contains("|") ? e.Text.Substring(e.Text.IndexOf('|') + 1) : "";
                    foreach (string p in list.Split(','))
                    {
                        int c = p.IndexOf(':');
                        if (c <= 0) continue;
                        if (!float.TryParse(p.Substring(0, c), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ang)) continue;
                        if (!float.TryParse(p.Substring(c + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float dist)) continue;
                        // Each pellet flies to where it stopped and sparks there as it arrives.
                        Fire(muzzle, e.Pos, ang / 1000f, dist, golden ? GoldenPellet : PelletRound, golden ? 2 : 1);
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- per frame

        void LateUpdate()
        {
            Draw();
            DrawJarvisWaves();
        }

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
                LungeLines(p);
            }
            BladeTrails(sim);
            PissDrips(sim);
            DrawRipples(sim);
            DrawWaves(sim);
            DrawVapes(sim);
            DrawRounds();
            DrawLasers(sim);
            NoteGlints();
            DrawNpcs(sim);
            DoorSounds(sim);
            BeamSounds(sim);
            ReloadSounds(sim);
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

        /// <summary>
        /// The indicator rings on the ground (Plasma's, the breath rings) are drawn 30% smaller and 30% thinner than they
        /// were, and 40% dimmer (at the user's request). Zach's charge and the Hemp Battery have no ring.
        /// </summary>
        const float RingSize = 0.7f, RingBright = 0.6f;

        /// <summary>
        /// A band on the ground between two radii (design units) about a point, from an angle for a sweep. Every 2.5 degrees
        /// round and every few centimetres across, each point is set on the ground's own facets (the very triangles the
        /// terrain is drawn with, or the building's floor), just above them, so it lies on every slope, bump and crease
        /// instead of floating over them or sinking into them.
        /// </summary>
        void GroundArc(Vector2 centre, float r0, float r1, float start, float sweep, Color color, bool masked = true, bool senses = false)
        {
            if (Mathf.Abs(sweep) < 1e-4f || color.a <= 0.002f) return;
            int n = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(sweep) / (Mathf.PI / 72f)));
            int rows = Mathf.Clamp(Mathf.CeilToInt((r1 - r0) / 0.1f), 1, 12);
            arcGrid.Clear();
            for (int i = 0; i <= n; i++)
            {
                float a = start + sweep * i / n;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                for (int j = 0; j <= rows; j++) arcGrid.Add(OnSurface(centre + d * Mathf.Lerp(r0, r1, j / (float)rows), 0.025f));
            }
            int w = rows + 1;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < rows; j++)
                    Fx.Quad(arcGrid[i * w + j], arcGrid[i * w + j + 1], arcGrid[(i + 1) * w + j + 1], arcGrid[(i + 1) * w + j], color, masked, senses);
        }

        /// <summary>A point just above the drawn ground (design units in, world out).</summary>
        Vector3 OnSurface(Vector2 at, float up) => world.transform.TransformPoint(new Vector3(at.x, world.SurfaceHeight(at) + up, at.y));

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
        /// Penjamin's cloud (yellow, or blue for 50 Nic): rolling out along its cone as the original's does (out in 0.6 s,
        /// hanging, then fading), drawn as wispy streaks of gas rather than puffs, each sitting on the terrain under it and
        /// thinning where it meets anything solid. It shows above the dark: Zach sees the gas run to the edge of his screen.
        /// </summary>
        void DrawVapes(MatchSim sim)
        {
            // Penjamin's gas: a cone of dense, billowing smoke rolling out from his mouth along the aim (Cold War's smoke,
            // tinted yellow, or blue for 50 Nic), growing out over the original's 0.6 s, hanging, then thinning away, seen
            // 2 s longer than it acts. Patchy, thick pockets and thin stretches with gaps through it. From the moment it's
            // fired the whole cloud drifts slowly off on a wind of its own (a random way for each cloud), spreading wider
            // and taller and thinning as it goes. All of this is only how it looks: where it acts doesn't move or grow.
            float now = sim.Time;
            float grow = Balance.Hunter.Vape.GrowTime, linger = Balance.Hunter.Vape.LingerTime, fadeT = Balance.Hunter.Vape.FadeTime;
            float seen = fadeT + Balance.Hunter.Vape.SeenExtra;
            foreach (VapeCloud v in sim.Vapes)
            {
                float age = now - v.T0;
                float k = Mathf.Min(1f, age / grow);
                float length = v.Range * (1f - (1f - k) * (1f - k)) * S;
                float fade = Mathf.Clamp01((grow + linger + seen - age) / seen);
                float endRadius = Mathf.Max(0.35f * S, Mathf.Tan(MatchSim.VapeHalfAngle(v.Nic) * Mathf.Deg2Rad) * length + 0.3f * S) * 1.15f;
                // Its wind: a direction of its own, steady for the cloud's life.
                float windAng = Hash01(v.Id * 7 + 3) * Mathf.PI * 2f;
                Vector3 wind = new Vector3(Mathf.Cos(windAng), 0f, Mathf.Sin(windAng)) * (VapeWind * S);
                // Blown: spreading out (wider and taller) and thinning, from the moment it leaves him.
                float spread = 1f + VapeSpreadWide * age, tall = 1f + VapeSpreadTall * age;
                // Spread through more of the air it's thinner gas, not just fainter: the medium itself loses density as
                // the cloud swells (the same gas through a bigger volume), and the whole fades a little too.
                float thin = 1f / (1f + VapeThin * age);
                float dense = 1f / (spread * Mathf.Sqrt(tall));
                var look = new VolumetricGas.Look
                {
                    TintA = v.Nic ? new Color(0.62f, 0.78f, 0.88f) : new Color(0.8f, 0.74f, 0.5f),
                    TintB = v.Nic ? new Color(0.25f, 0.42f, 0.62f) : new Color(0.5f, 0.43f, 0.22f),
                    // 60% less opaque than at first, then 30% less again (Penjamin's then 40% more again); thinning as it's
                    // blown about.
                    Opacity = fade * 0.4f * 0.7f * (v.Nic ? 1f : 1.4f) * thin,
                    Extinction = 1.5f / S * dense,
                    NoiseScale = 0.6f / S,
                    Erosion = 0.8f,
                    Drift = WorldDir(v.Angle) * (0.9f * S) + wind + Vector3.up * (0.12f * S),
                    Seed = v.Id,
                    Clumps = 1f,
                    Wobble = 0.55f,
                };
                Vector3 tip = Ground(v.Origin, 0f) + wind * age;
                float r = endRadius * spread;
                // In the scene, unmasked: it shows past his light, not cut to it.
                VolumetricGas.Cone(tip, WorldDir(v.Angle), length, r, 0.22f / spread, endRadius * 0.5f * tall, look, false, false, tall / spread);
            }
        }

        /// <summary>
        /// How Penjamin's cloud is blown about (only how it looks): its drift (metres a second, before the world's scale), how
        /// fast it spreads wider and taller (a share of its size a second), and how fast it thins.
        /// </summary>
        public const float VapeWind = 0.35f, VapeSpreadWide = 0.07f, VapeSpreadTall = 0.12f, VapeThin = 0.05f;

        /// <summary>A steady random number in [0, 1) for a whole number.</summary>
        static float Hash01(int i)
        {
            unchecked
            {
                uint h = (uint)i * 2654435761u;
                h ^= h >> 15;
                h *= 0x2c1b3c6d;
                h ^= h >> 12;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        public int TrailCount => trails.Count;
        public int WaveCount => waves.Count;
    }
}
