using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Net;
using Vision.Player;
using Vision.UI;

namespace Vision.Tests
{
    /// <summary>
    /// The lobby's balance sliders (their defaults, ranges and what each one changes in a match), the new numbers, the
    /// 0.50 cal's rounds count, Zach's bar worn down by downs and grown by stakes, the two exit gates, the end game and the
    /// hatch, no time limit, and the NPC changes (Plasma's one pump a side, the clips for his rage and for attacks on
    /// Chacko, only Chacko's four avengers set off by his death, each taking a third, Jaden alerted only up close, Chris
    /// Zelley). Ghosts have their own tests (GhostsAndPossessionTests).
    /// </summary>
    public class SlidersGhostsAndExitsTests
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
        static Tuning Tune => MatchState.Current.Tuning;

        static T Add<T>(SimRig rig, T n, float x, float y) where T : Npc
        {
            n.Pos = SimRig.U(x, y);
            rig.Sim.Npcs.Add(n);
            return n;
        }

        /// <summary>Runs ticks keeping every event raised (the rig clears them).</summary>
        static List<GameEvent> RunKeeping(SimRig rig, int ticks, System.Func<SimPlayer, InputCmd?> input = null)
        {
            var kept = new List<GameEvent>();
            for (int t = 0; t < ticks; t++)
            {
                foreach (SimPlayer p in rig.Sim.Order.ToArray())
                {
                    InputCmd cmd = input?.Invoke(p) ?? new InputCmd { Aim = p.Facing, AimDist = 100f, Item = p.SelSlot + 1 };
                    rig.Sim.SubmitInput(p.Id, cmd);
                    Vector2 delta = Movement.Step(p.Move, p.Pos, cmd, rig.Sim.MoveContextFor(p, false), rig.Geo, MatchSim.TickDt, out Gait gait);
                    rig.Sim.SetPose(p.Id, p.Pos + delta, cmd.AimDist > 0f ? cmd.Aim : p.Facing, gait);
                }
                rig.Sim.Step();
                kept.AddRange(rig.Sim.Events);
                rig.Sim.Events.Clear();
            }
            return kept;
        }

        // ---------------------------------------------------------------- the lobby's sliders

        [Test]
        public void TheSliders_DefaultToTheGamesOwnNumbers_SnapIntoRange_AndCrossTheWire()
        {
            var t = new Tuning();
            Assert.IsTrue(t.IsDefault);
            Assert.AreEqual(17, Tuning.Knobs.Length, "generators, two speeds, two sprints, two recoveries, two sides' damage, four guns' damage, four guns' rounds");
            Assert.AreEqual(5, t.Generators);
            Assert.AreEqual(Balance.Survivor.StaminaMax, t.SurvivorSprint);
            Assert.AreEqual(Balance.Hunter.StaminaMax, t.ZachSprint);
            Assert.AreEqual(Balance.Survivor.StaminaRefill, t.SurvivorRecover);
            Assert.AreEqual(Balance.Hunter.StaminaRefill, t.ZachRecover);
            Assert.AreEqual(1f, t.SurvivorSpeed);
            Assert.AreEqual(1f, t.ZachDamage);
            Assert.AreEqual(6, t.ShotgunAmmo);
            Assert.AreEqual(12, t.GoldenAmmo, "golden pumps hold twelve shells");
            Assert.AreEqual(10, t.PistolAmmo);
            Assert.AreEqual(5, t.SniperAmmo, "the 0.50 cal: five rounds");
            foreach (Tuning.Knob k in Tuning.Knobs)
                Assert.That(k.Default, Is.InRange(k.Min, k.Max), $"{k.Label}'s default within its range");

            t.SetKnob(0, 0f);
            Assert.AreEqual(2, t.Generators, "never fewer than two generators");
            t.SetKnob(0, 99f);
            Assert.AreEqual(Tuning.MaxGenerators, t.Generators);
            t.SetKnob(1, 1.234f);
            Assert.AreEqual(1.25f, t.SurvivorSpeed, 1e-4f, "onto the slider's step");
            t.SetKnob(1, float.NaN);
            Assert.AreEqual(1f, t.SurvivorSpeed, "nonsense goes back to the default");
            t.SetKnob(16, 3.4f);
            Assert.AreEqual(3, t.SniperAmmo, "whole rounds");
            t.SetKnob(99, 1f);
            Assert.IsFalse(t.IsDefault);

            // On the wire with the lobby's settings.
            var s = new LobbySettings { Hunters = 2, Survivors = 5, Seed = "abc", Tuning = t };
            var ms = new MemoryStream();
            s.Write(new BinaryWriter(ms));
            LobbySettings back = LobbySettings.Read(new BinaryReader(new MemoryStream(ms.ToArray())));
            for (int i = 0; i < Tuning.Knobs.Length; i++) Assert.AreEqual(Tuning.Knobs[i].Get(t), Tuning.Knobs[i].Get(back.Tuning), 1e-5f, Tuning.Knobs[i].Label);
            Assert.AreEqual("abc", back.Seed);
            Assert.GreaterOrEqual(Wire.Version, 2, "a new wire version: the old builds can't join and get told so");
        }

