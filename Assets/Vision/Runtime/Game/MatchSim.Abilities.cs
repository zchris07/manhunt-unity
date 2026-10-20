using System.Collections.Generic;
using UnityEngine;
using Vision.Player;

namespace Vision.Game
{
    /// <summary>A Soundcloud Burst wave: origin, unit direction, launch time (design units).</summary>
    public sealed class BurstWave
    {
        public Vector2 Origin, Dir;
        public float T0;
        public int By;
        public readonly HashSet<object> Hit = new HashSet<object>();
    }

    /// <summary>A Penjamin cloud: a cone from Origin along Angle, growing out to Range (design units).</summary>
    public sealed class VapeCloud
    {
        public int Id;
        public Vector2 Origin;
        public float Angle, Range, T0;
        public int By;
        public bool Nic;
        public readonly HashSet<Npc> Provoked = new HashSet<Npc>();
        public readonly Dictionary<Npc, float> NpcAcc = new Dictionary<Npc, float>();
    }

    /// <summary>A 0.50 cal round: it pierces everything and never stops.</summary>
    public sealed class Bullet
    {
        public Vector2 Origin, Dir;
        public float Front;
        public int By;
        /// <summary>NPC rounds (Monique): how much of a survivor's health they take (-1: a player's round, which downs).</summary>
        public float Damage = -1f;
        public readonly HashSet<object> Hit = new HashSet<object>();
        public readonly List<(float t, int kind, int i)> Path = new List<(float, int, int)>();
    }

    public struct TrailPoint
    {
        public int Id;
        public Vector2 Pos;
        public float T;
        /// <summary>0 scent, 1 blood.</summary>
        public int Kind;
        public int Who;
        /// <summary>Scent: how far along that survivor's scent trail this point is (design units), so its pattern stays put.</summary>
        public float Dist;
    }

    /// <summary>Zach's abilities, JARVIS, Penjamin, the Hemp Beam and the 0.50 cal (abilities.ts, vape.ts, zachBeam.ts, sniper.ts).</summary>
    public sealed partial class MatchSim
    {
        public readonly List<BurstWave> Bursts = new List<BurstWave>();
        public readonly List<VapeCloud> Vapes = new List<VapeCloud>();
        public readonly List<Bullet> Snipes = new List<Bullet>();
        public readonly List<TrailPoint> Trails = new List<TrailPoint>();
        int trailSeq = 1;
        /// <summary>Seconds left of a JARVIS reveal: every survivor sees everything on screen.</summary>
        public float RevealT;
        /// <summary>JARVIS running (seconds left of it), who set it going, and how many times it's been used this match.</summary>
        public float JarvisRunT;
        public int JarvisBy, JarvisUses;

        public static bool AbilitiesOn(SimPlayer h) => h.Role == Role.Hunter && h.AbilityLockT <= 0f && h.Health != Health.Eliminated && h.KnockT <= 0f;

        /// <summary>Soundcloud Burst (F): an aimed wave through every wall; each survivor it passes is jump-scared.</summary>
        void TryBurst(SimPlayer h, float aim)
        {
            if (h.BurstCd > 0f || !AbilitiesOn(h)) return;
            h.BurstCd = Balance.Hunter.Burst.Cooldown;
            Bursts.Add(new BurstWave { Origin = h.Pos, Dir = new Vector2(Mathf.Cos(aim), Mathf.Sin(aim)), T0 = Time, By = h.Id });
            Emit(null, new GameEvent { Kind = EventKind.Talk, A = h.Id, Text = "burst", Pos = h.Pos, F = aim });
        }

        /// <summary>Hemp Battery (Q): on or off. On: wider view, light through walls, faster, while its charge lasts.</summary>
        void TryHemp(SimPlayer h)
        {
            if (!AbilitiesOn(h) || h.Zone >= 2) return;
            if (h.HempOn)
            {
                h.HempOn = false;
                return;
            }
            if (h.HempLock > 0f || (h.Hemp != 2 && h.HempLeft <= 0f)) return;
            h.HempOn = true;
            Emit(null, new GameEvent { Kind = EventKind.Hemp, A = h.Id });
        }

        void UpdateHemp(SimPlayer h, float dt)
        {
            h.HempLock = Mathf.Max(0f, h.HempLock - dt);
            if (h.HempOn && !AbilitiesOn(h)) h.HempOn = false;
            if (h.HempOn)
            {
                if (!TestMode && h.Hemp != 2) h.HempLeft = Mathf.Max(0f, h.HempLeft - dt);
                if (h.HempLeft <= 0f)
                {
                    h.HempOn = false;
                    h.HempLock = Balance.Hunter.Hemp.Lockout;
                    Tell(h, "Hemp Battery drained");
                }
            }
            else h.HempLeft = Mathf.Min(Balance.Hunter.Hemp.Duration, h.HempLeft + dt * Balance.Hunter.Hemp.Duration / Balance.Hunter.Hemp.Recover);
            h.Move.HempT = h.HempOn ? Balance.Hunter.Hemp.Grace : 0f;
        }

