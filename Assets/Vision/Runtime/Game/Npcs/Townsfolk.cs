using System.Collections.Generic;
using UnityEngine;
using Vision.Player;

namespace Vision.Game
{
    /// <summary>
    /// Njaaron (the original's folk.ts): "you wanna go to the Y later?". Yes from Zach makes Zach's health come back faster
    /// for good; yes from a survivor and he comes with them and takes on Zach when Zach comes close or hurts them; no, and
    /// he fights whoever said it. Zach's hits make him fight back; four kill him, as do three stunning survivor items or
    /// one shot. However he dies, he explodes.
    /// </summary>
    public sealed class Njaaron : Walker
    {
        public enum Mode { Wander, Ask, Follow, Defend, Attack }

        public Mode State = Mode.Wander;
        int hp = Balance.Njaaron.Hp, stunHits, askWho, followId, targetId;
        /// <summary>Zach takes four points; survivors three stunning hits (or one shot).</summary>
        public override float? HealthFor(Role viewer) => !alive ? (float?)null
            : viewer == Role.Hunter ? hp / (float)Balance.Njaaron.Hp : 1f - stunHits / (float)Balance.Njaaron.StunHits;
        float askT, angryT, punchCd, punchAge = 9f, lastHp = 1f;

        public override string Name => "Njaaron";
        public override int Kind => 8;
        public override float RadiusUnits => Balance.Njaaron.Radius;
        public override bool Slashable => alive;
        public override bool Aggressive => alive && (State == Mode.Attack || State == Mode.Defend);
        public override NpcFlags Flags => base.Flags | (punchAge < 0.3f ? NpcFlags.Punching : 0) | (State == Mode.Follow ? NpcFlags.Following : 0)
            | (State == Mode.Attack || State == Mode.Defend ? NpcFlags.Angry : 0);

        public Njaaron(MatchSim sim) : base(sim, 201) => SpawnAnywhere(6f, 600f);

        bool CanTalk(SimPlayer p)
        {
            if (!alive || State != Mode.Wander || StunT > 0f) return false;
            return CanChat(p) && Near(p, Balance.Njaaron.Reach);
        }

        public override Prompt TalkPrompt(MatchSim sim, SimPlayer p) => CanTalk(p) ? Prompt.TalkNjaaron : Prompt.None;
        public override Prompt Awaiting(MatchSim sim, SimPlayer p) => alive && State == Mode.Ask && askWho == p.Id ? Prompt.NjaaronAsk : Prompt.None;

        public override void Talk(MatchSim sim, SimPlayer p)
        {
            if (!CanTalk(p)) return;
            State = Mode.Ask;
            askWho = p.Id;
            askT = Balance.Njaaron.AskSec;
            Face(p.Pos);
            Speak(Balance.Njaaron.LineAsk, Balance.Njaaron.AskSec);
        }

        /// <summary>Y or N from whoever he asked.</summary>
        public override void Answer(MatchSim sim, SimPlayer p, bool yes)
        {
            if (Awaiting(sim, p) == Prompt.None) return;
            askWho = 0;
            TalkT = 1.5f;
            if (yes)
            {
                Speak(Balance.Njaaron.LineYes);
                if (p.Role == Role.Hunter)
                {
                    p.NjaaronRegen = true;
                    Sim.Tell(p, $"Njaaron: health recovery +{Mathf.RoundToInt((Balance.Njaaron.RegenMul - 1f) * 100f)}%");
                    State = Mode.Wander;
                    Idle = false;
                    ModeT = 3f;
                }
                else
                {
                    State = Mode.Follow;
                    followId = p.Id;
                    lastHp = p.Hp;
                    Sim.Tell(p, "Njaaron is coming with you");
                }
            }
            else
            {
                Speak(Balance.Njaaron.LineNo);
                State = Mode.Attack;
                targetId = p.Id;
                angryT = Balance.Njaaron.AngrySec;
            }
        }

        public override void Slash(MatchSim sim, SimPlayer h, int power, bool harm = true)
        {
            if (!alive || !harm) return;
            h.Stats.Hits++;
            hp -= power;
            HurtT = 0.3f;
            StunT = Mathf.Max(StunT, 0.2f);
            if (hp <= 0) { Die(); return; }
            // Hit by Zach he fights back.
            State = Mode.Attack;
            targetId = h.Id;
            angryT = Balance.Njaaron.AngrySec;
            askWho = 0;
        }