        [Test]
        public void TheSliders_SetEachSidesSpeedSprintAndRecovery()
        {
            Tune.SurvivorSpeed = 1.5f;
            Tune.ZachSpeed = 0.5f;
            Tune.SurvivorSprint = 4f;
            Tune.SurvivorRecover = 2f;
            var walk = new InputCmd { Move = Vector2.right, Aim = 0f, AimDist = 100f };
            var ctxS = new MoveContext { Role = Role.Survivor, HunterSpeedMul = 1f };
            var ctxZ = new MoveContext { Role = Role.Hunter, HunterSpeedMul = 1f };
            Vector2 ds = Movement.Step(new MoveState(Role.Survivor), Vector2.zero, walk, ctxS, null, 0.1f, out _);
            Assert.AreEqual(Scale.Speed(Balance.Survivor.Walk) * 1.5f * 0.1f, ds.magnitude, 1e-4f, "survivors 50% faster");
            Vector2 dz = Movement.Step(new MoveState(Role.Hunter), Vector2.zero, walk, ctxZ, null, 0.1f, out _);
            Assert.AreEqual(Scale.Speed(Balance.Hunter.Walk) * 0.5f * 0.1f, dz.magnitude, 1e-4f, "Zach at half speed");

            var m = new MoveState(Role.Survivor);
            Assert.AreEqual(4f, m.Stamina, 1e-4f, "a four-second sprint meter");
            var sprint = new InputCmd { Move = Vector2.right, Buttons = Btn.Run, Aim = 0f, AimDist = 100f };
            float t = 0f;
            while (m.Stamina > 0f && t < 10f) { Movement.Step(m, Vector2.zero, sprint, ctxS, null, 0.05f, out _); t += 0.05f; }
            Assert.AreEqual(4f, t, 0.11f, "it runs dry in four seconds");
            // Empty to full in the two seconds of recovery (it fills even while the sprint is locked out).
            var idle = new InputCmd { Aim = 0f, AimDist = 100f };
            Movement.Step(m, Vector2.zero, idle, ctxS, null, 1f, out _);
            Assert.AreEqual(2f, m.Stamina, 0.05f, "half the meter in a second");
            Movement.Step(m, Vector2.zero, idle, ctxS, null, 1f, out _);
            Assert.AreEqual(4f, m.Stamina, 0.05f, "full in two");
        }

        [Test]
        public void TheSliders_ScaleEachSidesDamage_EachGunsDamage_AndItsRounds()
        {
            var rig = new SimRig(2, 1);
            SimPlayer zach = rig.P(1), s = rig.P(2);
            Tune.ZachDamage = 2f;
            rig.Sim.HurtSurvivor(s, 0.1f, zach, "slash");
            Assert.AreEqual(0.8f, s.Hp, 1e-4f, "Zach's damage doubled");
            Tune.SurvivorDamage = 0.5f;
            rig.Sim.HurtHunter(zach, 20f, s, "bullet");
            Assert.AreEqual(0.9f, zach.Hp, 1e-4f, "a survivor's halved");
            rig.Sim.HurtSurvivor(rig.P(3), 0.2f, null, "punch");
            Assert.AreEqual(0.8f, rig.P(3).Hp, 1e-4f, "an NPC's untouched");

            // The P250's slider on top of the side's.
            zach.Hp = 1f;
            Tune.SurvivorDamage = 1f;
            Tune.PistolDamage = 2f;
            rig.Place(s, 0f, 0f);
            rig.Place(zach, 80f, 0f);
            s.Inv.Clear();
            s.Inv.Add(ItemType.Pistol);
            rig.Run(2);
            // (A straight shot: its bloom is only random up to 7.5 degrees, and Zach's wide at this range.)
            rig.Tap(s.Id, Btn.Primary, 0f, 1);
            Assert.AreEqual(1f - 2f * Balance.Items.Pistol.ZachDamage / Balance.Hunter.Health.Max, zach.Hp, 1e-3f, "twice the P250's 10");

            // Starting rounds.
            Tune.SniperAmmo = 7;
            Tune.GoldenAmmo = 20;
            Assert.AreEqual(7f, Items.FreshAmount(ItemType.Sniper));
            Assert.AreEqual(20f, Items.FreshAmount(ItemType.Shotgun, true));
            var inv = new Inventory();
            inv.Add(ItemType.Sniper);
            Assert.AreEqual("7/7", inv.RoundsAt(0));
        }

