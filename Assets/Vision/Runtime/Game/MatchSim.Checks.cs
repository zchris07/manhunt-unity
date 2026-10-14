using UnityEngine;

namespace Vision.Game
{
    /// <summary>
    /// A generator's skill checks (<see cref="Balance.SkillCheck"/>): repairing, every eight seconds a dial flashes up and a
    /// fast tick sweeps round it once; Space or a click on the mark takes four seconds off the repair. Missing, or letting
    /// the tick get all the way round, costs nothing. The sim judges each press, as the lock pick's.
    /// </summary>
    public sealed partial class MatchSim
    {
        /// <summary>Where the tick is (degrees clockwise from the top), or 0 while it waits to start.</summary>
        public static float CheckNeedle(SimPlayer p) => p.CheckT <= 0f ? 0f : Mathf.Repeat(Balance.SkillCheck.Speed * p.CheckT * p.CheckDir, 360f);

        /// <summary>Counted while they repair: a check comes up every few seconds, and runs out after one sweep.</summary>
        void UpdateCheck(SimPlayer p, float dt)
        {
            if (!p.CheckOn)
            {
                p.RepairClock += dt;
                if (p.RepairClock < Balance.SkillCheck.Every) return;
                p.CheckOn = true;
                p.CheckT = -Balance.SkillCheck.Lead;
                p.CheckDir = Rng.NextDouble() < 0.5 ? 1 : -1;
                p.CheckZone = 110f + (float)Rng.NextDouble() * 140f;
                if (p.CheckDir < 0) p.CheckZone = 360f - p.CheckZone;
                return;
            }
            p.CheckT += dt;
            if (p.CheckT > 0f && Balance.SkillCheck.Speed * p.CheckT >= 360f) EndCheck(p, false);
        }

        /// <summary>Space or a click while a check is up (ignored while it waits to start).</summary>
        void CheckStrike(SimPlayer p)
        {
            if (!p.CheckOn || p.CheckT <= 0f) return;
            // The mark's half width, and half a tick's travel (presses land on the 30 Hz tick).
            float tol = Balance.SkillCheck.Width * 0.5f + Balance.SkillCheck.Speed / Balance.Net.TickHz * 0.5f;
            EndCheck(p, Mathf.Abs(Mathf.DeltaAngle(CheckNeedle(p), p.CheckZone)) <= tol);
        }

        void EndCheck(SimPlayer p, bool hit)
        {
            p.CheckOn = false;
            p.RepairClock = 0f;
            p.CheckResult = hit ? 1 : -1;
            p.CheckResultT = Time;
            if (hit && p.ActionTarget >= 0 && p.ActionTarget < Gens.Length)
            {
                GenState g = Gens[p.ActionTarget];
                g.Progress = Mathf.Min(1f, g.Progress + Balance.SkillCheck.Bonus / Bal.RepairTime);
            }
            Emit(p.Id, new GameEvent { Kind = EventKind.Talk, A = p.Id, Text = hit ? "pick_hit" : "pick_miss", Pos = p.Pos });
        }
    }
}
