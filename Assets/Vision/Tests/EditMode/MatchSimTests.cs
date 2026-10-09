using System;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Player;

namespace Vision.Tests
{
    /// <summary>
    /// Drives a match the way the game does: each tick every player's input goes in, each player moves themselves (client-owned
    /// movement) and reports their pose, then the rules advance. Open ground, no walls.
    /// </summary>
    sealed class SimRig
    {
        public readonly MatchSim Sim;
        public readonly OpenGeometry Geo = new OpenGeometry();
        public readonly SimMap Map = new SimMap();

        public static int Secs(float s) => Mathf.CeilToInt(s * Balance.Net.TickHz);
        public static Vector2 U(float x, float y) => Scale.D(new Vector2(x, y));

        /// <param name="survivors">Survivors get ids after the hunters (hunters are 1..H).</param>
        public SimRig(int survivors = 1, int hunters = 1, bool testMode = false, int seed = 1)
        {
            Map.HalfExtent = 90f;
            foreach (Vector2 g in new[] { U(600, 0), U(-600, 0), U(0, 600), U(0, -600), U(900, 900) }) Map.Generators.Add(g);
            Map.Stakes.Add(U(-900, -900));
            Map.Stakes.Add(U(-900, 900));
            Map.HasGate = true;
            Map.Lever = U(0, 1200);
            Map.GatePos = U(0, 1260);
            Geo.ExitZone = new Rect(U(-200, 1400), U(400, 400));
            Map.ExitZone = Geo.ExitZone;
            for (int i = 0; i < 10; i++) Map.SurvivorSpawns.Add(U(-300 + i * 60, -300));
            for (int i = 0; i < 9; i++) Map.HunterSpawns.Add(U(1500, -1500 + i * 60));
            Sim = new MatchSim(Map, Geo, MatchRules.Resolve(survivors, hunters), testMode, seed);
            for (int i = 0; i < hunters; i++) Sim.AddPlayer(1 + i, "Zach " + (i + 1), Role.Hunter);
            for (int i = 0; i < survivors; i++) Sim.AddPlayer(1 + hunters + i, "Survivor " + (i + 1), Role.Survivor);
        }

        public SimPlayer P(int id) => Sim.Get(id);

        /// <summary>Puts a player at a point given in original units.</summary>
        public void Place(SimPlayer p, float x, float y) => Sim.Teleport(p.Id, U(x, y));

        /// <summary>Runs whole ticks; <paramref name="input"/> gives each player's frame (none: standing still).</summary>
        public void Run(int ticks, Func<SimPlayer, InputCmd?> input = null)
        {
            for (int t = 0; t < ticks; t++)
            {
                foreach (SimPlayer p in Sim.Order.ToArray())
                {
                    InputCmd cmd = input?.Invoke(p) ?? new InputCmd { Aim = p.Facing, AimDist = 100f, Item = p.SelSlot + 1 };
                    Sim.SubmitInput(p.Id, cmd);
                    Vector2 delta = Movement.Step(p.Move, p.Pos, cmd, Sim.MoveContextFor(p, false), Geo, MatchSim.TickDt, out Gait gait);
                    Sim.SetPose(p.Id, p.Pos + delta, cmd.AimDist > 0f ? cmd.Aim : p.Facing, gait);
                }
                Sim.Step();
                Sim.Events.Clear();
            }
        }

        /// <summary>One tick with a button pressed by one player (others idle).</summary>
        public void Tap(int id, Btn b, float aim = 0f, int item = 0)
        {
            Run(1, p => p.Id == id ? new InputCmd { Buttons = b, Aim = aim, AimDist = 100f, Item = item > 0 ? item : p.SelSlot + 1 } : (InputCmd?)null);
        }

        /// <summary>Holds a button for some ticks (one player; others idle).</summary>
        public void Hold(int id, Btn b, int ticks, float aim = 0f, int item = 0)
        {
            Run(ticks, p => p.Id == id ? new InputCmd { Buttons = b, Aim = aim, AimDist = 100f, Item = item > 0 ? item : p.SelSlot + 1 } : (InputCmd?)null);
        }

