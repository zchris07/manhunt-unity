using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using Vision.Game;

namespace Vision.Net
{
    /// <summary>
    /// A player who joined a host by IP:port: it connects (off the main thread), says hello, follows the lobby, loads
    /// the level when the host starts, then sends its input and pose 30 times a second and puts the host's snapshots on
    /// its copy of the match. Dropped, it tries to rejoin for the grace period.
    /// </summary>
    public sealed class NetClient
    {
        readonly INetGame game;
        readonly string name, token, host;
        readonly int port;
        Connection conn;
        Thread connecting;
        volatile Connection connected;
        volatile string connectError;
        readonly SnapshotReader reader = new SnapshotReader();
        float sendT, now, retryT, lostAt = -1f;
        bool awaitingLoad;

        public int YourId { get; private set; }
        public NetPhase Phase { get; private set; } = NetPhase.Lobby;
        public Lobby Lobby { get; private set; }
        public bool Connected => conn != null && !conn.Closed && YourId != 0;
        public string Address => $"{host}:{port}";
        /// <summary>Set when the client gave up (the host left, refused it, or it couldn't reach it).</summary>
        public string Failed { get; private set; }
        public int Ping { get; private set; }
        public event Action LobbyChanged;
        public event Action<string, string> ChatLine;
        public event Action<string> Notice;

        public NetClient(INetGame game, string address, string name, string token)
        {
            this.game = game;
            this.name = name;
            this.token = token;
            if (!Wire.TryParseAddress(address, out host, out port)) throw new ArgumentException("Type the host's IP:port, like 192.168.1.20:7777");
            StartConnect();
        }

        void StartConnect()
        {
            connectError = null;
            connected = null;
            connecting = new Thread(() =>
            {
                try { connected = Connection.Connect(host, port); }
                catch (Exception e) { connectError = e is TimeoutException ? e.Message : $"Couldn't reach {host}:{port} ({e.Message})"; }
            }) { IsBackground = true, Name = "Net connect" };
            connecting.Start();
        }

        public void Stop()
        {
            if (conn != null)
            {
                conn.Flush(100);
                conn.Close();
            }
            conn = null;
        }

        public void Update(float dt)
        {
            now += dt;
            if (Failed != null) return;
            if (conn == null)
            {
                if (connected != null)
                {
                    conn = connected;
                    connected = null;
                    BinaryWriter w = Wire.Begin(Msg.Hello);
                    w.Write(Wire.Version);
                    Wire.WriteString(w, name);
                    Wire.WriteString(w, token);
                    conn.Send(Wire.End(w));
                }
                else if (connectError != null)
                {
                    if (lostAt >= 0f && now - lostAt < NetServer.ReconnectGrace)
                    {
                        retryT -= dt;
                        if (retryT <= 0f) { retryT = 2f; StartConnect(); }
                    }
                    else Failed = connectError;
                }
                return;
            }
            while (conn.Inbox.TryDequeue(out byte[] m))
            {
                try { Handle(m); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Net] bad message from the host: {e}");
                    Failed = "The host sent something this game doesn't understand";
                    conn.Close();
                    return;
                }
                if (Failed != null) return;
            }
            if (conn.Closed)
            {
                // Lost: try to get back in while the host keeps our place.
                if (YourId != 0 && Phase == NetPhase.Match)
                {
                    lostAt = now;
                    retryT = 1f;
                    Notice?.Invoke("Connection lost: reconnecting...");
                    conn = null;
                    connectError = "Lost the connection to the host";
                    return;
                }
                Failed = conn.CloseReason ?? "Lost the connection to the host";
                return;
            }
            if (awaitingLoad && game.Loaded)
            {
                awaitingLoad = false;
                BinaryWriter w = Wire.Begin(Msg.Loaded);
                w.Write(game.WorldHash);
                conn.Send(Wire.End(w));
            }
            if (Phase == NetPhase.Match)
            {
                sendT += dt;
                if (sendT >= MatchSim.TickDt)
                {
                    sendT = 0f;
                    SendInput();
                }
            }
        }

