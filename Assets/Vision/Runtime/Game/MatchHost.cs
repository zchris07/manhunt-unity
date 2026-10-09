using System.Collections.Generic;
using UnityEngine;
using Vision.Player;
using Vision.World;

namespace Vision.Game
{
    /// <summary>
    /// Runs the match for a level: it builds the <see cref="MatchSim"/> when the level is generated, adds the local player,
    /// feeds it the local input every frame, advances it in fixed 30 Hz ticks (the original's rate), puts its state onto the
    /// level (generators, gate, doors, pallets, loot, windows) and hands its events to whoever listens (the HUD, sound,
    /// effects). Online, the host does the same with every player's input; a client applies the host's snapshots instead.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class MatchHost : MonoBehaviour
    {
        public SandboxWorld world;

        public MatchSim Sim { get; private set; }
        public WorldGeometry Geometry { get; private set; }
        public SimPlayer Local => Sim != null ? Sim.Get(LocalId) : null;
        public int LocalId = 1;
        public string LocalName = "Survivor";

        /// <summary>Raised for every event that reaches the local player (after each tick).</summary>
        public event System.Action<GameEvent> EventRaised;
        /// <summary>Raised when a new match begins (a new level, or the mode changed).</summary>
        public event System.Action Began;

        float accumulator;
        readonly List<GameEvent> local = new List<GameEvent>();

        /// <summary>An online match: who plays, who this machine is, and whether it runs the rules (the host) or follows them.</summary>
        public sealed class OnlinePlan
        {
            public IList<Vision.Net.RosterEntry> Roster;
            public int LocalId;
            public bool Authority;
            public Vision.Net.LobbySettings Settings;
        }

        /// <summary>Set while online (null offline): the next match (and every rebuild of the level) follows it.</summary>
        public OnlinePlan Online;
        /// <summary>A client's copy of the host's match: it never steps; snapshots fill it in.</summary>
        public bool Remote => Online != null && !Online.Authority;
        /// <summary>Online, between matches: the level stands still behind the lobby.</summary>
        public bool Paused;
        /// <summary>The host: every event its match raised (the server sends each to whoever it is for).</summary>
        public readonly List<GameEvent> Outgoing = new List<GameEvent>();
        /// <summary>A client: how to ask the host for something (a slot moved, a testing tool).</summary>
        public System.Action<Vision.Net.Command, int, int, Vector2> SendCommand;

        public static MatchHost For(SandboxWorld w)
        {
            if (w == null) return null;
            MatchHost h = w.GetComponent<MatchHost>();
            if (h == null)
            {
                h = w.gameObject.AddComponent<MatchHost>();
                h.world = w;
            }
            return h;
        }

        void OnEnable() => SandboxWorld.Built += OnBuilt;
        void OnDisable() => SandboxWorld.Built -= OnBuilt;

        void Start()
        {
            if (world == null) world = GetComponent<SandboxWorld>();
            if (Sim == null && world != null && world.Layout != null) Begin();
        }

        void OnBuilt(SandboxWorld w)
        {
            if (w == world) Begin();
        }

        /// <summary>A fresh match on the current level (keeping the local player's role in testing mode).</summary>
        public void Begin()
        {
            if (world == null) world = GetComponent<SandboxWorld>();
            if (world == null || world.Layout == null) return;
            Role role = Local != null && MatchState.Current.TestingMode ? Local.Role : Role.Survivor;
            Geometry = new WorldGeometry(world);
            int hunters = 1, survivors = 4;
            if (Online != null)
            {
                // Everyone in an online match plays by the host's settings and moves at its pace.
                MatchState.Current.TestingMode = Online.Settings.TestMode;
                MatchState.Current.SetPace(Online.Settings.Pace, false);
                LocalId = Online.LocalId;
                hunters = survivors = 0;
                foreach (Vision.Net.RosterEntry e in Online.Roster)
                {
                    if (e.Role == Role.Hunter) hunters++;
                    else if (e.Role == Role.Survivor) survivors++;
                }
            }
            ResolvedBalance bal = MatchRules.Resolve(Mathf.Max(1, survivors), Mathf.Max(1, hunters));
            SimMap map = SimMapBuilder.Build(world, MatchRules.StakeCount(bal.Survivors));
            Sim = new MatchSim(map, Geometry, bal, MatchState.Current.TestingMode, world.seed);
            Geometry.BrokenWindows = Sim.WindowsBroken;
            Paused = false;
            Outgoing.Clear();
            if (Online != null)
            {
                foreach (Vision.Net.RosterEntry e in Online.Roster)
                {
                    SimPlayer q = Sim.AddPlayer(e.Id, e.Name, e.Role);
                    // The host places everyone at their spawn (their clients put their avatars there).
                    if (!Remote) Sim.Place(q, q.Pos);
                    q.IsLocal = e.Id == LocalId;
                }
            }
            else
            {
                Vector2 at = world.Player != null ? PlayerPlane() : (map.SurvivorSpawns.Count > 0 ? map.SurvivorSpawns[0] : Vector2.zero);
                SimPlayer p = Sim.AddPlayer(LocalId, LocalName, role, at);
                p.IsLocal = true;
            }
            // The NPCs, and how testing respawns them.
            Sim.NpcSpawner = NpcRoster.Spawn;
            NpcRoster.Spawn(Sim);
            if (GetComponent<Vision.UI.NpcViews>() == null) gameObject.AddComponent<Vision.UI.NpcViews>().world = world;
            if (world.Wanderer != null && !Vision.Player.VisionCapture.Requested) world.Wanderer.gameObject.SetActive(false);
            accumulator = 0f;
            if (GetComponent<Vision.Player.PlayerPuppets>() == null) gameObject.AddComponent<Vision.Player.PlayerPuppets>().world = world;
            Apply(true);
            Began?.Invoke();
        }

        Vector2 PlayerPlane()
        {
            Vector3 l = world.transform.InverseTransformPoint(world.Player.transform.position);
            return new Vector2(l.x, l.z);
        }

        /// <summary>Testing mode (T): the local player becomes Zach or a survivor where they stand.</summary>
        public bool SwitchRole()
        {
            if (Sim == null || !Sim.TestMode) return false;
            if (Remote)
            {
                SendCommand?.Invoke(Vision.Net.Command.SwitchRole, 0, 0, default);
                return false;
            }
            bool ok = Sim.SwitchRole(LocalId);
            if (ok) Local.IsLocal = true;
            return ok;
        }

        /// <summary>Testing: an inert survivor or Zach a couple of metres in front of you.</summary>
        public SimPlayer SpawnDummy(Role role)
        {
            SimPlayer me = Local;
            if (Sim == null || me == null) return null;
            Vector2 at = me.Pos + me.FacingDir * 2.2f;
            if (Geometry != null && Geometry.Blocked(at, 0.4f)) at = me.Pos - me.FacingDir * 2.2f;
            if (Remote)
            {
                SendCommand?.Invoke(Vision.Net.Command.SpawnDummy, (int)role, 0, at);
                return null;
            }
            SimPlayer d = Sim.AddDummy(role, at);
            if (d != null) d.Facing = me.Facing + Mathf.PI;
            return d;
        }

        /// <summary>Testing mode changed: the match restarts on the same level under the new rules.</summary>
        public void Restart() => Begin();

        // ---------------------------------------------------------------- the local player's requests (sent to the host online)

        public void MoveSlot(int from, int to)
        {
            if (Remote) SendCommand?.Invoke(Vision.Net.Command.MoveSlot, from, to, default);
            else Sim?.MoveSlot(LocalId, from, to);
        }

        public void PlayTestFx(TestFx fx)
        {
            if (Remote) SendCommand?.Invoke(Vision.Net.Command.TestFx, (int)fx, 0, default);
            else Sim?.PlayTestFx(LocalId, fx);
        }

        public void RespawnNpcs()
        {
            if (Remote) SendCommand?.Invoke(Vision.Net.Command.RespawnNpcs, 0, 0, default);
            else Sim?.RespawnNpcs();
        }

        public void ClearDummies()
        {
            if (Remote) SendCommand?.Invoke(Vision.Net.Command.ClearDummies, 0, 0, default);
            else Sim?.ClearDummies();
        }

        public void CycleSpectate(int dir)
        {
            SimPlayer me = Local;
            if (me == null) return;
            if (Remote) SendCommand?.Invoke(Vision.Net.Command.Spectate, dir, 0, default);
            else Sim.CycleSpectate(me, dir);
        }

        /// <summary>Testing: the local player jumps to a point.</summary>
        public void Teleport(Vector2 at)
        {
            if (Remote) SendCommand?.Invoke(Vision.Net.Command.Teleport, 0, 0, at);
            else Sim?.Teleport(LocalId, at);
        }

        float sentReach, sentReachAt;

        /// <summary>How far this screen reaches (original units): the host needs it for Penjamin and teammates' glow.</summary>
        public void ReportViewReach(float units)
        {
            SimPlayer me = Local;
            if (me == null) return;
            me.ViewReach = units;
            if (!Remote || Mathf.Abs(units - sentReach) < sentReach * 0.03f || Time.unscaledTime - sentReachAt < 0.25f) return;
            sentReach = units;
            sentReachAt = Time.unscaledTime;
            SendCommand?.Invoke(Vision.Net.Command.ViewReach, 0, 0, new Vector2(units, 0f));
        }

        /// <summary>A client: a snapshot was put on the match; show it, and raise the events that came with it.</summary>
        public void Delivered(List<GameEvent> events)
        {
            if (Sim == null) return;
            Apply(false);
            foreach (GameEvent e in events) EventRaised?.Invoke(e);
        }

        void Update()
        {
            if (Sim == null || Remote || Paused) return;
            Step(Time.deltaTime);
        }

        /// <summary>Advances the match by real time: as many fixed ticks as fit, then the level shows the result.</summary>
        public void Step(float dt)
        {
            if (Sim == null) return;
            accumulator += Mathf.Min(dt, 0.25f);
            int guard = 0;
            while (accumulator >= MatchSim.TickDt && guard++ < 8)
            {
                accumulator -= MatchSim.TickDt;
                Sim.StepDummies();
                Sim.Step();
                Dispatch();
            }
            Apply(false);
        }

        /// <summary>Runs whole ticks right away (tests and tools).</summary>
        public void Tick(int ticks = 1)
        {
            for (int i = 0; i < ticks; i++)
            {
                Sim.StepDummies();
                Sim.Step();
                Dispatch();
            }
            Apply(false);
        }

        void Dispatch()
        {
            if (Online != null) Outgoing.AddRange(Sim.Events);
            local.Clear();
            foreach (GameEvent e in Sim.Events)
                if (e.To == null || System.Array.IndexOf(e.To, LocalId) >= 0) local.Add(e);
            Sim.Events.Clear();
            foreach (GameEvent e in local) EventRaised?.Invoke(e);
        }

        /// <summary>Puts the match's state onto the level.</summary>
        void Apply(bool force)
        {
            if (world == null || Sim == null) return;
            for (int i = 0; i < Sim.Gens.Length && i < world.Generators.Count; i++)
            {
                var g = world.Generators[i] != null ? world.Generators[i].GetComponent<GeneratorObjective>() : null;
                if (g != null) g.SetState(Sim.Gens[i].Progress, Sim.Gens[i].Repaired);
            }
            ExitGate.MatchPowered = Sim.Gate.Powered;
            if (world.Gate != null) world.Gate.SetState(Sim.Gate.Progress, Sim.Gate.Open);
            for (int i = 0; i < Sim.Doors.Length && i < world.Doors.Count; i++)
            {
                if (world.Doors[i] == null) continue;
                if (force || world.Doors[i].IsOpen != Sim.Doors[i]) world.Doors[i].SetOpen(Sim.Doors[i]);
                if (world.Doors[i].IsBroken != Sim.DoorBroken[i]) world.Doors[i].SetBroken(Sim.DoorBroken[i]);
            }
            for (int i = 0; i < Sim.Barricades.Length && i < world.BarricadeList.Count; i++)
                if (world.BarricadeList[i] != null) world.BarricadeList[i].SetState((int)Sim.Barricades[i]);
            for (int i = 0; i < Sim.LootTaken.Length && i < world.Pickups.Count; i++)
                if (world.Pickups[i] != null) world.Pickups[i].SetTaken(Sim.LootTaken[i]);
            for (int i = 0; i < Sim.WindowsBroken.Length && i < world.Windows.Count; i++)
                if (world.Windows[i] != null) world.Windows[i].SetBroken(Sim.WindowsBroken[i]);
            Applied?.Invoke();
        }

        /// <summary>Raised after the match's state was put on the level (views of dynamic things update here).</summary>
        public event System.Action Applied;
    }
}
