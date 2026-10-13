using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vision.Characters
{
    /// <summary>
    /// Builds a character from a <see cref="CharacterSpec"/>: a flat-shaded, vertex-coloured low-poly body skinned to the
    /// <see cref="HumanoidSkeleton"/> (so the procedural gait and the action clips drive every character alike), at most
    /// <see cref="MaxTriangles"/> triangles. A fourteen-sided torso in seven rings (hips, belly, waist, ribs, chest, upper
    /// chest, shoulders), a ten-sided head with a jaw, brow, nose and ears, eight-sided limbs with deltoid, bicep, elbow,
    /// forearm, knee and calf rings, mitten hands with a thumb, and shaped feet: a rounded heel, the instep rising to the
    /// ankle, a waisted arch, the broad ball and a toe box tapering to the big toe, on a flat sole; then hair, hats,
    /// glasses, a mask, a headset, a hood or bands as the spec asks. Joint rings are weighted half to each bone so joints
    /// bend cleanly.
    /// </summary>
    public static class CharacterBuilder
    {
        public const int MaxTriangles = 1000;
        /// <summary>Zach's budget: 500 more for his jacket's thickness and his mask (the user's allowance).</summary>
        public const int ZachMaxTriangles = 1500;
        const int TorsoSides = 14, HeadSides = 10, LimbSides = 8;

        struct Point
        {
            public Vector3 Position;
            public BoneWeight Weight;
        }

        sealed class Builder
        {
            public readonly List<Point> Points = new List<Point>(512);
            public readonly List<int> Triangles = new List<int>(MaxTriangles * 3);
            public readonly List<Color> Colors = new List<Color>(MaxTriangles);
            public Color Paint = Color.grey;
            /// <summary>Optional per-triangle recolour (by centroid), for checks and stripes.</summary>
            public System.Func<Vector3, int, Color, Color> Pattern;

            public int Add(Vector3 p, BoneWeight w)
            {
                Points.Add(new Point { Position = p, Weight = w });
                return Points.Count - 1;
            }

            public void Tri(int a, int b, int c, Vector3 inside)
            {
                Vector3 pa = Points[a].Position, pb = Points[b].Position, pc = Points[c].Position;
                Vector3 n = Vector3.Cross(pb - pa, pc - pa);
                if (n.sqrMagnitude < 1e-12f) return;
                Vector3 centroid = (pa + pb + pc) / 3f;
                bool flip = Vector3.Dot(n, centroid - inside) < 0f;
                Triangles.Add(a);
                Triangles.Add(flip ? c : b);
                Triangles.Add(flip ? b : c);
                Colors.Add(Pattern != null ? Pattern(centroid, Colors.Count, Paint) : Paint);
            }

            public void Quad(int a, int b, int c, int d, Vector3 inside)
            {
                Tri(a, b, c, inside);
                Tri(a, c, d, inside);
            }

            public int[] Ring(Vector3 center, Vector3 axis, int sides, float halfWidth, float front, float back, float phaseDeg, BoneWeight w)
            {
                Vector3 n = axis.y < 0f ? -axis.normalized : axis.normalized;
                Vector3 fz = (Vector3.forward - n * Vector3.Dot(Vector3.forward, n)).normalized;
                Vector3 fx = Vector3.Cross(n, fz);
                var ring = new int[sides];
                for (int s = 0; s < sides; s++)
                {
                    float a = (phaseDeg + s * 360f / sides) * Mathf.Deg2Rad;
                    float x = Mathf.Sin(a), z = Mathf.Cos(a);
                    var local = new Vector3(x * halfWidth, 0f, z * (z >= 0f ? front : back));
                    ring[s] = Add(center + fx * local.x + fz * local.z, w);
                }
                return ring;
            }

            public void Join(int[] a, int[] b, Vector3 inside)
            {
                for (int s = 0; s < a.Length; s++)
                {
                    int s1 = (s + 1) % a.Length;
                    Quad(a[s], b[s], b[s1], a[s1], inside);
                }
            }

            public void Fan(int[] ring, int apex, Vector3 inside)
            {
                for (int s = 0; s < ring.Length; s++) Tri(apex, ring[s], ring[(s + 1) % ring.Length], inside);
            }

            public Vector3 P(int i) => Points[i].Position;
        }

        static BoneWeight W(Bone a) => new BoneWeight { boneIndex0 = (int)a, weight0 = 1f };

        static BoneWeight W(Bone a, Bone b, float wb = 0.5f) =>
            new BoneWeight { boneIndex0 = (int)a, weight0 = 1f - wb, boneIndex1 = (int)b, weight1 = wb };

        static Vector3 J(Bone b) => HumanoidSkeleton.BindPosition(b);

        static Color Darker(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k);

        /// <summary>A stable pseudo-random 0-1 from an integer (patterns: stains, blotches).</summary>
        static float Hash01(int i)
        {
            unchecked
            {
                uint h = (uint)i * 2654435761u;
                h ^= h >> 15;
                h *= 0x2c1b3c6du;
                h ^= h >> 12;
                return (h & 0xFFFF) / 65536f;
            }
        }

        static Builder Construct(CharacterSpec s)
        {
            var m = new Builder();
            Vector3 up = Vector3.up;
            bool jacket = s.Jacket.a > 0f;
            // A field jacket is a separate shell over the body: the body itself wears the shirt.
            bool field = s.Has(Feature.FieldJacket);
            Color outer = jacket && !field ? s.Jacket : s.Shirt;
            Color hands = s.Gloves.a > 0f ? s.Gloves : s.Skin;
            float hz = s.Hunch;

            // ---- Torso: hips, belly, waist, ribs, chest, upper chest, shoulders (fourteen sides), a fan to the base of the neck.
            m.Pattern = (c, i, paint) =>
            {
                if (s.Has(Feature.OpenJacket) && jacket && c.z > 0.04f && Mathf.Abs(c.x) < 0.035f * s.Chest && c.y > 0.95f) return s.Shirt;
                if (s.Has(Feature.Flannel) && i % 2 == 1) return Darker(paint, 0.55f);
                return paint;
            };
            const float tp = 180f / TorsoSides;   // a face, not a corner, at the front centre
            float wb = Mathf.Lerp(s.Hips, s.Waist, 0.5f), rb = Mathf.Lerp(s.Waist, s.Chest, 0.5f), uc = Mathf.Lerp(s.Chest, s.Shoulders, 0.5f);
            int[] hips = m.Ring(new Vector3(0f, 0.88f, 0f), up, TorsoSides, 0.165f * s.Hips, 0.10f * s.Hips, 0.115f * s.Hips, tp, W(Bone.Pelvis));
            int[] belly = m.Ring(new Vector3(0f, 0.975f, hz * 0.15f), up, TorsoSides, 0.153f * wb, 0.099f * wb, 0.102f * wb, tp, W(Bone.Pelvis, Bone.Spine, 0.3f));
            int[] waist = m.Ring(new Vector3(0f, 1.07f, hz * 0.3f), up, TorsoSides, 0.140f * s.Waist, 0.095f * s.Waist, 0.085f * s.Waist, tp, W(Bone.Pelvis, Bone.Spine));
            int[] ribs = m.Ring(new Vector3(0f, 1.19f, -0.003f + hz * 0.5f), up, TorsoSides, 0.158f * rb, 0.11f * rb, 0.094f * rb, tp, W(Bone.Spine, Bone.Chest, 0.4f));
            int[] chest = m.Ring(new Vector3(0f, 1.30f, -0.005f + hz * 0.7f), up, TorsoSides, 0.170f * s.Chest, 0.12f * s.Chest, 0.10f * s.Chest, tp, W(Bone.Spine, Bone.Chest, 0.7f));
            int[] upperChest = m.Ring(new Vector3(0f, 1.39f, -0.008f + hz * 0.85f), up, TorsoSides, 0.19f * uc, 0.108f * s.Chest, 0.097f * s.Chest, tp, W(Bone.Chest));
            int[] shoulders = m.Ring(new Vector3(0f, 1.45f, -0.012f + hz), up, TorsoSides, 0.225f * s.Shoulders, 0.08f * s.Chest, 0.09f * s.Chest, tp, W(Bone.Chest));
            int neckBase = m.Add(new Vector3(0f, 1.505f, -0.015f + hz), W(Bone.Chest));
            m.Paint = s.Has(Feature.TornShirt) ? s.Shirt : outer;
            m.Join(hips, belly, new Vector3(0f, 0.93f, 0f));
            m.Join(belly, waist, new Vector3(0f, 1.02f, 0f));
            m.Paint = s.Has(Feature.HiVis) ? s.Accent : s.Has(Feature.Jersey) ? s.Accent : outer;
            m.Join(waist, ribs, new Vector3(0f, 1.13f, hz * 0.4f));
            m.Join(ribs, chest, new Vector3(0f, 1.24f, hz * 0.6f));
            m.Paint = outer;
            m.Join(chest, upperChest, new Vector3(0f, 1.34f, hz * 0.8f));
            m.Join(upperChest, shoulders, new Vector3(0f, 1.42f, hz * 0.9f));
            m.Fan(shoulders, neckBase, new Vector3(0f, 1.35f, hz));
            m.Pattern = null;

            // Zach's work shirt hangs in ragged tails below the belt.
            if (s.Has(Feature.TornShirt))
            {
                m.Paint = s.Shirt;
                int[] belt = m.Ring(new Vector3(0f, 0.93f, 0f), up, TorsoSides, 0.172f * s.Hips, 0.107f * s.Hips, 0.12f * s.Hips, tp, W(Bone.Pelvis));
                var tails = new int[TorsoSides];
                for (int i = 0; i < TorsoSides; i++)
                {
                    Vector3 p = m.P(belt[i]);
                    float drop = i % 2 == 0 ? 0.13f : 0.07f;
                    tails[i] = m.Add(new Vector3(p.x * 1.06f, 0.93f - drop, p.z * 1.06f), W(Bone.Pelvis));
                }
                m.Join(belt, tails, new Vector3(0f, 0.9f, 0f));
            }

            // ---- Neck (eight sides, open: buried in the shoulders and the head).
            m.Paint = s.Skin;
            float ng = s.Neck;
            int[] neckLow = m.Ring(new Vector3(0f, 1.44f, -0.018f + hz), up, 8, 0.054f * ng, 0.054f * ng, 0.054f * ng, 22.5f, W(Bone.Chest, Bone.Neck));
            int[] neckHigh = m.Ring(new Vector3(0f, 1.61f, 0f), up, 8, 0.048f * ng, 0.05f * ng, 0.046f * ng, 22.5f, W(Bone.Neck, Bone.Head));
            m.Join(neckLow, neckHigh, new Vector3(0f, 1.52f, -0.01f));

            if (field) FieldJacket(m, s, hz);

            // ---- Head (ten sides): chin, jaw, cheekbones, brow, crown, apex; a nose and ears.
            float k = s.Head;
            if (s.Has(Feature.Decayed))
            {
                // Bald and rotten: a third of the scalp's and neck's facets blotched grey or raw red-brown.
                Color raw = Color.Lerp(s.Skin, s.Hair, 0.7f), grey = Color.Lerp(s.Skin, new Color(0.45f, 0.45f, 0.42f), 0.5f);
                m.Pattern = (c, i, paint) => paint == s.Skin && Hash01(i * 13 + 5) < 0.34f ? (Hash01(i * 7 + 1) < 0.55f ? raw : grey) : paint;
            }
            Vector3 hc = new Vector3(0f, 1.69f, 0.005f);
            Vector3 HP(float y) => new Vector3(0f, hc.y + (y - hc.y) * k, 0f);
            int[] chin = m.Ring(HP(1.585f) + new Vector3(0f, 0f, 0.02f), up, HeadSides, 0.042f * k, 0.058f * k, 0.035f * k, 0f, W(Bone.Head));
            int[] jaw = m.Ring(HP(1.63f) + new Vector3(0f, 0f, 0.01f), up, HeadSides, 0.068f * k, 0.085f * k, 0.072f * k, 0f, W(Bone.Head));
            int[] cheek = m.Ring(HP(1.685f) + new Vector3(0f, 0f, 0.005f), up, HeadSides, 0.079f * k, 0.094f * k, 0.09f * k, 0f, W(Bone.Head));
            int[] brow = m.Ring(HP(1.745f), up, HeadSides, 0.077f * k, 0.094f * k, 0.09f * k, 0f, W(Bone.Head));
            int[] crown = m.Ring(HP(1.787f) + new Vector3(0f, 0f, -0.006f), up, HeadSides, 0.064f * k, 0.066f * k, 0.078f * k, 0f, W(Bone.Head));
            int apex = m.Add(HP(1.81f) + new Vector3(0f, 0f, -0.012f), W(Bone.Head));
            int chinPoint = m.Add(HP(1.572f) + new Vector3(0f, 0f, 0.012f), W(Bone.Head));
            Vector3 headIn = HP(1.69f);
            bool buzz = s.HairStyle == HairStyle.Buzz;
            m.Fan(chin, chinPoint, headIn);
            m.Join(chin, jaw, headIn);
            m.Join(jaw, cheek, headIn);
            m.Join(cheek, brow, headIn);
            if (buzz) m.Paint = s.Hair;
            m.Join(brow, crown, headIn);
            m.Fan(crown, apex, headIn);
            m.Paint = s.Skin;
            // Nose: a small wedge on the ridge between cheekbones and brow.
            {
                Vector3 top = m.P(brow[0]) + new Vector3(0f, -0.012f * k, 0.004f);
                Vector3 tip = m.P(cheek[0]) + new Vector3(0f, -0.004f, 0.02f * k);
                int nt = m.Add(top, W(Bone.Head));
                int np = m.Add(tip, W(Bone.Head));
                int nl = m.Add(m.P(cheek[0]) + new Vector3(-0.016f * k, 0.004f, -0.006f), W(Bone.Head));
                int nr = m.Add(m.P(cheek[0]) + new Vector3(0.016f * k, 0.004f, -0.006f), W(Bone.Head));
                Vector3 inside = m.P(cheek[0]) + new Vector3(0f, 0.01f, -0.03f);
                m.Tri(nt, np, nl, inside);
                m.Tri(nt, nr, np, inside);
                m.Tri(np, nr, nl, inside);
            }
            // Ears: a flat triangle each side.
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 e0 = new Vector3(side * 0.079f * k, hc.y + 0.02f * k, -0.005f);
                int a = m.Add(e0 + new Vector3(0f, 0.025f * k, 0f), W(Bone.Head));
                int b = m.Add(e0 + new Vector3(0f, -0.028f * k, 0.012f), W(Bone.Head));
                int c = m.Add(e0 + new Vector3(side * 0.012f, -0.005f, -0.025f * k), W(Bone.Head));
                m.Tri(a, b, c, new Vector3(0f, hc.y, 0f));
            }
            m.Pattern = null;

            // ---- Hair and hats.
            if (s.HairStyle == HairStyle.Short || s.HairStyle == HairStyle.Long || s.HairStyle == HairStyle.Curly || s.HairStyle == HairStyle.Cap || s.HairStyle == HairStyle.Bowl)
            {
                m.Paint = s.HairStyle == HairStyle.Cap ? s.Hair : s.Hair;
                bool curly = s.HairStyle == HairStyle.Curly;
                // The hairline: low at the back (nape), up at the forehead; then the crown, a little proud of the scalp.
                var line = new int[HeadSides];
                var top = new int[HeadSides];
                for (int i = 0; i < HeadSides; i++)
                {
                    float a = i * Mathf.PI * 2f / HeadSides;
                    float x = Mathf.Sin(a), z = Mathf.Cos(a);
                    bool bowl = s.HairStyle == HairStyle.Bowl;
                    float bump = curly ? (i % 2 == 0 ? 1.14f : 1.0f) : bowl ? 1.1f : 1.05f;
                    float y = Mathf.Lerp(1.66f, 1.765f, (z + 1f) * 0.5f);
                    if (s.HairStyle == HairStyle.Cap) y = 1.755f;
                    // A bowl cut: one straight edge all the way round, a heavy fringe down to the brows.
                    if (bowl) y = 1.728f;
                    line[i] = m.Add(new Vector3(x * 0.083f * k * bump, hc.y + (y - hc.y) * k, z * (z >= 0f ? 0.098f : 0.094f) * k * bump), W(Bone.Head));
                    float tb = curly ? (i % 2 == 1 ? 1.16f : 1.04f) : 1.06f;
                    top[i] = m.Add(new Vector3(x * 0.068f * k * tb, hc.y + (1.797f - hc.y) * k, (z * (z >= 0f ? 0.07f : 0.083f) - 0.006f) * k * tb), W(Bone.Head));
                }
                int crownTop = m.Add(new Vector3(0f, hc.y + ((curly ? 1.84f : 1.825f) - hc.y) * k, -0.012f), W(Bone.Head));
                m.Join(line, top, headIn);
                m.Fan(top, crownTop, headIn);
                if (s.HairStyle == HairStyle.Long)
                {
                    // Hair down the back to the shoulders.
                    int l3 = line[4], l4 = line[5], l5 = line[6];
                    int d3 = m.Add(m.P(l3) + new Vector3(0.01f, -0.20f, -0.02f), W(Bone.Neck, Bone.Head, 0.4f));
                    int d4 = m.Add(m.P(l4) + new Vector3(0f, -0.22f, -0.03f), W(Bone.Neck, Bone.Head, 0.4f));
                    int d5 = m.Add(m.P(l5) + new Vector3(-0.01f, -0.20f, -0.02f), W(Bone.Neck, Bone.Head, 0.4f));
                    Vector3 inside = new Vector3(0f, 1.58f, 0.02f);
                    m.Quad(l3, d3, d4, l4, inside);
                    m.Quad(l4, d4, d5, l5, inside);
                }
                if (s.HairStyle == HairStyle.Cap)
                {
                    // A backwards cap: the brim sticks out behind.
                    int b0 = line[4], b1 = line[6];
                    Vector3 back = (m.P(b0) + m.P(b1)) * 0.5f;
                    int t0 = m.Add(m.P(b0) + new Vector3(0f, -0.006f, -0.07f), W(Bone.Head));
                    int t1 = m.Add(m.P(b1) + new Vector3(0f, -0.006f, -0.07f), W(Bone.Head));
                    int bm = m.Add(back + new Vector3(0f, -0.004f, -0.09f), W(Bone.Head));
                    Vector3 above = back + new Vector3(0f, 0.08f, 0f), below = back + new Vector3(0f, -0.08f, 0f);
                    m.Tri(b0, t0, bm, below);
                    m.Tri(b0, bm, b1, below);
                    m.Tri(b1, bm, t1, below);
                    m.Tri(b0, bm, t0, above);
                    m.Tri(b1, t1, bm, above);
                }
            }

            // ---- A short goatee: a small wedge of beard on the chin, below the mouth.
            if (s.Has(Feature.Goatee))
            {
                m.Paint = s.Hair;
                float Z(float y) => Mathf.Lerp(0.082f, 0.097f, Mathf.InverseLerp(1.585f, 1.63f, y)) * k;
                Vector3 G(float x, float y) => new Vector3(x * k, hc.y + (y - hc.y) * k, Z(y) + 0.004f);
                int g0 = m.Add(G(-0.022f, 1.612f), W(Bone.Head)), g1 = m.Add(G(0.022f, 1.612f), W(Bone.Head));
                int g2 = m.Add(G(0.016f, 1.58f), W(Bone.Head)), g3 = m.Add(G(-0.016f, 1.58f), W(Bone.Head));
                int tip = m.Add(G(0f, 1.56f) + new Vector3(0f, 0f, -0.006f), W(Bone.Head));
                Vector3 behind = new Vector3(0f, hc.y - 0.08f, 0f);
                m.Quad(g0, g1, g2, g3, behind);
                m.Tri(g3, g2, tip, behind);
            }

            // ---- Glasses: two dark lenses and a bridge in front of the eyes.
            if (s.Has(Feature.Glasses))
            {
                m.Paint = s.Accent;
                float ey = hc.y + 0.022f * k, ez = 0.088f * k;
                foreach (int side in new[] { -1, 1 })
                {
                    float x0 = side * 0.012f * k, x1 = side * 0.052f * k;
                    int a = m.Add(new Vector3(x0, ey + 0.014f, ez + 0.006f), W(Bone.Head));
                    int b = m.Add(new Vector3(x1, ey + 0.014f, ez - 0.012f), W(Bone.Head));
                    int c = m.Add(new Vector3(x1, ey - 0.012f, ez - 0.012f), W(Bone.Head));
                    int d = m.Add(new Vector3(x0, ey - 0.012f, ez + 0.006f), W(Bone.Head));
                    m.Quad(a, b, c, d, new Vector3(0f, ey, 0f));
                    // The arm back to the ear.
                    int e = m.Add(new Vector3(side * 0.08f * k, ey + 0.01f, -0.01f), W(Bone.Head));
                    m.Tri(b, e, c, new Vector3(0f, ey, 0f));
                }
            }

            // ---- The hockey mask (MKX Jason's): an ivory plate curving round the face and standing off it, its rim turned back
            // to the face so it has thickness, rows of round vent holes, dark eye holes, red chevrons over the brows and on
            // the cheeks, and black straps round the back of the head and over the top.
            if (s.Has(Feature.HockeyMask))
            {
                float[] ys = { 1.792f, 1.752f, 1.705f, 1.655f, 1.607f, 1.566f };
                float[] xs = { -0.08f, -0.041f, 0f, 0.041f, 0.08f };
                float MZ(float x, float y)
                {
                    float u = Mathf.Abs(x) / 0.08f;
                    float z = 0.118f - u * u * 0.05f;
                    if (y < 1.62f) z -= (1.62f - y) * 0.4f;
                    if (y > 1.77f) z -= (y - 1.77f) * 0.7f;
                    return z;
                }
                float MX(float x, float y) => x * (y < 1.607f ? Mathf.Lerp(0.7f, 1f, (y - 1.566f) / 0.041f) : 1f);
                Vector3 MP(float x, float y, float lift) => new Vector3(MX(x, y) * k, hc.y + (y - hc.y) * k, (MZ(x, y) + lift) * k);
                var grid = new int[6, 5];
                var back = new int[6, 5];
                for (int r = 0; r < 6; r++)
                    for (int c = 0; c < 5; c++)
                    {
                        grid[r, c] = m.Add(MP(xs[c], ys[r], 0f), W(Bone.Head));
                        back[r, c] = m.Add(MP(xs[c], ys[r], -0.016f), W(Bone.Head));
                    }
                Vector3 behind = new Vector3(0f, hc.y, -0.05f);
                Color ivory = s.Accent, worn = Darker(s.Accent, 0.86f);
                for (int r = 0; r < 5; r++)
                    for (int c = 0; c < 4; c++)
                    {
                        m.Paint = Hash01(r * 5 + c + 11) < 0.25f ? worn : ivory;
                        m.Quad(grid[r, c], grid[r, c + 1], grid[r + 1, c + 1], grid[r + 1, c], behind);
                    }
                // The rim, all the way round.
                m.Paint = worn;
                Vector3 centre = new Vector3(0f, hc.y, 0.02f);
                void Rim(int r0, int c0, int r1, int c1) => m.Quad(grid[r0, c0], grid[r1, c1], back[r1, c1], back[r0, c0], centre);
                for (int c = 0; c < 4; c++) { Rim(0, c, 0, c + 1); Rim(5, c, 5, c + 1); }
                for (int r = 0; r < 5; r++) { Rim(r, 0, r + 1, 0); Rim(r, 4, r + 1, 4); }
                Vector3 OnMask(float x, float y) => MP(x, y, 0.004f);
                void Chevron(float x, float y, float w, float h)
                {
                    // An upside-down V: two strokes meeting at the bottom.
                    int tl = m.Add(OnMask(x - w, y + h), W(Bone.Head));
                    int tl2 = m.Add(OnMask(x - w + 0.009f, y + h), W(Bone.Head));
                    int bot = m.Add(OnMask(x, y), W(Bone.Head));
                    int bot2 = m.Add(OnMask(x, y + 0.011f), W(Bone.Head));
                    int tr = m.Add(OnMask(x + w, y + h), W(Bone.Head));
                    int tr2 = m.Add(OnMask(x + w - 0.009f, y + h), W(Bone.Head));
                    m.Quad(tl, tl2, bot2, bot, behind);
                    m.Quad(bot, bot2, tr2, tr, behind);
                }
                m.Paint = s.Accent2;
                Chevron(-0.03f, 1.75f, 0.018f, 0.022f);
                Chevron(0.03f, 1.75f, 0.018f, 0.022f);
                Chevron(-0.052f, 1.66f, 0.014f, 0.018f);
                Chevron(0.052f, 1.66f, 0.014f, 0.018f);
                // Eye holes, and rows of round vents.
                m.Paint = new Color(0.035f, 0.03f, 0.03f);
                foreach (int side in new[] { -1, 1 })
                {
                    int a = m.Add(OnMask(side * 0.015f, 1.728f), W(Bone.Head));
                    int b = m.Add(OnMask(side * 0.05f, 1.726f), W(Bone.Head));
                    int c = m.Add(OnMask(side * 0.047f, 1.7f), W(Bone.Head));
                    int d = m.Add(OnMask(side * 0.017f, 1.702f), W(Bone.Head));
                    m.Quad(a, b, c, d, behind);
                }
                (float y, float[] x)[] vents =
                {
                    (1.782f, new[] { 0.012f, 0.034f, 0.058f }),
                    (1.676f, new[] { 0.024f, 0.074f }),
                    (1.64f, new[] { 0.014f, 0.036f, 0.06f }),
                    (1.618f, new[] { 0.025f, 0.048f }),
                    (1.592f, new[] { 0.012f, 0.032f }),
                };
                foreach (var (vy, vx) in vents)
                    foreach (float x0 in vx)
                        foreach (int side in new[] { -1, 1 })
                        {
                            float x = side * x0, r = 0.0058f;
                            int a = m.Add(OnMask(x, vy + r), W(Bone.Head));
                            int b = m.Add(OnMask(x + r, vy), W(Bone.Head));
                            int c = m.Add(OnMask(x, vy - r), W(Bone.Head));
                            int d = m.Add(OnMask(x - r, vy), W(Bone.Head));
                            m.Quad(a, b, c, d, behind);
                        }
                // The straps: round the back of the head at the temples, and over the crown from the mask's top.
                m.Paint = s.Gloves.a > 0f ? s.Gloves : new Color(0.08f, 0.08f, 0.08f);
                float sy = hc.y + (1.752f - hc.y) * k;
                int prevT = -1, prevB = -1;
                for (int i = 0; i <= 6; i++)
                {
                    float a = Mathf.Lerp(Mathf.PI * 0.42f, Mathf.PI * 1.58f, i / 6f);
                    float x = Mathf.Sin(a), z = Mathf.Cos(a);
                    var p = new Vector3(x * (0.077f * k + 0.007f), 0f, z * ((z >= 0f ? 0.094f : 0.09f) * k + 0.007f));
                    int t = m.Add(p + new Vector3(0f, sy + 0.012f, 0f), W(Bone.Head));
                    int bb = m.Add(p + new Vector3(0f, sy - 0.012f, 0f), W(Bone.Head));
                    if (prevT >= 0) m.Quad(prevT, t, bb, prevB, new Vector3(0f, sy, 0f));
                    prevT = t;
                    prevB = bb;
                }
                Vector3 crownC = new Vector3(0f, hc.y + 0.01f, -0.005f);
                float ry = (1.81f - hc.y) * k + 0.008f, rz = 0.092f * k + 0.007f;
                int prevL = -1, prevR = -1;
                for (int i = 0; i <= 4; i++)
                {
                    float a = Mathf.Lerp(Mathf.PI * 0.32f, Mathf.PI * 0.95f, i / 4f);
                    var p = crownC + new Vector3(0f, Mathf.Sin(a) * ry, Mathf.Cos(a) * rz);
                    int l = m.Add(p + new Vector3(-0.012f, 0f, 0f), W(Bone.Head));
                    int r = m.Add(p + new Vector3(0.012f, 0f, 0f), W(Bone.Head));
                    if (prevL >= 0) m.Quad(prevL, l, r, prevR, crownC);
                    prevL = l;
                    prevR = r;
                }
            }

            // ---- Headset: a pink band over the head, cyan-lit ear cups.
            if (s.Has(Feature.Headset))
            {
                m.Paint = s.Accent;
                float r = 0.092f * k;
                int prevA = -1, prevB = -1;
                for (int i = 0; i <= 4; i++)
                {
                    float a = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, i / 4f);
                    Vector3 p = new Vector3(Mathf.Sin(a) * r * 1.05f, hc.y + 0.02f + Mathf.Cos(a) * (0.17f * k), -0.012f);
                    int pa = m.Add(p + new Vector3(0f, 0f, 0.012f), W(Bone.Head));
                    int pb = m.Add(p + new Vector3(0f, 0f, -0.012f), W(Bone.Head));
                    if (prevA >= 0) m.Quad(prevA, pa, pb, prevB, new Vector3(0f, hc.y, 0f));
                    prevA = pa;
                    prevB = pb;
                }
                foreach (int side in new[] { -1, 1 })
                {
                    m.Paint = s.Accent2;
                    Vector3 cc = new Vector3(side * 0.086f * k, hc.y + 0.015f, -0.012f);
                    int[] inner = m.Ring(cc, Vector3.right, 4, 0.026f, 0.03f, 0.03f, 45f, W(Bone.Head));
                    int outerPt = m.Add(cc + new Vector3(side * 0.022f, 0f, 0f), W(Bone.Head));
                    m.Fan(inner, outerPt, cc - new Vector3(side * 0.01f, 0f, 0f));
                }
            }

            // ---- A hood lying behind the neck.
            if (s.Has(Feature.Hood))
            {
                m.Paint = outer;
                var lo = new int[5];
                var hi = new int[5];
                for (int i = 0; i < 5; i++)
                {
                    float a = Mathf.Lerp(Mathf.PI * 0.55f, Mathf.PI * 1.45f, i / 4f);
                    lo[i] = m.Add(new Vector3(Mathf.Sin(a) * 0.12f * s.Shoulders, 1.46f, Mathf.Cos(a) * 0.1f - 0.02f + hz), W(Bone.Chest));
                    hi[i] = m.Add(new Vector3(Mathf.Sin(a) * 0.09f * s.Shoulders, 1.56f, Mathf.Cos(a) * 0.11f - 0.04f + hz), W(Bone.Chest, Bone.Neck, 0.3f));
                }
                Vector3 inside = new Vector3(0f, 1.5f, 0.03f + hz);
                for (int i = 0; i < 4; i++) m.Quad(lo[i], lo[i + 1], hi[i + 1], hi[i], inside);
            }

            // ---- A star of life on the back.
            if (s.Has(Feature.StarOfLife))
            {
                m.Paint = s.Accent2;
                Vector3 c = new Vector3(0f, 1.3f, -0.105f * s.Chest - 0.006f + hz * 0.7f);
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.PI / 3f;
                    Vector3 d = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f) * 0.05f, w = new Vector3(Mathf.Cos(a), -Mathf.Sin(a), 0f) * 0.011f;
                    int p0 = m.Add(c - d - w, W(Bone.Chest)), p1 = m.Add(c - d + w, W(Bone.Chest)), p2 = m.Add(c + d + w, W(Bone.Chest)), p3 = m.Add(c + d - w, W(Bone.Chest));
                    m.Quad(p0, p1, p2, p3, c + new Vector3(0f, 0f, 0.05f));
                }
            }

            foreach (int side in new[] { -1, 1 })
            {
                bool left = side < 0;
                Bone upper = left ? Bone.UpperArmL : Bone.UpperArmR, fore = upper + 1, hand = upper + 2;
                Bone thigh = left ? Bone.ThighL : Bone.ThighR, shin = thigh + 1, foot = thigh + 2, toe = thigh + 3;
                float ag = s.Arms, lg = s.Legs;

                // ---- Arm: deltoid cap, shoulder, bicep, elbow, forearm, wrist (eight sides); a mitten hand with a thumb.
                Vector3 sh = J(upper), el = J(fore), wr = J(hand);
                Vector3 shOut = sh + new Vector3(side * (0.225f * s.Shoulders - 0.215f), 0f, hz);
                var knuckle = wr + new Vector3(side * 0.004f, -0.095f, 0.004f);
                var tip = wr + new Vector3(side * 0.006f, -0.148f, 0.01f);
                m.Paint = s.Has(Feature.TornShirt) ? s.Shirt : field ? s.Jacket : outer;
                float cg = Mathf.Lerp(1f, ag, 0.55f);
                int[] cap = m.Ring(new Vector3(side * (0.195f + (0.225f * s.Shoulders - 0.225f)), 1.478f, -0.008f + hz), up, LimbSides, 0.06f * cg, 0.06f * cg, 0.06f * cg, 22.5f, W(Bone.Chest, upper));
                int capTop = m.Add(new Vector3(side * (0.172f + (0.225f * s.Shoulders - 0.225f)), 1.50f, -0.01f + hz), W(Bone.Chest, upper, 0.3f));
                int[] a0 = m.Ring(shOut, el - sh, LimbSides, 0.056f * ag, 0.054f * ag, 0.054f * ag, 22.5f, W(upper, Bone.Chest, 0.3f));
                m.Join(cap, a0, new Vector3(side * 0.19f, 1.45f, -0.005f + hz));
                m.Fan(cap, capTop, new Vector3(side * 0.19f, 1.45f, -0.005f + hz));
                Vector3 bicepAt = Vector3.Lerp(sh, el, 0.45f) + new Vector3(side * (0.225f * s.Shoulders - 0.225f) * 0.5f, 0f, hz * 0.5f);
                int[] a1 = m.Ring(bicepAt, el - sh, LimbSides, 0.054f * ag, 0.056f * ag, 0.05f * ag, 22.5f, W(upper));
                int[] a2 = m.Ring(el, wr - sh, LimbSides, 0.043f * ag, 0.044f * ag, 0.043f * ag, 22.5f, W(upper, fore));
                Vector3 foreAt = Vector3.Lerp(el, wr, 0.35f);
                int[] a2b = m.Ring(foreAt, wr - el, LimbSides, 0.041f * ag, 0.042f * ag, 0.04f * ag, 22.5f, W(fore));
                int[] a3 = m.Ring(wr, wr - el, LimbSides, 0.031f * ag, 0.033f * ag, 0.033f * ag, 22.5f, W(fore, hand));
                m.Join(a0, a1, (sh + bicepAt) * 0.5f);
                m.Join(a1, a2, (bicepAt + el) * 0.5f);
                // Forearm: sleeve, or bare when the shirt has short sleeves (or Zach's are torn off), with hi-vis cuffs.
                m.Paint = s.Has(Feature.TornShirt) || !jacket ? s.Skin : s.Jacket;
                if (s.Has(Feature.HiVis)) m.Paint = s.Accent;
                m.Join(a2, a2b, (el + foreAt) * 0.5f);
                m.Join(a2b, a3, (foreAt + wr) * 0.5f);
                if (field)
                {
                    // The jacket's sleeve: a shell of cloth over the arm to just short of the wrist, its cuff showing its
                    // thickness.
                    const float th = 0.02f;
                    Vector3 cuffAt = Vector3.Lerp(foreAt, wr, 0.72f);
                    int[] s0 = m.Ring(shOut, el - sh, LimbSides, 0.056f * ag + th, 0.054f * ag + th, 0.054f * ag + th, 22.5f, W(upper, Bone.Chest, 0.3f));
                    int[] s1 = m.Ring(bicepAt, el - sh, LimbSides, 0.054f * ag + th, 0.056f * ag + th, 0.05f * ag + th, 22.5f, W(upper));
                    int[] s2 = m.Ring(el, wr - sh, LimbSides, 0.043f * ag + th, 0.044f * ag + th, 0.043f * ag + th, 22.5f, W(upper, fore));
                    int[] s3 = m.Ring(foreAt, wr - el, LimbSides, 0.041f * ag + th, 0.042f * ag + th, 0.04f * ag + th, 22.5f, W(fore));
                    int[] s4 = m.Ring(cuffAt, wr - el, LimbSides, 0.036f * ag + th, 0.038f * ag + th, 0.037f * ag + th, 22.5f, W(fore));
                    int[] s4in = m.Ring(cuffAt, wr - el, LimbSides, 0.036f * ag + 0.003f, 0.038f * ag + 0.003f, 0.037f * ag + 0.003f, 22.5f, W(fore));
                    m.Paint = s.Jacket;
                    m.Pattern = JacketWear(s.Jacket);
                    m.Join(s0, s1, (sh + bicepAt) * 0.5f);
                    m.Join(s1, s2, (bicepAt + el) * 0.5f);
                    m.Join(s2, s3, (el + foreAt) * 0.5f);
                    m.Join(s3, s4, (foreAt + cuffAt) * 0.5f);
                    m.Pattern = null;
                    m.Paint = Darker(s.Jacket, 0.55f);
                    m.Join(s4, s4in, foreAt);
                }
                m.Paint = hands;
                int[] a4 = m.Ring(knuckle, knuckle - wr, LimbSides, 0.021f * ag, 0.05f * ag, 0.046f * ag, 22.5f, W(hand));
                int fingertip = m.Add(tip, W(hand));
                m.Join(a3, a4, (wr + knuckle) * 0.5f);
                m.Fan(a4, fingertip, knuckle);
                // Thumb: a wedge on the front of the hand.
                {
                    int t0 = m.Add(wr + new Vector3(side * 0.006f, -0.03f, 0.03f * ag), W(hand));
                    int t1 = m.Add(wr + new Vector3(side * 0.004f, -0.1f, 0.045f * ag), W(hand));
                    int t2 = m.Add(wr + new Vector3(-side * 0.012f, -0.06f, 0.035f * ag), W(hand));
                    int t3 = m.Add(wr + new Vector3(side * 0.02f, -0.06f, 0.035f * ag), W(hand));
                    Vector3 inside = wr + new Vector3(0f, -0.06f, 0f);
                    m.Tri(t0, t1, t2, inside);
                    m.Tri(t0, t3, t1, inside);
                }

                // ---- Leg: hip, thigh, knee, calf, boot cuff, ankle (eight sides); a shaped foot.
                Vector3 hip = J(thigh), knee = J(shin);
                float x = hip.x;
                var hipTop = new Vector3(x * Mathf.Max(1f, s.Hips * 0.95f), 0.97f, 0f);
                var calf = new Vector3(x, 0.33f, -0.012f);
                var cuff = new Vector3(x, 0.17f, -0.004f);
                var ankle = new Vector3(x, 0.075f, 0f);
                m.Paint = s.Pants;
                var midThigh = Vector3.Lerp(hipTop, knee, 0.5f) + new Vector3(0f, 0f, 0.006f);
                int[] l0 = m.Ring(hipTop, knee - hipTop, LimbSides, 0.08f * lg, 0.08f * lg, 0.08f * lg, 22.5f, W(thigh));
                int[] l0b = m.Ring(midThigh, knee - hipTop, LimbSides, 0.068f * lg, 0.072f * lg, 0.066f * lg, 22.5f, W(thigh));
                int[] l1 = m.Ring(knee, ankle - hipTop, LimbSides, 0.054f * lg, 0.056f * lg, 0.05f * lg, 22.5f, W(thigh, shin));
                int[] l2 = m.Ring(calf, ankle - knee, LimbSides, 0.052f * lg, 0.046f * lg, 0.058f * lg, 22.5f, W(shin));
                int[] l3 = m.Ring(cuff, ankle - knee, LimbSides, 0.046f * lg, 0.046f * lg, 0.046f * lg, 22.5f, W(shin));
                int[] l4 = m.Ring(ankle, ankle - knee, LimbSides, 0.04f * lg, 0.043f * lg, 0.043f * lg, 22.5f, W(shin, foot));
                m.Join(l0, l0b, (hipTop + midThigh) * 0.5f);
                m.Join(l0b, l1, (midThigh + knee) * 0.5f);
                m.Join(l1, l2, (knee + calf) * 0.5f);
                m.Join(l2, l3, (calf + cuff) * 0.5f);
                m.Paint = s.Shoes;
                m.Join(l3, l4, (cuff + ankle) * 0.5f);

                // The foot, lofted heel to toe through rings with a flat sole and a rounded top: the heel's curve, the instep
                // up to the ankle, the arch, the broad ball and the toe box narrowing to the big toe on the inside.
                float fw = Mathf.Lerp(1f, lg, 0.5f);
                (float z, float w, float h, float dx, BoneWeight wt)[] sections = Foot(foot, toe);
                var rings = new int[sections.Length][];
                for (int r = 0; r < sections.Length; r++)
                {
                    var (fz, hw, fh, dx, wt) = sections[r];
                    rings[r] = new int[8];
                    for (int q = 0; q < 8; q++)
                    {
                        float a = (q + 0.5f) * Mathf.PI / 4f;
                        float px = x + side * dx + Mathf.Cos(a) * hw * fw;
                        float py = Mathf.Max(0f, fh * 0.5f + Mathf.Sin(a) * fh * 0.5f);
                        rings[r][q] = m.Add(new Vector3(px, py, fz), wt);
                    }
                }
                m.Pattern = (c, i2, paint) => c.y < 0.012f ? Darker(s.Shoes, 0.55f) : paint;
                m.Paint = s.Shoes;
                for (int r = 0; r + 1 < sections.Length; r++)
                    m.Join(rings[r], rings[r + 1], new Vector3(x + side * sections[r].dx, sections[r + 1].h * 0.4f, (sections[r].z + sections[r + 1].z) * 0.5f));
                int heelPt = m.Add(new Vector3(x, 0.03f, sections[0].z - 0.012f), sections[0].wt);
                m.Fan(rings[0], heelPt, new Vector3(x, 0.035f, sections[0].z + 0.03f));
                int last = sections.Length - 1;
                int toePt = m.Add(new Vector3(x - side * 0.011f, 0.017f, sections[last].z + 0.034f), W(toe));
                m.Fan(rings[last], toePt, new Vector3(x - side * 0.008f, 0.02f, sections[last].z - 0.02f));
                m.Pattern = null;
            }
            return m;
        }

        /// <summary>The jacket's worn cloth: some facets stained darker, some faded lighter.</summary>
        static System.Func<Vector3, int, Color, Color> JacketWear(Color cloth)
        {
            Color stain = Darker(cloth, 0.7f), faded = Color.Lerp(cloth, new Color(0.85f, 0.83f, 0.7f), 0.14f);
            return (c, i, paint) => paint != cloth ? paint : Hash01(i * 31 + 7) < 0.16f ? stain : Hash01(i * 17 + 3) < 0.28f ? faded : paint;
        }

        /// <summary>
        /// Zach's field jacket over the torso: a fourteen-sided shell of cloth standing a couple of centimetres off the body
        /// from below the belt to the shoulders, open down the front over the shirt, with a stand-up collar round the back
        /// and sides of the neck and four pocket flaps. Its thickness shows where it ends: the hem, both front edges, the
        /// collar's top and inside, each a band of darker lining joining the outside to the inside of the cloth.
        /// </summary>
        static void FieldJacket(Builder m, CharacterSpec s, float hz)
        {
            const float th = 0.024f, tp = 180f / TorsoSides;
            Vector3 up = Vector3.up;
            Color cloth = s.Jacket, lining = Darker(s.Jacket, 0.5f);
            // Each ring: height, forward shift, half width, front and back depth (of the shell's outside), weights.
            var sec = new (float y, float z, float hw, float f, float b, BoneWeight w)[]
            {
                (0.84f, hz * 0.05f, 0.172f * s.Hips + th + 0.012f, 0.108f * s.Hips + th + 0.012f, 0.122f * s.Hips + th + 0.012f, W(Bone.Pelvis)),
                (0.95f, hz * 0.15f, 0.16f * s.Hips + th, 0.102f * s.Hips + th, 0.11f * s.Hips + th, W(Bone.Pelvis, Bone.Spine, 0.2f)),
                (1.12f, hz * 0.4f, 0.15f * s.Waist + th, 0.104f * s.Waist + th, 0.09f * s.Waist + th, W(Bone.Pelvis, Bone.Spine, 0.7f)),
                (1.30f, -0.005f + hz * 0.7f, 0.17f * s.Chest + th, 0.12f * s.Chest + th, 0.10f * s.Chest + th, W(Bone.Spine, Bone.Chest, 0.7f)),
                (1.45f, -0.012f + hz, 0.225f * s.Shoulders + th, 0.08f * s.Chest + th, 0.09f * s.Chest + th, W(Bone.Chest)),
            };
            int n = sec.Length;
            var outside = new int[n][];
            var inside = new int[n][];
            for (int r = 0; r < n; r++)
            {
                var (y, z, hw, f, b, w) = sec[r];
                outside[r] = m.Ring(new Vector3(0f, y, z), up, TorsoSides, hw, f, b, tp, w);
                inside[r] = m.Ring(new Vector3(0f, y, z), up, TorsoSides, hw - th * 0.85f, f - th * 0.85f, b - th * 0.85f, tp, w);
            }
            // Open down the front: the three faces either side of the front centre (vertices 12 to 1).
            bool Open(int face) => face == 12 || face == 13 || face == 0;
            m.Paint = cloth;
            m.Pattern = JacketWear(cloth);
            for (int r = 0; r + 1 < n; r++)
                for (int f = 0; f < TorsoSides; f++)
                {
                    if (Open(f)) continue;
                    int f1 = (f + 1) % TorsoSides;
                    m.Quad(outside[r][f], outside[r + 1][f], outside[r + 1][f1], outside[r][f1], new Vector3(0f, (sec[r].y + sec[r + 1].y) * 0.5f, sec[r].z));
                }

            // Over the shoulders to the collar's foot.
            float ng = s.Neck;
            Vector3 neckAt = new Vector3(0f, 1.5f, -0.016f + hz);
            int[] foot = m.Ring(neckAt, up, TorsoSides, 0.06f * ng + th, 0.058f * ng + th, 0.062f * ng + th, tp, W(Bone.Chest));
            for (int f = 0; f < TorsoSides; f++)
            {
                if (Open(f)) continue;
                int f1 = (f + 1) % TorsoSides;
                m.Quad(outside[n - 1][f], foot[f], foot[f1], outside[n - 1][f1], new Vector3(0f, 1.38f, hz));
            }

            // Pocket flaps: two on the chest, two low on the front, standing proud of the cloth with a thick lower edge.
            float FrontZ(int r, float x)
            {
                var (_, z, hw, f, _, _) = sec[r];
                float u = Mathf.Clamp01(Mathf.Abs(x) / hw);
                return z + f * Mathf.Sqrt(1f - u * u);
            }
            foreach (int side in new[] { -1, 1 })
                foreach (var (r, fy, fx, fw) in new[] { (3, 1.31f, 0.2f * s.Chest, 0.07f), (1, 0.98f, 0.17f * s.Hips, 0.08f) })
                {
                    float x0 = side * (fx - fw * 0.5f), x1 = side * (fx + fw * 0.5f);
                    BoneWeight w = sec[r].w;
                    float z0 = FrontZ(r, x0), z1 = FrontZ(r, x1);
                    int a = m.Add(new Vector3(x0, fy + 0.02f, z0 + 0.002f), w), b = m.Add(new Vector3(x1, fy + 0.02f, z1 + 0.002f), w);
                    int c = m.Add(new Vector3(x1, fy - 0.022f, z1 + 0.009f), w), d = m.Add(new Vector3(x0, fy - 0.022f, z0 + 0.009f), w);
                    int c2 = m.Add(new Vector3(x1, fy - 0.022f, z1 - 0.004f), w), d2 = m.Add(new Vector3(x0, fy - 0.022f, z0 - 0.004f), w);
                    Vector3 behindFlap = new Vector3(side * fx, fy, z0 - 0.1f);
                    m.Paint = Darker(cloth, 0.86f);
                    m.Quad(a, b, c, d, behindFlap);
                    m.Paint = lining;
                    m.Quad(d, c, c2, d2, new Vector3(side * fx, fy + 0.1f, z0));
                }
            m.Pattern = null;

            // The thickness: a band of lining joining the outside to the inside along the hem and both front edges.
            m.Paint = lining;
            for (int f = 0; f < TorsoSides; f++)
            {
                if (Open(f)) continue;
                int f1 = (f + 1) % TorsoSides;
                m.Quad(outside[0][f], outside[0][f1], inside[0][f1], inside[0][f], new Vector3(0f, 1.1f, 0f));
            }
            for (int r = 0; r + 1 < n; r++)
                foreach (int e in new[] { 12, 1 })
                {
                    Vector3 mid = (m.P(outside[r][e]) + m.P(outside[r + 1][e])) * 0.5f;
                    m.Quad(outside[r][e], outside[r + 1][e], inside[r + 1][e], inside[r][e], new Vector3(-mid.x * 0.2f, mid.y, mid.z - 0.25f));
                }

            // The collar: standing up round the back and sides of the neck, open at the front, with its top edge and its
            // inside showing.
            bool CollarOpen(int face) => face >= 11 || face <= 1;
            Vector3 topAt = new Vector3(0f, 1.585f, -0.024f + hz);
            int[] top = m.Ring(topAt, up, TorsoSides, 0.066f * ng + th + 0.012f, 0.062f * ng + th + 0.01f, 0.07f * ng + th + 0.014f, tp, W(Bone.Chest, Bone.Neck, 0.25f));
            int[] topIn = m.Ring(topAt, up, TorsoSides, 0.066f * ng + 0.012f, 0.062f * ng + 0.01f, 0.07f * ng + 0.014f, tp, W(Bone.Chest, Bone.Neck, 0.25f));
            int[] footIn = m.Ring(neckAt, up, TorsoSides, 0.06f * ng + 0.004f, 0.058f * ng + 0.004f, 0.062f * ng + 0.004f, tp, W(Bone.Chest));
            for (int f = 0; f < TorsoSides; f++)
            {
                if (CollarOpen(f)) continue;
                int f1 = (f + 1) % TorsoSides;
                Vector3 mid = (m.P(foot[f]) + m.P(top[f1])) * 0.5f;
                Vector3 radial = new Vector3(mid.x, 0f, mid.z - topAt.z).normalized;
                m.Paint = cloth;
                m.Quad(foot[f], top[f], top[f1], foot[f1], mid - radial * 0.06f);
                m.Paint = lining;
                m.Quad(top[f], topIn[f], topIn[f1], top[f1], mid - up * 0.08f);
                m.Quad(footIn[f], topIn[f], topIn[f1], footIn[f1], mid + radial * 0.06f);
            }
        }

        /// <summary>
        /// The foot's cross-sections from the back of the heel to the toe box: (z, half width, height, shift toward the
        /// outside of the foot, weights). The heel and arch ride on the foot bone, the ball bends with the toe bone.
        /// </summary>
        static (float z, float w, float h, float dx, BoneWeight wt)[] Foot(Bone foot0, Bone toe0) => new[]
        {
            (-0.085f, 0.027f, 0.064f, 0f, W(foot0)),
            (-0.064f, 0.038f, 0.088f, 0f, W(foot0)),
            (-0.01f, 0.043f, 0.106f, 0.002f, W(foot0)),
            (0.06f, 0.046f, 0.08f, 0.004f, W(foot0)),
            (0.13f, 0.053f, 0.057f, 0.001f, W(foot0, toe0)),
            (0.186f, 0.045f, 0.042f, -0.005f, W(toe0)),
        };

        /// <summary>Triangle count of a character.</summary>
        public static int Triangles(CharacterSpec spec) => Construct(spec).Triangles.Count / 3;

        /// <summary>Builds the skinned, vertex-coloured mesh (bind poses for <see cref="HumanoidSkeleton"/>).</summary>
        public static Mesh Build(CharacterSpec spec)
        {
            Builder m = Construct(spec);
            int n = m.Triangles.Count;
            var vertices = new Vector3[n];
            var normals = new Vector3[n];
            var colors = new Color[n];
            var weights = new BoneWeight[n];
            var indices = new int[n];
            for (int i = 0; i < n; i += 3)
            {
                Point a = m.Points[m.Triangles[i]], b = m.Points[m.Triangles[i + 1]], c = m.Points[m.Triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b.Position - a.Position, c.Position - a.Position).normalized;
                vertices[i] = a.Position; vertices[i + 1] = b.Position; vertices[i + 2] = c.Position;
                weights[i] = a.Weight; weights[i + 1] = b.Weight; weights[i + 2] = c.Weight;
                Color col = m.Colors[i / 3].linear;
                for (int k = 0; k < 3; k++)
                {
                    normals[i + k] = normal;
                    colors[i + k] = col;
                    indices[i + k] = i + k;
                }
            }
            var mesh = new Mesh { name = spec.Name, indexFormat = IndexFormat.UInt16 };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.triangles = indices;
            mesh.boneWeights = weights;
            mesh.bindposes = HumanoidSkeleton.BindPoses();
            mesh.RecalculateBounds();
            return mesh;
        }

        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        /// <summary>The mesh for a spec, built once per name.</summary>
        public static Mesh Shared(CharacterSpec spec)
        {
            if (cache.TryGetValue(spec.Name, out Mesh mesh) && mesh != null) return mesh;
            mesh = Build(spec);
            mesh.hideFlags = HideFlags.DontSave;
            cache[spec.Name] = mesh;
            return mesh;
        }
    }
}