        /// <summary>A text fingerprint of the match (positions to 1 mm, health, objectives) for determinism checks.</summary>
        public string Hash()
        {
            var sb = new StringBuilder();
            sb.Append(Sim.Tick).Append('|');
            foreach (SimPlayer p in Sim.Order)
                sb.Append(p.Id).Append(':').Append(Mathf.RoundToInt(p.Pos.x * 1000f)).Append(',').Append(Mathf.RoundToInt(p.Pos.y * 1000f))
                  .Append(',').Append(Mathf.RoundToInt(p.Hp * 1000f)).Append(',').Append((int)p.Health).Append(';');
            foreach (GenState g in Sim.Gens) sb.Append(Mathf.RoundToInt(g.Progress * 1000f)).Append(',');
            return sb.ToString();
        }
    }

    public class MatchSimTests
    {
        MatchState saved;

        [SetUp]
        public void SetUp()
        {
            saved = MatchState.Current;
            MatchState.Current = new MatchState();
        }

        [TearDown]
        public void TearDown() => MatchState.Current = saved;

        static int Secs(float s) => SimRig.Secs(s);

        // ---------------------------------------------------------------- balance and rules

        [Test]
        public void Resolve_ScalesTheMatchToThePlayers_AsTheOriginal()
        {
            ResolvedBalance four = MatchRules.Resolve(4, 1);
            Assert.AreEqual(2, four.EscapeNeeded, "half the survivors must escape");
            Assert.That(four.RepairTime, Is.InRange(70f * 0.75f, 70f * 1.35f));
            Assert.That(four.RequiredGenerators, Is.InRange(Balance.Scaling.RequiredGenMin, Balance.Scaling.RequiredGenMax));
            Assert.AreEqual(1, MatchRules.Resolve(1, 1).EscapeNeeded);
            Assert.GreaterOrEqual(MatchRules.Resolve(8, 1).RequiredGenerators, four.RequiredGenerators, "more survivors, more generators");
            Assert.AreEqual(1f, MatchRules.HunterHealthMul(1f, 0), 1e-5f);
            Assert.AreEqual(0.9f, MatchRules.HunterHealthMul(0.75f, 0), 1e-4f, "10% slower per quarter of his bar");
            Assert.AreEqual(1.1f, MatchRules.HunterStakeMul(2), 1e-4f, "5% faster per staking");
        }

        [Test]
        public void CheckWin_HasNoTimer_OnlyTheOriginalsTwoRules()
        {
            Assert.AreEqual(Winner.None, MatchRules.CheckWin(4, 1, 1, 0, 1, 0, 2, out _), "someone is still standing");
            Assert.AreEqual(Winner.Survivors, MatchRules.CheckWin(4, 0, 2, 1, 1, 0, 2, out _), "two of four out: survivors");
            Assert.AreEqual(Winner.Hunters, MatchRules.CheckWin(4, 0, 1, 2, 1, 0, 2, out string reason), "only one out: Zach");
            StringAssert.Contains("1 escaped", reason);
            Assert.AreEqual(Winner.Survivors, MatchRules.CheckWin(4, 3, 0, 0, 1, 1, 2, out _), "the hunters left");

            var rig = new SimRig(1, 1);
            rig.Run(Secs(16f * 60f));
            Assert.IsNull(rig.Sim.Result, "sixteen minutes in, nobody has won: there is no clock");
        }

        // ---------------------------------------------------------------- movement and the pace

        [Test]
        public void Movement_UsesTheOriginalsSpeeds_AtTheChosenPace()
        {
            float Walked(float pace, Btn buttons, float seconds = 1f)
            {
                MatchState.Current.Pace = pace;
                var rig = new SimRig(1, 1);
                SimPlayer s = rig.P(2);
                Vector2 start = s.Pos;
                rig.Run(Secs(seconds), p => p.Id == 2 ? new InputCmd { Move = Vector2.right, Buttons = buttons, Aim = 0f, AimDist = 100f } : (InputCmd?)null);
                return (s.Pos - start).magnitude / seconds;
            }
            float walk = Walked(1f, Btn.None);
            Assert.AreEqual(Scale.D(Balance.Survivor.Walk), walk, Scale.D(Balance.Survivor.Walk) * 0.04f, "the original's walk (144 u/s)");
            Assert.AreEqual(Scale.D(Balance.Survivor.Run), Walked(1f, Btn.Run), Scale.D(Balance.Survivor.Run) * 0.06f, "and run (228 u/s)");
            Assert.AreEqual(walk * 0.5f, Walked(0.5f, Btn.None), walk * 0.03f, "the pace scales every speed");
            float human = Scale.D(Balance.Survivor.Walk) * Scale.HumanPace * Vision.World.WorldScale.S;
            Assert.AreEqual(3.2f, human, 0.01f, "the slider's low end is this game's earlier 3.2 m/s walk");
            Assert.That(Scale.HumanPace, Is.InRange(Scale.MinPace, Scale.MaxPace));
        }

