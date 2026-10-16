using System.Collections.Generic;
using UnityEngine;

namespace Vision.Characters
{
    /// <summary>
    /// A spent shotgun shell kicked out of the ejection port as the pump comes back: a tiny red hull with a brass head,
    /// tumbling out to the right and up, falling, bouncing once on the ground and lying there a while before it's gone.
    /// </summary>
    public sealed class ShellCasing : MonoBehaviour
    {
        /// <summary>How long a shell lies on the ground (seconds), and the most lying about at once.</summary>
        public const float Lifetime = 8f;
        public const int MaxShells = 40;

        static readonly List<ShellCasing> live = new List<ShellCasing>();
        static Mesh mesh;

        Vector3 velocity, spin;
        float groundY, age, scale;
        int bounces;
        bool resting;

        /// <summary>Kicks a shell out of a port at <paramref name="at"/> (world) with a velocity; it lands at <paramref name="groundY"/>.</summary>
        public static ShellCasing Eject(Vector3 at, Vector3 velocity, float groundY, float scale, Material material, int layer)
        {
            while (live.Count >= MaxShells)
            {
                if (live[0] != null) Destroy(live[0].gameObject);
                live.RemoveAt(0);
            }
            var go = new GameObject("Shell Casing") { layer = layer };
            go.transform.SetPositionAndRotation(at, Random.rotation);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = Mesh();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var c = go.AddComponent<ShellCasing>();
            c.velocity = velocity;
            c.groundY = groundY;
            c.scale = scale;
            c.spin = Random.insideUnitSphere * 1400f;
            live.Add(c);
            return c;
        }

        /// <summary>The hull: a short red cylinder (6.5 cm by 1.8 cm) with a brass head at one end, along +Z.</summary>
        public static Mesh Mesh()
        {
            if (mesh != null) return mesh;
            var v = new List<Vector3>();
            var col = new List<Color>();
            void Tube(float z0, float z1, float r, Color c)
            {
                const int sides = 6;
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides, b = (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 d0 = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r, d1 = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f) * r;
                    Vector3 p00 = d0 + Vector3.forward * z0, p01 = d1 + Vector3.forward * z0, p10 = d0 + Vector3.forward * z1, p11 = d1 + Vector3.forward * z1;
                    foreach (Vector3 p in new[] { p00, p10, p11, p00, p11, p01, Vector3.forward * z0, p01, p00, Vector3.forward * z1, p10, p11 })
                    {
                        v.Add(p);
                        col.Add(c.linear);
                    }
                }
            }
            Tube(-0.0325f, 0.018f, 0.009f, new Color(0.72f, 0.1f, 0.08f));
            Tube(0.018f, 0.0325f, 0.0095f, new Color(0.82f, 0.64f, 0.26f));
            mesh = new Mesh { name = "Shell Casing", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(v);
            mesh.SetColors(col);
            var idx = new int[v.Count];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void Update() => Step(Time.deltaTime);

        /// <summary>Advances the shell (tests drive it directly).</summary>
        public void Step(float dt)
        {
            age += dt;
            if (age > Lifetime)
            {
                live.Remove(this);
                Destroy(gameObject);
                return;
            }
            // Shrinks away over its last half second.
            if (age > Lifetime - 0.5f) transform.localScale = Vector3.one * scale * Mathf.Clamp01((Lifetime - age) / 0.5f);
            if (resting) return;
            velocity += Vector3.down * 9.8f * scale * dt;
            Vector3 p = transform.position + velocity * dt;
            transform.Rotate(spin * dt, Space.World);
            float floor = groundY + 0.009f * scale;
            if (p.y <= floor)
            {
                p.y = floor;
                if (bounces++ < 1 && velocity.y < -0.6f * scale)
                {
                    velocity = new Vector3(velocity.x * 0.45f, -velocity.y * 0.3f, velocity.z * 0.45f);
                    spin *= 0.4f;
                }
                else
                {
                    // Lying on its side.
                    resting = true;
                    Vector3 fwd = transform.forward;
                    fwd.y = 0f;
                    transform.rotation = Quaternion.LookRotation(fwd.sqrMagnitude > 1e-4f ? fwd : Vector3.forward, Vector3.up);
                }
            }
            transform.position = p;
        }

        public bool Resting => resting;

        void OnDestroy() => live.Remove(this);
    }
}
