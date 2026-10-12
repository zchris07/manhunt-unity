using System.Collections.Generic;
using UnityEngine;

namespace Vision.Visibility
{
    /// <summary>Scene-wide registries shared by occluders, light sources and the mask renderer.</summary>
    public static class VisionWorld
    {
        static OccluderSet occluders, withLow;

        /// <summary>What blocks sight, light and shots: everything but the low things (fallen logs and trees).</summary>
        public static OccluderSet Occluders => occluders ??= new OccluderSet();

        /// <summary>
        /// The same plus the low things: the view behind a fallen log or tree is still seen over it, but in the dimmer
        /// see-through shade (the vision cone is drawn against this set at full strength, against the other at that shade).
        /// </summary>
        public static OccluderSet WithLow => withLow ??= new OccluderSet();

        public static readonly List<VisionLight> Lights = new List<VisionLight>();

        /// <summary>Characters that cast cosmetic shadows (Vision.Characters.CharacterShadow).</summary>
        public static readonly List<Vision.Characters.CharacterShadow> Casters = new List<Vision.Characters.CharacterShadow>();

        /// <summary>World position to the 2D gameplay plane.</summary>
        public static Vector2 ToPlane(Vector3 world) => new Vector2(world.x, world.z);

        public static Vector3 ToWorld(Vector2 plane, float y = 0f) => new Vector3(plane.x, y, plane.y);

        // Statics survive Play mode when domain reload is disabled; start every session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            occluders = null;
            withLow = null;
            Lights.Clear();
            Casters.Clear();
            VisionLight.FlickerEnabled = true;
            Vision.World.TerrainField.SetActive(null, null);
            Vision.Characters.HumanoidAnimator.Ground = null;
        }
    }
}
