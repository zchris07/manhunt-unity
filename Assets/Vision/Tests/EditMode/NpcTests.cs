using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Player;

namespace Vision.Tests
{
    /// <summary>
    /// The NPCs against the original's rules (npcs.test.ts and the batch tests): the nav grid, Shane's alert and chase,
    /// Jaden's pistol and death, Marc's confit, Waz's looksie and slaying, Njaaron's question, Soham's fuse, Thomas's beam,
    /// Monique's arrow and 0.50 cal, and the roster.
    /// </summary>
    public class NpcTests
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

        static float Dist(Npc n, SimPlayer p) => Vector2.Distance(n.Pos, p.Pos) / Scale.Unit;

        [Test]
        public void NavGridPathsAroundAWall()
        {
            // A wall across x = 0 from y = -30 to 40 (design units).
            var grid = new NavGrid(60f, 1f, p => Mathf.Abs(p.x) < 1f && p.y > -30f && p.y < 40f);
            List<Vector2> path = grid.FindPath(new Vector2(-10f, 0f), new Vector2(10f, 0f));
            Assert.IsNotNull(path, "no path round the wall");
            Assert.Greater(path.Count, 2);
            bool round = false;
            foreach (Vector2 p in path)
            {
                Assert.IsFalse(grid.IsBlocked(p), $"path goes through the wall at {p}");
                if (p.y >= 39f || p.y <= -29f) round = true;
            }
            Assert.IsTrue(round, "the path should go round an end of the wall");
            Assert.Less(Vector2.Distance(path[path.Count - 1], new Vector2(10f, 0f)), 1.5f);

            // Walled in completely: no path.
            var shut = new NavGrid(60f, 1f, p => Mathf.Abs(p.x) < 1f);
            Assert.IsNull(shut.FindPath(new Vector2(-10f, 0f), new Vector2(10f, 0f)));
        }

        [Test]
        public void ShaneIsAlertedTailsTheSurvivorAndZachBreaksIt()
        {
            var rig = new SimRig();
            SimPlayer s = rig.P(2), h = rig.P(1);
            rig.Place(s, 0, 0);
            s.Facing = 0f;
            var shane = Add(rig, new Shane(rig.Sim), 80, 0);
            Assert.AreEqual(0f, shane.AlertLevel);
            rig.Run(SimRig.Secs(0.5f));
            Assert.Greater(shane.AlertLevel, 0f, "a flashlight on him and standing close builds the meter");
            rig.Run(SimRig.Secs(3f));
            Assert.IsTrue(shane.Chasing, "alerted within a few seconds");
            Assert.AreEqual(s.Id, shane.Target);
            Assert.AreEqual(1f, shane.AlertLevel);

            rig.Place(s, 700, 0);
            float before = Dist(shane, s);
            rig.Run(SimRig.Secs(1.5f));
            Assert.Less(Dist(shane, s), before - 200f, "he tails the survivor at a run");
            Assert.AreEqual(2, shane.Gait);

            // Zach close by ends the chase.
            rig.Sim.Teleport(h.Id, shane.Pos + SimRig.U(100, 0));
            rig.Run(2);
            Assert.IsFalse(shane.Chasing);
        }

        [Test]
        public void ShaneIgnoresZachAndTwoBottlesShakeHimOff()
        {
            var rig = new SimRig(1, 1);
            SimPlayer s = rig.P(2), h = rig.P(1);
            rig.Place(s, 2000, 2000);
            rig.Place(h, 0, 0);
            h.Facing = 0f;
            var shane = Add(rig, new Shane(rig.Sim), 60, 0);
            rig.Run(SimRig.Secs(5f));
            Assert.IsFalse(shane.Chasing, "Zach never alerts him");

            rig.Place(h, 3000, -3000);
            rig.Place(s, 0, 0);
            s.Facing = 0f;
            shane.Pos = SimRig.U(60, 0);
            rig.Run(SimRig.Secs(4f));
            Assert.IsTrue(shane.Chasing);
            shane.ItemHit(rig.Sim, s, "bottle");
            Assert.IsTrue(shane.Chasing, "one bottle isn't enough");
            shane.ItemHit(rig.Sim, s, "bottle");
            Assert.AreEqual(Shane.Mode.Flee, shane.State);
            rig.Run(SimRig.Secs(1f));
            Assert.AreEqual(0f, shane.AlertLevel, "no alert builds while he runs off");
        }

