using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Net;

namespace Vision.Tests
{
    /// <summary>
    /// Online play the way friends use it, over real TCP on this machine: the balance sliders moved by a joiner and seen by
    /// everyone (the host alone can reset them; the host's own settings changes never undo them; the match plays by them),
    /// someone joining while a match is under way (they spectate it, then play the next), a player dropping out mid-match
    /// and coming back into their place (or forfeiting after the grace period), the host taking everyone back to the lobby
    /// and starting again, and an old build turned away with a reason.
    /// </summary>
    public class MultiplayerScenarioTests
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

        /// <summary>A stand-in for the game: open ground, the roster's players and the NPCs; it keeps the settings it was started with.</summary>
        sealed class FakeGame : INetGame
        {
            public MatchSim Sim { get; private set; }
            public bool Loaded { get; private set; }
            public int WorldHash => Sim != null ? Sim.Map.Hash() : 0;
            public bool InLobby;
            public LobbySettings Settings;
            public List<RosterEntry> Roster;
            public int Prepared;

            public void Prepare(int seed, LobbySettings settings, IList<RosterEntry> roster, int localId, bool authority)
            {
                Settings = settings;
                Roster = new List<RosterEntry>(roster);
                Prepared++;
                var rig = new SimRig(1, 1, settings.TestMode, 5);
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

            public void Delivered(List<GameEvent> events) { }
            public void BackToLobby() => InLobby = true;
        }

        sealed class Table
        {
            public NetServer Server;
            public FakeGame HostGame = new FakeGame();
            public readonly List<(NetClient client, FakeGame game)> Guests = new List<(NetClient, FakeGame)>();
            /// <summary>Extra seconds of host time added each pump (to run past the reconnect grace).</summary>
            public float HostDt = 0.02f;

            public Table() => Server = new NetServer(HostGame, "Host", "host-token", 0);

            public (NetClient client, FakeGame game) Join(string name, string token = null)
            {
                var g = new FakeGame();
                var c = new NetClient(g, $"127.0.0.1:{Server.Port}", name, token ?? name + "-token");
                Guests.Add((c, g));
                return (c, g);
            }

            public void Pump(Func<bool> until, string what, float seconds = 6f)
            {
                var sw = Stopwatch.StartNew();
                while (!until())
                {
                    if (HostGame.Sim != null && Server.Phase == NetPhase.Match) HostGame.Sim.Step();
                    Server.Update(HostDt);
                    foreach (var (c, _) in Guests) c.Update(0.02f);
                    if (sw.Elapsed.TotalSeconds > seconds) Assert.Fail("timed out waiting for " + what);
                    System.Threading.Thread.Sleep(4);
                }
            }

            /// <summary>Runs everyone for a number of frames whatever happens.</summary>
            public void Spin(int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    if (HostGame.Sim != null && Server.Phase == NetPhase.Match) HostGame.Sim.Step();
                    Server.Update(HostDt);
                    foreach (var (c, _) in Guests) c.Update(0.02f);
                    System.Threading.Thread.Sleep(4);
                }
            }

            public void Stop()
            {
                foreach (var (c, _) in Guests) c.Stop();
                Server.Stop();
            }
        }

