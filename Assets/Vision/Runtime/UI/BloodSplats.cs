using System.Collections.Generic;
using UnityEngine;
using Vision.Effects;
using Vision.World;

namespace Vision.UI
{
    /// <summary>
    /// The blood the hurt drip on the ground (the trail Zach follows): each drop a miniature splatter, a few centimetres
    /// across, laid on the ground's own facets so it lies on every slope and bump, and each one its own shape: a round drop
    /// with a crown of specks thrown round it, a teardrop with a tail of droplets flicked off it, a little cluster of
    /// droplets, or a star-burst with thin spikes. Dark crimson, drying darker as it fades. Each splatter is built once, the
    /// first time it's drawn, and kept while its drop lasts.
    /// </summary>
    public sealed class BloodSplats
    {
        // Drawn this dark: the lit ground's exposure, warm tint and contrast lift what's drawn about four times over, so on
        // screen it's a dark crimson (about #6e0814 fresh).
        static readonly Color Fresh = new Color(0.052f, 0.0015f, 0.0042f), Dried = new Color(0.03f, 0.001f, 0.0026f);

        sealed class Splat
        {
            /// <summary>World triangles, three corners each, and a shade (0-1) for each triangle.</summary>
            public readonly List<Vector3> V = new List<Vector3>(96);
            public readonly List<float> Shade = new List<float>(32);
        }

        readonly Dictionary<int, Splat> splats = new Dictionary<int, Splat>();
        readonly HashSet<int> drawn = new HashSet<int>();
        readonly List<int> gone = new List<int>();
        readonly List<Vector2> ring = new List<Vector2>(16);

        public int Count => splats.Count;

        /// <summary>The corners of drop <paramref name="id"/>'s triangles (world), once it has been drawn.</summary>
        public IReadOnlyList<Vector3> Corners(int id) => splats.TryGetValue(id, out Splat s) ? s.V : null;

        /// <summary>Call before drawing this frame's drops.</summary>
        public void Begin() => drawn.Clear();

        /// <summary>Draws drop <paramref name="id"/> at <paramref name="at"/> (design units), <paramref name="age"/> 0-1 through its life.</summary>
        public void Draw(SandboxWorld world, int id, Vector2 at, float age)
        {
            drawn.Add(id);
            if (!splats.TryGetValue(id, out Splat s)) splats[id] = s = Build(world, id, at);
            float fade = Mathf.Pow(1f - Mathf.Clamp01(age), 1.3f) * 0.95f;
            if (fade <= 0.004f) return;
            Color baseColor = Color.Lerp(Fresh, Dried, Mathf.SmoothStep(0f, 1f, age * 1.4f));
            Vfx fx = Vfx.Instance;
            for (int t = 0; t < s.Shade.Count; t++)
            {
                Color c = baseColor * Mathf.Lerp(0.8f, 1.15f, s.Shade[t]);
                c.a = fade;
                fx.Tri(s.V[t * 3], s.V[t * 3 + 1], s.V[t * 3 + 2], c, true, false, Vfx.Blend.Alpha);
            }
        }

        /// <summary>Call after drawing: forgets the splatters whose drops are gone.</summary>
        public void End()
        {
            gone.Clear();
            foreach (int id in splats.Keys) if (!drawn.Contains(id)) gone.Add(id);
            foreach (int id in gone) splats.Remove(id);
        }

        // ---------------------------------------------------------------- shapes

