using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Player;
using Vision.World;
using static Vision.Tests.SimRig;

namespace Vision.Tests
{
    /// <summary>
    /// The Hemp Beam in anyone's hands (1 HP ten times a second to whoever it touches, glass and doors), Penjamin through
    /// the shield and its shorter reach, JARVIS, vaulting fallen logs, Plasma's beast in doorways and the turned view.
    /// </summary>
    public class BeamGasAndViewTests
    {
        MatchState saved;

        [SetUp]
        public void SaveState() => saved = MatchState.Current;

        [TearDown]
        public void RestoreState() => MatchState.Current = saved;

        /// <summary>Fires a beam due east from where the beamer stands and lets it run for <paramref name="firing"/> s after the charge.</summary>
        static void Beam(SimRig rig, SimPlayer by, float firing)
        {
            if (by.Role == Role.Survivor)
            {
                // A survivor's is an item: in hand, fired with a left click.
                int slot = by.Inv.FirstSlotOf(ItemType.HempBeam);
                if (slot < 0)
                {
                    by.Inv.Add(ItemType.HempBeam);
                    slot = by.Inv.FirstSlotOf(ItemType.HempBeam);
                }
                rig.Tap(by.Id, Btn.Primary, 0f, slot + 1);
            }
            else
            {
                by.BeamCharges = Balance.Hunter.Beam.Charges;
                rig.Tap(by.Id, Btn.Beam, 0f);
            }
            rig.Run(Secs(Balance.Hunter.Beam.Windup + firing), p => new InputCmd { Aim = 0f, AimDist = 100f, Item = p.SelSlot + 1 });
        }

        [Test]
        public void EveryBeam_TakesOneHpATick_FromAnyoneItTouches_TeammatesToo()
        {
            // A survivor's beam on another survivor: friendly fire, 1 HP a tick.
            var rig = new SimRig(2, 1);
            SimPlayer s = rig.P(2), mate = rig.P(3), zach = rig.P(1);
            rig.Place(s, 0f, 0f);
            rig.Place(mate, 150f, 0f);
            rig.Place(zach, 0f, 2500f);
            Beam(rig, s, 0.5f);
            float lost = 1f - mate.Hp;
            Assert.That(lost, Is.InRange(0.03f, 0.07f), "about five ticks of 1 HP in half a second");

            // Zach's beam on a survivor: the same 1 HP a tick (no longer 3).
            rig = new SimRig(1, 1);
            s = rig.P(2);
            zach = rig.P(1);
            rig.Place(zach, 0f, 0f);
            rig.Place(s, 150f, 0f);
            Beam(rig, zach, 0.5f);
            Assert.That(1f - s.Hp, Is.InRange(0.03f, 0.07f), "Zach's beam: 1 HP a tick");

            // A survivor's beam on Zach: 1 HP a tick of his 100.
            rig = new SimRig(1, 1);
            s = rig.P(2);
            zach = rig.P(1);
            rig.Place(s, 0f, 0f);
            rig.Place(zach, 150f, 0f);
            float before = zach.Hp;
            Beam(rig, s, 0.5f);
            Assert.That(before - zach.Hp, Is.InRange(0.03f, 0.07f), "on Zach too");
        }

        [Test]
        public void TheBeam_ShattersGlass_AndBlowsOpenADoor_OnItsFifthTickOnEach()
        {
            var rig = new SimRig(1, 1, false, 1, m =>
            {
                m.Windows.Add(new SimMap.WindowDef { A = U(200, -60), B = U(200, 60) });
                m.Doors.Add(new SimMap.DoorDef { A = U(400, -60), B = U(400, 60) });
            });
            // Sight stops at the closed door.
            rig.Geo.Sight = (from, dir, max) =>
            {
                if (rig.Sim.Doors[0] || rig.Sim.DoorBroken[0]) return max;
                float t = MatchSim.RaySegment(from, dir, rig.Map.Doors[0].A, rig.Map.Doors[0].B);
                return Mathf.Min(max, t);
            };
            SimPlayer s = rig.P(2);
            rig.Place(s, 0f, 0f);
            rig.Place(rig.P(1), 0f, 2500f);
            Assert.AreEqual(5, Balance.Hunter.Beam.BarrierTicks);
            Beam(rig, s, 0.3f);
            Assert.IsFalse(rig.Sim.WindowsBroken[0], "three ticks: the glass holds (and stops the beam)");
            Assert.IsFalse(rig.Sim.DoorBroken[0], "the door behind it untouched");
            Keep(rig, 0.25f);
            Assert.IsTrue(rig.Sim.WindowsBroken[0], "the fifth tick: the glass goes");
            Assert.IsFalse(rig.Sim.DoorBroken[0], "the door only just reached");
            Keep(rig, 0.6f);
            Assert.IsTrue(rig.Sim.DoorBroken[0], "and five ticks on the door: it goes");
            Assert.IsTrue(rig.Sim.Doors[0]);
        }