        public override void ItemHit(MatchSim sim, SimPlayer by, string kind)
        {
            if (!alive) return;
            if (by.Role == Role.Hunter) { Slash(sim, by, 1); return; }
            // A firearm kills him; three stunning hits do.
            if (kind == "shot") { Die(); return; }
            stunHits++;
            HurtT = 0.3f;
            StunT = Balance.Njaaron.Stun;
            if (stunHits >= Balance.Njaaron.StunHits) Die();
        }

        void Die()
        {
            if (!alive) return;
            alive = false;
            Moving = false;
            Sim.Feed("Njaaron exploded");
            Sim.ExplodeAt(Pos);
        }

        void Punch(SimPlayer t)
        {
            punchCd = Balance.Njaaron.PunchCooldown;
            punchAge = 0f;
            if (t.Role == Role.Hunter) Sim.HurtHunter(t, Balance.Njaaron.PunchHp, null, "fist");
            else Sim.HurtSurvivor(t, Balance.Njaaron.SurvivorPunch, null, "fist");
            Sim.Shove(t, Pos, Balance.Njaaron.KbPeak, Balance.Njaaron.KbDuration);
        }

        /// <summary>Goes after <paramref name="t"/> and punches them when close.</summary>
        void Fight(SimPlayer t, float dt)
        {
            Face(t.Pos);
            float reach = Balance.Njaaron.Radius + t.Radius + Balance.Njaaron.PunchRange * 0.4f;
            bool close = WalkTo(t.Pos, Balance.Njaaron.Run, reach, dt);
            Gait = Moving ? 2 : 0;
            if (close && punchCd <= 0f) Punch(t);
        }

        SimPlayer ZachNear(SimPlayer p)
        {
            foreach (SimPlayer h in Sim.Order)
                if (h.Role == Role.Hunter && h.Health != Health.Eliminated && h.KnockT <= 0f && Vector2.Distance(h.Pos, p.Pos) < Scale.D(Balance.Njaaron.ZachClose)) return h;
            return null;
        }

        public override void Update(MatchSim sim, float dt)
        {
            if (!alive) return;
            Tick(dt);
            punchCd = Mathf.Max(0f, punchCd - dt);
            punchAge += dt;
            Moving = false;
            Gait = 0;
            if (StunT > 0f) return;
            switch (State)
            {
                case Mode.Ask:
                {
                    SimPlayer p = Sim.Get(askWho);
                    askT -= dt;
                    if (p != null) Face(p.Pos);
                    if (p == null || askT <= 0f || !Near(p, Balance.Njaaron.Reach * 2.5f))
                    {
                        State = Mode.Wander;
                        askWho = 0;
                    }
                    return;
                }
                case Mode.Follow:
                case Mode.Defend:
                {
                    SimPlayer f = Sim.Get(followId);
                    if (f == null || (f.Health != Health.Healthy && f.Health != Health.Wounded && f.Health != Health.Downed)) { State = Mode.Wander; return; }
                    SimPlayer zach = ZachNear(f);
                    bool attacked = f.Hp < lastHp - 0.001f;
                    lastHp = f.Hp;
                    if (State == Mode.Follow)
                    {
                        // Attacked, or Zach closing in: he takes on Zach.
                        if (zach != null || attacked)
                        {
                            SimPlayer z = zach;
                            if (z == null) foreach (SimPlayer h in Sim.Order) if (h.Role == Role.Hunter && h.Health != Health.Eliminated) { z = h; break; }
                            if (z != null)
                            {
                                State = Mode.Defend;
                                targetId = z.Id;
                                return;
                            }
                        }
                        WalkTo(f.Pos, Balance.Njaaron.Run, 90f, dt);
                        Gait = Moving ? 2 : 0;
                        return;
                    }
                    SimPlayer zz = Sim.Get(targetId);
                    if (zz == null || zz.Health == Health.Eliminated || Vector2.Distance(zz.Pos, f.Pos) > Scale.D(Balance.Njaaron.ZachLose)) { State = Mode.Follow; return; }
                    if (zz.KnockT > 0f)
                    {
                        // Zach is down: back to their side.
                        WalkTo(f.Pos, Balance.Njaaron.Run, 90f, dt);
                        Gait = Moving ? 2 : 0;
                        return;
                    }
                    Fight(zz, dt);
                    return;
                }
                case Mode.Attack:
                {
                    SimPlayer t = Sim.Get(targetId);
                    angryT -= dt;
                    bool ok = t != null && (t.Role == Role.Hunter ? t.Health != Health.Eliminated && t.KnockT <= 0f : OnFeet(t));
                    if (!ok || angryT <= 0f)
                    {
                        State = Mode.Wander;
                        Idle = true;
                        ModeT = 2f;
                        return;
                    }
                    Fight(t, dt);
                    return;
                }
                default:
                    Wander(Balance.Njaaron.Walk, dt);
                    Gait = Moving ? 1 : 0;
                    return;
            }
        }
    }

