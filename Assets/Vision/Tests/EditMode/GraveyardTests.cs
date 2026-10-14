using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Player;

namespace Vision.Tests
{
    /// <summary>
    /// The graveyard's gates (locked at the start, picked open by a survivor in three spinning-tick checks, two misses
    /// starting it over; afterwards opened and closed freely by survivors; Zach never opens them and only smashes them in
    /// once they're unlocked and shut again, with four full charged swings), its fence (only Zach climbs it, five times as
    /// slowly as a log), and the gas trap planted by holding the button.
    /// </summary>
    public class GraveyardTests
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

        /// <summary>Open ground with the graveyard's gates, shut and locked, across the way east of the origin.</summary>
        static MatchSim GateArena()
        {
            var map = new SimMap { HalfExtent = 90f };
            map.Generators.Add(SimRig.U(2000, 2000));
            map.Doors.Add(new SimMap.DoorDef { A = SimRig.U(100, -60), B = SimRig.U(100, 60), Gate = true });
            map.SurvivorSpawns.Add(SimRig.U(-2000, -2000));
            map.HunterSpawns.Add(SimRig.U(2000, -2000));
            return new MatchSim(map, new OpenGeometry(), MatchRules.Resolve(2, 1), false, 7);
        }

        static void Run(MatchSim sim, int ticks, int id = 0, Btn b = Btn.None, float aim = 0f)
        {
            for (int t = 0; t < ticks; t++)
            {
                foreach (SimPlayer p in sim.Order)
                    sim.SubmitInput(p.Id, p.Id == id ? new InputCmd { Buttons = b, Aim = aim, AimDist = 100f } : new InputCmd { Aim = p.Facing, AimDist = 100f });
                sim.Step();
                sim.Events.Clear();
            }
        }

        /// <summary>Where the tick will be <paramref name="ahead"/> seconds on, against its mark (degrees off it).</summary>
        static float Off(SimPlayer p, float ahead)
        {
            float a = Mathf.Repeat(MatchSim.PickSpeed(p.PickStage) * (p.PickT + ahead) * p.PickDir, 360f);
            return Mathf.Abs(Mathf.DeltaAngle(a, p.PickZone));
        }

        /// <summary>Waits for the tick to come round onto the mark (now and a tick on), then presses.</summary>
        static void Hit(MatchSim sim, SimPlayer p)
        {
            float half = MatchSim.PickWidth(p.PickStage) * 0.5f - 2f;
            for (int i = 0; i < 400; i++)
            {
                if (p.PickT > 0f && Off(p, 0f) <= half && Off(p, MatchSim.TickDt) <= half)
                {
                    Run(sim, 1, p.Id, Btn.Interact);
                    Run(sim, 1);
                    return;
                }
                Run(sim, 1);
            }
            Assert.Fail("the tick never came round");
        }

        /// <summary>Presses with the tick well away from the mark.</summary>
        static void Miss(MatchSim sim, SimPlayer p)
        {
            for (int i = 0; i < 400; i++)
            {
                if (p.PickT > 0f && Off(p, 0f) > 90f && Off(p, MatchSim.TickDt) > 90f)
                {
                    Run(sim, 1, p.Id, Btn.Space);
                    Run(sim, 1);
                    return;
                }
                Run(sim, 1);
            }
            Assert.Fail("the tick never came away from the mark");
        }

