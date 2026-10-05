using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// Twelve hand-shaped dead trees modelled on real ones found in the woods. Each design is fixed (its
    /// branches are laid out by hand; a fixed seed only roughens bark and twig tips) and comes in a few sizes.
    /// Origin at the base of the trunk; +Y up.
    /// </summary>
    public static partial class LowPolyModels
    {
        public enum DeadTreeKind
        {
            Oak, PineSnag, Birch, Spruce, Juniper, Stump, Maple, Cedar, Elm, AspenCluster, HollowTrunk, Fallen,
        }

        public const int DeadTreeKinds = 12;

        /// <summary>Size steps per design (index into <see cref="DeadTreeSizes"/>).</summary>
        public static readonly float[] DeadTreeSizes = { 0.75f, 1f, 1.3f };

        public readonly struct DeadTreeInfo
        {
            public readonly string Name;
            /// <summary>Trunk radius at the base, design units (collider and sight footprint).</summary>
            public readonly float TrunkRadius;
            public readonly Color Bark;
            /// <summary>Lying on the ground (the fallen tree): a box footprint along +X.</summary>
            public readonly bool Lying;

            public DeadTreeInfo(string name, float trunkRadius, Color bark, bool lying = false)
            {
                Name = name;
                TrunkRadius = trunkRadius;
                Bark = bark;
                Lying = lying;
            }
        }

        static readonly Color GreyBark = new Color(0.36f, 0.34f, 0.31f);
        static readonly Color SilverBark = new Color(0.50f, 0.48f, 0.44f);
        static readonly Color BirchBark = new Color(0.74f, 0.71f, 0.64f);
        static readonly Color BirchMark = new Color(0.16f, 0.14f, 0.13f);
        static readonly Color CedarBark = new Color(0.36f, 0.25f, 0.19f);
        static readonly Color RotWood = new Color(0.27f, 0.20f, 0.14f);

        public static DeadTreeInfo DeadTreeInfoFor(DeadTreeKind kind) => kind switch
        {
            DeadTreeKind.Oak => new DeadTreeInfo("Dead Oak", 0.42f, Palette.Bark),
            DeadTreeKind.PineSnag => new DeadTreeInfo("Pine Snag", 0.30f, GreyBark),
            DeadTreeKind.Birch => new DeadTreeInfo("Dead Birch", 0.15f, BirchBark),
            DeadTreeKind.Spruce => new DeadTreeInfo("Dead Spruce", 0.22f, GreyBark),
            DeadTreeKind.Juniper => new DeadTreeInfo("Dead Juniper", 0.26f, SilverBark),
            DeadTreeKind.Stump => new DeadTreeInfo("Snapped Stump", 0.45f, Palette.BarkDark),
            DeadTreeKind.Maple => new DeadTreeInfo("Dead Maple", 0.34f, GreyBark),
            DeadTreeKind.Cedar => new DeadTreeInfo("Dead Cedar", 0.30f, CedarBark),
            DeadTreeKind.Elm => new DeadTreeInfo("Dead Elm", 0.36f, Palette.Bark),
            DeadTreeKind.AspenCluster => new DeadTreeInfo("Dead Aspens", 0.55f, BirchBark),
            DeadTreeKind.HollowTrunk => new DeadTreeInfo("Hollow Trunk", 0.52f, Palette.BarkDark),
            _ => new DeadTreeInfo("Fallen Tree", 0.32f, Palette.Bark, true),
        };

        /// <summary>One dead tree of a design at a size step (0 small, 1 medium, 2 large).</summary>
        public static Mesh DeadTree(DeadTreeKind kind, int size)
        {
            float s = DeadTreeSizes[Mathf.Clamp(size, 0, DeadTreeSizes.Length - 1)];
            var b = new LowPolyMeshBuilder(new System.Random(7000 + (int)kind * 10 + size));
            DeadTreeInfo info = DeadTreeInfoFor(kind);
            switch (kind)
            {
                case DeadTreeKind.Oak: Oak(b, s, info.Bark); break;
                case DeadTreeKind.PineSnag: PineSnag(b, s, info.Bark); break;
                case DeadTreeKind.Birch: Birch(b, s); break;
                case DeadTreeKind.Spruce: Spruce(b, s, info.Bark); break;
                case DeadTreeKind.Juniper: Juniper(b, s, info.Bark); break;
                case DeadTreeKind.Stump: Stump(b, s, info.Bark); break;
                case DeadTreeKind.Maple: Maple(b, s, info.Bark); break;
                case DeadTreeKind.Cedar: Cedar(b, s, info.Bark); break;
                case DeadTreeKind.Elm: Elm(b, s, info.Bark); break;
                case DeadTreeKind.AspenCluster: Aspens(b, s); break;
                case DeadTreeKind.HollowTrunk: Hollow(b, s, info.Bark); break;
                default: Fallen(b, s, info.Bark); break;
            }
            return b.ToMesh(info.Name);
        }

        /// <summary>Back-compatible: a random design and size.</summary>
        public static Mesh DeadTree(System.Random rng) => DeadTree((DeadTreeKind)rng.Next(DeadTreeKinds), rng.Next(DeadTreeSizes.Length));

        // ------------------------------------------------------------------ building blocks

        /// <summary>Direction from yaw (degrees, 0 = +Z) and elevation (degrees above horizontal).</summary>
        static Vector3 Dir(float yaw, float elevation)
        {
            float y = yaw * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(y) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(y) * Mathf.Cos(e));
        }

        /// <summary>
        /// A limb from <paramref name="start"/> along <paramref name="dir"/>, bending by <paramref name="bend"/> (added to the
        /// direction per segment), tapering from <paramref name="radius"/> to a point (or to <paramref name="endRadius"/>).
        /// Returns the centre line.
        /// </summary>
        static Vector3[] Limb(LowPolyMeshBuilder b, Vector3 start, Vector3 dir, float length, float radius, Color color,
            Vector3 bend = default, int segments = 3, float endRadius = 0f, float wobble = 0.05f)
        {
            var c = new Vector3[segments + 1];
            var r = new float[segments + 1];
            c[0] = start;
            r[0] = radius;
            Vector3 d = dir.normalized;
            float step = length / segments;
            for (int i = 1; i <= segments; i++)
            {
                d = (d + bend + new Vector3(b.Range(-wobble, wobble), b.Range(-wobble, wobble), b.Range(-wobble, wobble))).normalized;
                c[i] = c[i - 1] + d * step;
                r[i] = Mathf.Lerp(radius, endRadius, i / (float)segments);
            }
            int sides = PolyBudget.Sides(radius, TreeClass, 3, 7);
            b.AddTube(c, r, sides, color, 0.1f, 0.06f, 0f, null, false, endRadius > 0f);
            return c;
        }

        static Vector3 Along(Vector3[] line, float t)
        {
            float f = Mathf.Clamp01(t) * (line.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), line.Length - 2);
            return Vector3.Lerp(line[i], line[i + 1], f - i);
        }

        /// <summary>Thin bare twigs fanned out from the end part of a limb.</summary>
        static void Twigs(LowPolyMeshBuilder b, Vector3[] limb, int count, float length, float radius, Color color, float from = 0.55f, float lift = 0.3f)
        {
            Vector3 axis = (limb[limb.Length - 1] - limb[0]).normalized;
            for (int i = 0; i < count; i++)
            {
                float t = Mathf.Lerp(from, 0.98f, (i + 0.5f) / count);
                Vector3 p = Along(limb, t);
                Vector3 side = Vector3.Cross(axis, Vector3.up);
                if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
                side = Quaternion.AngleAxis(i * 137.5f, axis) * side.normalized;
                Vector3 d = (axis * 0.6f + side + Vector3.up * lift).normalized;
                Limb(b, p, d, length * b.Range(0.7f, 1.2f), radius, color, Vector3.zero, 1);
            }
        }

        /// <summary>Root flares spreading from the trunk base.</summary>
        static void Roots(LowPolyMeshBuilder b, int count, float reach, float radius, Color color, float phase = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                float a = phase + i * 360f / count + b.Range(-12f, 12f);
                Vector3 d = Dir(a, 0f);
                b.AddTube(new[] { d * radius * 0.3f + Vector3.up * radius * 1.4f, d * reach * 0.5f + Vector3.up * radius * 0.35f, d * reach - Vector3.up * 0.03f },
                          new[] { radius, radius * 0.55f, 0f }, PolyBudget.Sides(radius, TreeClass, 3, 6), color * 0.85f, 0.1f, 0.08f, 0f, null, false, false);
            }
        }

        /// <summary>A tapering trunk from the ground to <paramref name="height"/> along a gently bent line; returns its centre line.</summary>
        static Vector3[] Trunk(LowPolyMeshBuilder b, float height, float baseRadius, float topRadius, Color color, Vector3 lean = default, int segments = 4, bool capTop = true)
        {
            var c = new Vector3[segments + 1];
            var r = new float[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                c[i] = new Vector3(lean.x * t * t * height, height * t, lean.z * t * t * height) + (i == 0 ? Vector3.zero : new Vector3(b.Range(-0.03f, 0.03f), 0f, b.Range(-0.03f, 0.03f)));
                r[i] = Mathf.Lerp(baseRadius, topRadius, Mathf.Pow(t, 0.8f));
            }
            b.AddTube(c, r, PolyBudget.Sides(baseRadius, TreeClass, 5, 8), color, 0.09f, 0.07f, 5f, null, false, capTop);
            return c;
        }

        /// <summary>A jagged, broken top: a ring of splinters around the trunk's end.</summary>
        static void BrokenTop(LowPolyMeshBuilder b, Vector3 top, float radius, Color color, float splinter)
        {
            int n = 5;
            for (int i = 0; i < n; i++)
            {
                float a = i * 360f / n + b.Range(-15f, 15f);
                Vector3 at = top + Dir(a, 0f) * radius * 0.6f;
                b.AddCone(at - Vector3.up * 0.05f, at + new Vector3(b.Range(-0.05f, 0.05f), splinter * b.Range(0.4f, 1.1f), b.Range(-0.05f, 0.05f)),
                          radius * 0.45f, 3, color * 1.1f, 0.12f, false);
            }
        }

        // ------------------------------------------------------------------ the twelve designs

        /// <summary>Dead oak: short massive trunk splitting into three gnarled, wide-spreading limbs that fork again.</summary>
        static void Oak(LowPolyMeshBuilder b, float s, Color bark)
        {
            Roots(b, 5, 1.0f * s, 0.18f * s, bark);
            Vector3[] trunk = Trunk(b, 1.9f * s, 0.42f * s, 0.30f * s, bark, new Vector3(0.05f, 0f, 0f), 3, false);
            Vector3 crown = trunk[trunk.Length - 1];
            float[] yaws = { 20f, 150f, 260f };
            float[] elev = { 38f, 30f, 48f };
            for (int i = 0; i < 3; i++)
            {
                Vector3[] limb = Limb(b, crown - Vector3.up * 0.1f * s, Dir(yaws[i], elev[i]), 2.1f * s, 0.2f * s, bark, new Vector3(0f, -0.08f, 0f), 3);
                for (int k = 0; k < 2; k++)
                {
                    Vector3[] sub = Limb(b, Along(limb, 0.55f + k * 0.3f), Dir(yaws[i] + (k == 0 ? -40f : 45f), 25f + k * 20f), 1.2f * s, 0.08f * s, bark,
                                         new Vector3(0f, -0.05f, 0f), 2);
                    Twigs(b, sub, 3, 0.45f * s, 0.025f * s, bark);
                }
            }
            Limb(b, crown, Dir(80f, 75f), 1.5f * s, 0.14f * s, bark, Vector3.zero, 2);
        }

        /// <summary>Pine snag: tall straight grey trunk, sheared-off top and dozens of short dead branch stubs.</summary>
        static void PineSnag(LowPolyMeshBuilder b, float s, Color bark)
        {
            Roots(b, 4, 0.6f * s, 0.12f * s, bark);
            float h = 7f * s;
            Vector3[] trunk = Trunk(b, h, 0.30f * s, 0.14f * s, bark, Vector3.zero, 5, false);
            BrokenTop(b, trunk[trunk.Length - 1], 0.14f * s, bark, 0.5f * s);
            for (int i = 0; i < 16; i++)
            {
                float t = Mathf.Lerp(0.25f, 0.92f, i / 15f);
                Vector3 p = Along(trunk, t);
                Limb(b, p, Dir(i * 137.5f, b.Range(-15f, 10f)), Mathf.Lerp(0.6f, 0.3f, t) * s, 0.045f * s, bark * 0.9f, Vector3.zero, 1);
            }
        }

        /// <summary>Dead birch: slender pale trunk with dark scars, a slight bend, a few thin upright branches.</summary>
        static void Birch(LowPolyMeshBuilder b, float s)
        {
            float h = 5.6f * s, r = 0.15f * s;
            int rings = 7;
            var c = new Vector3[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings;
                c[i] = new Vector3(0.35f * s * t * t, h * t, -0.15f * s * t * t);
            }
            // Bands of pale bark and dark scars.
            for (int i = 0; i < rings; i++)
            {
                Color col = i % 3 == 1 ? Color.Lerp(BirchBark, BirchMark, 0.55f) : BirchBark;
                float r0 = Mathf.Lerp(r, r * 0.45f, i / (float)rings), r1 = Mathf.Lerp(r, r * 0.45f, (i + 1) / (float)rings);
                b.AddTube(new[] { c[i], c[i + 1] }, new[] { r0, r1 }, 6, b.Jitter(col, 0.06f), 0.05f, 0.04f, 0f, null, false, i == rings - 1);
            }
            Vector3 top = c[rings];
            BrokenTop(b, top, r * 0.45f, BirchBark, 0.25f * s);
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = Along(c, Mathf.Lerp(0.45f, 0.9f, i / 5f));
                Vector3[] limb = Limb(b, p, Dir(i * 120f + 30f, 55f), Mathf.Lerp(1.3f, 0.7f, i / 5f) * s, 0.04f * s, BirchMark * 2.2f, Vector3.zero, 2);
                Twigs(b, limb, 2, 0.3f * s, 0.015f * s, BirchMark * 2.2f, 0.6f, 0.6f);
            }
        }

        /// <summary>Dead spruce: grey spire ringed with whorls of short drooping bare twigs, shorter toward the pointed top.</summary>
        static void Spruce(LowPolyMeshBuilder b, float s, Color bark)
        {
            float h = 6.2f * s;
            Vector3[] trunk = Trunk(b, h, 0.22f * s, 0.02f * s, bark, Vector3.zero, 5, true);
            int whorls = 11;
            for (int w = 0; w < whorls; w++)
            {
                float t = Mathf.Lerp(0.2f, 0.95f, w / (float)(whorls - 1));
                Vector3 p = Along(trunk, t);
                float len = Mathf.Lerp(1.1f, 0.25f, t) * s;
                for (int k = 0; k < 4; k++)
                {
                    if (b.Next() < 0.25f) continue;   // lost twigs
                    Limb(b, p, Dir(w * 47f + k * 90f, -b.Range(15f, 30f)), len * b.Range(0.7f, 1.1f), 0.03f * s, bark * 1.05f, new Vector3(0f, -0.08f, 0f), 2);
                }
            }
        }

        /// <summary>Dead juniper: short, silver, spiral-twisted trunk splitting into contorted limbs.</summary>
        static void Juniper(LowPolyMeshBuilder b, float s, Color bark)
        {
            Roots(b, 4, 0.7f * s, 0.12f * s, bark);
            int rings = 7;
            var c = new Vector3[rings + 1];
            var r = new float[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings, a = t * 300f * Mathf.Deg2Rad;
                c[i] = new Vector3(Mathf.Cos(a) * 0.25f * s * t, 1.6f * s * t, Mathf.Sin(a) * 0.25f * s * t);
                r[i] = Mathf.Lerp(0.26f, 0.13f, t) * s;
            }
            b.AddTube(c, r, 6, bark, 0.12f, 0.12f, 18f, null, false, false);
            Vector3 top = c[rings];
            float[] yaws = { 40f, 170f, 290f };
            for (int i = 0; i < 3; i++)
            {
                Vector3[] limb = Limb(b, top - Vector3.up * 0.1f * s, Dir(yaws[i], 35f), 1.4f * s, 0.11f * s, bark,
                                      new Vector3(Mathf.Sin(i * 2f) * 0.2f, -0.05f, Mathf.Cos(i * 2f) * 0.2f), 4, 0f, 0.12f);
                Twigs(b, limb, 3, 0.35f * s, 0.025f * s, bark, 0.5f, 0.4f);
            }
        }

        /// <summary>Snapped stump: what is left after the wind took the top, a jagged shattered end and big roots.</summary>
        static void Stump(LowPolyMeshBuilder b, float s, Color bark)
        {
            Roots(b, 6, 1.0f * s, 0.18f * s, bark, 15f);
            Vector3[] trunk = Trunk(b, 1.5f * s, 0.45f * s, 0.38f * s, bark, new Vector3(0.05f, 0f, 0.02f), 2, false);
            Vector3 top = trunk[trunk.Length - 1];
            b.AddFrustum(top - Vector3.up * 0.04f, top, 0.37f * s, 0.35f * s, 7, RotWood, 0.1f, 0f, false, true);
            int n = 7;
            for (int i = 0; i < n; i++)
            {
                float a = i * 360f / n + b.Range(-12f, 12f);
                Vector3 at = top + Dir(a, 0f) * 0.27f * s;
                float tall = (i == 0 ? 1.4f : b.Range(0.25f, 0.8f)) * s;
                b.AddCone(at - Vector3.up * 0.05f, at + new Vector3(b.Range(-0.06f, 0.06f), tall, b.Range(-0.06f, 0.06f)), 0.13f * s, 3, i % 2 == 0 ? bark : RotWood, 0.12f, false);
            }
        }

        /// <summary>Dying maple: smooth grey bole that forks into a tall V, each lead forking again.</summary>
        static void Maple(LowPolyMeshBuilder b, float s, Color bark)
        {
            Roots(b, 4, 0.7f * s, 0.14f * s, bark);
            Vector3[] trunk = Trunk(b, 2.1f * s, 0.34f * s, 0.26f * s, bark, Vector3.zero, 3, false);
            Vector3 fork = trunk[trunk.Length - 1];
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3[] lead = Limb(b, fork - Vector3.up * 0.1f * s, Dir(90f * side, 68f), 3.0f * s, 0.18f * s, bark, Vector3.zero, 3);
                for (int k = 0; k < 2; k++)
                {
                    Vector3[] sub = Limb(b, Along(lead, 0.6f + k * 0.3f), Dir(90f * side + (k == 0 ? 50f : -50f), 55f), 1.4f * s, 0.07f * s, bark, Vector3.zero, 2);
                    Twigs(b, sub, 3, 0.4f * s, 0.02f * s, bark, 0.5f, 0.5f);
                }
            }
        }

        /// <summary>Leaning dead cedar: a reddish trunk tilted hard to one side, its thin dead branches drooping.</summary>
        static void Cedar(LowPolyMeshBuilder b, float s, Color bark)
        {
            Roots(b, 5, 0.8f * s, 0.13f * s, bark, 30f);
            float h = 5.6f * s;
            Vector3[] trunk = Trunk(b, h, 0.30f * s, 0.06f * s, bark, new Vector3(0.55f, 0f, 0.1f), 5, true);
            for (int i = 0; i < 12; i++)
            {
                float t = Mathf.Lerp(0.3f, 0.95f, i / 11f);
                Vector3 p = Along(trunk, t);
                Limb(b, p, Dir(i * 97f, b.Range(-5f, 20f)), Mathf.Lerp(1.2f, 0.4f, t) * s, 0.04f * s, bark * 0.95f, new Vector3(0f, -0.15f, 0f), 3);
            }
        }

        /// <summary>Dead elm: a vase of five limbs rising and spreading from one point, ending in fine twigs.</summary>
        static void Elm(LowPolyMeshBuilder b, float s, Color bark)
        {
            Roots(b, 5, 0.8f * s, 0.15f * s, bark);
            Vector3[] trunk = Trunk(b, 2.3f * s, 0.36f * s, 0.24f * s, bark, Vector3.zero, 3, false);
            Vector3 top = trunk[trunk.Length - 1];
            for (int i = 0; i < 5; i++)
            {
                Vector3[] limb = Limb(b, top - Vector3.up * 0.15f * s, Dir(i * 72f + 10f, 62f), 3.2f * s, 0.12f * s, bark,
                                      new Vector3(0f, 0.02f, 0f) + Dir(i * 72f + 10f, 0f) * 0.06f, 3);
                Twigs(b, limb, 4, 0.55f * s, 0.022f * s, bark, 0.65f, 0.5f);
            }
        }

        /// <summary>A clump of dead aspens: four thin pale trunks from one root mass, with black eye scars.</summary>
        static void Aspens(LowPolyMeshBuilder b, float s)
        {
            b.AddBlob(Vector3.up * 0.05f, new Vector3(0.5f, 0.18f, 0.45f) * s, 0, 0.2f, _ => b.Jitter(Palette.BarkDark, 0.1f), true);
            Vector2[] at = { new Vector2(-0.25f, 0.1f), new Vector2(0.22f, 0.2f), new Vector2(0.05f, -0.28f), new Vector2(0.32f, -0.12f) };
            float[] heights = { 5.2f, 4.4f, 3.6f, 2.4f };
            for (int i = 0; i < at.Length; i++)
            {
                var root = new Vector3(at[i].x, 0f, at[i].y) * s;
                float h = heights[i] * s;
                Vector3 lean = new Vector3(at[i].x, 0f, at[i].y).normalized * 0.12f;
                int rings = 5;
                var c = new Vector3[rings + 1];
                for (int k = 0; k <= rings; k++)
                {
                    float t = k / (float)rings;
                    c[k] = root + new Vector3(lean.x * t * h, h * t, lean.z * t * h);
                }
                for (int k = 0; k < rings; k++)
                {
                    Color col = (k + i) % 3 == 0 ? Color.Lerp(BirchBark, BirchMark, 0.6f) : BirchBark * 1.02f;
                    float r0 = Mathf.Lerp(0.1f, 0.04f, k / (float)rings) * s, r1 = Mathf.Lerp(0.1f, 0.04f, (k + 1) / (float)rings) * s;
                    b.AddTube(new[] { c[k], c[k + 1] }, new[] { r0, r1 }, 5, b.Jitter(col, 0.06f), 0.05f, 0f, 0f, null, false, k == rings - 1);
                }
                if (i < 3)
                {
                    Vector3[] limb = Limb(b, Along(c, 0.8f), Dir(i * 110f, 50f), 0.9f * s, 0.03f * s, BirchMark * 2.4f, Vector3.zero, 2);
                    Twigs(b, limb, 2, 0.25f * s, 0.012f * s, BirchMark * 2.4f, 0.5f, 0.6f);
                }
            }
        }

        /// <summary>Hollow trunk: a rotted-out shell split open down one side, dark inside, with a broken rim.</summary>
        static void Hollow(LowPolyMeshBuilder b, float s, Color bark)
        {
            Roots(b, 5, 0.9f * s, 0.15f * s, bark, 70f);
            const int staves = 8;
            float r = 0.5f * s, h = 3.0f * s;
            for (int i = 0; i < staves; i++)
            {
                if (i == 0 || i == 1) continue;   // the open split
                float a0 = i * 360f / staves, a1 = (i + 1) * 360f / staves;
                float top = h * (i == 2 || i == staves - 1 ? 0.55f : b.Range(0.75f, 1.05f));
                Vector3 p0 = Dir(a0, 0f) * r, p1 = Dir(a1, 0f) * r;
                var corners = new[]
                {
                    p0 * 0.82f, p1 * 0.82f, p1, p0,
                    p0 * 0.72f + Vector3.up * top, p1 * 0.72f + Vector3.up * top * b.Range(0.9f, 1f), p1 * 0.88f + Vector3.up * top, p0 * 0.88f + Vector3.up * top,
                };
                b.AddHexahedron(corners, b.Jitter(bark, 0.08f), 0.08f);
            }
            // The dark rot inside, and a pile of punk wood at the bottom.
            b.AddFrustum(Vector3.zero, Vector3.up * h * 0.9f, r * 0.75f, r * 0.55f, 7, new Color(0.08f, 0.06f, 0.05f), 0.05f, 0f, false, false);
            b.AddBlob(new Vector3(0.1f, 0f, 0.35f) * s, new Vector3(0.35f, 0.15f, 0.3f) * s, 0, 0.25f, _ => b.Jitter(RotWood, 0.15f), true);
            Limb(b, Vector3.up * h * 0.8f + Dir(200f, 0f) * r * 0.8f, Dir(200f, 35f), 1.2f * s, 0.08f * s, bark, Vector3.zero, 2);
        }

        /// <summary>A fallen dead tree lying along +X: its torn-up root plate standing on edge at one end, broken stubs pointing up.</summary>
        static void Fallen(LowPolyMeshBuilder b, float s, Color bark)
        {
            float len = 7f * s, r = 0.32f * s;
            var c = new[] { new Vector3(0f, r * 0.9f, 0f), new Vector3(len * 0.35f, r * 0.85f, 0.08f * s), new Vector3(len * 0.7f, r * 0.7f, -0.05f * s), new Vector3(len, r * 0.55f, 0.04f * s) };
            b.AddTube(c, new[] { r, r * 0.85f, r * 0.62f, r * 0.4f }, 7, bark, 0.1f, 0.06f, 0f, null, true, true);
            // Root plate: a disc of soil and roots standing on edge at the base.
            float plateY = 1.18f * s;
            b.AddBlob(new Vector3(-0.15f * s, plateY, 0f), new Vector3(0.22f, 1.15f, 1.15f) * s, 1, 0.12f,
                local => b.Jitter(local.x < 0f ? new Color(0.26f, 0.21f, 0.16f) : bark * 0.8f, 0.12f), false);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 20f;
                Vector3 at = new Vector3(-0.2f * s, plateY, 0f) + new Vector3(0f, Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad)) * 1.0f * s;
                Vector3 d = (at - new Vector3(-0.2f * s, plateY, 0f)).normalized + Vector3.left * 0.6f;
                if (at.y + d.normalized.y * 0.6f * s < 0.05f) d.y = Mathf.Abs(d.y);   // roots point out of the plate, not into the ground
                Limb(b, at, d, 0.6f * s, 0.06f * s, bark * 0.75f, Vector3.zero, 1);
            }
            for (int i = 0; i < 7; i++)
            {
                Vector3 p = Along(c, Mathf.Lerp(0.2f, 0.95f, i / 6f));
                float up = i % 2 == 0 ? 1f : 0.12f;   // upright stubs, or broken limbs lying along the ground
                Limb(b, p, new Vector3(b.Range(-0.3f, 0.3f), up, b.Range(-0.8f, 0.8f)), Mathf.Lerp(1.3f, 0.5f, i / 6f) * s, 0.06f * s, bark, Vector3.zero, 2);
            }
        }
    }
}
