using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// A ruin's stone floor (<see cref="RuinPlan.Floor"/>), laid within its walls stretch by stretch the way a mason lays
    /// one: a kerb course of narrow stones round each room (down both sides of a hallway), and inside it flagstones of
    /// mixed lengths in staggered courses, square tiles in a grid, a chequer of pale and dark tiles framed by a dark band
    /// (a hall), or a hallway's runner of slabs laid across it; doorways bridged by a worn threshold stone. Every stone
    /// stands a little proud of the grout with a chamfered edge, a little uneven (now and then sunk and tipped), some
    /// cracked in two, some with a corner chipped off, the odd one gone (bare earth and grit where it was). Over it all
    /// the discolouring of a floor left to the weather, shaded across each stone: patchy damp, darker toward the walls;
    /// a brownish grime washed over it; moss and algae creeping in from them (more where it's open to the sky); white
    /// salts bloomed along the walls' feet; worn paler down the middle of a hallway; the odd stone stained right through;
    /// stains (rust, a fire's soot, old blood, a water mark) fading out at their edges; fine mottling; lichen spots on a
    /// few stones; grit and chips of stone and brick lying about, most of them by the walls. The material's own grain
    /// (Vision/LowPoly with _VISION_STONE) adds texture inside each stone.
    /// Everything is in the ruin's own frame, metres: x and z the plan's, y up from its base.
    /// </summary>
    public static class RuinFloors
    {
        /// <summary>The stones' tops and the grout's over the ground under them.</summary>
        public const float StoneTop = 0.016f, GroutTop = 0.004f;
        /// <summary>The joint between two stones, and the chamfer round each stone's top.</summary>
        public const float Joint = 0.012f, Bevel = 0.013f;
        /// <summary>How far in from a wall's line the floor starts (tucked a little under the wall's face).</summary>
        public const float WallInset = LowPolyModels.WallThickness * 0.5f - 0.02f;
        /// <summary>The kerb course's width.</summary>
        public const float Kerb = 0.2f;

        /// <summary>Stone tones, one set to a stretch: grey limestone, warm sandstone, blue-grey slate.</summary>
        static readonly Color[][] Palettes =
        {
            new[] { new Color(0.47f, 0.46f, 0.43f), new Color(0.41f, 0.4f, 0.38f), new Color(0.52f, 0.5f, 0.46f), new Color(0.37f, 0.37f, 0.36f) },
            new[] { new Color(0.5f, 0.45f, 0.38f), new Color(0.45f, 0.4f, 0.33f), new Color(0.55f, 0.5f, 0.42f), new Color(0.42f, 0.38f, 0.33f) },
            new[] { new Color(0.34f, 0.36f, 0.38f), new Color(0.3f, 0.32f, 0.34f), new Color(0.39f, 0.4f, 0.41f), new Color(0.27f, 0.28f, 0.3f) },
        };
        static readonly Color Pale = new Color(0.63f, 0.61f, 0.56f), Dark = new Color(0.22f, 0.23f, 0.25f), KerbTone = new Color(0.33f, 0.32f, 0.31f);
        static readonly Color Grout = new Color(0.2f, 0.19f, 0.17f), Earth = new Color(0.17f, 0.13f, 0.1f);
        static readonly Color Grime = new Color(0.3f, 0.26f, 0.21f), Salt = new Color(0.7f, 0.7f, 0.66f);
        static readonly Color Moss = new Color(0.24f, 0.33f, 0.16f), Worn = new Color(0.6f, 0.58f, 0.54f), Sill = new Color(0.54f, 0.52f, 0.48f);
        static readonly Color Rust = new Color(0.47f, 0.29f, 0.17f), Soot = new Color(0.11f, 0.1f, 0.09f), Blood = new Color(0.24f, 0.07f, 0.05f), Water = new Color(0.25f, 0.25f, 0.24f);
        static readonly Color Lichen = new Color(0.63f, 0.63f, 0.46f), LichenOrange = new Color(0.66f, 0.47f, 0.22f), BrickChip = new Color(0.42f, 0.24f, 0.17f);

        /// <summary>What was laid (for checks).</summary>
        public struct Laid { public int Stones, Kerbs, Cracked, Chipped, Missing, Sunk, Lichen, Stains, Chips, Thresholds, Chequer, Edge; }

        sealed class Mason
        {
            public LowPolyMeshBuilder B;
            public System.Random Rng;
            public float Ground;
            /// <summary>The stone being laid sits on the dirt at the open edge: its sides reach down below it, and it's raised.</summary>
            public float Skirt, EdgeLift;
            public int Seed;
            public readonly List<(Vector2 at, float r, Color c, float k)> Stains = new List<(Vector2, float, Color, float)>();
            public Laid Laid;
            public float R(float lo, float hi) => lo + (float)Rng.NextDouble() * (hi - lo);
            public bool Chance(float p) => Rng.NextDouble() < p;
        }

        /// <summary>The floor's area inside a stretch's walls.</summary>
        public static Rect Inset(Rect r) => Rect.MinMaxRect(r.xMin + WallInset, r.yMin + WallInset, r.xMax - WallInset, r.yMax - WallInset);

        /// <summary>A ruin's whole floor as one mesh, laid on ground <paramref name="ground"/> over its base (its frame).</summary>
        public static Mesh Build(RuinPlan plan, System.Random rng, float ground, out Laid laid)
        {
            var m = new Mason { B = new LowPolyMeshBuilder(rng), Rng = rng, Ground = ground, Seed = rng.Next() };
            // A few stains over the floor: rust from old iron, a fire's soot, old blood, a water mark.
            float area = 0f;
            foreach (FloorZone z in plan.Floor) area += z.Area.width * z.Area.height;
            int stains = Mathf.Max(1, Mathf.RoundToInt(area / 22f)) + rng.Next(2);
            for (int i = 0; i < stains && plan.Floor.Count > 0; i++)
            {
                Rect a = Inset(plan.Floor[rng.Next(plan.Floor.Count)].Area);
                var at = new Vector2(m.R(a.xMin, a.xMax), m.R(a.yMin, a.yMax));
                float roll = m.R(0f, 1f);
                if (roll < 0.35f) m.Stains.Add((at, m.R(0.3f, 0.8f), Rust, m.R(0.35f, 0.6f)));
                else if (roll < 0.6f) m.Stains.Add((at, m.R(0.6f, 1.2f), Soot, m.R(0.45f, 0.75f)));
                else if (roll < 0.8f) m.Stains.Add((at, m.R(0.35f, 0.75f), Blood, m.R(0.5f, 0.8f)));
                else m.Stains.Add((at, m.R(0.5f, 1.1f), Water, m.R(0.3f, 0.5f)));
            }
            m.Laid.Stains = m.Stains.Count;
            foreach (FloorZone z in plan.Floor) Lay(m, z);
            Thresholds(m, plan);
            laid = m.Laid;
            return m.B.ToMesh("Stone Floor Ruin");
        }

        // ------------------------------------------------------------------ discolouring

        static float Noise(int seed, float x, float y, int channel)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = RuinHash.Value(seed, channel, ix, iy), b = RuinHash.Value(seed, channel, ix + 1, iy);
            float c = RuinHash.Value(seed, channel, ix, iy + 1), d = RuinHash.Value(seed, channel, ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>A stone's (or the grout's) colour at a point of the floor, weathered.</summary>
        static Color Discolour(Mason m, FloorZone z, Vector2 p, Color c)
        {
            Rect a = z.Area;
            float edge = Mathf.Min(Mathf.Min(p.x - a.xMin, a.xMax - p.x), Mathf.Min(p.y - a.yMin, a.yMax - p.y));
            float wall = Mathf.Clamp01(1f - edge / 0.9f);
            // Grime: a brownish wash over it, thicker in some places than others.
            float grime = Noise(m.Seed, p.x / 1.1f, p.y / 1.1f, 4) * 0.7f + Noise(m.Seed, p.x / 0.45f, p.y / 0.45f, 5) * 0.3f;
            c = Color.Lerp(c, Grime, Mathf.Clamp01((grime - 0.35f) / 0.5f) * 0.42f);
            // Patchy damp, darker toward the walls (and all over where it's open to the sky).
            float damp = Noise(m.Seed, p.x / 1.7f, p.y / 1.7f, 1);
            float wet = Mathf.Clamp01((damp - 0.35f) / 0.45f) * 0.8f + wall * 0.4f + (z.Exposed ? 0.15f : 0f);
            c = Color.Lerp(c, c * 0.6f, Mathf.Clamp01(wet) * 0.7f);
            // White salts bloomed out of the walls along their feet, here and there.
            float salt = wall * wall * Mathf.Clamp01((Noise(m.Seed, p.x / 0.6f, p.y / 0.6f, 6) - 0.6f) * 3f);
            c = Color.Lerp(c, Salt, salt * 0.45f);
            // Moss and algae creeping in from the walls where it's damp, and in patches where it's open to the sky.
            float moss = wall * wall * Mathf.Clamp01(damp * 1.5f - 0.3f) * (z.Exposed ? 0.85f : 0.5f);
            moss += Mathf.Clamp01((Noise(m.Seed, p.x / 0.8f, p.y / 0.8f, 2) - (z.Exposed ? 0.6f : 0.75f)) * 2.5f) * (z.Exposed ? 0.5f : 0.25f);
            c = Color.Lerp(c, Moss, Mathf.Clamp01(moss));
            // Worn paler down the middle of a hallway.
            if (z.Wear > 0f)
            {
                bool alongX = a.width >= a.height;
                float u = alongX ? Mathf.Abs(p.y - a.center.y) / (a.height * 0.5f) : Mathf.Abs(p.x - a.center.x) / (a.width * 0.5f);
                c = Color.Lerp(c, Worn, z.Wear * Mathf.Clamp01(1f - u * u) * 0.24f);
            }
            // Stains, fading out raggedly at their edges.
            foreach (var (at, r, sc, k) in m.Stains)
            {
                float d = (p - at).magnitude / r;
                if (d >= 1.25f) continue;
                float rag = 0.7f + 0.6f * Noise(m.Seed, p.x / 0.22f, p.y / 0.22f, 7);
                c = Color.Lerp(c, sc, Mathf.Clamp01((1f - d * d) * rag) * k);
            }
            // Fine mottling.
            c *= 0.9f + 0.2f * Noise(m.Seed, p.x / 0.33f, p.y / 0.33f, 3);
            c.a = 1f;
            return c;
        }

        // ------------------------------------------------------------------ stones

        /// <summary>
        /// One stone: its footprint (convex), its top raised over the grout and chamfered round, tipped a little; each
        /// corner of its top shaded by the discolouring there.
        /// </summary>
        static void Stone(Mason m, FloorZone z, List<Vector2> poly, Color tone, float lift, Vector2 tilt)
        {
            int n = poly.Count;
            Vector2 c = Vector2.zero;
            foreach (Vector2 p in poly) c += p;
            c /= n;
            float baseY = m.Ground + GroutTop + 0.001f - m.Skirt, topY = m.Ground + StoneTop + lift;
            var top = new Vector3[n];
            var foot = new Vector3[n];
            var col = new Color[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 q = poly[i], toC = c - q;
                float dist = toC.magnitude;
                Vector2 t = q + toC / Mathf.Max(1e-4f, dist) * Mathf.Min(Bevel * 1.41f, dist * 0.4f);
                // (Tipped, never down into the grout.)
                top[i] = new Vector3(t.x, Mathf.Max(baseY + 0.003f, topY + Vector2.Dot(t - c, tilt)), t.y);
                foot[i] = new Vector3(q.x, baseY, q.y);
                col[i] = Discolour(m, z, t, tone);
            }
            var below = new Vector3(c.x, m.Ground - 1f, c.y);
            for (int i = 1; i + 1 < n; i++) m.B.AddTriangleShaded(top[0], top[i], top[i + 1], col[0], col[i], col[i + 1], below);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Color e = (col[i] + col[j]) * (0.5f * 0.8f);
                e.a = 1f;
                m.B.AddTriangleShaded(foot[i], foot[j], top[j], e, e, e, below);
                m.B.AddTriangleShaded(foot[i], top[j], top[i], e, e, e, below);
            }
        }

        static List<Vector2> Corners(Rect r) => new List<Vector2> { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };

        static Rect Shrink(Rect r, float by) => Rect.MinMaxRect(r.xMin + by, r.yMin + by, r.xMax - by, r.yMax - by);

        /// <summary>A stone of the field, with whatever's befallen it: gone, sunk, cracked, chipped, lichen on it.</summary>
        static void Field(Mason m, FloorZone z, Rect r, Color tone, bool kerb = false)
        {
            bool edge = false;
            if (z.Cut)
            {
                // Along the cut line a stone is kept or left off by where its centre falls against a wobbling line: the last
                // row is a staircase of whole stones that runs along the diagonal, never quite straight.
                Vector2 c = r.center, dir = (z.CutB - z.CutA).normalized;
                float size = Mathf.Max(r.width, r.height), depth = z.Depth(c);
                float wobble = (Noise(m.Seed, Vector2.Dot(c, dir) / 0.8f, 0f, 9) - 0.5f) * 0.4f;
                if (depth + wobble + m.R(-0.4f, 0.4f) * size < 0f) return;
                edge = depth < size * 1.2f + 0.3f;
            }
            m.Skirt = edge ? 0.035f : 0f;
            m.EdgeLift = edge ? 0.008f : 0f;
            if (edge) m.Laid.Edge++;
            FieldBody(m, z, r, tone, kerb);
            m.Skirt = m.EdgeLift = 0f;
        }

        static void FieldBody(Mason m, FloorZone z, Rect r, Color tone, bool kerb)
        {
            r = Shrink(r, Joint * 0.5f);
            if (r.width < 0.05f || r.height < 0.05f) return;
            if (kerb) m.Laid.Kerbs++;
            else m.Laid.Stones++;
            if (!kerb && m.Chance(0.025f))
            {
                Hole(m, z, r);
                m.Laid.Missing++;
                return;
            }
            float lift = m.R(-0.0015f, 0.0015f) + m.EdgeLift;
            var tilt = new Vector2(m.R(-0.008f, 0.008f), m.R(-0.008f, 0.008f));
            if (m.Chance(0.05f))
            {
                lift -= 0.005f;
                tilt *= 2.5f;
                m.Laid.Sunk++;
            }
            if (m.Chance(0.07f) && Mathf.Max(r.width, r.height) > 0.3f)
            {
                Crack(m, z, r, tone, lift, tilt);
                m.Laid.Cracked++;
                return;
            }
            List<Vector2> poly = Corners(r);
            if (m.Chance(0.08f))
            {
                Chip(m, poly);
                m.Laid.Chipped++;
            }
            Stone(m, z, poly, tone, lift, tilt);
            if (m.Chance(z.Exposed ? 0.12f : 0.04f))
            {
                LichenSpot(m, r, m.Ground + StoneTop + lift + (Mathf.Abs(tilt.x) * r.width + Mathf.Abs(tilt.y) * r.height) * 0.5f + 0.0015f, z.Exposed && m.Chance(0.5f));
                m.Laid.Lichen++;
            }
        }

        /// <summary>A stone cracked right across: two pieces, the crack wandering, one settled a little lower.</summary>
        static void Crack(Mason m, FloorZone z, Rect r, Color tone, float lift, Vector2 tilt)
        {
            float t1 = m.R(0.3f, 0.7f), t2 = Mathf.Clamp(t1 + m.R(-0.25f, 0.25f), 0.2f, 0.8f), g = 0.005f;
            List<Vector2> a, b;
            if (r.width >= r.height)
            {
                float x1 = r.xMin + r.width * t1, x2 = r.xMin + r.width * t2;
                a = new List<Vector2> { new Vector2(r.xMin, r.yMin), new Vector2(x1 - g, r.yMin), new Vector2(x2 - g, r.yMax), new Vector2(r.xMin, r.yMax) };
                b = new List<Vector2> { new Vector2(x1 + g, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(x2 + g, r.yMax) };
            }
            else
            {
                float y1 = r.yMin + r.height * t1, y2 = r.yMin + r.height * t2;
                a = new List<Vector2> { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, y2 - g), new Vector2(r.xMin, y1 - g) };
                b = new List<Vector2> { new Vector2(r.xMin, y1 + g), new Vector2(r.xMax, y2 + g), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
            }
            Stone(m, z, a, tone, lift, tilt);
            Color settled = tone * 0.96f;
            settled.a = 1f;
            Stone(m, z, b, settled, lift - m.R(0.001f, 0.004f), tilt + new Vector2(m.R(-0.01f, 0.01f), m.R(-0.01f, 0.01f)));
        }

        /// <summary>A corner knocked off a stone.</summary>
        static void Chip(Mason m, List<Vector2> poly)
        {
            int k = m.Rng.Next(poly.Count);
            Vector2 p = poly[k], prev = poly[(k + poly.Count - 1) % poly.Count], next = poly[(k + 1) % poly.Count];
            float c1 = Mathf.Min(m.R(0.04f, 0.09f), (prev - p).magnitude * 0.4f), c2 = Mathf.Min(m.R(0.04f, 0.09f), (next - p).magnitude * 0.4f);
            poly[k] = p + (prev - p).normalized * c1;
            poly.Insert(k + 1, p + (next - p).normalized * c2);
        }

        /// <summary>A spot of lichen on a stone's top: a small ragged disc, pale green-grey or orange.</summary>
        static void LichenSpot(Mason m, Rect r, float y, bool orange)
        {
            float rad = m.R(0.025f, 0.06f);
            var at = new Vector2(m.R(r.xMin + rad, r.xMax - rad), m.R(r.yMin + rad, r.yMax - rad));
            if (r.width < rad * 2.2f || r.height < rad * 2.2f) at = r.center;
            Color col = m.B.Jitter(orange ? LichenOrange : Lichen, 0.08f);
            int n = 6;
            var ring = new Vector3[n];
            float phase = m.R(0f, 6.283f);
            for (int i = 0; i < n; i++)
            {
                float a = phase + i * Mathf.PI * 2f / n, rr = rad * m.R(0.6f, 1.1f);
                ring[i] = new Vector3(at.x + Mathf.Cos(a) * rr, y, at.y + Mathf.Sin(a) * rr);
            }
            var below = new Vector3(at.x, y - 1f, at.y);
            for (int i = 1; i + 1 < n; i++) m.B.AddTriangleOutward(ring[0], ring[i], ring[i + 1], col, below);
        }

        /// <summary>Where a stone's gone: bare earth sunk in its place, and grit.</summary>
        static void Hole(Mason m, FloorZone z, Rect r)
        {
            float y = m.Ground + GroutTop + 0.0012f;
            List<Vector2> c = Corners(r);
            var cols = new Color[4];
            for (int i = 0; i < 4; i++) cols[i] = Discolour(m, z, c[i], Earth);
            var below = new Vector3(r.center.x, y - 1f, r.center.y);
            Vector3 V(Vector2 p) => new Vector3(p.x, y, p.y);
            m.B.AddTriangleShaded(V(c[0]), V(c[1]), V(c[2]), cols[0], cols[1], cols[2], below);
            m.B.AddTriangleShaded(V(c[0]), V(c[2]), V(c[3]), cols[0], cols[2], cols[3], below);
            int n = 2 + m.Rng.Next(3);
            for (int i = 0; i < n; i++) Grit(m, new Vector2(m.R(r.xMin, r.xMax), m.R(r.yMin, r.yMax)), y, m.Chance(0.5f) ? BrickChip : Worn * 0.7f);
        }

        /// <summary>A chip of stone or brick lying on the floor: a little tetrahedron.</summary>
        static void Grit(Mason m, Vector2 at, float y, Color tone)
        {
            float s = m.R(0.02f, 0.05f), a = m.R(0f, 6.283f);
            Vector3 p0 = new Vector3(at.x + Mathf.Cos(a) * s, y, at.y + Mathf.Sin(a) * s);
            Vector3 p1 = new Vector3(at.x + Mathf.Cos(a + 2.1f) * s, y, at.y + Mathf.Sin(a + 2.1f) * s);
            Vector3 p2 = new Vector3(at.x + Mathf.Cos(a + 4.2f) * s * 0.8f, y, at.y + Mathf.Sin(a + 4.2f) * s * 0.8f);
            Vector3 top = new Vector3(at.x + m.R(-0.3f, 0.3f) * s, y + s * m.R(0.5f, 0.9f), at.y + m.R(-0.3f, 0.3f) * s);
            Vector3 mid = (p0 + p1 + p2 + top) * 0.25f;
            Color col = m.B.Jitter(tone, 0.1f);
            m.B.AddTriangleOutward(p0, p1, top, col, mid);
            m.B.AddTriangleOutward(p1, p2, top, col * 0.9f, mid);
            m.B.AddTriangleOutward(p2, p0, top, col * 0.8f, mid);
            m.Laid.Chips++;
        }

        // ------------------------------------------------------------------ laying

        static Color Tone(Mason m, Color[] tones)
        {
            // Now and then a stone from another quarry (a repair), or one stained right through (darker, or yellowed).
            Color[] from = m.Chance(0.05f) ? Palettes[m.Rng.Next(Palettes.Length)] : tones;
            Color c = m.B.Jitter(from[m.Rng.Next(from.Length)], 0.1f);
            float roll = m.R(0f, 1f);
            if (roll < 0.06f) c = Color.Lerp(c, c * 0.7f, 0.8f);
            else if (roll < 0.1f) c = Color.Lerp(c, new Color(0.55f, 0.48f, 0.3f), 0.35f);
            c.a = 1f;
            return c;
        }

        /// <summary>A piece of a field, by its span along the long way (a) and across (c), from its lower corner.</summary>
        static Rect Sub(Rect f, bool alongX, float a0, float a1, float c0, float c1) => alongX
            ? Rect.MinMaxRect(f.xMin + a0, f.yMin + c0, f.xMin + a1, f.yMin + c1)
            : Rect.MinMaxRect(f.xMin + c0, f.yMin + a0, f.xMin + c1, f.yMin + a1);

        static void Lay(Mason m, FloorZone z)
        {
            Rect a = Inset(z.Area);
            if (a.width < 0.6f || a.height < 0.6f) return;
            Color[] tones = Palettes[m.Rng.Next(Palettes.Length)];
            Bed(m, z, a);
            bool alongX = a.width >= a.height, runner = z.Laying == FloorLaying.Runner;
            Rect field = runner
                ? (alongX ? Rect.MinMaxRect(a.xMin, a.yMin + Kerb, a.xMax, a.yMax - Kerb) : Rect.MinMaxRect(a.xMin + Kerb, a.yMin, a.xMax - Kerb, a.yMax))
                : Shrink(a, Kerb);
            Kerbs(m, z, a, runner, alongX);
            switch (z.Laying)
            {
                case FloorLaying.Grid: Grid(m, z, field, tones, false); break;
                case FloorLaying.Checker: Grid(m, z, field, tones, true); break;
                case FloorLaying.Runner: Runner(m, z, field, tones); break;
                default: Flags(m, z, field, tones); break;
            }
            if (z.Cut) EdgeDirt(m, z, tones);
            // Grit and chips, most of them by the walls.
            int n = Mathf.RoundToInt(a.width * a.height * 0.5f);
            for (int i = 0; i < n; i++)
            {
                Vector2 p = new Vector2(m.R(a.xMin, a.xMax), m.R(a.yMin, a.yMax));
                if (m.Chance(0.6f))
                {
                    // Pushed out to the nearest wall.
                    float dx = Mathf.Min(p.x - a.xMin, a.xMax - p.x), dy = Mathf.Min(p.y - a.yMin, a.yMax - p.y), by = m.R(0.02f, 0.45f);
                    if (dx < dy) p.x = p.x - a.xMin < a.xMax - p.x ? a.xMin + by : a.xMax - by;
                    else p.y = p.y - a.yMin < a.yMax - p.y ? a.yMin + by : a.yMax - by;
                }
                if (z.Cut && !z.Keeps(p, -0.05f)) continue;
                Grit(m, p, m.Ground + StoneTop, m.Chance(0.45f) ? BrickChip : tones[m.Rng.Next(tones.Length)] * 0.9f);
            }
        }

        /// <summary>
        /// The open diagonal edge: soil crumbs, grit and pebbles on the dirt just outside the last row, and now and then a
        /// stone knocked loose and lying out there on top of the ground. Nothing covers the stones' edges.
        /// </summary>
        static void EdgeDirt(Mason m, FloorZone z, Color[] tones)
        {
            Vector2 d = z.CutB - z.CutA;
            float len = d.magnitude;
            d /= len;
            Vector2 n = new Vector2(-d.y, d.x);
            if (Vector2.Dot(n, z.Keep - z.CutA) > 0f) n = -n;
            Color soil = new Color(0.3f, 0.23f, 0.16f);
            for (float t = 0.1f; t < len - 0.1f; t += m.R(0.12f, 0.3f))
            {
                Vector2 on = z.CutA + d * t;
                Vector2 p = on + n * m.R(0.3f, 0.95f);
                Grit(m, p, m.Ground + 0.002f, m.Chance(0.6f) ? soil : Worn * 0.6f);
                if (m.Chance(0.12f))
                {
                    float s = m.R(0.16f, 0.3f), yaw = m.R(0f, 6.283f);
                    Vector2 at = on + n * m.R(0.5f, 0.9f);
                    Vector2 u = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw)) * s * 0.5f, v = new Vector2(-u.y, u.x) * m.R(0.7f, 1f);
                    m.Skirt = 0.035f;
                    Stone(m, z, new List<Vector2> { at - u - v, at + u - v, at + u + v, at - u + v }, Tone(m, tones), 0.004f, new Vector2(m.R(-0.05f, 0.05f), m.R(-0.05f, 0.05f)));
                    m.Skirt = 0f;
                }
            }
        }

        /// <summary>The bedding and grout under a stretch, in cells, darkening with the damp.</summary>
        static void Bed(Mason m, FloorZone z, Rect a)
        {
            int nx = Mathf.Max(1, Mathf.CeilToInt(a.width / 0.9f)), ny = Mathf.Max(1, Mathf.CeilToInt(a.height / 0.9f));
            float y = m.Ground + GroutTop;
            Vector3 P(int i, int j) => new Vector3(a.xMin + a.width * i / nx, y, a.yMin + a.height * j / ny);
            Color C(int i, int j) => Discolour(m, z, new Vector2(a.xMin + a.width * i / nx, a.yMin + a.height * j / ny), Grout);
            var below = new Vector3(a.center.x, y - 1f, a.center.y);
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    // (Under a cut stretch, grout only where it's well inside: the dirt shows between the edge stones.)
                    if (z.Cut)
                    {
                        float d0 = Mathf.Min(Mathf.Min(z.Depth(new Vector2(P(i, j).x, P(i, j).z)), z.Depth(new Vector2(P(i + 1, j).x, P(i + 1, j).z))),
                            Mathf.Min(z.Depth(new Vector2(P(i, j + 1).x, P(i, j + 1).z)), z.Depth(new Vector2(P(i + 1, j + 1).x, P(i + 1, j + 1).z))));
                        if (d0 < 0.3f) continue;
                    }
                    m.B.AddTriangleShaded(P(i, j), P(i + 1, j), P(i + 1, j + 1), C(i, j), C(i + 1, j), C(i + 1, j + 1), below);
                    m.B.AddTriangleShaded(P(i, j), P(i + 1, j + 1), P(i, j + 1), C(i, j), C(i + 1, j + 1), C(i, j + 1), below);
                }
        }

        /// <summary>The kerb course: square stones in the corners and runs of narrow ones between (a hallway's two sides only).</summary>
        static void Kerbs(Mason m, FloorZone z, Rect a, bool runner, bool alongX)
        {
            Color K() => m.B.Jitter(KerbTone, 0.07f);
            void Along(float x0, float x1, float y0, float y1, bool inX)
            {
                float len = inX ? x1 - x0 : y1 - y0;
                for (float s = 0f; s < len - 0.02f;)
                {
                    float l = Mathf.Min(m.R(0.45f, 0.8f), len - s);
                    if (len - s - l < 0.2f) l = len - s;
                    Field(m, z, inX ? Rect.MinMaxRect(x0 + s, y0, x0 + s + l, y1) : Rect.MinMaxRect(x0, y0 + s, x1, y0 + s + l), K(), true);
                    s += l;
                }
            }
            if (runner)
            {
                if (alongX)
                {
                    Along(a.xMin, a.xMax, a.yMin, a.yMin + Kerb, true);
                    Along(a.xMin, a.xMax, a.yMax - Kerb, a.yMax, true);
                }
                else
                {
                    Along(a.xMin, a.xMin + Kerb, a.yMin, a.yMax, false);
                    Along(a.xMax - Kerb, a.xMax, a.yMin, a.yMax, false);
                }
                return;
            }
            foreach (Vector2 c in new[] { new Vector2(a.xMin, a.yMin), new Vector2(a.xMax - Kerb, a.yMin), new Vector2(a.xMax - Kerb, a.yMax - Kerb), new Vector2(a.xMin, a.yMax - Kerb) })
                Field(m, z, new Rect(c.x, c.y, Kerb, Kerb), K(), true);
            Along(a.xMin + Kerb, a.xMax - Kerb, a.yMin, a.yMin + Kerb, true);
            Along(a.xMin + Kerb, a.xMax - Kerb, a.yMax - Kerb, a.yMax, true);
            Along(a.xMin, a.xMin + Kerb, a.yMin + Kerb, a.yMax - Kerb, false);
            Along(a.xMax - Kerb, a.xMax, a.yMin + Kerb, a.yMax - Kerb, false);
        }

        /// <summary>Flagstones of mixed lengths in courses the long way, each course's joints off the last's.</summary>
        static void Flags(Mason m, FloorZone z, Rect f, Color[] tones)
        {
            bool alongX = f.width >= f.height;
            float len = alongX ? f.width : f.height, across = alongX ? f.height : f.width;
            for (float c0 = 0f; c0 < across - 0.01f;)
            {
                float rh = m.R(0.36f, 0.58f);
                if (across - c0 - rh < 0.22f) rh = across - c0;
                float start = 0f, a = -m.R(0f, 0.4f);
                while (start < len - 0.01f)
                {
                    a += m.R(0.38f, 0.85f);
                    float end = Mathf.Min(len, a);
                    if (len - end < 0.16f) end = len;
                    if (end - start < 0.14f && end < len) continue;
                    Field(m, z, Sub(f, alongX, start, end, c0, c0 + rh), Tone(m, tones));
                    start = end;
                }
                c0 += rh;
            }
        }

        /// <summary>
        /// Square tiles in a grid; for a chequer, pale and dark tiles inside a dark band, plain ones round it (or the
        /// chequer right across a small floor).
        /// </summary>
        static void Grid(Mason m, FloorZone z, Rect f, Color[] tones, bool chequer)
        {
            float s = chequer ? m.R(0.4f, 0.48f) : m.R(0.42f, 0.52f);
            int nx = Mathf.Max(1, Mathf.RoundToInt(f.width / s)), ny = Mathf.Max(1, Mathf.RoundToInt(f.height / s));
            bool framed = chequer && nx >= 6 && ny >= 6;
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    var r = Rect.MinMaxRect(f.xMin + f.width * i / nx, f.yMin + f.height * j / ny, f.xMin + f.width * (i + 1) / nx, f.yMin + f.height * (j + 1) / ny);
                    Color tone;
                    int ring = Mathf.Min(Mathf.Min(i, nx - 1 - i), Mathf.Min(j, ny - 1 - j));
                    if (!chequer || (framed && ring == 0)) tone = Tone(m, tones);
                    else if (framed && ring == 1) tone = m.B.Jitter(Dark, 0.06f);
                    else
                    {
                        tone = m.B.Jitter((i + j) % 2 == 0 ? Pale : Dark, 0.05f);
                        m.Laid.Chequer++;
                    }
                    Field(m, z, r, tone);
                }
        }

        /// <summary>A hallway's runner: slabs laid across it (two to a course, staggered, if it's wide), course after course.</summary>
        static void Runner(Mason m, FloorZone z, Rect f, Color[] tones)
        {
            bool alongX = f.width >= f.height;
            float len = alongX ? f.width : f.height, across = alongX ? f.height : f.width;
            bool split = across > 1.3f;
            for (float a = 0f; a < len - 0.01f;)
            {
                float l = Mathf.Min(m.R(0.42f, 0.75f), len - a);
                if (len - a - l < 0.2f) l = len - a;
                if (!split) Field(m, z, Sub(f, alongX, a, a + l, 0f, across), Tone(m, tones));
                else
                {
                    float t = across * m.R(0.4f, 0.6f);
                    Field(m, z, Sub(f, alongX, a, a + l, 0f, t), Tone(m, tones));
                    Field(m, z, Sub(f, alongX, a, a + l, t, across), Tone(m, tones));
                }
                a += l;
            }
        }

        /// <summary>A worn threshold stone across the wall's line in every doorway and arch onto the floor.</summary>
        static void Thresholds(Mason m, RuinPlan plan)
        {
            // (Spanning the gap between the floors either side of the wall, exactly.)
            float half = WallInset - 0.004f;
            foreach (RuinRun run in plan.Runs)
            {
                Vector2 d = run.Dir, n = new Vector2(-d.y, d.x);
                foreach (RuinOpening o in run.Openings)
                {
                    if (!o.Passage) continue;
                    Vector2 mid = run.A + d * o.Center;
                    FloorZone? zone = null;
                    foreach (FloorZone z in plan.Floor)
                        if (z.Area.Contains(mid + n * 0.4f) || z.Area.Contains(mid - n * 0.4f)) { zone = z; break; }
                    if (zone == null) continue;
                    float x0 = o.Center - o.Half - 0.04f, x1 = o.Center + o.Half + 0.04f;
                    var poly = new List<Vector2> { run.A + d * x0 - n * half, run.A + d * x1 - n * half, run.A + d * x1 + n * half, run.A + d * x0 + n * half };
                    Stone(m, zone.Value, poly, m.B.Jitter(Sill, 0.05f), 0.002f, Vector2.zero);
                    m.Laid.Thresholds++;
                }
            }
        }
    }
}