        /// <summary>
        /// JARVIS (Q, survivors with Sexton's tablet): for 20 s every survivor in the match gets its boot, its screen (with
        /// its recognition boxes), everything on their screen lit and Zach shown; the whole map only to the one who set it
        /// going. The tablet's ready again 90 s after it ends.
        /// </summary>
        public bool TryJarvis(SimPlayer p)
        {
            if (p.Jarvis != 1 && p.Jarvis != 3) return false;
            if (p.SilencedT > 0f) return false;
            if (p.Ghost || p.Health == Health.Eliminated || p.Health == Health.Escaped) return false;
            // No vision aids past the second ring; one at a time; the tablet cools down after each.
            if (p.Zone >= 2 || JarvisRunT > 0f || (p.JarvisCd > 0f && p.Jarvis != 3)) return false;
            JarvisRunT = Balance.Sexton.JarvisUserSec;
            JarvisBy = p.Id;
            JarvisUses++;
            p.JarvisSlot = p.SelSlot;
            RevealT = Balance.Sexton.JarvisUserSec;
            foreach (SimPlayer q in Order)
                if (q.Role == Role.Survivor && !q.Ghost && q.Health != Health.Eliminated && q.Health != Health.Escaped)
                {
                    q.JarvisT = Balance.Sexton.JarvisUserSec;
                    // No night vision while JARVIS runs.
                    q.GogglesOn = false;
                }
            Emit(null, new GameEvent { Kind = EventKind.Jarvis, A = p.Id, B = JarvisUses });
            return true;
        }

        /// <summary>JARVIS winding down, and the tablets' cooldowns (the user's starts as it ends).</summary>
        void UpdateJarvis(float dt)
        {
            if (JarvisRunT > 0f)
            {
                JarvisRunT = Mathf.Max(0f, JarvisRunT - dt);
                if (JarvisRunT <= 0f && Get(JarvisBy) is SimPlayer by && by.Jarvis == 1)
                {
                    by.JarvisCd = TestMode ? 0f : Balance.Sexton.JarvisCooldown;
                    Tell(by, $"JARVIS is recharging: {Mathf.RoundToInt(Balance.Sexton.JarvisCooldown)} s");
                }
            }
            foreach (SimPlayer p in Order)
                if (p.JarvisCd > 0f && JarvisRunT <= 0f) p.JarvisCd = Mathf.Max(0f, p.JarvisCd - dt);
        }

        void UpdateAbilities(float dt)
        {
            RevealT = Mathf.Max(0f, RevealT - dt);
            UpdateJarvis(dt);
            foreach (SimPlayer h in Order)
            {
                if (h.Role != Role.Hunter) continue;
                h.BurstCd = Mathf.Max(0f, h.BurstCd - dt);
                if (TestMode)
                {
                    h.BurstCd = 0f;
                    h.VapeCd = 0f;
                    h.Move.LungeCharges = Balance.Hunter.Lunge.Charges + h.JadenBonus * Balance.Hunter.JadenSlainLunge;
                    h.HempLock = 0f;
                    h.Move.LungeRecharge = 0f;
                }
                h.AbilityLockT = Mathf.Max(0f, h.AbilityLockT - dt);
                UpdateHemp(h, dt);
            }

            // Soundcloud Burst waves fly straight on through everything, tested against the band swept since last tick.
            float width = R(Balance.Hunter.Burst.Width), thick = R(Balance.Hunter.Burst.Thickness), speed = R(Balance.Hunter.Burst.Speed);
            float maxD = Map.HalfExtent * 2.9f + width;
            for (int k = Bursts.Count - 1; k >= 0; k--)
            {
                BurstWave b = Bursts[k];
                float front = speed * (Time - b.T0), prev = Mathf.Max(0f, front - speed * dt);
                bool Crosses(Vector2 at, float r)
                {
                    Vector2 rel = at - b.Origin;
                    float along = Vector2.Dot(rel, b.Dir), side = rel.x * -b.Dir.y + rel.y * b.Dir.x;
                    if (Mathf.Abs(side) > width / 2f + r) return false;
                    float sag = R(MatchRules.BurstSag(Scale.ToUnits(Mathf.Min(Mathf.Abs(side), width / 2f))));
                    return along >= prev - thick - sag - r && along <= front + sag + r;
                }
                foreach (SimPlayer p in Order)
                {
                    // Survivors still in it, and a body a ghost is in (scared all the same, hurt by nothing; no NPC hears of it).
                    bool body = InBody(p);
                    if (p.Role != Role.Survivor || b.Hit.Contains(p)) continue;
                    if (!body && (p.Ghost || p.Health == Health.Escaped || p.Health == Health.Eliminated)) continue;
                    if (!Crosses(p.Pos, p.RadiusD)) continue;
                    b.Hit.Add(p);
                    p.ScareT = Balance.Hunter.Burst.ScareTime;
                    if (!body) PlayerAttacked(p, Get(b.By));
                    Emit(p.Id, new GameEvent { Kind = EventKind.Scare, A = b.By });
                }
                // The wave also stuns NPCs that are being aggressive; any it passes over is set off by it (Monique and
                // Daphne turn their guns on Zach).
                foreach (Npc n in Npcs)
                {
                    if (!n.Solid || b.Hit.Contains(n) || !Crosses(n.Pos, n.HitRadius)) continue;
                    b.Hit.Add(n);
                    if (n.Aggressive && !n.SparedBy(Get(b.By))) n.StunT = Mathf.Max(n.StunT, Balance.Hunter.Burst.NpcStun);
                    n.BurstHit(this, Get(b.By));
                }
                if (prev >= maxD) Bursts.RemoveAt(k);
            }

            UpdateVapes(dt);
            UpdateBeams(dt);
            UpdateSnipes(dt);
        }

