using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Net;
using Vision.Player;

namespace Vision.Tests
{
    /// <summary>
    /// Every mechanic added lately, played over a real TCP connection between a host (Zach) and two guests (survivors) on
    /// this machine, with the full rules and the NPCs: the guests' copies follow the host's for Zach's worn bar, the
    /// 0.50 cal's rounds count, Monique's arrow, JARVIS set going from a guest's own key (every survivor's, its run counted,
    /// the voice's event), both gates' power and opening, the end game and the hatch, a guest sacrificed and back as a
    /// ghost (moved by the host, then walking on their own, sending a pulse from their own click that flashes the other
    /// guest and never them), the NPCs after Zach for Chacko, and Zach picking up a golden pump.
    /// </summary>
    public class NewMechanicsOnlineTests
    {
        MatchState saved;

        [SetUp]
        public void SaveState()
        {
            saved = MatchState.Current;
            MatchState.Current = new MatchState();
            Listener.LoopbackOnly = true;
        }

        [TearDown]
        public void RestoreState()
        {
            MatchState.Current = saved;
            Listener.LoopbackOnly = false;
        }

        /// <summary>The level both sides build: the test rig's, with the second gate, the hatch and a golden pump lying about.</summary>
        static void Level(SimMap m)
        {
            m.HasGate2 = true;
            m.Lever2 = SimRig.U(-1200f, 0f);
            m.GatePos2 = SimRig.U(-1260f, 0f);
            m.ExitZone2 = new Rect(SimRig.U(-1700f, -200f), SimRig.U(400f, 400f));
            m.HasHatch = true;
            m.Hatch = SimRig.U(-800f, -800f);
            m.Loot.Add(new SimMap.LootDef { Pos = SimRig.U(1300f, -1300f), Item = ItemType.Shotgun, Golden = true });
        }

        sealed class Game : INetGame
        {
            public MatchSim Sim { get; private set; }
            public bool Loaded { get; private set; }
            public int WorldHash => Sim != null ? Sim.Map.Hash() : 0;
            public bool InLobby;
            public readonly List<GameEvent> Received = new List<GameEvent>();

            public void Prepare(int seed, LobbySettings settings, IList<RosterEntry> roster, int localId, bool authority)
            {
                var rig = new SimRig(1, 1, settings.TestMode, 5, Level);
                Sim = rig.Sim;
                foreach (SimPlayer p in Sim.Order.ToArray()) Sim.DropPlayer(p.Id);
                foreach (RosterEntry e in roster)
                {
                    SimPlayer q = Sim.AddPlayer(e.Id, e.Name, e.Role);
                    if (authority) Sim.Place(q, q.Pos);
                    q.IsLocal = e.Id == localId;
                }
                NpcRoster.Spawn(Sim);
                InLobby = false;
                Loaded = true;
            }

            public List<GameEvent> TakeEvents()
            {
                var list = new List<GameEvent>(Sim != null ? Sim.Events : new List<GameEvent>());
                Sim?.Events.Clear();
                return list;
            }

            public void Delivered(List<GameEvent> events) => Received.AddRange(events);
            public void BackToLobby() => InLobby = true;
        }

