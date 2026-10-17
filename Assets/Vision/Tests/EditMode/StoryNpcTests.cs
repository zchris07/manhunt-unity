using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Player;
using Plan = Vision.World.BuildingPlan;

namespace Vision.Tests
{
    /// <summary>
    /// The story NPCs against the original's rules (sexton.ts, chris.ts and chris.test.ts, plasma.ts, chacko.ts): Sexton's
    /// two-part talk and JARVIS, his Hemp Beam defence and the beam he drops; Chris pacing, enlisted, reviving and
    /// ascending; Plasma's GAMER RAGE, golden pump and beast-only death; Chacko's gifts, explosion and vengeance; and the
    /// lounge every building has.
    /// </summary>
    public class StoryNpcTests
    {
        MatchState saved;

        [SetUp]
        public void SaveState() => saved = MatchState.Current;

        [TearDown]
        public void RestoreState() => MatchState.Current = saved;

        static T Add<T>(SimRig rig, T n, float x, float y) where T : Npc
        {
            n.Pos = SimRig.U(x, y);
            rig.Sim.Npcs.Add(n);
            return n;
        }

        [Test]
        public void SextonTalksTwiceThenHandsOverJarvisAndWalksOff()
        {
            var rig = new SimRig();
            SimPlayer s = rig.P(2);
            var sexton = Add(rig, new Sexton(rig.Sim), 0, 0);
            rig.Place(s, 50, 0);
            rig.Run(1);
            Assert.AreEqual(Prompt.TalkSexton, s.Prompt);
            rig.Tap(s.Id, Btn.Interact);
            Assert.AreEqual(ActionKind.Talk, s.Action, "listening");
            Assert.AreEqual(Sexton.Mode.Talk, sexton.State);
            rig.Run(SimRig.Secs(Balance.Sexton.TalkTime + 0.2f));
            Assert.AreEqual(ActionKind.None, s.Action);
            Assert.AreEqual(Prompt.SextonMore, s.Prompt, "he waits for E again");
            rig.Tap(s.Id, Btn.Interact);
            Assert.AreEqual(Sexton.TalkStage.Second, sexton.Stage);
            StringAssert.Contains(s.Name, sexton.Say, "the second line names you");
            rig.Run(SimRig.Secs(Balance.Sexton.SecondTalkTime + Balance.Sexton.HandTime + 0.2f));
            Assert.AreEqual(1, s.Jarvis, "JARVIS");
            Assert.AreEqual(Sexton.Mode.Leave, sexton.State, "then he walks off");
            rig.Run(1);
            Assert.AreNotEqual(Prompt.TalkSexton, s.Prompt, "once each");
        }

        [Test]
        public void SextonDefendsHimselfWithHempBeams()
        {
            var rig = new SimRig();
            SimPlayer s = rig.P(2);
            var sexton = Add(rig, new Sexton(rig.Sim), 0, 0);
            rig.Place(s, 250, 0);
            sexton.ItemHit(rig.Sim, s, "bottle");
            Assert.IsTrue(sexton.Defending);
            Assert.AreEqual(1f, s.Hp, "a survivor's item can't hurt him, and he doesn't hurt back at once");
            bool beamed = false;
            for (int t = 0; t < SimRig.Secs(6f); t++)
            {
                rig.Run(1);
                beamed |= sexton.Beaming;
            }
            Assert.IsTrue(beamed, "a Hemp Beam");
            Assert.AreEqual(1f - Balance.Sexton.Defense.BeamDamage, s.Hp, 1e-3f, "one hit per beam");
            sexton.ItemHit(rig.Sim, s, "bottle");
            Assert.Greater(sexton.StunT, 0f, "hit again while defending, he's stunned");
            // Left alone long enough, he calms down.
            rig.Place(s, 2400, 2400);
            rig.Run(SimRig.Secs(Balance.Sexton.Defense.ResetAfter + 1f));
            Assert.IsFalse(sexton.Defending);
        }

        [Test]
        public void ZachSlaysSextonInThreeHitsAndTakesHisHempBeam()
        {
            var rig = new SimRig();
            SimPlayer h = rig.P(1);
            rig.Place(rig.P(2), -2000, -2000);
            var sexton = Add(rig, new Sexton(rig.Sim), 0, 0);
            sexton.Slash(rig.Sim, h, 2);
            Assert.IsTrue(sexton.Alive);
            Assert.IsTrue((sexton.Flags & NpcFlags.Fleeing) != 0, "he runs");
            sexton.Slash(rig.Sim, h, 2);
            sexton.Slash(rig.Sim, h, 2);
            Assert.IsFalse(sexton.Alive);
            DropItem beam = rig.Sim.Drops.Find(d => d.Item == ItemType.HempBeam);
            Assert.IsNotNull(beam, "his Hemp Beam on the ground");
            rig.Sim.Teleport(h.Id, beam.Pos + SimRig.U(10, 0));
            rig.Run(1);
            Assert.AreEqual(Prompt.PickDrop, h.Prompt, "Zach can take it");
            rig.Tap(h.Id, Btn.Interact);
            Assert.AreEqual(Balance.Hunter.Beam.Charges, h.BeamCharges, "its charges into his own beam (R)");
            Assert.IsFalse(rig.Sim.Drops.Contains(beam));
        }

