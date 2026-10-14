using UnityEngine;

namespace Vision.Game
{
    /// <summary>
    /// The graveyard's gates: locked at the start, opened only by a survivor picking the lock (three spinning-tick timing
    /// checks; two misses and it's back to the first), after which they swing open and anyone on the survivors' side
    /// opens and closes them freely. Zach can't open them; closed again after they were unlocked, he can only smash them
    /// in (four full charged swings' worth of hits). The fence round the graveyard: survivors can't climb it, Zach can,
    /// slowly.
    /// </summary>
    public sealed partial class MatchSim
    {
        /// <summary>The graveyard gates still locked (the rest are false).</summary>
        public readonly bool[] DoorLocked;

        /// <summary>How fast the tick spins at each of the three checks (degrees a second), and how wide its mark is (degrees).</summary>
        public static float PickSpeed(int stage) => Balance.Graveyard.PickSpeeds[Mathf.Clamp(stage, 0, Balance.Graveyard.PickSpeeds.Length - 1)];
        public static float PickWidth(int stage) => Balance.Graveyard.PickWidths[Mathf.Clamp(stage, 0, Balance.Graveyard.PickWidths.Length - 1)];

        /// <summary>Where the spinning tick is (degrees clockwise from the top), or 0 while it waits to start.</summary>
        public static float PickNeedle(SimPlayer p) => p.PickT <= 0f ? 0f : Mathf.Repeat(PickSpeed(p.PickStage) * p.PickT * p.PickDir, 360f);

        void StartPick(SimPlayer p, int gate)
        {
            StartAction(p, ActionKind.PickLock, 0f, gate);
            p.PickStage = 0;
            p.PickMiss = 0;
            p.PickResult = 0;
            NewPickSpin(p);
        }

        /// <summary>A fresh check: the tick back at the top after a short breath, the mark somewhere new, the spin turning about.</summary>
        void NewPickSpin(SimPlayer p)
        {
            p.PickT = -Balance.Graveyard.PickPause;
            p.PickZone = 110f + (float)Rng.NextDouble() * 140f;
            p.PickDir = p.PickStage % 2 == 0 ? 1 : -1;
            if (p.PickDir < 0) p.PickZone = 360f - p.PickZone;
        }

        /// <summary>The survivor tries to stop the tick on the mark.</summary>
        void PickStrike(SimPlayer p)
        {
            if (p.Action != ActionKind.PickLock || p.PickT <= 0f) return;
            int stage = p.PickStage;
            // The mark's half width, and half a tick's travel (presses land on the 30 Hz tick).
            float tol = PickWidth(stage) * 0.5f + PickSpeed(stage) / Balance.Net.TickHz * 0.5f;
            bool hit = Mathf.Abs(Mathf.DeltaAngle(PickNeedle(p), p.PickZone)) <= tol;
            p.PickResultT = Time;
            if (hit)
            {
                p.PickStage++;
                p.PickResult = 1;
                if (p.PickStage >= Balance.Graveyard.PickChecks)
                {
                    UnlockGate(p, p.ActionTarget);
                    return;
                }
                NewPickSpin(p);
                Emit(p.Id, new GameEvent { Kind = EventKind.Talk, A = p.Id, Text = "pick_hit", Pos = p.Pos });
                return;
            }
            p.PickMiss++;
            if (p.PickMiss >= Balance.Graveyard.PickMisses)
            {
                // Two misses: the pins drop, back to the first check.
                p.PickStage = 0;
                p.PickMiss = 0;
                p.PickResult = -2;
                NewPickSpin(p);
                Emit(p.Id, new GameEvent { Kind = EventKind.Talk, A = p.Id, Text = "pick_reset", Pos = p.Pos });
                return;
            }
            p.PickResult = -1;
            Emit(p.Id, new GameEvent { Kind = EventKind.Talk, A = p.Id, Text = "pick_miss", Pos = p.Pos });
        }

        void UpdatePick(SimPlayer p, float dt)
        {
            int g = p.ActionTarget;
            if (g < 0 || g >= DoorLocked.Length || !DoorLocked[g] || !p.CanAct || p.HideState != 0 ||
                SimMap.SegmentDistance(p.Pos, Map.Doors[g].A, Map.Doors[g].B) > R(Balance.Reach.Door + 20f))
            {
                CancelAction(p);
                return;
            }
            p.PickT += dt;
        }

        /// <summary>The lock gives: the chain falls away and the gates swing open, for good unlocked.</summary>
        void UnlockGate(SimPlayer p, int g)
        {
            p.Action = ActionKind.None;
            p.ActionTarget = -1;
            if (g < 0 || g >= DoorLocked.Length) return;
            DoorLocked[g] = false;
            SetDoor(g, true);
            Vector2 at = (Map.Doors[g].A + Map.Doors[g].B) * 0.5f;
            Noise(at, 900f, "gate_unlock");
            Tell(p, "The graveyard gates are open");
        }

        /// <summary>Survivors, and Zach (slowly), over the fence: whether a body here is climbing, and how much slower than a log.</summary>
        public static float VaultFor(bool hunter, float slow, bool hunterOnly) =>
            hunterOnly ? (hunter ? Balance.Graveyard.ZachFenceSlow : 0f) : Mathf.Max(1f, slow);
    }
}
