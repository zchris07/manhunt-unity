namespace Vision.Player
{
    /// <summary>
    /// The mode the game is in. The only mode so far is the original's testing mode: every item and never used up,
    /// no win condition. Speed mode (testing) makes movement six times as fast (+500%) and keeps the sprint meter full.
    /// </summary>
    public static class GameSession
    {
        public static bool TestingMode;
        public static bool SpeedMode;

        /// <summary>Movement multiplier in speed mode.</summary>
        public const float SpeedMultiplier = 6f;

        /// <summary>
        /// The original's testing kit (bottle and book nine each, goggles, shotgun, mini shield, Mr Beast bar, gas trap;
        /// a Doctor Pepper in place of the pistol this game doesn't have), never used up.
        /// </summary>
        public static readonly (ItemType item, int count)[] TestKit =
        {
            (ItemType.Bottle, 9), (ItemType.Book, 9), (ItemType.Goggles, 1), (ItemType.Shotgun, 1),
            (ItemType.MiniShield, 1), (ItemType.MrBeastBar, 1), (ItemType.Trap, 1), (ItemType.DoctorPepper, 1),
        };

        public static void ApplyTestKit(PlayerStats stats)
        {
            if (stats == null) return;
            stats.inventory.Clear();
            foreach ((ItemType item, int count) in TestKit) stats.inventory.Add(item, count);
            stats.inventory.Infinite = true;
        }

        public static void Reset()
        {
            TestingMode = false;
            SpeedMode = false;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Reset();
    }
}
