using System.Collections.Generic;
using UnityEngine;

namespace Vision.Visibility
{
    /// <summary>
    /// Registers this object's ground footprint as occluder segments. Boxes become 4 segments, round
    /// things (trees, rocks) become an N-gon. The footprint is taken on enable from the
    /// transform's position, Y rotation and scale, and again at Start (so a prop placed after it was spawned is where it
    /// stands); static world geometry should not move afterwards.
    /// Doors and windows toggle <see cref="Blocking"/>.
    /// </summary>
    public sealed class Occluder : MonoBehaviour
    {
        public enum Shape { Box, Circle }

        public Shape shape = Shape.Box;
        [Tooltip("Box footprint size in local X/Z (metres, before scale).")]
        public Vector2 size = Vector2.one;
        [Tooltip("Circle radius (metres, before scale).")]
        public float radius = 0.5f;
        [Range(3, 16)] public int sides = 8;
        [Tooltip("Local footprint centre offset in X/Z.")]
        public Vector2 offset;
        [SerializeField] bool blocking = true;

        int handle = -1;
        static readonly List<Vector2> Points = new List<Vector2>(16);

        /// <summary>False lets sight pass, e.g. an open door or open shutters.</summary>
        public bool Blocking
        {
            get => blocking;
            set
            {
                blocking = value;
                if (handle >= 0) VisionWorld.Occluders.SetActive(handle, value);
            }
        }

        void OnEnable()
        {
            BuildFootprint(Points);
            handle = VisionWorld.Occluders.Register(Points, true, blocking);
        }

        // Props are often spawned (and enabled) first and placed afterwards: take the footprint again where they stand.
        void Start() => Refresh();

        /// <summary>Re-registers the footprint from the transform as it is now (after the object was moved).</summary>
        public void Refresh()
        {
            if (handle >= 0) VisionWorld.Occluders.Unregister(handle);
            BuildFootprint(Points);
            handle = VisionWorld.Occluders.Register(Points, true, blocking);
        }

        void OnDisable() => Release();

        /// <summary>Takes the footprint out of the occluder set.</summary>
        public void Release()
        {
            VisionWorld.Occluders.Unregister(handle);
            handle = -1;
        }

        public void BuildFootprint(List<Vector2> points)
        {
            points.Clear();
            Transform t = transform;
            Vector3 scale = t.lossyScale;
            if (shape == Shape.Box)
            {
                float hx = size.x * 0.5f, hz = size.y * 0.5f;
                AddLocal(points, t, scale, offset.x - hx, offset.y - hz);
                AddLocal(points, t, scale, offset.x + hx, offset.y - hz);
                AddLocal(points, t, scale, offset.x + hx, offset.y + hz);
                AddLocal(points, t, scale, offset.x - hx, offset.y + hz);
            }
            else
            {
                for (int i = 0; i < sides; i++)
                {
                    float a = (i + 0.5f) / sides * Mathf.PI * 2f;
                    AddLocal(points, t, scale, offset.x + Mathf.Cos(a) * radius, offset.y + Mathf.Sin(a) * radius);
                }
            }
        }

        static void AddLocal(List<Vector2> points, Transform t, Vector3 scale, float x, float z)
        {
            Vector3 world = t.position + t.rotation * new Vector3(x * scale.x, 0f, z * scale.z);
            points.Add(VisionWorld.ToPlane(world));
        }

        void OnDrawGizmosSelected()
        {
            var pts = new List<Vector2>();
            BuildFootprint(pts);
            Gizmos.color = blocking ? Color.yellow : Color.gray;
            for (int i = 0; i < pts.Count; i++)
                Gizmos.DrawLine(VisionWorld.ToWorld(pts[i], 0.05f), VisionWorld.ToWorld(pts[(i + 1) % pts.Count], 0.05f));
        }
    }
}