        [Test]
        public void TheGeneratorSlider_SetsHowManyThereAre_OneInTheBuildingUnderFive_AlwaysTheGraveyards()
        {
            var plain = new World.MapLayout(1337, 90f);
            var five = new World.MapLayout(1337, 90f, 5);
            CollectionAssert.AreEqual(plain.WoodsGenerators, five.WoodsGenerators, "five is the map as it always was");
            for (int n = 2; n <= Tuning.MaxGenerators; n++)
            {
                var L = new World.MapLayout(1337, 90f, n);
                int building = L.Plan.Rooms.Count(r => r.HasGenerator);
                Assert.AreEqual(n >= 5 ? 2 : 1, building, $"{n}: generators in the building");
                Assert.IsTrue(L.WoodsGenerators.Any(g => (g - L.Graveyard.center).magnitude < 0.01f), $"{n}: always the graveyard's");
                int total = building + L.WoodsGenerators.Count;
                if (n <= 6) Assert.AreEqual(n, total, $"{n} generators in all");
                else Assert.That(total, Is.InRange(6, n), "as many as the woods have clearings for");
            }
            var two = new World.MapLayout(1337, 90f, 2);
            Assert.AreEqual(1, two.WoodsGenerators.Count, "two: the building's and the graveyard's");
        }

        // ---------------------------------------------------------------- the new numbers

        [Test]
        public void TheNewNumbers_AndTheHempBatteryRechargingOverFiftySeconds()
        {
            Assert.AreEqual(50f, Balance.Hunter.Hemp.Recover);
            Assert.AreEqual(0.45f * 0.6f, Balance.Hunter.Vape.Slow, 1e-5f, "Penjamin's strongest slow 40% less");
            Assert.AreEqual(5, Balance.Hunter.Beam.Charges);
            Assert.AreEqual(5f, Items.FreshAmount(ItemType.HempBeam));
            Assert.AreEqual(12, Balance.Items.Golden.Shells);
            Assert.AreEqual(5, Balance.Items.Sniper.Shots);
            Assert.AreEqual(10f, Balance.Objectives.GateOpenTime, "half the original's 20 s");
            Assert.AreEqual(0.1f, Balance.Hunter.Attack.DamageBase, 1e-5f, "an uncharged swing: 10");

            var rig = new SimRig(1, 1);
            SimPlayer h = rig.P(1);
            h.Hemp = 1;
            h.HempLeft = 0f;
            rig.Run(Secs(25f));
            Assert.AreEqual(Balance.Hunter.Hemp.Duration * 0.5f, h.HempLeft, 0.15f, "half charged in 25 s");
            rig.Run(Secs(25.2f));
            Assert.AreEqual(Balance.Hunter.Hemp.Duration, h.HempLeft, 1e-3f, "full in 50");
        }

        [Test]
        public void TheFiftyCal_Shows5of5_AnotherMakesIt10of10_AndAnEmptyGunIsGone()
        {
            var inv = new Inventory();
            inv.Add(ItemType.Sniper);
            Assert.AreEqual("5/5", inv.RoundsAt(0));
            inv.SetAmountAt(0, 4f);
            Assert.AreEqual("4/5", inv.RoundsAt(0));
            inv.Add(ItemType.Sniper);
            Assert.AreEqual(1, inv.Count(ItemType.Sniper), "into the one carried");
            Assert.AreEqual("9/10", inv.RoundsAt(0), "another's rounds, and room for five more");
            inv.Add(ItemType.Pistol);
            Assert.AreEqual("10/10", inv.RoundsAt(1), "every gun shows its rounds");
            inv.Add(ItemType.Bottle);
            Assert.IsNull(inv.RoundsAt(2), "a bottle doesn't");

            // Fired dry, a gun is gone.
            var rig = new SimRig(1, 1);
            SimPlayer s = rig.P(2);
            rig.Place(s, 0f, 0f);
            rig.Place(rig.P(1), 2000f, 2000f);
            s.Inv.Clear();
            s.Inv.Add(ItemType.Pistol, 1, 1f);
            s.Inv.Add(ItemType.Sniper, 1, 1f);
            rig.Tap(s.Id, Btn.Primary, Mathf.PI, 1);
            Assert.AreEqual(0, s.Inv.Count(ItemType.Pistol), "the P250's last round: gone");
            rig.Run(Secs(1f));
            int slot = s.Inv.FirstSlotOf(ItemType.Sniper) + 1;
            rig.Tap(s.Id, Btn.Primary, Mathf.PI, slot);
            Assert.AreEqual(0, s.Inv.Count(ItemType.Sniper), "the 0.50 cal's: gone");
        }

        // ---------------------------------------------------------------- Zach's bar