        [Test]
        public void JadenShootsWhoeverSetHimOffThenLetsThemGo()
        {
            var rig = new SimRig();
            SimPlayer s = rig.P(2);
            rig.Place(s, 0, 0);
            s.Facing = 0f;
            var jaden = Add(rig, new Jaden(rig.Sim), 150, 0);
            rig.Run(SimRig.Secs(4f));
            Assert.IsTrue(jaden.Chasing, "alerted like Shane");
            Assert.IsTrue((jaden.Flags & NpcFlags.Armed) != 0, "pistol out");
            int shots = 0;
            for (int t = 0; t < SimRig.Secs(12f) && jaden.Chasing; t++)
            {
                rig.Sim.SubmitInput(s.Id, new InputCmd { Aim = 0f, AimDist = 100f, Item = 1 });
                rig.Sim.Step();
                foreach (GameEvent e in rig.Sim.Events) if (e.Kind == EventKind.Shot && e.A == -jaden.Id) shots++;
                rig.Sim.Events.Clear();
            }
            Assert.Greater(shots, 2);
            Assert.Less(s.Hp, 1f, "his shots land");
            Assert.IsFalse(jaden.Chasing, "he lets them go once they've lost half their health");
            Assert.LessOrEqual(s.Hp, 0.5f + 1e-4f);
            Assert.Greater(s.Hp, 0.5f - Balance.Jaden.Gun.Damage - 1e-4f);
        }

        [Test]
        public void JadenDiesToThreeSurvivorHitsAndDropsHisPistol()
        {
            var rig = new SimRig();
            SimPlayer s = rig.P(2);
            rig.Place(s, -800, -800);
            var jaden = Add(rig, new Jaden(rig.Sim), 0, 0);
            jaden.ItemHit(rig.Sim, s, "bottle");
            Assert.Greater(jaden.StunT, 0f, "stunned");
            Assert.IsTrue(jaden.Alive);
            jaden.ItemHit(rig.Sim, s, "bottle");
            jaden.ItemHit(rig.Sim, s, "bottle");
            Assert.IsFalse(jaden.Alive);
            Assert.IsFalse(jaden.Gone, "he lies where he fell");
            Assert.IsTrue(rig.Sim.Drops.Exists(d => d.Item == ItemType.Pistol), "his pistol drops");
        }

        [Test]
        public void ZachSlayingJadenMakesHimStronger()
        {
            var rig = new SimRig();
            SimPlayer h = rig.P(1);
            rig.Place(h, -40, 0);
            var jaden = Add(rig, new Jaden(rig.Sim), 0, 0);
            jaden.Slash(rig.Sim, h, 2);
            Assert.IsTrue(jaden.Alive);
            Assert.IsTrue(jaden.Chasing, "attacking him sets him on Zach");
            Assert.AreEqual(h.Id, jaden.Target);
            Assert.Greater(Vector2.Distance(jaden.Pos, Vector2.zero), 0f, "shoved");
            jaden.Slash(rig.Sim, h, 2);
            jaden.Slash(rig.Sim, h, 2);
            Assert.IsFalse(jaden.Alive, "six points of the machete");
            Assert.AreEqual(1, h.JadenBonus);
            Assert.IsFalse(rig.Sim.Drops.Exists(d => d.Item == ItemType.Pistol), "Zach can't use a pistol");
        }

        [Test]
        public void MarcHandsOverConfitOnceAndShrugsOffZach()
        {
            var rig = new SimRig();
            SimPlayer s = rig.P(2), h = rig.P(1);
            var marc = Add(rig, new Marc(rig.Sim), 0, 0);
            rig.Place(s, 50, 0);
            rig.Run(1);
            Assert.AreEqual(Prompt.TalkMarc, s.Prompt);
            rig.Tap(s.Id, Btn.Interact);
            Assert.AreEqual(1, s.Inv.Count(ItemType.Confit));
            Assert.IsNotNull(marc.Say);
            rig.Run(SimRig.Secs(Balance.Marc.TalkCooldown + 0.2f));
            marc.Pos = s.Pos + SimRig.U(50, 0);
            rig.Run(1);
            rig.Tap(s.Id, Btn.Interact);
            Assert.AreEqual(1, s.Inv.Count(ItemType.Confit), "once each");

            marc.Slash(rig.Sim, h, 2);
            Assert.IsTrue(marc.Alive, "nothing hurts him");
            StringAssert.Contains("what the heck", marc.Say);
        }