        [Test]
        public void SurvivorsSlaySextonInThreeHits_AndTakeHisHempBeam()
        {
            var rig = new SimRig();
            SimPlayer s = rig.P(2);
            rig.Place(rig.P(1), -2000, -2000);
            var sexton = Add(rig, new Sexton(rig.Sim), 0, 0);
            rig.Place(s, 250, 0);
            Assert.AreEqual(1f, sexton.HealthFor(Role.Survivor), "survivors see his health: they can slay him");
            sexton.ItemHit(rig.Sim, s, "bottle");
            Assert.IsTrue(sexton.Alive);
            Assert.IsTrue(sexton.Defending, "he fights back");
            Assert.AreEqual(2f / 3f, sexton.HealthFor(Role.Survivor).Value, 1e-4f);
            sexton.ItemHit(rig.Sim, s, "shot");
            sexton.ItemHit(rig.Sim, s, "shot");
            Assert.IsFalse(sexton.Alive, "three hits");
            Assert.IsFalse(sexton.Beaming);
            DropItem beam = rig.Sim.Drops.Find(d => d.Item == ItemType.HempBeam);
            Assert.IsNotNull(beam, "his Hemp Beam on the ground");
            rig.Sim.Teleport(s.Id, beam.Pos + SimRig.U(10, 0));
            rig.Run(1);
            Assert.AreEqual(Prompt.PickDrop, s.Prompt);
            rig.Tap(s.Id, Btn.Interact);
            Assert.GreaterOrEqual(s.Inv.FirstSlotOf(ItemType.HempBeam), 0, "the survivor has the Hemp Beam, as an item");
            Assert.IsFalse(rig.Sim.Drops.Contains(beam));
        }

        [Test]
        public void ChrisPacesHisAmbulanceThenRevivesTheDownedAndAscends()
        {
            var rig = new SimRig(2, 1);
            SimPlayer s = rig.P(2), s2 = rig.P(3);
            rig.Place(s, 2000, 2000);
            rig.Place(s2, -2000, 2000);
            var chris = new Chris(rig.Sim);
            rig.Sim.Npcs.Add(chris);
            float far = (rig.Sim.Map.AmbulanceSize * 0.5f + Vector2.one * Scale.D(Balance.Chris.Pace)).magnitude * 1.05f;
            for (int t = 0; t < SimRig.Secs(12f); t++)
            {
                rig.Run(1);
                Assert.LessOrEqual(Vector2.Distance(chris.Pos, rig.Sim.Map.AmbulanceAt), far, "he never strays from the ambulance");
            }
            // Enlisted by a survivor.
            rig.Sim.Teleport(s.Id, chris.Pos + SimRig.U(40, 0));
            rig.Run(1);
            Assert.AreEqual(Prompt.TalkChris, s.Prompt);
            rig.Tap(s.Id, Btn.Interact);
            Assert.IsTrue(chris.Active);
            // A survivor down for long enough: he runs to them, revives them, and goes.
            rig.Sim.Teleport(s2.Id, chris.Pos + SimRig.U(-400, 300));
            rig.Sim.HurtSurvivor(s2, 1f, null, "test");
            Assert.AreEqual(Health.Downed, s2.Health);
            rig.Run(SimRig.Secs(Balance.Chris.DownedAfter + 0.5f));
            Assert.AreEqual(Chris.Mode.Rescue, chris.State);
            rig.Run(SimRig.Secs(4f + Balance.Survivor.ReviveTime));
            Assert.AreEqual(Health.Wounded, s2.Health, "back on their feet");
            Assert.AreEqual(Balance.Survivor.ReviveHp, s2.Hp, 1e-3f);
            Assert.IsTrue(chris.State == Chris.Mode.Ascend || chris.State == Chris.Mode.Gone);
            rig.Run(SimRig.Secs(Balance.Chris.AscendTime + 0.2f));
            Assert.IsTrue(chris.Gone, "flown to the heavens");
            Assert.IsFalse(chris.Solid);
        }

