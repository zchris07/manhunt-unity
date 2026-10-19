namespace Vision.World
{
    /// <summary>
    /// The ruins' bricks: real proportions at the low-poly scale, laid two to the wall's depth (back to back). The walls
    /// themselves are laid by <see cref="RuinBricks"/> from a <see cref="RuinPlan"/> (<see cref="RuinPlanner"/>).
    /// </summary>
    public static partial class LowPolyModels
    {
        /// <summary>A brick's length along the wall, a course's height, and a brick's depth into it (two to the wall).</summary>
        public const float BrickLength = 0.2f, BrickCourse = 0.088f, BrickDepth = 0.144f;
        /// <summary>A ruin's walls: two bricks back to back.</summary>
        public const float WallThickness = 2f * BrickDepth;
    }
}
