using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Net;

namespace Vision.Tests
{
    /// <summary>
    /// Online play: the snapshot codec (a client's copy of the match matches the host's, the local player keeps their own
    /// movement, hidden survivors stay hidden from Zach), the lobby's role split, and a real host and client over TCP on
    /// this machine: join, roles, start, the map check, snapshots, input and events.
    /// </summary>
    public class NetTests
    {
        MatchState saved;

        [SetUp]
        public void SaveState() => saved = MatchState.Current;

        [TearDown]
        public void RestoreState() => MatchState.Current = saved;

        /// <summary>A stand-in for the game: open ground (no level), the roster's players and the NPCs.</summary>
        sealed class FakeGame : INetGame
        {
            public MatchSim Sim { get; private set; }
            public bool Loaded { get; private set; }
            public int WorldHash => Sim != null ? Sim.Map.Hash() : 0;
            public bool Authority, InLobby;
            public readonly List<GameEvent> Received = new List<GameEvent>();
            public int Seed;

            public void Prepare(int seed, LobbySettings settings, IList<RosterEntry> roster, int localId, bool authority)
            {
                Seed = seed;
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
                Authority = authority;
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

        static SimRig Match()
        {
            var rig = new SimRig(2, 1, false, 9);
            NpcRoster.Spawn(rig.Sim);
            return rig;
        }

        [Test]
        public void ASnapshotRebuildsTheMatchOnAClient()
        {
            SimRig host = Match();
            host.Run(SimRig.Secs(2f));
            SimRig client = Match();
            // The client is player 3; it sees the others as the host has them.
            var writer = new SnapshotWriter();
            var reader = new SnapshotReader();
            byte[] full = writer.Write(SnapshotFrame.Build(host.Sim), false, new List<GameEvent> { new GameEvent { Kind = EventKind.Feed, Text = "hello" } });
            List<GameEvent> events = reader.Apply(client.Sim, full, 1, 3);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("hello", events[0].Text);
            Assert.AreEqual(host.Sim.Order.Count, client.Sim.Order.Count);
            Assert.AreEqual(host.P(1).Pos, client.P(1).Pos, "Zach where the host has him");
            for (int i = 0; i < host.Sim.Npcs.Count; i++)
            {
                Assert.AreEqual(host.Sim.Npcs[i].Pos, client.Sim.Npcs[i].Pos, host.Sim.Npcs[i].Name);
                Assert.AreEqual(host.Sim.Npcs[i].Flags, client.Sim.Npcs[i].Flags, host.Sim.Npcs[i].Name);
            }
            Assert.AreEqual(host.Sim.Time, client.Sim.Time);

            // Nothing changed: the next snapshot is nearly empty.
            byte[] none = writer.Write(SnapshotFrame.Build(host.Sim), false, new List<GameEvent>());
            Assert.Less(none.Length, 64, "only what changed is sent");

            // A generator worked on, a stake taken, an item dropped: they arrive.
            host.Sim.Gens[0].Progress = 0.4f;
            host.Sim.Stakes[0] = 3;
            host.Sim.Drops.Add(new DropItem { Id = 900, Pos = new Vector2(1f, 2f), Item = Vision.Player.ItemType.Bottle, Amount = 1f });
            host.Sim.Npcs[0].Pos += new Vector2(3f, 0f);
            byte[] delta = writer.Write(SnapshotFrame.Build(host.Sim), false, new List<GameEvent>());
            Assert.Less(delta.Length, full.Length / 4, "a delta is small");
            reader.Apply(client.Sim, delta, 1, 3);
            Assert.AreEqual(0.4f, client.Sim.Gens[0].Progress, 1e-5f);
            Assert.AreEqual(3, client.Sim.Stakes[0], "a stake taken");
            Assert.AreEqual(1, client.Sim.Drops.Count);
            Assert.AreEqual(host.Sim.Npcs[0].Pos, client.Sim.Npcs[0].Pos);
        }

        [Test]
        public void TheLocalPlayerKeepsTheirOwnMovementUnlessTheRulesMoveThem()
        {
            SimRig host = Match();
            SimRig client = Match();
            var writer = new SnapshotWriter();
            var reader = new SnapshotReader();
            reader.Apply(client.Sim, writer.Write(SnapshotFrame.Build(host.Sim), false, new List<GameEvent>()), 1, 2);
            SimPlayer me = client.P(2);
            // The client walked on ahead of what the host last heard.
            me.Pos = new Vector2(10f, 10f);
            me.Move.Stamina = 3f;
            host.P(2).Pos = new Vector2(9f, 10f);
            reader.Apply(client.Sim, writer.Write(SnapshotFrame.Build(host.Sim), false, new List<GameEvent>()), 1, 2);
            Assert.AreEqual(new Vector2(10f, 10f), me.Pos, "their own position stands");
            Assert.AreEqual(3f, me.Move.Stamina, "their own stamina stands");

            // The rules knock them back and slow them: that comes through.
            host.P(2).Move.KbT = 0.3f;
            host.P(2).Move.KbPeak = 400f;
            host.P(2).Move.SlowT = 1f;
            host.P(2).Move.SlowMul = 0.5f;
            reader.Apply(client.Sim, writer.Write(SnapshotFrame.Build(host.Sim), false, new List<GameEvent>()), 1, 2);
            Assert.AreEqual(0.3f, me.Move.KbT, 1e-5f);
            Assert.AreEqual(0.5f, me.Move.SlowMul, 1e-5f);
            // The host echoing the client's own decayed slow later doesn't push it back up.
            me.Move.SlowT = 0.2f;
            host.P(2).Move.SlowT = 0.6f;
            reader.Apply(client.Sim, writer.Write(SnapshotFrame.Build(host.Sim), false, new List<GameEvent>()), 1, 2);
            Assert.AreEqual(0.2f, me.Move.SlowT, 1e-5f);

            // The rules place them (a teleport, a stake): their position follows.
            host.Sim.Place(host.P(2), new Vector2(-20f, 5f));
            reader.Apply(client.Sim, writer.Write(SnapshotFrame.Build(host.Sim), false, new List<GameEvent>()), 1, 2);
            Assert.AreEqual(new Vector2(-20f, 5f), me.Pos);
        }

        [Test]
        public void ZachNeverLearnsWhereAHiddenSurvivorIs()
        {
            SimRig host = Match();
            SimPlayer s = host.P(2);
            s.Pos = new Vector2(12f, -7f);
            s.HideState = 2;
            s.HideSpot = 3;
            SimRig zach = Match();
            new SnapshotReader().Apply(zach.Sim, new SnapshotWriter().Write(SnapshotFrame.Build(host.Sim), true, new List<GameEvent>()), 1, 1);
            Assert.AreEqual(Vector2.zero, zach.P(2).Pos);
            Assert.AreEqual(-1, zach.P(2).HideSpot);
            SimRig mate = Match();
            new SnapshotReader().Apply(mate.Sim, new SnapshotWriter().Write(SnapshotFrame.Build(host.Sim), false, new List<GameEvent>()), 1, 3);
            Assert.AreEqual(new Vector2(12f, -7f), mate.P(2).Pos, "a teammate knows");
            Assert.AreEqual(new Vector2(12f, -7f), s.Pos, "the host's own copy is untouched");
        }

        [Test]
        public void TheLobbySplitsRolesAsTheOriginal()
        {
            var lobby = new Lobby();
            LobbyPlayer a = lobby.Add("Ana", "a", 0f), b = lobby.Add("Ben", "b", 1f), c = lobby.Add("Cam", "c", 2f), d = lobby.Add("Dee", "d", 3f);
            a.Pref = RolePref.Survivor;
            b.Pref = RolePref.Hunter;
            c.Pref = RolePref.Any;
            d.Pref = RolePref.Survivor;
            var (h, s, spec) = lobby.ResolveRoles(new System.Random(3));
            CollectionAssert.AreEqual(new[] { b.Id }, h, "the one who wants Zach");
            Assert.AreEqual(3, s.Count);
            Assert.AreEqual(0, spec.Count);
            lobby.Settings.Survivors = 2;
            (h, s, spec) = lobby.ResolveRoles(new System.Random(3));
            Assert.AreEqual(2, s.Count);
            Assert.AreEqual(1, spec.Count, "over the cap, they spectate");
            Assert.IsTrue(s.Contains(a.Id) && s.Contains(d.Id), "those who want to survive come first");
            a.Assigned = Assigned.Hunter;
            (h, _, _) = lobby.ResolveRoles(new System.Random(3));
            Assert.Contains(a.Id, h, "the host's assignment wins");
            Assert.AreEqual("ana 2", lobby.UniqueName("ana", -1), "a name already taken gets a number");
            Assert.IsTrue(Wire.TryParseAddress("192.168.1.20:7777", out string host, out int port) && host == "192.168.1.20" && port == 7777);
            Assert.IsTrue(Wire.TryParseAddress("10.0.0.5", out _, out port) && port == Wire.DefaultPort);
            Assert.IsTrue(Wire.TryParseAddress("[::1]:9000", out host, out port) && host == "::1" && port == 9000);
            Assert.IsFalse(Wire.TryParseAddress("host:notaport", out _, out _));
        }

        [Test]
        public void AHostAndAClientPlayOverTcp()
        {
            var hostGame = new FakeGame();
            var clientGame = new FakeGame();
            Listener.LoopbackOnly = true;
            var server = new NetServer(hostGame, "Hosty", "host-token", 0);
            NetClient client = null;
            try
            {
                client = new NetClient(clientGame, $"127.0.0.1:{server.Port}", "Guest", "guest-token");
                void Pump(System.Func<bool> until, string what, float seconds = 5f)
                {
                    var sw = Stopwatch.StartNew();
                    while (!until())
                    {
                        if (hostGame.Sim != null && server.Phase == NetPhase.Match) hostGame.Sim.Step();
                        server.Update(0.02f);
                        client.Update(0.02f);
                        Assert.IsNull(client.Failed, "the client failed: " + client.Failed);
                        if (sw.Elapsed.TotalSeconds > seconds) Assert.Fail("timed out waiting for " + what);
                        System.Threading.Thread.Sleep(5);
                    }
                }

                Pump(() => client.Lobby != null && client.Lobby.Players.Count == 2, "the lobby");
                Assert.AreEqual(2, client.YourId);
                client.SetPref(RolePref.Hunter);
                client.SetReady(true);
                Pump(() => server.Lobby.Get(2).Ready && server.Lobby.Get(2).Pref == RolePref.Hunter, "ready");
                server.Lobby.Get(1).Pref = RolePref.Survivor;
                Assert.IsNull(server.CannotStart());
                Assert.IsTrue(server.Start());
                Pump(() => server.Phase == NetPhase.Match && client.Phase == NetPhase.Match, "the match to start");
                Assert.AreEqual(hostGame.Seed, clientGame.Seed, "the same map");
                Assert.AreEqual(Role.Hunter, hostGame.Sim.Get(2).Role, "the guest wanted Zach");

                // Snapshots: the client's copy follows the host's.
                Pump(() => clientGame.Sim.Get(1) != null && clientGame.Sim.Get(1).Pos == hostGame.Sim.Get(1).Pos && clientGame.Sim.Tick > 0, "a snapshot");
                Assert.AreEqual(Role.Hunter, clientGame.Sim.Get(2).Role);

                // The client walks: the host hears where it is.
                SimPlayer me = clientGame.Sim.Get(2);
                Vector2 to = me.Pos + new Vector2(2f, 0f);
                clientGame.Sim.SetPose(2, to, 0.5f, Gait.Walk);
                clientGame.Sim.SubmitInput(2, new InputCmd { Aim = 0.5f, AimDist = 100f, Move = Vector2.right });
                Pump(() => (hostGame.Sim.Get(2).Pos - to).sqrMagnitude < 1e-6f, "the client's pose on the host");

                // An event for the client reaches it.
                hostGame.Sim.Emit(2, new GameEvent { Kind = EventKind.Item, Text = "for the guest" });
                hostGame.Sim.Feed("for everyone");
                Pump(() => clientGame.Received.Exists(e => e.Text == "for everyone") && clientGame.Received.Exists(e => e.Text == "for the guest"), "events");

                // Back to the lobby.
                server.ToLobby();
                Pump(() => clientGame.InLobby && client.Phase == NetPhase.Lobby, "the lobby again");
                Assert.IsFalse(server.Lobby.Get(2).Ready, "everyone readies up again");
            }
            finally
            {
                client?.Stop();
                server.Stop();
                Listener.LoopbackOnly = false;
            }
        }
    }
}
