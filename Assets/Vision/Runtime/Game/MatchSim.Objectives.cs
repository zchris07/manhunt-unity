using System.Collections.Generic;
using UnityEngine;

namespace Vision.Game
{
    /// <summary>Generators, the gate, escaping, the win check, senses and vengeance (objectives.ts, senses.ts).</summary>
    public sealed partial class MatchSim
    {
        /// <summary>The survivor Jaden, Plasma and Shane are hunting for Chacko's sake (0 nobody), and seconds left.</summary>
        public int Vengeance;
        public float VengeanceT;

        void UpdateObjectives(float dt)
        {
            // Generators.
            var workers = new int[Gens.Length];
            foreach (SimPlayer p in Order)
                if (p.Role == Role.Survivor && p.Action == ActionKind.Repair && p.ActionTarget >= 0 && p.ActionTarget < Gens.Length) workers[p.ActionTarget]++;
            for (int gi = 0; gi < Gens.Length; gi++)
            {
                GenState g = Gens[gi];
                g.Workers = workers[gi];
                if (g.Repaired) continue;
                if (g.Workers > 0 && !Gate.Powered)
                {
                    float mul = 1f + Balance.Objectives.CoopStep * (g.Workers - 1);
                    g.Progress += mul / Bal.RepairTime * dt;
                    g.Regressing = false;
                }
                else if (g.Regressing)
                {
                    g.Progress -= Balance.Objectives.RegressPerSec * dt;
                    if (g.Progress <= 0f)
                    {
                        g.Progress = 0f;
                        g.Regressing = false;
                    }
                }
                if (g.Progress < 1f) continue;
                g.Progress = 1f;
                g.Repaired = true;
                g.Regressing = false;
                foreach (SimPlayer p in Order) if (p.Action == ActionKind.Repair && p.ActionTarget == gi) CancelAction(p);
                Emit(null, new GameEvent { Kind = EventKind.GenDone, A = gi, Pos = Map.Generators[gi] });
                int done = 0;
                foreach (GenState x in Gens) if (x.Repaired) done++;
                Feed($"Generator restored ({Mathf.Min(done, Bal.RequiredGenerators)}/{Bal.RequiredGenerators})");
            }

            // Enough generators running: the gate has power (checked every tick, so a generator set running by a tool counts too).
            if (!Gate.Powered)
            {
                int repaired = 0;
                foreach (GenState x in Gens) if (x.Repaired) repaired++;
                if (repaired >= Bal.RequiredGenerators)
                {
                    Gate.Powered = true;
                    Emit(null, new GameEvent { Kind = EventKind.GatePowered });
                    Feed("The exit gate has power");
                    foreach (SimPlayer p in Order) if (p.Action == ActionKind.Repair) CancelAction(p);
                }
            }

            // Exit gate.
            if (Gate.Powered && !Gate.Open)
            {
                var openers = new List<SimPlayer>();
                foreach (SimPlayer p in Order) if (p.Action == ActionKind.OpenGate) openers.Add(p);
                if (openers.Count > 0)
                {
                    Gate.Progress = Mathf.Min(1f, Gate.Progress + dt / Balance.Objectives.GateOpenTime);
                    if (Gate.Progress >= 1f)
                    {
                        Gate.Open = true;
                        foreach (SimPlayer p in openers) CancelAction(p);
                        Emit(null, new GameEvent { Kind = EventKind.GateOpen });
                        Feed("The gate is open");
                    }
                }
            }

            // Escape through the open gate.
            if (Gate.Open)
                foreach (SimPlayer p in Order)
                {
                    if (p.Role != Role.Survivor || (p.Health != Health.Healthy && p.Health != Health.Wounded) || p.HideState != 0) continue;
                    if (!Geo.InExitZone(p.Pos)) continue;
                    CancelAction(p);
                    p.Health = Health.Escaped;
                    p.Stats.Outcome = "escaped";
                    p.EndedTime = Time;
                    p.Spectating = DefaultSpectateTarget(p.Id);
                    Emit(null, new GameEvent { Kind = EventKind.Escaped, A = p.Id });
                    Feed($"{p.Name} escaped");
                }

            // Disconnected players past the grace period forfeit.
            foreach (SimPlayer p in Order.ToArray())
            {
                if (p.Connected || p.DisconnectedAt <= 0f || TestMode) continue;
                if (Time - p.DisconnectedAt > Balance.Net.ReconnectGraceSec)
                {
                    p.DisconnectedAt = -1f;
                    Forfeit(p.Id);
                }
            }

            // Keep spectators pointed at someone still playing.
            foreach (SimPlayer p in Order)
            {
                bool watching = p.Role == Role.Spectator || p.Health == Health.Escaped || p.Health == Health.Eliminated;
                if (!watching) continue;
                SimPlayer t = Get(p.Spectating);
                if (t == null || t.Id == p.Id || t.Health == Health.Escaped || t.Health == Health.Eliminated || t.Role == Role.Spectator)
                    p.Spectating = DefaultSpectateTarget(p.Id);
            }
        }

