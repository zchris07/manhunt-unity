using UnityEngine;

namespace Vision.Game
{
    /// <summary>
    /// Ghosts (the user's rule): a survivor sacrificed (on a stake, or gone into the woods) comes back a little later as a
    /// glowing white ghost of themselves, with a sprint that never runs out. They can't be hurt, can't pick anything up or
    /// work anything, and count as sacrificed for the night's result; nothing they do touches an NPC. Their one power is the
    /// Ghost Pulse (left click or F, every 15 s): a red wave like Zach's Soundcloud Burst that rolls out through every wall,
    /// and everyone it passes over (survivors and Zach) gets a random picture flashed and shaken at them for 0.6 s, while
    /// the clip plays out whole for the ghost and anyone near. There's never a time limit on the night.
    /// </summary>
    public sealed partial class MatchSim
    {
        /// <summary>
        /// Puts a sacrificed survivor back as a ghost after a moment: on their stake, or (out of the woods) back at the
        /// survivors' spawn.
        /// </summary>
        void ScheduleGhost(SimPlayer p, string why)
        {
            if (p.Role != Role.Survivor || (why != "stake" && why != "woods")) return;
            p.GhostAt = Time + (why == "woods" ? Balance.Boundary.SacrificedHold + 1f : Balance.Ghost.RespawnDelay);
            p.GhostSpawn = why == "woods" ? (Map.SurvivorSpawns.Count > 0 ? Map.SurvivorSpawns[0] : Vector2.zero) : p.Pos;
        }

        void UpdateGhosts(float dt)
        {
            foreach (SimPlayer p in Order)
            {
                if (p.Role != Role.Survivor || p.Health != Health.Eliminated) continue;
                if (!p.Ghost)
                {
                    if (p.GhostAt > 0f && Time >= p.GhostAt) MakeGhost(p);
                    continue;
                }
                p.GhostCd = Mathf.Max(0f, p.GhostCd - dt);
                p.Move.Stamina = MoveState.MaxStamina(Role.Survivor, 0f);
            }
        }

        /// <summary>Back as a ghost: the light comes back on, where they were staked (or at the spawn, out of the woods).</summary>
        public void MakeGhost(SimPlayer p)
        {
            p.Ghost = true;
            p.GhostAt = 0f;
            p.GhostCd = 0f;
            p.LostInWoods = false;
            p.ShutdownT = 0f;
            p.BoundaryTickT = 0f;
            p.Zone = 0;
            p.Hp = 1f;
            p.Shield = 0f;
            p.StunT = 0f;
            p.ScareT = 0f;
            p.GogglesOn = false;
            p.JarvisT = 0f;
            p.SelSlot = -1;
            Place(p, p.GhostSpawn);
            Emit(null, new GameEvent { Kind = EventKind.Talk, A = p.Id, Text = "ghost", Pos = p.Pos });
            Feed($"{p.Name} came back as a ghost");
            Tell(p, "You're a ghost: left click or F sends out a Ghost Pulse");
        }

        /// <summary>The Ghost Pulse: a red wave out along the aim, through every wall (15 s between them).</summary>
        public bool TryGhostPulse(SimPlayer p, float aim)
        {
            if (!p.Ghost || p.GhostCd > 0f) return false;
            p.GhostCd = TestMode ? 0.5f : Balance.Ghost.Cooldown;
            Bursts.Add(new BurstWave { Origin = p.Pos, Dir = new Vector2(Mathf.Cos(aim), Mathf.Sin(aim)), T0 = Time, By = p.Id, Ghost = true });
            // The clip plays out whole from the ghost, for them and anyone near enough to hear it.
            Emit(Near(p.Pos, Balance.Ghost.HearRadius), new GameEvent { Kind = EventKind.Talk, A = p.Id, Text = "ghost_pulse", Pos = p.Pos, F = aim });
            return true;
        }

        /// <summary>A ghost's wave reached a player: a picture flashed at them (survivors still in it and Zach alike).</summary>
        void GhostPulseHit(BurstWave b, SimPlayer p)
        {
            p.ScareT = Mathf.Max(p.ScareT, Balance.Ghost.FlashTime);
            Emit(p.Id, new GameEvent { Kind = EventKind.Scare, A = b.By, B = 1 });
        }

        /// <summary>Whether a ghost's pulse can reach this player (anyone still playing: never a ghost, the escaped or the gone).</summary>
        static bool GhostPulseTarget(SimPlayer p) =>
            p.Role == Role.Hunter ? p.Health != Health.Eliminated
            : p.Role == Role.Survivor && !p.Ghost && p.Health != Health.Escaped && p.Health != Health.Eliminated;
    }
}