        /// <summary>Keeps the beam going (aimed the same way) a while longer.</summary>
        static void Keep(SimRig rig, float seconds) =>
            rig.Run(Secs(seconds), p => new InputCmd { Aim = 0f, AimDist = 100f, Item = p.SelSlot + 1 });

        [Test]
        public void TheBeam_DestroysADoorStandingOpen_AndAPallet_InFiveTicks_UnlockedGatesAfterAWholeCharge_NeverLockedOnes()
        {
            // A door standing open, its panel swung out across the beam.
            var rig = new SimRig(1, 1, false, 1, m => m.Doors.Add(new SimMap.DoorDef { A = U(200, 10), B = U(200, 90), StartsOpen = true }));
            SimPlayer s = rig.P(2);
            rig.Place(s, 0f, 0f);
            rig.Place(rig.P(1), 0f, 2500f);
            Assert.IsTrue(rig.Sim.Doors[0], "standing open");
            Beam(rig, s, 0.25f);
            Assert.IsFalse(rig.Sim.DoorBroken[0], "a couple of ticks: it holds");
            Keep(rig, 0.4f);
            Assert.IsTrue(rig.Sim.DoorBroken[0], "five: the open door burned through");
            // A pallet standing in the way.
            rig = new SimRig(1, 1, false, 1, m => m.Barricades.Add(new SimMap.BarricadeDef { A = U(250, -60), B = U(250, 60), Pos = U(250, 0) }));
            s = rig.P(2);
            rig.Place(s, 0f, 0f);
            rig.Place(rig.P(1), 0f, 2500f);
            Beam(rig, s, 0.25f);
            Assert.AreNotEqual(BarricadeState.Broken, rig.Sim.Barricades[0], "a couple of ticks: the pallet holds");
            Keep(rig, 0.4f);
            Assert.AreEqual(BarricadeState.Broken, rig.Sim.Barricades[0], "five: the pallet destroyed");
            // The graveyard's gates hold.
            rig = new SimRig(1, 1, false, 1, m => m.Doors.Add(new SimMap.DoorDef { A = U(300, -60), B = U(300, 60), Gate = true }));
            rig.Geo.Sight = (from, dir, max) => rig.Sim.Doors[0] ? max : Mathf.Min(max, MatchSim.RaySegment(from, dir, rig.Map.Doors[0].A, rig.Map.Doors[0].B));
            s = rig.P(2);
            rig.Place(s, 0f, 0f);
            rig.Place(rig.P(1), 0f, 2500f);
            Beam(rig, s, 2.3f);
            Assert.IsFalse(rig.Sim.DoorBroken[0], "never the graveyard's gates while they're locked");
            // Unlocked and shut again (the machete could break them then): a whole charge of the beam held on them breaks them.
            rig.Sim.DoorLocked[0] = false;
            rig.Run(Secs(Balance.Hunter.Beam.Cooldown + 3f));
            Beam(rig, s, Balance.Sexton.Defense.BeamTime * 0.5f);
            Assert.IsFalse(rig.Sim.DoorBroken[0], "half a charge on them: they hold");
            rig.Run(Secs(Balance.Sexton.Defense.BeamTime * 0.5f + 0.2f), p => new InputCmd { Aim = 0f, AimDist = 100f, Item = p.SelSlot + 1 });
            Assert.IsTrue(rig.Sim.DoorBroken[0], "the whole charge: they give");
        }

        [Test]
        public void TheBeam_StopsDeadOnAShutGraveyardGate_LockedOrNot_EvenThroughItsBars()
        {
            // Bars the eye sees through (sight passes the gate): the beam still stops on them.
            var rig = new SimRig(1, 1, false, 1, m => m.Doors.Add(new SimMap.DoorDef { A = U(300, -60), B = U(300, 60), Gate = true }));
            SimPlayer s = rig.P(2), zach = rig.P(1);
            rig.Place(s, 0f, 0f);
            rig.Place(zach, 500f, 0f);
            float hp = zach.Hp;
            Beam(rig, s, 1.2f);
            Assert.AreEqual(0, s.BeamGate, "burning into the gate");
            Assert.AreEqual(300f, Scale.ToUnits(s.BeamLen), 12f, "it ends on the bars");
            Assert.AreEqual(hp, zach.Hp, 1e-6f, "nobody behind the gate is touched");
            Assert.IsFalse(rig.Sim.DoorBroken[0]);
            // Open, it passes.
            rig.Run(Secs(Balance.Sexton.Defense.BeamTime + Balance.Hunter.Beam.Cooldown + 1f));
            rig.Sim.DoorLocked[0] = false;
            rig.Sim.SetDoor(0, true);
            Beam(rig, s, 1.2f);
            Assert.AreEqual(-1, s.BeamGate);
            Assert.Less(zach.Hp, hp, "through the open gateway");
        }

