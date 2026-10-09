using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vision.Game;
using Vision.World;

namespace Vision.Net
{
    /// <summary>
    /// Online play on this machine: a lobby it hosts (<see cref="NetServer"/>) or one it joined (<see cref="NetClient"/>),
    /// and the game's side of it: building the host's level from its seed, starting the match with the lobby's roster,
    /// handing the host's events to the server, and putting a client's snapshots on the screen.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public sealed class NetSession : MonoBehaviour, INetGame
    {
        public SandboxWorld world;
        public NetServer Server { get; private set; }
        public NetClient Client { get; private set; }
        public static NetSession Active { get; private set; }
        public bool IsHost => Server != null;
        public string Name { get; private set; }

        public bool Loaded { get; private set; }
        public int WorldHash { get; private set; }
        public MatchSim Sim => Host != null ? Host.Sim : null;
        MatchHost Host => MatchHost.For(world);

        /// <summary>Raised when the match starts loading (hide the lobby), and when it is back in the lobby.</summary>
        public event Action MatchLoading, LobbyShown;

        public NetPhase Phase => Server != null ? Server.Phase : Client != null ? Client.Phase : NetPhase.Lobby;
        public Lobby Lobby => Server != null ? Server.Lobby : Client?.Lobby;
        public int YourId => Server != null ? Server.LocalId : Client != null ? Client.YourId : 0;

        static readonly string token = Guid.NewGuid().ToString("N");

        /// <summary>Who this game is to a host (for this run): a dropped connection that rejoins gets its place back.</summary>
        static string Token() => token;

        static NetSession Make(SandboxWorld w, string name)
        {
            Active?.Leave();
            var go = new GameObject("Net Session");
            var s = go.AddComponent<NetSession>();
            s.world = w;
            s.Name = name;
            Active = s;
            return s;
        }

        /// <summary>Opens a lobby on a port (throws if the port is taken).</summary>
        public static NetSession HostLobby(SandboxWorld w, string name, int port = Wire.DefaultPort)
        {
            NetSession s = Make(w, name);
            try
            {
                s.Server = new NetServer(s, name, Token(), port);
                s.Server.Lobby.Settings.Pace = MatchState.Current.Pace;
            }
            catch
            {
                Active = null;
                Destroy(s.gameObject);
                throw;
            }
            return s;
        }

        /// <summary>Joins a lobby at "IP:port" (throws if that isn't an address).</summary>
        public static NetSession Join(SandboxWorld w, string address, string name)
        {
            NetSession s = Make(w, name);
            try { s.Client = new NetClient(s, address, name, Token()); }
            catch
            {
                Active = null;
                Destroy(s.gameObject);
                throw;
            }
            return s;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Server?.Update(dt);
            Client?.Update(dt);
        }

        /// <summary>Leaves (or closes) the lobby; the game goes back to playing offline.</summary>
        public void Leave()
        {
            Server?.Stop();
            Client?.Stop();
            Server = null;
            Client = null;
            MatchHost h = world != null ? Host : null;
            if (h != null)
            {
                h.Online = null;
                h.SendCommand = null;
                h.Paused = false;
                h.LocalId = 1;
            }
            MatchState.Current.SetPace(MatchState.SavedPace(), false);
            if (Active == this) Active = null;
            if (this != null) Destroy(gameObject);
        }

        void OnDestroy()
        {
            Server?.Stop();
            Client?.Stop();
            if (Active == this) Active = null;
        }

        // ---------------------------------------------------------------- INetGame

        public void Prepare(int seed, LobbySettings settings, IList<RosterEntry> roster, int localId, bool authority)
        {
            MatchHost h = Host;
            h.Online = new MatchHost.OnlinePlan { Roster = new List<RosterEntry>(roster), LocalId = localId, Authority = authority, Settings = settings };
            h.SendCommand = Client != null ? (c, a, b, v) => Client.Command(c, a, b, v) : (Action<Command, int, int, Vector2>)null;
            h.LocalName = Name;
            Loaded = false;
            MatchLoading?.Invoke();
            StartCoroutine(Load(seed));
        }

        IEnumerator Load(int seed)
        {
            // A frame for the "generating" screen to show.
            yield return null;
            yield return null;
            MatchHost h = Host;
            if (world.Layout == null || world.seed != seed) world.Regenerate(seed);
            else h.Begin();
            yield return null;
            WorldHash = h.Sim != null ? h.Sim.Map.Hash() : 0;
            Loaded = h.Sim != null;
        }

        public List<GameEvent> TakeEvents()
        {
            MatchHost h = Host;
            var list = new List<GameEvent>(h.Outgoing);
            h.Outgoing.Clear();
            return list;
        }

        public void Delivered(List<GameEvent> events) => Host.Delivered(events);

        public void BackToLobby()
        {
            Host.Paused = true;
            LobbyShown?.Invoke();
        }

        // ---------------------------------------------------------------- the lobby, from either side

        public void SetPref(RolePref p) { if (Server != null) Server.SetPref(p); else Client?.SetPref(p); }
        public void SetReady(bool r) => Client?.SetReady(r);
        public void Chat(string text) { if (Server != null) Server.Chat(Name, text); else Client?.Chat(text); }
        public void SetSettings(LobbySettings s) { if (Server != null) Server.SetSettings(s); else Client?.SetSettings(s); }
        public void Assign(int id, Assigned role) { if (Server != null) Server.Assign(id, role); else Client?.Assign(id, role); }
        public void Shuffle() { if (Server != null) Server.Shuffle(); else Client?.Shuffle(); }
        public void StartMatch() { if (Server != null) Server.Start(); else Client?.Start(); }
        public void ToLobby() { if (Server != null) Server.ToLobby(); else Client?.ToLobby(); }

        /// <summary>The owner's powers: the host, or anyone in a testing lobby (as the original).</summary>
        public bool CanManage => Server != null || (Lobby != null && Lobby.Settings.TestMode);

        public event Action<string, string> ChatLine
        {
            add { if (Server != null) Server.ChatLine += value; if (Client != null) Client.ChatLine += value; }
            remove { if (Server != null) Server.ChatLine -= value; if (Client != null) Client.ChatLine -= value; }
        }

        public event Action LobbyChanged
        {
            add { if (Server != null) Server.LobbyChanged += value; if (Client != null) Client.LobbyChanged += value; }
            remove { if (Server != null) Server.LobbyChanged -= value; if (Client != null) Client.LobbyChanged -= value; }
        }

        public event Action<string> Notice
        {
            add { if (Server != null) Server.Notice += value; if (Client != null) Client.Notice += value; }
            remove { if (Server != null) Server.Notice -= value; if (Client != null) Client.Notice -= value; }
        }

        /// <summary>The room code to give others: this machine's LAN address and the port.</summary>
        public string RoomCode
        {
            get
            {
                if (Server == null) return Client?.Address ?? "";
                string[] a = Listener.LocalAddresses();
                return $"{a[0]}:{Server.Port}";
            }
        }
    }
}
