using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>Outdoor set pieces: fences, logs, the lake's dock and water, power lines, graveyard, playground, hanging tree.</summary>
    public static partial class LowPolyModels
    {
        public static class Site
        {
            public static readonly Color Weathered = new Color(0.40f, 0.35f, 0.29f);
            public static readonly Color WeatheredDark = new Color(0.27f, 0.23f, 0.19f);
            public static readonly Color Granite = new Color(0.50f, 0.50f, 0.49f);
            public static readonly Color GraniteDark = new Color(0.36f, 0.36f, 0.35f);
            public static readonly Color Moss = new Color(0.30f, 0.36f, 0.24f);
            public static readonly Color Iron = new Color(0.13f, 0.12f, 0.12f);
            public static readonly Color Grave = new Color(0.24f, 0.20f, 0.16f);
            public static readonly Color Rope = new Color(0.46f, 0.40f, 0.29f);
            public static readonly Color FadedRed = new Color(0.50f, 0.20f, 0.16f);
            public static readonly Color FadedBlue = new Color(0.24f, 0.32f, 0.42f);
            public static readonly Color FadedYellow = new Color(0.62f, 0.52f, 0.22f);
            public static readonly Color Water = new Color(0.07f, 0.10f, 0.12f);
            public static readonly Color Pole = new Color(0.30f, 0.24f, 0.18f);
            public static readonly Color Insulator = new Color(0.45f, 0.55f, 0.55f);
        }

        // ------------------------------------------------------------------ fences and logs

        /// <summary>A split-rail fence along +X from 0 to <paramref name="length"/>, posts every ~2 m, leaning a little.</summary>
        public static Mesh Fence(System.Random rng, float length)
        {
            var b = new LowPolyMeshBuilder(rng);
            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 2f) + 1);
            var tops = new Vector3[posts];
            for (int i = 0; i < posts; i++)
            {
                float x = length * i / (posts - 1);
                var lean = new Vector3(b.Range(-0.06f, 0.06f), 0f, b.Range(-0.08f, 0.08f));
                tops[i] = new Vector3(x, 1.15f, 0f) + lean;
                b.AddFrustum(new Vector3(x, -0.1f, 0f), tops[i], 0.07f, 0.06f, 4, b.Jitter(Site.Weathered, 0.1f), 0.08f);
            }
            for (int i = 0; i < posts - 1; i++)
            {
                if (b.Next() < 0.12f) continue;   // a missing rail
                foreach (float y in new[] { 0.45f, 0.9f })
                {
                    Vector3 a = Vector3.Lerp(new Vector3(tops[i].x, 0f, tops[i].z), tops[i], y / 1.15f);
                    Vector3 c = Vector3.Lerp(new Vector3(tops[i + 1].x, 0f, tops[i + 1].z), tops[i + 1], y / 1.15f) + Vector3.down * b.Range(0f, 0.12f);
                    b.AddFrustum(a, c, 0.045f, 0.045f, 4, b.Jitter(Site.WeatheredDark, 0.1f), 0.08f);
                }
            }
            return b.ToMesh("Fence");
        }

        /// <summary>A fallen log lying along +X, centred on the origin, with a broken stub and moss on top.</summary>
        public static Mesh Log(System.Random rng, float length)
        {
            var b = new LowPolyMeshBuilder(rng);
            float r = 0.3f;
            b.AddTube(new[] { new Vector3(-length * 0.5f, r, 0f), new Vector3(0f, r * 0.95f, 0.05f), new Vector3(length * 0.5f, r * 0.85f, -0.03f) },
                      new[] { r, r * 0.92f, r * 0.8f }, 7, Palette.Bark, 0.1f, 0.08f);
            b.AddFrustum(new Vector3(length * 0.15f, r * 1.6f, 0f), new Vector3(length * 0.2f, r * 2.6f, 0.1f), 0.07f, 0f, 4, Palette.BarkDark, 0.1f, 0f, false, false);
            b.AddBlob(new Vector3(-length * 0.2f, r * 1.75f, 0f), new Vector3(length * 0.18f, 0.06f, r * 0.6f), 0, 0.2f, _ => b.Jitter(Site.Moss, 0.15f), true);
            return b.ToMesh("Log");
        }

        // ------------------------------------------------------------------ the lake

        /// <summary>A flat dark water surface: a fan from the centre to the shore points (local to the lake centre).</summary>
        public static Mesh Water(IReadOnlyList<Vector2> shore)
        {
            var b = new LowPolyMeshBuilder();
            for (int i = 0; i < shore.Count; i++)
            {
                Vector2 a = shore[i], c = shore[(i + 1) % shore.Count];
                b.AddTriangleOutward(Vector3.zero, new Vector3(a.x, 0f, a.y), new Vector3(c.x, 0f, c.y), Site.Water, Vector3.down);
            }
            return b.ToMesh("Lake Water");
        }

        /// <summary>A plank dock along +Z from 0 to <paramref name="length"/>, on posts, deck at <paramref name="deck"/> above the origin.</summary>
        public static Mesh Dock(System.Random rng, float length, float halfWidth, float deck)
        {
            var b = new LowPolyMeshBuilder(rng);
            int planks = Mathf.RoundToInt(length / 0.32f);
            for (int i = 0; i < planks; i++)
            {
                if (i > planks - 4 && b.Next() < 0.35f) continue;   // missing boards near the end
                float z = i * length / planks;
                b.AddBox(new Vector3(-halfWidth, deck - 0.05f + b.Range(-0.02f, 0.02f), z + 0.02f), new Vector3(halfWidth * 2f, 0.06f, length / planks - 0.04f), Site.Weathered, 0.12f);
            }
            for (float z = 0.4f; z < length; z += 2f)
                foreach (float x in new[] { -halfWidth + 0.08f, halfWidth - 0.08f })
                    b.AddFrustum(new Vector3(x, -1.2f, z), new Vector3(x, deck + 0.25f, z), 0.08f, 0.08f, 5, Site.WeatheredDark, 0.1f);
            return b.ToMesh("Dock");
        }

        // ------------------------------------------------------------------ power lines

        /// <summary>A wooden utility pole with a crossarm (along X), three insulators and a transformer can on some.</summary>
        public static Mesh PowerPole(System.Random rng, bool transformer)
        {
            var b = new LowPolyMeshBuilder(rng);
            float h = PowerPoleHeight;
            var lean = new Vector3(b.Range(-0.12f, 0.12f), 0f, b.Range(-0.12f, 0.12f));
            b.AddFrustum(Vector3.zero, new Vector3(0f, h, 0f) + lean, 0.16f, 0.11f, 6, b.Jitter(Site.Pole, 0.08f), 0.08f);
            Vector3 arm = new Vector3(0f, h - 0.5f, 0f) + lean * (h - 0.5f) / h;
            b.AddBox(arm + new Vector3(-1.2f, -0.06f, -0.07f), new Vector3(2.4f, 0.12f, 0.14f), Site.WeatheredDark, 0.08f);
            foreach (float x in PowerWireOffsets)
                b.AddFrustum(arm + new Vector3(x, 0.06f, 0f), arm + new Vector3(x, 0.26f, 0f), 0.05f, 0.04f, 5, Site.Insulator, 0.08f);
            // Diagonal braces under the arm.
            foreach (float s in new[] { -1f, 1f })
                b.AddFrustum(arm + new Vector3(s * 0.7f, -0.05f, 0f), new Vector3(0f, h - 1.2f, 0f) + lean, 0.03f, 0.03f, 4, Site.Iron, 0.05f);
            if (transformer)
            {
                b.AddFrustum(new Vector3(0.28f, h - 2.2f, 0f), new Vector3(0.28f, h - 1.5f, 0f), 0.22f, 0.22f, 7, new Color(0.40f, 0.41f, 0.40f), 0.06f);
                b.AddFrustum(new Vector3(0.28f, h - 1.5f, 0f), new Vector3(0.28f, h - 1.45f, 0f), 0.24f, 0.18f, 7, Site.Iron, 0.05f);
            }
            return b.ToMesh("Power Pole");
        }

        public const float PowerPoleHeight = 8.5f;
        public static readonly float[] PowerWireOffsets = { -1.05f, 0f, 1.05f };

        /// <summary>
        /// Sagging wires between consecutive poles (world-local positions of each pole base and its crossarm yaw),
        /// each a thin faceted catenary; one mesh for the whole line.
        /// </summary>
        public static Mesh PowerWires(IReadOnlyList<Vector3> bases, IReadOnlyList<float> yaws)
        {
            var b = new LowPolyMeshBuilder();
            float top = PowerPoleHeight - 0.5f + 0.26f;
            for (int i = 0; i < bases.Count - 1; i++)
            {
                float span = Vector3.Distance(bases[i], bases[i + 1]);
                float sag = 0.9f + span * 0.035f;
                foreach (float x in PowerWireOffsets)
                {
                    Vector3 a = bases[i] + Quaternion.Euler(0f, yaws[i], 0f) * new Vector3(x, top, 0f);
                    Vector3 c = bases[i + 1] + Quaternion.Euler(0f, yaws[i + 1], 0f) * new Vector3(x, top, 0f);
                    const int n = 8;
                    var pts = new Vector3[n + 1];
                    var radii = new float[n + 1];
                    for (int k = 0; k <= n; k++)
                    {
                        float t = k / (float)n;
                        pts[k] = Vector3.Lerp(a, c, t) + Vector3.down * sag * 4f * t * (1f - t);
                        radii[k] = 0.022f;
                    }
                    b.AddTube(pts, radii, 3, Site.Iron, 0.03f, 0f, 0f, null, false, false);
                }
            }
            return b.ToMesh("Power Wires");
        }

        // ------------------------------------------------------------------ graveyard

        /// <summary>Headstone styles: 0 rounded slab, 1 cross, 2 obelisk, 3 slanted broken slab, 4 wooden cross, 5 flat plaque.</summary>
        public const int HeadstoneStyles = 6;

        /// <summary>A headstone facing +Z (its front), with a grave mound in front of it.</summary>
        public static Mesh Headstone(System.Random rng, int style)
        {
            var b = new LowPolyMeshBuilder(rng);
            Color stone = b.Next() < 0.5f ? Site.Granite : Site.GraniteDark;
            // The grave in front: a low sunken mound.
            b.AddBlob(new Vector3(0f, 0f, 1.05f), new Vector3(0.45f, 0.12f, 0.95f), 0, 0.15f, _ => b.Jitter(Site.Grave, 0.12f), true);
            float tilt = b.Range(-8f, 8f), lean = b.Range(-6f, 10f);
            Quaternion q = Quaternion.Euler(lean, 0f, tilt);
            switch (style)
            {
                case 1:
                    AddBoxAt(b, new Vector3(0f, 0.55f, 0f), new Vector3(0.14f, 1.1f, 0.12f), q, stone);
                    AddBoxAt(b, q * new Vector3(0f, 0.78f, 0f), new Vector3(0.56f, 0.13f, 0.12f), q, stone);
                    AddBoxAt(b, new Vector3(0f, 0.08f, 0f), new Vector3(0.4f, 0.16f, 0.3f), Quaternion.identity, stone * 0.9f);
                    break;
                case 2:
                    AddBoxAt(b, new Vector3(0f, 0.15f, 0f), new Vector3(0.45f, 0.3f, 0.45f), Quaternion.identity, stone * 0.9f);
                    b.AddFrustum(new Vector3(0f, 0.3f, 0f), q * new Vector3(0f, 1.6f, 0f) + Vector3.up * 0.3f, 0.17f, 0.1f, 4, stone, 0.08f);
                    b.AddCone(q * new Vector3(0f, 1.6f, 0f) + Vector3.up * 0.3f, q * new Vector3(0f, 1.82f, 0f) + Vector3.up * 0.3f, 0.12f, 4, stone, 0.08f);
                    break;
                case 3:
                    q = Quaternion.Euler(b.Range(14f, 24f), 0f, b.Range(-14f, 14f));
                    AddBoxAt(b, q * new Vector3(0f, 0.32f, 0f), new Vector3(0.6f, 0.62f, 0.12f), q, stone * 0.85f);
                    b.AddBlob(new Vector3(0.45f, 0.05f, 0.3f), new Vector3(0.18f, 0.06f, 0.12f), 0, 0.3f, _ => stone * 0.8f, true);
                    break;
                case 4:
                    AddBoxAt(b, new Vector3(0f, 0.5f, 0f), new Vector3(0.08f, 1f, 0.06f), q, Site.WeatheredDark);
                    AddBoxAt(b, q * new Vector3(0f, 0.72f, 0f), new Vector3(0.44f, 0.07f, 0.06f), q, Site.WeatheredDark);
                    break;
                case 5:
                    AddBoxAt(b, new Vector3(0f, 0.08f, 0.2f), new Vector3(0.55f, 0.16f, 0.4f), Quaternion.Euler(-12f, 0f, 0f), stone);
                    break;
                default:
                    // Rounded slab: a box with a half-disc top.
                    AddBoxAt(b, q * new Vector3(0f, 0.36f, 0f), new Vector3(0.58f, 0.72f, 0.14f), q, stone);
                    b.AddTube(new[] { q * new Vector3(0f, 0.72f, -0.07f), q * new Vector3(0f, 0.72f, 0.07f) }, new[] { 0.29f, 0.29f }, 8, stone, 0.06f);
                    break;
            }
            if (b.Next() < 0.4f) b.AddBlob(new Vector3(b.Range(-0.2f, 0.2f), 0.02f, 0.12f), new Vector3(0.22f, 0.08f, 0.1f), 0, 0.3f, _ => b.Jitter(Site.Moss, 0.15f), true);
            return b.ToMesh("Headstone");
        }

        static void AddBoxAt(LowPolyMeshBuilder b, Vector3 centre, Vector3 size, Quaternion rot, Color color)
        {
            Vector3 h = size * 0.5f;
            var c = new Vector3[8];
            int k = 0;
            foreach (float y in new[] { -h.y, h.y })
            {
                c[k++] = centre + rot * new Vector3(-h.x, y, -h.z);
                c[k++] = centre + rot * new Vector3(h.x, y, -h.z);
                c[k++] = centre + rot * new Vector3(h.x, y, h.z);
                c[k++] = centre + rot * new Vector3(-h.x, y, h.z);
            }
            b.AddHexahedron(c, color, 0.07f);
        }

        /// <summary>A wrought-iron graveyard fence along +X: posts with finials and two rails, bars between.</summary>
        public static Mesh IronFence(System.Random rng, float length)
        {
            var b = new LowPolyMeshBuilder(rng);
            int bays = Mathf.Max(1, Mathf.RoundToInt(length / 1.6f));
            for (int i = 0; i <= bays; i++)
            {
                float x = length * i / bays;
                b.AddBox(new Vector3(x - 0.05f, 0f, -0.05f), new Vector3(0.1f, 1.25f, 0.1f), Site.Iron, 0.06f);
                b.AddCone(new Vector3(x, 1.25f, 0f), new Vector3(x, 1.42f, 0f), 0.06f, 4, Site.Iron, 0.06f);
            }
            float sag = 0f;
            foreach (float y in new[] { 0.25f, 1.05f })
                b.AddBox(new Vector3(0f, y - 0.025f + sag, -0.02f), new Vector3(length, 0.05f, 0.04f), Site.Iron, 0.06f);
            for (float x = 0.2f; x < length - 0.1f; x += 0.2f)
            {
                if (b.Next() < 0.06f) continue;   // missing bars
                b.AddBox(new Vector3(x - 0.012f, 0.25f, -0.012f), new Vector3(0.024f, 0.95f, 0.024f), Site.Iron, 0.06f);
            }
            return b.ToMesh("Iron Fence");
        }

        // ------------------------------------------------------------------ playground

        public enum PlayKind { Swings, Slide, Seesaw, Roundabout, Climber }

        /// <summary>One piece of rusty, faded playground equipment (origin at its middle, on the ground).</summary>
        public static Mesh Playground(System.Random rng, PlayKind kind)
        {
            var b = new LowPolyMeshBuilder(rng);
            Color frame = b.Next() < 0.5f ? Site.FadedBlue : Site.FadedRed;
            Color rust = Rust.Orange;
            switch (kind)
            {
                case PlayKind.Swings:
                    foreach (float x in new[] { -1.9f, 1.9f })
                        foreach (float z in new[] { -0.9f, 0.9f })
                            b.AddFrustum(new Vector3(x, 0f, z), new Vector3(x, 2.4f, 0f), 0.05f, 0.05f, 5, b.Jitter(frame, 0.1f), 0.08f);
                    b.AddFrustum(new Vector3(-2f, 2.4f, 0f), new Vector3(2f, 2.4f, 0f), 0.06f, 0.06f, 5, rust, 0.1f);
                    break;
                case PlayKind.Slide:
                    // Ladder, platform and a chute down to +X.
                    foreach (float z in new[] { -0.3f, 0.3f })
                    {
                        b.AddFrustum(new Vector3(-1.4f, 0f, z), new Vector3(-0.9f, 1.8f, z), 0.04f, 0.04f, 4, b.Jitter(frame, 0.1f), 0.08f);
                        b.AddFrustum(new Vector3(-0.45f, 0f, z), new Vector3(-0.45f, 1.8f, z), 0.04f, 0.04f, 4, b.Jitter(frame, 0.1f), 0.08f);
                    }
                    for (int i = 1; i < 6; i++)
                    {
                        float t = i / 6f;
                        Vector3 p = Vector3.Lerp(new Vector3(-1.4f, 0f, 0f), new Vector3(-0.9f, 1.8f, 0f), t);
                        b.AddFrustum(p + Vector3.back * 0.3f, p + Vector3.forward * 0.3f, 0.025f, 0.025f, 4, rust, 0.1f);
                    }
                    b.AddBox(new Vector3(-0.95f, 1.75f, -0.35f), new Vector3(0.55f, 0.08f, 0.7f), rust, 0.1f);
                    var s0 = new Vector3(-0.45f, 1.75f, 0f);
                    var s1 = new Vector3(1.6f, 0.25f, 0f);
                    Vector3 side = Vector3.forward * 0.28f;
                    b.AddQuadOutward(s0 - side, s0 + side, s1 + side, s1 - side, Site.FadedYellow, 0.1f, (s0 + s1) * 0.5f + Vector3.down);
                    b.AddQuadOutward(s0 - side, s0 + side, s1 + side, s1 - side, Site.FadedYellow * 0.7f, 0.1f, (s0 + s1) * 0.5f + Vector3.up);
                    foreach (float z in new[] { -0.3f, 0.3f })
                        b.AddFrustum(s0 + new Vector3(0f, 0.12f, z), s1 + new Vector3(0f, 0.12f, z), 0.025f, 0.025f, 4, Site.FadedYellow * 0.8f, 0.08f);
                    b.AddFrustum(new Vector3(1.6f, 0f, 0f), new Vector3(1.6f, 0.25f, 0f), 0.04f, 0.04f, 4, rust, 0.1f);
                    break;
                case PlayKind.Seesaw:
                    b.AddBox(new Vector3(-0.15f, 0f, -0.2f), new Vector3(0.3f, 0.45f, 0.4f), rust, 0.1f);
                    var r = Quaternion.Euler(0f, 0f, 14f);
                    AddBoxAt(b, new Vector3(0f, 0.5f, 0f), new Vector3(3.4f, 0.07f, 0.28f), r, b.Jitter(frame, 0.1f));
                    foreach (float x in new[] { -1.35f, 1.35f })
                        b.AddFrustum(new Vector3(0f, 0.5f, 0f) + r * new Vector3(x, 0.05f, -0.15f), new Vector3(0f, 0.5f, 0f) + r * new Vector3(x, 0.3f, -0.15f), 0.025f, 0.025f, 4, Site.Iron, 0.08f);
                    break;
                case PlayKind.Roundabout:
                    b.AddFrustum(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.32f, 0f), 0.15f, 0.12f, 6, Site.Iron, 0.08f);
                    b.AddFrustum(new Vector3(0f, 0.3f, 0f), new Vector3(0f, 0.38f, 0f), 1.3f, 1.3f, 12, b.Jitter(Site.FadedRed, 0.1f), 0.1f);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * 0.5f;
                        var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        b.AddFrustum(d * 1.1f + Vector3.up * 0.38f, d * 0.95f + Vector3.up * 0.95f, 0.03f, 0.03f, 4, rust, 0.08f);
                        b.AddFrustum(d * 0.95f + Vector3.up * 0.95f, Vector3.up * 1.05f, 0.03f, 0.03f, 4, rust, 0.08f);
                    }
                    break;
                default:
                    // A dome climber of bent bars.
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        var pts = new Vector3[6];
                        var radii = new float[6];
                        for (int k = 0; k < 6; k++)
                        {
                            float t = k / 5f * Mathf.PI;
                            pts[k] = new Vector3(Mathf.Cos(a) * Mathf.Cos(t) * 1.4f, Mathf.Sin(t) * 1.5f, Mathf.Sin(a) * Mathf.Cos(t) * 1.4f);
                            radii[k] = 0.035f;
                        }
                        b.AddTube(pts, radii, 4, i % 2 == 0 ? rust : frame, 0.1f, 0f, 0f, null, false, false);
                    }
                    break;
            }
            return b.ToMesh(kind.ToString());
        }

        /// <summary>A swing seat on two chains, hanging from its top (origin at the crossbar), seat 0.45 m off the ground.</summary>
        public static Mesh SwingSeat(System.Random rng, bool broken)
        {
            var b = new LowPolyMeshBuilder(rng);
            float drop = 2.4f - 0.45f;
            foreach (float x in new[] { -0.22f, 0.22f })
            {
                bool snapped = broken && x > 0f;
                float len = snapped ? drop * 0.55f : drop;
                b.AddFrustum(new Vector3(x, 0f, 0f), new Vector3(x, -len, 0f), 0.012f, 0.012f, 3, Site.Iron, 0.05f);
            }
            if (broken)
                AddBoxAt(b, new Vector3(0f, -drop + 0.25f, 0f), new Vector3(0.5f, 0.04f, 0.18f), Quaternion.Euler(0f, 0f, -58f), Site.FadedRed);
            else
                b.AddBox(new Vector3(-0.27f, -drop - 0.03f, -0.1f), new Vector3(0.54f, 0.04f, 0.2f), Site.FadedRed, 0.1f);
            return b.ToMesh(broken ? "Broken Swing" : "Swing");
        }

        // ------------------------------------------------------------------ the hanging tree

        /// <summary>Height above the ground of the hanging tree's rope limb, and how far out along +X the rope hangs.</summary>
        public static readonly Vector2 HangingRope = new Vector2(3.6f, 1.9f);

        /// <summary>A huge dead oak with one long, near-horizontal limb reaching out over +X; the rope hangs from it.</summary>
        public static Mesh HangingOak(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            Color bark = Palette.BarkDark;
            Roots(b, 6, 1.4f, 0.24f, bark);
            Vector3[] trunk = Trunk(b, 3.0f, 0.55f, 0.36f, bark, new Vector3(-0.08f, 0f, 0f), 3, false);
            Vector3 crown = trunk[trunk.Length - 1];
            // The hanging limb: out to +X, dipping slightly, ending well past the rope.
            Vector3 start = crown + new Vector3(0.05f, -0.15f, 0f);
            Limb(b, start, new Vector3(1f, 0.32f, 0.02f), 3.1f, 0.2f, bark, new Vector3(0f, -0.1f, 0f), 4, 0.04f, 0.02f);
            float[] yaws = { 160f, 230f, 300f, 20f };
            float[] elev = { 40f, 52f, 35f, 62f };
            for (int i = 0; i < yaws.Length; i++)
            {
                Vector3[] limb = Limb(b, crown - Vector3.up * 0.1f, Dir(yaws[i], elev[i]), 2.6f, 0.17f, bark, new Vector3(0f, -0.06f, 0f), 3);
                Vector3[] sub = Limb(b, Along(limb, 0.6f), Dir(yaws[i] + 40f, 30f), 1.4f, 0.07f, bark, Vector3.zero, 2);
                Twigs(b, sub, 3, 0.5f, 0.025f, bark);
                Twigs(b, limb, 3, 0.55f, 0.03f, bark, 0.7f);
            }
            return b.ToMesh("Hanging Tree");
        }

        /// <summary>A rope from a limb (origin) down to a noose, <paramref name="length"/> long.</summary>
        public static Mesh Noose(float length)
        {
            var b = new LowPolyMeshBuilder();
            b.AddFrustum(new Vector3(0f, 0.08f, 0f), new Vector3(0f, -length, 0f), 0.028f, 0.028f, 4, Site.Rope, 0.08f);
            b.AddFrustum(new Vector3(0f, -length - 0.02f, 0f), new Vector3(0f, -length + 0.12f, 0f), 0.05f, 0.045f, 5, Site.Rope * 0.9f, 0.08f);
            b.AddTube(new[] { new Vector3(0f, 0.12f, -0.18f), new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0.12f, 0.18f) }, new[] { 0.03f, 0.03f, 0.03f }, 4, Site.Rope, 0.08f);
            return b.ToMesh("Noose");
        }
    }
}