        [Test]
        public void DowningZach_WearsTenOffHisBar_HeGetsUpWithHalf_StakesAddFive_WornAwayHesDead()
        {
            var rig = new SimRig(2, 1);
            SimPlayer zach = rig.P(1), s = rig.P(2), s2 = rig.P(3);
            rig.Place(zach, 1500f, 1500f);
            rig.Sim.DownHunter(zach, true);
            Assert.AreEqual(0.9f, zach.MaxHp, 1e-4f, "10 off the top for good");
            rig.Run(Secs(Balance.Hunter.Health.DownTime + 0.1f));
            Assert.AreEqual(0.45f, zach.Hp, 0.003f, "up with half of what's left");
            float slowed = MatchRules.HunterHealthMul(zach.Hp, zach.Downs);
            Assert.Less(slowed, MatchRules.HunterHealthMul(0.5f, zach.Downs), "his speed still goes by his health out of the original 100");
            rig.Run(Secs(Balance.Hunter.Health.RegenTime));
            Assert.AreEqual(0.9f, zach.Hp, 1e-3f, "he heals only to the top of what's left");

            // A stake: 5 more on the bar, and in it.
            s.Health = Health.Carried;
            s.CarriedBy = zach.Id;
            zach.Carrying = s.Id;
            rig.Sim.StakeSurvivor(zach, s, 0);
            Assert.AreEqual(0.95f, zach.MaxHp, 1e-4f);
            Assert.AreEqual(0.95f, zach.Hp, 1e-3f);
            Assert.Greater(MatchRules.HunterHealthMul(1.05f, 0), 1f, "past the original 100 he's faster");

            // Worn away to nothing: dead, and the survivors win.
            zach.MaxHp = 0.1f;
            rig.Sim.DownHunter(zach, true);
            Assert.IsTrue(zach.Killed);
            Assert.AreEqual(Health.Eliminated, zach.Health);
            rig.Run(1);
            Assert.NotNull(rig.Sim.Result);
            Assert.AreEqual(Winner.Survivors, rig.Sim.Result.Winner);
            Assert.AreEqual("Zach was killed", rig.Sim.Result.Reason);
            Assert.AreEqual(Health.Healthy, s2.Health);
        }

        // ---------------------------------------------------------------- pictures

        [Test]
        public void TheGrapesOfWrathsPictures_AreNeverTheSameTwiceRunning()
        {
            var rig = new SimRig(1, 1);
            int prev = -1;
            for (int i = 0; i < 60; i++)
            {
                int b = rig.Sim.NextBookImage();
                Assert.AreNotEqual(prev, b, "the Grapes of Wrath's pictures never twice running");
                prev = b;
            }
        }

        // ---------------------------------------------------------------- the exits

        static SimRig TwoGates(int survivors)
        {
            return new SimRig(survivors, 1, false, 3, m =>
            {
                m.HasGate2 = true;
                m.Lever2 = SimRig.U(-1200f, 0f);
                m.GatePos2 = SimRig.U(-1260f, 0f);
                m.ExitZone2 = new Rect(SimRig.U(-1700f, -200f), SimRig.U(400f, 400f));
                m.HasHatch = true;
                m.Hatch = SimRig.U(-800f, -800f);
            });
        }

        [Test]
        public void TwoGates_EachOpensAtItsOwnLeverInTenSeconds_AndTheEndGameTakesWhoeverIsLeft()
        {
            SimRig rig = TwoGates(3);
            SimPlayer zach = rig.P(1), a = rig.P(2), b = rig.P(3), c = rig.P(4);
            rig.Place(zach, 1500f, -1500f);
            foreach (GenState g in rig.Sim.Gens) { g.Progress = 1f; g.Repaired = true; }
            rig.Run(1);
            Assert.IsTrue(rig.Sim.Gate.Powered && rig.Sim.Gate2.Powered, "every generator running powers both gates");
            rig.Sim.Teleport(a.Id, rig.Map.Lever2 + SimRig.U(20f, 0f));
            rig.Run(1);
            Assert.AreEqual(Prompt.OpenGate, a.Prompt);
            Assert.AreEqual(1, a.PromptTarget, "Miga Sushi's gate");
            rig.Hold(a.Id, Btn.Interact, Secs(9f));
            Assert.IsFalse(rig.Sim.Gate2.Open, "not yet at nine seconds");
            rig.Hold(a.Id, Btn.Interact, Secs(1.3f));
            Assert.IsTrue(rig.Sim.Gate2.Open, "open in ten");
            Assert.IsFalse(rig.Sim.Gate.Open, "the other stays shut");
            Assert.IsTrue(rig.Sim.EndGame, "the end game has begun");
            Assert.AreEqual(Balance.Objectives.EndGameTime, rig.Sim.EndGameT, 1.5f);

            // Out through the open one; the shut one's yard does nothing.
            rig.Sim.Teleport(a.Id, rig.Map.ExitZone2.center);
            rig.Sim.Teleport(b.Id, rig.Map.ExitZone.center);
            rig.Run(1);
            Assert.AreEqual(Health.Escaped, a.Health);
            Assert.AreNotEqual(Health.Escaped, b.Health, "that gate isn't open");

            // The end game runs out: whoever's left is taken.
            rig.Run(Secs(Balance.Objectives.EndGameTime));
            Assert.AreEqual(Health.Eliminated, b.Health);
            Assert.AreEqual(Health.Eliminated, c.Health);
            Assert.IsFalse(b.Ghost || c.Ghost, "the Entity's taken them: no ghost");
            Assert.NotNull(rig.Sim.Result);
        }

        [Test]
        public void TheNight_HasNoTimeLimit()
        {
            var rig = new SimRig(2, 1);
            rig.Place(rig.P(1), 1500f, -1500f);
            rig.Run(Secs(20f * 60f));
            Assert.IsNull(rig.Sim.Result, "twenty minutes and nobody's done anything: the night goes on");
            Assert.IsFalse(rig.Sim.EndGame);
        }