        [Test]
        public void WazTakesALooksieAndHisSlayerIsMarked()
        {
            var rig = new SimRig(2, 1);
            SimPlayer s = rig.P(2), s2 = rig.P(3), h = rig.P(1);
            var waz = Add(rig, new Waz(rig.Sim), 0, 0);
            rig.Place(s, 50, 0);
            rig.Place(s2, -2000, -2000);
            rig.Run(1);
            Assert.AreEqual(Prompt.TalkWaz, s.Prompt);
            rig.Tap(s.Id, Btn.Interact);
            Assert.IsTrue(s.WazLooked);
            Assert.AreEqual(1.1f, s.FovMul, 1e-4f);
            rig.Run(1);
            Assert.AreNotEqual(Prompt.TalkWaz, s.Prompt, "once each");

            // Zach: three hits, bolting after each.
            waz.Slash(rig.Sim, h, 1);
            Assert.IsTrue(waz.Alive);
            Assert.IsTrue((waz.Flags & NpcFlags.Fleeing) != 0);
            waz.Slash(rig.Sim, h, 1);
            rig.Sim.Events.Clear();
            waz.Slash(rig.Sim, h, 1);
            Assert.IsFalse(waz.Alive);
            Assert.AreEqual(1.1f, h.FovMul, 1e-4f, "Zach who slays him sees more");
            Assert.IsTrue(rig.Sim.Events.Exists(e => e.Kind == EventKind.WazSlain && e.A == h.Id));

            // A survivor's item slays him at once, and that survivor sees less.
            var rig2 = new SimRig();
            var waz2 = Add(rig2, new Waz(rig2.Sim), 0, 0);
            waz2.ItemHit(rig2.Sim, rig2.P(2), "bottle");
            Assert.IsFalse(waz2.Alive);
            Assert.AreEqual(0.9f, rig2.P(2).FovMul, 1e-4f);
        }

        [Test]
        public void NjaaronAsksAndActsOnTheAnswer()
        {
            // A survivor says yes: he follows them, and takes on Zach when Zach closes in.
            var rig = new SimRig();
            SimPlayer s = rig.P(2), h = rig.P(1);
            var nj = Add(rig, new Njaaron(rig.Sim), 0, 0);
            rig.Place(s, 50, 0);
            rig.Run(1);
            Assert.AreEqual(Prompt.TalkNjaaron, s.Prompt);
            rig.Tap(s.Id, Btn.Interact);
            Assert.AreEqual(Njaaron.Mode.Ask, nj.State);
            rig.Run(1);
            Assert.AreEqual(Prompt.NjaaronAsk, s.Prompt);
            rig.Tap(s.Id, Btn.Yes);
            Assert.AreEqual(Njaaron.Mode.Follow, nj.State);
            rig.Place(s, 600, 0);
            rig.Run(SimRig.Secs(3f));
            Assert.Less(Dist(nj, s), 200f, "he keeps up");
            rig.Sim.Teleport(h.Id, s.Pos + SimRig.U(200, 0));
            rig.Run(SimRig.Secs(2.5f));
            Assert.AreEqual(Njaaron.Mode.Defend, nj.State);
            Assert.Less(h.Hp, 1f, "he punches Zach");

            // No: he fights whoever said it.
            var rig2 = new SimRig();
            SimPlayer s2 = rig2.P(2);
            var nj2 = Add(rig2, new Njaaron(rig2.Sim), 0, 0);
            rig2.Place(s2, 50, 0);
            rig2.Run(1);
            rig2.Tap(s2.Id, Btn.Interact);
            rig2.Run(1);
            rig2.Tap(s2.Id, Btn.No);
            Assert.AreEqual(Njaaron.Mode.Attack, nj2.State);
            rig2.Run(SimRig.Secs(1.5f));
            Assert.Less(s2.Hp, 1f, "punched");

            // Zach says yes: his health comes back faster.
            var rig3 = new SimRig();
            SimPlayer h3 = rig3.P(1);
            var nj3 = Add(rig3, new Njaaron(rig3.Sim), 0, 0);
            rig3.Place(h3, 50, 0);
            rig3.Place(rig3.P(2), -2000, -2000);
            rig3.Run(1);
            Assert.AreEqual(Prompt.TalkNjaaron, h3.Prompt);
            rig3.Tap(h3.Id, Btn.Interact);
            rig3.Run(1);
            rig3.Tap(h3.Id, Btn.Yes);
            Assert.IsTrue(h3.NjaaronRegen);

            // Four of Zach's points kill him, and he explodes.
            rig3.Sim.Events.Clear();
            for (int i = 0; i < Balance.Njaaron.Hp; i++) nj3.Slash(rig3.Sim, h3, 1);
            Assert.IsFalse(nj3.Alive);
            Assert.IsTrue(rig3.Sim.Events.Exists(e => e.Kind == EventKind.Explosion));
            Assert.Less(h3.Hp, 1f, "caught in the blast");
        }

