using System.Collections.Generic;
using UnityEngine;
using Vision.Player;

namespace Vision.Game
{
    /// <summary>
    /// Marc Cortez (the original's marc.ts): starts in the warehouse and wanders, through doors, out into the woods and
    /// back. Talk to him and he hands you duck confit (once each). Nothing hurts him: Zach's machete gets a complaint, a
    /// survivor's item a flinch; he just stands there a moment.
    /// </summary>
    public sealed class Marc : Walker
    {
        float holdT;
        int complaints;
        readonly HashSet<int> confitGiven = new HashSet<int>();
        readonly Dictionary<int, float> lastTalk = new Dictionary<int, float>();

        public override string Name => "Marc Cortez";
        public override int Kind => 3;
        public override float RadiusUnits => Balance.Marc.Radius;
        public override bool Slashable => true;
        public override bool Alive => true;
        public override bool Solid => true;
        public override bool Gone => false;

        public Marc(MatchSim sim) : base(sim, 13)
        {
            SpawnIn(sim.Map.Building, 6f);
            Idle = true;
        }

        bool CanTalk(SimPlayer p)
        {
            if (p.Role != Role.Survivor || !OnFeet(p)) return false;
            if (lastTalk.TryGetValue(p.Id, out float t) && Sim.Time - t < Balance.Marc.TalkCooldown) return false;
            return Near(p, Balance.Marc.Reach);
        }

        public override Prompt TalkPrompt(MatchSim sim, SimPlayer p) => CanTalk(p) ? Prompt.TalkMarc : Prompt.None;

        public override void Talk(MatchSim sim, SimPlayer p)
        {
            if (!CanTalk(p)) return;
            lastTalk[p.Id] = Sim.Time;
            Face(p.Pos);
            holdT = 2f;
            TalkT = 2f;
            bool confit = confitGiven.Add(p.Id);
            if (confit)
            {
                Sim.AddItem(p, ItemType.Confit, 1, false);
                Sim.Tell(p, "Got duck confit");
            }
            Speak(confit ? "Here, take some duck confit. Trust me." : "That's all the confit I've got, man. Stay safe out there.");
        }

        /// <summary>Zach's machete (or anything of his): he protests and stands his ground.</summary>
        public override void Slash(MatchSim sim, SimPlayer h, int power, bool harm = true)
        {
            if (harm)
            {
                h.Stats.Hits++;
                HurtT = 0.35f;
            }
            holdT = 1.5f;
            Face(h.Pos);
            Speak(complaints++ % 2 == 0 ? "Hey man, what the heck?" : "Cut it out");
        }

        public override void ItemHit(MatchSim sim, SimPlayer by, string kind)
        {
            if (by.Role == Role.Hunter) { Slash(sim, by, 1); return; }
            HurtT = 0.35f;
            holdT = 1f;
        }

        public override void Update(MatchSim sim, float dt)
        {
            Tick(dt);
            if (holdT > 0f)
            {
                holdT -= dt;
                Moving = false;
                Gait = 0;
                return;
            }
            ModeT -= dt;
            float speed = 0f;
            if (Idle)
            {
                Facing += Mathf.Sin(Sim.Time * 1.1f + Id) * dt * 0.7f;
                if (ModeT <= 0f)
                {
                    Idle = false;
                    ModeT = Range(3f, 9f);
                    Heading = Facing + Range(-1.5f, 1.5f);
                }
            }
            else
            {
                Heading += Range(-1f, 1f) * dt * 1.4f;
                speed = Balance.Marc.Walk;
                if (ModeT <= 0f)
                {
                    Idle = true;
                    ModeT = Range(1f, 4f);
                }
            }
            Moving = speed > 0f;
            Gait = Moving ? 1 : 0;
            if (!Moving) return;
            Step(speed, dt);
            Facing = Heading;
            if (StuckT > 0f)
            {
                // He uses doors: opens the one in the way, else turns from the wall.
                Vector2 ahead = Pos + new Vector2(Mathf.Cos(Heading), Mathf.Sin(Heading)) * Scale.D(30f);
                int di = Sim.NearbyDoor(ahead, 40f);
                if (di >= 0 && !Sim.Doors[di] && Sim.DoorCd[di] <= 0f) Sim.SetDoor(di, true);
                else if (StuckT > 0.25f)
                {
                    Heading += Mathf.PI * (0.5f + Next());
                    StuckT = 0f;
                }
            }
        }
    }

