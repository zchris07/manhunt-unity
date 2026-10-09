using System.Collections.Generic;
using UnityEngine;

namespace Vision.Characters
{
    public enum PropKind { None, Machete, Shotgun, GoldenPump, Pistol, Sniper, Flashlight, Magnifier, Tablet, Controller, Bottle, Book, Jar, Can, Bar }

    /// <summary>
    /// Held props: separate flat-shaded, vertex-coloured meshes of at most <see cref="MaxTriangles"/> triangles, built
    /// in the grip's frame (the hand closes round the origin; the prop points along +Z, its top is +Y), so a socket on the
    /// hand bone holds them.
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
                    // A worn grip, a short guard, a long broad blade.
                    b.Box(new Vector3(0f, 0f, 0.02f), new Vector3(0.014f, 0.018f, 0.07f), H(0x3a2a1e));
                    b.Box(new Vector3(0f, 0f, 0.095f), new Vector3(0.018f, 0.028f, 0.006f), dark);
                    b.Blade(new Vector3(0f, -0.006f, 0.1f), 0.46f, 0.07f, 0.004f, H(0x9a9c98), H(0xd8dad6));
                    break;
                case PropKind.Shotgun:
                case PropKind.GoldenPump:
                {
                    bool gold = kind == PropKind.GoldenPump;
                    Color body = gold ? H(0xd8a830) : dark, stock = gold ? H(0xb8862a) : wood;
                    b.Box(new Vector3(0f, -0.02f, -0.16f), new Vector3(0.018f, 0.04f, 0.13f), stock, Quaternion.Euler(8f, 0f, 0f));
                    b.Box(new Vector3(0f, 0.01f, 0.05f), new Vector3(0.02f, 0.03f, 0.1f), body);
                    b.Tube(new Vector3(0f, 0.025f, 0f), 0.14f, 0.62f, 0.012f, 0.012f, 6, gold ? H(0xe8c050) : metal, true, black);
                    b.Box(new Vector3(0f, -0.004f, 0.3f), new Vector3(0.02f, 0.018f, 0.07f), stock);
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
            }
            return b.Mesh(kind.ToString());
        }

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
            _ => PropKind.None,
        };
    }
}
