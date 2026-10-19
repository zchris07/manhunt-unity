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
        /// <summary>
        /// The lake dock, from the shore (its origin, on the water's level) out along +z: boards of uneven widths and
        /// tones laid across three stringers (a few missing near the end, greened by the water out there), on pairs of
        /// posts (dark and wet up to the waterline) braced crosswise under the deck; a ladder down into the water at the
        /// end, a pair of cleats and a coil of rope. At the shore a ramp of boards runs up to the deck from the ground
        /// <paramref name="rampLength"/> inland (its foot <paramref name="rampFoot"/> over the water's level), on a footing
        /// of stones, with rocks along the bank either side and reeds at the water's edge, so it rises out of the bank.
        /// </summary>
        public static Mesh Dock(System.Random rng, float length, float halfWidth, float deck, float rampLength = 0f, float rampFoot = 0f)
        {
            var b = new LowPolyMeshBuilder(rng);
            float hw = halfWidth;
            Color wet = new Color(0.17f, 0.19f, 0.14f), algae = new Color(0.27f, 0.33f, 0.22f);
            // The deck's boards, across it.
            for (float z = 0f; z < length - 0.05f;)
            {
                float w = b.Range(0.24f, 0.34f);
                float far = z / length;
                if (far > 0.85f && b.Next() < 0.3f) { z += w; continue; }   // missing boards near the end
                float x0 = -hw + b.Range(-0.05f, 0.04f), x1 = hw + b.Range(-0.04f, 0.05f);
                bool broken = far > 0.4f && b.Next() < 0.06f;
                if (broken) { if (b.Next() < 0.5f) x0 += b.Range(0.4f, hw); else x1 -= b.Range(0.4f, hw); }
                float roll = b.Next();
                Color c = roll < 0.15f ? Site.WeatheredDark : roll < 0.25f ? Site.Weathered * 1.15f : Site.Weathered;
                if (far > 0.6f && b.Next() < far - 0.45f) c = Color.Lerp(c, algae, 0.45f);
                float y = deck - 0.05f + b.Range(-0.012f, 0.012f);
                b.AddBox(new Vector3(x0, y, z + 0.015f), new Vector3(x1 - x0, 0.06f, w - 0.03f), b.Jitter(c, 0.1f), 0.06f);
                z += w;
            }
            // Three stringers under it.
            foreach (float x in new[] { -hw + 0.12f, 0f, hw - 0.12f })
                b.AddBox(new Vector3(x - 0.06f, deck - 0.24f, 0f), new Vector3(0.12f, 0.19f, length), Site.WeatheredDark, 0.08f);
            // Posts in pairs, dark and wet to the waterline, braced crosswise.
            for (float z = 0.3f; z < length; z += 1.8f)
            {
                foreach (float x in new[] { -hw + 0.04f, hw - 0.04f })
                {
                    b.AddFrustum(new Vector3(x, -1.6f, z), new Vector3(x, 0.06f, z), 0.1f, 0.095f, 6, b.Jitter(wet, 0.08f), 0.05f);
                    b.AddFrustum(new Vector3(x, 0.06f, z), new Vector3(x, deck + 0.16f, z), 0.095f, 0.09f, 6, b.Jitter(Site.WeatheredDark, 0.08f), 0.06f);
                }
                b.AddFrustum(new Vector3(-hw + 0.1f, -0.25f, z), new Vector3(hw - 0.1f, deck - 0.28f, z), 0.035f, 0.035f, 4, Site.WeatheredDark * 0.9f, 0.06f);
            }
            // A ladder down into the water at the end.
            float lx = hw - 0.45f, lz = length + 0.04f;
            foreach (float x in new[] { lx - 0.22f, lx + 0.22f })
                b.AddFrustum(new Vector3(x, -0.9f, lz + 0.06f), new Vector3(x, deck + 0.45f, lz), 0.025f, 0.025f, 4, Site.Iron, 0.05f);
            for (float y = -0.6f; y < deck; y += 0.28f)
                b.AddFrustum(new Vector3(lx - 0.22f, y, lz + 0.05f), new Vector3(lx + 0.22f, y, lz + 0.05f), 0.018f, 0.018f, 4, Site.Iron, 0.05f);
            // Two cleats near the end, and a coil of rope.
            foreach (float x in new[] { -hw + 0.18f, hw - 0.18f })
            {
                float z = length - b.Range(0.5f, 0.9f);
                b.AddBox(new Vector3(x - 0.03f, deck + 0.01f, z - 0.12f), new Vector3(0.06f, 0.05f, 0.24f), Site.Iron * 0.8f, 0.05f);
                b.AddBox(new Vector3(x - 0.04f, deck, z - 0.04f), new Vector3(0.08f, 0.03f, 0.08f), Site.Iron * 0.8f, 0.05f);
            }
            var coil = new Vector3(-hw * 0.4f, deck + 0.02f, length - 1.5f);
            for (int ring = 0; ring < 3; ring++)
            {
                float r = 0.22f - ring * 0.05f;
                for (int i = 0; i < 8; i++)
                {
                    float a0 = i * Mathf.PI / 4f, a1 = (i + 1) * Mathf.PI / 4f;
                    b.AddFrustum(coil + new Vector3(Mathf.Cos(a0) * r, ring * 0.03f, Mathf.Sin(a0) * r), coil + new Vector3(Mathf.Cos(a1) * r, ring * 0.03f, Mathf.Sin(a1) * r),
                        0.022f, 0.022f, 4, new Color(0.5f, 0.43f, 0.3f), 0.06f);
                }
            }
            if (rampLength > 0.05f)
            {
                // The ramp up from the bank: boards on two sloping stringers.
                float Rise(float z) => Mathf.Lerp(rampFoot, deck, Mathf.InverseLerp(-rampLength, 0f, z));
                for (float z = -rampLength; z < -0.02f;)
                {
                    float w = Mathf.Min(b.Range(0.24f, 0.32f), -z);
                    float y0 = Rise(z) - 0.05f, y1 = Rise(z + w) - 0.05f;
                    float x0 = -hw + 0.06f + b.Range(-0.04f, 0.03f), x1 = hw - 0.06f + b.Range(-0.03f, 0.04f);
                    var c = b.Jitter(b.Next() < 0.2f ? Site.WeatheredDark : Site.Weathered, 0.1f);
                    b.AddHexahedron(new[]
                    {
                        new Vector3(x0, y0, z + 0.015f), new Vector3(x1, y0, z + 0.015f), new Vector3(x1, y1, z + w - 0.015f), new Vector3(x0, y1, z + w - 0.015f),
                        new Vector3(x0, y0 + 0.06f, z + 0.015f), new Vector3(x1, y0 + 0.06f, z + 0.015f), new Vector3(x1, y1 + 0.06f, z + w - 0.015f), new Vector3(x0, y1 + 0.06f, z + w - 0.015f),
                    }, c, 0.04f);
                    z += w;
                }
                foreach (float x in new[] { -hw + 0.14f, hw - 0.14f })
                    b.AddHexahedron(new[]
                    {
                        new Vector3(x - 0.06f, rampFoot - 0.3f, -rampLength), new Vector3(x + 0.06f, rampFoot - 0.3f, -rampLength), new Vector3(x + 0.06f, deck - 0.24f, 0.2f), new Vector3(x - 0.06f, deck - 0.24f, 0.2f),
                        new Vector3(x - 0.06f, rampFoot - 0.1f, -rampLength), new Vector3(x + 0.06f, rampFoot - 0.1f, -rampLength), new Vector3(x + 0.06f, deck - 0.06f, 0.2f), new Vector3(x - 0.06f, deck - 0.06f, 0.2f),
                    }, Site.WeatheredDark, 0.05f);
                // A footing of stones where the dock meets the bank, and rocks along the bank either side.
                Color Stone() => b.Jitter(b.Next() < 0.5f ? Palette.Stone : Palette.StoneDark, 0.12f);
                for (int i = 0; i < 9; i++)
                {
                    float x = b.Range(-hw, hw), z = b.Range(-0.5f, 0.7f), s = b.Range(0.16f, 0.3f);
                    b.AddBlob(new Vector3(x, Mathf.Min(rampFoot, deck - 0.3f) - s * 0.3f + b.Range(0f, 0.15f), z), new Vector3(s * 1.3f, s * 0.8f, s), 0, 0.25f, _ => Stone(), true);
                }
                foreach (float side in new[] { -1f, 1f })
                    for (int i = 0; i < 4; i++)
                    {
                        float s = b.Range(0.14f, 0.34f), z = b.Range(-rampLength * 0.9f, 0.8f);
                        b.AddBlob(new Vector3(side * (hw + b.Range(0.15f, 0.7f)), Mathf.Lerp(rampFoot, 0f, Mathf.InverseLerp(-rampLength, 0.8f, z)) - s * 0.35f, z),
                            new Vector3(s * 1.2f, s * 0.7f, s), 0, 0.25f, _ => Stone(), true);
                    }
                // Reeds at the water's edge, either side.
                foreach (float side in new[] { -1f, 1f })
                    for (int i = 0; i < 3; i++)
                    {
                        b.Transform = Matrix4x4.TRS(new Vector3(side * (hw + b.Range(0.6f, 1.6f)), -0.25f, b.Range(0.4f, 2.6f)), Quaternion.Euler(0f, b.Range(0f, 360f), 0f), Vector3.one * b.Range(0.8f, 1.15f));
                        AddReeds(b, b.Range(0.9f, 1.3f));
                    }
                b.Transform = null;
            }
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

        /// <summary>
        /// Headstone styles: 0 round-topped tablet on a plinth, 1 ringed Celtic cross on stepped bases, 2 obelisk on a die
        /// with a bronze plaque, 3 broken tablet (its top fallen on the grave), 4 weathered wooden cross hung with a wreath
        /// inside a picket rail, 5 ledger slab over the whole grave, 6 angel on a pedestal, 7 gothic arched tablet between
        /// pinnacles, 8 chest tomb, 9 double tablet.
        /// </summary>
        public const int HeadstoneStyles = 10;

        /// <summary>
        /// The ground a grave takes, from its headstone (at the origin) out over the grave in front: half its width, how far
        /// behind the stone and how far in front.
        /// </summary>
        public const float GraveHalfWidth = 0.52f, GraveBack = 0.3f, GraveFront = 2.05f;

        /// <summary>A headstone's solid part (its collider), centre and size in its own frame.</summary>
        public static (Vector3 centre, Vector3 size) HeadstoneSolid(int style) => style switch
        {
            1 => (new Vector3(0f, 0.68f, 0f), new Vector3(0.62f, 1.36f, 0.42f)),
            2 => (new Vector3(0f, 1.0f, 0f), new Vector3(0.62f, 2.0f, 0.62f)),
            3 => (new Vector3(0f, 0.3f, 0f), new Vector3(0.74f, 0.6f, 0.34f)),
            4 => (new Vector3(0f, 0.55f, 0f), new Vector3(0.52f, 1.1f, 0.2f)),
            5 => (new Vector3(0f, 0.22f, 0.9f), new Vector3(0.86f, 0.44f, 2.04f)),
            6 => (new Vector3(0f, 1.1f, 0f), new Vector3(0.7f, 2.2f, 0.6f)),
            7 => (new Vector3(0f, 0.56f, 0f), new Vector3(0.84f, 1.12f, 0.36f)),
            8 => (new Vector3(0f, 0.4f, 0.9f), new Vector3(0.96f, 0.8f, 2.04f)),
            9 => (new Vector3(0f, 0.46f, 0f), new Vector3(1.02f, 0.92f, 0.34f)),
            _ => (new Vector3(0f, 0.53f, 0f), new Vector3(0.72f, 1.06f, 0.34f)),
        };

        static readonly Color Marble = new Color(0.68f, 0.67f, 0.64f), Sandstone = new Color(0.56f, 0.50f, 0.40f);
        static readonly Color Bronze = new Color(0.33f, 0.30f, 0.18f), Ivy = new Color(0.20f, 0.30f, 0.16f);
        static readonly Color[] Blooms = { new Color(0.62f, 0.16f, 0.16f), new Color(0.80f, 0.78f, 0.72f), new Color(0.72f, 0.60f, 0.22f), new Color(0.46f, 0.30f, 0.52f) };

        /// <summary>
        /// A grave facing +Z (its front): the stone at the origin and the grave before it, out to <see cref="GraveFront"/>.
        /// Each style is carved: a plinth or stepped base, letters cut in its face, a border or a cross; most graves are
        /// kerbed in stone and filled with chippings, or heaped with earth, some with a footstone; some have a vase of
        /// flowers (a few wilted), a lantern, moss creeping up the base.
        /// </summary>
        public static Mesh Headstone(System.Random rng, int style)
        {
            var b = new LowPolyMeshBuilder(rng);
            float pick = b.Next();
            Color stone = b.Jitter(pick < 0.35f ? Site.Granite : pick < 0.6f ? Site.GraniteDark : pick < 0.82f ? Marble : Sandstone, 0.04f);
            Color plinth = Shade(stone, 0.86f), cut = Shade(stone, 0.55f), panel = Shade(stone, 0.9f);
            float tilt = b.Range(-3.5f, 3.5f), lean = b.Range(-2f, 5f);
            Quaternion q = Quaternion.Euler(lean, 0f, tilt), id = Quaternion.identity;

            // Letters cut into a face: lines of shadow, the name's line widest (the face's front at z, after turning by rot).
            void Letters(Quaternion rot, float x, float top, float z, float width, int lines)
            {
                for (int i = 0; i < lines; i++)
                {
                    float w = width * (i == 0 ? 0.85f : b.Range(0.4f, 0.72f));
                    AddBoxAt(b, rot * new Vector3(x, top - i * 0.07f, z), new Vector3(w, i == 0 ? 0.028f : 0.02f, 0.012f), rot, cut);
                }
            }
            // A small cross cut into a face.
            void Cross(Quaternion rot, Vector3 at, float size)
            {
                AddBoxAt(b, rot * at, new Vector3(size * 0.22f, size, 0.012f), rot, cut);
                AddBoxAt(b, rot * (at + new Vector3(0f, size * 0.18f, 0f)), new Vector3(size * 0.62f, size * 0.2f, 0.012f), rot, cut);
            }

            bool covered = style == 5 || style == 8;
            if (!covered) Plot(b, stone, style == 4);
            switch (style)
            {
                case 1:
                {
                    // A ringed cross on two steps.
                    AddBoxAt(b, new Vector3(0f, 0.08f, 0f), new Vector3(0.62f, 0.16f, 0.42f), id, plinth);
                    AddBoxAt(b, new Vector3(0f, 0.22f, 0f), new Vector3(0.44f, 0.12f, 0.3f), id, Shade(plinth, 1.05f));
                    Letters(id, 0f, 0.09f, 0.212f, 0.42f, 1);
                    AddBoxAt(b, q * new Vector3(0f, 0.78f, 0f), new Vector3(0.15f, 1.0f, 0.12f), q, stone);
                    AddBoxAt(b, q * new Vector3(0f, 1.0f, 0f), new Vector3(0.58f, 0.14f, 0.12f), q, stone);
                    var ring = new List<Vector3>();
                    for (int k = 0; k <= 12; k++)
                    {
                        float a = k / 12f * Mathf.PI * 2f;
                        ring.Add(q * new Vector3(Mathf.Cos(a) * 0.18f, 1.0f + Mathf.Sin(a) * 0.18f, 0f));
                    }
                    var radii = new float[ring.Count];
                    for (int k = 0; k < radii.Length; k++) radii[k] = 0.03f;
                    b.AddTube(ring, radii, 4, stone, 0.05f, 0f, 0f, null, false, false);
                    b.AddBlob(q * new Vector3(0f, 1.0f, 0.065f), new Vector3(0.045f, 0.045f, 0.025f), 0, 0.1f, _ => panel);
                    // Knotwork down the shaft, in a few cut bands.
                    for (int k = 0; k < 4; k++) AddBoxAt(b, q * new Vector3(0f, 0.42f + k * 0.12f, 0.062f), new Vector3(0.09f, 0.025f, 0.01f), q, cut);
                    break;
                }
                case 2:
                {
                    // An obelisk: two steps, a die with a bronze plaque under a cornice, a tapering shaft and its point.
                    AddBoxAt(b, new Vector3(0f, 0.06f, 0f), new Vector3(0.62f, 0.12f, 0.62f), id, plinth);
                    AddBoxAt(b, new Vector3(0f, 0.18f, 0f), new Vector3(0.5f, 0.12f, 0.5f), id, Shade(plinth, 1.05f));
                    AddBoxAt(b, new Vector3(0f, 0.42f, 0f), new Vector3(0.38f, 0.36f, 0.38f), id, stone);
                    AddBoxAt(b, new Vector3(0f, 0.625f, 0f), new Vector3(0.45f, 0.05f, 0.45f), id, plinth);
                    AddBoxAt(b, new Vector3(0f, 0.43f, 0.195f), new Vector3(0.24f, 0.18f, 0.015f), id, Bronze);
                    for (int i = 0; i < 3; i++) AddBoxAt(b, new Vector3(0f, 0.48f - i * 0.05f, 0.204f), new Vector3(i == 0 ? 0.17f : 0.12f, 0.016f, 0.008f), id, Shade(Bronze, 0.55f));
                    Taper(b, q * new Vector3(0f, 0.65f, 0f), 0.15f, 1.12f, 0.09f, q, stone);
                    Taper(b, q * new Vector3(0f, 1.77f, 0f), 0.095f, 0.2f, 0.004f, q, stone);
                    Cross(q, new Vector3(0f, 1.38f, 0.125f), 0.16f);
                    break;
                }
                case 3:
                {
                    // A broken tablet: the stump standing jagged on its plinth, the top fallen face up on the grave.
                    AddBoxAt(b, new Vector3(0f, 0.07f, 0f), new Vector3(0.72f, 0.14f, 0.32f), id, plinth);
                    Quaternion qs = Quaternion.Euler(b.Range(4f, 10f), 0f, b.Range(-6f, 6f));
                    AddBoxAt(b, qs * new Vector3(0f, 0.31f, 0f), new Vector3(0.58f, 0.34f, 0.13f), qs, stone);
                    for (int k = 0; k < 3; k++)
                        AddBoxAt(b, qs * new Vector3(-0.2f + k * 0.2f, 0.5f + b.Range(-0.02f, 0.05f), 0f), new Vector3(0.18f, 0.1f, 0.12f),
                            qs * Quaternion.Euler(0f, 0f, b.Range(-30f, 30f)), stone);
                    Letters(qs, 0f, 0.4f, 0.07f, 0.4f, 2);
                    Quaternion qf = Quaternion.Euler(b.Range(-4f, 4f), b.Range(-28f, 28f), b.Range(-4f, 4f));
                    Vector3 fallen = new Vector3(b.Range(-0.08f, 0.08f), 0.11f, 0.78f);
                    AddBoxAt(b, fallen, new Vector3(0.56f, 0.1f, 0.5f), qf, Shade(stone, 0.95f));
                    b.AddTube(new[] { fallen + qf * new Vector3(0f, -0.05f, 0.25f), fallen + qf * new Vector3(0f, 0.05f, 0.25f) }, new[] { 0.28f, 0.28f }, 10, Shade(stone, 0.95f), 0.05f);
                    for (int i = 0; i < 3; i++)
                        AddBoxAt(b, fallen + qf * new Vector3(0f, 0.052f, 0.18f - i * 0.08f), new Vector3(i == 0 ? 0.32f : 0.22f, 0.01f, 0.025f), qf, cut);
                    for (int k = 0; k < 4; k++)
                        b.AddBlob(new Vector3(b.Range(-0.4f, 0.4f), 0.03f, b.Range(0.25f, 1.3f)), new Vector3(0.06f, 0.04f, 0.05f) * b.Range(0.6f, 1.3f), 0, 0.3f, _ => Shade(stone, 0.9f), true);
                    break;
                }
                case 4:
                {
                    // A weathered wooden cross, a tin nameplate and a wreath of ivy hung on it.
                    Color wood = b.Jitter(Site.WeatheredDark, 0.08f);
                    AddBoxAt(b, q * new Vector3(0f, 0.55f, 0f), new Vector3(0.09f, 1.1f, 0.07f), q, wood);
                    AddBoxAt(b, q * new Vector3(0f, 0.82f, 0f), new Vector3(0.52f, 0.08f, 0.07f), q, wood);
                    Taper(b, q * new Vector3(0f, 1.1f, 0f), 0.06f, 0.06f, 0.01f, q, Shade(wood, 0.8f));
                    AddBoxAt(b, q * new Vector3(0f, 0.82f, 0.042f), new Vector3(0.17f, 0.055f, 0.012f), q, new Color(0.42f, 0.42f, 0.40f));
                    Vector3 hub = q * new Vector3(0f, 0.55f, 0.07f);
                    for (int k = 0; k < 11; k++)
                    {
                        float a = k / 11f * Mathf.PI * 2f;
                        b.AddBlob(hub + q * new Vector3(Mathf.Cos(a) * 0.12f, Mathf.Sin(a) * 0.12f, 0f), new Vector3(0.045f, 0.045f, 0.03f), 0, 0.3f, _ => b.Jitter(Ivy, 0.2f));
                    }
                    for (int k = 0; k < 3; k++)
                        b.AddBlob(hub + q * new Vector3(b.Range(-0.12f, 0.12f), -0.1f + b.Range(-0.03f, 0.03f), 0.03f), Vector3.one * 0.022f, 0, 0.1f, _ => Blooms[0]);
                    break;
                }
                case 5:
                {
                    // A ledger: a slab over the whole grave on a base, a cross in relief down it, a small tablet at its head.
                    AddBoxAt(b, new Vector3(0f, 0.06f, 0.9f), new Vector3(0.86f, 0.12f, 2.02f), id, plinth);
                    AddBoxAt(b, new Vector3(0f, 0.17f, 0.9f), new Vector3(0.78f, 0.1f, 1.94f), id, stone);
                    AddBoxAt(b, new Vector3(0f, 0.235f, 0.9f), new Vector3(0.68f, 0.03f, 1.84f), id, panel);
                    AddBoxAt(b, new Vector3(0f, 0.26f, 1.02f), new Vector3(0.07f, 0.025f, 1.3f), id, stone);
                    AddBoxAt(b, new Vector3(0f, 0.26f, 0.7f), new Vector3(0.42f, 0.025f, 0.07f), id, stone);
                    AddBoxAt(b, new Vector3(0f, 0.38f, 0f), new Vector3(0.62f, 0.3f, 0.16f), id, stone);
                    b.AddTube(new[] { new Vector3(0f, 0.53f, -0.08f), new Vector3(0f, 0.53f, 0.08f) }, new[] { 0.18f, 0.18f }, 8, stone, 0.05f);
                    Letters(id, 0f, 0.47f, 0.083f, 0.44f, 3);
                    if (b.Next() < 0.6f) b.AddBlob(new Vector3(b.Range(-0.25f, 0.25f), 0.25f, b.Range(1.2f, 1.7f)), new Vector3(0.12f, 0.03f, 0.18f), 0, 0.3f, _ => b.Jitter(Site.Moss, 0.15f), true);
                    break;
                }
                case 6:
                {
                    // An angel on a pedestal, head bowed, hands together, wings half spread.
                    AddBoxAt(b, new Vector3(0f, 0.07f, 0f), new Vector3(0.66f, 0.14f, 0.56f), id, plinth);
                    AddBoxAt(b, new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.62f, 0.42f), id, stone);
                    AddBoxAt(b, new Vector3(0f, 0.795f, 0f), new Vector3(0.58f, 0.07f, 0.5f), id, plinth);
                    AddBoxAt(b, new Vector3(0f, 0.46f, 0.214f), new Vector3(0.36f, 0.42f, 0.01f), id, panel);
                    Letters(id, 0f, 0.6f, 0.222f, 0.3f, 4);
                    Color white = b.Jitter(Marble, 0.03f);
                    b.AddTube(new[] { new Vector3(0f, 0.83f, 0f), new Vector3(0f, 1.2f, 0.01f), new Vector3(0f, 1.55f, 0.02f), new Vector3(0f, 1.68f, 0.02f) },
                        new[] { 0.19f, 0.14f, 0.1f, 0.085f }, 8, white, 0.05f);
                    b.AddBlob(new Vector3(0f, 1.79f, 0.06f), new Vector3(0.07f, 0.08f, 0.07f), 1, 0.05f, _ => white);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        b.AddFrustum(new Vector3(s * 0.09f, 1.64f, 0.02f), new Vector3(s * 0.02f, 1.44f, 0.11f), 0.032f, 0.026f, 5, white, 0.05f);
                        AddBoxAt(b, new Vector3(s * 0.19f, 1.58f, -0.1f), new Vector3(0.06f, 0.62f, 0.2f), Quaternion.Euler(0f, s * 18f, s * -24f), white);
                        AddBoxAt(b, new Vector3(s * 0.24f, 1.36f, -0.12f), new Vector3(0.05f, 0.5f, 0.16f), Quaternion.Euler(0f, s * 18f, s * -12f), Shade(white, 0.95f));
                    }
                    b.AddBlob(new Vector3(0f, 0.2f, 0.24f), new Vector3(0.18f, 0.06f, 0.06f), 0, 0.3f, _ => b.Jitter(Site.Moss, 0.15f), true);
                    break;
                }
                case 7:
                {
                    // A gothic tablet: a pointed arch between two pinnacled pilasters, its recessed panel lettered.
                    AddBoxAt(b, new Vector3(0f, 0.07f, 0f), new Vector3(0.84f, 0.14f, 0.36f), id, plinth);
                    AddBoxAt(b, q * new Vector3(0f, 0.47f, 0f), new Vector3(0.58f, 0.66f, 0.14f), q, stone);
                    Gable(b, q, new Vector3(0f, 0.8f, 0f), 0.3f, 0.3f, 0.07f, stone);
                    AddBoxAt(b, q * new Vector3(0f, 0.48f, 0.072f), new Vector3(0.38f, 0.48f, 0.01f), q, panel);
                    Gable(b, q, new Vector3(0f, 0.72f, 0.072f), 0.19f, 0.17f, 0.006f, panel);
                    b.AddBlob(q * new Vector3(0f, 0.95f, 0.07f), new Vector3(0.04f, 0.04f, 0.02f), 0, 0.1f, _ => cut);
                    Letters(q, 0f, 0.62f, 0.08f, 0.3f, 4);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        AddBoxAt(b, q * new Vector3(s * 0.34f, 0.54f, 0f), new Vector3(0.1f, 0.8f, 0.18f), q, Shade(stone, 1.04f));
                        Taper(b, q * new Vector3(s * 0.34f, 0.94f, 0f), 0.06f, 0.2f, 0.004f, q, Shade(stone, 1.04f));
                    }
                    break;
                }
                case 8:
                {
                    // A chest tomb: panelled sides between corner pilasters, an overhanging lid knocked a little askew.
                    AddBoxAt(b, new Vector3(0f, 0.05f, 0.9f), new Vector3(0.96f, 0.1f, 2.02f), id, plinth);
                    AddBoxAt(b, new Vector3(0f, 0.38f, 0.9f), new Vector3(0.8f, 0.56f, 1.8f), id, stone);
                    foreach (float sx in new[] { -1f, 1f })
                        foreach (float sz in new[] { -1f, 1f })
                            AddBoxAt(b, new Vector3(sx * 0.4f, 0.38f, 0.9f + sz * 0.9f), new Vector3(0.1f, 0.56f, 0.1f), id, Shade(stone, 1.05f));
                    foreach (float sx in new[] { -1f, 1f })
                        for (int k = 0; k < 2; k++)
                        {
                            float z = 0.48f + k * 0.84f;
                            AddBoxAt(b, new Vector3(sx * 0.402f, 0.38f, z), new Vector3(0.012f, 0.36f, 0.66f), id, panel);
                            b.AddBlob(new Vector3(sx * 0.41f, 0.38f, z), new Vector3(0.01f, 0.07f, 0.07f), 0, 0.1f, _ => cut);
                        }
                    AddBoxAt(b, new Vector3(0f, 0.38f, 1.802f), new Vector3(0.6f, 0.36f, 0.012f), id, panel);
                    Letters(id, 0f, 0.5f, 1.81f, 0.48f, 4);
                    Quaternion lid = Quaternion.Euler(0f, b.Range(-4f, 4f), 0f);
                    Vector3 lc = new Vector3(b.Range(-0.03f, 0.03f), 0.7f, 0.9f + b.Range(-0.04f, 0.04f));
                    AddBoxAt(b, lc, new Vector3(0.96f, 0.08f, 2.0f), lid, plinth);
                    AddBoxAt(b, lc + Vector3.up * 0.06f, new Vector3(0.84f, 0.04f, 1.88f), lid, stone);
                    AddBoxAt(b, lc + lid * new Vector3(0f, 0.09f, 0.1f), new Vector3(0.07f, 0.025f, 0.9f), lid, panel);
                    AddBoxAt(b, lc + lid * new Vector3(0f, 0.09f, -0.12f), new Vector3(0.36f, 0.025f, 0.07f), lid, panel);
                    if (b.Next() < 0.6f) b.AddBlob(lc + lid * new Vector3(b.Range(-0.3f, 0.3f), 0.08f, b.Range(-0.8f, 0.8f)), new Vector3(0.16f, 0.03f, 0.22f), 0, 0.3f, _ => b.Jitter(Site.Moss, 0.15f), true);
                    break;
                }
                case 9:
                {
                    // A double tablet for two: twin rounded tops, a lettered panel under each, a cross between.
                    AddBoxAt(b, new Vector3(0f, 0.07f, 0.02f), new Vector3(1.0f, 0.14f, 0.34f), id, plinth);
                    AddBoxAt(b, q * new Vector3(0f, 0.42f, 0f), new Vector3(0.9f, 0.56f, 0.13f), q, stone);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        b.AddTube(new[] { q * new Vector3(s * 0.22f, 0.7f, -0.065f), q * new Vector3(s * 0.22f, 0.7f, 0.065f) }, new[] { 0.22f, 0.22f }, 10, stone, 0.05f);
                        AddBoxAt(b, q * new Vector3(s * 0.22f, 0.5f, 0.068f), new Vector3(0.32f, 0.44f, 0.01f), q, panel);
                        Letters(q, s * 0.22f, 0.64f, 0.076f, 0.26f, 4);
                    }
                    Cross(q, new Vector3(0f, 0.76f, 0.068f), 0.16f);
                    break;
                }
                default:
                {
                    // A round-topped tablet on a plinth, a bordered panel lettered, a cross cut above it.
                    AddBoxAt(b, new Vector3(0f, 0.07f, 0.02f), new Vector3(0.72f, 0.14f, 0.32f), id, plinth);
                    AddBoxAt(b, q * new Vector3(0f, 0.45f, 0f), new Vector3(0.58f, 0.62f, 0.13f), q, stone);
                    b.AddTube(new[] { q * new Vector3(0f, 0.76f, -0.065f), q * new Vector3(0f, 0.76f, 0.065f) }, new[] { 0.29f, 0.29f }, 10, stone, 0.05f);
                    AddBoxAt(b, q * new Vector3(0f, 0.46f, 0.068f), new Vector3(0.46f, 0.5f, 0.01f), q, panel);
                    Letters(q, 0f, 0.62f, 0.076f, 0.36f, 4);
                    Cross(q, new Vector3(0f, 0.86f, 0.07f), 0.13f);
                    break;
                }
            }
            if (style != 6 && b.Next() < 0.45f)
                b.AddBlob(new Vector3(b.Range(-0.25f, 0.25f), 0.03f, b.Range(0.1f, 0.2f)), new Vector3(0.18f, 0.07f, 0.07f), 0, 0.3f, _ => b.Jitter(Site.Moss, 0.15f), true);
            if (!covered && style != 4)
            {
                if (b.Next() < 0.4f) Vase(b, new Vector3(b.Next() < 0.5f ? -0.3f : 0.3f, 0f, 0.36f));
                else if (b.Next() < 0.15f) Lantern(b, new Vector3(b.Next() < 0.5f ? -0.32f : 0.32f, 0f, 0.34f));
            }
            return b.ToMesh("Headstone");
        }

        static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        /// <summary>
        /// The grave before a stone: kerbed in stone and filled with chippings or earth, or a heap of earth, or sunk with a
        /// footstone; or (<paramref name="picket"/>) a heap inside a low picket rail.
        /// </summary>
        static void Plot(LowPolyMeshBuilder b, Color stone, bool picket)
        {
            float r = b.Next();
            if (picket)
            {
                b.AddBlob(new Vector3(0f, 0f, 1.05f), new Vector3(0.38f, 0.13f, 0.82f), 0, 0.15f, _ => b.Jitter(Site.Grave, 0.12f), true);
                Color wood = Site.Weathered;
                foreach (float x in new[] { -0.44f, 0.44f })
                {
                    for (float z = 0.3f; z < 1.95f; z += 0.24f)
                    {
                        if (b.Next() < 0.12f) continue;
                        Quaternion lean = Quaternion.Euler(b.Range(-8f, 8f), 0f, b.Range(-8f, 8f));
                        AddBoxAt(b, new Vector3(x, 0.17f, z), new Vector3(0.04f, 0.34f, 0.05f), lean, b.Jitter(wood, 0.1f));
                    }
                    AddBoxAt(b, new Vector3(x, 0.24f, 1.12f), new Vector3(0.03f, 0.04f, 1.68f), Quaternion.Euler(b.Range(-2f, 2f), 0f, 0f), Site.WeatheredDark);
                }
                return;
            }
            if (r < 0.48f)
            {
                // Kerbs round the grave, corner posts at its foot, filled with pale chippings or bare earth.
                Color kerb = Shade(stone, 0.92f);
                AddBoxAt(b, new Vector3(-0.44f, 0.065f, 1.06f), new Vector3(0.08f, 0.13f, 1.76f), Quaternion.identity, kerb);
                AddBoxAt(b, new Vector3(0.44f, 0.065f, 1.06f), new Vector3(0.08f, 0.13f, 1.76f), Quaternion.identity, kerb);
                AddBoxAt(b, new Vector3(0f, 0.065f, 1.94f), new Vector3(0.96f, 0.13f, 0.08f), Quaternion.identity, kerb);
                foreach (float x in new[] { -0.44f, 0.44f })
                {
                    AddBoxAt(b, new Vector3(x, 0.09f, 1.94f), new Vector3(0.12f, 0.18f, 0.12f), Quaternion.identity, Shade(stone, 0.97f));
                    b.AddCone(new Vector3(x, 0.18f, 1.94f), new Vector3(x, 0.23f, 1.94f), 0.06f, 4, Shade(stone, 0.97f), 0.05f);
                }
                Color fill = b.Next() < 0.55f ? new Color(0.58f, 0.57f, 0.53f) : Site.Grave;
                AddBoxAt(b, new Vector3(0f, 0.04f, 1.06f), new Vector3(0.8f, 0.08f, 1.76f), Quaternion.identity, fill);
                for (int k = 0; k < 6; k++)
                    b.AddBlob(new Vector3(b.Range(-0.32f, 0.32f), 0.08f, b.Range(0.3f, 1.8f)), Vector3.one * b.Range(0.02f, 0.035f), 0, 0.3f, _ => b.Jitter(fill, 0.15f), true);
            }
            else if (r < 0.85f)
            {
                b.AddBlob(new Vector3(0f, 0f, 1.05f), new Vector3(0.44f, 0.13f, 0.9f), 0, 0.15f, _ => b.Jitter(Site.Grave, 0.12f), true);
                if (b.Next() < 0.5f) b.AddBlob(new Vector3(b.Range(-0.2f, 0.2f), 0.1f, b.Range(0.6f, 1.5f)), new Vector3(0.12f, 0.05f, 0.12f), 0, 0.3f, _ => b.Jitter(Site.Moss, 0.2f), true);
            }
            else
            {
                // Sunk a little, with a footstone at its foot.
                b.AddBlob(new Vector3(0f, -0.02f, 1.05f), new Vector3(0.42f, 0.04f, 0.88f), 0, 0.1f, _ => b.Jitter(Shade(Site.Grave, 0.85f), 0.1f), true);
                AddBoxAt(b, new Vector3(0f, 0.09f, 1.9f), new Vector3(0.32f, 0.18f, 0.09f), Quaternion.Euler(b.Range(-6f, 6f), 0f, b.Range(-5f, 5f)), stone);
            }
        }

        /// <summary>A stone vase of flowers before a grave, a few of them wilting.</summary>
        static void Vase(LowPolyMeshBuilder b, Vector3 at)
        {
            Color vase = b.Next() < 0.5f ? new Color(0.48f, 0.47f, 0.45f) : Bronze;
            b.AddFrustum(at, at + Vector3.up * 0.05f, 0.05f, 0.04f, 6, vase, 0.05f);
            b.AddFrustum(at + Vector3.up * 0.05f, at + Vector3.up * 0.17f, 0.04f, 0.062f, 6, vase, 0.05f);
            Color bloom = Blooms[Mathf.Min(Blooms.Length - 1, (int)(b.Next() * Blooms.Length))];
            bool wilted = b.Next() < 0.3f;
            int n = 3 + (int)(b.Next() * 4f);
            for (int i = 0; i < n; i++)
            {
                Vector3 top = at + Vector3.up * 0.17f, tip = top + new Vector3(b.Range(-0.08f, 0.08f), b.Range(0.1f, 0.2f), b.Range(-0.06f, 0.08f));
                if (wilted && b.Next() < 0.6f) tip += new Vector3(0f, -0.1f, 0.06f);
                b.AddFrustum(top, tip, 0.008f, 0.006f, 3, Ivy, 0.1f);
                b.AddBlob(tip, Vector3.one * b.Range(0.025f, 0.035f), 0, 0.2f, _ => wilted ? Shade(bloom, 0.55f) : b.Jitter(bloom, 0.1f));
            }
        }

        /// <summary>A small iron grave lantern, its glass fogged.</summary>
        static void Lantern(LowPolyMeshBuilder b, Vector3 at)
        {
            b.AddBox(at + new Vector3(-0.06f, 0f, -0.06f), new Vector3(0.12f, 0.03f, 0.12f), Site.Iron, 0.05f);
            b.AddBox(at + new Vector3(-0.045f, 0.03f, -0.045f), new Vector3(0.09f, 0.12f, 0.09f), new Color(0.55f, 0.55f, 0.5f), 0.05f);
            b.AddCone(at + Vector3.up * 0.15f, at + Vector3.up * 0.22f, 0.075f, 4, Site.Iron, 0.05f);
        }

        /// <summary>A four-sided taper (an obelisk's shaft, a pinnacle) up from <paramref name="foot"/>, square to <paramref name="rot"/>.</summary>
        static void Taper(LowPolyMeshBuilder b, Vector3 foot, float halfFoot, float height, float halfTop, Quaternion rot, Color color)
        {
            var c = new Vector3[8];
            int k = 0;
            foreach (var (y, h) in new[] { (0f, halfFoot), (height, halfTop) })
            {
                c[k++] = foot + rot * new Vector3(-h, y, -h);
                c[k++] = foot + rot * new Vector3(h, y, -h);
                c[k++] = foot + rot * new Vector3(h, y, h);
                c[k++] = foot + rot * new Vector3(-h, y, h);
            }
            b.AddHexahedron(c, color, 0.06f);
        }

        /// <summary>A pointed gable (a gothic arch's top) standing on <paramref name="at"/>, turned by <paramref name="rot"/>.</summary>
        static void Gable(LowPolyMeshBuilder b, Quaternion rot, Vector3 at, float halfWidth, float height, float halfDepth, Color color)
        {
            Vector3 P(float x, float y, float z) => rot * (at + new Vector3(x, y, z));
            Vector3 lf = P(-halfWidth, 0f, halfDepth), rf = P(halfWidth, 0f, halfDepth), tf = P(0f, height, halfDepth);
            Vector3 lb = P(-halfWidth, 0f, -halfDepth), rb = P(halfWidth, 0f, -halfDepth), tb = P(0f, height, -halfDepth);
            Vector3 inside = P(0f, height * 0.3f, 0f);
            b.AddTriangleOutward(lf, rf, tf, color, inside);
            b.AddTriangleOutward(lb, rb, tb, color, inside);
            b.AddQuadOutward(lf, tf, tb, lb, color, 0.05f, inside);
            b.AddQuadOutward(rf, tf, tb, rb, color, 0.05f, inside);
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

        /// <summary>How tall the graveyard fence stands to the tips of its bars (20% taller than at first).</summary>
        public const float IronFenceHeight = 1.42f * 1.2f;

        /// <summary>
        /// A wrought-iron graveyard fence along +X, 20% taller than at first: posts crowned with tall four-sided spikes and a
        /// collar of short thorns, two rails, and bars between that run up past the top rail into spear points.
        /// </summary>
        public static Mesh IronFence(System.Random rng, float length)
        {
            const float k = 1.2f;
            var b = new LowPolyMeshBuilder(rng);
            int bays = Mathf.Max(1, Mathf.RoundToInt(length / 1.6f));
            for (int i = 0; i <= bays; i++)
            {
                float x = length * i / bays;
                b.AddBox(new Vector3(x - 0.05f, 0f, -0.05f), new Vector3(0.1f, 1.25f * k, 0.1f), Site.Iron, 0.06f);
                // A knob, then the spike, and four thorns about its foot.
                b.AddBox(new Vector3(x - 0.065f, 1.25f * k - 0.02f, -0.065f), new Vector3(0.13f, 0.06f, 0.13f), Site.Iron, 0.06f);
                b.AddCone(new Vector3(x, 1.25f * k + 0.04f, 0f), new Vector3(x, IronFenceHeight + 0.08f, 0f), 0.06f, 4, Site.Iron, 0.06f);
                for (int t = 0; t < 4; t++)
                {
                    float a = t * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                    var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 foot = new Vector3(x, 1.25f * k + 0.05f, 0f) + o * 0.04f;
                    b.AddCone(foot, foot + o * 0.1f + Vector3.up * 0.08f, 0.018f, 3, Site.Iron, 0.06f);
                }
            }
            foreach (float y in new[] { 0.25f * k, 1.05f * k })
                b.AddBox(new Vector3(0f, y - 0.025f, -0.02f), new Vector3(length, 0.05f, 0.04f), Site.Iron, 0.06f);
            for (float x = 0.2f; x < length - 0.1f; x += 0.2f)
            {
                if (b.Next() < 0.06f) continue;   // missing bars
                // Each bar rises past the top rail into a spear point.
                float top = 1.25f * k + 0.04f;
                b.AddBox(new Vector3(x - 0.012f, 0.25f * k, -0.012f), new Vector3(0.024f, top - 0.25f * k, 0.024f), Site.Iron, 0.06f);
                b.AddCone(new Vector3(x, top, 0f), new Vector3(x, top + 0.15f, 0f), 0.03f, 4, Site.Iron, 0.06f);
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