        [Test]
        public void Sprinting_RunsTheMeterDry_ThenLocksUntilItRefills()
        {
            var rig = new SimRig(1, 1);
            SimPlayer s = rig.P(2);
            rig.Run(Secs(Balance.Survivor.StaminaMax + 0.1f), p => p.Id == 2 ? new InputCmd { Move = Vector2.right, Buttons = Btn.Run, AimDist = 100f } : (InputCmd?)null);
            Assert.Less(s.Move.Stamina, 0.15f, "eight seconds of sprint");
            Assert.Greater(s.Move.StaminaLock, 0f, "then a lockout");
            Vector2 at = s.Pos;
            rig.Run(30, p => p.Id == 2 ? new InputCmd { Move = Vector2.right, Buttons = Btn.Run, AimDist = 100f } : (InputCmd?)null);
            Assert.AreEqual(Scale.D(Balance.Survivor.Walk), (s.Pos - at).magnitude, Scale.D(Balance.Survivor.Walk) * 0.06f, "dry: walking pace");
            rig.Run(Secs(Balance.SprintLockout + Balance.Survivor.StaminaMax / Balance.Survivor.StaminaRefill * 10f + 1f));
            Assert.Greater(s.Move.Stamina, Balance.Survivor.StaminaMax * 0.5f, "standing still refills it");
        }

        // ---------------------------------------------------------------- the machete

        static SimRig Duel(float dist, int survivors = 1)
        {
            var rig = new SimRig(survivors, 1);
            SimPlayer h = rig.P(1), s = rig.P(2);
            rig.Place(h, 0f, 0f);
            rig.Place(s, dist, 0f);
            for (int i = 3; i <= survivors + 1; i++) rig.Place(rig.P(i), 2500f, 2500f - i * 40f);
            h.Facing = 0f;
            return rig;
        }

        [Test]
        public void Swipes_TakeAThirdEach_ThreeDownThem_WithASpeedBurst()
        {
            SimRig rig = Duel(60f, 2);
            SimPlayer h = rig.P(1), s = rig.P(2);
            rig.Tap(1, Btn.Primary);
            rig.Run(10);
            Assert.AreEqual(Health.Wounded, s.Health);
            Assert.AreEqual(2f / 3f, s.Hp, 1e-3f);
            Assert.Greater(s.Move.HasteT, 0f, "a burst of speed");
            foreach (float left in new[] { 1f / 3f, 0f })
            {
                rig.Run(Secs(Balance.Hunter.Attack.SwingTime) + 2);
                rig.Place(s, Scale.ToUnits(h.Pos.x) + 60f, Scale.ToUnits(h.Pos.y));
                rig.Tap(1, Btn.Primary);
                rig.Run(10);
                Assert.AreEqual(left, s.Hp, 1e-3f);
            }
            Assert.AreEqual(Health.Downed, s.Health);
            Assert.AreEqual(MoveMode.Crawl, rig.Sim.MoveModeFor(s));
            Assert.AreEqual(1, h.Stats.Downs);
        }

        [Test]
        public void Swipe_Reaches124Units_InFront_NotBehind()
        {
            SimRig rig = Duel(115f);
            rig.Tap(1, Btn.Primary);
            rig.Run(10);
            Assert.AreEqual(Health.Wounded, rig.P(2).Health, "115 u in front: a hit");

            rig = Duel(-60f);
            rig.Tap(1, Btn.Primary);
            rig.Run(10);
            Assert.AreEqual(Health.Healthy, rig.P(2).Health, "behind him: a miss");
            Assert.AreEqual(0, rig.P(1).Stats.Hits);

            rig = Duel(170f);
            rig.Tap(1, Btn.Primary);
            rig.Run(10);
            Assert.AreEqual(Health.Healthy, rig.P(2).Health, "out of reach");
        }

