using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;

namespace Vision.Tests
{
    /// <summary>
    /// NPCs on a chase climb over what a survivor climbs (fences, logs, windows, smashing the glass) and Jaden, Njaaron and
    /// Plasma knock a shut door down with three punches; survivors open a gate faster together.
    /// </summary>
    public class NpcChaseAndGateTests
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

        static int Secs(float s) => SimRig.Secs(s);
        static Vector2 U(float x, float y) => SimRig.U(x, y);

        [Test]
        public void OnAChase_ShaneClimbsAFenceAndAWindow_SmashingItsGlass_ButNotWhenHesOnlyWalking()
        {
            // A long fence (climbable) between Shane and a survivor, a window in it.
            var rig = new SimRig(1, 1, false, 1, m => m.Windows.Add(new SimMap.WindowDef { A = U(0f, -40f), B = U(0f, 40f) }));
            rig.Geo.Climbables.Add((U(0f, -3000f), U(0f, 3000f), Scale.D(6f)));
            SimPlayer s = rig.P(2);
            rig.Place(rig.P(1), -2500f, 2500f);
            rig.Place(s, 320f, 0f);
            var shane = new Shane(rig.Sim) { Pos = U(-320f, 0f) };
            rig.Sim.Npcs.Add(shane);
            Assert.IsTrue(rig.Geo.Blocked(U(0f, 0f), Scale.D(10f)) && !rig.Geo.BlockedClimbing(U(0f, 0f), Scale.D(10f)), "the fence stops a body that doesn't climb");
            shane.Avenge(rig.Sim, s);
            Assert.IsTrue(shane.Chasing);
            bool climbed = false;
            for (int i = 0; i < Secs(6f) && shane.Pos.x < U(60f, 0f).x; i++)
            {
                rig.Run(1);
                climbed |= (shane.Flags & NpcFlags.Climbing) != 0;
            }
            Assert.Greater(shane.Pos.x, 0f, "over it, after them");
            Assert.IsTrue(climbed, "climbing as he goes over");
            Assert.IsTrue(rig.Sim.WindowsBroken[0], "the window's glass smashed as he went through");

            // Walking (no chase), the fence stops him.
            var rig2 = new SimRig(1, 1);
            rig2.Geo.Climbables.Add((U(0f, -3000f), U(0f, 3000f), Scale.D(6f)));
            rig2.Place(rig2.P(1), -2500f, 2500f);
            rig2.Place(rig2.P(2), 2500f, 2500f);
            var walker = new Shane(rig2.Sim) { Pos = U(-40f, 0f) };
            rig2.Sim.Npcs.Add(walker);
            for (int i = 0; i < Secs(20f); i++)
            {
                rig2.Run(1);
                Assert.Less(walker.Pos.x, 0f, "a fence he only climbs on a chase");
            }
        }

        [Test]
        public void OnAChase_NjaaronKnocksAShutDoorDown_WithThreePunches()
        {
            // A wall (pillars all along) with a shut door in it, the survivor beyond.
            var rig = new SimRig(1, 1, false, 1, m => m.Doors.Add(new SimMap.DoorDef { A = U(0f, -40f), B = U(0f, 40f) }));
            for (float y = -3000f; y <= 3000f; y += 15f) rig.Geo.Pillars.Add((U(0f, y), Scale.D(9f)));
            SimPlayer s = rig.P(2);
            rig.Place(rig.P(1), -2500f, 2500f);
            rig.Place(s, 140f, 0f);
            var njaaron = new Njaaron(rig.Sim) { Pos = U(-140f, 0f) };
            rig.Sim.Npcs.Add(njaaron);
            Assert.IsFalse(rig.Sim.Doors[0], "shut");
            njaaron.Avenge(rig.Sim, s);
            int punches = 0;
            bool wasPunching = false;
            for (int i = 0; i < Secs(8f) && !rig.Sim.DoorBroken[0]; i++)
            {
                rig.Run(1);
                bool punching = (njaaron.Flags & NpcFlags.Punching) != 0;
                if (punching && !wasPunching) punches++;
                wasPunching = punching;
            }
            Assert.IsTrue(rig.Sim.DoorBroken[0], "knocked down");
            Assert.IsTrue(rig.Sim.Doors[0], "and open for good");
            Assert.AreEqual(Balance.NpcChase.DoorPunches, punches, "three punches");
            Assert.AreEqual(3, Balance.NpcChase.DoorPunches);
        }

        [Test]
        public void TwoSurvivorsAtAGate_OpenItFaster_AsAtAGenerator()
        {
            var rig = new SimRig(2, 1);
            SimPlayer a = rig.P(2), b = rig.P(3);
            rig.Place(rig.P(1), 1500f, -1500f);
            rig.Sim.Gate.Powered = true;
            Vector2 lever = rig.Map.Lever;
            rig.Sim.Teleport(a.Id, lever + U(-30f, 0f));
            rig.Sim.Teleport(b.Id, lever + U(30f, 0f));
            rig.Run(2);
            InputCmd? Hold(SimPlayer p, params SimPlayer[] who) => who.Contains(p) ? new InputCmd { Buttons = Btn.Interact, AimDist = 100f } : (InputCmd?)null;
            rig.Run(Secs(2f), p => Hold(p, a));
            float alone = rig.Sim.Gate.Progress;
            rig.Run(1, p => Hold(p, a, b));
            Assert.AreEqual(ActionKind.OpenGate, b.Action, "both at the lever");
            float before = rig.Sim.Gate.Progress;
            rig.Run(Secs(2f), p => Hold(p, a, b));
            Assert.AreEqual(1f + Balance.Objectives.CoopStep, (rig.Sim.Gate.Progress - before) / alone, 0.03f, "a quarter faster with two");
        }
    }
}