    /// <summary>
    /// Waz (the original's waz.ts): talk to him as a survivor and he takes a looksie, and you see 10% more for good (once
    /// each). Zach slays him in three hits (he bolts after each); a survivor's item slays him at once, and that survivor
    /// sees 10% less; Zach who slays him sees 10% more. Either way the slayer gets a picture flashed across their screen.
    /// </summary>
    public sealed class Waz : Walker
    {
        int hp = Balance.Waz.Hp;
        bool fleeing;
        float turnT;
        SimPlayer fleeBy;

        public override string Name => "Waz";
        public override int Kind => 6;
        public override float RadiusUnits => Balance.Waz.Radius;
        public override bool Slashable => alive;
        /// <summary>Slain, he lies where he fell.</summary>
        public override bool Gone => false;
        public override NpcFlags Flags => base.Flags | (fleeing ? NpcFlags.Fleeing : 0);

        public Waz(MatchSim sim) : base(sim, 14) => SpawnAnywhere(4f, 600f);

        bool CanTalk(SimPlayer p) => alive && p.Role == Role.Survivor && !p.WazLooked && !fleeing && OnFeet(p) && Near(p, Balance.Waz.Reach);

        public override Prompt TalkPrompt(MatchSim sim, SimPlayer p) => CanTalk(p) ? Prompt.TalkWaz : Prompt.None;

        public override void Talk(MatchSim sim, SimPlayer p)
        {
            if (!CanTalk(p)) return;
            p.WazLooked = true;
            p.FovMul *= 1f + Balance.Waz.FovBonus;
            Face(p.Pos);
            TalkT = 2f;
            Idle = true;
            ModeT = 2f;
            Speak(Balance.Waz.Line);
            Sim.Tell(p, "Waz took a looksie: you see 10% more");
        }

        public override void Slash(MatchSim sim, SimPlayer h, int power, bool harm = true)
        {
            if (!alive) return;
            if (harm)
            {
                h.Stats.Hits++;
                HurtT = 0.35f;
                hp--;
            }
            if (hp <= 0) { Slay(h); return; }
            fleeing = true;
            ModeT = Balance.Waz.FleeTime;
            fleeBy = h;
            turnT = 0f;
        }

        public override void ItemHit(MatchSim sim, SimPlayer by, string kind)
        {
            if (!alive) return;
            if (by.Role == Role.Hunter) Slash(sim, by, 1);
            else Slay(by);
        }

        void Slay(SimPlayer by)
        {
            alive = false;
            Moving = false;
            by.FovMul *= by.Role == Role.Hunter ? 1f + Balance.Waz.FovBonus : 1f - Balance.Waz.FovPenalty;
            // Everyone in the lobby sees the flash.
            Sim.Emit(null, new GameEvent { Kind = EventKind.WazSlain, A = by.Id });
            Sim.Tell(by, by.Role == Role.Hunter ? "Slew Waz: you see 10% more" : "Slew Waz: you see 10% less");
            Sim.Feed($"{by.Name} slew Waz");
        }

        public override void Update(MatchSim sim, float dt)
        {
            if (!alive) return;
            Tick(dt);
            ModeT -= dt;
            float speed = 0f;
            if (fleeing)
            {
                // Erratic, mostly away from whoever hit him.
                turnT -= dt;
                if (turnT <= 0f)
                {
                    float away = fleeBy != null ? Mathf.Atan2(Pos.y - fleeBy.Pos.y, Pos.x - fleeBy.Pos.x) : Heading;
                    Heading = away + Range(-1.3f, 1.3f);
                    turnT = Range(0.2f, 0.45f);
                }
                speed = Balance.Waz.Flee;
                if (ModeT <= 0f)
                {
                    fleeing = false;
                    Idle = false;
                    ModeT = Range(2f, 5f);
                }
            }
            else if (Idle)
            {
                if (TalkT <= 0f) Facing += Mathf.Sin(Sim.Time * 1.2f + Id) * dt * 0.7f;
                if (ModeT <= 0f)
                {
                    Idle = false;
                    ModeT = Range(3f, 8f);
                    Heading = Facing + Range(-1.5f, 1.5f);
                }
            }
            else
            {
                Heading += Range(-1f, 1f) * dt * 1.6f;
                speed = Balance.Waz.Walk;
                if (ModeT <= 0f)
                {
                    Idle = true;
                    ModeT = Range(1f, 3.5f);
                }
            }
            if (GasT > 0f) speed *= 0.5f;
            Moving = speed > 0f;
            Gait = !Moving ? 0 : fleeing ? 2 : 1;
            if (!Moving) return;
            Step(speed, dt);
            Facing = Heading;
            if (StuckT > 0.25f)
            {
                Heading += Mathf.PI * (0.5f + Next());
                StuckT = 0f;
            }
        }
    }
}