        // ---------------------------------------------------------------- Penjamin

        static float VapeLife => Balance.Hunter.Vape.GrowTime + Balance.Hunter.Vape.LingerTime + Balance.Hunter.Vape.FadeTime;

        /// <summary>How far a cloud reaches right now (it grows out over its grow time, easing off).</summary>
        public float VapeExtent(VapeCloud v)
        {
            float k = Mathf.Clamp01((Time - v.T0) / Balance.Hunter.Vape.GrowTime);
            return v.Range * (1f - (1f - k) * (1f - k));
        }

        /// <summary>
        /// Inside the cloud: within its reach along the aim, and no further to the side than the cone's edge plus the width
        /// the drawn cloud has right from his mouth (<see cref="Balance.Hunter.Vape.MouthHalfWidth"/>), so the narrow cone
        /// still takes in a survivor standing in the gas close up.
        /// </summary>
        static bool InCloud(VapeCloud v, float extent, Vector2 at)
        {
            Vector2 d = at - v.Origin;
            if (d.magnitude > extent) return false;
            if (d.magnitude < R(1f)) return true;
            var dir = new Vector2(Mathf.Cos(v.Angle), Mathf.Sin(v.Angle));
            float along = Vector2.Dot(d, dir);
            if (along <= 0f) return false;
            float side = Mathf.Abs(d.x * dir.y - d.y * dir.x);
            return side <= along * Mathf.Tan(VapeHalfAngle(v.Nic) * Mathf.Deg2Rad) + R(Balance.Hunter.Vape.MouthHalfWidth);
        }

        /// <summary>The cloud's half angle (degrees): 50 Nic's 20% wider than Penjamin's.</summary>
        public static float VapeHalfAngle(bool nic) => Balance.Hunter.Vape.HalfAngleDeg * (nic ? Balance.Hunter.Vape.NicAngleMul : 1f);

        static readonly Vector2[] Disc = BuildDisc();

        static Vector2[] BuildDisc()
        {
            var d = new List<Vector2> { Vector2.zero };
            for (int i = 0; i < 6; i++) d.Add(new Vector2(Mathf.Cos(i * Mathf.PI / 3f), Mathf.Sin(i * Mathf.PI / 3f)) * 0.5f);
            for (int i = 0; i < 12; i++) d.Add(new Vector2(Mathf.Cos(i * Mathf.PI / 6f + 0.26f), Mathf.Sin(i * Mathf.PI / 6f + 0.26f)) * 0.85f);
            return d.ToArray();
        }

        /// <summary>The share of a body (circle) inside the cloud.</summary>
        public static float VapeCoverage(VapeCloud v, float extent, Vector2 at, float r)
        {
            int n = 0;
            foreach (Vector2 o in Disc) if (InCloud(v, extent, at + o * r)) n++;
            return (float)n / Disc.Length;
        }

        /// <summary>Penjamin (Space): a cone of vape gas toward the cursor, reaching past the edge of his screen.</summary>
        void TryVape(SimPlayer h, float aim)
        {
            if (h.VapeCharges <= 0 || h.Role != Role.Hunter || h.AbilityLockT > 0f || h.KnockT > 0f) return;
            h.VapeCharges--;
            if (h.VapeCd <= 0f) h.VapeCd = Balance.Hunter.Vape.Cooldown;
            float reach = Mathf.Clamp(h.ViewReach > 0f ? h.ViewReach : Balance.Hunter.Vape.DefaultView, Balance.Hunter.Vape.MinView, Balance.Hunter.Vape.MaxView);
            SpawnVape(h.Pos, aim, R(reach * Balance.Hunter.Vape.ReachMul * (h.Nic ? Balance.Chacko.NicRangeMul : 1f)), h.Id, h.Nic);
        }