        [Test]
        public void Chris_IsUnkillable_ExceptByZachWhileHesAtWork()
        {
            var rig = new SimRig();
            var chris = new Chris(rig.Sim);
            rig.Sim.Npcs.Add(chris);
            SimPlayer zach = rig.P(1);
            zach.SwingDamage = Balance.Hunter.Attack.DamageFull;
            for (int i = 0; i < 6; i++) chris.Slash(rig.Sim, zach, 2);
            Assert.IsTrue(chris.Alive, "pacing round his ambulance, Zach can't kill him");
            Assert.AreEqual(Chris.Mode.Flee, chris.State, "only scared off");
            Assert.IsNull(chris.HealthFor(Role.Hunter), "no health bar while he can't be hurt");
            chris.ItemHit(rig.Sim, rig.P(2), "bottle");
            for (int i = 0; i < 5; i++) chris.ItemHit(rig.Sim, rig.P(2), "shot");
            Assert.IsTrue(chris.Alive, "a survivor's item only makes him flinch");
            Assert.AreEqual(Balance.Chris.Health, chris.HealthLeft, 1e-4f);
            Assert.AreEqual(50f, Balance.Chris.Health);
            // At work on someone: a full swing takes two thirds of 100, more than his 50.
            chris.State = Chris.Mode.Work;
            Assert.IsTrue(chris.Vulnerable);
            Assert.AreEqual(1f, chris.HealthFor(Role.Hunter) ?? -1f, 1e-4f, "his bar shows while he's at work");
            chris.ItemHit(rig.Sim, rig.P(2), "shot");
            Assert.AreEqual(Balance.Chris.Health, chris.HealthLeft, 1e-4f, "survivors can't hurt him even then");
            zach.SwingDamage = 0.1f;
            chris.Slash(rig.Sim, zach, 1);
            Assert.AreEqual(Balance.Chris.Health - 10f, chris.HealthLeft, 1e-3f, "a light swing takes 10");
            zach.SwingDamage = Balance.Hunter.Attack.DamageFull;
            chris.Slash(rig.Sim, zach, 2);
            Assert.IsFalse(chris.Alive, "and a full one finishes him");
        }

        [Test]
        public void PlasmaRagesPunchesTheSurvivorDownAndCalms()
        {
            // Two survivors, so the match goes on once one is down.
            var rig = new SimRig(2, 1);
            SimPlayer s = rig.P(2);
            rig.Place(s, 0, 0);
            rig.Place(rig.P(3), -2400, 2400);
            var plasma = Add(rig, new Plasma(rig.Sim), 70, 0);
            plasma.ItemHit(rig.Sim, s, "bottle");
            Assert.AreEqual(Plasma.Mode.Transform, plasma.State, "GAMER RAGE");
            Assert.AreEqual("GAMER RAGE", plasma.Say);
            rig.Run(SimRig.Secs(Balance.Plasma.TransformTime + 0.1f));
            Assert.AreEqual(Plasma.Mode.Rage, plasma.State);
            Assert.IsTrue(plasma.Beast);
            Assert.AreEqual(Balance.Plasma.BeastRadius, plasma.RadiusUnits);
            rig.Run(SimRig.Secs(5f));
            Assert.AreEqual(Health.Downed, s.Health, "punched until they're down");
            Assert.IsFalse(plasma.Raging, $"then he turns back ({plasma.State})");
            rig.Run(SimRig.Secs(1.2f));
            Assert.IsFalse(plasma.Beast, $"a man again ({plasma.State}, stun {plasma.StunT}, target {plasma.Target}, survivor {s.Health})");
        }

        [Test]
        public void PlasmaHandsOutGoldenPumpsAndOnlyTheBeastCanDie()
        {
            var rig = new SimRig();
            SimPlayer s = rig.P(2), h = rig.P(1);
            var plasma = Add(rig, new Plasma(rig.Sim), 0, 0);
            rig.Place(s, 50, 0);
            rig.Run(1);
            Assert.AreEqual(Prompt.TalkPlasma, s.Prompt);
            rig.Tap(s.Id, Btn.Interact);
            Assert.AreEqual(1, s.Inv.Count(ItemType.Shotgun));
            Assert.AreEqual("ggs", plasma.Say);
            rig.Place(s, -2000, -2000);
            rig.Place(h, -50, 0);
            rig.Run(1);
            Assert.AreEqual(Prompt.TalkPlasma, h.Prompt);
            rig.Tap(h.Id, Btn.Interact);
            Assert.AreEqual(Balance.Items.ZachPump.Shots, h.Pump, "Zach's golden pump");

            // A 0.50 cal round only sets the man off; the beast it slays, and his pump drops.
            plasma.Snipe(rig.Sim, s);
            Assert.IsTrue(plasma.Alive);
            Assert.AreEqual(Plasma.Mode.Transform, plasma.State);
            rig.Run(SimRig.Secs(Balance.Plasma.TransformTime + 0.1f));
            plasma.Snipe(rig.Sim, s);
            Assert.IsFalse(plasma.Alive);
            Assert.IsTrue(rig.Sim.Drops.Exists(d => d.Item == ItemType.Shotgun && d.Golden));
        }