        [Test]
        public void Penjamin_GoesStraightThroughTheShield_AndReaches20PercentFurtherThanItWas()
        {
            var rig = new SimRig(1, 1);
            SimPlayer s = rig.P(2);
            s.Shield = 0.5f;
            rig.Sim.HurtSurvivor(s, 0.1f, null, "vape");
            Assert.AreEqual(0.5f, s.Shield, 1e-5f, "the shield isn't touched");
            Assert.AreEqual(0.9f, s.Hp, 1e-5f, "health takes it all");
            rig.Sim.HurtSurvivor(s, 0.1f, null, "slash");
            Assert.AreEqual(0.4f, s.Shield, 1e-5f, "anything else still hits the shield first");
            Assert.AreEqual(1.1f * 0.7f * 1.2f * 1.1f * 1.15f, Balance.Hunter.Vape.ReachMul, 1e-5f, "30% less than the original's, then 20%, 10% and 15% more");
            Assert.AreEqual(10f * 0.8f * 0.6f * 0.7f * 1.5f, Balance.Hunter.Vape.HalfAngleDeg, 1e-5f, "20% narrower than the original's 10 deg, then 40%, then 30% narrower again, then 50% wider");
            Assert.AreEqual(0.3f, Balance.Items.Trap.ZachConeCut, 1e-5f, "galaxy gas narrows Zach's cone by 30%");
        }

        [Test]
        public void Thomas_GivesHisBeamToZachToo_AndTestingSurvivorsCarryOne()
        {
            var rig = new SimRig(1, 1);
            SimPlayer zach = rig.P(1);
            var thomas = new Thomas(rig.Sim) { Pos = U(0, 0) };
            rig.Sim.Npcs.Add(thomas);
            rig.Place(zach, 50f, 0f);
            rig.Run(1);
            rig.Tap(zach.Id, Btn.Interact);
            Assert.AreEqual(Balance.Hunter.Beam.Charges, zach.BeamCharges, "Zach can be the one he gives it to");

            var test = new SimRig(1, 1, true);
            Assert.GreaterOrEqual(test.P(2).Inv.FirstSlotOf(ItemType.HempBeam), 0, "testing mode: a survivor has the beam to try, as an item");
        }

        [Test]
        public void Jarvis_LightsEverySurvivorsScreen_ForItsTwentySeconds()
        {
            var rig = new SimRig(1, 1, true);
            SimPlayer s = rig.P(2);
            rig.Tap(s.Id, Btn.Ability);
            Assert.AreEqual(Balance.Sexton.JarvisUserSec, rig.Sim.RevealT, 0.1f, "the reveal runs");
            rig.Run(Secs(Balance.Sexton.JarvisUserSec + 0.2f));
            Assert.AreEqual(0f, rig.Sim.RevealT);
            Assert.Greater(Vision.Rendering.VisionMaskRenderer.RevealLight, 0.5f, "above the line that shows people");
        }

        [Test]
        public void AnyoneVaultsAFallenLog_AtTheClimbingPace()
        {
            var rig = new SimRig(1, 1);
            rig.Geo.Lows.Add((U(100, -80), U(100, 80), Scale.D(10f)));
            SimPlayer s = rig.P(2), zach = rig.P(1);
            rig.Place(s, 0f, 0f);
            rig.Place(zach, 0f, -300f);
            bool climbed = false;
            for (int i = 0; i < Secs(3f); i++)
            {
                rig.Run(1, p => p.Id == s.Id ? new InputCmd { Move = Vector2.right, Aim = 0f, AimDist = 100f } : (InputCmd?)null);
                climbed |= s.Move.Climbing;
            }
            Assert.IsTrue(climbed, "over the log");
            Assert.Greater(Scale.ToUnits(s.Pos.x), 140f, "and on past it");

            rig.Place(zach, 0f, 0f);
            climbed = false;
            for (int i = 0; i < Secs(3f); i++)
            {
                rig.Run(1, p => p.Id == zach.Id ? new InputCmd { Move = Vector2.right, Aim = 0f, AimDist = 100f } : (InputCmd?)null);
                climbed |= zach.Move.Climbing;
            }
            Assert.IsTrue(climbed, "Zach vaults it too");
        }

