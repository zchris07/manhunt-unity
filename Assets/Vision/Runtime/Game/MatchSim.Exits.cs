using System.Collections.Generic;
using UnityEngine;

namespace Vision.Game
{
    /// <summary>
    /// The ways out (the user's rules, after Dead by Daylight): two exit gates, the building's and a second one of its own
    /// beside Miga Sushi, both powered once every generator runs and each opened at its lever (in half the original's time);
    /// an open gate stays open and anyone can leave through it. Once a gate is open the end game starts: a timer runs and
    /// anyone still on the map when it ends is taken by the Entity. With only one survivor left on the map the hatch by the
    /// lake's dock opens: jumping in is an instant escape, unless Zach gets there first and slams it shut, which powers the
    /// gates and starts the end game. Nothing else ends the night: there's no time limit.
    /// </summary>
    public sealed partial class MatchSim
    {
        /// <summary>The second exit gate (beside Miga Sushi).</summary>
        public readonly GateState Gate2 = new GateState();
        /// <summary>The hatch: open (one survivor left), or slammed shut by Zach.</summary>
        public bool HatchOpen, HatchClosed;
        /// <summary>The end game's seconds left (negative: not started).</summary>
        public float EndGameT = -1f;
        /// <summary>How many survivors the night started with (the hatch only opens for the last of two or more).</summary>
        public int SurvivorsAtStart;

        public bool EndGame => EndGameT >= 0f;

        /// <summary>Gate <paramref name="i"/> (0 the building's, 1 Miga Sushi's).</summary>
        public GateState GateAt(int i) => i == 1 ? Gate2 : Gate;
        public bool HasGateAt(int i) => i == 1 ? Map.HasGate2 : Map.HasGate;
        public Vector2 LeverAt(int i) => i == 1 ? Map.Lever2 : Map.Lever;
        bool InGateZone(int i, Vector2 p) => i == 1 ? Map.ExitZone2.Contains(p) : Geo.InExitZone(p);

        /// <summary>Every generator ran (or Zach shut the hatch): both gates have power.</summary>
        void PowerGates()
        {
            if (Gate.Powered && (Gate2.Powered || !Map.HasGate2)) return;
            Gate.Powered = true;
            Gate2.Powered = Map.HasGate2;
            Emit(null, new GameEvent { Kind = EventKind.GatePowered });
            Feed(Map.HasGate2 ? "The exit gates have power" : "The exit gate has power");
            foreach (SimPlayer p in Order) if (p.Action == ActionKind.Repair) CancelAction(p);
        }

        void UpdateGates(float dt)
        {
            for (int gi = 0; gi < 2; gi++)
            {
                GateState g = GateAt(gi);
                if (!HasGateAt(gi) || !g.Powered || g.Open) continue;
                var openers = new List<SimPlayer>();
                foreach (SimPlayer p in Order) if (p.Action == ActionKind.OpenGate && p.ActionTarget == gi) openers.Add(p);
                if (openers.Count == 0) continue;
                g.Progress = Mathf.Min(1f, g.Progress + dt / Balance.Objectives.GateOpenTime);
                if (g.Progress < 1f) continue;
                g.Open = true;
                foreach (SimPlayer p in openers) CancelAction(p);
                Emit(null, new GameEvent { Kind = EventKind.GateOpen, A = gi });
                Feed(gi == 1 ? "The gate by Miga Sushi is open" : "The gate is open");
                StartEndGame();
            }
        }

        /// <summary>The end game: anyone still on the map when the timer runs out is taken by the Entity.</summary>
        public void StartEndGame()
        {
            if (EndGame || TestMode) return;
            EndGameT = Balance.Objectives.EndGameTime;
            Feed($"The end game has begun: {Mathf.RoundToInt(Balance.Objectives.EndGameTime)} s until the Entity comes");
        }

        void UpdateEndGame(float dt)
        {
            if (!EndGame) return;
            EndGameT = Mathf.Max(0f, EndGameT - dt);
            if (EndGameT > 0f) return;
            foreach (SimPlayer p in Order)
                if (p.Role == Role.Survivor && p.Health != Health.Escaped && p.Health != Health.Eliminated)
                    Eliminate(p, "entity");
            EndGameT = -1f;
        }

        /// <summary>Through an open gate's yard, or into the open hatch: out.</summary>
        void UpdateEscapes()
        {
            foreach (SimPlayer p in Order)
            {
                if (p.Role != Role.Survivor || p.Ghost || (p.Health != Health.Healthy && p.Health != Health.Wounded) || p.HideState != 0) continue;
                bool gate = (Gate.Open && InGateZone(0, p.Pos)) || (Gate2.Open && Map.HasGate2 && InGateZone(1, p.Pos));
                bool hatch = HatchOpen && Map.HasHatch && Vector2.Distance(p.Pos, Map.Hatch) < R(Balance.Objectives.HatchRadius);
                if (!gate && !hatch) continue;
                Escape(p, hatch);
            }
        }

        public void Escape(SimPlayer p, bool hatch)
        {
            CancelAction(p);
            p.Health = Health.Escaped;
            p.Stats.Outcome = "escaped";
            p.EndedTime = Time;
            p.Spectating = DefaultSpectateTarget(p.Id);
            Emit(null, new GameEvent { Kind = EventKind.Escaped, A = p.Id, Text = hatch ? "hatch" : null });
            Feed(hatch ? $"{p.Name} jumped into the hatch" : $"{p.Name} escaped");
            if (hatch)
            {
                HatchOpen = false;
                HatchClosed = true;
            }
        }

        /// <summary>One survivor left on the map (of two or more): the hatch by the dock opens.</summary>
        void UpdateHatch()
        {
            if (!Map.HasHatch || HatchOpen || HatchClosed || TestMode || SurvivorsAtStart < 2) return;
            int left = 0;
            foreach (SimPlayer p in Order)
                if (p.Role == Role.Survivor && p.Health != Health.Escaped && p.Health != Health.Eliminated) left++;
            if (left != 1) return;
            HatchOpen = true;
            Feed("The hatch is open, by the dock");
        }

        /// <summary>Zach slams the open hatch shut: the gates get power and the end game starts.</summary>
        public bool CloseHatch(SimPlayer h)
        {
            if (!HatchOpen || h.Role != Role.Hunter || !h.CanAct) return false;
            HatchOpen = false;
            HatchClosed = true;
            Noise(Map.Hatch, 1200f, "hatch_slam");
            Feed($"{h.Name} slammed the hatch shut");
            PowerGates();
            StartEndGame();
            return true;
        }
    }
}