        [Test]
        public void ChackoServesEachOnce_FlinchesAtASurvivorsHit_AndTheyAreHunted()
        {
            var rig = new SimRig();
            SimPlayer s = rig.P(2), h = rig.P(1);
            var chacko = new Chacko(rig.Sim);
            rig.Sim.Npcs.Add(chacko);
            var jaden = Add(rig, new Jaden(rig.Sim), 600, 0);
            var plasma = Add(rig, new Plasma(rig.Sim), -600, 0);
            rig.Sim.Teleport(s.Id, chacko.Pos + SimRig.U(50, 0));
            rig.Run(1);
            Assert.AreEqual(Prompt.TalkChacko, s.Prompt);
            rig.Tap(s.Id, Btn.Interact);
            Assert.AreEqual(1, s.Inv.Count(ItemType.DoctorPepper));
            rig.Run(1);
            Assert.AreNotEqual(Prompt.TalkChacko, s.Prompt, "once each");
            rig.Sim.Teleport(h.Id, chacko.Pos + SimRig.U(-50, 0));
            rig.Run(1);
            rig.Tap(h.Id, Btn.Interact);
            Assert.IsTrue(h.Nic, "50 Nic");

            Assert.AreEqual(1f, chacko.HealthFor(Role.Survivor), "survivors can slay him now: one blow");
            Assert.AreEqual(1f, chacko.HealthFor(Role.Hunter), "as Zach's can");
            rig.Sim.Events.Clear();
            chacko.ItemHit(rig.Sim, s, "bottle");
            Assert.IsFalse(chacko.Alive, "a survivor's hit kills him");
            Assert.IsTrue(chacko.Gone, "blown apart");
            Assert.IsTrue(rig.Sim.Events.Exists(e => e.Kind == EventKind.Explosion), "the same explosion");
            Assert.IsTrue(rig.Sim.Events.Exists(e => e.Kind == EventKind.Talk && e.Text == "chacko_attacked"), "and the clip");
            Assert.AreEqual(1f - Balance.Chacko.Explosion, s.Hp, 1e-3f, "half his killer's health");
            Assert.AreEqual(s.Id, rig.Sim.Vengeance);
            Assert.IsTrue(jaden.Chasing && jaden.Target == s.Id, "Jaden goes after his killer");
            Assert.AreEqual(s.Id, plasma.Target, "and so does Plasma");
            Assert.IsTrue(plasma.Raging);
        }

        [Test]
        public void ZachKillingChackoBlowsHimUp()
        {
            var rig = new SimRig();
            SimPlayer h = rig.P(1);
            var chacko = new Chacko(rig.Sim);
            rig.Sim.Npcs.Add(chacko);
            chacko.Slash(rig.Sim, h, 1);
            Assert.IsFalse(chacko.Alive);
            Assert.IsTrue(chacko.Gone);
            Assert.AreEqual(1f - Balance.Chacko.Explosion, h.Hp, 1e-3f, "half of Zach's health");
            Assert.IsTrue(rig.Sim.Events.Exists(e => e.Kind == EventKind.Explosion));
        }

        [Test]
        public void EveryBuildingHasALoungeWithACouchFacingATv()
        {
            foreach (int seed in new[] { 1, 2, 3, 7, 42, 99, 1337, 2024, 31337, 8 })
            {
                var p = new Plan(seed, new Rect(-18f, -18f, 36f, 36f));
                Assert.GreaterOrEqual(p.LoungeRoom, 0, $"seed {seed}: a lounge");
                Plan.Room room = p.Rooms[p.LoungeRoom];
                Assert.IsFalse(room.HasGenerator || room.IsHallway, $"seed {seed}");
                var sofa = p.Items.Where(i => i.Room == p.LoungeRoom && i.Kind == Plan.Furn.Sofa).ToList();
                var tv = p.Items.Where(i => i.Room == p.LoungeRoom && i.Kind == Plan.Furn.Tv).ToList();
                Assert.AreEqual(1, sofa.Count, $"seed {seed}: one couch");
                Assert.AreEqual(1, tv.Count, $"seed {seed}: one TV");
                Assert.IsTrue(room.Area.Contains(p.LoungeSeat), $"seed {seed}: Chacko's seat is in the room");
                Vector2 look = (tv[0].Position - sofa[0].Position).normalized;
                var front = new Vector2(Mathf.Sin(sofa[0].Yaw * Mathf.Deg2Rad), Mathf.Cos(sofa[0].Yaw * Mathf.Deg2Rad));
                Assert.Greater(Vector2.Dot(look, front), 0.99f, $"seed {seed}: the couch faces the TV");
                Assert.AreEqual(tv[0].Position, p.LoungeTv);
            }
        }
    }
}
