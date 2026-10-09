using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vision.Characters
{
    /// <summary>
    /// Builds a character from a <see cref="CharacterSpec"/>: a flat-shaded, vertex-coloured low-poly body skinned to the
    /// <see cref="HumanoidSkeleton"/> (so the procedural gait and the action clips drive every character alike), at most
    /// <see cref="MaxTriangles"/> triangles. Ten-sided torso, eight-sided head with a jaw, brow, nose and ears, six-sided
    /// limbs with knee, elbow and boot rings, mitten hands with a thumb, wedge feet; then hair, hats, glasses, a mask, a
    /// headset, a hood or bands as the spec asks. Joint rings are weighted half to each bone so joints bend cleanly.
    /// </summary>
    public static class CharacterBuilder
    {
        public const int MaxTriangles = 500;

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

        static Builder Construct(CharacterSpec s)
        {
            var m = new Builder();
            Vector3 up = Vector3.up;
            bool jacket = s.Jacket.a > 0f;
            Color outer = jacket ? s.Jacket : s.Shirt;
            Color hands = s.Gloves.a > 0f ? s.Gloves : s.Skin;
            float hz = s.Hunch;

            // ---- Torso: hips, waist, chest, shoulders (ten sides), a fan to the base of the neck.
            m.Pattern = (c, i, paint) =>
            {
                if (s.Has(Feature.OpenJacket) && jacket && c.z > 0.04f && Mathf.Abs(c.x) < 0.035f * s.Chest && c.y > 0.95f) return s.Shirt;
                if (s.Has(Feature.Flannel) && i % 2 == 1) return Darker(paint, 0.55f);
                return paint;
            };
            int[] hips = m.Ring(new Vector3(0f, 0.88f, 0f), up, 10, 0.165f * s.Hips, 0.10f * s.Hips, 0.11f * s.Hips, 18f, W(Bone.Pelvis));
            int[] waist = m.Ring(new Vector3(0f, 1.07f, hz * 0.3f), up, 10, 0.140f * s.Waist, 0.095f * s.Waist, 0.085f * s.Waist, 18f, W(Bone.Pelvis, Bone.Spine));
            int[] chest = m.Ring(new Vector3(0f, 1.30f, -0.005f + hz * 0.7f), up, 10, 0.170f * s.Chest, 0.12f * s.Chest, 0.10f * s.Chest, 18f, W(Bone.Spine, Bone.Chest, 0.7f));
            int[] shoulders = m.Ring(new Vector3(0f, 1.45f, -0.012f + hz), up, 10, 0.225f * s.Shoulders, 0.08f * s.Chest, 0.09f * s.Chest, 18f, W(Bone.Chest));
            int neckBase = m.Add(new Vector3(0f, 1.505f, -0.015f + hz), W(Bone.Chest));
            m.Paint = s.Has(Feature.TornShirt) ? s.Shirt : outer;
            m.Join(hips, waist, new Vector3(0f, 0.97f, 0f));
            m.Paint = s.Has(Feature.HiVis) ? s.Accent : s.Has(Feature.Jersey) ? s.Accent : outer;
            m.Join(waist, chest, new Vector3(0f, 1.18f, hz * 0.5f));
            m.Paint = outer;
            m.Join(chest, shoulders, new Vector3(0f, 1.37f, hz * 0.8f));
            m.Fan(shoulders, neckBase, new Vector3(0f, 1.35f, hz));
            m.Pattern = null;

            // Zach's work shirt hangs in ragged tails below the belt.
            if (s.Has(Feature.TornShirt))
            {
                m.Paint = s.Shirt;
                int[] belt = m.Ring(new Vector3(0f, 0.93f, 0f), up, 10, 0.172f * s.Hips, 0.107f * s.Hips, 0.117f * s.Hips, 18f, W(Bone.Pelvis));
                var tails = new int[10];
                for (int i = 0; i < 10; i++)
                {
                    Vector3 p = m.P(belt[i]);
                    float drop = i % 2 == 0 ? 0.13f : 0.07f;
                    tails[i] = m.Add(new Vector3(p.x * 1.06f, 0.93f - drop, p.z * 1.06f), W(Bone.Pelvis));
                }
                m.Join(belt, tails, new Vector3(0f, 0.9f, 0f));
            }

            // ---- Neck (six sides, open: buried in the shoulders and the head).
            m.Paint = s.Skin;
            float ng = s.Neck;
            int[] neckLow = m.Ring(new Vector3(0f, 1.44f, -0.018f + hz), up, 6, 0.054f * ng, 0.054f * ng, 0.054f * ng, 0f, W(Bone.Chest, Bone.Neck));
            int[] neckHigh = m.Ring(new Vector3(0f, 1.61f, 0f), up, 6, 0.048f * ng, 0.048f * ng, 0.048f * ng, 0f, W(Bone.Neck, Bone.Head));
            m.Join(neckLow, neckHigh, new Vector3(0f, 1.52f, -0.01f));

            // ---- Head (eight sides): chin, jaw, cheekbones, brow, crown, apex; a nose and ears.
            float k = s.Head;
            Vector3 hc = new Vector3(0f, 1.69f, 0.005f);
            Vector3 HP(float y) => new Vector3(0f, hc.y + (y - hc.y) * k, 0f);
            int[] chin = m.Ring(HP(1.585f) + new Vector3(0f, 0f, 0.02f), up, 8, 0.042f * k, 0.058f * k, 0.035f * k, 0f, W(Bone.Head));
            int[] jaw = m.Ring(HP(1.63f) + new Vector3(0f, 0f, 0.01f), up, 8, 0.068f * k, 0.085f * k, 0.072f * k, 0f, W(Bone.Head));
            int[] cheek = m.Ring(HP(1.685f) + new Vector3(0f, 0f, 0.005f), up, 8, 0.079f * k, 0.094f * k, 0.09f * k, 0f, W(Bone.Head));
            int[] brow = m.Ring(HP(1.745f), up, 8, 0.077f * k, 0.094f * k, 0.09f * k, 0f, W(Bone.Head));
            int[] crown = m.Ring(HP(1.787f) + new Vector3(0f, 0f, -0.006f), up, 8, 0.064f * k, 0.066f * k, 0.078f * k, 0f, W(Bone.Head));
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

            // ---- Hair and hats.
            if (s.HairStyle == HairStyle.Short || s.HairStyle == HairStyle.Long || s.HairStyle == HairStyle.Curly || s.HairStyle == HairStyle.Cap)
            {
                m.Paint = s.HairStyle == HairStyle.Cap ? s.Hair : s.Hair;
                bool curly = s.HairStyle == HairStyle.Curly;
                // The hairline: low at the back (nape), up at the forehead; then the crown, a little proud of the scalp.
                var line = new int[8];
                var top = new int[8];
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    float x = Mathf.Sin(a), z = Mathf.Cos(a);
                    float bump = curly ? (i % 2 == 0 ? 1.14f : 1.0f) : 1.05f;
                    float y = Mathf.Lerp(1.66f, 1.765f, (z + 1f) * 0.5f);
                    if (s.HairStyle == HairStyle.Cap) y = 1.755f;
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
                    int l3 = line[3], l4 = line[4], l5 = line[5];
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
                    int b0 = line[3], b1 = line[5];
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

            // ---- The hockey mask: a curved white plate, red chevrons, dark eye holes and vents.
            if (s.Has(Feature.HockeyMask))
            {
                m.Paint = s.Accent;
                float[] ys = { 1.775f, 1.715f, 1.645f, 1.585f };
                float[] xs = { -0.068f, 0f, 0.068f };
                var grid = new int[4, 3];
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 3; c++)
                    {
                        float z = c == 1 ? 0.112f : 0.074f;
                        if (r == 3) z -= 0.012f;
                        float x = xs[c] * (r == 3 ? 0.75f : 1f);
                        grid[r, c] = m.Add(new Vector3(x * k, hc.y + (ys[r] - hc.y) * k, (z + (r == 0 ? -0.006f : 0f)) * k), W(Bone.Head));
                    }
                Vector3 behind = new Vector3(0f, hc.y, -0.05f);
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 2; c++)
                        m.Quad(grid[r, c], grid[r, c + 1], grid[r + 1, c + 1], grid[r + 1, c], behind);
                // Marks just proud of the mask, following its curve.
                Vector3 OnMask(float x, float y)
                {
                    float z = Mathf.Lerp(0.112f, 0.074f, Mathf.Abs(x) / 0.068f) + 0.004f;
                    return new Vector3(x * k, hc.y + (y - hc.y) * k, z * k);
                }
                void Chevron(float x, float y, float w, float h)
                {
                    // An upside-down V: two thin strokes meeting at the bottom.
                    int tl = m.Add(OnMask(x - w, y + h), W(Bone.Head));
                    int tl2 = m.Add(OnMask(x - w + 0.008f, y + h), W(Bone.Head));
                    int bot = m.Add(OnMask(x, y), W(Bone.Head));
                    int bot2 = m.Add(OnMask(x, y + 0.01f), W(Bone.Head));
                    int tr = m.Add(OnMask(x + w, y + h), W(Bone.Head));
                    int tr2 = m.Add(OnMask(x + w - 0.008f, y + h), W(Bone.Head));
                    m.Quad(tl, tl2, bot2, bot, behind);
                    m.Quad(bot, bot2, tr2, tr, behind);
                }
                m.Paint = s.Accent2;
                Chevron(-0.026f, 1.748f, 0.016f, 0.018f);
                Chevron(0.026f, 1.748f, 0.016f, 0.018f);
                Chevron(-0.047f, 1.655f, 0.012f, 0.014f);
                Chevron(0.047f, 1.655f, 0.012f, 0.014f);
                // Eye holes and breathing vents.
                m.Paint = new Color(0.04f, 0.04f, 0.04f);
                foreach (int side in new[] { -1, 1 })
                {
                    int a = m.Add(OnMask(side * 0.016f, 1.722f), W(Bone.Head));
                    int b = m.Add(OnMask(side * 0.046f, 1.722f), W(Bone.Head));
                    int c = m.Add(OnMask(side * 0.044f, 1.700f), W(Bone.Head));
                    int d = m.Add(OnMask(side * 0.018f, 1.702f), W(Bone.Head));
                    m.Quad(a, b, c, d, behind);
                }
                for (int i = 0; i < 4; i++)
                {
                    float x = -0.018f + i * 0.012f, y = 1.612f;
                    int a = m.Add(OnMask(x - 0.004f, y + 0.006f), W(Bone.Head));
                    int b = m.Add(OnMask(x + 0.004f, y + 0.006f), W(Bone.Head));
                    int c = m.Add(OnMask(x, y - 0.006f), W(Bone.Head));
                    m.Tri(a, b, c, behind);
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

                // ---- Arm: deltoid cap, shoulder, bicep, elbow, wrist (six sides); a mitten hand with a thumb.
                Vector3 sh = J(upper), el = J(fore), wr = J(hand);
                Vector3 shOut = sh + new Vector3(side * (0.225f * s.Shoulders - 0.215f), 0f, hz);
                var knuckle = wr + new Vector3(side * 0.004f, -0.095f, 0.004f);
                var tip = wr + new Vector3(side * 0.006f, -0.148f, 0.01f);
                m.Paint = s.Has(Feature.TornShirt) ? s.Shirt : outer;
                float cg = Mathf.Lerp(1f, ag, 0.55f);
                int[] cap = m.Ring(new Vector3(side * (0.195f + (0.225f * s.Shoulders - 0.225f)), 1.478f, -0.008f + hz), up, 6, 0.06f * cg, 0.06f * cg, 0.06f * cg, 0f, W(Bone.Chest, upper));
                int capTop = m.Add(new Vector3(side * (0.172f + (0.225f * s.Shoulders - 0.225f)), 1.50f, -0.01f + hz), W(Bone.Chest, upper, 0.3f));
                int[] a0 = m.Ring(shOut, el - sh, 6, 0.056f * ag, 0.054f * ag, 0.054f * ag, 30f, W(upper, Bone.Chest, 0.3f));
                m.Join(cap, a0, new Vector3(side * 0.19f, 1.45f, -0.005f + hz));
                m.Fan(cap, capTop, new Vector3(side * 0.19f, 1.45f, -0.005f + hz));
                Vector3 bicepAt = Vector3.Lerp(sh, el, 0.45f) + new Vector3(side * (0.225f * s.Shoulders - 0.225f) * 0.5f, 0f, hz * 0.5f);
                int[] a1 = m.Ring(bicepAt, el - sh, 6, 0.054f * ag, 0.056f * ag, 0.05f * ag, 30f, W(upper));
                int[] a2 = m.Ring(el, wr - sh, 6, 0.043f * ag, 0.044f * ag, 0.043f * ag, 30f, W(upper, fore));
                int[] a3 = m.Ring(wr, wr - el, 6, 0.031f * ag, 0.033f * ag, 0.033f * ag, 30f, W(fore, hand));
                m.Join(a0, a1, (sh + bicepAt) * 0.5f);
                m.Join(a1, a2, (bicepAt + el) * 0.5f);
                // Forearm: sleeve, or bare when the shirt has short sleeves (or Zach's are torn off), with hi-vis cuffs.
                m.Paint = s.Has(Feature.TornShirt) || !jacket ? s.Skin : s.Jacket;
                if (s.Has(Feature.HiVis)) m.Paint = s.Accent;
                m.Join(a2, a3, (el + wr) * 0.5f);
                m.Paint = hands;
                int[] a4 = m.Ring(knuckle, knuckle - wr, 6, 0.021f * ag, 0.05f * ag, 0.046f * ag, 30f, W(hand));
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

                // ---- Leg: hip, knee, calf, boot cuff, ankle (six sides); a wedge foot.
                Vector3 hip = J(thigh), knee = J(shin);
                float x = hip.x;
                var hipTop = new Vector3(x * Mathf.Max(1f, s.Hips * 0.95f), 0.97f, 0f);
                var calf = new Vector3(x, 0.33f, -0.012f);
                var cuff = new Vector3(x, 0.17f, -0.004f);
                var ankle = new Vector3(x, 0.075f, 0f);
                m.Paint = s.Pants;
                int[] l0 = m.Ring(hipTop, knee - hipTop, 6, 0.08f * lg, 0.08f * lg, 0.08f * lg, 30f, W(thigh));
                int[] l1 = m.Ring(knee, ankle - hipTop, 6, 0.054f * lg, 0.056f * lg, 0.05f * lg, 30f, W(thigh, shin));
                int[] l2 = m.Ring(calf, ankle - knee, 6, 0.052f * lg, 0.046f * lg, 0.058f * lg, 30f, W(shin));
                int[] l3 = m.Ring(cuff, ankle - knee, 6, 0.046f * lg, 0.046f * lg, 0.046f * lg, 30f, W(shin));
                int[] l4 = m.Ring(ankle, ankle - knee, 6, 0.04f * lg, 0.043f * lg, 0.043f * lg, 30f, W(shin, foot));
                m.Join(l0, l1, (hipTop + knee) * 0.5f);
                m.Join(l1, l2, (knee + calf) * 0.5f);
                m.Join(l2, l3, (calf + cuff) * 0.5f);
                m.Paint = s.Shoes;
                m.Join(l3, l4, (cuff + ankle) * 0.5f);

                float fw = Mathf.Lerp(1f, lg, 0.5f);
                int heelL = m.Add(new Vector3(x - 0.044f * fw, 0f, -0.078f), W(foot));
                int heelR = m.Add(new Vector3(x + 0.044f * fw, 0f, -0.078f), W(foot));
                int toeL = m.Add(new Vector3(x - 0.054f * fw, 0f, 0.21f), W(toe));
                int toeR = m.Add(new Vector3(x + 0.054f * fw, 0f, 0.21f), W(toe));
                int topL = m.Add(new Vector3(x - 0.04f * fw, 0.12f, -0.04f), W(foot));
                int topR = m.Add(new Vector3(x + 0.04f * fw, 0.12f, -0.04f), W(foot));
                var footInside = new Vector3(x, 0.04f, 0.02f);
                m.Paint = Darker(s.Shoes, 0.6f);
                m.Quad(heelL, heelR, toeR, toeL, footInside);
                m.Paint = s.Shoes;
                m.Quad(toeL, toeR, topR, topL, footInside);
                m.Quad(topL, topR, heelR, heelL, footInside);
                m.Tri(heelL, toeL, topL, footInside);
                m.Tri(heelR, toeR, topR, footInside);
            }
            return m;
        }

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
