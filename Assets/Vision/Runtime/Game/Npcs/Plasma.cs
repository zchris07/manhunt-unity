using System.Collections.Generic;
using UnityEngine;
using Vision.Player;

namespace Vision.Game
{
    /// <summary>
    /// Plasma.TTV (the original's plasma.ts): a regular guy wandering about. Attack him and GAMER RAGE: over two seconds he
    /// turns into a hulking beast, then chases whoever hit him and punches until they're down (Zach goes down, without the
    /// lasting slowdown), then turns back and walks off. Losing him for ten seconds calms him, and ten seconds after he
    /// changes he turns back on his own. Items and the machete stun him; only the beast can be killed. Talk to him (either
    /// side) and he says "ggs" and hands you a golden pump, once each.
    /// </summary>
    public sealed class Plasma : Walker
    {
        public enum Mode { Idle, Walk, Transform, Rage, Revert }

        public Mode State = Mode.Idle;
        /// <summary>Who he's after (0 nobody).</summary>
        public int Target;
        /// <summary>Hits taken in beast form: from survivors' items, and from Zach (a heavy swing counts two).</summary>
        public int SurvivorHits, ZachHits;
        float escapeT, rageT, punchCd, punchAge = 9f;
        bool avenging;
        readonly HashSet<int> given = new HashSet<int>();
        readonly Chaser chaser;

        public override string Name => "Plasma.TTV";
        public override int Kind => 4;
        public override float RadiusUnits => State == Mode.Rage || State == Mode.Revert ? Balance.Plasma.BeastRadius : Balance.Plasma.Radius;
        public override bool Slashable => alive;
        /// <summary>Slain, he lies where he fell.</summary>
        public override bool Gone => false;
        public bool Raging => State == Mode.Transform || State == Mode.Rage;
        public bool Beast => State == Mode.Transform || State == Mode.Rage || State == Mode.Revert;
        /// <summary>How far through changing he is (0 man, 1 beast), for the view.</summary>
        public float BeastAmount => State == Mode.Rage ? 1f : State == Mode.Transform ? 1f - ModeT / Balance.Plasma.TransformTime : State == Mode.Revert ? Mathf.Clamp01(ModeT) : 0f;
        public override bool Aggressive => alive && Raging;
        public override bool TripsTraps => alive && Raging;
        public override NpcFlags Flags => base.Flags | (State == Mode.Rage ? NpcFlags.Raging | NpcFlags.Chasing : 0) | (State == Mode.Transform ? NpcFlags.Transforming : 0)
            | (Beast ? NpcFlags.Beast : 0) | (punchAge < 0.3f ? NpcFlags.Punching : 0);

        public Plasma(MatchSim sim) : base(sim, 104)
        {
            chaser = new Chaser(sim, Balance.Plasma.BeastRadius - 6f);
            SpawnAnywhere(Balance.Plasma.BeastRadius - Balance.Plasma.Radius + 6f, 700f);
        }

        bool CanTalk(SimPlayer p)
        {
            if (!alive || Beast || given.Contains(p.Id)) return false;
            if (p.Role == Role.Survivor && !OnFeet(p)) return false;
            if (p.Role == Role.Hunter && (p.Carrying != 0 || p.KnockT > 0f)) return false;
            if (p.Role == Role.Spectator) return false;
            return Near(p, Balance.Plasma.Reach);
        }

        public override Prompt TalkPrompt(MatchSim sim, SimPlayer p) => CanTalk(p) ? Prompt.TalkPlasma : Prompt.None;

        /// <summary>"ggs": a golden pump for whoever asks (once each).</summary>
        public override void Talk(MatchSim sim, SimPlayer p)
        {
            if (!CanTalk(p)) return;
            given.Add(p.Id);
            Face(p.Pos);
            State = Mode.Idle;
            ModeT = 1.5f;
            TalkT = 1.5f;
            Speak("ggs");
            if (p.Role == Role.Hunter)
            {
                p.Pump = Balance.Items.ZachPump.Shots;
                p.ChargeT = -1f;
            }
            else Sim.AddItem(p, ItemType.Shotgun, null, true);
            Sim.Tell(p, "Got golden pump");
        }

        public override void ItemHit(MatchSim sim, SimPlayer by, string kind) => Attacked(by, kind == "bottle" ? Balance.Plasma.BottleStun : Balance.Plasma.ShotStun, 1);