    /// <summary>Soham: says Hi, then his fuse burns for two seconds and he explodes. Nothing slays him; he just flinches.</summary>
    public sealed class Soham : Walker
    {
        float fuseT;
        bool lit;

        public override string Name => "Soham";
        public override int Kind => 11;
        public override float RadiusUnits => Balance.Soham.Radius;
        public override bool Slashable => alive;
        public override NpcFlags Flags => base.Flags | (lit ? NpcFlags.Fuse : 0);

        public Soham(MatchSim sim) : base(sim, 204) => SpawnAnywhere(6f, 600f);

        bool CanTalk(SimPlayer p) => alive && !lit && CanChat(p) && Near(p, Balance.Soham.Reach);
        public override Prompt TalkPrompt(MatchSim sim, SimPlayer p) => CanTalk(p) ? Prompt.TalkSoham : Prompt.None;

        public override void Talk(MatchSim sim, SimPlayer p)
        {
            if (!CanTalk(p)) return;
            lit = true;
            fuseT = Balance.Soham.Fuse;
            TalkT = Balance.Soham.Fuse + 0.5f;
            Face(p.Pos);
            Speak(Balance.Soham.Line);
        }

        public override void ItemHit(MatchSim sim, SimPlayer by, string kind)
        {
            if (alive) HurtT = 0.3f;
        }

        public override void Slash(MatchSim sim, SimPlayer h, int power, bool harm = true)
        {
            if (alive && harm) HurtT = 0.3f;
        }

        public override void Update(MatchSim sim, float dt)
        {
            if (!alive) return;
            Tick(dt);
            if (lit)
            {
                Moving = false;
                Gait = 0;
                fuseT -= dt;
                if (fuseT <= 0f)
                {
                    alive = false;
                    Sim.Feed("Soham exploded");
                    Sim.ExplodeAt(Pos);
                }
                return;
            }
            Wander(Balance.Soham.Walk, dt);
            Gait = Moving ? 1 : 0;
        }
    }

    /// <summary>Thomas Bourgeois: hands one player a fully charged Hemp Beam. Attacked by anyone, he runs.</summary>
    public sealed class Thomas : Walker
    {
        bool given;

        public override string Name => "Thomas Bourgeois";
        public override int Kind => 10;
        public override float RadiusUnits => Balance.Thomas.Radius;
        public override bool Slashable => alive;

        public Thomas(MatchSim sim) : base(sim, 203) => SpawnAnywhere(6f, 600f);

        bool CanTalk(SimPlayer p) => alive && FleeT <= 0f && CanChat(p) && Near(p, Balance.Thomas.Reach);
        public override Prompt TalkPrompt(MatchSim sim, SimPlayer p) => CanTalk(p) ? Prompt.TalkThomas : Prompt.None;

        public override void Talk(MatchSim sim, SimPlayer p)
        {
            if (!CanTalk(p)) return;
            Face(p.Pos);
            TalkT = 1.5f;
            if (given) { Speak(Balance.Thomas.LineAfter); return; }
            given = true;
            Speak(Balance.Thomas.Line);
            // A survivor gets the Hemp Beam as an item (fired with a left click); Zach, his own R beam topped up.
            if (p.Role == Role.Hunter)
            {
                p.BeamCharges = Balance.Hunter.Beam.Charges;
                Sim.Tell(p, $"Got a full Hemp Beam: R, {Balance.Hunter.Beam.Charges} charges");
                return;
            }
            Sim.AddItem(p, Vision.Player.ItemType.HempBeam);
            Sim.Tell(p, $"Got a full Hemp Beam: left click, {Balance.Hunter.Beam.Charges} charges");
        }

        public override void ItemHit(MatchSim sim, SimPlayer by, string kind)
        {
            if (!alive) return;
            HurtT = 0.3f;
            StartFlee(by.Pos, Balance.Thomas.FleeTime);
        }

        public override void Slash(MatchSim sim, SimPlayer h, int power, bool harm = true)
        {
            if (harm) ItemHit(sim, h, "slash");
        }

