using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Audio;
using Vision.Game;
using Vision.Net;
using Vision.Player;

namespace Vision.Tests
{
    /// <summary>
    /// Ghosts and the bodies they take (the user's rules): a sacrificed survivor's body left where they died and their
    /// ghost back in Miga Sushi or the Diddy Chamber; the Ghost Pulse gone; who sees a ghost; taking a body (E), its rise
    /// and the merge; walking in it as a survivor does; lending a living survivor a hand with a generator or a gate (and
    /// nothing else); the Soundcloud Burst and Penjamin reaching it without harm; every one of Zach's blows knocking the
    /// ghost out of it for good, to see everything; testing mode's T through a ghost and its generators starting at once;
    /// the lock's last click firing nothing; the lock 10% harder; the 0.50 cal's testing effect; the sound changes.
    /// </summary>
    public class GhostsAndPossessionTests
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

        static readonly Vector2 Sushi = SimRig.U(-1200f, 1200f), Chamber = SimRig.U(1200f, 1200f);

        static SimRig Rig(int survivors = 2, bool test = false, int seed = 1) =>
            new SimRig(survivors, 1, test, seed, m => { m.GhostSpawns.Add(Sushi); m.GhostSpawns.Add(Chamber); });

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

        static InputCmd Press(Btn b, float aim = 0f) => new InputCmd { Buttons = b, Aim = aim, AimDist = 100f };

        /// <summary>A survivor sacrificed on a stake where they stand, and back as a ghost.</summary>
        static Body Sacrifice(SimRig rig, SimPlayer s, SimPlayer zach)
        {
            rig.Sim.Eliminate(s, "stake", zach);
            Body b = rig.Sim.Bodies.Single(x => !x.OfNpc && x.Of == s.Id);
            rig.Run(Secs(Balance.Ghost.RespawnDelay + 0.1f));
            Assert.IsTrue(s.FreeGhost, "back as a ghost");
            return b;
        }

        /// <summary>A ghost beside a body takes it (E), and the merge runs through.</summary>
        static void TakeBody(SimRig rig, SimPlayer g, Body b)
        {
            rig.Sim.Teleport(g.Id, b.Pos + SimRig.U(30f, 0f));
            rig.Run(2);
            Assert.AreEqual(Prompt.Possess, g.Prompt, "a body in reach");
            rig.Tap(g.Id, Btn.Interact);
            Assert.AreEqual(b.Id, g.Body, "taken");
            rig.Run(Secs(Balance.Ghost.PossessTime + 0.1f));
            Assert.IsTrue(MatchSim.InBody(g), "one with it");
        }

        /// <summary>A rig with Zach (1) far off, a ghost in its own body (2) and a living survivor (3).</summary>
        static SimRig Reanimated(out SimPlayer zach, out SimPlayer g, out SimPlayer s, out Body body)
        {
            SimRig rig = Rig(2);
            zach = rig.P(1);
            g = rig.P(2);
            s = rig.P(3);
            rig.Place(zach, 1500f, -1500f);
            rig.Place(s, -600f, 600f);
            rig.Place(g, 300f, 300f);
            body = Sacrifice(rig, g, zach);
            TakeBody(rig, g, body);
            return rig;
        }

        // ---------------------------------------------------------------- the ghost

        [Test]
        public void ASacrifice_LeavesTheBody_AndTheGhostComesBack_InMigaSushiOrTheDiddyChamber()
        {
            var rig = Rig(2);
            SimPlayer zach = rig.P(1), s = rig.P(2);
            rig.Place(zach, 1500f, -1500f);
            rig.Place(s, 300f, 300f);
            Vector2 where = s.Pos;
            rig.Sim.Eliminate(s, "stake", zach);
            Body b = rig.Sim.Bodies.Single();
            Assert.AreEqual(where, b.Pos, "the body where they were staked");
            Assert.AreEqual(s.Name, b.Name);
            Assert.AreEqual("Survivor", b.Look);
            Assert.IsFalse(b.OfNpc);
            Assert.AreEqual(0, b.Ghost, "nobody in it");
            Assert.IsFalse(s.Ghost, "not straight away");
            rig.Run(Secs(Balance.Ghost.RespawnDelay + 0.1f));
            Assert.IsTrue(s.Ghost && s.FreeGhost);
            Assert.AreEqual(Health.Eliminated, s.Health, "still counts as sacrificed");
            Assert.IsTrue(s.Pos == Sushi || s.Pos == Chamber, "back in Miga Sushi or the Diddy Chamber");
            Assert.AreSame(s, rig.Sim.ViewerFor(s), "playing as themselves");
            Assert.AreEqual(0, s.Inv.Count(ItemType.Bottle) + s.Inv.Count(ItemType.Shotgun), "no inventory");
            var seen = new HashSet<Vector2>();
            for (int seed = 1; seed < 60 && seen.Count < 2; seed++)
            {
                SimRig r = Rig(2, false, seed);
                r.Sim.Eliminate(r.P(2), "stake", r.P(1));
                seen.Add(r.P(2).GhostSpawn);
            }
            Assert.AreEqual(2, seen.Count, "either place comes up");

            // Out of the woods: the body at the edge of the trees, the ghost in one of the two places.
            var rig2 = Rig(2);
            SimPlayer w = rig2.P(2);
            w.Pos = new Vector2(rig2.Sim.Ring2 + 2f, 0f);
            rig2.Sim.Eliminate(w, "woods");
            Body wb = rig2.Sim.Bodies.Single();
            Assert.Less(wb.Pos.magnitude, rig2.Sim.Ring1, "inside the first ring, where a ghost can reach it");
            rig2.Run(Secs(Balance.Boundary.SacrificedHold + 1.2f));
            Assert.IsTrue(w.Ghost);
            Assert.IsTrue(w.Pos == Sushi || w.Pos == Chamber);

            // A disconnect leaves no body and no ghost.
            var rig3 = Rig(2);
            rig3.Sim.Eliminate(rig3.P(3), "disconnected");
            rig3.Run(Secs(6f));
            Assert.AreEqual(0, rig3.Sim.Bodies.Count);
            Assert.IsFalse(rig3.P(3).Ghost);
        }