        [Test]
        public void HoldingTheSwing_ChargesAHeavyHit_ThatTakesTwoThirds()
        {
            SimRig rig = Duel(150f);
            SimPlayer h = rig.P(1), s = rig.P(2);
            rig.Hold(1, Btn.Primary, Secs(Balance.Hunter.Attack.ChargeMax) + 1);
            Assert.GreaterOrEqual(h.ChargeT, Balance.Hunter.Attack.HeavyAt);
            Assert.AreEqual(Health.Healthy, s.Health, "nothing until he lets go");
            rig.Run(10);
            Assert.AreEqual(-1f, h.ChargeT);
            Assert.AreEqual(Health.Wounded, s.Health, "the heavy swipe reaches 150 u");
            Assert.AreEqual(1f / 3f, s.Hp, 1e-3f);
            Assert.AreEqual(1f / 3f, MatchSim.SwipeDamage(0f), 1e-5f);
            Assert.AreEqual(2f / 3f, MatchSim.SwipeDamage(Balance.Hunter.Attack.ChargeMax), 1e-5f);
        }

        [Test]
        public void Downed_IsCarriedToAStake_AndTheFirstStageTimesOutIntoElimination()
        {
            SimRig rig = Duel(45f, 3);
            SimPlayer h = rig.P(1), s = rig.P(2);
            s.Hp = 0f;
            s.Health = Health.Downed;
            rig.Run(2);
            Assert.AreEqual(Prompt.PickUp, h.Prompt);
            rig.Tap(1, Btn.Interact);
            rig.Run(Secs(Balance.Hunter.PickupTime) + 2);
            Assert.AreEqual(Health.Carried, s.Health);
            Assert.AreEqual(s.Id, h.Carrying);
            Vector2 stake = rig.Map.Stakes[0];
            rig.Place(h, Scale.ToUnits(stake.x) + 30f, Scale.ToUnits(stake.y));
            rig.Run(2);
            rig.Tap(1, Btn.Interact);
            rig.Run(Secs(Balance.Hunter.StakeTime) + 2);
            Assert.AreEqual(Health.Staked, s.Health);
            Assert.AreEqual(1, s.StakeStage);
            Assert.AreEqual(s.Id, rig.Sim.Stakes[0]);
            rig.Run(Secs(Balance.Objectives.StakeStageTime) + 5);
            Assert.AreEqual(Health.Eliminated, s.Health);
            Assert.AreEqual(0, rig.Sim.Stakes[0]);
            Assert.AreEqual(1, h.Stats.Eliminations);
        }

        // ---------------------------------------------------------------- objectives

        [Test]
        public void Generators_TakeTheRepairTime_AndHelpersSpeedThemUp()
        {
            var rig = new SimRig(2, 1);
            SimPlayer a = rig.P(2), b = rig.P(3);
            rig.Place(rig.P(1), 2500f, -2500f);
            rig.Place(a, 640f, 0f);
            rig.Place(b, 2500f, 2500f);
            float t = rig.Sim.Bal.RepairTime;
            rig.Hold(2, Btn.Interact, Secs(t * 0.5f));
            Assert.AreEqual(0.5f, rig.Sim.Gens[0].Progress, 0.02f, "halfway after half the repair time");
            Assert.AreEqual(ActionKind.Repair, a.Action);
            rig.Place(b, 560f, 0f);
            rig.Run(Secs(t * 0.2f), p => new InputCmd { Buttons = Btn.Interact, AimDist = 100f });
            Assert.AreEqual(0.5f + 0.2f * (1f + Balance.Objectives.CoopStep), rig.Sim.Gens[0].Progress, 0.02f, "two at once: +25%");
            rig.Run(Secs(t * 0.4f), p => new InputCmd { Buttons = Btn.Interact, AimDist = 100f });
            Assert.IsTrue(rig.Sim.Gens[0].Repaired);
            Assert.AreEqual(ActionKind.None, a.Action, "done: they let go");
        }

