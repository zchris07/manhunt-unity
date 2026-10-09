using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Vision.Game;
using Vision.Player;
using Vision.World;

namespace Vision.Net
{
    /// <summary>
    /// A scripted online session between two copies of the build on one machine (launched with -visionNetHost or
    /// -visionNetJoin IP:port, and -visionNetOut folder): the host opens a lobby, the guest joins, asks for Zach and
    /// readies up, the host starts the night, both walk about, each takes screenshots and writes what it saw, then the
    /// host takes everyone back to the lobby and both quit.
    /// </summary>
    public sealed class NetSmoke : MonoBehaviour
    {
        static string Arg(string name)
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Arg("-visionNetHost") != null || Arg("-visionNetJoin") != null) new GameObject("Net Smoke").AddComponent<NetSmoke>();
        }

        string folder;
        StreamWriter log;
        bool host;

        void Log(string line)
        {
            log?.WriteLine($"[{Time.realtimeSinceStartup:0.0}] {line}");
            log?.Flush();
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return null;
            yield return null;
        }

        static IEnumerator Until(Func<bool> done, float timeout)
        {
            float t = 0f;
            while (!done() && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        IEnumerator Start()
        {
            host = Arg("-visionNetHost") != null;
            Listener.LoopbackOnly = true;
            folder = Arg("-visionNetOut") ?? "Captures";
            Directory.CreateDirectory(folder);
            log = new StreamWriter(Path.Combine(folder, host ? "net_host.txt" : "net_client.txt"));
            GameHud hud = null;
            SandboxWorld world = null;
            yield return Until(() => (hud = FindAnyObjectByType<GameHud>()) != null && (world = FindAnyObjectByType<SandboxWorld>()) != null && world.Layout != null, 30f);
            yield return new WaitForSeconds(1f);
            if (hud == null || world == null) { Quit("no game"); yield break; }

            if (host) hud.CreateLobby("Hosty");
            else hud.JoinLobby("Guesty", Arg("-visionNetJoin"));
            NetSession s = NetSession.Active;
            if (s == null) { Quit("no session"); yield break; }
            Log(host ? $"hosting at {s.RoomCode}" : $"joining {Arg("-visionNetJoin")}");

            if (host)
            {
                yield return Until(() => s.Lobby != null && s.Lobby.Players.Count >= 2 && s.Lobby.AllReady(), 90f);
                Log($"lobby: {s.Lobby.Players.Count} players, can start: {s.Server.CannotStart() ?? "yes"}");
                yield return new WaitForSeconds(1f);
                yield return Shot("net_lobby_host");
                s.StartMatch();
            }
            else
            {
                yield return Until(() => s.Lobby != null || (s.Client != null && s.Client.Failed != null), 30f);
                if (s.Lobby == null) { Quit("couldn't join: " + s.Client?.Failed); yield break; }
                s.SetPref(RolePref.Hunter);
                s.SetReady(true);
                yield return new WaitForSeconds(1f);
                yield return Shot("net_lobby_client");
            }

            yield return Until(() => s.Phase == NetPhase.Match && s.Loaded, 90f);
            MatchHost mh = MatchHost.For(world);
            SimPlayer me = mh.Local;
            Log($"match: phase {s.Phase}, loaded {s.Loaded}, seed {world.seed}, hash {s.WorldHash}, me {me?.Id} {me?.Role}, players {mh.Sim?.Order.Count}");
            if (me == null) { Quit("no local player"); yield break; }

            // Walk about for a while: a slow circle.
            PlayerController pc = world.Player;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 8f)
            {
                float a = (Time.realtimeSinceStartup - t0) * 0.8f + (host ? 0f : Mathf.PI);
                pc.MoveOverride = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                yield return null;
            }
            pc.MoveOverride = Vector2.zero;
            yield return new WaitForSeconds(1f);
            foreach (SimPlayer p in mh.Sim.Order) Log($"sees player {p.Id} {p.Name} {p.Role} at {p.Pos.x:0.00},{p.Pos.y:0.00} health {p.Health}");
            Log($"tick {mh.Sim.Tick}, npcs {mh.Sim.Npcs.Count}, first NPC {mh.Sim.Npcs[0].Name} at {mh.Sim.Npcs[0].Pos.x:0.00},{mh.Sim.Npcs[0].Pos.y:0.00}");
            if (s.Server != null) Log($"traffic: sent {s.Server.Traffic().sent / 1024} KB, received {s.Server.Traffic().received / 1024} KB");
            if (s.Client != null) Log($"ping {s.Lobby?.Get(s.YourId)?.Ping} ms");
            yield return Shot(host ? "net_match_host" : "net_match_client");

            if (host)
            {
                yield return new WaitForSeconds(4f);
                s.ToLobby();
                yield return new WaitForSeconds(2f);
                yield return Shot("net_back_host");
                Log("back in the lobby");
                yield return new WaitForSeconds(3f);
            }
            else
            {
                yield return Until(() => s.Phase == NetPhase.Lobby, 30f);
                yield return new WaitForSeconds(1f);
                yield return Shot("net_back_client");
                Log($"back in the lobby: {s.Phase == NetPhase.Lobby}");
            }
            Quit("done");
        }

        void Quit(string why)
        {
            Log(why);
            log?.Close();
            log = null;
            Application.Quit();
        }
    }
}
