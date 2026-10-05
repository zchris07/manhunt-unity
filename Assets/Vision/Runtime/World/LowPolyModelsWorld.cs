using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// Living trees, wrecks, machines, fire and the small plants merged into the ground. Same rules as the
    /// rest of <see cref="LowPolyModels"/>: flat triangles, origin at the bottom centre, resolution from
    /// <see cref="PolyBudget"/>.
    /// </summary>
    public static partial class LowPolyModels
    {
        public static class Nature
        {
            public static readonly Color Leaf = new Color(0.24f, 0.33f, 0.17f);
            public static readonly Color LeafLight = new Color(0.32f, 0.40f, 0.20f);
            public static readonly Color Autumn = new Color(0.58f, 0.34f, 0.13f);
            public static readonly Color AutumnLight = new Color(0.66f, 0.50f, 0.17f);
            public static readonly Color Pine = new Color(0.15f, 0.24f, 0.16f);
            public static readonly Color GrassGreen = new Color(0.33f, 0.42f, 0.22f);
            public static readonly Color Straw = new Color(0.60f, 0.53f, 0.33f);
            public static readonly Color Reed = new Color(0.40f, 0.42f, 0.25f);
            public static readonly Color Cattail = new Color(0.30f, 0.20f, 0.12f);
            public static readonly Color Twig = new Color(0.30f, 0.26f, 0.21f);
            public static readonly Color Stem = new Color(0.28f, 0.37f, 0.20f);
            public static readonly Color MushroomStem = new Color(0.80f, 0.76f, 0.66f);
            public static readonly Color MushroomCap = new Color(0.46f, 0.30f, 0.19f);
            public static readonly Color MushroomRed = new Color(0.62f, 0.16f, 0.10f);
            public static readonly Color[] Blossoms =
            {
                new Color(0.86f, 0.85f, 0.79f), new Color(0.86f, 0.76f, 0.26f), new Color(0.52f, 0.36f, 0.62f),
            };
        }

        public static class Rust
        {
            public static readonly Color Orange = new Color(0.42f, 0.23f, 0.12f);
            public static readonly Color Dark = new Color(0.20f, 0.13f, 0.09f);
            public static readonly Color Rubber = new Color(0.09f, 0.09f, 0.10f);
            public static readonly Color Glass = new Color(0.12f, 0.15f, 0.16f);
            public static readonly Color Hole = new Color(0.04f, 0.04f, 0.045f);
            public static readonly Color Chrome = new Color(0.40f, 0.40f, 0.38f);
            public static readonly Color Lamp = new Color(0.70f, 0.68f, 0.58f);
            public static readonly Color[] Paint =
            {
                new Color(0.27f, 0.32f, 0.36f),   // faded blue sedan
                new Color(0.55f, 0.52f, 0.44f),   // cream van
                new Color(0.42f, 0.20f, 0.16f),   // red pickup
            };
            public static readonly Color MachineYellow = new Color(0.56f, 0.46f, 0.16f);
            public static readonly Color FuelRed = new Color(0.48f, 0.12f, 0.10f);
        }

        const PolyBudget.Class PlantClass = PolyBudget.Class.Plant;
        const PolyBudget.Class VehicleClass = PolyBudget.Class.Vehicle;

        // ------------------------------------------------------------------ evergreens

        /// <summary>
        /// Evergreen forms: 0 fir, 1 spruce, 2 pine, 3 young fir, 4 dying pine (brown), 5 spiky spruce, 6 black spruce
        /// (5 and 6 are the ragged, spiky family). Every crown starts in the lower third of the trunk and tapers to a
        /// point, and is at most <see cref="CrownWidthRatio"/> of the tree's height wide, so none looks like an umbrella.
        /// </summary>
        public const int ConiferStyles = 7;

        /// <summary>Widest crown (diameter) relative to the tree's height, before per-tree girth scaling.</summary>
        public const float CrownWidthRatio = 0.4f;

        public static Mesh Conifer(System.Random rng, int style = 0)
        {
            var b = new LowPolyMeshBuilder(rng);
            style = Mathf.Abs(style) % ConiferStyles;
            float height = style switch
            {
                1 => b.Range(5.6f, 7.0f),
                2 => b.Range(5.2f, 6.6f),
                3 => b.Range(2.2f, 3.0f),
                4 => b.Range(4.4f, 5.8f),
                5 => b.Range(4.6f, 6.4f),
                6 => b.Range(4.8f, 6.6f),
                _ => b.Range(4.2f, 6.0f),
            };
            if (style >= 5) Spiky(b, height, style == 6);
            else Tiered(b, height, style);
            string name = style switch { 1 => "Spruce", 2 => "Pine", 4 => "Dying Pine", 5 => "Spiky Spruce", 6 => "Black Spruce", _ => "Fir" };
            return b.ToMesh(name);
        }

        /// <summary>Stacked, faceted cones of needles over a trunk (fir, spruce, pine, young fir, dying pine).</summary>
        static void Tiered(LowPolyMeshBuilder b, float height, int style)
        {
            // Crown base (fraction of height), base crown radius (fraction of height) and tier count per style.
            float crownBase = style switch { 2 => 0.3f, 4 => 0.22f, 1 => 0.1f, 3 => 0.08f, _ => 0.14f };
            float radius = style switch { 1 => 0.15f, 2 => 0.16f, 3 => 0.185f, 4 => 0.16f, _ => 0.18f } * height;
            int tiers = style switch { 1 => 6, 2 => 4, 3 => 3, 4 => 4, _ => 5 };
            Color needles = style switch
            {
                1 => new Color(0.13f, 0.21f, 0.15f),
                2 => new Color(0.16f, 0.25f, 0.22f),
                3 => new Color(0.20f, 0.30f, 0.18f),
                4 => new Color(0.33f, 0.25f, 0.16f),
                _ => new Color(0.15f, 0.24f, 0.16f),
            };
            Color bark = style == 4 ? new Color(0.30f, 0.28f, 0.26f) : Palette.Bark;
            float trunkTop = height * (style == 2 ? 0.75f : 0.6f);
            b.AddFrustum(Vector3.zero, new Vector3(0f, trunkTop, 0f), 0.04f * height, 0.02f * height, PolyBudget.Sides(0.2f, TreeClass, 5), bark, 0.1f);
            if (style == 2 || style == 4)
            {
                // Stubs of lost lower branches below the crown.
                for (int i = 0; i < 4; i++)
                {
                    float y = b.Range(0.4f, crownBase * height), a = b.Next() * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(a), b.Range(-0.1f, 0.3f), Mathf.Sin(a)).normalized;
                    b.AddTube(new[] { new Vector3(0f, y, 0f), new Vector3(0f, y, 0f) + dir * b.Range(0.25f, 0.5f) }, new[] { 0.035f, 0f }, 3, bark, 0.1f, 0f, 0f, null, false, false);
                }
            }
            float y0 = crownBase * height, span = height - y0;
            for (int i = 0; i < tiers; i++)
            {
                float t = tiers == 1 ? 0f : i / (float)(tiers - 1);
                // Tiers shrink steadily toward the tip, so the outline is a narrow spire.
                float r = radius * Mathf.Lerp(1f, 0.28f, t) * b.Range(0.9f, 1.05f);
                float bottom = y0 + span * (i / (float)(tiers + 0.6f));
                float top = i == tiers - 1 ? height : Mathf.Min(height, bottom + span * 0.42f);
                var offset = new Vector3(b.Range(-0.04f, 0.04f), 0f, b.Range(-0.04f, 0.04f));
                Color c = style == 4 && b.Next() < 0.35f ? needles * 0.75f : needles;
                b.AddCone(offset + Vector3.up * bottom, Vector3.up * top, r, PolyBudget.Sides(r, TreeClass, 6, 9), b.Jitter(c, 0.08f), 0.12f, true);
            }
        }

        /// <summary>
        /// Ragged, spiky evergreen: a thin trunk bristling with sharp needle-spikes jutting out at angles, sparse and uneven,
        /// longest low down and shortest at the tip. The black spruce form is narrower with a dense clump near the top.
        /// </summary>
        static void Spiky(LowPolyMeshBuilder b, float height, bool black)
        {
            Color bark = new Color(0.22f, 0.19f, 0.16f);
            Color needles = black ? new Color(0.11f, 0.17f, 0.13f) : new Color(0.14f, 0.22f, 0.15f);
            b.AddFrustum(Vector3.zero, new Vector3(0f, height * 0.97f, 0f), 0.035f * height, 0.008f * height, 5, bark, 0.1f);
            float crownBase = (black ? 0.2f : 0.12f) * height;
            float maxReach = (black ? 0.13f : 0.18f) * height;
            int levels = black ? 16 : 14;
            for (int l = 0; l < levels; l++)
            {
                float t = l / (float)(levels - 1);
                float y = Mathf.Lerp(crownBase, height * 0.93f, t);
                float reach = maxReach * Mathf.Lerp(1f, 0.2f, t);
                if (black && t > 0.72f) reach = maxReach * 0.55f;   // the club-shaped top
                int spikes = black ? 6 : 7 + b.Rng.Next(3);
                for (int k = 0; k < spikes; k++)
                {
                    if (b.Next() < (black ? 0.22f : 0.15f)) continue;   // gaps: ragged, not regular
                    float a = (l * 61f + k * 360f / spikes + b.Range(-25f, 25f)) * Mathf.Deg2Rad;
                    var outward = new Vector3(Mathf.Cos(a), b.Range(-0.35f, 0.25f), Mathf.Sin(a)).normalized;
                    float len = reach * b.Range(0.6f, 1.25f);
                    var root = new Vector3(0f, y, 0f);
                    // A spike is a thin, sharp cone, a few needles thick at its root.
                    b.AddCone(root, root + outward * len, Mathf.Max(0.07f, len * 0.24f), 4, b.Jitter(needles, 0.1f), 0.12f, true);
                }
            }
            b.AddCone(new Vector3(0f, height * 0.85f, 0f), new Vector3(0f, height, 0f), maxReach * 0.22f, 4, needles, 0.1f, true);
        }

        // ------------------------------------------------------------------ wrecks and machines

        /// <summary>Footprint and height (x = width, y = height, z = length) of a car wreck kind (0 sedan, 1 van, 2 pickup).</summary>
        public static Vector3 CarSize(int kind) => kind switch
        {
            1 => new Vector3(1.95f, 2.05f, 4.8f),
            2 => new Vector3(1.85f, 1.65f, 5.0f),
            _ => new Vector3(1.8f, 1.45f, 4.3f),
        };

        /// <summary>A rotated box (centre, size, rotation) as a hexahedron.</summary>
        static void RotBox(LowPolyMeshBuilder b, Vector3 center, Vector3 size, Quaternion rot, Color color, float jitter = 0.06f)
        {
            Vector3 h = size * 0.5f;
            var c = new Vector3[8];
            int k = 0;
            foreach (float y in new[] { -h.y, h.y })
            {
                c[k++] = center + rot * new Vector3(-h.x, y, -h.z);
                c[k++] = center + rot * new Vector3(h.x, y, -h.z);
                c[k++] = center + rot * new Vector3(h.x, y, h.z);
                c[k++] = center + rot * new Vector3(-h.x, y, h.z);
            }
            b.AddHexahedron(c, color, jitter);
        }

        /// <summary>A quad laid just outside a face (windows, rust patches): corners a, b, c, d pushed along the face normal.</summary>
        static void Panel(LowPolyMeshBuilder b, Vector3 a, Vector3 bb, Vector3 c, Vector3 d, Vector3 outward, Color color)
        {
            Vector3 o = outward.normalized * 0.012f;
            Vector3 inside = (a + bb + c + d) * 0.25f - outward;
            b.AddQuadOutward(a + o, bb + o, c + o, d + o, color, 0.05f, inside);
        }

        /// <summary>
        /// A rusted, abandoned car (0 sedan, 1 van, 2 pickup), +Z forward: faded paint with rust patches, broken or
        /// missing windows, a wheel missing or flat so the body sags to that corner, and sometimes an open hood.
        /// </summary>
        public static Mesh Car(System.Random rng, int kind)
        {
            var b = new LowPolyMeshBuilder(rng);
            Vector3 size = CarSize(kind);
            float w = size.x * 0.5f, l = size.z * 0.5f;
            Color paint = b.Jitter(Rust.Paint[kind % Rust.Paint.Length], 0.08f);
            float wheelR = kind == 1 ? 0.36f : 0.34f, wheelW = 0.24f;
            int wheelSides = PolyBudget.Sides(wheelR, VehicleClass, 6, 8);
            int missing = b.Rng.Next(4), flat = (missing + 1 + b.Rng.Next(3)) % 4;
            var wheelPos = new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, -1) };
            float axle = l - (kind == 1 ? 0.85f : 0.8f);

            // Sag: the corner over a missing wheel drops the most, a flat tyre a little.
            float Sag(float sx, float sz)
            {
                float s = 0f;
                for (int i = 0; i < 4; i++)
                {
                    float near = Mathf.Clamp01(1f - (Mathf.Abs(sx - wheelPos[i].x) + Mathf.Abs(sz - wheelPos[i].y)) * 0.5f);
                    if (i == missing) s -= 0.2f * near;
                    else if (i == flat) s -= 0.08f * near;
                }
                return s;
            }
            Vector3 P(float x, float y, float z) => new Vector3(x, y + Sag(x / w, z / l), z);

            // Lower body.
            float y0 = 0.28f, y1 = kind == 1 ? 1.0f : 0.86f;
            var body = new[]
            {
                P(-w, y0, -l), P(w, y0, -l), P(w, y0, l), P(-w, y0, l),
                P(-w, y1, -l + 0.05f), P(w, y1, -l + 0.05f), P(w, y1, l - 0.12f), P(-w, y1, l - 0.12f),
            };
            b.AddHexahedron(body, paint, 0.07f);

            // Cabin (sedan and pickup: sloped glasshouse; van: tall box to the roof).
            float cabFront = kind == 1 ? l - 0.6f : (kind == 2 ? 0.55f : 0.75f);
            float cabBack = kind == 1 ? -l + 0.1f : (kind == 2 ? -0.45f : -1.25f);
            float roofY = size.y;
            float inset = kind == 1 ? 0.04f : 0.14f;
            var cab = new[]
            {
                P(-w + 0.04f, y1, cabBack), P(w - 0.04f, y1, cabBack), P(w - 0.04f, y1, cabFront), P(-w + 0.04f, y1, cabFront),
                P(-w + inset, roofY, cabBack + (kind == 1 ? 0f : 0.3f)), P(w - inset, roofY, cabBack + (kind == 1 ? 0f : 0.3f)),
                P(w - inset, roofY, cabFront - (kind == 1 ? 0.35f : 0.55f)), P(-w + inset, roofY, cabFront - (kind == 1 ? 0.35f : 0.55f)),
            };
            b.AddHexahedron(cab, paint, 0.06f);

            // Windows on the cabin's sides, front and back: glass, or a dark hole where it is smashed.
            Vector3 Mix(Vector3 a, Vector3 c, float t) => Vector3.Lerp(a, c, t);
            void Window(Vector3 a, Vector3 bb, Vector3 c, Vector3 d, Vector3 outward)
            {
                Vector3 m = (a + bb + c + d) * 0.25f;
                Color col = b.Next() < 0.45f ? Rust.Hole : b.Jitter(Rust.Glass, 0.15f);
                Panel(b, Mix(a, m, 0.18f), Mix(bb, m, 0.18f), Mix(c, m, 0.18f), Mix(d, m, 0.18f), outward, col);
            }
            Window(cab[0], cab[3], cab[7], cab[4], Vector3.left);
            Window(cab[1], cab[2], cab[6], cab[5], Vector3.right);
            Window(cab[3], cab[2], cab[6], cab[7], Vector3.Cross(cab[6] - cab[3], cab[2] - cab[3]));
            Window(cab[0], cab[1], cab[5], cab[4], Vector3.Cross(cab[1] - cab[0], cab[4] - cab[0]));

            if (kind == 2)
            {
                // Pickup bed: floor and three low walls, tailgate gone.
                float bedY = y1 + 0.02f, bedTop = y1 + 0.42f, bedFront = cabBack - 0.05f, bedBack = -l + 0.08f;
                RotBox(b, P(0f, bedY + 0.02f, (bedFront + bedBack) * 0.5f), new Vector3(size.x - 0.1f, 0.05f, bedFront - bedBack), Quaternion.identity, Rust.Dark);
                foreach (float sx in new[] { -1f, 1f })
                    RotBox(b, P(sx * (w - 0.06f), (bedY + bedTop) * 0.5f, (bedFront + bedBack) * 0.5f), new Vector3(0.08f, bedTop - bedY, bedFront - bedBack), Quaternion.identity, paint);
                RotBox(b, P(0f, (bedY + bedTop) * 0.5f, bedFront), new Vector3(size.x - 0.1f, bedTop - bedY, 0.08f), Quaternion.identity, paint);
            }

            // Hood: shut, or propped open on the sedan and pickup.
            if (kind != 1)
            {
                float hoodBack = cabFront + 0.02f, hoodLen = l - 0.12f - hoodBack;
                if (b.Next() < 0.5f)
                {
                    Quaternion open = Quaternion.Euler(-b.Range(35f, 60f), 0f, 0f);
                    Vector3 hinge = P(0f, y1 + 0.02f, hoodBack);
                    RotBox(b, hinge + open * new Vector3(0f, 0.02f, hoodLen * 0.5f), new Vector3(size.x - 0.16f, 0.04f, hoodLen), open, paint);
                    RotBox(b, P(0f, y1 - 0.1f, hoodBack + hoodLen * 0.5f), new Vector3(size.x * 0.55f, 0.22f, hoodLen * 0.6f), Quaternion.identity, Rust.Dark);   // engine
                }
            }

            // Rust patches on the flanks and roof.
            int patches = 3 + b.Rng.Next(3);
            for (int i = 0; i < patches; i++)
            {
                float side = b.Next() < 0.5f ? -1f : 1f;
                float z = b.Range(-l + 0.3f, l - 0.5f), y = b.Range(y0 + 0.08f, y1 - 0.15f), pw = b.Range(0.25f, 0.6f), ph = b.Range(0.12f, 0.3f);
                Color c = b.Next() < 0.6f ? Rust.Orange : Rust.Dark;
                Panel(b, P(side * w, y, z), P(side * w, y, z + pw), P(side * w, y + ph, z + pw), P(side * w, y + ph, z), new Vector3(side, 0f, 0f), c);
            }
            Vector3 Roof(float u, float v) => Vector3.Lerp(Vector3.Lerp(cab[4], cab[5], u), Vector3.Lerp(cab[7], cab[6], u), v);
            float ru = b.Range(0.1f, 0.5f), rv = b.Range(0.1f, 0.5f);
            Panel(b, Roof(ru, rv), Roof(ru + 0.35f, rv), Roof(ru + 0.35f, rv + 0.4f), Roof(ru, rv + 0.4f), Vector3.up, Rust.Orange);

            // Bumpers and lights.
            RotBox(b, P(0f, y0 + 0.12f, l), new Vector3(size.x, 0.14f, 0.1f), Quaternion.identity, Rust.Chrome);
            RotBox(b, P(0f, y0 + 0.12f, -l), new Vector3(size.x, 0.14f, 0.1f), Quaternion.identity, Rust.Chrome);
            foreach (float sx in new[] { -0.7f, 0.7f })
                RotBox(b, P(sx * w, y1 - 0.18f, l - 0.08f), new Vector3(0.28f, 0.12f, 0.06f), Quaternion.identity, b.Next() < 0.5f ? Rust.Lamp : Rust.Hole);

            // Wheels: axle along X; one missing (a brake drum shows), one flat.
            for (int i = 0; i < 4; i++)
            {
                Vector2 wp = wheelPos[i];
                float x = wp.x * (w - wheelW * 0.4f), z = wp.y * axle;
                if (i == missing)
                {
                    b.AddFrustum(new Vector3(x - wp.x * 0.05f, 0.2f, z), new Vector3(x + wp.x * 0.05f, 0.2f, z), 0.16f, 0.16f, 6, Rust.Dark, 0.1f);
                    continue;
                }
                float r = i == flat ? wheelR * 0.82f : wheelR;
                float cy = r;   // a flat tyre is smaller, and the body above it sags
                b.AddFrustum(new Vector3(x - wheelW * 0.5f, cy, z), new Vector3(x + wheelW * 0.5f, cy, z), r, r, wheelSides, Rust.Rubber, 0.08f);
                b.AddFrustum(new Vector3(x + wp.x * (wheelW * 0.5f), cy, z), new Vector3(x + wp.x * (wheelW * 0.5f + 0.01f), cy, z), r * 0.5f, r * 0.5f, wheelSides, Rust.Chrome, 0.1f);
            }
            return b.ToMesh(kind == 1 ? "Van Wreck" : kind == 2 ? "Pickup Wreck" : "Sedan Wreck");
        }

        /// <summary>Footprint of the generator (x, z) and its height.</summary>
        public static readonly Vector3 GeneratorSize = new Vector3(1.3f, 1.05f, 0.9f);

        /// <summary>A portable generator on skids: yellow casing, engine block, exhaust stack, control panel and a fuel can.</summary>
        public static Mesh Generator(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            Color yellow = b.Jitter(Rust.MachineYellow, 0.08f);
            foreach (float z in new[] { -0.32f, 0.32f })
                b.AddBox(new Vector3(-0.62f, 0f, z - 0.05f), new Vector3(1.24f, 0.1f, 0.1f), Palette.Iron, 0.08f);
            b.AddBox(new Vector3(-0.55f, 0.1f, -0.36f), new Vector3(1.1f, 0.62f, 0.72f), yellow, 0.07f);
            b.AddBox(new Vector3(-0.42f, 0.72f, -0.26f), new Vector3(0.7f, 0.18f, 0.52f), Rust.Dark, 0.08f);
            b.AddBox(new Vector3(0.3f, 0.3f, 0.36f), new Vector3(0.22f, 0.3f, 0.04f), Palette.Iron, 0.08f);   // control panel
            b.AddTube(new[] { new Vector3(0.36f, 0.72f, -0.2f), new Vector3(0.36f, 0.98f, -0.2f), new Vector3(0.48f, 1.05f, -0.2f) },
                      new[] { 0.045f, 0.045f, 0.04f }, 5, Palette.Iron, 0.06f);
            foreach (float x in new[] { -0.5f, 0.5f })
                foreach (float z in new[] { -0.33f, 0.33f })
                    b.AddBox(new Vector3(x - 0.04f, 0.1f, z - 0.04f), new Vector3(0.08f, 0.66f, 0.08f), Palette.Iron, 0.06f);   // frame posts
            // Rust streaks on the casing.
            for (int i = 0; i < 3; i++)
            {
                float x = b.Range(-0.45f, 0.4f);
                Panel(b, new Vector3(x, 0.15f, 0.36f), new Vector3(x + 0.1f, 0.15f, 0.36f), new Vector3(x + 0.08f, b.Range(0.4f, 0.6f), 0.36f),
                      new Vector3(x + 0.01f, b.Range(0.4f, 0.6f), 0.36f), Vector3.forward, Rust.Orange);
            }
            // Fuel can beside it.
            b.AddBox(new Vector3(0.68f, 0f, -0.05f), new Vector3(0.2f, 0.36f, 0.3f), Rust.FuelRed, 0.08f);
            b.AddBox(new Vector3(0.74f, 0.36f, 0.02f), new Vector3(0.08f, 0.06f, 0.16f), Palette.Iron, 0.06f);
            return b.ToMesh("Generator");
        }

        // ------------------------------------------------------------------ fire

        /// <summary>An oil drum with rusty rims, glowing embers inside its open top. Flames come from <see cref="Flames"/>.</summary>
        public static Mesh BurningBarrel(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            int sides = PolyBudget.Sides(0.3f, PropClass, 6, 9);
            b.AddFrustum(Vector3.zero, new Vector3(0f, 0.86f, 0f), 0.3f, 0.29f, sides, b.Jitter(Rust.Orange, 0.1f), 0.12f, 0.03f, true, false);
            foreach (float y in new[] { 0.06f, 0.42f, 0.8f })
                b.AddFrustum(new Vector3(0f, y, 0f), new Vector3(0f, y + 0.05f, 0f), 0.315f, 0.31f, sides, Rust.Dark, 0.08f, 0f, false, false);
            b.AddFrustum(new Vector3(0f, 0.78f, 0f), new Vector3(0f, 0.8f, 0f), 0.27f, 0.27f, sides, Palette.Ember, 0.15f, 0f, false, true);
            for (int i = 0; i < 3; i++)
            {
                float a = b.Next() * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a) * 0.12f, 0.8f, Mathf.Sin(a) * 0.12f);
                b.AddTube(new[] { p, p + new Vector3(b.Range(-0.15f, 0.15f), 0.12f, b.Range(-0.15f, 0.15f)) }, new[] { 0.04f, 0.035f }, 4, Palette.BarkDark, 0.1f);
            }
            return b.ToMesh("Burning Barrel");
        }

        /// <summary>
        /// Separate flame tongues for a fire (each with its base at its own origin, so it can sway and stretch
        /// about its base): one tall central flame and a few smaller ones, with where to place each.
        /// </summary>
        public static List<(Mesh mesh, Vector3 position)> Flames(System.Random rng, float spread, float height, int count)
        {
            var flames = new List<(Mesh, Vector3)>();
            var r = new LowPolyMeshBuilder(rng);
            for (int i = 0; i < count; i++)
            {
                var b = new LowPolyMeshBuilder(rng);
                float a = r.Next() * Mathf.PI * 2f, d = i == 0 ? 0f : r.Range(spread * 0.4f, spread);
                float h = i == 0 ? height : height * r.Range(0.45f, 0.75f);
                float radius = (i == 0 ? 0.11f : 0.08f) * Mathf.Max(0.6f, spread / 0.14f);
                b.AddCone(Vector3.zero, new Vector3(r.Range(-0.03f, 0.03f), h, r.Range(-0.03f, 0.03f)), radius, PolyBudget.Sides(radius, PropClass, 4, 5),
                          i == 0 ? Palette.Flame : Palette.Ember, 0.1f, false);
                if (i == 0) b.AddCone(Vector3.up * 0.02f, new Vector3(0f, h * 0.6f, 0f), radius * 0.55f, 4, new Color(1f, 0.93f, 0.6f), 0.05f, false);
                flames.Add((b.ToMesh($"Flame {i}"), new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d)));
            }
            return flames;
        }

        /// <summary>A tiny glowing ember (a jittered tetra-ish blob) for the rising sparks.</summary>
        public static Mesh Ember(System.Random rng)
        {
            var b = new LowPolyMeshBuilder(rng);
            b.AddBlob(Vector3.zero, Vector3.one * 0.022f, 0, 0.25f, _ => Palette.Flame);
            return b.ToMesh("Ember");
        }

        // ------------------------------------------------------------------ supplies

        /// <summary>The original's supplies, lying on the ground (they are drawn 1.6x larger so they read from above).</summary>
        public static Mesh Item(System.Random rng, Vision.Player.ItemType item)
        {
            var b = new LowPolyMeshBuilder(rng);
            switch (item)
            {
                case Vision.Player.ItemType.Bottle:
                {
                    // An empty glass bottle on its side.
                    var glass = new Color(0.22f, 0.40f, 0.22f);
                    b.AddFrustum(new Vector3(-0.1f, 0.045f, 0f), new Vector3(0.06f, 0.045f, 0f), 0.045f, 0.045f, 7, glass, 0.05f);
                    b.AddFrustum(new Vector3(0.06f, 0.045f, 0f), new Vector3(0.1f, 0.045f, 0f), 0.045f, 0.018f, 7, glass, 0.05f);
                    b.AddFrustum(new Vector3(0.1f, 0.045f, 0f), new Vector3(0.16f, 0.045f, 0f), 0.018f, 0.018f, 5, glass * 1.1f, 0.05f);
                    b.AddFrustum(new Vector3(-0.04f, 0.045f, 0f), new Vector3(0.03f, 0.045f, 0f), 0.047f, 0.047f, 7, new Color(0.70f, 0.66f, 0.52f), 0.05f);
                    break;
                }
                case Vision.Player.ItemType.Book:
                    // The Grapes of Wrath: a worn paperback, cover up.
                    b.AddBox(new Vector3(-0.07f, 0f, -0.1f), new Vector3(0.14f, 0.035f, 0.2f), new Color(0.80f, 0.76f, 0.64f), 0.03f);
                    b.AddBox(new Vector3(-0.072f, 0.035f, -0.102f), new Vector3(0.144f, 0.006f, 0.204f), new Color(0.62f, 0.44f, 0.20f), 0.05f);
                    b.AddBox(new Vector3(-0.05f, 0.041f, 0.02f), new Vector3(0.1f, 0.002f, 0.05f), new Color(0.30f, 0.16f, 0.10f), 0.05f);
                    b.AddBox(new Vector3(-0.072f, 0f, -0.102f), new Vector3(0.012f, 0.041f, 0.204f), new Color(0.45f, 0.30f, 0.14f), 0.05f);
                    break;
                case Vision.Player.ItemType.Goggles:
                    // Night vision goggles: two tubes on a strap.
                    foreach (float x in new[] { -0.045f, 0.045f })
                    {
                        b.AddFrustum(new Vector3(x, 0.04f, -0.05f), new Vector3(x, 0.04f, 0.07f), 0.035f, 0.03f, 7, new Color(0.14f, 0.16f, 0.14f), 0.05f);
                        b.AddFrustum(new Vector3(x, 0.04f, 0.07f), new Vector3(x, 0.04f, 0.075f), 0.026f, 0.026f, 7, new Color(0.25f, 0.70f, 0.30f), 0.05f);
                    }
                    b.AddBox(new Vector3(-0.08f, 0.02f, -0.07f), new Vector3(0.16f, 0.04f, 0.04f), new Color(0.2f, 0.2f, 0.19f), 0.05f);
                    b.AddBox(new Vector3(-0.12f, 0.005f, -0.11f), new Vector3(0.24f, 0.01f, 0.03f), new Color(0.12f, 0.12f, 0.12f), 0.05f);
                    break;
                case Vision.Player.ItemType.Shotgun:
                    // A pump shotgun lying flat: stock, receiver, barrel and the pump.
                    b.AddBox(new Vector3(-0.32f, 0.01f, -0.035f), new Vector3(0.2f, 0.035f, 0.07f), new Color(0.40f, 0.26f, 0.14f), 0.06f);
                    b.AddBox(new Vector3(-0.12f, 0.012f, -0.025f), new Vector3(0.14f, 0.04f, 0.05f), new Color(0.16f, 0.16f, 0.17f), 0.04f);
                    b.AddFrustum(new Vector3(0.02f, 0.035f, 0f), new Vector3(0.38f, 0.035f, 0f), 0.014f, 0.014f, 6, new Color(0.18f, 0.18f, 0.19f), 0.04f);
                    b.AddFrustum(new Vector3(0.06f, 0.02f, 0f), new Vector3(0.2f, 0.02f, 0f), 0.022f, 0.022f, 6, new Color(0.36f, 0.24f, 0.13f), 0.05f);
                    break;
                case Vision.Player.ItemType.DoctorPepper:
                    b.AddFrustum(Vector3.zero, new Vector3(0f, 0.12f, 0f), 0.033f, 0.033f, 8, new Color(0.45f, 0.06f, 0.10f), 0.04f);
                    b.AddFrustum(new Vector3(0f, 0.04f, 0f), new Vector3(0f, 0.08f, 0f), 0.034f, 0.034f, 8, new Color(0.85f, 0.82f, 0.78f), 0.03f);
                    b.AddFrustum(new Vector3(0f, 0.12f, 0f), new Vector3(0f, 0.13f, 0f), 0.033f, 0.026f, 8, new Color(0.62f, 0.62f, 0.62f), 0.03f);
                    break;
                case Vision.Player.ItemType.Trap:
                    // Galaxy gas trap: a squat canister with a purple band and a nozzle.
                    b.AddFrustum(Vector3.zero, new Vector3(0f, 0.1f, 0f), 0.065f, 0.06f, 8, new Color(0.22f, 0.22f, 0.25f), 0.05f);
                    b.AddFrustum(new Vector3(0f, 0.03f, 0f), new Vector3(0f, 0.07f, 0f), 0.067f, 0.067f, 8, new Color(0.48f, 0.26f, 0.70f), 0.06f);
                    b.AddFrustum(new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0.14f, 0f), 0.015f, 0.01f, 5, new Color(0.6f, 0.6f, 0.62f), 0.04f);
                    break;
                case Vision.Player.ItemType.Confit:
                    // Duck confit: a wide tin with a pale label.
                    b.AddFrustum(Vector3.zero, new Vector3(0f, 0.07f, 0f), 0.075f, 0.075f, 9, new Color(0.62f, 0.60f, 0.56f), 0.04f);
                    b.AddFrustum(new Vector3(0f, 0.012f, 0f), new Vector3(0f, 0.058f, 0f), 0.077f, 0.077f, 9, new Color(0.80f, 0.62f, 0.34f), 0.05f);
                    b.AddFrustum(new Vector3(0f, 0.07f, 0f), new Vector3(0f, 0.075f, 0f), 0.07f, 0.07f, 9, new Color(0.55f, 0.55f, 0.52f), 0.03f);
                    break;
                case Vision.Player.ItemType.MrBeastBar:
                    // A chocolate bar in a bright blue wrapper.
                    b.AddBox(new Vector3(-0.09f, 0f, -0.04f), new Vector3(0.18f, 0.025f, 0.08f), new Color(0.22f, 0.48f, 0.88f), 0.04f);
                    b.AddBox(new Vector3(-0.1f, 0.003f, -0.042f), new Vector3(0.012f, 0.02f, 0.084f), new Color(0.85f, 0.85f, 0.88f), 0.03f);
                    b.AddBox(new Vector3(0.088f, 0.003f, -0.042f), new Vector3(0.012f, 0.02f, 0.084f), new Color(0.85f, 0.85f, 0.88f), 0.03f);
                    b.AddBox(new Vector3(-0.04f, 0.025f, -0.02f), new Vector3(0.08f, 0.003f, 0.04f), new Color(0.95f, 0.92f, 0.85f), 0.03f);
                    break;
                default:
                    // Mini shield: a small blue potion jug with a white cap.
                    b.AddFrustum(Vector3.zero, new Vector3(0f, 0.08f, 0f), 0.045f, 0.05f, 7, new Color(0.30f, 0.60f, 0.95f), 0.05f);
                    b.AddFrustum(new Vector3(0f, 0.08f, 0f), new Vector3(0f, 0.11f, 0f), 0.05f, 0.02f, 7, new Color(0.30f, 0.60f, 0.95f), 0.05f);
                    b.AddFrustum(new Vector3(0f, 0.11f, 0f), new Vector3(0f, 0.135f, 0f), 0.021f, 0.021f, 6, new Color(0.88f, 0.88f, 0.90f), 0.03f);
                    break;
            }
            return b.ToMesh(item.ToString());
        }

        // ------------------------------------------------------------------ small plants (merged into the ground)
        // Written in the plant's own space around the origin; place them with LowPolyMeshBuilder.Transform.

        /// <summary>A low, spiky evergreen shrub (juniper-like cones), or a dry brown one.</summary>
        public static void AddBush(LowPolyMeshBuilder b, float size, bool dry)
        {
            int cones = 4 + b.Rng.Next(4);
            Color baseColor = dry ? new Color(0.40f, 0.33f, 0.20f) : (b.Next() < 0.5f ? Nature.Pine : new Color(0.20f, 0.29f, 0.18f));
            for (int i = 0; i < cones; i++)
            {
                float a = b.Next() * Mathf.PI * 2f, r = i == 0 ? 0f : size * b.Range(0.25f, 0.55f);
                var root = new Vector3(Mathf.Cos(a) * r, -0.02f, Mathf.Sin(a) * r);
                float h = size * (i == 0 ? 1.1f : b.Range(0.6f, 0.95f));
                var tip = root + new Vector3(root.x * 0.4f + b.Range(-0.08f, 0.08f), h, root.z * 0.4f + b.Range(-0.08f, 0.08f));
                b.AddCone(root, tip, size * b.Range(0.3f, 0.45f), 5, b.Jitter(baseColor, 0.12f), 0.1f, false);
            }
        }

        public static void AddFern(LowPolyMeshBuilder b, float size)
        {
            int fronds = 6 + b.Rng.Next(3);
            for (int i = 0; i < fronds; i++)
            {
                float a = (i + b.Next() * 0.4f) / fronds * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var side = new Vector3(-dir.z, 0f, dir.x);
                float len = size * b.Range(0.8f, 1.1f);
                Vector3 p0 = Vector3.up * 0.02f, p1 = dir * len * 0.5f + Vector3.up * len * 0.45f, p2 = dir * len + Vector3.up * len * 0.2f;
                float wd = len * 0.16f;
                Color c = b.Jitter(Nature.LeafLight, 0.12f);
                b.AddDoubleSided(p0, p1 + side * wd, p1 - side * wd, c);
                b.AddDoubleSided(p1 + side * wd, p2, p1 - side * wd, b.Jitter(c, 0.06f));
            }
        }

        public static void AddTallGrass(LowPolyMeshBuilder b, float height, bool dead)
        {
            int blades = 7 + b.Rng.Next(5);
            Color c = dead ? Nature.Straw : Nature.GrassGreen;
            for (int i = 0; i < blades; i++)
            {
                var root = new Vector3(b.Range(-0.18f, 0.18f), -0.02f, b.Range(-0.18f, 0.18f));
                float h = height * b.Range(0.6f, 1.05f);
                var tip = root + new Vector3(root.x * 1.5f + b.Range(-0.1f, 0.1f), h, root.z * 1.5f + b.Range(-0.1f, 0.1f));
                b.AddCone(root, tip, 0.025f, 3, b.Jitter(c, 0.15f), 0.05f, false);
            }
        }

        public static void AddReeds(LowPolyMeshBuilder b, float height)
        {
            int stalks = 6 + b.Rng.Next(5);
            for (int i = 0; i < stalks; i++)
            {
                var root = new Vector3(b.Range(-0.25f, 0.25f), -0.02f, b.Range(-0.25f, 0.25f));
                float h = height * b.Range(0.7f, 1.1f);
                var tip = root + new Vector3(b.Range(-0.08f, 0.08f), h, b.Range(-0.08f, 0.08f));
                b.AddCone(root, tip, 0.02f, 3, b.Jitter(Nature.Reed, 0.12f), 0.05f, false);
                if (b.Next() < 0.45f)
                {
                    Vector3 head = Vector3.Lerp(root, tip, 0.72f);
                    b.AddFrustum(head, head + (tip - root).normalized * 0.13f, 0.03f, 0.03f, 4, Nature.Cattail, 0.08f);
                }
            }
        }

        public static void AddDeadShrub(LowPolyMeshBuilder b, float size)
        {
            int twigs = 5 + b.Rng.Next(4);
            for (int i = 0; i < twigs; i++)
            {
                float a = b.Next() * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a) * 0.6f, 1f, Mathf.Sin(a) * 0.6f).normalized;
                Vector3 mid = dir * size * 0.55f + new Vector3(b.Range(-0.1f, 0.1f), 0f, b.Range(-0.1f, 0.1f));
                Vector3 end = mid + (dir + new Vector3(b.Range(-0.5f, 0.5f), 0.2f, b.Range(-0.5f, 0.5f))).normalized * size * 0.5f;
                b.AddTube(new[] { Vector3.zero, mid, end }, new[] { 0.025f, 0.015f, 0f }, 3, b.Jitter(Nature.Twig, 0.15f), 0.08f, 0f, 0f, null, false, false);
            }
        }

        public static void AddFlowers(LowPolyMeshBuilder b)
        {
            Color blossom = Nature.Blossoms[b.Rng.Next(Nature.Blossoms.Length)];
            int stems = 5 + b.Rng.Next(5);
            for (int i = 0; i < stems; i++)
            {
                var root = new Vector3(b.Range(-0.3f, 0.3f), -0.01f, b.Range(-0.3f, 0.3f));
                var top = root + new Vector3(b.Range(-0.04f, 0.04f), b.Range(0.18f, 0.34f), b.Range(-0.04f, 0.04f));
                b.AddCone(root, top, 0.012f, 3, Nature.Stem, 0.08f, false);
                b.AddBlob(top, Vector3.one * b.Range(0.035f, 0.05f), 0, 0.2f, _ => b.Jitter(blossom, 0.1f));
            }
        }

        public static void AddMushrooms(LowPolyMeshBuilder b)
        {
            bool red = b.Next() < 0.3f;
            int count = 3 + b.Rng.Next(3);
            for (int i = 0; i < count; i++)
            {
                var root = new Vector3(b.Range(-0.15f, 0.15f), 0f, b.Range(-0.15f, 0.15f));
                float h = b.Range(0.07f, 0.14f), r = b.Range(0.05f, 0.09f);
                b.AddFrustum(root, root + Vector3.up * h, r * 0.35f, r * 0.3f, 4, Nature.MushroomStem, 0.06f);
                b.AddCone(root + Vector3.up * (h - 0.01f), root + Vector3.up * (h + r * 0.55f), r, 5, b.Jitter(red ? Nature.MushroomRed : Nature.MushroomCap, 0.1f), 0.08f, true);
            }
        }
    }
}