        Splat Build(SandboxWorld world, int id, Vector2 at)
        {
            var rng = new System.Random(id * 7919 + 17);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var s = new Splat();
            float turn = R(0f, Mathf.PI * 2f);
            var dir = new Vector2(Mathf.Cos(turn), Mathf.Sin(turn));
            var side = new Vector2(-dir.y, dir.x);
            float r0 = R(0.016f, 0.036f);
            switch (rng.Next(4))
            {
                case 0:
                    // A round drop, its rim a little uneven, and a crown of specks thrown out round it.
                    Blob(world, s, at, r0, 1f, dir, 10, 0.18f, R(0f, 1f), rng);
                    for (int i = rng.Next(3, 8); i > 0; i--)
                    {
                        float a = R(0f, Mathf.PI * 2f);
                        Blob(world, s, at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r0 * R(1.4f, 2.6f), r0 * R(0.12f, 0.28f), 1f, dir, 5, 0.25f, R(0f, 1f), rng);
                    }
                    break;
                case 1:
                    // A teardrop that landed moving: long one way, with a tail of shrinking droplets flicked on after it.
                    Blob(world, s, at, r0, 1.5f, dir, 10, 0.1f, R(0f, 1f), rng);
                    for (int i = 1; i <= rng.Next(2, 4); i++)
                        Blob(world, s, at + dir * r0 * (1.7f + i * 0.9f) + side * r0 * R(-0.25f, 0.25f), r0 * 0.34f / (1f + i * 0.5f), 1.3f, dir, 6, 0.15f, R(0f, 1f), rng);
                    break;
                case 2:
                    // A little cluster of droplets.
                    for (int i = rng.Next(3, 6); i > 0; i--)
                        Blob(world, s, at + new Vector2(R(-1f, 1f), R(-1f, 1f)) * r0 * 1.3f, r0 * R(0.32f, 0.6f), R(1f, 1.3f), dir, 7, 0.2f, R(0f, 1f), rng);
                    break;
                default:
                    // A star-burst: a body with thin spikes splashed out of it, a speck at the end of some.
                    Blob(world, s, at, r0 * 0.85f, 1f, dir, 9, 0.15f, R(0f, 1f), rng);
                    for (int i = rng.Next(5, 9); i > 0; i--)
                    {
                        float a = R(0f, Mathf.PI * 2f);
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        var n = new Vector2(-d.y, d.x);
                        float reach = r0 * R(1.5f, 2.5f), w = r0 * R(0.12f, 0.22f);
                        float shade = R(0f, 1f);
                        Tri(world, s, at + n * w, at - n * w, at + d * reach, shade);
                        if (rng.NextDouble() < 0.5) Blob(world, s, at + d * (reach + r0 * 0.35f), r0 * R(0.1f, 0.2f), 1f, dir, 5, 0.2f, shade, rng);
                    }
                    break;
            }
            return s;
        }

        /// <summary>An uneven round (or, stretched along <paramref name="dir"/>, oval) blob as a fan of triangles.</summary>
        void Blob(SandboxWorld world, Splat s, Vector2 c, float r, float stretch, Vector2 dir, int sides, float rough, float shade, System.Random rng)
        {
            var side = new Vector2(-dir.y, dir.x);
            ring.Clear();
            float phase = (float)rng.NextDouble() * Mathf.PI * 2f;
            for (int i = 0; i < sides; i++)
            {
                float a = phase + i * Mathf.PI * 2f / sides;
                float k = 1f + ((float)rng.NextDouble() * 2f - 1f) * rough;
                ring.Add(c + (dir * (Mathf.Cos(a) * stretch) + side * Mathf.Sin(a)) * r * k);
            }
            for (int i = 0; i < sides; i++) Tri(world, s, c, ring[i], ring[(i + 1) % sides], shade);
        }

        static void Tri(SandboxWorld world, Splat s, Vector2 a, Vector2 b, Vector2 c, float shade)
        {
            s.V.Add(OnGround(world, a));
            s.V.Add(OnGround(world, b));
            s.V.Add(OnGround(world, c));
            s.Shade.Add(shade);
        }

        /// <summary>A point a few millimetres over the drawn ground (its facets, or the building's floor).</summary>
        static Vector3 OnGround(SandboxWorld world, Vector2 at) => world.transform.TransformPoint(new Vector3(at.x, world.SurfaceHeight(at) + 0.006f, at.y));
    }
}