        [Test]
        public void TheGates_StartLocked_ASurvivorPicksThem_FiveChecks_TwoMissesStartOver_ThenTheyOpenAndCloseFreely()
        {
            MatchSim sim = GateArena();
            sim.AddPlayer(1, "Zach", Role.Hunter, SimRig.U(-2000, 2000));
            SimPlayer s = sim.AddPlayer(2, "S", Role.Survivor, SimRig.U(50, 0));
            sim.AddPlayer(3, "T", Role.Survivor, SimRig.U(-2000, -2000));
            Run(sim, 2);
            Assert.IsTrue(sim.DoorLocked[0], "locked at the start");
            sim.SetDoor(0, true);
            Assert.IsFalse(sim.Doors[0], "and a locked gate stays shut");
            Assert.AreEqual(Prompt.PickLock, s.Prompt);

            Run(sim, 1, 2, Btn.Interact);
            Run(sim, 1);
            Assert.AreEqual(ActionKind.PickLock, s.Action, "E starts picking");
            Hit(sim, s);
            Assert.AreEqual(1, s.PickStage, "the first pin sets");
            Miss(sim, s);
            Assert.AreEqual(1, s.PickStage);
            Assert.AreEqual(1, s.PickMiss, "one miss");
            Miss(sim, s);
            Assert.AreEqual(0, s.PickStage, "two misses: back to the first check");
            Assert.AreEqual(0, s.PickMiss);
            Assert.AreEqual(ActionKind.PickLock, s.Action, "still at it");

            for (int i = 0; i < Balance.Graveyard.PickChecks - 1; i++) Hit(sim, s);
            Assert.AreEqual(4, s.PickStage, "four pins set");
            Assert.IsTrue(sim.DoorLocked[0], "four hits aren't enough");
            Hit(sim, s);
            Assert.AreEqual(5, Balance.Graveyard.PickChecks);
            Assert.IsFalse(sim.DoorLocked[0], "five checks: the lock gives");
            Assert.IsTrue(sim.Doors[0], "and the gates swing open");
            Assert.AreEqual(ActionKind.None, s.Action);

            Run(sim, Secs(0.5f));
            Assert.AreEqual(Prompt.CloseGates, s.Prompt);
            Run(sim, 1, 2, Btn.Interact);
            Run(sim, Secs(0.5f));
            Assert.IsFalse(sim.Doors[0], "closed");
            Assert.AreEqual(Prompt.OpenGates, s.Prompt, "and open again, no lock now");
            Run(sim, 1, 2, Btn.Interact);
            Run(sim, 1);
            Assert.IsTrue(sim.Doors[0]);
        }

        [Test]
        public void MovingAway_StopsThePicking()
        {
            MatchSim sim = GateArena();
            sim.AddPlayer(1, "Zach", Role.Hunter, SimRig.U(-2000, 2000));
            SimPlayer s = sim.AddPlayer(2, "S", Role.Survivor, SimRig.U(50, 0));
            sim.AddPlayer(3, "T", Role.Survivor, SimRig.U(-2000, -2000));
            Run(sim, 2);
            Run(sim, 1, 2, Btn.Interact);
            Assert.AreEqual(ActionKind.PickLock, s.Action);
            sim.Teleport(2, SimRig.U(-600, 0));
            Run(sim, 2);
            Assert.AreNotEqual(ActionKind.PickLock, s.Action, "out of reach: the picking stops");
            Assert.IsTrue(sim.DoorLocked[0]);
        }

        static void Swipe(MatchSim sim, int id, bool full)
        {
            if (full) Run(sim, Secs(Balance.Hunter.Attack.HeavyAt) + 2, id, Btn.Primary, 0f);
            else Run(sim, 1, id, Btn.Primary, 0f);
            Run(sim, Secs(Balance.Hunter.Attack.SwingTime + Balance.Hunter.Attack.Windup) + 3, id, Btn.None, 0f);
        }

        [Test]
        public void Zach_NeverOpensTheGates_CantBreakThemLocked_AndSmashesThemShutOnlyWithFourFullSwings()
        {
            MatchSim sim = GateArena();
            SimPlayer zach = sim.AddPlayer(1, "Zach", Role.Hunter, SimRig.U(40, 0));
            sim.AddPlayer(2, "S", Role.Survivor, SimRig.U(-2000, -2000));
            sim.AddPlayer(3, "T", Role.Survivor, SimRig.U(-2000, -1900));
            zach.Facing = 0f;
            Run(sim, 2);
            Assert.AreNotEqual(Prompt.OpenDoor, zach.Prompt, "he can't open them");
            Assert.AreNotEqual(Prompt.OpenGates, zach.Prompt);
            Assert.AreNotEqual(Prompt.PickLock, zach.Prompt);
            Swipe(sim, 1, true);
            Assert.AreEqual(0, sim.DoorHits[0], "locked, they don't give at all");

            // Unlocked (a survivor picked them) and shut again.
            sim.DoorLocked[0] = false;
            for (int i = 0; i < 3; i++) Swipe(sim, 1, true);
            Assert.IsFalse(sim.DoorBroken[0], "three full swings aren't enough");
            Swipe(sim, 1, true);
            Assert.IsTrue(sim.DoorBroken[0], "the fourth smashes them in");

            // Light swings: as many as make four full ones.
            MatchSim sim2 = GateArena();
            SimPlayer z2 = sim2.AddPlayer(1, "Zach", Role.Hunter, SimRig.U(40, 0));
            sim2.AddPlayer(2, "S", Role.Survivor, SimRig.U(-2000, -2000));
            sim2.AddPlayer(3, "T", Role.Survivor, SimRig.U(-2000, -1900));
            z2.Facing = 0f;
            sim2.DoorLocked[0] = false;
            for (int i = 0; i < 7; i++) Swipe(sim2, 1, false);
            Assert.IsFalse(sim2.DoorBroken[0], "seven light swings aren't enough");
            Swipe(sim2, 1, false);
            Assert.IsTrue(sim2.DoorBroken[0], "eight are");
        }

