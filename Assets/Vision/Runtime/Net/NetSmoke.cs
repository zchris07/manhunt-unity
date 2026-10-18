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
    /// guest sacrificed and back as a ghost (in Miga Sushi or the Diddy Chamber) walking on its own, taking its own body
    /// with its own E, walking about in it, and knocked out of it by the host's own machete swing. Each side takes
    /// screenshots and writes what it saw; then the host takes everyone back to the lobby and both quit.
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
            // (A full path: the player saves screenshots against one, not the working folder.)
            folder = Path.GetFullPath(Arg("-visionNetOut") ?? "Captures");
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

            // The guest sacrificed: their body left at the stake, back as a ghost in Miga Sushi or the Diddy Chamber, walking
            // on its own; then put beside its body, it takes it with its own E, and the host's own machete swing knocks it out.
            GameHud hud = FindAnyObjectByType<GameHud>();
            if (host)
            {
                yield return new WaitForSeconds(1f);
                Vector2 at = guest.Pos;
                sim.Eliminate(guest, "stake", zach);
                Body body = sim.Bodies.Find(b => !b.OfNpc && b.Of == guest.Id);
                Log($"the guest's body lies at {(body != null ? $"{body.Pos.x:0.0},{body.Pos.y:0.0}" : "nowhere")} (eliminated at {at.x:0.0},{at.y:0.0})");
                yield return Until(() => guest.Ghost, 15f);
                Log($"the guest's a ghost on the host at {guest.Pos.x:0.0},{guest.Pos.y:0.0}: in {PlaceAt(guest.Pos) ?? "neither place"}");
                Vector2 before = guest.Pos;
                yield return Until(() => (guest.Pos - before).magnitude > 2f, 15f);
                Log($"the ghost walked on the host: {(guest.Pos - before).magnitude:0.0} m");
                yield return new WaitForSeconds(2.5f);
                if (body != null) sim.Place(guest, body.Pos + new Vector2(1.2f, 0f));
                yield return Until(() => guest.Reanimated && guest.PossessT <= 0f, 20f);
                yield return new WaitForSeconds(0.4f);
                Log($"the guest reanimated on the host: in body {guest.Body} ({guest.BodyName}, looking like {guest.BodyLook}), banner \"{hud?.BannerText}\"");
                yield return Shot("net_reanimated_host");
                // Zach up again beside it: the host's own click swings the machete.
                yield return Until(() => zach.KnockT <= 0f, 15f);
                zach.Hp = zach.MaxHp;
                sim.Place(zach, guest.Pos + new Vector2(-1.2f, 0f));
                yield return new WaitForSeconds(0.6f);
                pc.AimOverride = (guest.Pos - zach.Pos).normalized;
                yield return new WaitForSeconds(0.2f);
                pc.ExtraButtons = Btn.Primary;
                yield return new WaitForSeconds(0.1f);
                pc.ExtraButtons = Btn.None;
                yield return Until(() => guest.Spent, 5f);
                Log($"Zach's machete knocked the ghost out: spent {guest.Spent}, out of the body {guest.FreeGhost}, the body back down {(body != null && body.Ghost == 0)}");
                pc.AimOverride = null;
                yield return new WaitForSeconds(1.5f);
                yield return Shot("net_knocked_out_host");
            }
            else
            {
                yield return Until(() => me.Ghost, 20f);
                yield return new WaitForSeconds(1f);
                Log($"I'm a ghost: at {me.Pos.x:0.0},{me.Pos.y:0.0} in {PlaceAt(me.Pos) ?? "neither place"}, move mode {sim.MoveModeFor(me)}, endless sprint {sim.MoveContextFor(me, false).Ghost}, panel \"{hud?.GhostPanelText?.Replace("\n", " / ")}\"");
                yield return Shot("net_ghost_client");
                pc.MoveOverride = Vector2.right;
                pc.SprintOverride = true;
                yield return new WaitForSeconds(2f);
                pc.MoveOverride = Vector2.zero;
                pc.SprintOverride = null;
                Log($"walked as a ghost to {me.Pos.x:0.0},{me.Pos.y:0.0}, stamina {me.Move.Stamina:0.0}");
                // Put beside our own body: E takes it.
                yield return Until(() => me.Prompt == Prompt.Possess, 25f);
                Log($"beside a body: \"{PlayerController.PromptText(me, sim)}\"");
                pc.ExtraButtons = Btn.Interact;
                yield return new WaitForSeconds(0.2f);
                pc.ExtraButtons = Btn.None;
                yield return Until(() => me.Body != 0, 5f);
                yield return new WaitForSeconds(Balance.Ghost.PossessTime * 0.45f);
                yield return Shot("net_body_rising_client");
                yield return Until(() => me.Reanimated && me.PossessT <= 0f, 10f);
                yield return new WaitForSeconds(0.5f);
                Log($"reanimated: as {me.BodyName} ({me.BodyLook}), move mode {sim.MoveModeFor(me)}, endless sprint {sim.MoveContextFor(me, false).Ghost}, panel \"{hud?.GhostPanelText?.Replace("\n", " / ")}\", banner \"{hud?.BannerText}\"");
                yield return Shot("net_reanimated_client");
                yield return Until(() => me.Spent, 25f);
                yield return new WaitForSeconds(1.2f);
                Log($"knocked out: spent {me.Spent}, out of the body {me.FreeGhost}, sees everything {PlayerController.Seer(me)}, panel \"{hud?.GhostPanelText?.Replace("\n", " / ")}\"");
                yield return Shot("net_knocked_out_client");
            }
        }

        /// <summary>The named place a point is in (Miga Sushi, the Diddy Chamber), or null.</summary>
        string PlaceAt(Vector2 at)
        {
            SandboxWorld w = FindAnyObjectByType<SandboxWorld>();
            if (w == null) return null;
            foreach (var (name, area) in w.Places) if (area.Contains(at)) return name;
            return null;
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
