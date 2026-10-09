using UnityEngine;
using Vision.Player;

namespace Vision.Game
{
    /// <summary>
    /// Jaden Nguyen (the original's jaden.ts): wanders and is alerted exactly like Shane Jeans, but he has a pistol. Once
    /// alerted he hangs back a few metres and shoots the survivor who set him off, until they get out of range or have lost
    /// half the health they had when he started. Any survivor item stuns him for a moment; three kill him and he drops his
    /// pistol. Zach's machete shoves and stuns him; six points of it kill him, and Zach gets a lunge charge and longer reach
    /// for good. Anyone who attacks him (or gasses him with Penjamin) sets him on them.
    /// </summary>
    public sealed class Jaden : Shane
    {
        public static readonly Config JadenConfig = new Config
        {
            Radius = Balance.Jaden.Radius, Walk = Balance.Jaden.Walk, Chase = Balance.Jaden.Chase, AlertRadius = Balance.Jaden.AlertRadius,
            ProxAlertSec = Balance.Jaden.ProxAlertSec, FlashAlertSec = Balance.Jaden.FlashAlertSec, AlertDecay = Balance.Jaden.AlertDecay,
            ChaseTime = Balance.Jaden.ChaseTime, HunterBreakRadius = Balance.Jaden.HunterBreakRadius, LoseRadius = Balance.Jaden.LoseRadius,
            FleeTime = Balance.Jaden.FleeTime, Cooldown = Balance.Jaden.Cooldown, BottlesToShake = Balance.Jaden.BottlesToShake,
        };

        float fireCd, shotAge = 9f, dealt, startHp = 1f;
        int hits, zachHits;

        public override string Name => "Jaden Nguyen";
        public override int Kind => 5;

        public Jaden(MatchSim sim) : base(sim, JadenConfig, 12) { }

        public override float AlertLevel => alive ? base.AlertLevel : 0f;
        public override bool Slashable => alive;
        /// <summary>Slain, he lies where he fell.</summary>
        public override bool Gone => false;
        public override bool Aggressive => alive && State == Mode.Chase;
        public override bool TripsTraps => alive && State == Mode.Chase;
        public override NpcFlags Flags => base.Flags | (shotAge < 0.15f ? NpcFlags.Firing : 0) | (State == Mode.Chase ? NpcFlags.Armed : 0);

        /// <summary>Anything thrown or fired at him stuns him and sets him on whoever did it; three survivor hits kill him.</summary>
        public override void ItemHit(MatchSim sim, SimPlayer by, string kind)
        {
            if (!alive) return;
            StunT = Mathf.Max(StunT, Balance.Jaden.Stun);
            Moving = false;
            if (by.Role == Role.Survivor)
            {
                hits++;
                if (hits >= Balance.Jaden.Hp) { Die(by); return; }
            }
            ProvokeBy(by);
        }

        /// <summary>Zach's machete (power 1 light, 2 heavy): a flinch, a shove, a short stun; six points kill him.</summary>
        public override void Slash(MatchSim sim, SimPlayer h, int power, bool harm = true)
        {
            if (!alive) return;
            if (!harm) { ProvokeBy(h); return; }
            zachHits += power;
            HurtT = 0.3f;
            StunT = Mathf.Max(StunT, Balance.Jaden.MeleeStun);
            Moving = false;
            if (zachHits >= Balance.Jaden.ZachHp) { Die(h); return; }
            float a = Mathf.Atan2(Pos.y - h.Pos.y, Pos.x - h.Pos.x);
            Pos = Sim.MoveCircle(Pos, HitRadius, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Scale.D(Balance.Jaden.Kb));
            ProvokeBy(h);
        }

        public override void Provoke(MatchSim sim, SimPlayer by) => ProvokeBy(by);

        public override void Avenge(MatchSim sim, SimPlayer p)
        {
            if (!alive || !Targetable(p)) return;
            Alert(p);
            Avenging = true;
        }

        /// <summary>A 0.50 cal round: one shot slays him.</summary>
        public override void Snipe(MatchSim sim, SimPlayer shooter)
        {
            if (alive) Die(shooter);
        }

        public override void Gassed(MatchSim sim)
        {
            if (alive) StunT = Mathf.Max(StunT, Balance.Jaden.Stun);
        }

        void ProvokeBy(SimPlayer by)
        {
            if (!alive || (State == Mode.Chase && Target == by.Id)) return;
            if (!Targetable(by)) return;
            Alert(by);
        }

        static bool Targetable(SimPlayer p)
        {
            if (p.Role == Role.Hunter) return p.Health != Health.Eliminated && p.KnockT <= 0f;
            return p.Role == Role.Survivor && (p.Health == Health.Healthy || p.Health == Health.Wounded) && p.HideState == 0;
        }