        /// <summary>Cycles a spectator to the next live player.</summary>
        public void CycleSpectate(SimPlayer p, int dir)
        {
            var live = new List<SimPlayer>();
            foreach (SimPlayer q in Order)
                if (q.Id != p.Id && (q.Role == Role.Hunter ? q.Health != Health.Eliminated : q.Role == Role.Survivor && q.Health != Health.Escaped && q.Health != Health.Eliminated))
                    live.Add(q);
            if (live.Count == 0) return;
            int i = live.FindIndex(q => q.Id == p.Spectating);
            p.Spectating = live[((i + dir) % live.Count + live.Count) % live.Count].Id;
        }

        /// <summary>The original's win check without its time limit.</summary>
        void CheckWin()
        {
            if (Result != null) return;
            int survivors = 0, hunters = 0, escaped = 0, eliminated = 0, standing = 0, huntersGone = 0;
            foreach (SimPlayer p in Order)
            {
                if (p.Role == Role.Survivor)
                {
                    survivors++;
                    if (p.Health == Health.Escaped) escaped++;
                    if (p.Health == Health.Eliminated) eliminated++;
                    if (p.Health == Health.Healthy || p.Health == Health.Wounded) standing++;
                }
                else if (p.Role == Role.Hunter)
                {
                    hunters++;
                    if (p.Health == Health.Eliminated) huntersGone++;
                }
            }
            Winner w = MatchRules.CheckWin(survivors, standing, escaped, eliminated, hunters, huntersGone, Bal.EscapeNeeded, out string reason);
            if (w == Winner.None) return;
            foreach (SimPlayer p in Order)
                if (p.Role == Role.Survivor && p.Health != Health.Escaped && p.Health != Health.Eliminated)
                    p.Stats.Outcome = w == Winner.Hunters ? "eliminated" : "survived";
            int repaired = 0;
            foreach (GenState g in Gens) if (g.Repaired) repaired++;
            Result = new MatchResult
            {
                Winner = w,
                Reason = reason,
                DurationSec = Mathf.Round(Time),
                Escaped = escaped,
                Eliminated = eliminated,
                Survivors = survivors,
                Hunters = hunters,
                GeneratorsRepaired = repaired,
                GeneratorsRequired = Bal.RequiredGenerators,
            };
            foreach (SimPlayer p in Order) if (p.Role != Role.Spectator) Result.Stats.Add((p.Id, p.Name, p.Role, p.Stats));
            Emit(null, new GameEvent { Kind = EventKind.Result, A = (int)w, Text = reason });
        }

