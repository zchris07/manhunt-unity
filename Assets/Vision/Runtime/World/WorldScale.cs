namespace Vision.World
{
    /// <summary>
    /// Everything in the level is authored in real-world design units (a person is 1.8 m tall) and
    /// the level root is scaled by <see cref="S"/>. Occluders, lights, the viewer and the colliders all
    /// read their transform scale, so changing this one number resizes the whole world while the
    /// camera framing stays the same.
    /// </summary>
    public static class WorldScale
    {
        public const float S = 2f;
    }
}