        void Die(SimPlayer by)
        {
            alive = false;
            Moving = false;
            HurtT = 0f;
            if (State == Mode.Chase) EndChase(Mode.Walk);
            Meter.Clear();
            // Zach can't use a pistol: when he slays Jaden nothing drops.
            if (by.Role != Role.Hunter)
            {
                var drop = new DropItem { Id = Sim.AllocEntityId(), Pos = Pos, Item = ItemType.Pistol, Amount = Balance.Items.Pistol.Shots };
                Sim.PlaceDrop(drop, Pos + new Vector2(Mathf.Cos(Facing), Mathf.Sin(Facing)) * Scale.D(22f));
            }
            Sim.Feed($"{by.Name} took Jaden Nguyen down{(by.Role == Role.Hunter ? "" : ". His pistol is on the ground")}");
            if (by.Role == Role.Hunter && by.JadenBonus == 0)
            {
                // Slaying him makes Zach better for good: one more lunge charge and a longer reach.
                by.JadenBonus = 1;
                by.Move.LungeCharges += Balance.Hunter.JadenSlainLunge;
                Sim.Tell(by, "Slew Jaden: +1 lunge charge, +20% melee reach");
            }
        }

        protected override void Alert(SimPlayer p)
        {
            base.Alert(p);
            dealt = 0f;
            startHp = p.Hp;
            fireCd = 0.6f;
        }

        protected override void Announce(bool alerted)
        {
            if (!alerted) return;
            Sim.Feed("Jaden Nguyen has been alerted");
            Speak("Back up!");
        }

        /// <summary>Only someone on their feet sets him off (he won't shoot the downed); Zach only once he's the target.</summary>
        protected override bool Eligible(SimPlayer p)
        {
            if (p.Role == Role.Hunter) return p.Id == Target && Targetable(p);
            return Targetable(p);
        }

        public override void Update(MatchSim sim, float dt)
        {
            if (!alive) return;
            fireCd = Mathf.Max(0f, fireCd - dt);
            shotAge += dt;
            if (StunT > 0f)
            {
                Tick(dt);
                Moving = false;
                Gait = 0;
                return;
            }
            base.Update(sim, dt);
        }

        /// <summary>Closes to a few metres, then stands and shoots while he has a clear line.</summary>
        protected override float ChaseStep(SimPlayer t, float d, float dt)
        {
            bool clear = Sim.Geo.LineOfSight(Pos, t.Pos);
            Heading = Chaser.Heading(Pos, t.Pos, dt, StuckT >= 0.3f);
            if (clear) Face(t.Pos);
            else Facing = Heading;
            if (clear && d <= Scale.D(Balance.Jaden.Gun.Range) && fireCd <= 0f) Fire(t);
            if (State != Mode.Chase) return 0f;
            return clear && d <= Scale.D(Balance.Jaden.Gun.Keep) ? 0f : Balance.Jaden.Chase;
        }

        void Fire(SimPlayer t)
        {
            fireCd = Balance.Jaden.Gun.Cooldown;
            shotAge = 0f;
            float a = Facing + Range(-1f, 1f) * Balance.Jaden.Gun.SpreadDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 s = Pos + dir * (HitRadius + Scale.D(2f));
            float max = Scale.D(Balance.Jaden.Gun.Range * 1.5f);
            float best = Mathf.Min(max, Sim.Geo.CastSight(s, dir, max));
            SimPlayer hit = null;
            foreach (SimPlayer q in Sim.Order)
            {
                bool ok = q.Role == Role.Hunter ? q.Health != Health.Eliminated && q.KnockT <= 0f : q.Role == Role.Survivor && OnFeet(q) && q.HideState != 2;
                if (!ok) continue;
                float tt = MatchSim.RayCircle(s, dir, q.Pos, q.RadiusD);
                if (tt < best) { best = tt; hit = q; }
            }
            Sim.Emit(Sim.Near(Pos, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Shot, A = -Id, B = (int)ItemType.Pistol, Pos = Pos, F = a, G = best + HitRadius, Text = hit != null ? "hit" : "" });
            Sim.Noise(Pos, 900f, "shot");
            if (hit == null) return;
            float before = hit.Hp;
            if (hit.Role == Role.Hunter) Sim.HurtHunter(hit, Balance.Jaden.Gun.ZachDamage * Balance.Hunter.Health.Max, null, "bullet");
            else Sim.HurtSurvivor(hit, Balance.Jaden.Gun.Damage, null, "bullet");
            if (hit != t) return;
            dealt += before - hit.Hp;
            if ((!Avenging && dealt >= startHp * Balance.Jaden.Gun.StopAfter - 1e-6f) || t.Health == Health.Downed || t.KnockT > 0f)
            {
                Sim.Feed($"Jaden Nguyen let {t.Name} go");
                EndChase(Mode.Walk);
            }
        }
    }
}