        [Test]
        public void AGhost_SprintsForever_PassesThroughAnything_IsSeenByZachNotSurvivors_AndHasNoPulse()
        {
            var rig = Rig(2);
            SimPlayer zach = rig.P(1), g = rig.P(2), s = rig.P(3);
            rig.Place(zach, 1500f, -1500f);
            rig.Place(s, -600f, 600f);
            List<GameEvent> told = null;
            rig.Sim.Eliminate(g, "stake", zach);
            told = RunKeeping(rig, Secs(Balance.Ghost.RespawnDelay + 0.1f));
            Assert.IsTrue(told.Any(e => e.Kind == EventKind.Item && e.Text == "find a body, help your team" && e.To.Contains(g.Id)), "the ghost is told");

            MoveContext ctx = rig.Sim.MoveContextFor(g, false);
            Assert.IsTrue(ctx.Ghost, "a sprint that never runs out");
            float full = g.Move.Stamina;
            RunKeeping(rig, Secs(12f), p => p.Id == g.Id ? new InputCmd { Move = Vector2.up, Buttons = Btn.Run, Aim = Mathf.PI / 2f, AimDist = 100f } : (InputCmd?)null);
            Assert.AreEqual(full, g.Move.Stamina, 1e-3f);
            // Nothing slows it: no log, fence or window to climb (the avatar passes through walls on the player's own machine).
            var vaulting = new VaultEverywhere();
            var m = new MoveState(Role.Survivor) { Mode = MoveMode.Normal };
            Movement.Step(m, Vector2.zero, new InputCmd { Move = Vector2.up }, ctx, vaulting, MatchSim.TickDt, out _);
            Assert.IsFalse(m.Climbing, "a ghost never climbs");
            Movement.Step(m, Vector2.zero, new InputCmd { Move = Vector2.up }, rig.Sim.MoveContextFor(s, false), vaulting, MatchSim.TickDt, out _);
            Assert.IsTrue(m.Climbing, "a survivor does");

            // Who sees it.
            Assert.IsFalse(MatchSim.SeesGhosts(s), "a survivor never sees a ghost");
            Assert.IsFalse(MatchSim.SeesGhosts(zach), "nor does Zach");
            Assert.IsTrue(MatchSim.SeesGhosts(g), "only the other ghosts do");
            Assert.IsTrue(PlayerController.GhostForm(g));

            // The Ghost Pulse is gone: a click or F does nothing.
            var ev = RunKeeping(rig, 3, p => p.Id == g.Id ? Press(Btn.Primary | Btn.Secondary) : (InputCmd?)null);
            Assert.AreEqual(0, rig.Sim.Bursts.Count, "no pulse goes out");
            Assert.IsFalse(ev.Any(e => e.Kind == EventKind.Scare), "nobody is flashed");
            Assert.IsFalse(SoundBank.Cues.ContainsKey("ghost_pulse"));

            // Nothing hurts it; no generator for a ghost out of a body.
            rig.Sim.HurtSurvivor(g, 1f, zach, "slash");
            Assert.AreEqual(1f, g.Hp);
            rig.Sim.Teleport(g.Id, rig.Map.Generators[0]);
            rig.Run(2);
            Assert.AreEqual(Prompt.None, g.Prompt);
        }

        sealed class VaultEverywhere : IMoveEnv
        {
            public float Vault(Vector2 at, float radius, bool hunter) => 1.5f;
            public bool InBrokenWindow(Vector2 p, float radius) => true;
            public bool InWater(Vector2 p) => false;
            public bool OverLow(Vector2 p, float radius) => true;
        }