        [Test]
        public void TheHatch_OpensForTheLastSurvivor_AnInstantEscape_UnlessZachSlamsItShut()
        {
            SimRig rig = TwoGates(2);
            SimPlayer zach = rig.P(1), a = rig.P(2), b = rig.P(3);
            rig.Place(zach, 1500f, -1500f);
            rig.Run(1);
            Assert.IsFalse(rig.Sim.HatchOpen, "shut while two are left");
            rig.Sim.Eliminate(b, "disconnected");
            rig.Run(1);
            Assert.IsTrue(rig.Sim.HatchOpen, "one left: the hatch opens");
            rig.Sim.Teleport(a.Id, rig.Map.Hatch + SimRig.U(10f, 0f));
            rig.Run(1);
            Assert.AreEqual(Health.Escaped, a.Health, "jumped in");

            // Zach gets there first.
            SimRig rig2 = TwoGates(2);
            SimPlayer z2 = rig2.P(1), a2 = rig2.P(2);
            rig2.Place(a2, 1200f, 1200f);
            rig2.Sim.Eliminate(rig2.P(3), "disconnected");
            rig2.Sim.Teleport(z2.Id, rig2.Map.Hatch + SimRig.U(40f, 0f));
            rig2.Run(2);
            Assert.IsTrue(rig2.Sim.HatchOpen);
            Assert.AreEqual(Prompt.CloseHatch, z2.Prompt);
            rig2.Tap(z2.Id, Btn.Interact);
            Assert.IsFalse(rig2.Sim.HatchOpen);
            Assert.IsTrue(rig2.Sim.HatchClosed);
            Assert.IsTrue(rig2.Sim.Gate.Powered && rig2.Sim.Gate2.Powered, "slamming it powers the gates");
            Assert.IsTrue(rig2.Sim.EndGame, "and starts the end game");
            rig2.Sim.Teleport(a2.Id, rig2.Map.Hatch);
            rig2.Run(1);
            Assert.AreNotEqual(Health.Escaped, a2.Health, "shut is shut");

            // A night with one survivor from the start never opens it.
            SimRig solo = TwoGates(1);
            solo.Run(Secs(2f));
            Assert.IsFalse(solo.Sim.HatchOpen);
        }

        // ---------------------------------------------------------------- the NPCs

        [Test]
        public void Plasma_GivesOneGoldenPumpToEachSide_ThenHesAllOut()
        {
            var rig = new SimRig(2, 2);
            SimPlayer z1 = rig.P(1), z2 = rig.P(2), s1 = rig.P(3), s2 = rig.P(4);
            var plasma = Add(rig, new Plasma(rig.Sim), 0f, 0f);
            void TalkTo(SimPlayer p)
            {
                foreach (SimPlayer q in rig.Sim.Order) rig.Sim.Teleport(q.Id, SimRig.U(1500f + q.Id * 100f, 1500f));
                rig.Sim.Teleport(p.Id, plasma.Pos + SimRig.U(40f, 0f));
                plasma.Talk(rig.Sim, p);
                rig.Run(Secs(2f));
            }
            TalkTo(s1);
            Assert.AreEqual(1, s1.Inv.Count(ItemType.Shotgun));
            Assert.IsTrue(s1.Inv.GoldenAt(s1.Inv.FirstSlotOf(ItemType.Shotgun)));
            Assert.AreEqual("12/12", s1.Inv.RoundsAt(s1.Inv.FirstSlotOf(ItemType.Shotgun)), "twelve shells");
            TalkTo(s2);
            Assert.AreEqual(0, s2.Inv.Count(ItemType.Shotgun), "the survivors' one is gone");
            Assert.AreEqual(Plasma.LineAllOut, plasma.Say ?? Plasma.LineAllOut);
            TalkTo(z1);
            Assert.AreEqual(12, z1.Pump, "Zach's own");
            TalkTo(z2);
            Assert.AreEqual(0, z2.Pump, "and that's Zach's side's one gone too");
            Assert.IsFalse(plasma.HasPumpFor(Role.Hunter) || plasma.HasPumpFor(Role.Survivor));
        }

        [Test]
        public void GamerRage_AndAttacksOnChacko_SetOffTheirClips_AndZachKillingChackoSetsTheAvengersOnHim()
        {
            var rig = new SimRig(1, 1);
            SimPlayer zach = rig.P(1), s = rig.P(2);
            var plasma = Add(rig, new Plasma(rig.Sim), 0f, 0f);
            rig.Place(s, 60f, 0f);
            plasma.ItemHit(rig.Sim, s, "bottle");
            Assert.IsTrue(rig.Sim.Events.Any(e => e.Kind == EventKind.Talk && e.Text == "gamer_rage"), "the gamer smash");
            rig.Sim.Events.Clear();

            var chacko = new Chacko(rig.Sim);
            rig.Sim.Npcs.Add(chacko);
            var njaaron = Add(rig, new Njaaron(rig.Sim), 1500f, 1500f);
            var jaden = Add(rig, new Jaden(rig.Sim), -1500f, 1500f);
            var shane = Add(rig, new Shane(rig.Sim), 1500f, -1500f);
            rig.Sim.Teleport(zach.Id, chacko.Pos + SimRig.U(40f, 0f));
            chacko.Slash(rig.Sim, zach, 1);
            Assert.IsFalse(chacko.Alive);
            Assert.IsTrue(rig.Sim.Events.Any(e => e.Kind == EventKind.Talk && e.Text == "chacko_attacked"), "the clip for anyone near");
            foreach (Npc n in new Npc[] { njaaron, jaden, shane })
                Assert.AreEqual(zach.Id, n.GrudgeTarget, $"{n.Name} goes after Zach for it");
            Assert.IsTrue(shane.Chasing && shane.Target == zach.Id, "Shane runs to him");
            shane.Slash(rig.Sim, zach, 1);
            Assert.IsFalse(shane.Chasing, "Zach's machete throws Shane off");
            Assert.AreEqual(0, shane.GrudgeTarget);
        }