        /// <summary>A cloud from any spot (testing mode rolls one at a survivor from afar).</summary>
        public void SpawnVape(Vector2 at, float aim, float range, int by, bool nic)
        {
            var v = new VapeCloud { Id = AllocEntityId(), Origin = at, Angle = aim, Range = range, T0 = Time, By = by, Nic = nic };
            Vapes.Add(v);
            Emit(Near(at, Balance.Net.MaxSensingRadius + Scale.ToUnits(range)), new GameEvent { Kind = EventKind.Gas, A = by, Pos = at, F = aim, G = range, Text = nic ? "nic" : "vape" });
        }

        /// <summary>Puts a survivor under the vape's effects at a given strength (0 = the far end, 1 = point blank).</summary>
        static void VapeSurvivor(SimPlayer p, float near)
        {
            float k = Mathf.Clamp01(near);
            var V = (slow: Balance.Hunter.Vape.Slow, slowFar: Balance.Hunter.Vape.SlowFar, dps: Balance.Hunter.Vape.Dps, dpsFar: Balance.Hunter.Vape.DpsFar);
            p.VapeSlow = Mathf.Max(p.VapeSlowT > 0f ? p.VapeSlow : 0f, V.slowFar + (V.slow - V.slowFar) * k);
            p.VapeDps = Mathf.Max(p.VapeT > 0f ? p.VapeDps : 0f, V.dpsFar + (V.dps - V.dpsFar) * k);
            // Penjamin or 50 Nic: slow, blur and narrowed sight held while in it, then falling off together over five seconds.
            p.VapeSlowT = p.VapeT = p.DarkT = Balance.Hunter.Vape.Linger;
        }

        /// <summary>How much of the gas's lingering effects are left (1 in the cloud, falling linearly to 0 five seconds out).</summary>
        public static float VapeLeft(SimPlayer p) => Mathf.Clamp01(p.VapeT / Balance.Hunter.Vape.Linger);

        void UpdateVapes(float dt)
        {
            foreach (SimPlayer p in Order)
            {
                if (p.Role != Role.Hunter) continue;
                if (TestMode)
                {
                    p.VapeCharges = Balance.Hunter.Vape.Charges;
                    p.VapeCd = 0f;
                }
                else if (p.VapeCharges < Balance.Hunter.Vape.Charges)
                {
                    p.VapeCd -= dt;
                    if (p.VapeCd <= 0f)
                    {
                        p.VapeCharges++;
                        p.VapeCd = p.VapeCharges < Balance.Hunter.Vape.Charges ? p.VapeCd + Balance.Hunter.Vape.Cooldown : 0f;
                    }
                }
                else p.VapeCd = 0f;
            }
            // Kept a little after it stops acting, only to be seen thinning away.
            Vapes.RemoveAll(v => Time - v.T0 >= VapeLife + Balance.Hunter.Vape.SeenExtra);
            foreach (VapeCloud v in Vapes)
            {
                if (Time - v.T0 >= VapeLife) continue;
                float ext = VapeExtent(v);
                SimPlayer by = Get(v.By);
                foreach (SimPlayer p in Order)
                {
                    if (p.Role != Role.Survivor) continue;
                    // (A body a ghost is in is slowed and blinded too; the gas's burn passes it by.)
                    if (p.Health != Health.Healthy && p.Health != Health.Wounded && p.Health != Health.Downed && !InBody(p)) continue;
                    if (VapeCoverage(v, ext, p.Pos, p.RadiusD) < Balance.Hunter.Vape.Coverage) continue;
                    VapeSurvivor(p, 1f - Vector2.Distance(p.Pos, v.Origin) / v.Range);
                }
                if (by == null) continue;
                // NPCs that react to being attacked react to the gas: once as if attacked; those that can be hurt are also
                // slowed and take a light hit every 1.5 s in it.
                foreach (Npc n in Npcs)
                {
                    if (!n.Solid || !InCloud(v, ext, n.Pos)) continue;
                    if (v.Provoked.Add(n)) n.Provoke(this, by);
                    if (!n.VapeHurts) continue;
                    float k = Mathf.Clamp01(1f - Vector2.Distance(n.Pos, v.Origin) / v.Range);
                    n.VapeSlow = Mathf.Max(n.VapeSlowT > 0f ? n.VapeSlow : 0f, Balance.Hunter.Vape.SlowFar + (Balance.Hunter.Vape.Slow - Balance.Hunter.Vape.SlowFar) * k);
                    n.VapeSlowT = 0.3f;
                    float acc = (v.NpcAcc.TryGetValue(n, out float a0) ? a0 : 0f) + dt;
                    if (acc >= Balance.Hunter.Vape.NpcHitEvery)
                    {
                        v.NpcAcc[n] = 0f;
                        n.Slash(this, by, 1);
                    }
                    else v.NpcAcc[n] = acc;
                }
            }
            // A 0.50 cal round's slow: half speed, falling off linearly to nothing over a second.
            // (Slows that fade go on the movement's own per-tick multiplier, set afresh each tick.)
            foreach (SimPlayer p in Order)
            {
                p.Move.FadeMul = 1f;
                if (p.SniperSlowT <= 0f) continue;
                p.SniperSlowT = Mathf.Max(0f, p.SniperSlowT - dt);
                p.Move.FadeMul *= 1f - Balance.Items.Sniper.Slow * p.SniperSlowT / Balance.Items.Sniper.SlowTime;
            }
            // Lingering effects: slowed, blurred and narrowed in it, falling off together over five seconds once out;
            // choking in it and two seconds after.
            foreach (SimPlayer p in Order)
            {
                if (p.Role != Role.Survivor) continue;
                p.DarkT = Mathf.Max(0f, p.DarkT - dt);
                if (p.VapeSlowT > 0f)
                {
                    p.VapeSlowT = Mathf.Max(0f, p.VapeSlowT - dt);
                    p.Move.FadeMul *= 1f - p.VapeSlow * Mathf.Clamp01(p.VapeSlowT / Balance.Hunter.Vape.Linger);
                }
                if (p.VapeT <= 0f) continue;
                bool choking = p.VapeT > Balance.Hunter.Vape.Linger - Balance.Hunter.Vape.AfterTime;
                p.VapeT = Mathf.Max(0f, p.VapeT - dt);
                // Penjamin goes straight through the shield to the health bar (in it, and its two seconds after).
                if (choking) HurtSurvivor(p, p.VapeDps * dt, null, "vape");
            }
        }