        [Test]
        public void ThePoweredGate_Opens_AndWalkingOutEscapes_AndWins()
        {
            var rig = new SimRig(1, 1);
            SimPlayer s = rig.P(2);
            rig.Place(rig.P(1), 2500f, -2500f);
            rig.Place(s, 30f, 1200f);
            rig.Run(2);
            Assert.AreEqual(Prompt.GatePowerless, s.Prompt, "no power yet");
            foreach (GenState g in rig.Sim.Gens) { g.Progress = 1f; g.Repaired = true; }
            rig.Run(2);
            Assert.IsTrue(rig.Sim.Gate.Powered);
            Assert.AreEqual(Prompt.OpenGate, s.Prompt);
            rig.Hold(2, Btn.Interact, Secs(Balance.Objectives.GateOpenTime) + 5);
            Assert.IsTrue(rig.Sim.Gate.Open, "twenty seconds on the lever");
            rig.Place(s, 0f, 1600f);
            rig.Run(3);
            Assert.AreEqual(Health.Escaped, s.Health);
            Assert.NotNull(rig.Sim.Result);
            Assert.AreEqual(Winner.Survivors, rig.Sim.Result.Winner, "the only survivor got out");
        }

        // ---------------------------------------------------------------- items

        [Test]
        public void Supplies_HealAsTheOriginal_AndAreKeptWhenFull()
        {
            var rig = new SimRig(2, 1);
            SimPlayer s = rig.P(2);
            rig.Place(rig.P(1), 2500f, -2500f);
            s.Inv.Add(ItemType.Confit);
            s.Inv.Add(ItemType.MrBeastBar, 2);
            s.Inv.Add(ItemType.MiniShield, 2);
            rig.Tap(2, Btn.Primary, 0f, 1);
            Assert.AreEqual(1, s.Inv.Count(ItemType.Confit), "confit at full health is kept");
            s.Hp = 0.3f;
            s.Health = Health.Wounded;
            rig.Run(Secs(Balance.Items.UseSlowTime) + 1);
            rig.Tap(2, Btn.Primary, 0f, 2);
            Assert.AreEqual(0.5f, s.Hp, 1e-4f, "Mr Beast bar: +20%");
            rig.Run(Secs(Balance.Items.UseSlowTime) + 1);
            rig.Tap(2, Btn.Primary, 0f, 1);
            Assert.AreEqual(1f, s.Hp, 1e-4f, "confit: full");
            Assert.AreEqual(Health.Healthy, s.Health);
            Assert.AreEqual(0, s.Inv.Count(ItemType.Confit));

            int shield = s.Inv.FirstSlotOf(ItemType.MiniShield) + 1;
            rig.Run(Secs(Balance.Items.UseSlowTime) + 1);
            rig.Tap(2, Btn.Primary, 0f, shield);
            Assert.AreEqual(ActionKind.Drink, s.Action);
            rig.Hold(2, Btn.None, Secs(Balance.Items.ShieldDrinkTime) + 2, 0f, shield);
            Assert.AreEqual(Balance.Items.ShieldAmount, s.Shield, 1e-4f, "+25% shield after two seconds");
            Assert.AreEqual(1, s.Inv.Count(ItemType.MiniShield));
            rig.Sim.HurtSurvivor(s, 0.3f, rig.P(1), "slash");
            Assert.AreEqual(0f, s.Shield, 1e-4f, "the shield goes first");
            Assert.AreEqual(0.95f, s.Hp, 1e-4f, "then health");
        }