        [Test]
        public void Njaaron_RacesAfterChackosAttacker_FromAnywhere_AndStopsOnceHesTakenAThird()
        {
            var rig = new SimRig(2, 1);
            SimPlayer s = rig.P(2);
            rig.Place(rig.P(1), 1500f, -1500f);
            rig.Place(rig.P(3), -1500f, -1500f);
            var chacko = new Chacko(rig.Sim);
            rig.Sim.Npcs.Add(chacko);
            rig.Sim.Teleport(s.Id, chacko.Pos + SimRig.U(80f, 0f));
            var njaaron = Add(rig, new Njaaron(rig.Sim), 1600f, 1600f);
            chacko.ItemHit(rig.Sim, s, "bottle");
            Assert.IsFalse(chacko.Alive, "a survivor can slay him now");
            float afterBlast = s.Hp;
            Assert.AreEqual(s.Id, njaaron.GrudgeTarget);
            for (int t = 0; t < Secs(60f) && njaaron.GrudgeTarget != 0; t++) rig.Run(1);
            Assert.AreEqual(0, njaaron.GrudgeTarget, "done with them");
            Assert.GreaterOrEqual(afterBlast - s.Hp, 1f / 3f - 1e-3f, "a third of their health taken");
            Assert.Less(afterBlast - s.Hp, 1f / 3f + Balance.Njaaron.SurvivorPunch, "and no more");
            Assert.AreNotEqual(Health.Downed, s.Health);
        }

        [Test]
        public void Chris_WaitsWhileZachCarriesTheOneHeWasRunningTo_OrGoesToSomeoneElse()
        {
            var rig = new SimRig(3, 1);
            SimPlayer zach = rig.P(1), a = rig.P(2), b = rig.P(3);
            rig.Place(zach, 1500f, -1500f);
            var chris = new Chris(rig.Sim);
            rig.Sim.Npcs.Add(chris);
            chris.Active = true;
            chris.State = Chris.Mode.Wander;
            rig.Sim.Teleport(a.Id, chris.Pos + SimRig.U(900f, 0f));
            rig.Sim.HurtSurvivor(a, 1f, null, "punch");
            rig.Run(Secs(Balance.Chris.DownedAfter + 0.2f));
            Assert.AreEqual(Chris.Mode.Rescue, chris.State);
            Assert.AreEqual(a.Id, chris.Target);
            // Zach picks them up: Chris stops and waits.
            rig.Sim.CarrySurvivor(zach, a);
            rig.Run(3);
            Assert.IsTrue(chris.Waiting, "waits while they're carried");
            Assert.AreEqual(Chris.Mode.Rescue, chris.State);
            Vector2 at = chris.Pos;
            rig.Run(Secs(1f));
            Assert.Less(Vector2.Distance(at, chris.Pos), 0.01f, "standing his ground");
            // Someone else down long enough: he goes to them.
            rig.Sim.Teleport(b.Id, chris.Pos + SimRig.U(-300f, 0f));
            rig.Sim.HurtSurvivor(b, 1f, null, "punch");
            rig.Run(Secs(Balance.Chris.DownedAfter + 0.2f));
            Assert.AreEqual(b.Id, chris.Target, "off to the other one");
            Assert.IsFalse(chris.Waiting);
        }

        [Test]
        public void NightVisionsBeam_WidensGradually_AsItsLightLengthens()
        {
            Assert.AreEqual(0f, PlayerController.GoggleWiden(0f), 1e-5f, "not wider the moment it's on");
            float last = 0f;
            for (float k = 0.02f; k <= 1f; k += 0.02f)
            {
                float w = PlayerController.GoggleWiden(k);
                Assert.GreaterOrEqual(w, last - 1e-6f, "only ever wider as it comes on");
                last = w;
            }
            float atScreen = 1f - Mathf.Sqrt(1f - PlayerController.GoggleScreenShare);
            Assert.AreEqual(1f, PlayerController.GoggleWiden(atScreen + 1e-4f), 1e-4f, "all the way open once the light's past the side of the screen");
            Assert.That(PlayerController.GoggleWiden(atScreen * 0.5f), Is.InRange(0.2f, 0.8f), "partway on the way");
            Assert.Less(atScreen * Balance.Xray.FadeIn, 0.25f, "about as long as the light takes to cross the screen");
        }