        [Test]
        public void SohamSaysHiThenExplodes()
        {
            var rig = new SimRig();
            SimPlayer s = rig.P(2);
            var soham = Add(rig, new Soham(rig.Sim), 0, 0);
            rig.Place(s, 50, 0);
            rig.Run(1);
            Assert.AreEqual(Prompt.TalkSoham, s.Prompt);
            rig.Tap(s.Id, Btn.Interact);
            Assert.AreEqual("Hi", soham.Say);
            Assert.IsTrue((soham.Flags & NpcFlags.Fuse) != 0);
            rig.Run(SimRig.Secs(Balance.Soham.Fuse - 0.3f));
            Assert.IsTrue(soham.Alive);
            rig.Run(SimRig.Secs(0.5f));
            Assert.IsFalse(soham.Alive);
            Assert.IsTrue(soham.Gone);
            Assert.Less(s.Hp, 1f, "too close to the blast");
        }

        [Test]
        public void ThomasGivesOneFullHempBeam()
        {
            var rig = new SimRig(2, 1);
            SimPlayer s = rig.P(2), s2 = rig.P(3);
            var thomas = Add(rig, new Thomas(rig.Sim), 0, 0);
            rig.Place(s, 50, 0);
            rig.Place(s2, -50, 0);
            rig.Run(1);
            rig.Tap(s.Id, Btn.Interact);
            Assert.AreEqual(Balance.Hunter.Beam.Charges, s.BeamCharges);
            rig.Run(SimRig.Secs(1.6f));
            thomas.Pos = Vector2.zero;
            rig.Run(1);
            rig.Tap(s2.Id, Btn.Interact);
            Assert.AreEqual(0, s2.BeamCharges, "only one");
            Assert.AreEqual(Balance.Thomas.LineAfter, thomas.Say);

            thomas.ItemHit(rig.Sim, s, "bottle");
            Assert.IsTrue((thomas.Flags & NpcFlags.Fleeing) != 0, "attacked, he runs");
        }

        [Test]
        public void MoniqueGivesAnArrowThenBarsAndShootsWhoeverAttacksHer()
        {
            var rig = new SimRig(2, 1);
            SimPlayer s = rig.P(2), s2 = rig.P(3);
            var monique = Add(rig, new Monique(rig.Sim), 0, 0);
            rig.Place(s, 50, 0);
            rig.Place(s2, -50, 0);
            rig.Run(1);
            Assert.AreEqual(Prompt.TalkMonique, s.Prompt);
            rig.Tap(s.Id, Btn.Interact);
            Assert.AreEqual(Balance.Monique.ArrowSec, s.ArrowT, 0.1f);
            rig.Run(1);
            Assert.AreEqual(Prompt.TalkMonique, s2.Prompt);
            rig.Tap(s2.Id, Btn.Interact);
            Assert.AreEqual(1, s2.Inv.Count(ItemType.MrBeastBar));
            Assert.AreEqual(0f, s2.ArrowT);

            rig.Place(s, 400, 0);
            monique.ItemHit(rig.Sim, s, "bottle");
            Assert.IsTrue(monique.Armed);
            rig.Run(SimRig.Secs(2.5f));
            Assert.Less(s.Hp, 1f, "her 0.50 cal finds them");
            Assert.AreEqual(1f, s2.Hp, "only the one who attacked her");
            rig.Run(SimRig.Secs(Balance.Monique.AttackSec));
            Assert.IsFalse(monique.Armed, "ten seconds, then she puts it away");
        }

        [Test]
        public void TheRosterSpawnsEveryoneOnOpenGroundTheSameWayEachTime()
        {
            var a = new SimRig(seed: 7);
            var b = new SimRig(seed: 7);
            NpcRoster.Spawn(a.Sim);
            NpcRoster.Spawn(b.Sim);
            Assert.AreEqual(NpcRoster.Makers.Length, a.Sim.Npcs.Count);
            var ids = new HashSet<int>();
            for (int i = 0; i < a.Sim.Npcs.Count; i++)
            {
                Npc n = a.Sim.Npcs[i];
                Assert.IsTrue(ids.Add(n.Id), $"{n.Name}: duplicate id");
                Assert.IsFalse(a.Geo.Blocked(n.Pos, n.HitRadius), $"{n.Name} spawned in a wall");
                Assert.AreEqual(n.Pos, b.Sim.Npcs[i].Pos, $"{n.Name}: spawn differs between identical seeds");
            }
            a.Run(SimRig.Secs(10f));
            b.Run(SimRig.Secs(10f));
            for (int i = 0; i < a.Sim.Npcs.Count; i++)
                Assert.AreEqual(a.Sim.Npcs[i].Pos, b.Sim.Npcs[i].Pos, $"{a.Sim.Npcs[i].Name} drifted between identical runs");
        }
    }
}
