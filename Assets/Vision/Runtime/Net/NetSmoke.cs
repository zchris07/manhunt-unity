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
    /// -visionNetJoin IP:port, -visionNetPort for the host's port, and -visionNetOut folder): the host opens a lobby and
    /// moves two balance sliders, the guest joins as a survivor and readies up, the host (Zach) starts the night, both
    /// walk about; then the newer mechanics, the host working the rules and the guest playing its part with its own keys:
    /// JARVIS from the guest's key, both gates powered and one opened with the end game, the hatch, Zach's worn bar, the
    /// guest sacrificed and back as a ghost walking on its own and sending a pulse at Zach. Each side takes screenshots and
    /// writes what it saw; then the host takes everyone back to the lobby and both quit.
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

            if (host) hud.CreateLobby("Hosty", int.TryParse(Arg("-visionNetPort"), out int port) ? port : Wire.DefaultPort);
            else hud.JoinLobby("Guesty", Arg("-visionNetJoin"));
            NetSession s = NetSession.Active;
            if (s == null) { Quit("no session"); yield break; }
            Log(host ? $"hosting at {s.RoomCode}" : $"joining {Arg("-visionNetJoin")}");

            if (host)
            {
                s.SetPref(RolePref.Hunter);
                // Two of the balance sliders: three generators, survivors a quarter faster.
                s.Tune(0, 3f);
                s.Tune(1, 1.25f);
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
                s.SetPref(RolePref.Survivor);
                s.SetReady(true);
                yield return Until(() => s.Lobby.Settings.Tuning.Generators == 3, 10f);
                Log($"lobby sliders seen: generators {s.Lobby.Settings.Tuning.Generators}, survivor speed {s.Lobby.Settings.Tuning.SurvivorSpeed:0.00}");
                yield return new WaitForSeconds(1f);
                yield return Shot("net_lobby_client");
            }

            yield return Until(() => s.Phase == NetPhase.Match && s.Loaded, 90f);
            MatchHost mh = MatchHost.For(world);
            SimPlayer me = mh.Local;
            Log($"match: phase {s.Phase}, loaded {s.Loaded}, seed {world.seed}, hash {s.WorldHash}, me {me?.Id} {me?.Role}, players {mh.Sim?.Order.Count}");
            if (me == null) { Quit("no local player"); yield break; }
            Log($"level: {world.Layout.GeneratorCount} generators ({world.Generators.Count} built), gate 2 {(world.Gate2 != null)}, hatch {(world.Hatch != null)}, " +
                $"sliders: survivor speed {MatchState.Current.Tuning.SurvivorSpeed:0.00}, required {mh.Sim.Bal.RequiredGenerators}");
            // The host keeps a stand-in survivor in the match, so it goes on once the guest is sacrificed.
            if (host) mh.Sim.Place(mh.Sim.AddPlayer(77, "Stand-in", Role.Survivor), mh.Sim.Map.SurvivorSpawns[0] + new Vector2(-6f, 0f));

            // Walk about for a while: a slow circle.
            PlayerController pc = world.Player;
            float t0 = Time.realtimeSinceStartup;
            int frames = 0;
            float worst = 0f, nextPing = t0 + 2f;
            var pings = new System.Collections.Generic.List<int>();
            while (Time.realtimeSinceStartup - t0 < 8f)
            {
                float a = (Time.realtimeSinceStartup - t0) * 0.8f + (host ? 0f : Mathf.PI);
                pc.MoveOverride = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                frames++;
                // (Past the first second: the match's first frames warm up.)
                if (Time.realtimeSinceStartup - t0 > 1f) worst = Mathf.Max(worst, Time.unscaledDeltaTime);
                // The guest's round trip as the host measures it, sampled once a second.
                if (host && Time.realtimeSinceStartup >= nextPing)
                {
                    nextPing += 1f;
                    foreach (LobbyPlayer lp in s.Lobby.Players) if (!lp.Owner) pings.Add(lp.Ping);
                }
                yield return null;
            }
            Log($"frames while walking: {frames / 8f:0} fps, worst frame after the first second {worst * 1000f:0} ms" + (host ? $", the guest's ping each second {string.Join(" ", pings)} ms" : ""));
            pc.MoveOverride = Vector2.zero;
            yield return new WaitForSeconds(1f);
            foreach (SimPlayer p in mh.Sim.Order) Log($"sees player {p.Id} {p.Name} {p.Role} at {p.Pos.x:0.00},{p.Pos.y:0.00} health {p.Health}");
            Log($"tick {mh.Sim.Tick}, npcs {mh.Sim.Npcs.Count}, first NPC {mh.Sim.Npcs[0].Name} at {mh.Sim.Npcs[0].Pos.x:0.00},{mh.Sim.Npcs[0].Pos.y:0.00}");
            if (s.Server != null) Log($"traffic: sent {s.Server.Traffic().sent / 1024} KB, received {s.Server.Traffic().received / 1024} KB");
            if (s.Client != null) Log($"ping {s.Lobby?.Get(s.YourId)?.Ping} ms");
            yield return Shot(host ? "net_match_host" : "net_match_client");
            yield return Newer(s, mh, pc);

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

        /// <summary>The newer mechanics, each side playing its part (see the class summary).</summary>
        IEnumerator Newer(NetSession s, MatchHost mh, PlayerController pc)
        {
            MatchSim sim = mh.Sim;
            SimPlayer me = mh.Local;
            SimPlayer guest = null, zach = null;
            foreach (SimPlayer p in sim.Order)
            {
                if (p.Role == Role.Hunter) zach = p;
                else if (p.Role == Role.Survivor && p.Id != 77) guest = p;
            }
            if (guest == null || zach == null) { Log("no guest or no Zach"); yield break; }
            pc.MoveOverride = Vector2.zero;
            Log($"roles: Zach {zach.Id} ({zach.Name}), guest {guest.Id} ({guest.Name})");

            // JARVIS: the host hands the guest the tablet; the guest presses its own key.
            if (host)
            {
                guest.Jarvis = 1;
                yield return Until(() => sim.JarvisRunT > 0f, 20f);
                Log($"JARVIS from the guest's key: running {sim.JarvisRunT:0.0} s, uses {sim.JarvisUses}, guest's {guest.JarvisT:0.0} s");
                yield return new WaitForSeconds(0.5f);
                yield return Shot("net_jarvis_host");
            }
            else
            {
                yield return Until(() => me.Jarvis == 1, 20f);
                pc.ExtraButtons = Btn.Ability;
                yield return new WaitForSeconds(0.3f);
                pc.ExtraButtons = Btn.None;
                yield return Until(() => me.JarvisT > 0f, 10f);
                yield return new WaitForSeconds(0.6f);
                Log($"JARVIS on the guest: {me.JarvisT:0.0} s left, uses {sim.JarvisUses}, active {MatchSim.JarvisActive(me)}");
                yield return Shot("net_jarvis_client");
            }

            // Both gates powered, the second opened, the end game, the hatch.
            if (host)
            {
                yield return new WaitForSeconds(3f);
                foreach (GenState g in sim.Gens) { g.Progress = 1f; g.Repaired = true; }
                yield return new WaitForSeconds(0.5f);
                sim.Gate2.Progress = 1f;
                sim.Gate2.Open = true;
                sim.StartEndGame();
                sim.HatchOpen = true;
                Log($"gates powered {sim.Gate.Powered}/{sim.Gate2.Powered}, gate 2 open, end game {sim.EndGameT:0} s, hatch open");
                yield return new WaitForSeconds(4f);
                sim.EndGameT = -1f;
                sim.HatchOpen = false;
            }
            else
            {
                yield return Until(() => sim.Gate2.Open && sim.EndGame && sim.HatchOpen, 20f);
                yield return new WaitForSeconds(1.5f);
                Log($"gates on the guest: powered {sim.Gate.Powered}/{sim.Gate2.Powered}, gate 2 open {sim.Gate2.Open}, end game {sim.EndGameT:0} s, hatch {sim.HatchOpen}");
                yield return Shot("net_endgame_client");
            }

            // Zach goes down: his bar worn for good.
            if (host)
            {
                yield return new WaitForSeconds(1f);
                sim.DownHunter(zach, true);
                yield return new WaitForSeconds(2f);
            }
            else
            {
                yield return Until(() => zach.MaxHp < 0.95f, 15f);
                Log($"Zach's bar on the guest: top {zach.MaxHp * 100f:0}, down {zach.KnockT:0.0} s");
            }

            // The guest sacrificed: back as a ghost, walking on its own, a pulse at Zach.
            if (host)
            {
                yield return new WaitForSeconds(1f);
                Vector2 at = guest.Pos;
                sim.Eliminate(guest, "stake", zach);
                yield return Until(() => guest.Ghost, 15f);
                Log($"the guest's a ghost on the host at {guest.Pos.x:0.0},{guest.Pos.y:0.0} (eliminated at {at.x:0.0},{at.y:0.0})");
                Vector2 before = guest.Pos;
                yield return Until(() => (guest.Pos - before).magnitude > 2f, 15f);
                Log($"the ghost walked on the host: {(guest.Pos - before).magnitude:0.0} m");
                // Zach up again, a few metres in front of the ghost, standing still.
                yield return Until(() => zach.KnockT <= 0f, 15f);
                zach.Hp = zach.MaxHp;
                sim.Place(zach, guest.Pos + new Vector2(0f, 6f));
                bool flashed = false;
                void OnEvent(GameEvent e) { if (e.Kind == EventKind.Scare && e.B == 1) flashed = true; }
                mh.EventRaised += OnEvent;
                yield return Until(() => flashed, 20f);
                mh.EventRaised -= OnEvent;
                yield return new WaitForSeconds(0.2f);
                Log($"Zach got the ghost's flash: {flashed}");
                yield return Shot("net_ghost_flash_host");
                yield return new WaitForSeconds(1.5f);
            }
            else
            {
                yield return Until(() => me.Ghost, 20f);
                yield return new WaitForSeconds(1f);
                Log($"I'm a ghost: at {me.Pos.x:0.0},{me.Pos.y:0.0}, move mode {sim.MoveModeFor(me)}, endless sprint {sim.MoveContextFor(me, false).Ghost}");
                yield return Shot("net_ghost_client");
                pc.MoveOverride = Vector2.right;
                pc.SprintOverride = true;
                yield return new WaitForSeconds(2f);
                pc.MoveOverride = Vector2.zero;
                pc.SprintOverride = null;
                Log($"walked as a ghost to {me.Pos.x:0.0},{me.Pos.y:0.0}, stamina {me.Move.Stamina:0.0}");
                // Zach put in front of us: aim at him and click.
                yield return Until(() => (zach.Pos - me.Pos).magnitude < 7f && zach.KnockT <= 0f, 20f);
                yield return new WaitForSeconds(0.5f);
                Vector2 d = (zach.Pos - me.Pos).normalized;
                pc.AimOverride = d;
                yield return new WaitForSeconds(0.2f);
                pc.ExtraButtons = Btn.Primary;
                yield return new WaitForSeconds(0.15f);
                pc.ExtraButtons = Btn.None;
                yield return new WaitForSeconds(0.1f);
                yield return Shot("net_ghost_pulse_client");
                yield return Until(() => me.GhostCd > 0f, 5f);
                Log($"pulse sent: cooldown {me.GhostCd:0.0} s");
                pc.AimOverride = null;
            }
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