        // ---------------------------------------------------------------- the second list

        [Test]
        public void ChackosDeath_SetsOnlyJadenPlasmaNjaaronAndShaneOnHisKiller_NoOtherNpcIsTouched()
        {
            // Zach kills him: the four go after him; nobody else so much as turns a hair.
            var rig = new SimRig(1, 1);
            SimPlayer zach = rig.P(1), s = rig.P(2);
            rig.Place(s, -1500f, -1500f);
            var chacko = new Chacko(rig.Sim);
            rig.Sim.Npcs.Add(chacko);
            rig.Sim.Teleport(zach.Id, chacko.Pos + SimRig.U(40f, 0f));
            var four = new List<Npc> { Add(rig, new Njaaron(rig.Sim), 1500f, 1500f), Add(rig, new Jaden(rig.Sim), -1500f, 1500f), Add(rig, new Plasma(rig.Sim), 1500f, -900f), Add(rig, new Shane(rig.Sim), 1200f, 0f) };
            var others = new List<Npc>
            {
                Add(rig, new Marc(rig.Sim), 900f, 900f), Add(rig, new Waz(rig.Sim), -900f, 900f), Add(rig, new Thomas(rig.Sim), 900f, -900f),
                Add(rig, new Monique(rig.Sim), -900f, -600f), Add(rig, new Daphne(rig.Sim), 600f, 300f), Add(rig, new Soham(rig.Sim), -300f, 900f),
                Add(rig, new Sexton(rig.Sim), 300f, -900f),
            };
            var chris = new Chris(rig.Sim);
            rig.Sim.Npcs.Add(chris);
            others.Add(chris);
            chacko.Slash(rig.Sim, zach, 1);
            Assert.IsFalse(chacko.Alive);
            foreach (Npc n in four) Assert.AreEqual(zach.Id, n.GrudgeTarget, $"{n.Name} goes after Zach");
            foreach (Npc n in others)
            {
                Assert.AreEqual(0, n.GrudgeTarget, $"{n.Name} isn't after anyone");
                Assert.IsFalse(n.Aggressive, $"{n.Name} isn't roused");
                Assert.AreEqual(NpcFlags.None, n.Flags & (NpcFlags.Angry | NpcFlags.Chasing | NpcFlags.Punching), $"{n.Name} neither angry nor chasing");
            }

            // Not in any way: with the four left out, every other NPC does exactly what it would have done had he lived.
            List<Npc> Town(SimRig r) => new List<Npc>
            {
                Add(r, new Marc(r.Sim), 900f, 900f), Add(r, new Waz(r.Sim), -900f, 900f), Add(r, new Thomas(r.Sim), 900f, -900f),
                Add(r, new Monique(r.Sim), -900f, -600f), Add(r, new Daphne(r.Sim), 600f, 300f), Add(r, new Soham(r.Sim), -300f, 900f),
                Add(r, new Sexton(r.Sim), 300f, -900f),
            };
            var killed = new SimRig(1, 1);
            var lived = new SimRig(1, 1);
            Chacko ck = null;
            foreach (SimRig r in new[] { killed, lived })
            {
                r.Place(r.P(2), -1500f, -1500f);
                var c = new Chacko(r.Sim);
                r.Sim.Npcs.Add(c);
                r.Sim.Teleport(1, c.Pos + SimRig.U(40f, 0f));
                if (r == killed) ck = c;
            }
            List<Npc> a = Town(killed), b = Town(lived);
            ck.Slash(killed.Sim, killed.P(1), 1);
            killed.Run(Secs(6f));
            lived.Run(Secs(6f));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(b[i].Pos, a[i].Pos, $"{a[i].Name} goes on just the same");
                Assert.AreEqual(b[i].Flags, a[i].Flags, $"{a[i].Name}'s state the same");
            }

