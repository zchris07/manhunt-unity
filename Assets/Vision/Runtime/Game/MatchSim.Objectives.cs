using System.Collections.Generic;
using UnityEngine;

namespace Vision.Game
{
    /// <summary>Generators, the gate, escaping, the win check, senses and vengeance (objectives.ts, senses.ts).</summary>
    public sealed partial class MatchSim
    {
        /// <summary>The survivor Jaden, Plasma and Shane are hunting for Chacko's sake (0 nobody), and seconds left.</summary>
        /// <summary>The survivor who last attacked Chacko, while any NPC is still after them (0: nobody).</summary>
        public int Vengeance;

        void UpdateObjectives(float dt)
        {
            // Generators: each living survivor past the first adds a step of speed; a ghost in a body lending a hand, two
            // thirds of one (and only while a living survivor works it).
            var workers = new int[Gens.Length];
            var helpers = new int[Gens.Length];
            foreach (SimPlayer p in Order)
                if (p.Role == Role.Survivor && p.Action == ActionKind.Repair && p.ActionTarget >= 0 && p.ActionTarget < Gens.Length)
                {
                    if (p.Ghost) helpers[p.ActionTarget]++;
                    else workers[p.ActionTarget]++;
                }
            for (int gi = 0; gi < Gens.Length; gi++)
            {
                GenState g = Gens[gi];
                g.Workers = workers[gi] + helpers[gi];
                if (g.Repaired) continue;
                if (workers[gi] > 0 && !Gate.Powered)
                {
                    float mul = 1f + Balance.Objectives.CoopStep * (workers[gi] - 1 + Balance.Ghost.HelpShare * helpers[gi]);
                    g.Progress += mul / Bal.RepairTime * dt;
                    // Testing mode: a generator starts the moment anyone works it.
                    if (TestMode) g.Progress = 1f;
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

            // Enough generators running: the gates have power (checked every tick, so a generator set running by a tool counts too).
            if (!Gate.Powered)
            {
                int repaired = 0;
                foreach (GenState x in Gens) if (x.Repaired) repaired++;
                if (repaired >= Bal.RequiredGenerators) PowerGates();
            }

            // The exit gates, the end game, the hatch, and getting out.
            int survivorsNow = 0;
            foreach (SimPlayer p in Order) if (p.Role == Role.Survivor) survivorsNow++;
            SurvivorsAtStart = Mathf.Max(SurvivorsAtStart, survivorsNow);
            UpdateGates(dt);
            UpdateEscapes();
            UpdateHatch();
            UpdateEndGame(dt);

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
            bool killed = false;
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
                    if (p.Killed) killed = true;
                }
            }
            Winner w = MatchRules.CheckWin(survivors, standing, escaped, eliminated, hunters, huntersGone, Bal.EscapeNeeded, out string reason);
            if (w == Winner.None) return;
            if (w == Winner.Survivors && killed && huntersGone >= hunters) reason = hunters > 1 ? "Every Zach was killed" : "Zach was killed";
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

        /// <summary>Noise (for the HUD), scent and blood trails, and terror.</summary>
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
                    noise = p.Gait == Gait.Run ? Balance.Survivor.NoiseRun : p.Gait == Gait.Walk ? Balance.Survivor.NoiseWalk : p.Gait == Gait.Crouch ? Balance.Survivor.NoiseCrouch : Balance.Survivor.NoiseIdle;
                    if (p.Action == ActionKind.Repair) noise = Mathf.Max(noise, Balance.Objectives.RepairNoise);
                }
                p.Noise = noise;

                // Scent (walking or running; crouching leaves none) and blood (wounded or downed) for Zach's nose.
                bool onGround = p.Health == Health.Healthy || p.Health == Health.Wounded || p.Health == Health.Downed;
                if (onGround)
                {
                    bool running = p.Move.Sprinting && p.Gait == Gait.Run;
                    bool walking = !running && (p.Gait == Gait.Walk || p.Gait == Gait.Run);
                    if ((running || walking) && Time - p.LastScent >= Balance.Trails.ScentEvery)
                    {
                        p.LastScent = Time;
                        p.ScentDist += Vector2.Distance(p.Pos, p.ScentAt);
                        p.ScentAt = p.Pos;
                        Trails.Add(new TrailPoint { Id = trailSeq++, Pos = p.Pos, T = Time - (walking ? Balance.Trails.WalkHeadStart : 0f), Kind = 0, Who = p.Id, Dist = p.ScentDist });
                    }
                    if ((p.Health == Health.Wounded || p.Health == Health.Downed) && Time >= p.NextBlood)
                    {
                        // A drop at a random moment, landing somewhere about them. Standing still, the drops stop piling up
                        // under them after a few; once they've moved on, they start again.
                        p.NextBlood = Time + RandRange(Balance.Trails.BloodEveryMin, Balance.Trails.BloodEveryMax);
                        if (Vector2.Distance(p.Pos, p.BloodAt) > R(Balance.Trails.BloodPoolRadius))
                        {
                            p.BloodAt = p.Pos;
                            p.BloodPool = 0;
                        }
                        if (p.BloodPool < Balance.Trails.BloodPoolMax)
                        {
                            p.BloodPool++;
                            float a = RandRange(0f, Mathf.PI * 2f), r = Mathf.Sqrt(RandRange(0f, 1f)) * R(Balance.Trails.BloodSpread);
                            Trails.Add(new TrailPoint { Id = trailSeq++, Pos = p.Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, T = Time, Kind = 1, Who = p.Id });
                        }
                    }
                }

                float terror = 0f;
                foreach (SimPlayer h in hunters) terror = Mathf.Max(terror, 1f - Vector2.Distance(h.Pos, p.Pos) / R(700f));
                p.Terror = Mathf.Clamp01(terror);
            }
            float cut = Time - Balance.Trails.MaxAgeSec;
            if (Trails.Count > 0 && Trails[0].T < cut) Trails.RemoveAll(t => t.T < cut);
        }

        /// <summary>
        /// Someone attacked Chacko (a survivor, or Zach killing him): Njaaron, Jaden and Plasma race after them from wherever
        /// they are, with no time limit, and keep at them until each has dealt them a third of their health himself, or he's
        /// slain, or they're down (by anything); Shane runs after them until he's stunned and driven off (two bottles, a
        /// shotgun blast or galaxy gas; Zach's machete or Soundcloud Burst). Each keeps his own grudge (<see cref="Npc.GrudgeTarget"/>).
        /// Those four only (the user's rule): no other NPC is touched by it.
        /// </summary>
        public void Avenge(SimPlayer target)
        {
            Vengeance = target.Id;
            foreach (Npc n in Npcs) n.Avenge(this, target);
        }

        void UpdateVengeance(float dt)
        {
            if (Vengeance == 0) return;
            foreach (Npc n in Npcs) if (n.GrudgeTarget == Vengeance) return;
            Vengeance = 0;
        }
    }
}
