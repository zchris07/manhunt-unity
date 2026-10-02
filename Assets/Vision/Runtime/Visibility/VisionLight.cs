using System.Collections.Generic;
using UnityEngine;

namespace Vision.Visibility
{
    /// <summary>
    /// A light source (campfire, lantern, lit generator). Each one gets its own visibility polygon,
    /// drawn into the mask's R channel. Static lights cache their polygon until an occluder changes.
    /// A light source brightens ground only where the viewer has a sightline (G); it never reveals
    /// entities.
    /// </summary>
    public sealed class VisionLight : MonoBehaviour
    {
        [Tooltip("Radius in design units; multiplied by the transform scale.")]
        public float range = 6f;
        [Range(0f, 1f)] public float intensity = 1f;
        [Tooltip("Cache the polygon; rebuilt only when an occluder changes or the light moves.")]
        public bool isStatic = true;
        [Range(0f, 0.5f)] public float flickerAmount = 0.12f;
        public float flickerSpeed = 6f;

        readonly List<Vector2> cachedPolygon = new List<Vector2>(256);
        int cachedVersion = -1;
        Vector2 cachedOrigin;
        float cachedRange;
        float noiseSeed;

        public Vector2 PlanePosition => VisionWorld.ToPlane(transform.position);

        /// <summary>Radius in world units.</summary>
        public float WorldRange => range * transform.lossyScale.x;

        public float CurrentIntensity
        {
            get
            {
                if (flickerAmount <= 0f) return intensity;
                float n = Mathf.PerlinNoise(noiseSeed, Time.time * flickerSpeed);
                return Mathf.Clamp01(intensity * (1f - flickerAmount + n * flickerAmount));
            }
        }

        void OnEnable()
        {
            noiseSeed = Random.value * 100f;
            VisionWorld.Lights.Add(this);
        }

        void OnDisable() => VisionWorld.Lights.Remove(this);

        /// <summary>Returns this light's polygon, recomputing it only when needed.</summary>
        public List<Vector2> GetPolygon(VisibilityComputer computer, int occluderVersion)
        {
            Vector2 origin = PlanePosition;
            float worldRange = WorldRange;
            bool valid = isStatic && cachedVersion == occluderVersion && cachedOrigin == origin && Mathf.Approximately(cachedRange, worldRange);
            if (!valid)
            {
                computer.Compute(ViewQuery.Circle(origin, worldRange), cachedPolygon);
                cachedVersion = occluderVersion;
                cachedOrigin = origin;
                cachedRange = worldRange;
            }
            return cachedPolygon;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, WorldRange);
        }
    }
}
