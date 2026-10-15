using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// A generator's work-light set: the trailer light tower (a cream trailer with red and blue stripes, outriggers and a
    /// tow bar, a telescopic mast as tall as a big tree with four lamp heads on a frame at the top), the yellow work lights
    /// (a box light on a frame, a twin on a bar, a tripod with one head and a tripod with two), and what lies about: a
    /// cable spool, a jerry can, a toolbox, a small yellow portable generator, a blue tarp, broken glass, planks, a sheet of
    /// corrugated metal, overgrowth, and the cables themselves. Each light's glowing faces are a mesh of their own, so they
    /// can light up when the generator runs. Origin at the bottom centre, front facing +Z.
    /// </summary>
    public static partial class LowPolyModels
    {
        /// <summary>The light tower's mast height (to the top of its lamp frame), about a big tree's.</summary>
        public const float TowerHeight = 9f;

        static readonly Color TowerCream = new Color(0.86f, 0.84f, 0.76f), TowerSteel = new Color(0.62f, 0.63f, 0.64f),
            TowerDark = new Color(0.16f, 0.16f, 0.17f), WorkYellow = new Color(0.95f, 0.76f, 0.1f), LampOff = new Color(0.78f, 0.8f, 0.74f);

        static void Part(LowPolyMeshBuilder b, Vector3 centre, Quaternion rot, System.Action draw)
        {
            Matrix4x4? keep = b.Transform;
            b.Transform = (keep ?? Matrix4x4.identity) * Matrix4x4.TRS(centre, rot, Vector3.one);
            draw();
            b.Transform = keep;
        }

        /// <summary>The four lamp heads' places on the tower's frame (centre of each head, tipped down toward the front).</summary>
        static IEnumerable<Vector3> TowerHeads()
        {
            foreach (float y in new[] { TowerHeight - 0.3f, TowerHeight - 0.85f })
                foreach (float x in new[] { -0.38f, 0.38f })
                    yield return new Vector3(x, y, -0.62f);
        }

        static readonly Quaternion HeadTilt = Quaternion.Euler(18f, 0f, 0f);

        /// <summary>The trailer light tower, less its lamp faces (<see cref="LightTowerFaces"/>).</summary>
        public static Mesh LightTower(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            // Chassis, the canopy with its stripes, vents on top.
            b.AddBox(new Vector3(-0.56f, 0.34f, -0.92f), new Vector3(1.12f, 0.12f, 1.84f), TowerDark, 0.04f);
            b.AddBox(new Vector3(-0.6f, 0.46f, -0.95f), new Vector3(1.2f, 0.78f, 1.5f), TowerCream, 0.03f);
            b.AddBox(new Vector3(-0.55f, 1.24f, -0.9f), new Vector3(1.1f, 0.08f, 1.4f), TowerCream * 0.95f, 0.03f);
            foreach (float x in new[] { -0.605f, 0.6f })
            {
                b.AddBox(new Vector3(x, 0.66f, -0.9f), new Vector3(0.006f, 0.08f, 1.4f), new Color(0.72f, 0.12f, 0.1f), 0.02f);
                b.AddBox(new Vector3(x, 0.77f, -0.9f), new Vector3(0.006f, 0.04f, 1.4f), new Color(0.15f, 0.22f, 0.55f), 0.02f);
            }
            for (int i = 0; i < 3; i++) b.AddBox(new Vector3(-0.2f + i * 0.16f, 1.32f, 0.05f), new Vector3(0.06f, 0.22f, 0.06f), TowerDark, 0.05f);
            // Wheels and their mudguards.
            foreach (float x in new[] { -0.7f, 0.7f })
            {
                b.AddFrustum(new Vector3(x - 0.08f, 0.28f, -0.1f), new Vector3(x + 0.08f, 0.28f, -0.1f), 0.28f, 0.28f, 10, new Color(0.08f, 0.08f, 0.08f), 0.03f);
                b.AddFrustum(new Vector3(x - 0.09f, 0.28f, -0.1f), new Vector3(x + 0.09f, 0.28f, -0.1f), 0.12f, 0.12f, 8, TowerSteel, 0.03f);
                b.AddBox(new Vector3(x - 0.1f, 0.6f, -0.45f), new Vector3(0.2f, 0.04f, 0.7f), TowerDark, 0.03f);
            }
            // Tow bar, the jack stand and the red coupler.
            b.AddBox(new Vector3(-0.05f, 0.38f, 0.75f), new Vector3(0.1f, 0.1f, 0.95f), TowerDark, 0.04f);
            b.AddFrustum(new Vector3(0f, 0f, 1.35f), new Vector3(0f, 0.55f, 1.35f), 0.04f, 0.04f, 6, TowerSteel, 0.03f);
            b.AddBox(new Vector3(-0.08f, 0.36f, 1.66f), new Vector3(0.16f, 0.14f, 0.16f), new Color(0.75f, 0.12f, 0.1f), 0.04f);
            // Four outriggers down to pads on the ground.
            foreach (var c in new[] { new Vector3(-0.55f, 0f, -0.85f), new Vector3(0.55f, 0f, -0.85f), new Vector3(-0.55f, 0f, 0.55f), new Vector3(0.55f, 0f, 0.55f) })
            {
                Vector3 foot = c + new Vector3(Mathf.Sign(c.x) * 0.35f, 0f, 0f);
                b.AddFrustum(foot, c + Vector3.up * 0.4f, 0.03f, 0.03f, 4, TowerDark, 0.03f);
                b.AddBox(foot + new Vector3(-0.08f, 0f, -0.08f), new Vector3(0.16f, 0.03f, 0.16f), TowerDark, 0.03f);
            }
            // The telescopic mast at the back, in three sections with collars, and the cable hanging beside it.
            var mast = new Vector3(0f, 0f, -0.68f);
            float[] at = { 1.25f, 4.2f, 6.9f, TowerHeight - 0.2f };
            float[] r = { 0.11f, 0.09f, 0.072f };
            for (int i = 0; i < 3; i++)
            {
                b.AddFrustum(mast + Vector3.up * at[i], mast + Vector3.up * at[i + 1], r[i], r[i] * 0.95f, 8, TowerSteel * (1f - i * 0.04f), 0.03f);
                b.AddFrustum(mast + Vector3.up * (at[i + 1] - 0.08f), mast + Vector3.up * (at[i + 1] + 0.02f), r[i] * 1.35f, r[i] * 1.35f, 8, TowerDark, 0.03f);
            }
            b.AddFrustum(new Vector3(0.2f, 1.3f, -0.62f), new Vector3(0.2f, TowerHeight - 1.1f, -0.66f), 0.012f, 0.012f, 4, TowerDark, 0.02f);
            b.AddBox(new Vector3(-0.3f, 1.2f, -0.8f), new Vector3(0.6f, 0.16f, 0.16f), TowerSteel, 0.03f);
            // The lamp frame at the top and the four lamp housings on it.
            b.AddBox(new Vector3(-0.82f, TowerHeight - 0.06f, -0.73f), new Vector3(1.64f, 0.06f, 0.06f), TowerSteel, 0.03f);
            b.AddBox(new Vector3(-0.82f, TowerHeight - 0.6f, -0.73f), new Vector3(1.64f, 0.06f, 0.06f), TowerSteel, 0.03f);
            foreach (Vector3 h in TowerHeads())
                Part(b, h, HeadTilt, () =>
                {
                    b.AddBox(new Vector3(-0.3f, -0.22f, -0.12f), new Vector3(0.6f, 0.44f, 0.18f), new Color(0.74f, 0.73f, 0.68f), 0.03f);
                    b.AddBox(new Vector3(-0.32f, -0.24f, 0.04f), new Vector3(0.64f, 0.04f, 0.08f), TowerDark, 0.03f);
                });
            return b.ToMesh("Light Tower");
        }

        /// <summary>The tower's four lamp faces, lit when the generator runs.</summary>
        public static Mesh LightTowerFaces(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            foreach (Vector3 h in TowerHeads())
                Part(b, h, HeadTilt, () => b.AddBox(new Vector3(-0.26f, -0.18f, 0.06f), new Vector3(0.52f, 0.36f, 0.02f), LampOff, 0.02f));
            return b.ToMesh("Light Tower Lamps");
        }

        /// <summary>How the yellow work lights come: 0 a box light on a frame, 1 a twin on a bar, 2 a tripod with one head, 3 a tripod with two.</summary>
        public const int WorkLightKinds = 4;

        /// <summary>Where a work light's lamp heads are (centre, facing +Z, tipped down), by kind.</summary>
        static IEnumerable<Vector3> WorkHeads(int kind)
        {
            switch (kind)
            {
                case 0: yield return new Vector3(0f, 0.3f, 0f); break;
                case 1: yield return new Vector3(-0.32f, 0.28f, 0f); yield return new Vector3(0.32f, 0.28f, 0f); break;
                case 2: yield return new Vector3(0f, 1.9f, 0.05f); break;
                default: yield return new Vector3(-0.24f, 1.95f, 0.05f); yield return new Vector3(0.24f, 1.95f, 0.05f); break;
            }
        }

        static Quaternion WorkTilt(int kind) => Quaternion.Euler(kind >= 2 ? 25f : 8f, 0f, 0f);

        /// <summary>A yellow work light, less its lamp faces (<see cref="WorkLightFaces"/>).</summary>
        public static Mesh WorkLight(System.Random rng, int kind)
        {
            var b = new LowPolyMeshBuilder(rng);
            void Tube(Vector3 a, Vector3 c, float r = 0.02f) => b.AddFrustum(a, c, r, r, 5, WorkYellow, 0.04f);
            if (kind <= 1)
            {
                // A tubular frame on the ground: a base, uprights and a carrying handle over the top.
                float w = kind == 0 ? 0.26f : 0.62f;
                Tube(new Vector3(-w, 0.02f, -0.12f), new Vector3(w, 0.02f, -0.12f));
                Tube(new Vector3(-w, 0.02f, 0.12f), new Vector3(w, 0.02f, 0.12f));
                foreach (float x in new[] { -w, w })
                {
                    Tube(new Vector3(x, 0.02f, -0.12f), new Vector3(x, 0.02f, 0.12f));
                    Tube(new Vector3(x, 0.02f, 0f), new Vector3(x, 0.5f, 0f));
                }
                Tube(new Vector3(-w, 0.5f, 0f), new Vector3(w, 0.5f, 0f));
                if (kind == 1) Tube(new Vector3(0f, 0.02f, 0f), new Vector3(0f, 0.75f, 0f), 0.022f);
            }
            else
            {
                // A tripod: three splayed legs, a telescopic pole and, for two heads, a crossbar.
                var hub = new Vector3(0f, 0.75f, 0f);
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.PI * 2f / 3f;
                    Tube(hub, new Vector3(Mathf.Cos(a) * 0.62f, 0f, Mathf.Sin(a) * 0.62f), 0.018f);
                }
                Tube(Vector3.up * 0.02f, Vector3.up * 1.15f, 0.026f);
                b.AddFrustum(Vector3.up * 1.15f, Vector3.up * 1.82f, 0.019f, 0.019f, 5, WorkYellow * 0.9f, 0.04f);
                b.AddBox(new Vector3(-0.03f, 1.12f, -0.03f), new Vector3(0.06f, 0.06f, 0.06f), TowerDark, 0.03f);
                if (kind == 3) Tube(new Vector3(-0.32f, 1.84f, 0f), new Vector3(0.32f, 1.84f, 0f));
            }
            foreach (Vector3 h in WorkHeads(kind))
                Part(b, h, WorkTilt(kind), () =>
                {
                    b.AddBox(new Vector3(-0.17f, -0.13f, -0.08f), new Vector3(0.34f, 0.26f, 0.13f), new Color(0.1f, 0.1f, 0.11f), 0.04f);
                    b.AddBox(new Vector3(-0.2f, -0.15f, 0.03f), new Vector3(0.4f, 0.3f, 0.03f), new Color(0.13f, 0.13f, 0.14f), 0.03f);
                    b.AddBox(new Vector3(-0.15f, -0.18f, -0.06f), new Vector3(0.3f, 0.03f, 0.1f), WorkYellow, 0.04f);
                });
            return b.ToMesh("Work Light");
        }

        /// <summary>A work light's lamp faces (a grid of LEDs as one bright panel each), lit when the generator runs.</summary>
        public static Mesh WorkLightFaces(System.Random rng, int kind)
        {
            var b = new LowPolyMeshBuilder(rng);
            foreach (Vector3 h in WorkHeads(kind))
                Part(b, h, WorkTilt(kind), () => b.AddBox(new Vector3(-0.15f, -0.11f, 0.06f), new Vector3(0.3f, 0.22f, 0.012f), LampOff, 0.02f));
            return b.ToMesh("Work Light Lamps");
        }

        /// <summary>A cable spool: an orange drum of cable between two plywood discs, on its side.</summary>
        public static Mesh CableSpool(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            var ply = new Color(0.66f, 0.5f, 0.32f);
            b.AddFrustum(new Vector3(-0.26f, 0.36f, 0f), new Vector3(-0.22f, 0.36f, 0f), 0.36f, 0.36f, 10, ply, 0.05f);
            b.AddFrustum(new Vector3(0.22f, 0.36f, 0f), new Vector3(0.26f, 0.36f, 0f), 0.36f, 0.36f, 10, ply, 0.05f);
            b.AddFrustum(new Vector3(-0.22f, 0.36f, 0f), new Vector3(0.22f, 0.36f, 0f), 0.27f, 0.27f, 10, new Color(0.9f, 0.42f, 0.1f), 0.06f);
            return b.ToMesh("Cable Spool");
        }

        /// <summary>A red steel jerry can, standing.</summary>
        public static Mesh JerryCan(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            var red = new Color(0.7f, 0.1f, 0.08f);
            b.AddBox(new Vector3(-0.17f, 0f, -0.08f), new Vector3(0.34f, 0.44f, 0.16f), red, 0.05f);
            b.AddBox(new Vector3(-0.12f, 0.44f, -0.02f), new Vector3(0.24f, 0.04f, 0.04f), red * 0.8f, 0.04f);
            b.AddFrustum(new Vector3(0.12f, 0.44f, 0f), new Vector3(0.15f, 0.52f, 0f), 0.025f, 0.02f, 6, TowerDark, 0.03f);
            return b.ToMesh("Jerry Can");
        }

        /// <summary>A red toolbox with a black lid and handle.</summary>
        public static Mesh ToolBox(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            b.AddBox(new Vector3(-0.25f, 0f, -0.11f), new Vector3(0.5f, 0.2f, 0.22f), new Color(0.72f, 0.14f, 0.1f), 0.05f);
            b.AddBox(new Vector3(-0.26f, 0.2f, -0.12f), new Vector3(0.52f, 0.05f, 0.24f), TowerDark, 0.04f);
            b.AddBox(new Vector3(-0.1f, 0.25f, -0.015f), new Vector3(0.2f, 0.05f, 0.03f), TowerDark, 0.04f);
            return b.ToMesh("Tool Box");
        }

        /// <summary>A small yellow portable generator in its tubular frame, on two wheels.</summary>
        public static Mesh PortableGenerator(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            b.AddBox(new Vector3(-0.36f, 0.12f, -0.25f), new Vector3(0.72f, 0.36f, 0.5f), new Color(0.2f, 0.2f, 0.21f), 0.05f);
            b.AddBox(new Vector3(-0.34f, 0.46f, -0.22f), new Vector3(0.68f, 0.18f, 0.44f), WorkYellow, 0.05f);
            b.AddFrustum(new Vector3(0.36f, 0.3f, 0f), new Vector3(0.39f, 0.3f, 0f), 0.13f, 0.13f, 8, WorkYellow * 0.85f, 0.04f);
            foreach (float z in new[] { -0.27f, 0.27f })
            {
                b.AddFrustum(new Vector3(-0.42f, 0.05f, z), new Vector3(-0.42f, 0.66f, z), 0.016f, 0.016f, 5, WorkYellow, 0.04f);
                b.AddFrustum(new Vector3(0.42f, 0.05f, z), new Vector3(0.42f, 0.66f, z), 0.016f, 0.016f, 5, WorkYellow, 0.04f);
                b.AddFrustum(new Vector3(-0.42f, 0.66f, z), new Vector3(0.42f, 0.66f, z), 0.016f, 0.016f, 5, WorkYellow, 0.04f);
                b.AddFrustum(new Vector3(-0.3f, 0.1f, z * 1.15f), new Vector3(-0.3f, 0.1f, z * 1.3f), 0.1f, 0.1f, 8, new Color(0.08f, 0.08f, 0.08f), 0.03f);
            }
            return b.ToMesh("Portable Generator");
        }

        /// <summary>A tarp thrown down in folds (<paramref name="w"/> by <paramref name="d"/>), creased and humped, one corner lifted.</summary>
        public static Mesh Tarp(System.Random rng, float w, float d, Color color)
        {
            var b = new LowPolyMeshBuilder(rng);
            const int n = 6;
            var h = new float[n + 1, n + 1];
            for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    float u = i / (float)n, v = j / (float)n;
                    h[i, j] = 0.02f + 0.1f * Mathf.Max(0f, Mathf.Sin(u * 7f + v * 2f)) * Mathf.Sin(v * Mathf.PI) + (i == n && j == n ? 0.35f : 0f) + b.Range(0f, 0.04f);
                }
            Vector3 P(int i, int j) => new Vector3((i / (float)n - 0.5f) * w, h[i, j], (j / (float)n - 0.5f) * d);
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    Color c = b.Jitter(color, 0.08f);
                    b.AddDoubleSided(P(i, j), P(i + 1, j + 1), P(i + 1, j), c);
                    b.AddDoubleSided(P(i, j), P(i, j + 1), P(i + 1, j + 1), b.Jitter(c, 0.05f));
                }
            return b.ToMesh("Tarp");
        }

        /// <summary>Broken glass on the ground: a scatter of small flat shards.</summary>
        public static Mesh GlassShards(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            for (int i = 0; i < 14; i++)
            {
                var c = new Vector3(b.Range(-0.55f, 0.55f), 0.012f, b.Range(-0.55f, 0.55f));
                float s = b.Range(0.03f, 0.09f), a = b.Range(0f, 6.3f);
                Vector3 p0 = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * s, p1 = c + new Vector3(Mathf.Cos(a + 2.2f), 0f, Mathf.Sin(a + 2.2f)) * s * 0.8f,
                    p2 = c + new Vector3(Mathf.Cos(a + 4.1f), 0.004f, Mathf.Sin(a + 4.1f)) * s * 0.6f;
                b.AddDoubleSided(p0, p1, p2, b.Jitter(new Color(0.72f, 0.88f, 0.92f), 0.1f));
            }
            return b.ToMesh("Glass Shards");
        }

        /// <summary>A few loose planks dropped in a heap, some crossed and tipped.</summary>
        public static Mesh Planks(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            for (int i = 0; i < 4; i++)
                AddBoxAt(b, new Vector3(b.Range(-0.3f, 0.3f), 0.03f + i * 0.035f, b.Range(-0.2f, 0.2f)), new Vector3(1.6f, 0.035f, 0.16f),
                    Quaternion.Euler(b.Range(-4f, 4f), b.Range(-40f, 40f), b.Range(-3f, 3f)), b.Jitter(new Color(0.55f, 0.42f, 0.28f), 0.12f));
            return b.ToMesh("Planks");
        }

        /// <summary>A rusty sheet of corrugated metal lying on the ground.</summary>
        public static Mesh MetalSheet(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            for (int i = 0; i < 10; i++)
            {
                Color c = b.Jitter(Color.Lerp(new Color(0.52f, 0.52f, 0.5f), new Color(0.48f, 0.3f, 0.18f), b.Range(0f, 0.6f)), 0.06f);
                b.AddBox(new Vector3(-0.6f + i * 0.12f, i % 2 == 0 ? 0.01f : 0.035f, -0.9f), new Vector3(0.12f, 0.012f, 1.8f), c, 0.03f);
            }
            return b.ToMesh("Metal Sheet");
        }

        /// <summary>Overgrowth: ferns, tall grass and leafy clumps in a tangle.</summary>
        public static Mesh Overgrowth(System.Random rng, float size)
        {
            var b = new LowPolyMeshBuilder(rng);
            for (int i = 0; i < 4; i++)
            {
                b.Transform = Matrix4x4.Translate(new Vector3(b.Range(-0.6f, 0.6f), 0f, b.Range(-0.6f, 0.6f)) * size);
                AddFern(b, b.Range(0.45f, 0.75f) * size);
            }
            for (int i = 0; i < 3; i++)
            {
                b.Transform = Matrix4x4.Translate(new Vector3(b.Range(-0.7f, 0.7f), 0f, b.Range(-0.7f, 0.7f)) * size);
                AddTallGrass(b, b.Range(0.6f, 1f) * size, false);
            }
            b.Transform = null;
            for (int i = 0; i < 3; i++)
                b.AddBlob(new Vector3(b.Range(-0.5f, 0.5f), 0.18f, b.Range(-0.5f, 0.5f)) * size, new Vector3(0.32f, 0.22f, 0.3f) * size, 0, 0.3f,
                    _ => b.Jitter(new Color(0.22f, 0.38f, 0.16f), 0.15f), true);
            return b.ToMesh("Overgrowth");
        }

        /// <summary>A cable along a path of points (world-local), a thin dark tube.</summary>
        public static Mesh Cable(System.Random rng, IList<Vector3> path, float radius, Color color)
        {
            var b = new LowPolyMeshBuilder(rng);
            for (int i = 0; i + 1 < path.Count; i++)
                if ((path[i + 1] - path[i]).sqrMagnitude > 1e-6f) b.AddFrustum(path[i], path[i + 1], radius, radius, 4, color, 0.03f);
            return b.ToMesh("Cable");
        }
    }
}
