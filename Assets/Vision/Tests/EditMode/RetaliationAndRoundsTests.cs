using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Player;

namespace Vision.Tests
{
    /// <summary>
    /// Monique and Daphne turning on whoever attacks them (Zach's machete, gas and burst included) for the whole of their
    /// ten seconds; a player's rounds passing by NPCs off their screen that aren't already roused.
    /// </summary>
    public class RetaliationAndRoundsTests
    {
        MatchState saved;

        [SetUp]
        public void SaveState()
        {
            saved = MatchState.Current;
            MatchState.Current = new MatchState();
        }

        [TearDown]
        public void RestoreState() => MatchState.Current = saved;

        static T Add<T>(SimRig rig, T n, float x, float y) where T : Npc
        {
            n.Pos = SimRig.U(x, y);
            rig.Sim.Npcs.Add(n);
            return n;
        }

        [Test]
        public void Monique_SlashedByZach_ShootsHimWithHerFiftyCal_ForTheWholeTenSeconds()
        {
            var rig = new SimRig(1, 1);
            SimPlayer zach = rig.P(1);
            var monique = Add(rig, new Monique(rig.Sim), 0, 0);
            rig.Place(zach, 300f, 0f);
            rig.Place(rig.P(2), -2000f, -2000f);
            monique.Slash(rig.Sim, zach, 1);
            Assert.IsTrue(monique.Armed, "slashed by Zach, she pulls her rifle (no running off)");
            Assert.AreEqual(Balance.Items.Sniper.Reload, Balance.Monique.Reload, "reloading as a survivor's 0.50 cal");
            var shots = new HashSet<Bullet>();
            float before = zach.Hp;
            for (int i = 0; i < SimRig.Secs(Balance.Monique.AttackSec - 0.5f); i++)
            {
                rig.Run(1);
                foreach (Bullet b in rig.Sim.Snipes) if (b.By == -monique.Id) shots.Add(b);
            }
            Assert.Less(zach.Hp, before, "her rounds find him");
            Assert.Greater(shots.Count, 3, "more than three shots: she keeps at it");
            Assert.IsTrue(monique.Armed, "still armed nearly ten seconds on");
            rig.Run(SimRig.Secs(1f));
            Assert.IsFalse(monique.Armed, "then she puts it away");
        }

        [Test]
        public void MoniqueAndDaphne_TurnOnZach_ForHisGasAndHisBurst()
        {
            var rig = new SimRig(1, 1);
            SimPlayer zach = rig.P(1);
            rig.Place(zach, 0f, 0f);
            rig.Place(rig.P(2), -2000f, -2000f);
            var monique = Add(rig, new Monique(rig.Sim), 400, 0);
            var daphne = Add(rig, new Daphne(rig.Sim), 0, 400);
            monique.Provoke(rig.Sim, zach);
            Assert.IsTrue(monique.Armed, "Penjamin's gas sets her off");
            // A Soundcloud Burst wave rolling over Daphne.
            rig.Sim.Bursts.Add(new BurstWave { Origin = zach.Pos, Dir = Vector2.up, T0 = rig.Sim.Time, By = zach.Id });
            rig.Run(SimRig.Secs(1.5f));
            Assert.IsTrue(daphne.Armed, "the burst sets her off");
            Assert.AreEqual(NpcFlags.None, daphne.Flags & NpcFlags.Fleeing, "she stands and fights, no running");
            Assert.AreEqual(NpcFlags.None, monique.Flags & NpcFlags.Fleeing);
        }

        static void SnipeAt(SimRig rig, SimPlayer s, float aim, System.Action before = null)
        {
            if (s.Inv.Count(ItemType.Sniper) == 0) s.Inv.Add(ItemType.Sniper);
            int slot = s.Inv.FirstSlotOf(ItemType.Sniper) + 1;
            // (Past the last shot's reload; then whatever's to be set up just before the shot.)
            rig.Run(SimRig.Secs(Balance.Items.Sniper.Reload));
            before?.Invoke();
            rig.Run(2, p => p.Id == s.Id ? new InputCmd { Buttons = Btn.Lunge, Item = slot, Aim = aim, AimDist = 100f } : (InputCmd?)null);
            rig.Run(1, p => p.Id == s.Id ? new InputCmd { Buttons = Btn.Lunge | Btn.Primary, Item = slot, Aim = aim, AimDist = 100f } : (InputCmd?)null);
            rig.Run(SimRig.Secs(1f), p => p.Id == s.Id ? new InputCmd { Buttons = Btn.Lunge, Item = slot, Aim = aim, AimDist = 100f } : (InputCmd?)null);
        }

