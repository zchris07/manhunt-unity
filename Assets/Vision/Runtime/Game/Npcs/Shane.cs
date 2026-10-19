using System.Collections.Generic;
using UnityEngine;

namespace Vision.Game
{
    /// <summary>
    /// Shane Jeans (the original's shane.ts): a harmless, unkillable wanderer with a faint light. Survivors who crowd him
    /// or keep a flashlight on him alert him, and he tails that survivor as closely as he can, a beacon for Zach. He
    /// ignores Zach, can't open doors or break pallets, and gives up after a while; two bottles or a shotgun blast in one
    /// chase shake him off, and galaxy gas makes him lose them. After anyone attacks Chacko he runs after them from wherever
    /// he is and only gives up once he's shaken off like that (Zach shakes him off with his machete or a Soundcloud Burst).
    /// </summary>
    public class Shane : Walker
    {
        /// <summary>What a Shane-like wanderer is tuned by (Shane's numbers, or Jaden's).</summary>
        public struct Config
        {
            public float Radius, Walk, Chase, AlertRadius, ProxAlertSec, FlashAlertSec, AlertDecay, ChaseTime, HunterBreakRadius, LoseRadius, FleeTime, Cooldown;
            public int BottlesToShake;
        }

        public static readonly Config ShaneConfig = new Config
        {
            Radius = Balance.Shane.Radius, Walk = Balance.Shane.Walk, Chase = Balance.Shane.Chase, AlertRadius = Balance.Shane.AlertRadius,
            ProxAlertSec = Balance.Shane.ProxAlertSec, FlashAlertSec = Balance.Shane.FlashAlertSec, AlertDecay = Balance.Shane.AlertDecay,
            ChaseTime = Balance.Shane.ChaseTime, HunterBreakRadius = Balance.Shane.HunterBreakRadius, LoseRadius = Balance.Shane.LoseRadius,
            FleeTime = Balance.Shane.FleeTime, Cooldown = Balance.Shane.Cooldown, BottlesToShake = Balance.Shane.BottlesToShake,
        };

        public enum Mode { Idle, Walk, Chase, Flee }

        protected readonly Config C;
        public Mode State = Mode.Idle;
        /// <summary>Who he's chasing (0: nobody).</summary>
        public int Target;
        /// <summary>The alert each survivor has built up on him (0-1; full alerts him).</summary>
        public readonly Dictionary<int, float> Meter = new Dictionary<int, float>();
        protected float ChaseT, CooldownT;
        protected int BottleHits;
        /// <summary>Chasing for Chacko: no giving up for time, distance or Zach coming near.</summary>
        protected bool Avenging;
        /// <summary>The survivor he's after for Chacko (0: none).</summary>
        protected int Grudge;
        protected readonly Chaser Chaser;

        public override string Name => "Shane Jeans";
        public override int Kind => 1;
        public override float RadiusUnits => C.Radius;

        public Shane(MatchSim sim) : this(sim, ShaneConfig, 11) { }

        protected Shane(MatchSim sim, Config cfg, int salt) : base(sim, salt)
        {
            C = cfg;
            // No door opening: closed doors stop him.
            // On a chase he climbs logs, fences and windows; Jaden knocks shut doors down too (his paths go through them).
            Chaser = new Chaser(sim, cfg.Radius, BreaksDoors, true);
            SpawnAnywhere(4f, 700f);
            State = Mode.Idle;
        }

        public bool Chasing => State == Mode.Chase;

        public override float AlertLevel
        {
            get
            {
                if (State == Mode.Chase) return 1f;
                float m = 0f;
                foreach (float v in Meter.Values) m = Mathf.Max(m, v);
                return Mathf.Min(1f, m);
            }
        }

        public override NpcFlags Flags => base.Flags | (State == Mode.Chase ? NpcFlags.Chasing : 0) | (State == Mode.Flee ? NpcFlags.Fleeing : 0);
        public override int GrudgeTarget => alive ? Grudge : 0;
        public override bool TripsTraps => State == Mode.Chase;
        /// <summary>Chasing Zach for Chacko, his machete can throw Shane off.</summary>
        public override bool Slashable => alive && State == Mode.Chase && ChasingZach;
        protected bool ChasingZach => Sim.Get(Target) is SimPlayer t && t.Role == Role.Hunter;

        /// <summary>Zach's machete (only while Shane's on him for Chacko): thrown off.</summary>
        public override void Slash(MatchSim sim, SimPlayer h, int power, bool harm = true)
        {
            if (alive && harm && State == Mode.Chase && ChasingZach) ShakeOff();
        }