        [Test]
        public void OnlyZach_ClimbsTheGraveyardFence_FiveTimesAsSlowlyAsALog()
        {
            int Crossing(bool hunter, bool graveyard, out bool climbed)
            {
                var rig = new SimRig(1, 1);
                rig.Geo.Lows.Add((SimRig.U(100, -80), SimRig.U(100, 80), Scale.D(10f)));
                rig.Geo.LowSlow.Add(1f);
                rig.Geo.LowHunterOnly.Add(graveyard);
                SimPlayer who = hunter ? rig.P(1) : rig.P(2), other = hunter ? rig.P(2) : rig.P(1);
                rig.Place(who, 0f, 0f);
                rig.Place(other, 0f, -2000f);
                int ticks = 0;
                climbed = false;
                for (int i = 0; i < Secs(14f); i++)
                {
                    rig.Run(1, p => p.Id == who.Id ? new InputCmd { Move = Vector2.right, Aim = 0f, AimDist = 100f } : (InputCmd?)null);
                    if (who.Move.Climbing) ticks++;
                    if (Scale.ToUnits(who.Pos.x) > 160f) break;
                }
                climbed = ticks > 0;
                return ticks;
            }
            Crossing(false, true, out bool survivorClimbs);
            Assert.IsFalse(survivorClimbs, "survivors can't climb it (the fence stops them: its own physics layer)");
            int log = Crossing(true, false, out _), fence = Crossing(true, true, out bool zachClimbs);
            Assert.IsTrue(zachClimbs, "Zach can");
            Assert.AreEqual(5f, fence / (float)log, 0.6f, "taking 400% longer than over a log");
            Assert.AreEqual(5f, Balance.Graveyard.ZachFenceSlow);
        }

        [Test]
        public void TheGasTrap_IsPlantedByHoldingTheButton_LettingGoStartsItOver()
        {
            var rig = new SimRig(1, 1);
            SimPlayer s = rig.P(2);
            rig.Place(s, 0f, 0f);
            rig.Place(rig.P(1), 0f, -2000f);
            rig.Sim.AddItem(s, ItemType.Trap);
            rig.Run(1, p => p.Id == s.Id ? new InputCmd { Aim = 0f, AimDist = 100f, Item = 1 } : (InputCmd?)null);
            rig.Hold(s.Id, Btn.Primary, Secs(Balance.Items.Trap.PlantTime * 0.6f), 0f, 1);
            Assert.AreEqual(ActionKind.Plant, s.Action, "planting while held");
            rig.Run(Secs(Balance.Items.Trap.PlantTime), p => p.Id == s.Id ? new InputCmd { Aim = 0f, AimDist = 100f, Item = 1 } : (InputCmd?)null);
            Assert.AreEqual(0, rig.Sim.Traps.Count, "let go before it's down: nothing planted");
            Assert.AreEqual(ActionKind.None, s.Action, "and it starts over");
            rig.Hold(s.Id, Btn.Primary, Secs(Balance.Items.Trap.PlantTime) + 3, 0f, 1);
            Assert.AreEqual(1, rig.Sim.Traps.Count, "held the whole way: planted");
        }
    }
}