        [Test]
        public void TheBalanceSliders_AnyoneMovesThem_EveryoneSeesThem_OnlyTheHostResets_AndTheMatchPlaysByThem()
        {
            var t = new Table();
            try
            {
                var (a, ga) = t.Join("Ana");
                var (b, gb) = t.Join("Ben");
                t.Pump(() => a.Lobby != null && b.Lobby != null && a.Lobby.Players.Count == 3 && b.Lobby.Players.Count == 3, "everyone in the lobby");

                // A joiner moves Zach's speed: the host and the other joiner see it.
                a.Tune(2, 1.3f);
                t.Pump(() => Mathf.Abs(b.Lobby.Settings.Tuning.ZachSpeed - 1.3f) < 1e-4f, "the other guest to see Ana's slider");
                Assert.AreEqual(1.3f, t.Server.Lobby.Settings.Tuning.ZachSpeed, 1e-4f, "the host too");
                // Two at once: whichever reaches the host last wins, the same for all.
                a.Tune(0, 3f);
                b.Tune(0, 7f);
                t.Spin(30);
                t.Pump(() => a.Lobby.Settings.Tuning.Generators == t.Server.Lobby.Settings.Tuning.Generators
                             && b.Lobby.Settings.Tuning.Generators == t.Server.Lobby.Settings.Tuning.Generators, "everyone to agree");
                Assert.That(t.Server.Lobby.Settings.Tuning.Generators, Is.EqualTo(3).Or.EqualTo(7));
                b.Tune(0, 7f);
                t.Pump(() => a.Lobby.Settings.Tuning.Generators == 7 && b.Lobby.Settings.Tuning.Generators == 7, "Ben's to reach everyone");
                Assert.AreEqual(7, t.Server.Lobby.Settings.Tuning.Generators);
                // Out of range: pulled into it.
                b.Tune(13, 999f);
                t.Pump(() => a.Lobby.Settings.Tuning.ShotgunAmmo == 30, "the shells pulled into range");

                // The host's own settings change (more survivors) leaves the sliders be.
                LobbySettings s = t.Server.Lobby.Settings.Copy();
                s.Survivors = 4;
                s.Tuning = new Tuning();
                t.Server.SetSettings(s);
                t.Pump(() => a.Lobby.Settings.Survivors == 4, "the host's change");
                Assert.AreEqual(1.3f, a.Lobby.Settings.Tuning.ZachSpeed, 1e-4f, "the sliders kept");
                Assert.AreEqual(7, a.Lobby.Settings.Tuning.Generators);

                // A joiner can't reset them; nor change the host's settings.
                b.ResetTuning();
                var hunters = t.Server.Lobby.Settings.Copy();
                hunters.Hunters = 3;
                b.SetSettings(hunters);
                t.Spin(40);
                Assert.AreEqual(7, t.Server.Lobby.Settings.Tuning.Generators, "a joiner's reset is ignored");
                Assert.AreEqual(1, t.Server.Lobby.Settings.Hunters, "and their settings change");

                // The match plays by them: everyone starts with the same sliders.
                a.SetReady(true);
                b.SetReady(true);
                t.Pump(() => t.Server.Lobby.AllReady(), "ready");
                Assert.IsTrue(t.Server.Start());
                t.Pump(() => ga.Settings != null && gb.Settings != null && t.HostGame.Settings != null, "the match to load");
                foreach (FakeGame g in new[] { t.HostGame, ga, gb })
                {
                    Assert.AreEqual(1.3f, g.Settings.Tuning.ZachSpeed, 1e-4f);
                    Assert.AreEqual(7, g.Settings.Tuning.Generators);
                    Assert.AreEqual(30, g.Settings.Tuning.ShotgunAmmo);
                }
                t.Pump(() => t.Server.Phase == NetPhase.Match && a.Phase == NetPhase.Match, "the match");

                // Back in the lobby, the host resets: defaults for everyone.
                t.Server.ToLobby();
                t.Pump(() => a.Phase == NetPhase.Lobby && b.Phase == NetPhase.Lobby, "the lobby");
                t.Server.ResetTuning();
                t.Pump(() => a.Lobby.Settings.Tuning.IsDefault && b.Lobby.Settings.Tuning.IsDefault, "the defaults for all");
            }
            finally { t.Stop(); }
        }