        // ---------------------------------------------------------------- the Hemp Beam

        static bool BeamUsable(SimPlayer h) => h.Role == Role.Hunter ? AbilitiesOn(h) : h.Role == Role.Survivor && h.CanAct;

        /// <summary>The beam's ticks each door, window and pallet has taken (it gives at <see cref="Balance.Hunter.Beam.BarrierTicks"/>).</summary>
        int[] doorBeamTicks, windowBeamTicks, barricadeBeamTicks;

        /// <summary>Adds this frame's ticks to what a barrier has taken; true once that's enough to break it.</summary>
        bool BeamWears(int[] taken, int i, int ticks)
        {
            if (i < 0 || i >= taken.Length) return true;
            taken[i] += ticks;
            return taken[i] >= Balance.Hunter.Beam.BarrierTicks;
        }

        /// <summary>Hemp Beam (R): Sexton's own beam along the aim, swinging slowly; single-use charges with a short cooldown.</summary>
        void TryBeam(SimPlayer h, float aim)
        {
            if (h.Role == Role.Spectator || h.BeamCharges <= 0 || !BeamReady(h)) return;
            // Zach's Hemp Beam doesn't work past the first ring.
            if (h.Role == Role.Hunter && h.Zone >= 1) return;
            if (!TestMode) h.BeamCharges--;
            StartBeam(h, aim);
        }

        /// <summary>Not firing one already, cooled down, free to act and not carrying anyone.</summary>
        static bool BeamReady(SimPlayer h) => h.BeamCd <= 0f && h.BeamT <= 0f && BeamUsable(h) && h.Carrying == 0;

        /// <summary>A survivor's Hemp Beam item in hand, fired with a left click: one of its charges spent from the slot.</summary>
        void FireBeamItem(SimPlayer p, int slot, float aim)
        {
            if (!BeamReady(p)) return;
            StartBeam(p, aim);
            SpendRound(p, slot);
        }

        void StartBeam(SimPlayer h, float aim)
        {
            if (h.BeamId == 0) h.BeamId = AllocEntityId();
            h.BeamT = Balance.Hunter.Beam.Windup + Balance.Sexton.Defense.BeamTime;
            h.BeamTick = 0f;
            h.BeamAng = aim;
            h.BeamLen = 0f;
            h.BeamHit.Clear();
            h.BeamGate = -1;
            h.BeamGateT = 0f;
            h.ChargeT = -1f;
            Feed($"{h.Name} fired a Hemp Beam");
        }