        [Test]
        public void ARound_PassesByAnNpcOffTheShootersScreen_UnlessItsAlreadyRoused()
        {
            var rig = new SimRig(1, 1, true);
            SimPlayer s = rig.P(2);
            rig.Place(s, 0f, 0f);
            rig.Place(rig.P(1), -2000f, 2000f);
            // Their screen: 300 units either side across, 200 up and down.
            s.ViewHalf = SimRig.U(300f, 200f);
            Assert.IsTrue(MatchSim.OnScreen(s, SimRig.U(320f, 0f)), "within 1.1 times of it");
            Assert.IsFalse(MatchSim.OnScreen(s, SimRig.U(340f, 0f)));
            Assert.IsFalse(MatchSim.OnScreen(s, SimRig.U(0f, 225f)));
            // Thomas, calm, off their screen: the round goes by him.
            var thomas = Add(rig, new Thomas(rig.Sim), 450, 0);
            SnipeAt(rig, s, 0f);
            Assert.AreEqual(NpcFlags.None, thomas.Flags & NpcFlags.Fleeing, "untouched off screen");
            // On their screen, it strikes him.
            SnipeAt(rig, s, 0f, () => thomas.Pos = SimRig.U(250f, 0f));
            Assert.AreNotEqual(NpcFlags.None, thomas.Flags & NpcFlags.Fleeing, "struck on screen, he runs");
            // Daphne, already roused (armed), is struck off screen too: she turns on the shooter.
            var rig2 = new SimRig(1, 1, true);
            SimPlayer s2 = rig2.P(2);
            SimPlayer zach2 = rig2.P(1);
            rig2.Place(s2, 0f, 0f);
            rig2.Place(zach2, 450f, 600f);
            s2.ViewHalf = SimRig.U(300f, 200f);
            var daphne = Add(rig2, new Daphne(rig2.Sim), 450, 0);
            daphne.Slash(rig2.Sim, zach2, 1);
            Assert.IsTrue(daphne.Aggravated);
            SnipeAt(rig2, s2, 0f);
            rig2.Run(SimRig.Secs(1f));
            Assert.IsTrue(daphne.Armed);
            Assert.IsFalse(MatchSim.OnScreen(s2, daphne.Pos), "she was off their screen");
            Assert.Less(s2.Hp, 1f, "struck, she turned her pistol on the shooter");
        }

        [Test]
        public void TheView_ReachesTheHost_AsSent()
        {
            var p = new SimPlayer(4, "S", Role.Survivor, Vector2.zero);
            int packed = (Mathf.RoundToInt(1.25f * 100f) << 16) | (Mathf.RoundToInt(-3.5f * 100f) & 0xffff);
            MatchHost.UnpackView(p, new Vector2(9f, 6f), Mathf.RoundToInt(0.7854f * 10000f), packed);
            Assert.AreEqual(new Vector2(9f, 6f), p.ViewHalf);
            Assert.AreEqual(0.7854f, p.ViewYaw, 1e-3f);
            Assert.AreEqual(1.25f, p.ViewOffset.x, 1e-3f);
            Assert.AreEqual(-3.5f, p.ViewOffset.y, 1e-3f);
        }

        [Test]
        public void ChrisZelleysAmbulance_Is40PercentSmaller()
        {
            Assert.AreEqual(0.6f, World.LowPolyModels.AmbulanceScale, 1e-5f);
            Assert.AreEqual(new Vector3(4.5f, 3.3f, 9f) * 0.6f, World.LowPolyModels.AmbulanceSize);
            Mesh m = World.LowPolyModels.Ambulance(new System.Random(1));
            Assert.Less(m.bounds.size.z, 9f * 0.6f + 0.4f, "the model built to match");
            Assert.Greater(m.bounds.size.z, 9f * 0.6f - 0.4f);
        }
    }
}