            // A survivor killing him sets the same four on them, and nobody else.
            var rig3 = new SimRig(1, 1);
            SimPlayer s3 = rig3.P(2);
            rig3.Place(rig3.P(1), 1500f, 1500f);
            var chacko3 = new Chacko(rig3.Sim);
            rig3.Sim.Npcs.Add(chacko3);
            var marc = Add(rig3, new Marc(rig3.Sim), 300f, 300f);
            var plasma3 = Add(rig3, new Plasma(rig3.Sim), -300f, 300f);
            chacko3.ItemHit(rig3.Sim, s3, "bottle");
            Assert.AreEqual(s3.Id, plasma3.GrudgeTarget, "Plasma after the survivor");
            Assert.AreEqual(0, marc.GrudgeTarget, "Marc not");
        }

        [Test]
        public void Daphne_FiresFiveBlastsAtMost_ThenGoesPassive()
        {
            var rig = new SimRig(1, 1);
            SimPlayer zach = rig.P(1);
            var daphne = Add(rig, new Daphne(rig.Sim), 0f, 0f);
            rig.Place(zach, 150f, 0f);
            rig.Place(rig.P(2), -2000f, -2000f);
            daphne.Slash(rig.Sim, zach, 1);
            int blasts = 0;
            for (int i = 0; i < Secs(Balance.Monique.AttackSec); i++)
            {
                rig.Sim.Step();
                foreach (GameEvent e in rig.Sim.Events) if (e.Kind == EventKind.Shot && e.A == -daphne.Id) blasts++;
                rig.Sim.Events.Clear();
                zach.KnockT = 0f;
                zach.Hp = 1f;
            }
            Assert.AreEqual(5, blasts, "five, then the gun away");
            Assert.AreEqual(5, Balance.Monique.DaphneShots);
            Assert.IsFalse(daphne.Armed, "passive again");
            daphne.Slash(rig.Sim, zach, 1);
            Assert.IsTrue(daphne.Armed, "another attack starts her again");
        }

        [Test]
        public void Zach_PicksUpAnyGoldenPump_ForTwelveShots()
        {
            var rig = new SimRig(1, 1, false, 1, m => m.Loot.Add(new SimMap.LootDef { Pos = SimRig.U(300f, 0f), Item = ItemType.Shotgun, Golden = true }));
            SimPlayer zach = rig.P(1);
            rig.Place(rig.P(2), -1500f, -1500f);
            rig.Sim.Teleport(zach.Id, rig.Map.Loot[0].Pos + SimRig.U(10f, 0f));
            rig.Run(1);
            Assert.AreEqual(Prompt.Loot, zach.Prompt, "Zach can take a golden pump lying on the map");
            rig.Tap(zach.Id, Btn.Interact);
            Assert.AreEqual(12, zach.Pump, "twelve shots");
            Assert.IsTrue(rig.Sim.LootTaken[0]);
            // One a survivor dropped.
            var d = new DropItem { Id = rig.Sim.AllocEntityId(), Pos = zach.Pos + SimRig.U(20f, 0f), Item = ItemType.Shotgun, Golden = true, Amount = 7f };
            rig.Sim.Drops.Add(d);
            rig.Run(1);
            Assert.AreEqual(Prompt.PickDrop, zach.Prompt);
            rig.Tap(zach.Id, Btn.Interact);
            Assert.AreEqual(19, zach.Pump, "its shells left on top");
            Assert.AreEqual(0, rig.Sim.Drops.Count);
        }

        [Test]
        public void TheNewSounds_StartWhereTheyShould_DecayAndFadeAsAsked()
        {
            var gen = Audio.SoundBank.Cues["generator_done"];
            CollectionAssert.AreEqual(new[] { "local_generator_start" }, gen.Prefer, "the diesel generator in place of the old whirr");
            Assert.AreEqual(0, gen.Files.Length, "nothing else");
            Assert.AreEqual(1f, Audio.AudioManager.Envelope(gen, 0f, 60f), 1e-4f);
            Assert.AreEqual(0.6f, Audio.AudioManager.Envelope(gen, 3.5f, 60f), 1e-3f, "falling off linearly");
            Assert.AreEqual(0.2f, Audio.AudioManager.Envelope(gen, 7f, 60f), 1e-4f, "to a fifth over seven seconds");
            Assert.AreEqual(0.2f, Audio.AudioManager.Envelope(gen, 40f, 60f), 1e-4f, "then held to the end");
            Assert.AreEqual(3.048f / 0.03f, gen.Radius, 0.01f, "heard within ten feet");
            var voice = Audio.SoundBank.Cues["jarvis_voice"];
            Assert.AreEqual(1f, Audio.AudioManager.Envelope(voice, 5f, 10f), 1e-4f);
            Assert.AreEqual(1f / (5.5f * 5.5f), Audio.AudioManager.Envelope(voice, 9.5f, 10f), 1e-4f, "its last second by an inverse square");
            Assert.Less(Audio.AudioManager.Envelope(voice, 10f, 10f), 0.011f);
            Assert.AreEqual(1.8f, Audio.SoundBank.Cues["gas"].Start, "the gas explosion from 1.8 s in");
            CollectionAssert.Contains(Audio.SoundBank.Cues["gas"].Prefer, "local_gas_explosion");
            Assert.AreEqual(0.225f, Audio.SoundBank.Cues["slash"].Volume, 1e-4f, "the slash half as loud, and half again");
        }

        [Test]
        public void JarvisBoot_CountsOneTwoThree()
        {
            Assert.AreEqual("", UI.JarvisHud.BootTitle(0f));
            Assert.AreEqual("INITIATING J.A.R.V.I.S.", UI.JarvisHud.BootTitle(0.68f));
            StringAssert.EndsWith("1...", UI.JarvisHud.BootTitle(0.95f));
            StringAssert.EndsWith("1... 2...", UI.JarvisHud.BootTitle(1.3f));
            Assert.AreEqual("INITIATING J.A.R.V.I.S. 1... 2... 3...", UI.JarvisHud.BootTitle(1.7f));
            Assert.AreEqual("INITIATING J.A.R.V.I.S. 1... 2... 3...", UI.JarvisHud.BootTitle(UI.JarvisHud.BootTime), "all three before the boot ends");
        }
    }
}
