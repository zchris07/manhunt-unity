using System.Collections.Generic;
using UnityEngine;

namespace Vision.Characters
{
    public enum PropKind { None, Machete, Shotgun, GoldenPump, Pistol, Sniper, Flashlight, Magnifier, Tablet, Controller, Bottle, Book, Jar, Can, Bar, Flask, Tank, Goggles }

    /// <summary>
    /// Held props: separate flat-shaded, vertex-coloured meshes of at most <see cref="MaxTriangles"/> triangles, built
    /// in the grip's frame (the hand closes round the origin; the prop points along +Z, its top is +Y), so a socket on the
    /// hand bone holds them. They follow the original's item art, but the shotgun is a pump-action (a single barrel over a
    /// magazine tube, a wooden stock, the wooden forend its own piece that slides as the pump is worked), the mini shield as a small round flask of glowing blue liquid with a stopper (Fortnite's small shield
    /// potion), galaxy gas as a stout whippit tank in a galaxy wrap with a silver shoulder and a valve.
    /// </summary>
    public static class PropModels
    {
        public const int MaxTriangles = 130;

        sealed class B
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Color> C = new List<Color>();

            void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
            {
                V.Add(a); V.Add(b); V.Add(c);
                Color l = col.linear;
                C.Add(l); C.Add(l); C.Add(l);
            }

            /// <summary>A box (12 triangles) at a centre, with half-sizes, turned by a rotation.</summary>
            public void Box(Vector3 centre, Vector3 half, Color col, Quaternion? rot = null)
            {
                Quaternion r = rot ?? Quaternion.identity;
                Vector3 P(float x, float y, float z) => centre + r * new Vector3(x * half.x, y * half.y, z * half.z);
                Vector3[] p = { P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1) };
                int[,] f = { { 0, 3, 2, 1 }, { 4, 5, 6, 7 }, { 0, 4, 7, 3 }, { 1, 2, 6, 5 }, { 3, 7, 6, 2 }, { 0, 1, 5, 4 } };
                for (int i = 0; i < 6; i++)
                {
                    Tri(p[f[i, 0]], p[f[i, 1]], p[f[i, 2]], col);
                    Tri(p[f[i, 0]], p[f[i, 2]], p[f[i, 3]], col);
                }
            }