        [Test]
        public void EveryNewMechanic_ReachesTheGuests_AndTheGuestsOwnInputWorksIt()
        {
            var hostGame = new Game();
            var server = new NetServer(hostGame, "Zach", "host-token", 0);
            var ga = new Game();
            var gb = new Game();
            NetClient a = null, b = null;
            // What each guest is pressing (and where they stand) right now.
            Btn aButtons = Btn.None, bButtons = Btn.None;
            float aAim = 0f;
            try
            {
                a = new NetClient(ga, $"127.0.0.1:{server.Port}", "Ana", "ana-token");
                b = new NetClient(gb, $"127.0.0.1:{server.Port}", "Ben", "ben-token");
                void Pump(Func<bool> until, string what, float seconds = 8f)
                {
                    var sw = Stopwatch.StartNew();
                    while (!until())
                    {
                        if (hostGame.Sim != null && server.Phase == NetPhase.Match) hostGame.Sim.Step();
                        // Each guest's own input, as their game sends it.
                        if (ga.Sim != null && a.Phase == NetPhase.Match && a.YourId != 0 && ga.Sim.Get(a.YourId) != null)
                            ga.Sim.SubmitInput(a.YourId, new InputCmd { Buttons = aButtons, Aim = aAim, AimDist = 100f });
                        if (gb.Sim != null && b.Phase == NetPhase.Match && b.YourId != 0 && gb.Sim.Get(b.YourId) != null)
                            gb.Sim.SubmitInput(b.YourId, new InputCmd { Buttons = bButtons, AimDist = 100f });
                        server.Update(0.02f);
                        a.Update(0.02f);
                        b.Update(0.02f);
                        Assert.IsNull(a.Failed ?? b.Failed, "a guest failed");
                        if (sw.Elapsed.TotalSeconds > seconds) Assert.Fail("timed out waiting for " + what);
                        System.Threading.Thread.Sleep(3);
                    }
                }
                void Frames(int n) { int i = 0; Pump(() => ++i > n, "frames"); }

                // The lobby: Zach the host, both guests survivors; a slider moved first.
                Pump(() => a.Lobby != null && b.Lobby != null && server.Lobby.Players.Count == 3, "the lobby");
                server.Lobby.Get(server.LocalId).Pref = RolePref.Hunter;
                a.SetPref(RolePref.Survivor);
                b.SetPref(RolePref.Survivor);
                a.Tune(13, 9f);
                a.SetReady(true);
                b.SetReady(true);
                Pump(() => server.Lobby.AllReady() && server.Lobby.Settings.Tuning.ShotgunAmmo == 9, "ready");
                Assert.IsTrue(server.Start());
                Pump(() => server.Phase == NetPhase.Match && a.Phase == NetPhase.Match && b.Phase == NetPhase.Match, "the match");
                MatchSim hs = hostGame.Sim;
                int zachId = server.LocalId, anaId = a.YourId, benId = b.YourId;
                SimPlayer zach = hs.Get(zachId), ana = hs.Get(anaId), ben = hs.Get(benId);
                Assert.AreEqual(Role.Hunter, zach.Role);
                Assert.AreEqual(Role.Survivor, ana.Role);
                Assert.AreEqual(Role.Survivor, ben.Role);
                Pump(() => ga.Sim.Tick > 0 && gb.Sim.Tick > 0, "snapshots");
                // Keep everyone well apart to start.
                hs.Teleport(zachId, SimRig.U(1500f, 1500f));
                hs.Teleport(anaId, SimRig.U(0f, 0f));
                hs.Teleport(benId, SimRig.U(600f, 0f));
                Frames(10);

                // Zach's bar worn down by a down.
                hs.DownHunter(zach, true);
                Pump(() => Mathf.Abs(ga.Sim.Get(zachId).MaxHp - 0.9f) < 1e-4f && gb.Sim.Get(zachId).KnockT > 0f, "Zach's worn bar on both guests");

                // A second 0.50 cal: 10/10 on Ana's own screen; Monique's arrow on it.
                ana.Inv.Add(ItemType.Sniper);
                ana.Inv.Add(ItemType.Sniper);
                ana.ArrowT = 30f;
                Pump(() => ga.Sim.Get(anaId).Inv.RoundsAt(ga.Sim.Get(anaId).Inv.FirstSlotOf(ItemType.Sniper)) == "10/10", "the rounds count on Ana's game");
                Pump(() => ga.Sim.Get(anaId).ArrowT > 20f, "Monique's arrow");

                // JARVIS: Ben has the tablet and presses his key: both survivors get it, the voice's event says it's the first.
                ben.Jarvis = 1;
                Frames(5);
                bButtons = Btn.Ability;
                Pump(() => hs.JarvisRunT > 0f, "Ben's key to reach the host");
                bButtons = Btn.None;
                Pump(() => ga.Sim.Get(anaId).JarvisT > 0f && gb.Sim.Get(benId).JarvisT > 0f && ga.Sim.JarvisUses == 1 && gb.Sim.JarvisUses == 1, "JARVIS for both survivors");
                Pump(() => ga.Received.Any(e => e.Kind == EventKind.Jarvis && e.B == 1) && gb.Received.Any(e => e.Kind == EventKind.Jarvis && e.B == 1), "the JARVIS event (first use)");
                Assert.AreEqual(0f, hs.Get(zachId).JarvisT, "not Zach");

                // Both gates powered; Miga Sushi's opened; the end game; the hatch.
                foreach (GenState g in hs.Gens) { g.Progress = 1f; g.Repaired = true; }
                Pump(() => ga.Sim.Gate.Powered && ga.Sim.Gate2.Powered && gb.Sim.Gate2.Powered, "both gates' power");
                hs.Gate2.Progress = 1f;
                hs.Gate2.Open = true;
                hs.StartEndGame();
                Pump(() => ga.Sim.Gate2.Open && gb.Sim.EndGame && ga.Sim.EndGameT > 100f, "the open gate and the end game");
                hs.HatchOpen = true;
                Pump(() => ga.Sim.HatchOpen && gb.Sim.HatchOpen, "the hatch");
                hs.HatchOpen = false;
                hs.EndGameT = -1f;

                // Ana sacrificed: back as a ghost where she was, on her own game too.
                Vector2 where = ana.Pos;
                hs.Eliminate(ana, "stake", zach);
                Pump(() => ga.Sim.Get(anaId).Ghost && gb.Sim.Get(anaId).Ghost, "Ana a ghost on both guests", 12f);
                Pump(() => (ga.Sim.Get(anaId).Pos - where).sqrMagnitude < 1e-4f, "her own game puts her where the host did");
                Assert.AreEqual(MoveMode.Normal, ga.Sim.MoveModeFor(ga.Sim.Get(anaId)), "she can move on her own game");
                Assert.IsTrue(ga.Sim.MoveContextFor(ga.Sim.Get(anaId), false).Ghost, "with endless sprint");
                // She walks (her own game moves her and tells the host).
                Vector2 to = where + new Vector2(2f, 0f);
                ga.Sim.SetPose(anaId, to, 0f, Gait.Walk);
                Pump(() => (hs.Get(anaId).Pos - to).sqrMagnitude < 1e-4f, "the host to see her ghost walk");
                // Ben and Zach in front of her; her click sends the pulse.
                hs.Teleport(benId, to + SimRig.U(300f, 0f));
                hs.Teleport(zachId, to + SimRig.U(450f, 20f));
                zach.KnockT = 0f;
                Frames(10);
                int benSaw = gb.Received.Count;
                aAim = 0f;
                aButtons = Btn.Primary;
                Pump(() => hs.Get(anaId).GhostCd > 0f, "Ana's click to reach the host");
                aButtons = Btn.None;
                Pump(() => gb.Received.Skip(benSaw).Any(e => e.Kind == EventKind.Scare && e.B == 1), "the picture flashed at Ben");
                Assert.IsTrue(ga.Received.Any(e => e.Kind == EventKind.Talk && e.Text == "ghost_pulse" && e.A == anaId), "Ana hears her own pulse go");
                Assert.IsFalse(ga.Received.Any(e => e.Kind == EventKind.Scare && e.B == 1), "and never gets a flash herself");
                Pump(() => ga.Sim.Get(anaId).GhostCd > 10f, "her cooldown on her own HUD");

                // Zach kills Chacko: the NPCs go after him, on the guests' copies too.
                Chacko chacko = hs.Npcs.OfType<Chacko>().First();
                int ci = hs.Npcs.IndexOf(chacko);
                zach.KnockT = 0f;
                zach.MaxHp = zach.Hp = 1f;
                hs.Teleport(zachId, chacko.Pos + SimRig.U(40f, 0f));
                chacko.Slash(hs, zach, 1);
                int thomas = hs.Npcs.FindIndex(n => n is Thomas);
                Pump(() => !ga.Sim.Npcs[ci].Alive && ga.Sim.Npcs[thomas].GrudgeTarget == zachId, "the avengers on Ana's copy");

                // Zach picks up the golden pump lying out.
                hs.Teleport(zachId, hs.Map.Loot[0].Pos + SimRig.U(10f, 0f));
                Frames(5);
                Assert.AreEqual(Prompt.Loot, zach.Prompt);
                Pump(() => gb.Sim.Get(zachId).Prompt == Prompt.Loot, "Zach's prompt replicated");
                hs.SubmitInput(zachId, new InputCmd { Buttons = Btn.Interact, AimDist = 100f });
                Frames(3);
                hs.SubmitInput(zachId, new InputCmd { AimDist = 100f });
                Pump(() => gb.Sim.Get(zachId).Pump == 12 && gb.Sim.LootTaken[0], "Zach's pump on the guests");

                // Back to the lobby: nobody's stuck behind the results.
                server.ToLobby();
                Pump(() => ga.InLobby && gb.InLobby && a.Phase == NetPhase.Lobby && b.Phase == NetPhase.Lobby, "the lobby again");
            }
            finally
            {
                a?.Stop();
                b?.Stop();
                server.Stop();
            }
        }
    }
}
