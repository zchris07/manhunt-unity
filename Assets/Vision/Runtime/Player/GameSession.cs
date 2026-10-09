using Vision.Game;

namespace Vision.Player
{
    /// <summary>
    /// Shortcuts to the current <see cref="MatchState"/>: testing mode (every item and ability, never used up, no win check,
    /// T switches between Zach and a survivor) and speed mode (testing: six times the speed, the sprint meter always full).
    /// </summary>
    public static class GameSession
    {
        public static bool TestingMode
        {
            get => MatchState.Current.TestingMode;
            set => MatchState.Current.TestingMode = value;
        }

        public static bool SpeedMode
        {
            get => MatchState.Current.SpeedMode;
            set => MatchState.Current.SpeedMode = value;
        }

        /// <summary>Movement multiplier in speed mode (+500%).</summary>
        public const float SpeedMultiplier = 6f;

        public static void Reset()
        {
            MatchState.Current.TestingMode = false;
            MatchState.Current.SpeedMode = false;
        }
    }
}
