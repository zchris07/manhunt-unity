using UnityEngine;

namespace Vision.Game
{
    /// <summary>Match numbers derived from the lobby shape by the original's auto-balance (<c>resolveBalance</c>).</summary>
    public readonly struct ResolvedBalance
    {
        public readonly int Hunters, Survivors;
        public readonly float Pressure, Scale;
        /// <summary>Every generator on the map must be started; this is how many there are.</summary>
        public readonly int RequiredGenerators;
        public readonly float RepairTime;
        /// <summary>Multiplier on Zach's walk and sprint.</summary>
        public readonly float HunterSpeedMul;
        public readonly float StunMul;
        public readonly int EscapeNeeded;

        public ResolvedBalance(int hunters, int survivors, float pressure, float scale, int required, float repairTime, float hunterSpeedMul, float stunMul, int escapeNeeded)
        {
            Hunters = hunters;
            Survivors = survivors;
            Pressure = pressure;
            Scale = scale;
            RequiredGenerators = required;
            RepairTime = repairTime;
            HunterSpeedMul = hunterSpeedMul;
            StunMul = stunMul;
            EscapeNeeded = escapeNeeded;
        }
    }

    public enum Winner : byte { None, Survivors, Hunters }

    /// <summary>The original's rules that are pure arithmetic: auto-balance, Zach's speed and regeneration multipliers, the win check.</summary>
    public static class MatchRules
    {
        /// <summary>
        /// Auto-balance: pressure P = survivors / hunters against P0 = 4, scale = sqrt(P / P0) clamped. More survivors per
        /// hunter means longer repairs, a slightly faster Zach and shorter stuns.
        /// </summary>
        public static ResolvedBalance Resolve(int survivors, int hunters)
        {
            int S = Mathf.Max(1, survivors), H = Mathf.Max(1, hunters);
            float pressure = (float)S / H;
            float scale = Mathf.Clamp(Mathf.Sqrt(pressure / Balance.Scaling.P0), Balance.Scaling.MinScale, Balance.Scaling.MaxScale);
            int required = Mathf.Clamp(Mathf.CeilToInt(S / Mathf.Sqrt(H)) + 1, Balance.Scaling.RequiredGenMin, Balance.Scaling.RequiredGenMax);
            float repair = Balance.Objectives.RepairTime * Mathf.Clamp(scale, Balance.Scaling.RepairTimeMin, Balance.Scaling.RepairTimeMax);
            float hunterSpeed = Mathf.Clamp(1f + Balance.Scaling.HunterSpeedSlope * (scale - 1f), Balance.Scaling.HunterSpeedMin, Balance.Scaling.HunterSpeedMax);
            float stun = Mathf.Clamp(1f / scale, Balance.Scaling.StunMin, Balance.Scaling.StunMax);
            int escape = Mathf.Max(1, Mathf.CeilToInt(S * Balance.World.EscapeFraction - 1e-6f));
            return new ResolvedBalance(H, S, pressure, scale, required, repair, hunterSpeed, stun, escape);
        }

        /// <summary>Zach's permanent speed bonus from the survivors he has staked.</summary>
        public static float HunterStakeMul(int stakes) => 1f + stakes * Balance.Hunter.StakeBuff;

        /// <summary>Zach's speed from his health: 10% slower per 25% of the bar gone, 5% more per time he was put down (at most 20%).</summary>
        public static float HunterHealthMul(float hp, int downs)
        {
            float lost = (1f - Mathf.Clamp01(hp)) / Balance.Hunter.Health.SpeedStep;
            return Mathf.Max(0.1f, 1f - lost * Balance.Hunter.Health.SpeedPerStep - Mathf.Min(Balance.Hunter.Health.DownPenaltyMax, downs * Balance.Hunter.Health.DownPenalty));
        }

        /// <summary>Health recovery multiplier from the Hemp Battery: +10% times the charge left.</summary>
        public static float HempRegenMul(float charge) => 1f + Balance.Hunter.Hemp.RegenBoost * Mathf.Clamp01(charge);

        /// <summary>Soundcloud Burst lens curvature at lateral offset s (original units).</summary>
        public static float BurstSag(float s)
        {
            float half = Balance.Hunter.Burst.Width / 2f;
            float k = Mathf.Min(Mathf.Abs(s), half) / half;
            return 0.12f * half * k * k;
        }

        /// <summary>
        /// The original's win check without its time limit. The night goes on while any survivor is still on their feet (or
        /// hiding); when none is, the survivors win if enough escaped. If every hunter has left, the survivors win.
        /// </summary>
        public static Winner CheckWin(int survivors, int standing, int escaped, int eliminated, int hunters, int huntersGone, int escapeNeeded, out string reason)
        {
            reason = "";
            int need = Mathf.Min(escapeNeeded, survivors);
            if (survivors > 0 && standing == 0)
            {
                int down = survivors - escaped - eliminated;
                reason = $"{escaped} escaped, {eliminated} eliminated{(down > 0 ? $", {down} left incapacitated" : "")}";
                return escaped >= need ? Winner.Survivors : Winner.Hunters;
            }
            if (hunters > 0 && huntersGone >= hunters)
            {
                reason = "The hunters left";
                return Winner.Survivors;
            }
            return Winner.None;
        }
    }

    /// <summary>
    /// The settings of the match being played: testing mode, speed mode and the pace. One instance is current; tests and a
    /// loopback host and client can make their own.
    /// </summary>
    public sealed class MatchState
    {
        public static MatchState Current = new MatchState();

        public bool TestingMode;
        public bool SpeedMode;
        /// <summary>Movement speed scale: 1 is the original's pace; <see cref="Scale.HumanPace"/> is this game's earlier pace.</summary>
        public float Pace = 1f;

        const string PaceKey = "manhunt.pace";

        /// <summary>The pace the player last chose (the original's, the first time).</summary>
        public static float SavedPace()
        {
            try { return Mathf.Clamp(PlayerPrefs.GetFloat(PaceKey, 1f), Scale.MinPace, Scale.MaxPace); }
            catch { return 1f; }
        }

        public void SetPace(float pace, bool save = true)
        {
            Pace = Mathf.Clamp(pace, Scale.MinPace, Scale.MaxPace);
            if (!save) return;
            try { PlayerPrefs.SetFloat(PaceKey, Pace); } catch { }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = new MatchState();
    }
}