        public override void Slash(MatchSim sim, SimPlayer h, int power, bool harm = true)
        {
            if (!harm) { Attacked(h, 0f, 0); return; }
            h.Stats.Hits++;
            Attacked(h, Balance.Plasma.SlashStun, power);
        }

        public override void Provoke(MatchSim sim, SimPlayer by) => Attacked(by, 0f, 0);

        /// <summary>Chacko was slain by <paramref name="p"/>: GAMER RAGE at them, wherever they are, until they're downed.</summary>
        public override void Avenge(MatchSim sim, SimPlayer p)
        {
            if (!alive || !Huntable(p)) return;
            if (!Beast) Attacked(p, 0f, 0);
            Target = p.Id;
            rageT = 0f;
            escapeT = 0f;
            avenging = true;
        }

        public override void StopAvenging(MatchSim sim)
        {
            bool was = avenging;
            avenging = false;
            if (was && Raging) CalmDown();
        }

        /// <summary>A 0.50 cal round slays him in one shot, but only in beast form (otherwise it just sets him off).</summary>
        public override void Snipe(MatchSim sim, SimPlayer by)
        {
            if (!alive) return;
            if (Beast && State != Mode.Transform)
            {
                if (by.Role == Role.Hunter) ZachHits = Balance.Plasma.ZachHits;
                else SurvivorHits = Balance.Plasma.SurvivorHits;
                Die(by);
            }
            else Attacked(by, 0f, 0);
        }

        public override void Gassed(MatchSim sim) => GasT = 0.25f;

        void Attacked(SimPlayer by, float stun, int damage)
        {
            if (!alive) return;
            if (damage > 0) HurtT = 0.3f;
            if (Beast)
            {
                // Someone else attacking while he hunts for Chacko draws him off to them (and ends the vendetta).
                if (avenging && by.Id != Target && Huntable(by))
                {
                    avenging = false;
                    Target = by.Id;
                    rageT = 0f;
                    escapeT = 0f;
                }
                if (stun > 0f) StunT = Mathf.Max(StunT, stun);
                // Only the beast can be hurt (not while he's still changing).
                if (damage > 0 && State != Mode.Transform)
                {
                    if (by.Role == Role.Hunter) ZachHits = Mathf.Min(Balance.Plasma.ZachHits, ZachHits + damage);
                    else SurvivorHits = Mathf.Min(Balance.Plasma.SurvivorHits, SurvivorHits + damage);
                    if (ZachHits >= Balance.Plasma.ZachHits || SurvivorHits >= Balance.Plasma.SurvivorHits) Die(by);
                }
                return;
            }
            // GAMER RAGE.
            State = Mode.Transform;
            ModeT = Balance.Plasma.TransformTime;
            Target = by.Id;
            escapeT = 0f;
            rageT = 0f;
            Moving = false;
            Face(by.Pos);
            chaser.Reset();
            Speak("GAMER RAGE");
            Sim.Feed($"{by.Name} made Plasma.TTV rage");
        }

        /// <summary>Slain (beast form only): he drops his golden pump.</summary>
        void Die(SimPlayer by)
        {
            alive = false;
            State = Mode.Idle;
            Target = 0;
            Moving = false;
            StunT = 0f;
            var drop = new DropItem { Id = Sim.AllocEntityId(), Pos = Pos, Item = ItemType.Shotgun, Golden = true, Amount = Balance.Items.Golden.Shells };
            Sim.PlaceDrop(drop, Pos + new Vector2(Mathf.Cos(Facing), Mathf.Sin(Facing)) * Scale.D(26f));
            Sim.Feed($"{by.Name} slew Plasma.TTV. His golden pump is on the ground");
        }

        void CalmDown()
        {
            avenging = false;
            State = Mode.Revert;
            ModeT = 1f;
            Target = 0;
            Moving = false;
        }

        static bool Huntable(SimPlayer p)
        {
            if (p == null) return false;
            if (p.Role == Role.Hunter) return p.Health != Health.Eliminated && p.KnockT <= 0f;
            return OnFeet(p) && p.HideState == 0;
        }