            /// <summary>A cylinder along +Z from z0 to z1 (sides, plus caps).</summary>
            public void Tube(Vector3 axisPoint, float z0, float z1, float r0, float r1, int sides, Color col, bool caps = true, Color? capColor = null)
            {
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides, b = (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 d0 = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f), d1 = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f);
                    Vector3 p00 = axisPoint + d0 * r0 + Vector3.forward * z0, p01 = axisPoint + d1 * r0 + Vector3.forward * z0;
                    Vector3 p10 = axisPoint + d0 * r1 + Vector3.forward * z1, p11 = axisPoint + d1 * r1 + Vector3.forward * z1;
                    Tri(p00, p10, p11, col);
                    Tri(p00, p11, p01, col);
                    if (!caps) continue;
                    Tri(axisPoint + Vector3.forward * z0, p01, p00, capColor ?? col);
                    Tri(axisPoint + Vector3.forward * z1, p10, p11, capColor ?? col);
                }
            }

            /// <summary>A flat blade (both sides) tapering from a base width to a tip, with a bevelled edge below.</summary>
            public void Blade(Vector3 basePt, float length, float width, float thick, Color col, Color edge)
            {
                Vector3 bt = basePt + new Vector3(0f, width * 0.5f, 0f), bb = basePt - new Vector3(0f, width * 0.5f, 0f);
                Vector3 tipTop = basePt + new Vector3(0f, width * 0.35f, length), tip = basePt + new Vector3(0f, -width * 0.1f, length * 1.04f);
                Vector3 t = new Vector3(thick, 0f, 0f);
                // Left and right faces.
                Tri(bt - t, tipTop - t, bb - t, col);
                Tri(bb - t, tipTop - t, tip - t, col);
                Tri(bt + t, bb + t, tipTop + t, col);
                Tri(bb + t, tip + t, tipTop + t, col);
                // Spine and edge.
                Tri(bt - t, bt + t, tipTop + t, col);
                Tri(bt - t, tipTop + t, tipTop - t, col);
                Tri(bb - t, tip, bb + t, edge);
                Tri(tipTop - t, tipTop + t, tip, edge);
            }

            /// <summary>
            /// A traditional machete's blade along +Z from <paramref name="z0"/>: at each station (t 0-1 along it) the spine's
            /// and the edge's heights; the spine is thick, the faces taper to a sharp edge, and the tip clips down to a point.
            /// Each facet (two rows a side, both sides) is steel, or blood where <paramref name="blood"/> says so.
            /// </summary>
            public void Machete(float z0, float length, (float t, float spine, float edge)[] st, float thick, Color steel, Color edgeCol, System.Func<int, int, Color?> blood)
            {
                int n = st.Length;
                var top = new Vector3[n]; var mid = new Vector3[n]; var bot = new Vector3[n];
                var midT = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float z = z0 + st[i].t * length;
                    top[i] = new Vector3(0f, st[i].spine, z);
                    bot[i] = new Vector3(0f, st[i].edge, z);
                    mid[i] = new Vector3(0f, Mathf.Lerp(st[i].spine, st[i].edge, 0.45f), z);
                    // Thick at the spine, thinning toward the tip.
                    midT[i] = thick * Mathf.Lerp(1f, 0.35f, st[i].t);
                }
                for (int i = 0; i + 1 < n; i++)
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector3 s0 = new Vector3(side * midT[i], 0f, 0f), s1 = new Vector3(side * midT[i + 1], 0f, 0f);
                        Vector3 m0 = new Vector3(side * midT[i] * 0.7f, 0f, 0f), m1 = new Vector3(side * midT[i + 1] * 0.7f, 0f, 0f);
                        // The upper row (spine to the middle) and the lower, bevelled down to the edge.
                        Color up = blood(i, side < 0f ? 0 : 2) ?? steel, low = blood(i, side < 0f ? 1 : 3) ?? edgeCol;
                        Quad(top[i] + s0, top[i + 1] + s1, mid[i + 1] + m1, mid[i] + m0, up, side);
                        Quad(mid[i] + m0, mid[i + 1] + m1, bot[i + 1], bot[i], low, side);
                    }
                // The spine: a narrow flat along the top.
                for (int i = 0; i + 1 < n; i++)
                {
                    Vector3 l0 = top[i] - new Vector3(midT[i], 0f, 0f), r0 = top[i] + new Vector3(midT[i], 0f, 0f);
                    Vector3 l1 = top[i + 1] - new Vector3(midT[i + 1], 0f, 0f), r1 = top[i + 1] + new Vector3(midT[i + 1], 0f, 0f);
                    Tri(l0, r1, r0, steel * 0.85f);
                    Tri(l0, l1, r1, steel * 0.85f);
                }
            }

            /// <summary>A four-cornered face, wound so it faces <paramref name="side"/> (-1 left, +1 right along X).</summary>
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, float side)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.x * side < 0f) { Tri(a, c, b, col); Tri(a, d, c, col); }
                else { Tri(a, b, c, col); Tri(a, c, d, col); }
            }

            /// <summary>
            /// A solid of revolution about the Z axis through an axis point: rings at (z, radius), each band between two
            /// rings in its own colour, closed at both ends.
            /// </summary>
            public void Lathe(Vector3 axisPoint, (float z, float r)[] profile, Color[] bands, int sides, float phase = 0f)
            {
                for (int k = 0; k + 1 < profile.Length; k++)
                {
                    var (z0, r0) = profile[k];
                    var (z1, r1) = profile[k + 1];
                    Color col = bands[Mathf.Min(k, bands.Length - 1)];
                    for (int i = 0; i < sides; i++)
                    {
                        float a = phase + i * Mathf.PI * 2f / sides, b = phase + (i + 1) * Mathf.PI * 2f / sides;
                        Vector3 d0 = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f), d1 = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f);
                        Vector3 p00 = axisPoint + d0 * r0 + Vector3.forward * z0, p01 = axisPoint + d1 * r0 + Vector3.forward * z0;
                        Vector3 p10 = axisPoint + d0 * r1 + Vector3.forward * z1, p11 = axisPoint + d1 * r1 + Vector3.forward * z1;
                        if (r0 > 1e-5f) Tri(p00, p10, p11, col);
                        if (r1 > 1e-5f || r0 > 1e-5f) Tri(p00, p11, p01, col);
                        if (k == 0 && r0 > 1e-5f) Tri(axisPoint + Vector3.forward * z0, p01, p00, col);
                        if (k + 2 == profile.Length && r1 > 1e-5f) Tri(axisPoint + Vector3.forward * z1, p10, p11, col);
                    }
                }
            }

            public Mesh Mesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(V);
                m.SetColors(C);
                var idx = new int[V.Count];
                for (int i = 0; i < idx.Length; i++) idx[i] = i;
                m.SetTriangles(idx, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        static Color H(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        static readonly Dictionary<PropKind, Mesh> cache = new Dictionary<PropKind, Mesh>();
        static readonly Dictionary<PropKind, Mesh> forends = new Dictionary<PropKind, Mesh>();

        /// <summary>
        /// A pump gun's forend, its own piece in the gun's frame, round the magazine tube where the support hand holds it
        /// (it slides back toward the receiver as the pump is worked); null for every other prop.
        /// </summary>
        public static Mesh Forend(PropKind kind)
        {
            if (kind != PropKind.Shotgun && kind != PropKind.GoldenPump) return null;
            if (forends.TryGetValue(kind, out Mesh m) && m != null) return m;
            var b = new B();
            Color body = kind == PropKind.GoldenPump ? H(0xb8862a) : H(0x7a4520), groove = kind == PropKind.GoldenPump ? H(0x8a6418) : H(0x4e2c14);
            b.Box(new Vector3(0f, 0.002f, 0.215f), new Vector3(0.022f, 0.017f, 0.07f), body);
            foreach (float z in new[] { 0.19f, 0.24f })
                b.Box(new Vector3(0f, -0.016f, z), new Vector3(0.0225f, 0.0015f, 0.006f), groove);
            m = b.Mesh(kind + " Forend");
            m.hideFlags = HideFlags.DontSave;
            forends[kind] = m;
            return m;
        }

        public static Mesh Get(PropKind kind)
        {
            if (kind == PropKind.None) return null;
            if (cache.TryGetValue(kind, out Mesh m) && m != null) return m;
            m = Build(kind);
            m.hideFlags = HideFlags.DontSave;
            cache[kind] = m;
            return m;
        }

        public static Mesh Build(PropKind kind)
        {
            var b = new B();
            Color metal = H(0x8a8e92), dark = H(0x24262a), wood = H(0x6a4424), black = H(0x141416);
            switch (kind)
            {
                case PropKind.Machete:
                {
                    // Jason's: a long traditional machete. A wrapped grip with a steel pommel, a flat oval guard, then a blade
                    // over 0.65 m long widening toward its belly, the spine straight until it clips down to the point, a
                    // bevelled edge, and blood smeared and spattered over the middle and lower blade.
                    Color wrap = H(0x3a2c22), wrap2 = H(0x5a4636), steel = H(0xa7aaa8), edgeCol = H(0xcfd2cf);
                    Color blood = H(0x6e0e10), bloodDark = H(0x4a0709);
                    b.Lathe(Vector3.zero, new[] { (-0.085f, 0.012f), (-0.078f, 0.021f), (-0.03f, 0.019f), (0.03f, 0.02f), (0.09f, 0.02f) },
                        new[] { metal, wrap, wrap2, wrap }, 5);
                    b.Box(new Vector3(0f, -0.004f, 0.097f), new Vector3(0.017f, 0.048f, 0.007f), H(0x9a9c98));
                    (float, float, float)[] st =
                    {
                        (0f, 0.022f, -0.026f), (0.18f, 0.024f, -0.034f), (0.38f, 0.025f, -0.043f), (0.58f, 0.025f, -0.052f),
                        (0.78f, 0.022f, -0.057f), (0.92f, 0.008f, -0.048f), (1f, -0.016f, -0.022f),
                    };
                    // Blood on the faces by (station, row): heavy over the middle, spattered elsewhere.
                    Color? Blood(int i, int row)
                    {
                        float h = Mathf.Repeat(Mathf.Sin(i * 12.9898f + row * 78.233f) * 43758.5453f, 1f);
                        bool heavy = i >= 1 && i <= 4 && (row == 1 || row == 3 || h < 0.6f);
                        if (heavy) return h < 0.5f ? bloodDark : blood;
                        return h < 0.3f ? blood : (Color?)null;
                    }
                    b.Machete(0.1f, 0.66f, st, 0.0045f, steel, edgeCol, Blood);
                    break;
                }
                case PropKind.Shotgun:
                    // A pump-action: a wooden stock, the steel receiver with its ejection port on the right, a single barrel
                    // over the magazine tube, the trigger guard; the forend is its own piece (Forend).
                    b.Box(new Vector3(0f, -0.022f, -0.17f), new Vector3(0.018f, 0.04f, 0.13f), wood, Quaternion.Euler(8f, 0f, 0f));
                    b.Box(new Vector3(0f, 0.014f, 0.03f), new Vector3(0.021f, 0.03f, 0.08f), H(0x3a3c40));
                    b.Box(new Vector3(0.0215f, 0.022f, 0.05f), new Vector3(0.002f, 0.009f, 0.022f), black);
                    b.Tube(new Vector3(0f, 0.03f, 0f), 0.1f, 0.62f, 0.0115f, 0.011f, 6, H(0x5a6270), true, black);
                    b.Tube(new Vector3(0f, 0.004f, 0f), 0.1f, 0.5f, 0.0095f, 0.0095f, 6, H(0x44484e), true, H(0x2a2c30));
                    b.Box(new Vector3(0f, -0.03f, 0.0f), new Vector3(0.008f, 0.02f, 0.03f), black);
                    break;
                case PropKind.GoldenPump:
                {
                    bool gold = true;
                    Color body = gold ? H(0xd8a830) : dark, stock = gold ? H(0xb8862a) : wood;
                    b.Box(new Vector3(0f, -0.02f, -0.16f), new Vector3(0.018f, 0.04f, 0.13f), stock, Quaternion.Euler(8f, 0f, 0f));
                    b.Box(new Vector3(0f, 0.01f, 0.05f), new Vector3(0.02f, 0.03f, 0.1f), body);
                    b.Tube(new Vector3(0f, 0.025f, 0f), 0.14f, 0.62f, 0.012f, 0.012f, 6, gold ? H(0xe8c050) : metal, true, black);
                    b.Tube(new Vector3(0f, 0.0f, 0f), 0.14f, 0.5f, 0.0095f, 0.0095f, 6, H(0xb8902c), true, H(0x8a6a20));
                    b.Box(new Vector3(0f, -0.04f, -0.01f), new Vector3(0.012f, 0.03f, 0.016f), body, Quaternion.Euler(-15f, 0f, 0f));
                    break;
                }
                case PropKind.Pistol:
                    b.Box(new Vector3(0f, -0.04f, -0.005f), new Vector3(0.014f, 0.05f, 0.02f), black, Quaternion.Euler(-12f, 0f, 0f));
                    b.Box(new Vector3(0f, 0.018f, 0.06f), new Vector3(0.015f, 0.018f, 0.1f), dark);
                    b.Box(new Vector3(0f, -0.012f, 0.03f), new Vector3(0.006f, 0.012f, 0.03f), black);
                    break;
                case PropKind.Sniper:
                    b.Box(new Vector3(0f, -0.02f, -0.2f), new Vector3(0.02f, 0.045f, 0.16f), H(0x3a3e30), Quaternion.Euler(6f, 0f, 0f));
                    b.Box(new Vector3(0f, 0.01f, 0.08f), new Vector3(0.022f, 0.032f, 0.16f), H(0x3a3e30));
                    b.Tube(new Vector3(0f, 0.02f, 0f), 0.22f, 0.95f, 0.014f, 0.011f, 6, dark, true, black);
                    b.Tube(new Vector3(0f, 0.075f, 0f), 0.0f, 0.24f, 0.022f, 0.022f, 6, black, true, H(0x40a0d0));
                    b.Box(new Vector3(0f, 0.05f, 0.12f), new Vector3(0.006f, 0.016f, 0.012f), black);
                    b.Box(new Vector3(0f, -0.05f, -0.01f), new Vector3(0.012f, 0.035f, 0.02f), dark, Quaternion.Euler(-15f, 0f, 0f));
                    break;
                case PropKind.Flashlight:
                    b.Tube(new Vector3(0f, 0f, 0f), -0.06f, 0.08f, 0.017f, 0.017f, 6, black);
                    b.Tube(new Vector3(0f, 0f, 0f), 0.08f, 0.13f, 0.017f, 0.028f, 6, dark, false);
                    b.Tube(new Vector3(0f, 0f, 0f), 0.13f, 0.135f, 0.028f, 0.028f, 6, dark, true, H(0xfff2c0));
                    break;
                case PropKind.Magnifier:
                {
                    b.Box(new Vector3(0f, 0f, 0.03f), new Vector3(0.012f, 0.012f, 0.06f), wood);
                    // The rim (six segments) and the glass.
                    Vector3 c = new Vector3(0f, 0f, 0.15f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = (i + 0.5f) * Mathf.PI / 3f;
                        b.Box(c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.052f, new Vector3(0.004f, 0.03f, 0.008f), metal, Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg));
                    }
                    b.Tube(c - new Vector3(0f, 0f, 0f), -0.002f, 0.002f, 0.052f, 0.052f, 8, H(0xa8d8e8), true);
                    break;
                }
                case PropKind.Tablet:
                    b.Box(new Vector3(0f, 0.08f, 0.04f), new Vector3(0.006f, 0.1f, 0.07f), black, Quaternion.Euler(0f, 0f, 0f));
                    b.Box(new Vector3(0.0062f, 0.08f, 0.04f), new Vector3(0.0005f, 0.088f, 0.06f), H(0x3ac0ff));
                    break;
                case PropKind.Controller:
                    b.Box(new Vector3(0f, 0.02f, 0.04f), new Vector3(0.07f, 0.016f, 0.035f), H(0x2a2a30));
                    b.Box(new Vector3(-0.055f, 0.012f, 0.0f), new Vector3(0.022f, 0.014f, 0.04f), H(0x2a2a30), Quaternion.Euler(0f, -15f, 0f));
                    b.Box(new Vector3(0.055f, 0.012f, 0.0f), new Vector3(0.022f, 0.014f, 0.04f), H(0x2a2a30), Quaternion.Euler(0f, 15f, 0f));
                    b.Box(new Vector3(0.03f, 0.038f, 0.05f), new Vector3(0.012f, 0.003f, 0.012f), H(0x30d070));
                    break;
                case PropKind.Bottle:
                    b.Tube(Vector3.zero, -0.04f, 0.12f, 0.03f, 0.03f, 6, H(0x2a7a3a), true);
                    b.Tube(Vector3.zero, 0.12f, 0.2f, 0.03f, 0.012f, 6, H(0x2a7a3a), true);
                    break;
                case PropKind.Book:
                    b.Box(new Vector3(0f, 0f, 0.05f), new Vector3(0.025f, 0.09f, 0.065f), H(0x7a2a24));
                    b.Box(new Vector3(0.002f, 0f, 0.05f), new Vector3(0.024f, 0.085f, 0.062f), H(0xe8e0c8));
                    break;
                case PropKind.Jar:
                    b.Tube(Vector3.zero, -0.04f, 0.1f, 0.04f, 0.04f, 6, H(0xd8c840), true);
                    b.Tube(Vector3.zero, 0.1f, 0.12f, 0.035f, 0.035f, 6, metal, true);
                    break;
                case PropKind.Can:
                    b.Tube(Vector3.zero, -0.03f, 0.09f, 0.03f, 0.03f, 6, H(0x7a1a24), true, metal);
                    break;
                case PropKind.Bar:
                    b.Box(new Vector3(0f, 0f, 0.04f), new Vector3(0.014f, 0.03f, 0.07f), H(0x6a3a1e));
                    break;
                case PropKind.Flask:
                {
                    // The mini shield: a round glass flask, the bottom two thirds glowing blue liquid, a slim neck, a cork.
                    Color fluid = H(0x2e9cff), fluidTop = H(0x7fd0ff), glass = H(0xbfe4ff), cork = H(0x9a6a3a);
                    b.Lathe(new Vector3(0f, 0f, 0f), new[] { (-0.035f, 0.022f), (-0.022f, 0.042f), (0.0f, 0.05f), (0.022f, 0.044f), (0.036f, 0.026f), (0.046f, 0.012f), (0.07f, 0.012f), (0.072f, 0.015f), (0.09f, 0.014f) },
                        new[] { fluid, fluid, fluidTop, glass, glass, glass, cork, cork }, 7);
                    break;
                }
                case PropKind.Tank:
                {
                    // A Galaxy Gas canister in hand: a white base ring, the label in watermelon pink over lemonade yellow
                    // with a magenta band, a clear white domed shoulder and a white spout tapering to its tip.
                    // (Seven sides, to keep to the props' triangle budget.)
                    Color white = H(0xeeeeec), yellow = H(0xffd052), pink = H(0xff9aae), magenta = H(0xc8337a), dome = H(0xe0e6ea);
                    b.Lathe(Vector3.zero, new[] { (-0.08f, 0.032f), (-0.07f, 0.032f), (-0.012f, 0.03f), (0.035f, 0.03f), (0.045f, 0.031f), (0.07f, 0.03f), (0.09f, 0.02f), (0.1f, 0.009f), (0.135f, 0.002f) },
                        new[] { white, yellow, pink, magenta, pink, dome, dome, white }, 7, Mathf.PI / 7f);
                    break;
                }
                case PropKind.Goggles:
                    // Night vision goggles: two tubes with green lenses on a dark frame, worn over the eyes (+Z out).
                    b.Box(new Vector3(0f, 0f, -0.005f), new Vector3(0.075f, 0.022f, 0.016f), H(0x2a2e36));
                    b.Tube(new Vector3(-0.033f, 0f, 0f), 0.0f, 0.05f, 0.021f, 0.018f, 6, H(0x1a1a20), true, H(0x5cff6a));
                    b.Tube(new Vector3(0.033f, 0f, 0f), 0.0f, 0.05f, 0.021f, 0.018f, 6, H(0x1a1a20), true, H(0x5cff6a));
                    b.Box(new Vector3(0f, 0.004f, -0.06f), new Vector3(0.088f, 0.01f, 0.05f), H(0x141416));
                    break;
            }
            return b.Mesh(kind.ToString());
        }

        /// <summary>The end of a gun's barrel in the prop's own frame (where the round leaves it), or null for anything else.</summary>
        public static Vector3? Muzzle(PropKind kind) => kind switch
        {
            PropKind.Shotgun => new Vector3(0f, 0.028f, 0.62f),
            PropKind.GoldenPump => new Vector3(0f, 0.025f, 0.62f),
            PropKind.Pistol => new Vector3(0f, 0.018f, 0.16f),
            PropKind.Sniper => new Vector3(0f, 0.02f, 0.95f),
            _ => (Vector3?)null,
        };

        /// <summary>The prop a survivor holds for an item.</summary>
        public static PropKind ForItem(Vision.Player.ItemType item, bool golden) => item switch
        {
            Vision.Player.ItemType.Shotgun => golden ? PropKind.GoldenPump : PropKind.Shotgun,
            Vision.Player.ItemType.Pistol => PropKind.Pistol,
            Vision.Player.ItemType.Sniper => PropKind.Sniper,
            Vision.Player.ItemType.Bottle => PropKind.Bottle,
            Vision.Player.ItemType.Book => PropKind.Book,
            Vision.Player.ItemType.Piss => PropKind.Jar,
            Vision.Player.ItemType.DoctorPepper => PropKind.Can,
            Vision.Player.ItemType.MrBeastBar => PropKind.Bar,
            Vision.Player.ItemType.MiniShield => PropKind.Flask,
            Vision.Player.ItemType.Trap => PropKind.Tank,
            Vision.Player.ItemType.Goggles => PropKind.Goggles,
            _ => PropKind.None,
        };
    }
}
