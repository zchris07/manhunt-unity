using UnityEngine;

namespace Vision.Visibility
{
    /// <summary>
    /// The player's own vision: flashlight cone, proximity circle, 360° line of sight and the optional
    /// see-through cone. Everything here is drawn into the mask's B channel except line of sight (G).
    /// Ranges are in design units and multiplied by the transform scale (see <see cref="Scale"/>).
    /// </summary>
    public sealed class VisionViewer : MonoBehaviour
    {
        [Header("Flashlight cone")]
        [Tooltip("Height the flashlight is held at, in design units (for surface shading).")]
        public float lightHeight = 1.3f;
        [Range(5f, 90f)] public float coneHalfAngleDeg = 50f;
        public float coneRange = 18f;
        [Range(0f, 1f)] public float coneFalloffStart = 0.45f;
        [Tooltip("The beam reaches the edge of the screen at full strength, instead of stopping (and fading) at coneRange.")]
        public bool reachScreenEdge = true;
        [Tooltip("Outer fraction of the half angle over which the beam fades to dark at its sides.")]
        [Range(0f, 1f)] public float coneEdgeSoftness = 0.35f;

        [Header("Proximity circle")]
        public float proximityRadius = 1.8f;
        [Range(0f, 1f)] public float proximityFalloffStart = 0.55f;

        [Header("360° line of sight (never lights anything itself)")]
        public float lineOfSightRange = 30f;

        [Header("See-through cone (ignores occluders)")]
        public bool seeThroughEnabled;
        [Tooltip("Scales how far the beam and line of sight reach (the original's 0.6 while downed).")]
        [Range(0.1f, 1f)] public float visionMultiplier = 1f;
        [Range(5f, 90f)] public float seeThroughHalfAngleDeg = 22f;
        public float seeThroughRange = 8f;
        [Range(0f, 1f)] public float seeThroughStrength = 0.7f;
        [Tooltip("How far the see-through light has grown in (0-1): it grows out of the torch to its full reach and brightness.")]
        [Range(0f, 1f)] public float seeThroughK = 1f;

        [Header("Teammates (a survivor's view)")]
        [Tooltip("Ground positions of teammates: each gets a small bright disc of the viewer's own light that shows them through the fog.")]
        public readonly System.Collections.Generic.List<Vector2> allyBodies = new System.Collections.Generic.List<Vector2>(8);
        public float allyBodyRadius = 1.2f;
        [Range(0f, 1f)] public float allyBodyIntensity = 0.55f;

        /// <summary>Facing on the ground plane, set by the controller.</summary>
        public Vector2 Facing { get; set; } = Vector2.up;

        public Vector2 PlanePosition => VisionWorld.ToPlane(transform.position);

        /// <summary>World units per design unit (the level root's scale).</summary>
        public float Scale => transform.lossyScale.x;

        public float FacingAngle => Mathf.Atan2(Facing.y, Facing.x);
    }
}