        public override void Update(MatchSim sim, float dt)
        {
            if (!alive) return;
            Tick(dt);
            Moving = false;
            if (FleeT > 0f) RunAway(Balance.Sexton.Flee * Balance.Thomas.FleeMul, dt);
            else Wander(Balance.Thomas.Walk, dt);
            Gait = !Moving ? 0 : FleeT > 0f ? 2 : 1;
        }
    }

    /// <summary>
    /// Monique Bourgeois: gives the first survivor who talks to her an arrow to Zach (40 s), later ones a Mr Beast bar.
    /// Attacked by anyone (Zach's machete, his Penjamin gas or Soundcloud Burst, a survivor's item or round), she pulls out
    /// a 0.50 cal and shoots at them for ten seconds (each new attack starts the ten seconds again, on whoever made it),
    /// reloading as a survivor's does; she holds fire while they're down or knocked out, but keeps her rifle up.
    /// </summary>
    public sealed class Monique : Walker
    {
        bool gaveArrow;
        readonly HashSet<int> fed = new HashSet<int>();
        float armedT, shotCd;
        int targetId;

        public override string Name => "Monique Bourgeois";
        public override int Kind => 9;
        public override float RadiusUnits => Balance.Monique.Radius;
        public override bool Slashable => alive;
        public bool Armed => armedT > 0f;
        public override NpcFlags Flags => base.Flags | (Armed ? NpcFlags.Armed : 0);

        public Monique(MatchSim sim) : base(sim, 202) => SpawnAnywhere(6f, 600f);

        bool CanTalk(SimPlayer p) => alive && p.Role == Role.Survivor && FleeT <= 0f && !Armed && !fed.Contains(p.Id) && OnFeet(p) && Near(p, Balance.Monique.Reach);
        public override Prompt TalkPrompt(MatchSim sim, SimPlayer p) => CanTalk(p) ? Prompt.TalkMonique : Prompt.None;

        public override void Talk(MatchSim sim, SimPlayer p)
        {
            if (!CanTalk(p)) return;
            fed.Add(p.Id);
            Face(p.Pos);
            TalkT = 1.5f;
            if (!gaveArrow)
            {
                gaveArrow = true;
                p.ArrowT = Balance.Monique.ArrowSec;
                Speak(Balance.Monique.LineArrow);
                Sim.Tell(p, $"Monique's arrow points to Zach for {Balance.Monique.ArrowSec:0} s");
            }
            else
            {
                Speak(Balance.Monique.LineHi);
                Sim.AddItem(p, ItemType.MrBeastBar, 1, false);
                Sim.Tell(p, "Got a Mr Beast bar");
            }
        }

        void Attacked(SimPlayer by)
        {
            if (!alive || by == null || (by.Role != Role.Hunter && by.Role != Role.Survivor)) return;
            HurtT = 0.3f;
            if (!Armed) shotCd = 0.4f;
            armedT = Balance.Monique.AttackSec;
            targetId = by.Id;
            FleeT = 0f;
        }

        public override void ItemHit(MatchSim sim, SimPlayer by, string kind) => Attacked(by);
        public override void Slash(MatchSim sim, SimPlayer h, int power, bool harm = true) => Attacked(h);
        public override void Provoke(MatchSim sim, SimPlayer by) => Attacked(by);
        public override void BurstHit(MatchSim sim, SimPlayer by) => Attacked(by);

        public override void Update(MatchSim sim, float dt)
        {
            if (!alive) return;
            Tick(dt);
            Moving = false;
            Gait = 0;
            if (Armed)
            {
                armedT = Mathf.Max(0f, armedT - dt);
                shotCd = Mathf.Max(0f, shotCd - dt);
                SimPlayer t = Sim.Get(targetId);
                if (t == null) return;
                // Rifle up on them the whole time; she fires whenever they can be hurt.
                Face(t.Pos);
                bool up = t.Role == Role.Hunter ? t.Health != Health.Eliminated && t.KnockT <= 0f : OnFeet(t) && t.HideState != 2;
                if (up && shotCd <= 0f)
                {
                    shotCd = Balance.Monique.Reload;
                    Vector2 muzzle = Pos + new Vector2(Mathf.Cos(Facing), Mathf.Sin(Facing)) * (HitRadius + Scale.D(4f));
                    Sim.SpawnBullet(muzzle, Facing, -Id, Balance.Monique.ShotDamage);
                }
                return;
            }
            if (FleeT > 0f) RunAway(Balance.Sexton.Flee * Balance.Monique.FleeMul, dt);
            else Wander(Balance.Monique.Walk, dt);
            Gait = !Moving ? 0 : FleeT > 0f ? 2 : 1;
        }
    }