        /// <summary>A Soundcloud Burst wave rolling over him while he's on Zach: stunned and driven off.</summary>
        public override void BurstHit(MatchSim sim, SimPlayer by)
        {
            if (alive && State == Mode.Chase && ChasingZach && by != null && by.Id == Target) ShakeOff();
        }

        public override void ItemHit(MatchSim sim, SimPlayer by, string kind)
        {
            if (kind == "bottle") BottleHit();
            else ShotHit();
        }

        /// <summary>Caught in galaxy gas: he loses the survivor and runs off.</summary>
        public override void Gassed(MatchSim sim)
        {
            base.Gassed(sim);
            if (State == Mode.Chase) ShakeOff();
        }

        /// <summary>A thrown bottle: two in one chase shake him off.</summary>
        public void BottleHit()
        {
            if (State != Mode.Chase) return;
            BottleHits++;
            if (BottleHits >= C.BottlesToShake) ShakeOff();
        }

        public void ShotHit()
        {
            if (State == Mode.Chase) ShakeOff();
        }

        protected void ShakeOff()
        {
            // Stunned and driven off: that ends a grudge too.
            Grudge = 0;
            SimPlayer t = Sim.Get(Target);
            FleeFrom = t != null ? t.Pos : Pos;
            EndChase(Mode.Flee);
            Sim.Feed($"{Name} ran off");
        }

        protected void EndChase(Mode next)
        {
            Target = 0;
            Avenging = false;
            CooldownT = C.Cooldown;
            Chaser.Reset();
            State = next;
            ModeT = next == Mode.Flee ? C.FleeTime : Range(2f, 5f);
        }

        /// <summary>
        /// <paramref name="p"/> attacked Chacko: he goes after them and keeps on (whatever the time, the distance or Zach coming
        /// near) until the grudge is over. While they're out of his reach (hidden, carried, staked) he loses them, and goes
        /// after them again the moment they're back out.
        /// </summary>
        public override void Avenge(MatchSim sim, SimPlayer p)
        {
            if (!alive || p == null) return;
            Grudge = p.Id;
            if (Eligible(p)) Pursue(p);
        }

        void Pursue(SimPlayer p)
        {
            Alert(p);
            Avenging = true;
        }

        /// <summary>Shane's grudge lasts until he's shaken off (<see cref="ShakeOff"/>), or the survivor has left the match.</summary>
        protected virtual bool GrudgeOver(SimPlayer t) => t == null || t.Health == Health.Escaped || t.Health == Health.Eliminated;

        /// <summary>Keeps after the survivor he holds a grudge against whenever they can be chased.</summary>
        protected void KeepGrudge()
        {
            if (Grudge == 0) return;
            SimPlayer t = Sim.Get(Grudge);
            if (GrudgeOver(t))
            {
                Grudge = 0;
                return;
            }
            if (State == Mode.Chase && Target == Grudge) Avenging = true;
            else if (Eligible(t)) Pursue(t);
        }

        protected virtual void Alert(SimPlayer p)
        {
            Avenging = false;
            State = Mode.Chase;
            Target = p.Id;
            ChaseT = C.ChaseTime;
            BottleHits = 0;
            Meter.Clear();
            Chaser.Reset();
            Announce(true);
        }

        protected virtual void Announce(bool alerted)
        {
            Sim.Emit(null, new GameEvent { Kind = EventKind.Talk, A = -Id, Text = alerted ? "shane_alerted" : "shane_calm", Pos = Pos });
            if (alerted) Sim.Feed("Shane Jeans has been alerted");
        }

        /// <summary>Knocks shut doors down on a chase (Jaden; Shane goes round).</summary>
        protected virtual bool BreaksDoors => false;

        /// <summary>One chase step toward <paramref name="t"/>: his speed (he stops right beside them).</summary>
        protected virtual float ChaseStep(SimPlayer t, float d, float dt)
        {
            Heading = Chaser.Heading(Pos, t.Pos, dt, StuckT >= 0.3f);
            Face(t.Pos);
            return d > HitRadius + t.RadiusD + Scale.D(6f) ? C.Chase : 0f;
        }

        /// <summary>Someone he'll follow: a survivor about (not hidden), or Zach himself when Chacko's attacker was Zach.</summary>
        protected virtual bool Eligible(SimPlayer p) =>
            (p.Role == Role.Survivor && !p.Ghost && (p.Health == Health.Healthy || p.Health == Health.Wounded || p.Health == Health.Downed) && p.HideState == 0)
            || (p.Role == Role.Hunter && p.Id == Grudge && p.Health != Health.Eliminated);