        void UpdateBeams(float dt)
        {
            foreach (SimPlayer h in Order)
            {
                if (h.Role == Role.Spectator) continue;
                h.BeamCd = Mathf.Max(0f, h.BeamCd - dt);
                if (TestMode && h.BeamT <= 0f) h.BeamCd = 0f;
                if (h.BeamT <= 0f) continue;
                if (!BeamUsable(h))
                {
                    h.BeamT = 0f;
                    h.BeamCd = Balance.Hunter.Beam.Cooldown;
                    continue;
                }
                h.BeamT = Mathf.Max(0f, h.BeamT - dt);
                h.ChargeT = -1f;
                // The beam swings toward the aim, slowly enough to dodge.
                float da = Mathf.DeltaAngle(h.BeamAng * Mathf.Rad2Deg, h.LastCmd.Aim * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                h.BeamAng += Mathf.Clamp(da, -Balance.Sexton.Defense.TurnRate * dt, Balance.Sexton.Defense.TurnRate * dt);
                if (h.BeamT > Balance.Sexton.Defense.BeamTime) h.BeamLen = 0f;   // charging
                else
                {
                    h.BeamTick += dt;
                    int ticks = 0;
                    while (h.BeamTick >= 1f / Balance.Hunter.Beam.TickRate - 1e-6f)
                    {
                        h.BeamTick -= 1f / Balance.Hunter.Beam.TickRate;
                        ticks++;
                    }
                    FireBeam(h, ticks, dt);
                }
                if (h.BeamT <= 0f) h.BeamCd = Balance.Hunter.Beam.Cooldown;
            }
        }

        /// <summary>
        /// The beam runs until it meets something solid or a person, and hurts whoever it touches: Zach or a survivor, the
        /// beamer's own side too (1 HP ten times a second). What it can destroy gives to the fifth of its ticks on it (half
        /// a second, the user's rule): a window's glass (which stops it till then; then it carries on through the empty
        /// frame), a door (shut or standing open), a pallet (standing or dropped). The graveyard's gates, shut, stop it dead
        /// like a wall (it burns into the bars, never through them); unlocked and shut again, they take a whole charge: held
        /// on them for all of one beam's firing, they give (locked, they hold; shutters never give).
        /// </summary>
        void FireBeam(SimPlayer h, int ticks, float dt)
        {
            var d = new Vector2(Mathf.Cos(h.BeamAng), Mathf.Sin(h.BeamAng));
            Vector2 s = h.Pos + d * (h.RadiusD + R(2f));
            float range = R(Balance.Sexton.Defense.BeamRange);
            float wall = Mathf.Min(Geo.CastSight(s, d, range), range);
            float len = wall;
            for (int guard = 0; guard < 8; guard++)
            {
                float t = WindowHit(s, d, wall, out int win);
                if (win < 0) break;
                if (!BeamWears(windowBeamTicks, win, ticks))
                {
                    len = Mathf.Min(len, t);
                    break;
                }
                BreakWindow(win);
                Noise(s + d * t, 800f, "glass");
            }
            // A shut graveyard gate (locked or not) stops it like a wall, the nearest it meets: it burns into the bars.
            int onGate = -1;
            for (int i = 0; i < Map.Doors.Count; i++)
            {
                if (!Map.Doors[i].Gate || DoorBroken[i] || Doors[i]) continue;
                float t = RaySegment(s, d, Map.Doors[i].A, Map.Doors[i].B);
                if (t > len) continue;
                len = t;
                onGate = i;
            }
            // What it can destroy gives to its fifth tick: a shut door at its end, a door standing open (its panel swung out
            // from the hinge), a pallet (standing or dropped) it crosses; the graveyard's gates, unlocked and shut again,
            // only after a whole charge on them; never a locked gate or a shutter.
            for (int i = 0; i < Map.Doors.Count; i++)
            {
                if (Map.Doors[i].Gate || DoorBroken[i] || Map.Doors[i].Shutter || DoorLocked[i]) continue;
                Vector2 da = Map.Doors[i].A, db = Map.Doors[i].B;
                float hitT = float.PositiveInfinity;
                if (!Doors[i])
                {
                    float t = RaySegment(s, d, da, db);
                    if (t <= len + R(6f)) hitT = t;
                }
                else
                {
                    Vector2 along = db - da;
                    foreach (float sg in new[] { 1f, -1f })
                    {
                        float ang = sg * DoorOpenDeg * Mathf.Deg2Rad, cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                        Vector2 leaf = da + new Vector2(along.x * cs - along.y * sn, along.x * sn + along.y * cs);
                        float t = RaySegment(s, d, da, leaf);
                        if (t <= len + R(6f)) hitT = Mathf.Min(hitT, t);
                    }
                }
                if (float.IsPositiveInfinity(hitT)) continue;
                if (!BeamWears(doorBeamTicks, i, ticks)) continue;
                SetDoor(i, true);
                DoorBroken[i] = true;
                Noise(s + d * Mathf.Min(hitT, len), 900f, "door_smash");
            }
            if (onGate != h.BeamGate)
            {
                h.BeamGate = onGate;
                h.BeamGateT = 0f;
            }
            if (onGate >= 0 && !DoorLocked[onGate])
            {
                h.BeamGateT += dt;
                // (Half a tick short, so a beam held on them from its first tick to its last does it.)
                if (h.BeamGateT >= Balance.Sexton.Defense.BeamTime - TickDt * 0.5f)
                {
                    Vector2 c = (Map.Doors[onGate].A + Map.Doors[onGate].B) * 0.5f;
                    SetDoor(onGate, true);
                    DoorBroken[onGate] = true;
                    Noise(c, 900f, "gate_smash");
                    h.BeamGate = -1;
                    h.BeamGateT = 0f;
                }
            }
            for (int i = 0; i < Map.Barricades.Count; i++)
            {
                if (Barricades[i] == BarricadeState.Broken) continue;
                float t = RaySegment(s, d, Map.Barricades[i].A, Map.Barricades[i].B);
                if (t > len + R(6f)) continue;
                if (!BeamWears(barricadeBeamTicks, i, ticks)) continue;
                SetBarricade(i, BarricadeState.Broken);
                Noise(s + d * Mathf.Min(t, len), 700f, "smash");
            }
            SimPlayer victim = null;
            float vt = len, halfW = R(Balance.Sexton.Defense.BeamWidth) / 2f;
            bool vsZach = h.Role == Role.Survivor;
            foreach (SimPlayer p in Order)
            {
                if (p == h || p.Role == Role.Spectator) continue;
                // A body a ghost is in stops Zach's beam (one tick knocks the ghost out); a survivor's passes through it.
                bool body = InBody(p);
                if (body && h.Role != Role.Hunter) continue;
                if (!body && (p.Role == Role.Hunter ? p.Health == Health.Eliminated : (p.Health != Health.Healthy && p.Health != Health.Wounded))) continue;
                float along = Vector2.Dot(p.Pos - s, d);
                if (along < 0f || along > vt) continue;
                if (SimMap.SegmentDistance(p.Pos, s, s + d * len) > p.RadiusD + halfW) continue;
                vt = Mathf.Max(0f, along - p.RadiusD * 0.6f);
                victim = p;
            }
            h.BeamLen = vt + h.RadiusD + R(2f);
            if (victim != null && InBody(victim))
            {
                if (ticks > 0) KnockOut(victim, h, "beam");
            }
            else if (victim != null)
                for (int i = 0; i < ticks; i++)
                {
                    h.BeamFlinch++;
                    string kind = h.BeamFlinch % 3 == 0 ? "beam" : "gas";
                    if (victim.Role == Role.Hunter) HurtHunter(victim, Balance.Hunter.Beam.ZachTickDamage * Balance.Hunter.Health.Max, h, kind);
                    else HurtSurvivor(victim, Balance.Hunter.Beam.TickDamage, h, kind);
                    if (victim.Health == Health.Downed || victim.KnockT > 0f) break;
                }
            // NPCs along it: a light hit every half second while it touches (a survivor's beam counts as a thrown item).
            Vector2 e = s + d * vt;
            foreach (Npc n in Npcs)
            {
                if (!n.Solid) continue;
                if (SimMap.SegmentDistance(n.Pos, s, e) > n.HitRadius + halfW) continue;
                string key = n.Name;
                if (h.BeamHit.TryGetValue(key, out float last) && Time - last < Balance.Hunter.Beam.NpcHitEvery - 1e-6f) continue;
                h.BeamHit[key] = Time;
                if (vsZach) n.ItemHit(this, h, "bottle");
                else if (n.Slashable) n.BeamHit(this, h);
            }
        }

        // ---------------------------------------------------------------- the 0.50 cal

        /// <summary>A player fired a gun (a P250, a shotgun, the 0.50 cal): any NPC that minds gunfire nearby hears it.</summary>
        void Gunfire(SimPlayer p)
        {
            foreach (Npc n in Npcs) if (n.Alive) n.Gunfire(this, p);
        }

        /// <summary>Sends a round away from a point along aim; by is the shooter (0 for an NPC, whose rounds take damage and spare other NPCs).</summary>
        public void SpawnBullet(Vector2 origin, float aim, int by, float damage = -1f)
        {
            var b = new Bullet { Origin = origin, Dir = new Vector2(Mathf.Cos(aim), Mathf.Sin(aim)), By = by, Damage = damage };
            for (int i = 0; i < Map.Windows.Count; i++)
            {
                if (WindowsBroken[i]) continue;
                float t = RaySegment(origin, b.Dir, Map.Windows[i].A, Map.Windows[i].B);
                if (!float.IsPositiveInfinity(t)) b.Path.Add((t, 0, i));
            }
            for (int i = 0; i < Map.Doors.Count; i++)
            {
                if (Doors[i] || DoorBroken[i] || Map.Doors[i].Gate) continue;
                float t = RaySegment(origin, b.Dir, Map.Doors[i].A, Map.Doors[i].B);
                if (!float.IsPositiveInfinity(t)) b.Path.Add((t, 1, i));
            }
            for (int i = 0; i < Map.Barricades.Count; i++)
            {
                if (Barricades[i] != BarricadeState.Down) continue;
                float t = RaySegment(origin, b.Dir, Map.Barricades[i].A, Map.Barricades[i].B);
                if (!float.IsPositiveInfinity(t)) b.Path.Add((t, 2, i));
            }
            for (int i = 0; i < Map.Generators.Count; i++)
            {
                if (Gens[i].Repaired) continue;
                float t = RayCircle(origin, b.Dir, Map.Generators[i], R(36f));
                if (!float.IsPositiveInfinity(t)) b.Path.Add((t, 3, i));
            }
            b.Path.Sort((x, y) => x.t.CompareTo(y.t));
            Snipes.Add(b);
            Emit(null, new GameEvent { Kind = EventKind.Shot, A = by, B = (int)ItemType.Sniper, Pos = origin, F = aim });
            Noise(origin, 2400f, "shot");
        }

        /// <summary>Aimed (right click), the round goes exactly where it's pointed; from the hip it strays up to 5 degrees.</summary>
        void FireSniper(SimPlayer p, float aim)
        {
            if (!p.Aiming) aim += RandRange(-1f, 1f) * Balance.Items.Sniper.HipSpreadDeg * Mathf.Deg2Rad;
            SpawnBullet(p.Pos + new Vector2(Mathf.Cos(aim), Mathf.Sin(aim)) * (p.RadiusD + R(2f)), aim, p.Id);
            Gunfire(p);
        }

        void UpdateSnipes(float dt)
        {
            float maxD = Map.HalfExtent * 2.9f + R(100f);
            for (int k = Snipes.Count - 1; k >= 0; k--)
            {
                Bullet b = Snipes[k];
                float prev = b.Front;
                b.Front += R(Balance.Items.Sniper.Speed) * dt;
                Vector2 a = b.Origin + b.Dir * prev, e = b.Origin + b.Dir * b.Front;
                SimPlayer shooter = Get(b.By);
                foreach ((float t, int kind, int i) in b.Path)
                {
                    if (t <= prev || t > b.Front) continue;
                    Vector2 at = b.Origin + b.Dir * t;
                    if (kind == 0 && !WindowsBroken[i])
                    {
                        BreakWindow(i);
                        Noise(at, 800f, "glass");
                    }
                    else if (kind == 1 && !DoorBroken[i])
                    {
                        SetDoor(i, true);
                        DoorBroken[i] = true;
                        Noise(at, 900f, "door_smash");
                    }
                    else if (kind == 2 && Barricades[i] == BarricadeState.Down)
                    {
                        SetBarricade(i, BarricadeState.Broken);
                        Noise(at, 700f, "smash");
                    }
                    else if (kind == 3 && !Gens[i].Repaired)
                    {
                        Gens[i].Progress = Mathf.Max(0f, Gens[i].Progress - Balance.Items.Sniper.GenDamage);
                        Noise(at, 900f, "gen_explode");
                    }
                }
                float hitR = R(Balance.Items.Sniper.HitRadius);
                if (b.By > 0) HitTraps(a, b.Dir, b.Front - prev, shooter?.Name ?? "Someone");
                foreach (SimPlayer q in Order)
                {
                    if (q.Id == b.By || b.Hit.Contains(q)) continue;
                    if (SimMap.SegmentDistance(q.Pos, a, e) > q.RadiusD + hitR) continue;
                    if (q.Role == Role.Survivor && (q.Health == Health.Healthy || q.Health == Health.Wounded))
                    {
                        b.Hit.Add(q);
                        BulletStruck(q.Pos);
                        HurtSurvivor(q, b.Damage >= 0f ? b.Damage : 10f, shooter, "bullet");
                    }
                    else if (q.Role == Role.Hunter && q.Health != Health.Eliminated)
                    {
                        b.Hit.Add(q);
                        BulletStruck(q.Pos);
                        HurtHunter(q, Balance.Items.Sniper.ZachHp * (shooter != null && shooter.Role == Role.Survivor ? MatchState.Current.Tuning.SniperDamage : 1f), shooter, "bullet");
                        Knock(q, Balance.Items.Sniper.KbDuration, Balance.Items.Sniper.KbPeak, Mathf.Atan2(b.Dir.y, b.Dir.x));
                        q.SniperSlowT = Balance.Items.Sniper.SlowTime;
                        q.Move.StaminaLock = Mathf.Max(q.Move.StaminaLock, Balance.Items.Sniper.SprintLock);
                        q.Move.SprintBlocked = true;
                        Feed($"{shooter?.Name ?? "Someone"} hit {q.Name} with a 0.50 cal");
                    }
                }
                if (shooter != null && b.Damage < 0f)
                    foreach (Npc n in Npcs)
                    {
                        if (!n.Solid || b.Hit.Contains(n) || !CanShoot(n, shooter)) continue;
                        if (SimMap.SegmentDistance(n.Pos, a, e) > n.HitRadius + hitR) continue;
                        b.Hit.Add(n);
                        BulletStruck(n.Pos);
                        n.Snipe(this, shooter);
                    }
                if (b.Front >= maxD) Snipes.RemoveAt(k);
            }
        }

        /// <summary>A survivor holding the 0.50 cal with it up: its laser shows to everyone.</summary>
        public static bool LaserHolder(SimPlayer p) =>
            p.Role == Role.Survivor && p.CanAct && p.Selected != null && p.Selected.item == ItemType.Sniper;
    }
}