        public override void Update(MatchSim sim, float dt)
        {
            if (!alive) return;
            Tick(dt);
            punchCd = Mathf.Max(0f, punchCd - dt);
            punchAge += dt;
            Gait = 0;
            if (StunT > 0f)
            {
                Moving = false;
                return;
            }
            float speed = 0f;
            if (Raging)
            {
                rageT += dt;
                if (rageT >= Balance.Plasma.RageTime && !avenging)
                {
                    CalmDown();
                    return;
                }
            }
            switch (State)
            {
                case Mode.Transform:
                    // In place, over two seconds.
                    ModeT -= dt;
                    if (ModeT <= 0f)
                    {
                        State = Mode.Rage;
                        Unstick(sim);
                    }
                    break;
                case Mode.Revert:
                    ModeT -= dt;
                    if (ModeT <= 0f)
                    {
                        State = Mode.Walk;
                        ModeT = Range(3f, 6f);
                        Heading = Facing + Mathf.PI * 0.8f;
                    }
                    break;
                case Mode.Rage:
                    speed = UpdateRage(dt);
                    break;
                case Mode.Idle:
                    ModeT -= dt;
                    if (TalkT <= 0f) Facing += Mathf.Sin(Sim.Time * 1.2f + Id) * dt * 0.7f;
                    if (ModeT <= 0f)
                    {
                        State = Mode.Walk;
                        ModeT = Range(3f, 8f);
                        Heading = Facing + Range(-1.5f, 1.5f);
                    }
                    break;
                default:
                    ModeT -= dt;
                    Heading += Range(-1f, 1f) * dt * 1.6f;
                    speed = Balance.Plasma.Walk;
                    if (ModeT <= 0f)
                    {
                        State = Mode.Idle;
                        ModeT = Range(1f, 3.5f);
                    }
                    break;
            }
            if (GasT > 0f) speed *= Balance.Plasma.GasSlowMul;
            Moving = speed > 0f;
            Gait = !Moving ? 0 : State == Mode.Rage ? 2 : 1;
            if (!Moving) return;
            Step(speed, dt);
            if (State != Mode.Rage || GasT > 0f) Facing = Heading;
            if (StuckT > 0f)
            {
                Vector2 ahead = Pos + new Vector2(Mathf.Cos(Heading), Mathf.Sin(Heading)) * Scale.D(34f);
                int di = Sim.NearbyDoor(ahead, 44f);
                if (di >= 0 && !Sim.Doors[di] && Sim.DoorCd[di] <= 0f) Sim.SetDoor(di, true);
                else if (StuckT > 0.25f && State != Mode.Rage)
                {
                    Heading += Mathf.PI * (0.5f + Next());
                    StuckT = 0f;
                }
            }
        }

        float UpdateRage(float dt)
        {
            SimPlayer t = Sim.Get(Target);
            if (!Huntable(t))
            {
                CalmDown();
                return 0f;
            }
            float d = Vector2.Distance(t.Pos, Pos);
            // Out of reach or out of sight counts toward escaping him; ten seconds and he gives up.
            bool lost = GasT > 0f || d > Scale.D(Balance.Plasma.LoseRadius) || !Sim.Geo.LineOfSight(Pos, t.Pos);
            escapeT = lost && !avenging ? escapeT + dt : 0f;
            if (escapeT >= Balance.Plasma.EscapeTime)
            {
                Sim.Feed($"{t.Name} got away from Plasma.TTV");
                CalmDown();
                return 0f;
            }
            if (GasT > 0f)
            {
                // Blinded: stumbling about.
                Heading += Range(-1f, 1f) * dt * 5f;
                return Balance.Plasma.Chase;
            }
            float reach = HitRadius + t.RadiusD + Scale.D(Balance.Plasma.PunchRange);
            if (d <= reach)
            {
                Face(t.Pos);
                if (punchCd <= 0f) Punch(t);
                return 0f;
            }
            Heading = chaser.Heading(Pos, t.Pos, dt, StuckT > 0.3f);
            Facing = Heading;
            return Balance.Plasma.Chase;
        }

        void Punch(SimPlayer t)
        {
            punchCd = Balance.Plasma.PunchCooldown;
            punchAge = 0f;
            Sim.Noise(Pos, 500f, "punch");
            if (t.Role == Role.Survivor)
            {
                Sim.HurtSurvivor(t, Balance.Plasma.PunchDamage, null, "punch");
                if (t.Health == Health.Downed)
                {
                    Sim.Feed($"Plasma.TTV beat {t.Name} down");
                    CalmDown();
                }
                return;
            }
            // Zach: punched until he goes down.
            Sim.HurtHunter(t, Balance.Plasma.ZachPunchDamage, null, "punch");
            if (t.KnockT > 0f) CalmDown();
        }
    }
}