    /// <summary>
    /// Daphne Cheng: hands a P250 to each survivor who talks to her (once each). Nobody can slay her. Attacked by anyone
    /// (a survivor, or Zach: his machete, Penjamin gas or Soundcloud Burst), she stands her ground and fires her pistol at
    /// them for ten seconds (each new attack starts them again), turning to keep them in her sights but never chasing;
    /// she fires whenever they're in her pistol's reach and in sight, and holds it up between; then she wanders again.
    /// </summary>
    public sealed class Daphne : Walker
    {
        readonly HashSet<int> given = new HashSet<int>();
        float armedT, fireCd;
        int targetId;

        public override string Name => "Daphne Cheng";
        public override int Kind => 12;
        public override float RadiusUnits => Balance.Monique.Radius;
        /// <summary>A swing reaches her (and sets her off), but she never falls.</summary>
        public override bool Slashable => alive;
        public bool Armed => armedT > 0f;
        public override NpcFlags Flags => base.Flags | (Armed ? NpcFlags.Armed : 0);

        public Daphne(MatchSim sim) : base(sim, 205) => SpawnAnywhere(6f, 600f);

        bool CanTalk(SimPlayer p) => alive && p.Role == Role.Survivor && !Armed && !given.Contains(p.Id) && OnFeet(p) && Near(p, Balance.Monique.Reach);
        public override Prompt TalkPrompt(MatchSim sim, SimPlayer p) => CanTalk(p) ? Prompt.TalkDaphne : Prompt.None;

        public override void Talk(MatchSim sim, SimPlayer p)
        {
            if (!CanTalk(p)) return;
            given.Add(p.Id);
            Face(p.Pos);
            TalkT = 1.5f;
            Speak("Here. Take this, you'll need it.");
            Sim.AddItem(p, ItemType.Pistol);
            Sim.Tell(p, "Got a P250");
        }

        void Attacked(SimPlayer by)
        {
            if (!alive || by == null || (by.Role != Role.Hunter && by.Role != Role.Survivor)) return;
            HurtT = 0.3f;
            if (!Armed) Speak("Back off!");
            armedT = Balance.Monique.AttackSec;
            targetId = by.Id;
            fireCd = Mathf.Max(fireCd, 0.4f);
        }

        public override void ItemHit(MatchSim sim, SimPlayer by, string kind) => Attacked(by);
        public override void Slash(MatchSim sim, SimPlayer h, int power, bool harm = true) => Attacked(h);
        public override void Provoke(MatchSim sim, SimPlayer by) => Attacked(by);
        public override void BurstHit(MatchSim sim, SimPlayer by) => Attacked(by);

        public override void Update(MatchSim sim, float dt)
        {
            if (!alive) return;
            Tick(dt);
            Moving = false;
            Gait = 0;
            if (Armed)
            {
                armedT = Mathf.Max(0f, armedT - dt);
                fireCd = Mathf.Max(0f, fireCd - dt);
                SimPlayer t = Sim.Get(targetId);
                if (t == null) return;
                // Where she stands: turning to keep them in her sights, firing while she has a clear line and they're in her
                // pistol's reach (and can be hurt).
                Face(t.Pos);
                bool up = t.Role == Role.Hunter ? t.Health != Health.Eliminated && t.KnockT <= 0f : OnFeet(t) && t.HideState != 2;
                if (up && fireCd <= 0f && Vector2.Distance(Pos, t.Pos) <= Scale.D(Balance.Jaden.Gun.Range * 1.5f) && Sim.Geo.LineOfSight(Pos, t.Pos)) Fire();
                return;
            }
            Wander(Balance.Monique.Walk, dt);
            Gait = Moving ? 1 : 0;
        }
        /// <summary>One P250 shot along where she faces (Jaden's pistol: its spread, reach and damage).</summary>
        void Fire()
        {
            fireCd = Balance.Jaden.Gun.Cooldown;
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
            Sim.BulletStruck(hit.Pos);
            if (hit.Role == Role.Hunter) Sim.HurtHunter(hit, Balance.Jaden.Gun.ZachDamage * Balance.Hunter.Health.Max, null, "bullet");
            else Sim.HurtSurvivor(hit, Balance.Jaden.Gun.Damage, null, "bullet");
        }
    }
}