        [Test]
        public void SomeoneJoiningMidMatch_SpectatesIt_ThenPlaysTheNext()
        {
            var t = new Table();
            try
            {
                var (a, ga) = t.Join("Ana");
                t.Pump(() => a.Lobby != null && a.Lobby.Players.Count == 2, "Ana in the lobby");
                a.SetPref(RolePref.Hunter);
                a.SetReady(true);
                t.Pump(() => t.Server.Lobby.AllReady() && t.Server.Lobby.Get(2).Pref == RolePref.Hunter, "ready");
                Assert.IsTrue(t.Server.Start());
                t.Pump(() => t.Server.Phase == NetPhase.Match && a.Phase == NetPhase.Match, "the match");

                // Cam arrives while it's under way: not turned away, a spectator in it.
                var (c, gc) = t.Join("Cam");
                t.Pump(() => c.YourId != 0 && gc.Loaded && c.Phase == NetPhase.Match, "Cam to load the match");
                Assert.IsNull(c.Failed, "not turned away");
                int camId = c.YourId;
                Assert.AreEqual(Role.Spectator, t.HostGame.Sim.Get(camId)?.Role, "spectating in the host's match");
                Assert.IsTrue(gc.Roster.Exists(e => e.Id == camId && e.Role == Role.Spectator), "and on the roster Cam's game loaded");
                t.Pump(() => gc.Sim.Get(1) != null && gc.Sim.Tick > 0, "snapshots for Cam");
                Assert.NotNull(gc.Sim.Get(2), "Cam's copy has the players in it");
                t.Pump(() => ga.Sim.Get(camId) != null, "Ana's copy to have Cam in it");
                Assert.AreEqual(Role.Spectator, ga.Sim.Get(camId).Role);
                Assert.IsTrue(t.Server.Lobby.Players.Exists(p => p.Id == camId), "in the lobby list");

                // Back to the lobby: Cam's a player like anyone else, and in the next match.
                t.Server.ToLobby();
                t.Pump(() => a.Phase == NetPhase.Lobby && c.Phase == NetPhase.Lobby, "the lobby");
                c.SetReady(true);
                a.SetReady(true);
                t.Pump(() => t.Server.Lobby.AllReady(), "ready again");
                Assert.IsTrue(t.Server.Start());
                t.Pump(() => t.Server.Phase == NetPhase.Match && c.Phase == NetPhase.Match && gc.Prepared == 2, "the next match");
                Assert.AreNotEqual(Role.Spectator, t.HostGame.Sim.Get(camId).Role, "playing this time");
            }
            finally { t.Stop(); }
        }

        [Test]
        public void APlayerDroppingOutMidMatch_ComesBackIntoTheirPlace_OrForfeitsAfterTheGrace()
        {
            var t = new Table();
            try
            {
                var (a, ga) = t.Join("Ana", "ana-token");
                var (b, gb) = t.Join("Ben", "ben-token");
                t.Pump(() => a.Lobby != null && b.Lobby != null && a.Lobby.Players.Count == 3, "the lobby");
                t.Server.Lobby.Get(1).Pref = RolePref.Hunter;
                a.SetPref(RolePref.Survivor);
                b.SetPref(RolePref.Survivor);
                a.SetReady(true);
                b.SetReady(true);
                t.Pump(() => t.Server.Lobby.AllReady(), "ready");
                Assert.IsTrue(t.Server.Start());
                t.Pump(() => t.Server.Phase == NetPhase.Match && a.Phase == NetPhase.Match && b.Phase == NetPhase.Match, "the match");
                int anaId = a.YourId;
                Assert.AreEqual(Role.Survivor, t.HostGame.Sim.Get(anaId).Role);

                // Ana's game drops: her place is kept.
                a.Stop();
                t.Guests.RemoveAt(0);
                t.Pump(() => !t.Server.Lobby.Get(anaId).Connected, "the host to notice");
                Assert.IsFalse(t.HostGame.Sim.Get(anaId).Connected);
                Assert.NotNull(t.HostGame.Sim.Get(anaId), "still in the match for now");

                // She comes back (the same game: the same token): back in her own place.
                var (a2, ga2) = t.Join("Ana", "ana-token");
                t.Pump(() => a2.YourId == anaId && ga2.Loaded && a2.Phase == NetPhase.Match, "Ana back in");
                Assert.IsTrue(t.Server.Lobby.Get(anaId).Connected);
                Assert.IsTrue(t.HostGame.Sim.Get(anaId).Connected);
                Assert.AreEqual(Role.Survivor, t.HostGame.Sim.Get(anaId).Role, "the same survivor");
                Assert.AreEqual(3, t.Server.Lobby.Players.Count, "no second Ana");

                // Ben drops and stays gone past the grace period: out of the match.
                int benId = b.YourId;
                b.Stop();
                t.Guests.RemoveAt(0);
                t.Pump(() => !t.Server.Lobby.Get(benId).Connected, "the host to notice Ben");
                t.HostDt = NetServer.ReconnectGrace + 1f;
                t.Pump(() => t.Server.Lobby.Get(benId) == null, "Ben to forfeit");
                t.HostDt = 0.02f;
                Assert.IsNull(t.HostGame.Sim.Get(benId), "gone from the match");
                t.Pump(() => ga2.Sim.Get(benId) == null, "and from Ana's copy");
                Assert.AreEqual(NetPhase.Match, t.Server.Phase, "the match goes on");
            }
            finally { t.Stop(); }
        }