        /// <summary>Whether a flashlight kept on him alerts him too (Shane), or only standing close does (Jaden).</summary>
        protected virtual bool AlertedByBeam => true;

        /// <summary>
        /// Standing close to him (faster the closer) or keeping a flashlight on him builds a survivor's alert meter;
        /// otherwise it drains. Full, he's alerted.
        /// </summary>
        protected void Watch(float dt)
        {
            if (CooldownT > 0f || State == Mode.Chase || State == Mode.Flee)
            {
                Meter.Clear();
                return;
            }
            foreach (SimPlayer p in Sim.Order)
            {
                if (!Eligible(p) || p.Role != Role.Survivor) continue;
                float d = Vector2.Distance(p.Pos, Pos);
                float near = Scale.D(C.AlertRadius) + p.RadiusD;
                float rate = 0f;
                bool sight = Sim.Geo.LineOfSight(p.Pos, Pos);
                if (d <= near && sight) rate += (0.5f + 0.5f * (1f - d / near)) / C.ProxAlertSec;
                if (AlertedByBeam && sight && Sim.InCone(p, Pos, HitRadius)) rate += 1f / C.FlashAlertSec;
                Meter.TryGetValue(p.Id, out float before);
                float m = Mathf.Max(0f, before + (rate > 0f ? rate * dt : -C.AlertDecay * dt));
                if (m >= 1f)
                {
                    Alert(p);
                    return;
                }
                if (m > 0f) Meter[p.Id] = m;
                else Meter.Remove(p.Id);
            }
        }

        public override void Update(MatchSim sim, float dt)
        {
            if (!alive) return;
            Tick(dt);
            CooldownT = Mathf.Max(0f, CooldownT - dt);
            KeepGrudge();
            Watch(dt);
            float speed = 0f;
            if (State == Mode.Chase)
            {
                SimPlayer t = Sim.Get(Target);
                ChaseT -= dt;
                // Zach coming close ends a chase, unless Zach is the one being chased (Jaden, provoked).
                bool zachNear = false;
                if (t == null || t.Role != Role.Hunter)
                    foreach (SimPlayer h in Sim.Order)
                        if (h.Role == Role.Hunter && h.Health != Health.Eliminated && Vector2.Distance(h.Pos, Pos) < Scale.D(C.HunterBreakRadius)) zachNear = true;
                bool sticky = Avenging && t != null && t.Id == Target;
                if (t == null || !Eligible(t) || (!sticky && (ChaseT <= 0f || zachNear || Vector2.Distance(t.Pos, Pos) > Scale.D(C.LoseRadius))))
                {
                    EndChase(Mode.Walk);
                    Announce(false);
                }
                else speed = ChaseStep(t, Vector2.Distance(t.Pos, Pos), dt);
            }
            else if (State == Mode.Flee)
            {
                ModeT -= dt;
                Heading = Mathf.Atan2(Pos.y - FleeFrom.y, Pos.x - FleeFrom.x) + Mathf.Sin(Sim.Time * 5f) * 0.5f;
                speed = C.Chase;
                if (ModeT <= 0f)
                {
                    State = Mode.Walk;
                    ModeT = Range(2f, 5f);
                }
            }
            else
            {
                ModeT -= dt;
                if (State == Mode.Idle)
                {
                    Facing += Mathf.Sin(Sim.Time * 1.1f + Id) * dt * 0.7f;
                    if (ModeT <= 0f)
                    {
                        Heading = Facing + Range(-1.5f, 1.5f);
                        State = Mode.Walk;
                        ModeT = Range(3f, 8f);
                    }
                }
                else
                {
                    Heading += Range(-1f, 1f) * dt * 1.6f;
                    speed = C.Walk;
                    if (ModeT <= 0f)
                    {
                        State = Mode.Idle;
                        ModeT = Range(1f, 3.5f);
                    }
                }
            }
            Moving = speed > 0f;
            Gait = !Moving ? 0 : speed > C.Walk * 1.5f ? 2 : 1;
            if (!Moving) return;
            Climbs = State == Mode.Chase;
            Step(speed, dt);
            // Jaden at a shut door on a chase: three punches and it's down.
            if (State == Mode.Chase && BreaksDoors && StuckT > 0f && PunchDoorAhead(dt)) return;
            if (State != Mode.Chase) Facing = Heading;
            if (State != Mode.Chase && StuckT > 0.25f)
            {
                Heading += Mathf.PI * (0.5f + Next());
                StuckT = 0f;
            }
        }
    }
}