        [Test]
        public void PenjaminsCloud_IsSeenTwoSecondsLonger_ButActsNoLonger()
        {
            var rig = new SimRig(1, 1);
            rig.Sim.SpawnVape(U(0, 0), 0f, Scale.D(600f), rig.P(1).Id, false);
            float life = Balance.Hunter.Vape.GrowTime + Balance.Hunter.Vape.LingerTime + Balance.Hunter.Vape.FadeTime;
            rig.Run(Secs(life + 0.5f));
            Assert.AreEqual(1, rig.Sim.Vapes.Count, "still seen thinning away");
            SimPlayer s = rig.P(2);
            rig.Place(s, 150f, 0f);
            s.VapeT = 0f;
            rig.Run(Secs(0.5f));
            Assert.AreEqual(0f, s.VapeT, 1e-5f, "but it no longer gasses anyone");
            rig.Run(Secs(Balance.Hunter.Vape.SeenExtra));
            Assert.AreEqual(0, rig.Sim.Vapes.Count, "gone 2 s after it stops acting");
        }

        [Test]
        public void PlasmasBeast_FitsThroughDoors_ButStaysAsBigToHit()
        {
            var rig = new SimRig(1, 1);
            var plasma = new Plasma(rig.Sim);
            Assert.AreEqual(Balance.Plasma.Radius, plasma.MoveRadiusUnits, "moves like a man (through doorways)");
            Assert.LessOrEqual(Balance.Plasma.Radius, Balance.SurvivorRadius, "no wider than a survivor, who fits a door");
        }

        [Test]
        public void ZachsSpeedAndHempRecovery_AreContinuous_AndShownToTheHundredth()
        {
            // Speed lost to wounds: continuous with health (no steps), 10% per quarter of the bar.
            Assert.AreEqual(1f - 0.1f * (0.137f / 0.25f), MatchRules.HunterHealthMul(1f - 0.137f, 0), 1e-5f);
            Assert.Less(MatchRules.HunterHealthMul(0.9999f, 0), 1f, "the smallest scratch slows him a little");
            // Recovery: 1 + 10% of the charge left, every fraction of it.
            Assert.AreEqual(1.1f, MatchRules.HempRegenMul(1f), 1e-6f);
            Assert.AreEqual(1.0437f, MatchRules.HempRegenMul(0.437f), 1e-6f);
            var z = new SimPlayer(1, "Z", Role.Hunter, Vector2.zero) { Hp = 0.863f, Hemp = 1, HempLeft = Balance.Hunter.Hemp.Duration * 0.4371f };
            string text = GameHud.ZachRates(z);
            StringAssert.Contains("HP 86.3/100   SPEED -5.48% FROM HEALTH", text);
            StringAssert.Contains("RECOVERY x1.0437 (HEMP 43.71%)", text);
        }

        [Test]
        public void RespawningTheNpcs_BringsThemAllBack_AndClearsWhatTheyLeft()
        {
            var rig = new SimRig(1, 1, true);
            rig.Sim.NpcSpawner = NpcRoster.Spawn;
            rig.Sim.RespawnNpcs();
            int all = rig.Sim.Npcs.Count;
            Assert.AreEqual(NpcRoster.Makers.Length, all);
            rig.Sim.Npcs.RemoveAt(0);
            rig.Sim.DropHempBeam(Vector2.zero);
            rig.Sim.Vengeance = 2;
            rig.Sim.RespawnNpcs();
            Assert.AreEqual(all, rig.Sim.Npcs.Count, "everyone back");
            Assert.IsFalse(rig.Sim.Drops.Exists(d => d.Item == ItemType.HempBeam), "Sexton's dropped beam gone");
            Assert.AreEqual(0, rig.Sim.Vengeance);
        }

        [Test]
        public void TheTurnedView_MovesUpTheScreen()
        {
            // W (up the screen) with the view turned 45 degrees to the right heads north-east.
            Vector2 w = TopDownCamera.ScreenToGround(Vector2.up, 45f);
            Assert.AreEqual(Mathf.Sqrt(0.5f), w.x, 1e-4f);
            Assert.AreEqual(Mathf.Sqrt(0.5f), w.y, 1e-4f);
            Vector2 d = TopDownCamera.ScreenToGround(Vector2.right, 90f);
            Assert.AreEqual(0f, d.x, 1e-4f);
            Assert.AreEqual(-1f, d.y, 1e-4f, "with the view turned to face east, right on screen is south");
            Assert.AreEqual(Vector2.up, TopDownCamera.ScreenToGround(Vector2.up, 0f), "unturned, up is north");
            Assert.AreEqual(45f, TopDownCamera.TurnStep);
        }
    }
}
