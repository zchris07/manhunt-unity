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
            if (!AbilitiesOn(h)) return;
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

        /// <summary>JARVIS (Q, survivors with Sexton's tablet): for 10 s everyone's whole screen is visible; its user sees the map and Zach.</summary>
        void TryJarvis(SimPlayer p)
        {
            if (p.Jarvis != 1 && p.Jarvis != 3) return;
            if (p.Health == Health.Eliminated || p.Health == Health.Escaped) return;
            if (p.Jarvis == 1) p.Jarvis = 2;
            p.JarvisT = Balance.Sexton.JarvisRadarSec;
            RevealT = Balance.Sexton.JarvisRadarSec;
            Emit(null, new GameEvent { Kind = EventKind.Jarvis, A = p.Id });
        }

        void UpdateAbilities(float dt)
        {
            RevealT = Mathf.Max(0f, RevealT - dt);
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
                    if (p.Role != Role.Survivor || b.Hit.Contains(p) || p.Health == Health.Escaped || p.Health == Health.Eliminated) continue;
                    if (!Crosses(p.Pos, p.RadiusD)) continue;
                    b.Hit.Add(p);
                    p.ScareT = Balance.Hunter.Burst.ScareTime;
                    Emit(p.Id, new GameEvent { Kind = EventKind.Scare, A = b.By });
                }
                // The wave also stuns NPCs that are being aggressive.
                foreach (Npc n in Npcs)
                {
                    if (!n.Aggressive || b.Hit.Contains(n) || !Crosses(n.Pos, n.HitRadius)) continue;
                    b.Hit.Add(n);
                    n.StunT = Mathf.Max(n.StunT, Balance.Hunter.Burst.NpcStun);
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

        static bool InCloud(VapeCloud v, float extent, Vector2 at)
        {
            float d = Vector2.Distance(at, v.Origin);
            if (d > extent) return false;
            if (d < R(1f)) return true;
            float a = Mathf.Atan2(at.y - v.Origin.y, at.x - v.Origin.x);
            return Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, v.Angle * Mathf.Rad2Deg)) <= Balance.Hunter.Vape.HalfAngleDeg;
        }

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
            p.VapeSlowT = Balance.Hunter.Vape.SlowAfter;
            p.VapeDps = Mathf.Max(p.VapeT > 0f ? p.VapeDps : 0f, V.dpsFar + (V.dps - V.dpsFar) * k);
            p.VapeT = Balance.Hunter.Vape.AfterTime;
            p.DarkT = Balance.Hunter.Vape.DarkAfter;
        }

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
            Vapes.RemoveAll(v => Time - v.T0 >= VapeLife);
            foreach (VapeCloud v in Vapes)
            {
                float ext = VapeExtent(v);
                SimPlayer by = Get(v.By);
                foreach (SimPlayer p in Order)
                {
                    if (p.Role != Role.Survivor || p.HideState == 2) continue;
                    if (p.Health != Health.Healthy && p.Health != Health.Wounded && p.Health != Health.Downed) continue;
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
            // Lingering effects: slowed and choking while in it and a little after; dark a while longer.
            foreach (SimPlayer p in Order)
            {
                if (p.Role != Role.Survivor) continue;
                p.DarkT = Mathf.Max(0f, p.DarkT - dt);
                if (p.VapeSlowT > 0f)
                {
                    p.VapeSlowT = Mathf.Max(0f, p.VapeSlowT - dt);
                    p.Move.SlowT = Mathf.Max(p.Move.SlowT, 0.1f);
                    p.Move.SlowMul = Mathf.Min(p.Move.SlowMul, 1f - p.VapeSlow);
                }
                if (p.VapeT <= 0f) continue;
                p.VapeT = Mathf.Max(0f, p.VapeT - dt);
                HurtSurvivor(p, p.VapeDps * dt, null, "gas");
            }
        }

        // ---------------------------------------------------------------- the Hemp Beam

        static bool BeamUsable(SimPlayer h) => h.Role == Role.Hunter ? AbilitiesOn(h) : h.Role == Role.Survivor && h.CanAct;

        /// <summary>Hemp Beam (R): Sexton's own beam along the aim, swinging slowly; single-use charges with a short cooldown.</summary>
        void TryBeam(SimPlayer h, float aim)
        {
            if (h.Role == Role.Spectator || h.BeamCharges <= 0 || h.BeamCd > 0f || h.BeamT > 0f || !BeamUsable(h) || h.Carrying != 0) return;
            if (h.BeamId == 0) h.BeamId = AllocEntityId();
            if (!TestMode) h.BeamCharges--;
            h.BeamT = Balance.Hunter.Beam.Windup + Balance.Sexton.Defense.BeamTime;
            h.BeamTick = 0f;
            h.BeamAng = aim;
            h.BeamLen = 0f;
            h.BeamHit.Clear();
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
                    FireBeam(h, ticks);
                }
                if (h.BeamT <= 0f) h.BeamCd = Balance.Hunter.Beam.Cooldown;
            }
        }

        /// <summary>The beam runs until it meets something solid or a person, and hurts what it touches.</summary>
        void FireBeam(SimPlayer h, int ticks)
        {
            var d = new Vector2(Mathf.Cos(h.BeamAng), Mathf.Sin(h.BeamAng));
            Vector2 s = h.Pos + d * (h.RadiusD + R(2f));
            float range = R(Balance.Sexton.Defense.BeamRange);
            float len = Mathf.Min(Geo.CastSight(s, d, range), WindowHit(s, d, range, out _));
            SimPlayer victim = null;
            float vt = len, halfW = R(Balance.Sexton.Defense.BeamWidth) / 2f;
            bool vsZach = h.Role == Role.Survivor;
            foreach (SimPlayer p in Order)
            {
                if (vsZach ? p.Role != Role.Hunter || p.Health == Health.Eliminated : p.Role != Role.Survivor || (p.Health != Health.Healthy && p.Health != Health.Wounded) || p.HideState == 2) continue;
                float along = Vector2.Dot(p.Pos - s, d);
                if (along < 0f || along > vt) continue;
                if (SimMap.SegmentDistance(p.Pos, s, s + d * len) > p.RadiusD + halfW) continue;
                vt = Mathf.Max(0f, along - p.RadiusD * 0.6f);
                victim = p;
            }
            h.BeamLen = vt + h.RadiusD + R(2f);
            if (victim != null)
                for (int i = 0; i < ticks; i++)
                {
                    h.BeamFlinch++;
                    string kind = h.BeamFlinch % 3 == 0 ? "beam" : "gas";
                    if (vsZach) HurtHunter(victim, Balance.Hunter.Beam.ZachTickDamage * Balance.Hunter.Health.Max, h, kind);
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
                else if (n.Slashable) n.Slash(this, h, 1);
            }
        }

        // ---------------------------------------------------------------- the 0.50 cal

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
                if (Doors[i] || DoorBroken[i]) continue;
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

        void FireSniper(SimPlayer p, float aim) => SpawnBullet(p.Pos + new Vector2(Mathf.Cos(aim), Mathf.Sin(aim)) * (p.RadiusD + R(2f)), aim, p.Id);

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
                foreach (SimPlayer q in Order)
                {
                    if (q.Id == b.By || b.Hit.Contains(q)) continue;
                    if (SimMap.SegmentDistance(q.Pos, a, e) > q.RadiusD + hitR) continue;
                    if (q.Role == Role.Survivor && (q.Health == Health.Healthy || q.Health == Health.Wounded) && q.HideState != 2)
                    {
                        b.Hit.Add(q);
                        HurtSurvivor(q, b.Damage >= 0f ? b.Damage : 10f, shooter, "bullet");
                    }
                    else if (q.Role == Role.Hunter && q.Health != Health.Eliminated)
                    {
                        b.Hit.Add(q);
                        HurtHunter(q, Balance.Items.Sniper.ZachHp, shooter, "bullet");
                        Knock(q, Balance.Items.Sniper.KbDuration, Balance.Items.Sniper.KbPeak, Mathf.Atan2(b.Dir.y, b.Dir.x));
                        q.Move.StaminaLock = Mathf.Max(q.Move.StaminaLock, Balance.Items.Sniper.SprintLock);
                        q.Move.SprintBlocked = true;
                        Feed($"{shooter?.Name ?? "Someone"} hit {q.Name} with a 0.50 cal");
                    }
                }
                if (shooter != null && b.Damage < 0f)
                    foreach (Npc n in Npcs)
                    {
                        if (!n.Solid || b.Hit.Contains(n)) continue;
                        if (SimMap.SegmentDistance(n.Pos, a, e) > n.HitRadius + hitR) continue;
                        b.Hit.Add(n);
                        n.Snipe(this, shooter);
                    }
                if (b.Front >= maxD) Snipes.RemoveAt(k);
            }
        }

        /// <summary>A survivor holding the 0.50 cal with it up: its laser shows to everyone.</summary>
        public static bool LaserHolder(SimPlayer p) =>
            p.Role == Role.Survivor && p.CanAct && p.HideState == 0 && p.Selected != null && p.Selected.item == ItemType.Sniper;
    }
}