        [Test]
        public void TheHost_LocksTheSliders_ThenOnlyTheHostMovesThem_UntilUnlocked()
        {
            var t = new Table();
            try
            {
                var (a, ga) = t.Join("Ana");
                t.Pump(() => a.Lobby != null && a.Lobby.Players.Count == 2, "Ana in the lobby");
                a.Tune(2, 1.3f);
                t.Pump(() => Mathf.Abs(t.Server.Lobby.Settings.Tuning.ZachSpeed - 1.3f) < 1e-4f, "Ana's slider, unlocked");
                t.Server.LockTuning(true);
                t.Pump(() => a.Lobby.Settings.TuningLocked, "the lock on Ana's screen");
                a.Tune(2, 0.7f);
                a.Tune(0, 8f);
                t.Spin(40);
                Assert.AreEqual(1.3f, t.Server.Lobby.Settings.Tuning.ZachSpeed, 1e-4f, "locked: Ana's moves ignored");
                Assert.AreEqual(Tuning.DefaultGenerators, t.Server.Lobby.Settings.Tuning.Generators);
                t.Server.Tune(2, 1.5f);
                t.Pump(() => Mathf.Abs(a.Lobby.Settings.Tuning.ZachSpeed - 1.5f) < 1e-4f, "the host still moves them, everyone sees it");
                // Nobody else's settings change unlocks them (in a testing-mode lobby anyone may change the settings).
                LobbySettings test = t.Server.Lobby.Settings.Copy();
                test.TestMode = true;
                t.Server.SetSettings(test);
                t.Pump(() => a.Lobby.Settings.TestMode, "a testing-mode lobby");
                Assert.IsTrue(t.Server.Lobby.Settings.TuningLocked, "the host's settings change keeps the lock");
                LobbySettings sneaky = a.Lobby.Settings.Copy();
                sneaky.TuningLocked = false;
                a.SetSettings(sneaky);
                t.Spin(40);
                Assert.IsTrue(t.Server.Lobby.Settings.TuningLocked, "a joiner can't unlock them");
                a.Tune(2, 0.7f);
                t.Spin(40);
                Assert.AreEqual(1.5f, t.Server.Lobby.Settings.Tuning.ZachSpeed, 1e-4f);
                // Unlocked: Ana moves them again.
                t.Server.LockTuning(false);
                t.Pump(() => !a.Lobby.Settings.TuningLocked, "unlocked on Ana's screen");
                a.Tune(2, 0.7f);
                t.Pump(() => Mathf.Abs(t.Server.Lobby.Settings.Tuning.ZachSpeed - 0.7f) < 1e-4f, "Ana's slider again");
                _ = ga;
            }
            finally
            {
                t.Stop();
            }
        }

        [Test]
        public void AnOldBuild_IsTurnedAwayWithAReason()
        {
            var t = new Table();
            Connection conn = null;
            try
            {
                conn = Connection.Connect("127.0.0.1", t.Server.Port);
                BinaryWriter w = Wire.Begin(Msg.Hello);
                w.Write(Wire.Version - 1);
                Wire.WriteString(w, "Old");
                Wire.WriteString(w, "old-token");
                conn.Send(Wire.End(w));
                string reason = null;
                t.Pump(() =>
                {
                    while (conn.Inbox.TryDequeue(out byte[] m))
                        if ((Msg)m[0] == Msg.Kicked) reason = Wire.ReadString(new BinaryReader(new MemoryStream(m, 1, m.Length - 1)));
                    return reason != null;
                }, "the kick");
                StringAssert.Contains("Different game versions", reason);
                Assert.AreEqual(1, t.Server.Lobby.Players.Count, "never let into the lobby");
            }
            finally
            {
                conn?.Close();
                t.Stop();
            }
        }
    }
}
