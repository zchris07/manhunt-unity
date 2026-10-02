using UnityEngine;
using Vision.Visibility;

namespace Vision.Characters
{
    /// <summary>
    /// Marks a character (or animal) that casts a soft shadow away from each light shining on it: the
    /// viewer's flashlight and nearby light sources. Shadows are purely cosmetic: they are drawn into
    /// the vision mask's alpha channel and darken already-lit ground. They never block light or sight,
    /// and never hide anything. An entity's shadow is only drawn while the entity itself is inside the
    /// viewer's own light, so a shadow can never give away something the viewer cannot see.
    /// </summary>
    public sealed class CharacterShadow : MonoBehaviour
    {
        [Tooltip("Footprint radius in design units.")]
        public float radius = 0.25f;
        [Tooltip("Height in design units (sets how long the shadow gets).")]
        public float height = 1.8f;
        [Tooltip("Hidden outside the viewer's light (wanderer, crows): its shadow is too.")]
        public bool isEntity;
        [Range(0f, 1f)] public float strength = 1f;

        public Vector2 PlanePosition => VisionWorld.ToPlane(transform.position);
        public float Scale => transform.lossyScale.x;

        void OnEnable() => VisionWorld.Casters.Add(this);
        void OnDisable() => VisionWorld.Casters.Remove(this);
    }
}
