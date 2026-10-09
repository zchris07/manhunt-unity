using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Vision.Game;

namespace Vision.Net
{
    /// <summary>What the network needs from the game: build the level and a match, run it, show what happened.</summary>
    public interface INetGame
    {
        /// <summary>Builds the level from the seed (when it isn't already) and starts a match with these players.</summary>
        void Prepare(int seed, LobbySettings settings, IList<RosterEntry> roster, int localId, bool authority);
        /// <summary>The level is built and the match ready since the last <see cref="Prepare"/> (it may take a few frames).</summary>
        bool Loaded { get; }
        /// <summary>The level's fingerprint: a client's must match its host's.</summary>
        int WorldHash { get; }
        MatchSim Sim { get; }
        /// <summary>Host: the events the match raised since the last call (the server sends each to its recipients).</summary>
        List<GameEvent> TakeEvents();
        /// <summary>Client: a snapshot was applied; these events reached the local player.</summary>
        void Delivered(List<GameEvent> events);
        /// <summary>The match is over for this machine: back to the lobby screen (the level stays).</summary>
        void BackToLobby();
    }

    public enum NetPhase { Lobby, Loading, Match }

    /// <summary>
    /// The host: a lobby on a port (the host's own player is in it too), the role split and start, then the match. It takes
    /// each client's input and pose at their rate, sends each a snapshot of what changed 20 times a second with the
    /// events meant for them, keeps a dropped player's place for 30 s, and sends everyone back to the lobby at the end.
    /// </summary>
    public sealed class NetServer
    {
        public const float SnapshotHz = 20f, ReconnectGrace = 30f, LoadTimeout = 45f;

        sealed class Peer
        {
            public Connection Conn;
            public int PlayerId;
            public bool Loaded;
            public int Hash;
            public readonly SnapshotWriter Writer = new SnapshotWriter();
            public readonly List<GameEvent> Pending = new List<GameEvent>();
            public float LastPing;
        }

        readonly INetGame game;
        readonly Listener listener;
        readonly List<Peer> peers = new List<Peer>();
        readonly System.Random rng = new System.Random();
        public readonly Lobby Lobby = new Lobby();
        public NetPhase Phase { get; private set; } = NetPhase.Lobby;
        public int LocalId { get; }
        public int Port => listener.Port;
        public List<RosterEntry> Roster { get; private set; } = new List<RosterEntry>();
        public int Seed { get; private set; }
        float now, sendT, loadStarted;
        /// <summary>Clients that finished loading before the host did (checked once it has).</summary>
        readonly List<Peer> pendingLoaded = new List<Peer>();
        /// <summary>Raised when the lobby changed (to redraw it), and for chat lines and notices.</summary>
        public event Action LobbyChanged;
        public event Action<string, string> ChatLine;
        public event Action<string> Notice;

        public NetServer(INetGame game, string hostName, string token, int port = Wire.DefaultPort)
        {
            this.game = game;
            listener = new Listener(port);
            LobbyPlayer me = Lobby.Add(hostName, token, 0f);
            me.Owner = true;
            me.Ready = true;
            LocalId = me.Id;
        }

        public void Stop()
        {
            foreach (Peer p in peers)
            {
                p.Conn.Send(Kick("The host left"));
                p.Conn.Flush(200);
                p.Conn.Close();
            }
            peers.Clear();
            listener.Stop();
        }

        static byte[] Kick(string reason)
        {
            BinaryWriter w = Wire.Begin(Msg.Kicked);
            Wire.WriteString(w, reason);
            return Wire.End(w);
        }

