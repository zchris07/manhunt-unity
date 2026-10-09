using System.Collections.Generic;
using UnityEngine;
using Vision.Player;

namespace Vision.Game
{
    /// <summary>
    /// Chacko (the original's chacko.ts) sits on the lounge couch watching Madden. A survivor who talks to him gets a Doctor
    /// Pepper (once each); Zach gets 50 Nic (once). A single hit from anything kills him: if a survivor did it, Jaden Nguyen,
    /// Plasma.TTV and Shane Jeans hunt that survivor until they're downed once (or Jaden or Plasma is slain); if Zach did,
    /// Chacko explodes for half of Zach's health.
    /// </summary>
    public sealed class Chacko : Npc
    {
        readonly MatchSim sim;
        bool alive = true;
        float talkT;
        /// <summary>Who he already served (a survivor's can and Zach's 50 Nic are separate).</summary>
        readonly HashSet<string> served = new HashSet<string>();

        public override string Name => "Chacko";
        public override int Kind => 7;
        public override float RadiusUnits => Balance.Chacko.Radius;
        public override bool Alive => alive;
        public override bool Solid => alive;
        /// <summary>Slain, he stays slumped on the couch (exploded, he's gone).</summary>
        public override bool Gone => exploded;
        public override bool Slashable => alive;
        public override NpcFlags Flags => base.Flags | NpcFlags.Seated | (talkT > 0f ? NpcFlags.Talking : 0);
        bool exploded;

        public Chacko(MatchSim sim)
        {
            this.sim = sim;
            Id = sim.AllocEntityId();
            Pos = sim.Map.LoungeSeat;
            // Looking at the TV.
            Vector2 tv = sim.Map.LoungeTv - Pos;
            Facing = tv.sqrMagnitude > 1e-6f ? Mathf.Atan2(tv.y, tv.x) : Mathf.PI / 2f;
        }

        bool CanTalk(SimPlayer p)
        {
            if (!alive || served.Contains($"{p.Role}{p.Id}")) return false;
            if (p.Role == Role.Survivor) { if (p.Health != Health.Healthy && p.Health != Health.Wounded) return false; }
            else if (p.Role != Role.Hunter || !p.CanAct) return false;
            return Vector2.Distance(p.Pos, Pos) < Scale.D(Balance.Chacko.Reach);
        }

        public override Prompt TalkPrompt(MatchSim s, SimPlayer p) => CanTalk(p) ? Prompt.TalkChacko : Prompt.None;

        /// <summary>A survivor gets a Doctor Pepper; Zach gets 50 Nic, which replaces Penjamin.</summary>
        public override void Talk(MatchSim s, SimPlayer p)
        {
            if (!CanTalk(p)) return;
            served.Add($"{p.Role}{p.Id}");
            talkT = 2f;
            if (p.Role == Role.Hunter)
            {
                p.Nic = true;
                Speak(Balance.Chacko.LineZach);
                sim.Tell(p, "Got 50 Nic: it replaces Penjamin (+50% reach, blue)");
            }
            else
            {
                sim.AddItem(p, ItemType.DoctorPepper);
                Speak(Balance.Chacko.LineSurvivor);
                sim.Tell(p, "Got a Doctor Pepper");
            }
        }

        /// <summary>Any item (bottle, book, pellets, a bullet) kills him.</summary>
        public override void ItemHit(MatchSim s, SimPlayer by, string kind) => Slay(by);

        /// <summary>Zach's machete or lunge (harm false: only startled, by Penjamin).</summary>
        public override void Slash(MatchSim s, SimPlayer h, int power, bool harm = true)
        {
            if (!alive) return;
            if (!harm)
            {
                HurtT = 0.35f;
                Speak(Balance.Chacko.LineHit);
                return;
            }
            h.Stats.Hits++;
            Slay(h);
        }

        void Slay(SimPlayer by)
        {
            if (!alive) return;
            alive = false;
            HurtT = 0f;
            if (by.Role == Role.Hunter)
            {
                // A bloody explosion on the spot that takes half of Zach's health.
                exploded = true;
                sim.Emit(sim.Near(Pos, Balance.Net.MaxSensingRadius), new GameEvent { Kind = EventKind.Explosion, Pos = Pos });
                sim.Noise(Pos, 1200f, "smash");
                sim.Feed($"{by.Name} killed Chacko and he exploded");
                sim.HurtHunter(by, Balance.Hunter.Health.Max * Balance.Chacko.Explosion, null, "blast");
                return;
            }
            sim.Feed($"{by.Name} killed Chacko");
            sim.Avenge(by);
        }

        public override void Update(MatchSim s, float dt)
        {
            TickTimers(dt);
            talkT = Mathf.Max(0f, talkT - dt);
        }
    }
}
