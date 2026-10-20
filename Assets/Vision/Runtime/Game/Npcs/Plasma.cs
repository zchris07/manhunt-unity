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
    /// side) and he says "ggs" and hands you a golden pump: one to the survivors and one to Zach, no more; anyone after
    /// that is told "sorry, i'm all out bro". After anyone attacks Chacko he rages at them, from wherever he is, until he's
    /// dealt them a third of their health (or they're down, or he's slain).
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
        /// <summary>Raging for Chacko: no calming down for time or for losing them.</summary>
        bool avenging;
        /// <summary>The player he's after for Chacko (0: none), and how much of their health he's taken off them so far.</summary>
        int grudge;
        float grudgeDealt;
        /// <summary>Who has talked to him, and whether each side has had its one golden pump.</summary>
        readonly HashSet<int> given = new HashSet<int>();
        bool survivorPump, zachPump;
        public const string LineAllOut = "sorry, i'm all out bro";
        readonly Chaser chaser;

        public override string Name => "Plasma.TTV";
        /// <summary>Only the beast can be slain: six hits from either side.</summary>
        public override float? HealthFor(Role viewer) => !alive || !Beast ? (float?)null
            : viewer == Role.Hunter ? 1f - ZachHits / (float)Balance.Plasma.ZachHits : 1f - SurvivorHits / (float)Balance.Plasma.SurvivorHits;
        public override int Kind => 4;
        public override float RadiusUnits => State == Mode.Rage || State == Mode.Revert ? Balance.Plasma.BeastRadius : Balance.Plasma.Radius;
        /// <summary>The beast is as easy to hit as he is big, but squeezes through doorways like a man (his model keeps its size).</summary>
        public override float MoveRadiusUnits => Balance.Plasma.Radius;
        public override bool Slashable => alive;
        /// <summary>Slain, he lies where he fell.</summary>
        public override bool Gone => false;
        public bool Raging => State == Mode.Transform || State == Mode.Rage;
        public bool Beast => State == Mode.Transform || State == Mode.Rage || State == Mode.Revert;
        /// <summary>How far through changing he is (0 man, 1 beast), for the view.</summary>
        public float BeastAmount => State == Mode.Rage ? 1f : State == Mode.Transform ? 1f - ModeT / Balance.Plasma.TransformTime : State == Mode.Revert ? Mathf.Clamp01(ModeT) : 0f;
        public override bool Aggressive => alive && Raging;
        public override int GrudgeTarget => alive ? grudge : 0;
        public override bool TripsTraps => alive && Raging;
        public override NpcFlags Flags => base.Flags | (State == Mode.Rage ? NpcFlags.Raging | NpcFlags.Chasing : 0) | (State == Mode.Transform ? NpcFlags.Transforming : 0)
            | (Beast ? NpcFlags.Beast : 0) | (punchAge < 0.3f ? NpcFlags.Punching : 0);

        public Plasma(MatchSim sim) : base(sim, 104)
        {
            chaser = new Chaser(sim, Balance.Plasma.Radius, true);
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

        /// <summary>Whether he still has the golden pump for a player of this side.</summary>
        public bool HasPumpFor(Role role) => role == Role.Hunter ? !zachPump : !survivorPump;

        /// <summary>"ggs": a golden pump for the first of each side to ask; anyone after that, he's all out.</summary>
        public override void Talk(MatchSim sim, SimPlayer p)
        {
            if (!CanTalk(p)) return;
            given.Add(p.Id);
            Face(p.Pos);
            State = Mode.Idle;
            ModeT = 1.5f;
            TalkT = 1.5f;
            if (!HasPumpFor(p.Role))
            {
                Speak(LineAllOut);
                Sim.Tell(p, "Plasma.TTV is all out of golden pumps");
                return;
            }
            if (p.Role == Role.Hunter) zachPump = true;
            else survivorPump = true;
            Speak("ggs");
            if (p.Role == Role.Hunter)
            {
                p.Pump = MatchState.Current.Tuning.GoldenAmmo;
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

        /// <summary><paramref name="p"/> attacked Chacko: GAMER RAGE at them, wherever they are, until he's taken a third of their health.</summary>
        public override void Avenge(MatchSim sim, SimPlayer p)
        {
            if (!alive || p == null) return;
            grudge = p.Id;
            grudgeDealt = 0f;
            if (Huntable(p)) Pursue(p);
        }

        /// <summary>A grudge for Chacko is over once he's dealt the player a third of their health.</summary>
        public const float GrudgeShare = 1f / 3f;

        void Pursue(SimPlayer p)
        {
            if (!Beast) Attacked(p, 0f, 0);
            Target = p.Id;
            rageT = 0f;
            escapeT = 0f;
            avenging = true;
        }

        /// <summary>
        /// Keeps after Chacko's attacker until they're down.
        /// </summary>
        void KeepGrudge()
        {
            if (grudge == 0) return;
            SimPlayer g = Sim.Get(grudge);
            if (g == null || !Huntable(g) || grudgeDealt >= GrudgeShare - 1e-4f)
            {
                grudge = 0;
                avenging = false;
                return;
            }
            if (Raging && Target == grudge) avenging = true;
            else if (State != Mode.Transform && Huntable(g)) Pursue(g);
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
            Sim.Emit(Sim.Near(Pos, Balance.Net.HearingRadius), new GameEvent { Kind = EventKind.Talk, A = -Id, Text = "gamer_rage", Pos = Pos });
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
            var drop = new DropItem { Id = Sim.AllocEntityId(), Pos = Pos, Item = ItemType.Shotgun, Golden = true, Amount = Items.FreshAmount(ItemType.Shotgun, true) };
            Sim.PlaceDrop(drop, Pos + new Vector2(Mathf.Cos(Facing), Mathf.Sin(Facing)) * Scale.D(26f));
            Sim.Feed($"{by.Name} slew Plasma.TTV. His golden pump is on the ground");
        }

        /// <summary>He's taken his third out of Chacko's attacker: done with them.</summary>
        void EndGrudge(SimPlayer t)
        {
            grudge = 0;
            Sim.Feed($"Plasma.TTV is done with {t.Name}");
            CalmDown();
        }

        void CalmDown()
        {
            avenging = false;
            State = Mode.Revert;
            ModeT = 1f;
            Target = 0;
            Moving = false;
        }

        /// <summary>Someone he can go after: Zach up and about, or a survivor on their feet.</summary>
        static bool Huntable(SimPlayer p)
        {
            if (p == null) return false;
            if (p.Role == Role.Hunter) return p.Health != Health.Eliminated && p.KnockT <= 0f;
            return p.Role == Role.Survivor && !p.Ghost && OnFeet(p);
        }

        public override void Update(MatchSim sim, float dt)
        {
            if (!alive) return;
            Tick(dt);
            punchCd = Mathf.Max(0f, punchCd - dt);
            punchAge += dt;
            KeepGrudge();
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
            // In his rage (a chase) he knocks a shut door down with three punches.
            Step(speed, dt);
            if (State != Mode.Rage || GasT > 0f) Facing = Heading;
            if (StuckT > 0f && State == Mode.Rage && PunchDoorAhead(dt)) return;
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
            float before = t.Hp;
            if (t.Role == Role.Survivor)
            {
                Sim.HurtSurvivor(t, Balance.Plasma.PunchDamage, null, "punch");
                if (t.Id == grudge) grudgeDealt += Mathf.Max(0f, before - t.Hp);
                if (t.Health == Health.Downed)
                {
                    Sim.Feed($"Plasma.TTV beat {t.Name} down");
                    CalmDown();
                }
                else if (t.Id == grudge && grudgeDealt >= GrudgeShare - 1e-4f) EndGrudge(t);
                return;
            }
            // Zach: punched until he goes down.
            Sim.HurtHunter(t, Balance.Plasma.ZachPunchDamage, null, "punch");
            if (t.Id == grudge) grudgeDealt += Mathf.Max(0f, before - t.Hp);
            if (t.KnockT > 0f) CalmDown();
            else if (t.Id == grudge && grudgeDealt >= GrudgeShare - 1e-4f) EndGrudge(t);
        }
    }
}