        [Test]
        public void TestingMode_GivesTheWholeKit_NeverUsedUp_AndTSwitchesRoles()
        {
            var rig = new SimRig(1, 1, testMode: true);
            SimPlayer s = rig.P(2);
            Assert.IsTrue(s.Inv.Infinite);
            Assert.AreEqual(Inventory.TestingSlots, s.Inv.Limit, "twelve slots");
            Assert.AreEqual(9, s.Inv.Count(ItemType.Bottle));
            Assert.AreEqual(1, s.Inv.Count(ItemType.Sniper));
            Assert.IsTrue(s.Inv.GoldenAt(1), "the golden pump");
            s.Hp = 0.4f;
            s.Health = Health.Wounded;
            int bar = s.Inv.FirstSlotOf(ItemType.MrBeastBar) + 1;
            for (int k = 0; k < 4; k++)
            {
                rig.Tap(2, Btn.Primary, 0f, bar);
                rig.Run(Secs(Balance.Items.UseSlowTime) + 1, p => new InputCmd { Item = bar, AimDist = 100f });
            }
            Assert.AreEqual(1, s.Inv.Count(ItemType.MrBeastBar), "never used up");
            Assert.AreEqual(1f, s.Hp, 1e-4f);
            s.Hp = 0f;
            s.Health = Health.Downed;
            rig.Run(Secs(30f));
            Assert.IsNull(rig.Sim.Result, "no win check in testing");

            Assert.IsTrue(rig.Sim.SwitchRole(2));
            Assert.AreEqual(Role.Hunter, rig.P(2).Role, "T: now Zach");
            Assert.AreEqual(2, rig.P(2).Hemp, "with his whole kit");
            Assert.IsTrue(rig.Sim.SwitchRole(2));
            Assert.AreEqual(Role.Survivor, rig.P(2).Role);
            Assert.AreEqual(Health.Healthy, rig.P(2).Health, "a fresh survivor");
            Assert.IsFalse(new SimRig(1, 1).Sim.SwitchRole(2), "only in testing mode");
            Assert.AreEqual(6f, GameSession.SpeedMultiplier, "speed mode: +500%");
        }

        [Test]
        public void Inventory_EightSlots_StacksAndWeaponsAlone_OverflowDrops()
        {
            var inv = new Inventory();
            Assert.AreEqual(8, inv.Limit);
            inv.Add(ItemType.Bottle, 20);
            Assert.AreEqual(20, inv.CountAt(0), "one stack");
            inv.Add(ItemType.Shotgun, 2);
            Assert.AreEqual(ItemType.Shotgun, inv.ItemAt(1));
            Assert.AreEqual(ItemType.Shotgun, inv.ItemAt(2), "each gun takes a slot");
            Assert.AreEqual(Balance.Items.Shotgun.Shells, inv.AmountAt(1), 1e-4f, "loaded");
            foreach (ItemType t in new[] { ItemType.Book, ItemType.Goggles, ItemType.Confit, ItemType.MrBeastBar, ItemType.Trap }) inv.Add(t);
            ItemType? dropped = null;
            inv.Dropped += (t, n, g, a) => dropped = t;
            inv.Add(ItemType.MiniShield);
            Assert.AreEqual(ItemType.Trap, dropped, "full: the last slot's item is dropped for the new one");
            Assert.AreEqual(ItemType.MiniShield, inv.ItemAt(7));
            inv.Add(ItemType.MrBeastBar, 3);
            Assert.AreEqual(4, inv.Count(ItemType.MrBeastBar), "a stack always takes more");
            Assert.IsTrue(inv.Remove(0));
            Assert.AreEqual(19, inv.Count(ItemType.Bottle));
            inv.Infinite = true;
            Assert.IsTrue(inv.Remove(0));
            Assert.AreEqual(19, inv.Count(ItemType.Bottle), "testing never runs out");
            int total = 0;
            foreach (ItemType t in Items.All) total += Items.Info(t).mapCount;
            Assert.AreEqual(96, total, "the original's 96 supplies on the map");
        }

        // ---------------------------------------------------------------- determinism

        [Test]
        public void SameSeedAndInputs_GiveTheSameMatch()
        {
            string Play(int seed)
            {
                var rig = new SimRig(3, 1, false, seed);
                var rng = new System.Random(5);
                rig.Run(600, p =>
                {
                    var c = new InputCmd { Move = new Vector2((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f).normalized, Aim = (float)rng.NextDouble() * 6.28f, AimDist = 200f };
                    if (rng.NextDouble() < 0.3) c.Buttons |= Btn.Run;
                    if (p.Role == Role.Hunter && rng.NextDouble() < 0.05) c.Buttons |= Btn.Primary;
                    return c;
                });
                return rig.Hash();
            }
            Assert.AreEqual(Play(11), Play(11));
        }
    }
}
