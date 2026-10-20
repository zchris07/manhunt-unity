using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Player;

namespace Vision.Tests
{
    /// <summary>
    /// The galaxy gas trap: its trigger half as wide again, its cloud 10% wider; and Zach in the gas can use none of his
    /// abilities (his lunge, the Burst, Penjamin, the Hemp Battery or Beam) until he's out of it.
    /// </summary>
    public class GasTrapTests
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

        [Test]
        public void TheTrap_TriggersHalfAsWideAgain_AndItsCloudIsTenPercentWider()
        {
            Assert.AreEqual(Balance.HunterWidth * 5f * 0.2f * 1.5f, Balance.Items.Trap.TriggerRadius, 1e-3f);
            Assert.AreEqual(Balance.HunterWidth * 10f * 0.65f * 0.85f * 0.85f * 1.1f, Balance.Items.Trap.GasRadius, 1e-3f);

            // An armed trap: Zach walking up sets it off from the wider ring.
            var rig = new SimRig();
            SimPlayer zach = rig.P(1);
            rig.Place(rig.P(2), 2500f, 2500f);
            rig.Sim.Traps.Add(new Trap { Id = rig.Sim.AllocEntityId(), Pos = SimRig.U(0f, 0f), Owner = 2, ArmT = 0f });
            float inside = Balance.Items.Trap.TriggerRadius * 0.9f;
            Assert.Greater(inside, Balance.HunterWidth * 5f * 0.2f, "where the old trigger never reached");
            rig.Place(zach, inside, 0f);
            rig.Run(2);
            Assert.AreEqual(0, rig.Sim.Traps.Count, "set off");
            Assert.AreEqual(1, rig.Sim.Gases.Count, "the cloud");
        }

        [Test]
        public void ZachInTheGas_CanUseNoneOfHisAbilities_UntilHesOutOfIt()
        {
            var rig = new SimRig();
            SimPlayer zach = rig.P(1);
            rig.Place(rig.P(2), 2500f, 2500f);
            rig.Place(zach, 0f, 0f);
            rig.Sim.Gases.Add(new GasCloud { Id = rig.Sim.AllocEntityId(), Pos = zach.Pos, Age = 1f });
            rig.Run(2);
            Assert.IsTrue(zach.Gassed);
            Assert.IsFalse(MatchSim.AbilitiesOn(zach), "all of them off in the gas");
            zach.BeamCharges = Balance.Hunter.Beam.Charges;
            zach.VapeCharges = 1;
            rig.Tap(zach.Id, Btn.Secondary);
            Assert.AreEqual(0, rig.Sim.Bursts.Count, "no Soundcloud Burst");
            rig.Tap(zach.Id, Btn.Ability);
            Assert.IsFalse(zach.HempOn, "no Hemp Battery");
            rig.Tap(zach.Id, Btn.Beam);
            Assert.AreEqual(0f, zach.BeamT, "no Hemp Beam");
            rig.Tap(zach.Id, Btn.Vape);
            Assert.AreEqual(0, rig.Sim.Vapes.Count, "no Penjamin");
            rig.Tap(zach.Id, Btn.Lunge);
            Assert.AreEqual(0f, zach.Move.LungeT, "no lunge");

            // Out of it, they're back.
            rig.Sim.Gases.Clear();
            rig.Run(SimRig.Secs(0.5f));
            Assert.IsTrue(MatchSim.AbilitiesOn(zach));
            rig.Tap(zach.Id, Btn.Secondary);
            Assert.AreEqual(1, rig.Sim.Bursts.Count, "the Burst again");
        }
    }
}