        void Handle(byte[] m)
        {
            var r = new BinaryReader(new MemoryStream(m, 1, m.Length - 1));
            switch ((Msg)m[0])
            {
                case Msg.Welcome:
                    YourId = r.ReadInt32();
                    lostAt = -1f;
                    break;
                case Msg.Lobby:
                {
                    r.ReadByte();
                    Lobby = Lobby.Read(r);
                    LobbyChanged?.Invoke();
                    break;
                }
                case Msg.ChatLine:
                    ChatLine?.Invoke(Wire.ReadString(r), Wire.ReadString(r));
                    break;
                case Msg.Kicked:
                    Failed = Wire.ReadString(r);
                    conn.Close(Failed);
                    break;
                case Msg.StartMatch:
                {
                    int seed = r.ReadInt32();
                    LobbySettings settings = LobbySettings.Read(r);
                    int n = r.ReadInt32();
                    var roster = new List<RosterEntry>();
                    for (int i = 0; i < n; i++) roster.Add(new RosterEntry { Id = r.ReadInt32(), Name = Wire.ReadString(r), Role = (Role)r.ReadByte() });
                    Phase = NetPhase.Loading;
                    reader.Reset();
                    game.Prepare(seed, settings, roster, YourId, false);
                    awaitingLoad = true;
                    break;
                }
                case Msg.Go:
                    Phase = NetPhase.Match;
                    reader.Reset();
                    break;
                case Msg.Snapshot:
                    if (game.Sim == null || Phase == NetPhase.Lobby) break;
                    game.Delivered(reader.Apply(game.Sim, m, 1, YourId));
                    break;
                case Msg.BackToLobby:
                    Phase = NetPhase.Lobby;
                    game.BackToLobby();
                    break;
                case Msg.Ping:
                {
                    BinaryWriter w = Wire.Begin(Msg.Pong);
                    w.Write(r.ReadSingle());
                    conn.Send(Wire.End(w));
                    break;
                }
            }
        }

        void SendInput()
        {
            MatchSim sim = game.Sim;
            SimPlayer me = sim?.Get(YourId);
            if (me == null || !sim.TakeInput(YourId, out InputCmd cmd)) return;
            BinaryWriter w = Wire.Begin(Msg.Input);
            Wire.Write(w, cmd);
            Wire.Write(w, me.Pos);
            w.Write(me.Facing);
            w.Write((byte)me.Gait);
            w.Write(me.PlaceVersion);
            SnapshotFrame.MovePlan.WriteAll(w, me.Move, new int[SnapshotFrame.MovePlan.Count + 1]);
            conn.Send(Wire.End(w));
        }

        // ---------------------------------------------------------------- the lobby, as seen by a guest

        public void SetPref(RolePref pref) => Send(Msg.RolePref, w => w.Write((byte)pref));
        public void SetReady(bool ready) => Send(Msg.Ready, w => w.Write(ready));
        public void Chat(string text) => Send(Msg.Chat, w => Wire.WriteString(w, text));
        public void SetSettings(LobbySettings s) => Send(Msg.Settings, s.Write);
        public void Assign(int id, Assigned role) => Send(Msg.Assign, w => { w.Write(id); w.Write((byte)role); });
        public void Shuffle() => Send(Msg.Shuffle, null);
        public void Start() => Send(Msg.Start, null);
        public void ToLobby() => Send(Msg.ToLobby, null);

        /// <summary>A match command (a slot moved, spectating someone else, testing tools) for the host to carry out.</summary>
        public void Command(Command c, int a = 0, int b = 0, Vector2 v = default) => Send(Msg.Command, w =>
        {
            w.Write((byte)c);
            w.Write(a);
            w.Write(b);
            Wire.Write(w, v);
        });

        void Send(Msg type, Action<BinaryWriter> body)
        {
            if (conn == null || conn.Closed) return;
            BinaryWriter w = Wire.Begin(type);
            body?.Invoke(w);
            conn.Send(Wire.End(w));
        }
    }
}