        /// <summary>Runs the host for a frame (before the match steps).</summary>
        public void Update(float dt)
        {
            now += dt;
            while (listener.Accepted.TryDequeue(out Connection c)) peers.Add(new Peer { Conn = c, LastPing = now });
            foreach (Peer p in peers.ToArray())
            {
                while (p.Conn.Inbox.TryDequeue(out byte[] m))
                {
                    try { Handle(p, m); }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Net] bad message from {p.Conn.Remote}: {e.Message}");
                        p.Conn.Close("Bad message");
                    }
                }
                if (p.Conn.Closed) Dropped(p);
                else if (now - p.LastPing > 1f)
                {
                    p.LastPing = now;
                    BinaryWriter w = Wire.Begin(Msg.Ping);
                    w.Write(now);
                    p.Conn.Send(Wire.End(w));
                }
            }
            if (game.Loaded && pendingLoaded.Count > 0 && Phase != NetPhase.Lobby)
            {
                foreach (Peer p in pendingLoaded.ToArray()) CheckLoaded(p);
                pendingLoaded.Clear();
            }
            if (Phase == NetPhase.Loading && game.Loaded && (peers.TrueForAll(p => p.PlayerId == 0 || p.Loaded) || now - loadStarted > LoadTimeout)) Go();
            if (Phase == NetPhase.Match) MatchUpdate(dt);
        }

        void Handle(Peer p, byte[] m)
        {
            var r = new BinaryReader(new MemoryStream(m, 1, m.Length - 1));
            var type = (Msg)m[0];
            if (type == Msg.Hello) { Hello(p, r); return; }
            if (type == Msg.Pong)
            {
                float sent = r.ReadSingle();
                LobbyPlayer who = Lobby.Get(p.PlayerId);
                if (who != null) who.Ping = Mathf.RoundToInt((now - sent) * 1000f);
                return;
            }
            if (type == Msg.Ping)
            {
                BinaryWriter w = Wire.Begin(Msg.Pong);
                w.Write(r.ReadSingle());
                p.Conn.Send(Wire.End(w));
                return;
            }
            LobbyPlayer lp = Lobby.Get(p.PlayerId);
            if (lp == null) return;
            bool owner = lp.Owner || Lobby.Settings.TestMode;
            switch (type)
            {
                case Msg.RolePref:
                    lp.Pref = (RolePref)r.ReadByte();
                    Changed();
                    break;
                case Msg.Ready:
                    lp.Ready = r.ReadBoolean();
                    Changed();
                    break;
                case Msg.Chat:
                    Chat(lp.Name, Wire.ReadString(r));
                    break;
                case Msg.Settings:
                    if (owner && Phase == NetPhase.Lobby) SetSettings(LobbySettings.Read(r));
                    break;
                case Msg.Assign:
                    if (owner && Phase == NetPhase.Lobby) Assign(r.ReadInt32(), (Assigned)r.ReadByte());
                    break;
                case Msg.Shuffle:
                    if (owner && Phase == NetPhase.Lobby) Shuffle();
                    break;
                case Msg.Start:
                    if (owner && Phase == NetPhase.Lobby) Start();
                    break;
                case Msg.ToLobby:
                    if (owner && Phase != NetPhase.Lobby) ToLobby();
                    break;
                case Msg.Loaded:
                    p.Hash = r.ReadInt32();
                    if (Phase == NetPhase.Lobby || !game.Loaded) { p.Loaded = false; pendingLoaded.Add(p); break; }
                    CheckLoaded(p);
                    break;
                case Msg.Input:
                    if (Phase == NetPhase.Match) Input(p, r);
                    break;
                case Msg.Command:
                    if (Phase == NetPhase.Match) Command(lp.Id, r);
                    break;
            }
        }

        void CheckLoaded(Peer p)
        {
            if (p.Conn.Closed) return;
            if (p.Hash != game.WorldHash)
            {
                p.Conn.Send(Kick("Your map doesn't match the host's: are you both on the same version?"));
                p.Conn.Flush();
                p.Conn.Close("Different map");
                return;
            }
            p.Loaded = true;
            p.Writer.Reset();
            if (Phase == NetPhase.Match) SendGo(p);
        }

        void Hello(Peer p, BinaryReader r)
        {
            int version = r.ReadInt32();
            string name = GameHudName(Wire.ReadString(r));
            string token = Wire.ReadString(r);
            if (version != Wire.Version)
            {
                p.Conn.Send(Kick($"Different game versions (yours {version}, the host's {Wire.Version})"));
                p.Conn.Flush();
                p.Conn.Close("Version");
                return;
            }
            LobbyPlayer lp = Lobby.ByToken(token);
            if (lp != null && !lp.Owner)
            {
                // Back from a dropped connection: take their place again.
                Peer old = peers.Find(q => q != p && q.PlayerId == lp.Id);
                if (old != null) { old.Conn.Close("Replaced"); peers.Remove(old); }
                lp.Connected = true;
                if (game.Sim != null && game.Sim.Get(lp.Id) is SimPlayer sp) sp.Connected = true;
                Notice?.Invoke($"{lp.Name} is back");
            }
            else
            {
                if (Phase != NetPhase.Lobby)
                {
                    p.Conn.Send(Kick("A match is under way: try again when it's over"));
                    p.Conn.Flush();
                    p.Conn.Close("In a match");
                    return;
                }
                if (Lobby.Players.Count >= Balance.Net.MaxPlayers)
                {
                    p.Conn.Send(Kick("The lobby is full"));
                    p.Conn.Flush();
                    p.Conn.Close("Full");
                    return;
                }
                lp = Lobby.Add(name, token, now);
                Notice?.Invoke($"{lp.Name} joined");
            }
            p.PlayerId = lp.Id;
            BinaryWriter w = Wire.Begin(Msg.Welcome);
            w.Write(lp.Id);
            p.Conn.Send(Wire.End(w));
            Changed();
            // Mid-match: they load the level and pick up where they were.
            if (Phase != NetPhase.Lobby) SendStart(p);
        }

        static string GameHudName(string raw)
        {
            string n = Vision.Player.GameHud.SanitizeName(raw);
            return n.Length >= 2 ? n : "Player";
        }

        void Dropped(Peer p)
        {
            peers.Remove(p);
            LobbyPlayer lp = Lobby.Get(p.PlayerId);
            if (lp == null) return;
            if (Phase == NetPhase.Lobby)
            {
                Lobby.Remove(lp.Id);
                Notice?.Invoke($"{lp.Name} left");
            }
            else
            {
                lp.Connected = false;
                lp.LeftAt = now;
                if (game.Sim != null && game.Sim.Get(lp.Id) is SimPlayer sp)
                {
                    sp.Connected = false;
                    sp.DisconnectedAt = game.Sim.Time;
                }
                Notice?.Invoke($"{lp.Name} lost connection");
            }
            Changed();
        }

        // ---------------------------------------------------------------- lobby (also called by the host's own UI)

        public void SetSettings(LobbySettings s)
        {
            Lobby.Settings = s.Clamped();
            Changed();
        }

        public void SetPref(RolePref pref)
        {
            Lobby.Get(LocalId).Pref = pref;
            Changed();
        }

        public void Assign(int id, Assigned role)
        {
            LobbyPlayer t = Lobby.Get(id);
            if (t != null) t.Assigned = role;
            Changed();
        }

        public void Shuffle()
        {
            Lobby.Shuffle(rng);
            Changed();
        }

        public void Chat(string from, string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return;
            if (text.Length > 140) text = text.Substring(0, 140);
            BinaryWriter w = Wire.Begin(Msg.ChatLine);
            Wire.WriteString(w, from);
            Wire.WriteString(w, text);
            Broadcast(Wire.End(w));
            ChatLine?.Invoke(from, text);
        }

        /// <summary>Why the host can't start yet (null: it can).</summary>
        public string CannotStart()
        {
            if (!Lobby.AllReady()) return "Waiting for players.";
            (int h, int s, _) = Lobby.Preview();
            if (!Lobby.Settings.TestMode && (h < 1 || s < 1)) return "Need 1 hunter and 1 survivor.";
            return null;
        }

        void Changed()
        {
            BinaryWriter w = Wire.Begin(Msg.Lobby);
            w.Write((byte)Phase);
            Lobby.Write(w);
            Broadcast(Wire.End(w));
            LobbyChanged?.Invoke();
        }

        void Broadcast(byte[] m)
        {
            foreach (Peer p in peers) if (p.PlayerId != 0) p.Conn.Send(m);
        }

        // ---------------------------------------------------------------- the match

        /// <summary>The host starts the night: roles, seed, everyone loads the level.</summary>
        public bool Start()
        {
            if (Phase != NetPhase.Lobby || CannotStart() != null) return false;
            var (hunters, survivors, spectators) = Lobby.ResolveRoles(rng);
            Roster = new List<RosterEntry>();
            foreach (LobbyPlayer lp in Lobby.Players)
            {
                if (!lp.Connected) continue;
                Role role = hunters.Contains(lp.Id) ? Role.Hunter : survivors.Contains(lp.Id) ? Role.Survivor : Role.Spectator;
                Roster.Add(new RosterEntry { Id = lp.Id, Name = lp.Name, Role = role });
            }
            Seed = Lobby.Settings.SeedValue(rng);
            Phase = NetPhase.Loading;
            loadStarted = now;
            foreach (Peer p in peers) { p.Loaded = false; p.Pending.Clear(); }
            foreach (Peer p in peers) if (p.PlayerId != 0) SendStart(p);
            game.Prepare(Seed, Lobby.Settings, Roster, LocalId, true);
            Changed();
            return true;
        }

        void SendStart(Peer p)
        {
            BinaryWriter w = Wire.Begin(Msg.StartMatch);
            w.Write(Seed);
            Lobby.Settings.Write(w);
            w.Write(Roster.Count);
            foreach (RosterEntry e in Roster)
            {
                w.Write(e.Id);
                Wire.WriteString(w, e.Name);
                w.Write((byte)e.Role);
            }
            p.Loaded = false;
            p.Conn.Send(Wire.End(w));
        }

        void Go()
        {
            Phase = NetPhase.Match;
            foreach (Peer p in peers.ToArray())
            {
                if (p.PlayerId == 0) continue;
                if (!p.Loaded)
                {
                    p.Conn.Send(Kick("Loading the level took too long"));
                    p.Conn.Flush();
                    p.Conn.Close("Timed out");
                    continue;
                }
                SendGo(p);
            }
            game.TakeEvents();
            Changed();
        }

        void SendGo(Peer p)
        {
            p.Conn.Send(Wire.Bare(Msg.Go));
            p.Writer.Reset();
            p.Pending.Clear();
        }

        void Input(Peer p, BinaryReader r)
        {
            SimPlayer sp = game.Sim?.Get(p.PlayerId);
            InputCmd cmd = Wire.ReadInput(r);
            Vector2 pos = Wire.ReadVector2(r);
            float facing = r.ReadSingle();
            var gait = (Gait)r.ReadByte();
            int placeSeen = r.ReadInt32();
            if (sp == null) return;
            game.Sim.SubmitInput(sp.Id, cmd);
            // Their own movement, unless the rules moved them since they last heard.
            if (placeSeen == sp.PlaceVersion) game.Sim.SetPose(sp.Id, pos, facing, gait);
            MoveState m = sp.Move;
            MoveMode mode = m.Mode;
            SnapshotFrame.MovePlan.ReadAll(r, m);
            m.Mode = mode;
        }

        void Command(int id, BinaryReader r)
        {
            MatchSim sim = game.Sim;
            SimPlayer sp = sim?.Get(id);
            if (sp == null) return;
            var c = (Command)r.ReadByte();
            int a = r.ReadInt32(), b = r.ReadInt32();
            Vector2 v = Wire.ReadVector2(r);
            switch (c)
            {
                case Net.Command.MoveSlot: sim.MoveSlot(id, a, b); break;
                case Net.Command.Spectate: sim.CycleSpectate(sp, a); break;
                case Net.Command.ViewReach: sp.ViewReach = v.x; break;
                case Net.Command.SwitchRole: if (sim.TestMode) sim.SwitchRole(id); break;
                case Net.Command.TestFx: if (sim.TestMode) sim.PlayTestFx(id, (TestFx)a); break;
                case Net.Command.Teleport: if (sim.TestMode) sim.Teleport(id, v); break;
                case Net.Command.RespawnNpcs: if (sim.TestMode) sim.RespawnNpcs(); break;
                case Net.Command.SpawnDummy: if (sim.TestMode) sim.AddDummy((Role)a, v); break;
                case Net.Command.ClearDummies: if (sim.TestMode) sim.ClearDummies(); break;
            }
        }

        void MatchUpdate(float dt)
        {
            MatchSim sim = game.Sim;
            if (sim == null) return;
            // Events since the last frame, to the peers they're meant for.
            foreach (GameEvent e in game.TakeEvents())
                foreach (Peer p in peers)
                    if (p.Loaded && (e.To == null || Array.IndexOf(e.To, p.PlayerId) >= 0)) p.Pending.Add(e);
            // A player gone longer than the grace period forfeits.
            foreach (LobbyPlayer lp in Lobby.Players.ToArray())
                if (!lp.Connected && now - lp.LeftAt > ReconnectGrace)
                {
                    if (sim.Get(lp.Id) != null) sim.RemovePlayer(lp.Id);
                    Lobby.Remove(lp.Id);
                    Notice?.Invoke($"{lp.Name} left the match");
                    Changed();
                }
            sendT += dt;
            if (sendT < 1f / SnapshotHz) return;
            sendT = 0f;
            SnapshotFrame frame = SnapshotFrame.Build(sim);
            foreach (Peer p in peers)
            {
                if (!p.Loaded || p.PlayerId == 0) continue;
                bool hunter = sim.Get(p.PlayerId) is SimPlayer sp && sp.Role == Role.Hunter;
                p.Conn.Send(p.Writer.Write(frame, hunter, p.Pending));
                p.Pending.Clear();
            }
        }

        /// <summary>The host takes everyone back to the lobby (after the results, or to stop the match).</summary>
        public void ToLobby()
        {
            if (Phase == NetPhase.Lobby) return;
            // The last snapshot (with the result) first.
            if (Phase == NetPhase.Match && game.Sim != null)
            {
                SnapshotFrame frame = SnapshotFrame.Build(game.Sim);
                foreach (Peer p in peers)
                    if (p.Loaded) p.Conn.Send(p.Writer.Write(frame, game.Sim.Get(p.PlayerId)?.Role == Role.Hunter, p.Pending));
            }
            Phase = NetPhase.Lobby;
            foreach (LobbyPlayer lp in Lobby.Players.ToArray())
            {
                if (!lp.Connected) Lobby.Remove(lp.Id);
                else if (!lp.Owner) lp.Ready = false;
            }
            Broadcast(Wire.Bare(Msg.BackToLobby));
            game.BackToLobby();
            Changed();
        }

        /// <summary>Bytes sent and received so far (all peers), for the perf line.</summary>
        public (long sent, long received) Traffic()
        {
            long s = 0, rcv = 0;
            foreach (Peer p in peers) { s += p.Conn.BytesOut; rcv += p.Conn.BytesIn; }
            return (s, rcv);
        }

        public int PeerCount => peers.Count;
    }
}