        /// <summary>Noise (for the HUD), scent and blood trails, terror, breath-holding and the breathing cue.</summary>
        void UpdateSenses(float dt)
        {
            var hunters = new List<SimPlayer>();
            foreach (SimPlayer p in Order) if (p.Role == Role.Hunter && p.Health != Health.Eliminated) hunters.Add(p);
            foreach (SimPlayer p in Order)
            {
                if (p.Role != Role.Survivor) continue;
                float noise = 0f;
                if (p.Health == Health.Healthy || p.Health == Health.Wounded)
                {
                    if (p.HideState == 2) noise = 0f;
                    else noise = p.Gait == Gait.Run ? Balance.Survivor.NoiseRun : p.Gait == Gait.Walk ? Balance.Survivor.NoiseWalk : p.Gait == Gait.Crouch ? Balance.Survivor.NoiseCrouch : Balance.Survivor.NoiseIdle;
                    if (p.Action == ActionKind.Repair) noise = Mathf.Max(noise, Balance.Objectives.RepairNoise);
                }
                p.Noise = noise;

                // Scent (walking or running; crouching leaves none) and blood (wounded or downed) for Zach's nose.
                bool onGround = p.HideState == 0 && (p.Health == Health.Healthy || p.Health == Health.Wounded || p.Health == Health.Downed);
                if (onGround)
                {
                    bool running = p.Move.Sprinting && p.Gait == Gait.Run;
                    bool walking = !running && (p.Gait == Gait.Walk || p.Gait == Gait.Run);
                    if ((running || walking) && Time - p.LastScent >= Balance.Trails.ScentEvery)
                    {
                        p.LastScent = Time;
                        Trails.Add(new TrailPoint { Id = trailSeq++, Pos = p.Pos, T = Time - (walking ? Balance.Trails.WalkHeadStart : 0f), Kind = 0, Who = p.Id });
                    }
                    if ((p.Health == Health.Wounded || p.Health == Health.Downed) && Time - p.LastBlood >= Balance.Trails.BloodEvery)
                    {
                        p.LastBlood = Time;
                        Trails.Add(new TrailPoint { Id = trailSeq++, Pos = p.Pos + new Vector2(RandRange(-1f, 1f), RandRange(-1f, 1f)) * R(6f), T = Time, Kind = 1, Who = p.Id });
                    }
                }

                float terror = 0f;
                foreach (SimPlayer h in hunters) terror = Mathf.Max(terror, 1f - Vector2.Distance(h.Pos, p.Pos) / R(700f));
                p.Terror = Mathf.Clamp01(terror);

                // Breath: holding it hides your breathing; running out makes you gasp.
                p.GaspCd = Mathf.Max(0f, p.GaspCd - dt);
                bool wantsHold = p.HideState == 2 && p.LastCmd.Has(Btn.Space) && p.GaspCd <= 0f;
                if (wantsHold && p.Breath > 0f)
                {
                    p.HoldingBreath = true;
                    p.Breath = Mathf.Max(0f, p.Breath - dt / Balance.Hiding.BreathMax);
                    if (p.Breath <= 0f)
                    {
                        p.HoldingBreath = false;
                        p.GaspCd = Balance.Hiding.GaspCooldown;
                        Emit(Near(p.Pos, 600f), new GameEvent { Kind = EventKind.Gasp, A = p.Id, Pos = p.Pos });
                        foreach (SimPlayer h in hunters)
                            if (Vector2.Distance(h.Pos, p.Pos) < R(Balance.Hiding.GaspHearRadius)) Emit(h.Id, new GameEvent { Kind = EventKind.Breath, Pos = p.Pos, F = 1f });
                    }
                }
                else
                {
                    p.HoldingBreath = false;
                    p.Breath = Mathf.Min(1f, p.Breath + Balance.Hiding.BreathRegen * dt / Balance.Hiding.BreathMax);
                }
                // Zach sees the breath of anyone hiding close by who isn't holding it.
                if (p.HideState == 2 && !p.HoldingBreath && Mathf.Floor(Time / Balance.Hiding.BreathingIntervalSec) != Mathf.Floor((Time - dt) / Balance.Hiding.BreathingIntervalSec))
                    foreach (SimPlayer h in hunters)
                        if (Vector2.Distance(h.Pos, p.Pos) < R(Balance.Hiding.BreathingHearRadius)) Emit(h.Id, new GameEvent { Kind = EventKind.Breath, Pos = p.Pos });
            }
            float cut = Time - Balance.Trails.MaxAgeSec;
            if (Trails.Count > 0 && Trails[0].T < cut) Trails.RemoveAll(t => t.T < cut);
        }

        /// <summary>Chacko was slain by a survivor: Jaden, Plasma and Shane hunt that survivor until they're downed once, or for 30 s.</summary>
        public void Avenge(SimPlayer target)
        {
            Vengeance = target.Id;
            VengeanceT = Balance.Chacko.VengeanceSec;
            foreach (Npc n in Npcs) n.Avenge(this, target);
        }

        void UpdateVengeance(float dt)
        {
            if (Vengeance == 0) return;
            SimPlayer t = Get(Vengeance);
            bool downed = t == null || (t.Health != Health.Healthy && t.Health != Health.Wounded);
            VengeanceT -= dt;
            bool huntersAlive = true;
            foreach (Npc n in Npcs) if ((n.Kind == 5 || n.Kind == 4) && !n.Alive) huntersAlive = false;
            if (downed || VengeanceT <= 0f || !huntersAlive)
            {
                Vengeance = 0;
                foreach (Npc n in Npcs) n.StopAvenging(this);
            }
        }
    }
}