        // ---------------------------------------------------------------- taking a body

        [Test]
        public void E_TakesABody_ItRisesAndTheyMerge_ThenTheyWalkAsASurvivor_AndEveryoneIsTold()
        {
            var rig = Rig(2);
            SimPlayer zach = rig.P(1), g = rig.P(2), s = rig.P(3);
            rig.Place(zach, 1500f, -1500f);
            rig.Place(s, -600f, 600f);
            rig.Place(g, 300f, 300f);
            Body b = Sacrifice(rig, g, zach);
            rig.Run(2);
            Assert.AreNotEqual(Prompt.Possess, g.Prompt, "too far from it");
            rig.Sim.Teleport(g.Id, b.Pos + SimRig.U(40f, 0f));
            rig.Run(2);
            Assert.AreEqual(Prompt.Possess, g.Prompt);
            Assert.AreEqual(b.Id, g.PromptTarget);
            Assert.AreEqual($"Press E to possess {b.Name}'s body", PlayerController.PromptText(g, rig.Sim));
            var ev = RunKeeping(rig, 1, p => p.Id == g.Id ? Press(Btn.Interact) : (InputCmd?)null);
            Assert.AreEqual(b.Id, g.Body);
            Assert.AreEqual(g.Id, b.Ghost);
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Talk && e.Text == "possess"), "the rise begins");
            Assert.AreEqual(MoveMode.Locked, rig.Sim.MoveModeFor(g), "still while it rises");
            Assert.IsFalse(MatchSim.InBody(g), "nothing to strike yet");
            Assert.IsTrue(PlayerController.GhostForm(g), "still a ghost, merging");
            // The body rises and swings upright while the ghost merges with it.
            UI.CorpseViews.Pose(true, Balance.Ghost.PossessTime * 0.5f, out float over, out float lift, out float glow, out bool shown);
            Assert.IsTrue(shown && over < 0.6f && lift > 0.3f && glow > 0.5f, "half way: off the ground, nearly upright, glowing");
            UI.CorpseViews.Pose(false, 0f, out float lying0, out _, out _, out _);
            UI.CorpseViews.Pose(false, Balance.Ghost.FallTime, out float lying1, out _, out _, out _);
            Assert.AreEqual(0f, lying0, 1e-4f, "a body dropped starts upright...");
            Assert.AreEqual(1f, lying1, 1e-4f, "...and lies flat once it's fallen");

            ev = RunKeeping(rig, Secs(Balance.Ghost.PossessTime + 0.1f));
            Assert.IsTrue(g.Reanimated && MatchSim.InBody(g));
            Assert.AreEqual(b.Pos, g.Pos, "standing where the body lay");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Feed && e.Text == $"{b.Name} has been reanimated"), "everyone told");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Talk && e.Text == "reanimated" && e.To == null), "the banner on every screen");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Item && e.Text == "you cannot use items or fight, but you can help your team repair generators" && e.To.Contains(g.Id)));
            UI.CorpseViews.Pose(true, Balance.Ghost.PossessTime + 0.1f, out _, out _, out _, out bool still);
            Assert.IsFalse(still, "the lying body is gone: it's them now");
            Assert.IsFalse(PlayerController.GhostForm(g));
            Assert.IsFalse(MatchSim.SeesGhosts(g), "back in a body, they can't see ghosts either");

            // A survivor's walk and sprint (its bar runs down), a survivor's look.
            Assert.AreEqual(MoveMode.Normal, rig.Sim.MoveModeFor(g));
            Assert.IsFalse(rig.Sim.MoveContextFor(g, false).Ghost);
            float full = g.Move.Stamina;
            RunKeeping(rig, Secs(2f), p => p.Id == g.Id ? new InputCmd { Move = Vector2.up, Buttons = Btn.Run, Aim = Mathf.PI / 2f, AimDist = 100f } : (InputCmd?)null);
            Assert.Less(g.Move.Stamina, full - 1.5f, "its sprint bar runs down like anyone's");
            Assert.AreEqual("Survivor", PlayerController.SpecFor(g).Name);
            Assert.AreEqual(Health.Eliminated, g.Health, "still counts as sacrificed");

            // A slain NPC's body: they walk as him, with none of what he could do.
            var rig2 = Rig(2);
            SimPlayer z2 = rig2.P(1), g2 = rig2.P(2);
            rig2.Place(z2, 1500f, -1500f);
            rig2.Place(rig2.P(3), -600f, 600f);
            var jaden = new Jaden(rig2.Sim) { Pos = SimRig.U(0f, 600f) };
            rig2.Sim.Npcs.Add(jaden);
            jaden.Snipe(rig2.Sim, rig2.P(3));
            rig2.Run(1);
            Body jb = rig2.Sim.Bodies.Single(x => x.OfNpc);
            Assert.AreEqual(jaden.Id, jb.Of);
            Assert.AreEqual("Jaden Nguyen", jb.Look);
            Sacrifice(rig2, g2, z2);
            TakeBody(rig2, g2, jb);
            Assert.AreEqual("Jaden Nguyen", g2.BodyLook);
            Assert.AreEqual("Jaden Nguyen", PlayerController.SpecFor(g2).Name, "looking just like him");
            Assert.AreEqual(MoveMode.Normal, rig2.Sim.MoveModeFor(g2));
            Assert.AreEqual(0, jaden.GrudgeTarget);
        }

        [Test]
        public void InABody_TheyCantPickUpTalkUseAnythingOrHurtAnyone()
        {
            SimRig rig = Reanimated(out SimPlayer zach, out SimPlayer g, out SimPlayer s, out Body body);
            // A dropped item and an NPC right there: nothing on offer, nothing taken.
            rig.Sim.Drops.Add(new DropItem { Id = rig.Sim.AllocEntityId(), Pos = g.Pos + SimRig.U(-10f, 0f), Item = ItemType.Shotgun, Amount = 4f });
            var chacko = new Chacko(rig.Sim) { Pos = g.Pos + SimRig.U(0f, 20f) };
            rig.Sim.Npcs.Add(chacko);
            rig.Run(2);
            Assert.AreEqual(Prompt.None, g.Prompt, "nothing to pick up, nobody to talk to");
            rig.Tap(g.Id, Btn.Interact);
            rig.Tap(g.Id, Btn.Primary, 0f, 1);
            rig.Tap(g.Id, Btn.Ability);
            rig.Tap(g.Id, Btn.Space);
            rig.Tap(g.Id, Btn.Drop);
            Assert.AreEqual(1, rig.Sim.Drops.Count, "the drop still there");
            Assert.AreEqual(0, g.Inv.Count(ItemType.Shotgun) + g.Inv.Count(ItemType.Bottle), "no inventory");
            Assert.IsTrue(chacko.Alive, "never harmed");
            Assert.AreEqual(-1, g.SelSlot);
            Assert.AreEqual(0, rig.Sim.Snipes.Count + rig.Sim.Thrown.Count);
            // Survivors' weapons pass it by.
            s.Inv.Add(ItemType.Shotgun);
            rig.Sim.Teleport(s.Id, g.Pos + SimRig.U(-150f, 0f));
            rig.Tap(s.Id, Btn.Primary, 0f, s.Inv.FirstSlotOf(ItemType.Shotgun) + 1);
            Assert.IsTrue(MatchSim.InBody(g), "a survivor's shot never knocks it out");
        }

        [Test]
        public void InABody_TheyHelpALivingSurvivorsRepairOrGate_AtTwoThirdsOfAHelpersSpeedUp_NeverAloneOrWithAChecks()
        {
            SimRig rig = Reanimated(out SimPlayer zach, out SimPlayer g, out SimPlayer s, out Body body);
            Vector2 gen = rig.Map.Generators[0];
            rig.Sim.Teleport(g.Id, gen + SimRig.U(40f, 0f));
            rig.Run(2);
            Assert.AreEqual(Prompt.None, g.Prompt, "nobody's working it: no hand to lend");
            rig.Hold(g.Id, Btn.Interact, Secs(1f));
            Assert.AreEqual(ActionKind.None, g.Action);
            Assert.AreEqual(0f, rig.Sim.Gens[0].Progress, "never alone");

            // A living survivor starts it: alone for 3 s, then with the ghost's hand for 3 s.
            rig.Sim.Teleport(s.Id, gen + SimRig.U(-40f, 0f));
            rig.Run(2);
            InputCmd? Hold(SimPlayer p, params SimPlayer[] who) => who.Contains(p) ? Press(Btn.Interact) : (InputCmd?)null;
            RunKeeping(rig, Secs(3f), p => Hold(p, s));
            float alone = rig.Sim.Gens[0].Progress;
            Assert.AreEqual(Prompt.Repair, g.Prompt, "a hand to lend now");
            StringAssert.Contains("help", PlayerController.PromptText(g, rig.Sim));
            bool checkSeen = false;
            RunKeeping(rig, 1, p => Hold(p, s, g));
            Assert.AreEqual(ActionKind.Repair, g.Action, "helping");
            float before = rig.Sim.Gens[0].Progress;
            for (int i = 0; i < Secs(3f); i++)
            {
                RunKeeping(rig, 1, p => Hold(p, s, g));
                checkSeen |= g.CheckOn;
            }
            float together = rig.Sim.Gens[0].Progress - before;
            float want = 1f + Balance.Objectives.CoopStep * Balance.Ghost.HelpShare;
            Assert.AreEqual(want, together / alone, 0.04f, "two thirds of a second survivor's speed-up");
            Assert.IsFalse(checkSeen, "never a skill check for the ghost");
            // The living survivor lets go: the help ends with it.
            RunKeeping(rig, 2, p => Hold(p, g));
            Assert.AreEqual(ActionKind.None, g.Action, "only with a living survivor at it");
            float stopped = rig.Sim.Gens[0].Progress;
            RunKeeping(rig, Secs(1f), p => Hold(p, g));
            Assert.AreEqual(stopped, rig.Sim.Gens[0].Progress, 1e-5f);

            // The gate: the same.
            rig.Sim.Gate.Powered = true;
            Vector2 lever = rig.Map.Lever;
            rig.Sim.Teleport(s.Id, lever + SimRig.U(-30f, 0f));
            rig.Sim.Teleport(g.Id, lever + SimRig.U(30f, 0f));
            rig.Run(2);
            RunKeeping(rig, Secs(2f), p => Hold(p, s));
            float gAlone = rig.Sim.Gate.Progress;
            RunKeeping(rig, 1, p => Hold(p, s, g));
            Assert.AreEqual(ActionKind.OpenGate, g.Action, "helping at the lever");
            float gBefore = rig.Sim.Gate.Progress;
            RunKeeping(rig, Secs(2f), p => Hold(p, s, g));
            Assert.AreEqual(want, (rig.Sim.Gate.Progress - gBefore) / gAlone, 0.05f, "the gate at the same share");
        }

        [Test]
        public void TheBurstAndPenjamin_ReachABody_ButHurtNothing()
        {
            SimRig rig = Reanimated(out SimPlayer zach, out SimPlayer g, out SimPlayer s, out Body body);
            rig.Sim.Teleport(zach.Id, g.Pos + SimRig.U(-400f, 0f));
            zach.BurstCd = 0f;
            rig.Tap(zach.Id, Btn.Secondary, 0f);
            rig.Run(Secs(1f));
            Assert.Greater(g.ScareT, 0f, "scared by the Soundcloud Burst");
            Assert.IsTrue(MatchSim.InBody(g), "still in it");
            rig.Run(Secs(3f));
            rig.Sim.SpawnVape(zach.Pos, 0f, SimRig.U(800f, 0f).x, zach.Id, false);
            rig.Run(Secs(2f));
            Assert.Greater(g.VapeSlowT, 0f, "slowed by the gas");
            Assert.Greater(g.DarkT, 0f, "and blinded");
            Assert.AreEqual(1f, g.Hp, "never hurt");
            Assert.IsTrue(MatchSim.InBody(g));
        }

        [Test]
        public void AGhostTakingABody_HoversInAnAPose_FacingIt_NeverWalking_AndInABodyWearsAGoldenHalo()
        {
            SimRig rig = Rig(2);
            SimPlayer zach = rig.P(1), g = rig.P(2);
            rig.Place(zach, 1500f, -1500f);
            rig.Place(rig.P(3), -600f, 600f);
            rig.Place(g, 300f, 300f);
            Body b = Sacrifice(rig, g, zach);
            rig.Sim.Teleport(g.Id, b.Pos + SimRig.U(50f, 0f));
            rig.Run(2);
            rig.Tap(g.Id, Btn.Interact);
            rig.Run(Secs(0.5f));
            Assert.AreEqual(Mathf.PI, Mathf.Abs(g.Facing), 0.05f, "turned to face the body");
            Assert.AreEqual(MoveMode.Locked, rig.Sim.MoveModeFor(g), "no walking while they merge");
            // The ghost's figure: the A pose, its legs still.
            GameObject go = Vision.World.PropFactory.CreateCharacter("Ghost", null, null);
            try
            {
                var view = go.GetComponent<Vision.Characters.CharacterView>();
                view.SetSpec(Vision.Characters.CharacterSpec.Survivor());
                view.Present(g);
                Assert.AreSame(Vision.Characters.ActionClips.APose, view.Layer.Current, "the A pose as it merges");
                // Hovering level with the body, up and then down again at the end.
                Assert.AreEqual(0f, UI.CorpseViews.PossessLift(0f), 1e-4f);
                Assert.Greater(UI.CorpseViews.PossessLift(0.5f), 0.4f, "floating, face to face");
                Assert.AreEqual(0f, UI.CorpseViews.PossessLift(1f), 1e-4f, "down together");
                Assert.Less(UI.CorpseViews.PossessGlide(0.25f), 0.01f, "it waits, face to face, before drifting in");
                Assert.AreEqual(1f, UI.CorpseViews.PossessGlide(0.9f), 1e-4f, "one with it before the end");
                Assert.GreaterOrEqual(Balance.Ghost.PossessTime, 3f, "slowly");
                rig.Run(Secs(Balance.Ghost.PossessTime));
                Assert.IsTrue(MatchSim.InBody(g));
                // In the body: a small golden halo over the head (and off again out of one).
                Vision.Player.Halo.Set(go, MatchSim.InBody(g), new Material(Shader.Find("Vision/LowPoly")));
                Assert.IsTrue(Vision.Player.Halo.Showing(go), "the halo");
                Transform halo = go.transform.Find("Golden Halo");
                Assert.Greater(halo.localPosition.y, Vision.Characters.HumanoidSkeleton.Height, "over the head");
                Vision.Player.Halo.Set(go, false, null);
                Assert.IsFalse(Vision.Player.Halo.Showing(go));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TestingMode_AStakedSurvivorDiesOnTheStake_LeavingTheirBody()
        {
            var rig = Rig(1, true);
            SimPlayer zach = rig.P(1);
            SimPlayer d = rig.Sim.AddDummy(Role.Survivor, rig.Map.Stakes[0] + SimRig.U(40f, 0f));
            rig.Sim.HurtSurvivor(d, 1f, zach, "slash");
            rig.Sim.CarrySurvivor(zach, d);
            rig.Sim.StakeSurvivor(zach, d, 0);
            Assert.AreEqual(Health.Staked, d.Health);
            rig.Run(Secs(Balance.Objectives.StakeStageTime + 0.5f));
            Assert.AreEqual(Health.Eliminated, d.Health, "testing mode too: the stake's time runs out");
            Body b = rig.Sim.Bodies.Single(x => x.Of == d.Id);
            Assert.Less(Vector2.Distance(b.Pos, rig.Map.Stakes[0]), 0.5f, "their body at the stake's foot");
        }

        // ---------------------------------------------------------------- knocked out

        static void AssertKnockedOut(SimRig rig, SimPlayer g, Body body, string how)
        {
            Assert.IsTrue(g.Spent, $"{how}: knocked out");
            Assert.IsTrue(g.FreeGhost, $"{how}: back out of it");
            Assert.AreEqual(0, body.Ghost, $"{how}: the body dropped");
            // (The ghost's been flung back a few metres since.)
            Assert.Less(Vector2.Distance(body.Pos, g.Pos), 6f, $"{how}: where it stood");
        }

        [Test]
        public void AnyOfZachsBlows_KnocksTheGhostOut_ThrownBackLikeA050CalHit_NeverToTakeAnother_SeeingEverything()
        {
            // A light machete tap.
            SimRig rig = Reanimated(out SimPlayer zach, out SimPlayer g, out SimPlayer s, out Body body);
            rig.Sim.Teleport(zach.Id, g.Pos + SimRig.U(-60f, 0f));
            var ev = RunKeeping(rig, 1, p => p.Id == zach.Id ? Press(Btn.Primary) : (InputCmd?)null);
            ev.AddRange(RunKeeping(rig, 1, p => p.Id == zach.Id ? Press(Btn.None) : (InputCmd?)null));
            ev.AddRange(RunKeeping(rig, Secs(0.3f)));
            AssertKnockedOut(rig, g, body, "the machete");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Talk && e.Text == "ghost_out"), "flung out");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Hit && e.A == g.Id), "the hit's flinch");
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Feed && e.Text.Contains("knocked out")));
            Assert.AreEqual(Balance.Items.Sniper.KbPeak, g.Move.KbPeak, 1e-3f, "thrown back as a 0.50 cal round throws a body");
            Assert.AreEqual(Balance.Items.Sniper.KbDuration, g.Move.KbDur, 1e-3f);
            Assert.IsTrue(PlayerController.Seer(g), "sees everything now");
            Assert.IsTrue(rig.Sim.MoveContextFor(g, false).Ghost, "endless sprint again");
            // Never another body (their own lies right there).
            rig.Run(Secs(1f));
            rig.Sim.Teleport(g.Id, body.Pos + SimRig.U(20f, 0f));
            rig.Run(2);
            Assert.AreEqual(Prompt.None, g.Prompt);
            rig.Tap(g.Id, Btn.Interact);
            Assert.AreEqual(0, g.Body);
            Assert.IsNull(rig.Sim.BodyInReach(g));
            // Another ghost can still take the dropped body.
            Assert.IsTrue(rig.Sim.Bodies.Contains(body) && body.Ghost == 0);

            // A lunge.
            rig = Reanimated(out zach, out g, out s, out body);
            rig.Sim.Teleport(zach.Id, g.Pos + SimRig.U(-160f, 0f));
            RunKeeping(rig, Secs(0.6f), p => p.Id == zach.Id ? Press(Btn.Lunge) : (InputCmd?)null);
            AssertKnockedOut(rig, g, body, "the lunge");

            // One tick of the Hemp Beam.
            rig = Reanimated(out zach, out g, out s, out body);
            rig.Sim.Teleport(zach.Id, g.Pos + SimRig.U(-200f, 0f));
            zach.BeamCharges = Balance.Hunter.Beam.Charges;
            rig.Tap(zach.Id, Btn.Beam, 0f);
            RunKeeping(rig, Secs(Balance.Hunter.Beam.Windup + 0.15f), p => p.Id == zach.Id ? Press(Btn.None) : (InputCmd?)null);
            AssertKnockedOut(rig, g, body, "the Hemp Beam");

            // A golden pump pellet.
            rig = Reanimated(out zach, out g, out s, out body);
            rig.Sim.Teleport(zach.Id, g.Pos + SimRig.U(-300f, 0f));
            zach.Pump = 12;
            rig.Tap(zach.Id, Btn.Primary, 0f);
            AssertKnockedOut(rig, g, body, "the golden pump");
        }

        // ---------------------------------------------------------------- testing mode

        [Test]
        public void TestingMode_TGoesThroughAGhost_ItsBodyAtItsFeet_AndAGeneratorStartsTheMomentItsWorked()
        {
            var rig = Rig(1, true);
            Vector2 at = rig.P(2).Pos;
            Assert.IsTrue(rig.Sim.SwitchRole(2, true));
            SimPlayer g = rig.P(2);
            Assert.AreEqual(Role.Survivor, g.Role);
            Assert.IsTrue(g.FreeGhost, "T: the survivor's ghost");
            Body mine = rig.Sim.Bodies.Single();
            Assert.AreEqual(at, mine.Pos, "their body at their feet");
            rig.Run(2);
            Assert.AreEqual(Prompt.Possess, g.Prompt, "right there to take");
            Assert.IsTrue(rig.Sim.SwitchRole(2, true));
            Assert.AreEqual(Role.Hunter, rig.P(2).Role, "T again: Zach");
            Assert.IsTrue(rig.Sim.SwitchRole(2, true));
            Assert.AreEqual(Role.Survivor, rig.P(2).Role);
            Assert.IsFalse(rig.P(2).Ghost, "and a survivor again");
            Assert.IsTrue(rig.Sim.SwitchRole(2, true));
            Assert.AreEqual(1, rig.Sim.Bodies.Count, "only their latest body lies about");
            // In their body, T to Zach drops it.
            g = rig.P(2);
            rig.Run(2);
            rig.Tap(2, Btn.Interact);
            rig.Run(Secs(Balance.Ghost.PossessTime + 0.1f));
            Assert.IsTrue(MatchSim.InBody(g));
            Assert.IsTrue(rig.Sim.SwitchRole(2, true));
            Assert.AreEqual(0, rig.Sim.Bodies[0].Ghost, "the body let go");
            // Without the cycle, T's old toggle stays Zach / survivor.
            Assert.IsTrue(rig.Sim.SwitchRole(2));
            Assert.AreEqual(Role.Survivor, rig.P(2).Role);
            Assert.IsFalse(rig.P(2).Ghost);

            // A generator starts the moment it's worked.
            SimPlayer s = rig.P(2);
            rig.Sim.Teleport(2, rig.Map.Generators[0] + SimRig.U(40f, 0f));
            rig.Run(2);
            rig.Hold(2, Btn.Interact, 3);
            Assert.IsTrue(rig.Sim.Gens[0].Repaired, "started at once in testing mode");
            Assert.IsFalse(Rig(1).Sim.TestMode);
            _ = s;
        }

        [Test]
        public void TheShotByA050CalTestEffect_ThrowsBack_Slows_AndLocksTheSprint_WithoutHarm()
        {
            Assert.IsTrue(MatchSim.TestFxButtons.Any(b => b.fx == TestFx.Sniper && b.label == "Shot by a 0.50 cal"), "in the testing effects panel");
            foreach (int who in new[] { 1, 2 })
            {
                var rig = Rig(1, true);
                SimPlayer p = rig.P(who);
                float hp = p.Hp;
                rig.Sim.PlayTestFx(who, TestFx.Sniper);
                Assert.AreEqual(Balance.Items.Sniper.KbDuration, p.Move.KbT, 1e-4f, "thrown back");
                Assert.AreEqual(Balance.Items.Sniper.KbPeak, p.Move.KbPeak, 1e-4f);
                Assert.AreEqual(Balance.Items.Sniper.SlowTime, p.SniperSlowT, 1e-4f, "slowed");
                Assert.GreaterOrEqual(p.Move.StaminaLock, Balance.Items.Sniper.SprintLock, "the sprint locked out");
                Assert.IsTrue(rig.Sim.Events.Any(e => e.Kind == EventKind.Shot && e.B == (int)ItemType.Sniper), "the round's crack and line");
                Assert.AreEqual(hp, p.Hp, "no damage");
            }
        }

        // ---------------------------------------------------------------- the lock

        [Test]
        public void TheLocksLastClick_FiresNothing_WhileItsHeld_AndTheLockIsTenPercentHarder()
        {
            var rig = new SimRig(1, 1, false, 1, m => m.Doors.Add(new SimMap.DoorDef { A = SimRig.U(-60f, 300f), B = SimRig.U(60f, 300f), Gate = true }));
            SimPlayer zach = rig.P(1), s = rig.P(2);
            rig.Place(zach, 1500f, -1500f);
            rig.Place(s, 0f, 250f);
            s.Inv.Add(ItemType.Sniper);
            int gun = s.Inv.FirstSlotOf(ItemType.Sniper) + 1;
            rig.Run(2, p => p.Id == s.Id ? new InputCmd { Item = gun, Aim = Mathf.PI / 2f, AimDist = 100f } : (InputCmd?)null);
            Assert.AreEqual(Prompt.PickLock, s.Prompt);
            rig.Tap(s.Id, Btn.Interact, Mathf.PI / 2f, gun);
            Assert.AreEqual(ActionKind.PickLock, s.Action);
            // The last check, the tick right on its mark.
            s.PickStage = Balance.Graveyard.PickChecks - 1;
            s.PickDir = 1;
            s.PickZone = 90f;
            s.PickT = 90f / MatchSim.PickSpeed(s.PickStage);
            int rounds = s.Inv.CountAt(gun - 1);
            float ammo = s.Inv.AmountAt(gun - 1);
            // The click, held a second (and E, and Space).
            var ev = RunKeeping(rig, Secs(1f), p => p.Id == s.Id ? new InputCmd { Buttons = Btn.Primary | Btn.Interact | Btn.Space, Item = gun, Aim = Mathf.PI / 2f, AimDist = 100f } : (InputCmd?)null);
            Assert.IsFalse(rig.Sim.DoorLocked[0], "unlocked");
            Assert.IsFalse(ev.Any(e => e.Kind == EventKind.Shot), "the 0.50 cal never fired");
            Assert.AreEqual(ammo, s.Inv.AmountAt(gun - 1), "not a round spent");
            Assert.IsTrue(rig.Sim.Doors[0], "the gates still open (E held did nothing more)");
            // Let go, then click: it fires as ever.
            rig.Run(2, p => p.Id == s.Id ? new InputCmd { Item = gun, Aim = Mathf.PI / 2f, AimDist = 100f } : (InputCmd?)null);
            ev = RunKeeping(rig, 2, p => p.Id == s.Id ? new InputCmd { Buttons = Btn.Primary, Item = gun, Aim = Mathf.PI / 2f, AimDist = 100f } : (InputCmd?)null);
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Shot && e.B == (int)ItemType.Sniper), "a fresh click fires");
            _ = rounds;

            // 10% harder: every mark 10% narrower than it was.
            float[] was = { 36f, 32f, 29f, 27f, 25f };
            for (int i = 0; i < was.Length; i++) Assert.AreEqual(was[i] * 0.9f, MatchSim.PickWidth(i), 1e-4f);
        }

        // ---------------------------------------------------------------- sounds and the wire

        [Test]
        public void VictoryRoyalesLastSecond_IsCut_AndTheMachetesFleshHit_IsHalfAsLoudAgain()
        {
            SoundBank.Cue v = SoundBank.Cues["victory_royale"];
            Assert.AreEqual(1.5f, v.CutEnd, 1e-5f, "its last second and a half (white noise from 3.35 s of its 4.79) cut off");
            Assert.IsTrue(v.Tracked, "played so it can be stopped");
            Assert.AreEqual(0.225f, SoundBank.Cues["slash"].Volume, 1e-5f, "half of 0.45");
            foreach (string cue in new[] { "possess", "reanimate", "ghost_out" })
                foreach (string f in SoundBank.Cues[cue].Files) Assert.NotNull(SoundBank.Load(f), $"{cue}: {f}");
        }

        [Test]
        public void Bodies_GoOverTheWire_WithEverythingAViewNeeds()
        {
            var list = new List<Body> { new Body { Id = 40, Pos = new Vector2(1f, 2f), Facing = 0.5f, Of = 7, OfNpc = true, Name = "Jaden Nguyen", Look = "Jaden Nguyen", Ghost = 3, Since = 12.5f } };
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            FieldPlan.WriteList(w, list, typeof(Body));
            w.Flush();
            ms.Position = 0;
            var back = new List<Body>();
            FieldPlan.ReadList(new BinaryReader(ms), back, typeof(Body));
            Body b = back.Single();
            Assert.AreEqual(40, b.Id);
            Assert.AreEqual(new Vector2(1f, 2f), b.Pos);
            Assert.AreEqual(7, b.Of);
            Assert.IsTrue(b.OfNpc);
            Assert.AreEqual("Jaden Nguyen", b.Look);
            Assert.AreEqual(3, b.Ghost);
            Assert.AreEqual(12.5f, b.Since);
            Assert.IsTrue(SnapshotFrame.Lists.Any(l => l.element == typeof(Body)), "sent with the snapshot");
            Assert.GreaterOrEqual(Wire.Version, 3, "a new wire: everyone needs this build");
        }
    }
}
