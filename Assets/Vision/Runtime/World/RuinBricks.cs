using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// Lays a ruin's walls brick by brick, the way brick-wall generators do it: whole courses in running bond (each course
    /// half a brick along from the one below), two bricks thick (the back layer a further quarter brick along, so the
    /// layers don't line up), each brick's length a little uneven; then the openings and the broken top are cut out of
    /// them: bricks inside a window, a door or an arch's head never laid, bricks above the wall's broken top left off
    /// (a brick or so either way, so the edge is ragged and stepped). Square corners interlock, alternate courses of
    /// each wall running through. Windows and doors get a stone lintel (and a window a stone sill); an arch's head is a
    /// ring of radial bricks, an odd number with one at the crown. A mortar core runs behind each course, set back from
    /// the faces so the bricks stand proud of the joints. Fallen bricks lie at the foot of the broken ends and fallen
    /// stretches, and a few loose ones teeter on top. Only faces that can be seen are drawn (a brick's face, its top
    /// where nothing sits on it, its end where nothing butts against it, its underside over an opening).
    /// Each wall is laid in its own frame: x along it from its start, y up from the ruin's base, z across it (+ its front).
    /// </summary>
    public static class RuinBricks
    {
        const float Joint = 0.01f, CoreInset = 0.008f;
        static float Course => LowPolyModels.BrickCourse;
        static float Half => LowPolyModels.WallThickness * 0.5f;

        public struct Brick
        {
            public float X0, X1, Y0, Y1;
            /// <summary>0 the front layer (+z), 1 the back (-z).</summary>
            public int Wythe;
            public Color Color;
            public bool Gray;
            public bool Top, Bottom, StartCap, EndCap;
        }

        /// <summary>A stone across the wall's whole depth (a lintel or a sill).</summary>
        public struct Stone { public Vector3 Min, Size; public Color Color; }

        /// <summary>One radial brick of an arch's head: its angles and radii about the springing centre.</summary>
        public struct Voussoir { public float Cx, Cy, A0, A1, R0, R1; public Color Color; }

        public sealed class Wall
        {
            public readonly List<Brick> Bricks = new List<Brick>();
            public readonly List<Vector4> Cores = new List<Vector4>();
            public readonly List<Color> CoreColors = new List<Color>();
            public readonly List<Stone> Stones = new List<Stone>();
            public readonly List<Voussoir> Arches = new List<Voussoir>();
            public int Courses;
            public float Length;
        }

        /// <summary>How far a wall runs past (or stops short of) its end at a join, course by course, so corners interlock.</summary>
        static float Extend(bool join, int bond, int course)
        {
            if (!join) return 0f;
            if (bond < 0) return Half;
            return (course + bond) % 2 == 0 ? Half : -Half;
        }

        /// <summary>Lays a wall's bricks (not yet drawn).</summary>
        public static Wall Lay(RuinRun run)
        {
            var w = new Wall { Length = run.Length };
            float len = run.Length;
            float maxTop = 0f;
            for (float x = 0f; x <= len; x += 0.1f) maxTop = Mathf.Max(maxTop, run.TopAt(x));
            int courses = Mathf.CeilToInt((maxTop + 0.15f) / Course);
            w.Courses = courses;
            var rows = new List<Brick>[courses, 2];
            var blocked = new List<Vector2>();
            var stoneSpans = new List<Vector3>();

            // The openings' parts on the course grid: their sills, tops and springing lines.
            var ops = new List<(RuinOpening o, int sill, int top)>();
            foreach (RuinOpening o in run.Openings)
                ops.Add((o, Mathf.Max(0, Mathf.RoundToInt(o.Sill / Course)), Mathf.Max(1, Mathf.RoundToInt(o.Top / Course))));

            for (int c = 0; c < courses; c++)
            {
                float y0 = c * Course, y1 = y0 + Course, ym = (y0 + y1) * 0.5f;
                blocked.Clear();
                foreach (var (o, sill, top) in ops)
                {
                    float spring = top * Course;
                    bool stoneSill = o.Kind != RuinOpeningKind.Door && sill >= 1;
                    if (o.Kind == RuinOpeningKind.Arch)
                    {
                        if (c >= sill && c < top) blocked.Add(new Vector2(o.Center - o.Half, o.Center + o.Half));
                        else if (c >= top)
                        {
                            float dy = ym - spring, ro = o.ArchOuter;
                            if (dy < ro) { float hw = Mathf.Sqrt(ro * ro - dy * dy); blocked.Add(new Vector2(o.Center - hw, o.Center + hw)); }
                        }
                    }
                    else
                    {
                        if (c >= sill && c < top) blocked.Add(new Vector2(o.Center - o.Half, o.Center + o.Half));
                        // The lintel's course.
                        if (c == top && (top + 1) * Course <= run.TopAt(o.Center) + 0.05f)
                        {
                            blocked.Add(new Vector2(o.Center - o.Half - 0.1f, o.Center + o.Half + 0.1f));
                            stoneSpans.Add(new Vector3(o.Center - o.Half - 0.1f, o.Center + o.Half + 0.1f, y0));
                        }
                    }
                    // The window's (or arched window's) sill stone, in the course under it.
                    if (stoneSill && c == sill - 1)
                    {
                        blocked.Add(new Vector2(o.Center - o.Half - 0.05f, o.Center + o.Half + 0.05f));
                        stoneSpans.Add(new Vector3(o.Center - o.Half - 0.05f, o.Center + o.Half + 0.05f, y0));
                    }
                }
                float s0 = -Extend(run.JoinStart, run.BondStart, c), s1 = len + Extend(run.JoinEnd, run.BondEnd, c);
                List<Vector2> solid = Subtract(new Vector2(s0, s1), blocked);
                for (int wy = 0; wy < 2; wy++)
                {
                    var row = new List<Brick>();
                    rows[c, wy] = row;
                    float off = (c % 2) * 0.5f * LowPolyModels.BrickLength + wy * 0.25f * LowPolyModels.BrickLength;
                    foreach (Vector2 span in solid) LaySpan(run, w, row, span.x, span.y, off, c, wy, y0, y1);
                }
            }

            // Stones across the whole depth (lintels and sills), the arches' rings.
            foreach (Vector3 s in stoneSpans)
            {
                float k = 0.92f + 0.12f * RuinHash.Value(run.Seed, 71, Mathf.RoundToInt(s.x * 100f), Mathf.RoundToInt(s.z * 100f));
                w.Stones.Add(new Stone { Min = new Vector3(s.x, s.z + Joint, -Half), Size = new Vector3(s.y - s.x, Course - Joint, Half * 2f), Color = new Color(0.6f * k, 0.58f * k, 0.53f * k) });
            }
            foreach (var (o, sill, top) in ops)
                if (o.Kind == RuinOpeningKind.Arch) LayArch(run, w, o, top * Course);

            // What shows of each brick: its top where nothing sits on it, its underside over a gap, its ends where
            // nothing butts against them.
            for (int c = 0; c < courses; c++)
                for (int wy = 0; wy < 2; wy++)
                {
                    List<Brick> row = rows[c, wy];
                    for (int i = 0; i < row.Count; i++)
                    {
                        Brick b = row[i];
                        b.Top = c == courses - 1 || !Covered(rows[c + 1, wy], b.X0, b.X1);
                        b.Bottom = c > 0 && !Covered(rows[c - 1, wy], b.X0, b.X1);
                        b.StartCap = i == 0 || row[i - 1].X1 < b.X0 - Joint * 1.5f;
                        b.EndCap = i == row.Count - 1 || row[i + 1].X0 > b.X1 + Joint * 1.5f;
                        w.Bricks.Add(b);
                    }
                    // The mortar core behind this course's bricks (both layers), in stretches.
                }
            for (int c = 0; c < courses; c++)
            {
                var spans = new List<Vector2>();
                for (int wy = 0; wy < 2; wy++)
                    foreach (Brick b in rows[c, wy]) spans.Add(new Vector2(b.X0, b.X1));
                spans.Sort((a, b) => a.x.CompareTo(b.x));
                Vector2 cur = new Vector2(float.NaN, 0f);
                foreach (Vector2 s in spans)
                {
                    if (float.IsNaN(cur.x)) { cur = s; continue; }
                    if (s.x <= cur.y + Joint * 2f) cur.y = Mathf.Max(cur.y, s.y);
                    else { AddCore(run, w, cur, c); cur = s; }
                }
                if (!float.IsNaN(cur.x)) AddCore(run, w, cur, c);
            }
            return w;
        }

        static void AddCore(RuinRun run, Wall w, Vector2 span, int c)
        {
            w.Cores.Add(new Vector4(span.x + 0.004f, span.y - 0.004f, c * Course, (c + 1) * Course - 0.004f));
            float k = 1f + (RuinHash.Value(run.Seed, 61, c, Mathf.RoundToInt(span.x * 10f)) - 0.5f) * 0.08f;
            w.CoreColors.Add(new Color(0.58f * k, 0.56f * k, 0.52f * k));
        }

        /// <summary>One solid stretch of a course in one layer: bricks between uneven joints, left off above the broken top.</summary>
        static void LaySpan(RuinRun run, Wall w, List<Brick> row, float a, float b, float off, int c, int wy, float y0, float y1)
        {
            float bl = LowPolyModels.BrickLength, ym = (y0 + y1) * 0.5f;
            int k0 = Mathf.FloorToInt((a - off) / bl) - 1, k1 = Mathf.CeilToInt((b - off) / bl) + 1;
            float JointAt(int k) => off + k * bl + (RuinHash.Value(run.Seed, 11 + wy, c, k) - 0.5f) * 0.16f * bl;
            var pieces = new List<Vector2>();
            for (int k = k0; k < k1; k++)
            {
                float x0 = Mathf.Max(a, JointAt(k)), x1 = Mathf.Min(b, JointAt(k + 1));
                if (x1 - x0 > 1e-3f) pieces.Add(new Vector2(x0, x1));
            }
            // A sliver at either end goes to its neighbour.
            if (pieces.Count > 1 && pieces[0].y - pieces[0].x < 0.06f) { pieces[1] = new Vector2(pieces[0].x, pieces[1].y); pieces.RemoveAt(0); }
            int n = pieces.Count;
            if (n > 1 && pieces[n - 1].y - pieces[n - 1].x < 0.06f) { pieces[n - 2] = new Vector2(pieces[n - 2].x, pieces[n - 1].y); pieces.RemoveAt(n - 1); }
            foreach (Vector2 p in pieces)
            {
                float xc = (p.x + p.y) * 0.5f;
                int key = Mathf.RoundToInt(xc * 50f);
                // Off above the broken top (give or take, brick by brick).
                float ragged = (RuinHash.Value(run.Seed, 21 + wy, c, key) - 0.5f) * 0.14f;
                if (ym > run.TopAt(xc) + ragged) continue;
                bool firstOrLast = p.x <= a + 1e-4f || p.y >= b - 1e-4f;
                float x0 = p.x + (p.x <= a + 1e-4f ? 0f : Joint * 0.5f), x1 = p.y - (p.y >= b - 1e-4f ? 0f : Joint * 0.5f);
                if (firstOrLast && x1 - x0 < 0.03f) continue;
                Color col = BrickColor(run.Seed, c, wy, key, xc, ym, out bool gray);
                row.Add(new Brick { X0 = x0, X1 = x1, Y0 = y0 + Joint, Y1 = y1, Wythe = wy, Color = col, Gray = gray });
            }
        }

        /// <summary>An arch's head: an odd number of radial bricks round it, one at the crown, left off above the broken top.</summary>
        static void LayArch(RuinRun run, Wall w, RuinOpening o, float spring)
        {
            float r0 = o.ArchRadius, r1 = o.ArchOuter, rm = (r0 + r1) * 0.5f;
            int n = Mathf.Max(5, Mathf.RoundToInt(Mathf.PI * rm / Course));
            if (n % 2 == 0) n++;
            float step = Mathf.PI / n, gap = Joint / rm;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * step + gap * 0.5f, a1 = (i + 1) * step - gap * 0.5f, am = (a0 + a1) * 0.5f;
                float x = o.Center + Mathf.Cos(am) * rm, y = spring + Mathf.Sin(am) * rm;
                float ragged = (RuinHash.Value(run.Seed, 31, i, Mathf.RoundToInt(o.Center * 10f)) - 0.5f) * 0.14f;
                if (y > run.TopAt(x) + ragged) continue;
                Color col = BrickColor(run.Seed, 900 + i, 0, Mathf.RoundToInt(o.Center * 50f), x, y, out _) * 0.94f;
                col.a = 1f;
                w.Arches.Add(new Voussoir { Cx = o.Center, Cy = spring, A0 = a0, A1 = a1, R0 = r0, R1 = r1, Color = col });
            }
        }

        /// <summary>A stretch with the given ones taken out of it.</summary>
        static List<Vector2> Subtract(Vector2 span, List<Vector2> cuts)
        {
            var result = new List<Vector2> { span };
            foreach (Vector2 cut in cuts)
            {
                for (int i = result.Count - 1; i >= 0; i--)
                {
                    Vector2 s = result[i];
                    if (cut.y <= s.x || cut.x >= s.y) continue;
                    result.RemoveAt(i);
                    if (cut.y < s.y) result.Insert(i, new Vector2(cut.y, s.y));
                    if (cut.x > s.x) result.Insert(i, new Vector2(s.x, cut.x));
                }
            }
            return result;
        }

        /// <summary>Whether a row's bricks cover a stretch (joints allowed).</summary>
        static bool Covered(List<Brick> row, float a, float b)
        {
            float cur = a;
            foreach (Brick k in row)
            {
                if (k.X1 <= cur) continue;
                if (k.X0 > cur + 0.025f) return false;
                cur = Mathf.Max(cur, k.X1);
                if (cur >= b - 0.025f) return true;
            }
            return cur >= b - 0.025f;
        }

        // ------------------------------------------------------------------ colour

        static readonly (Color c, float w)[] Tones =
        {
            (new Color(0.47f, 0.22f, 0.15f), 0.34f),   // rust
            (new Color(0.40f, 0.20f, 0.14f), 0.24f),   // brown-red
            (new Color(0.30f, 0.15f, 0.11f), 0.14f),   // dark clinker
            (new Color(0.58f, 0.31f, 0.20f), 0.16f),   // salmon-orange
            (new Color(0.50f, 0.32f, 0.22f), 0.12f),   // brown-tan
        };
        static readonly Color Weathered = new Color(0.50f, 0.48f, 0.45f);

        /// <summary>
        /// A brick's colour: one of the rust-brown tones (weighted), shifted a little in hue, saturation and value; greyed
        /// where a weathered patch spreads over the wall (slow noise over its face), and now and then a grey brick outright.
        /// </summary>
        public static Color BrickColor(int seed, int course, int wythe, int key, float x, float y, out bool gray)
        {
            float pick = RuinHash.Value(seed, 41, course * 2 + wythe, key);
            Color c = Tones[Tones.Length - 1].c;
            foreach (var (tone, wt) in Tones)
            {
                if (pick < wt) { c = tone; break; }
                pick -= wt;
            }
            Color.RGBToHSV(c, out float h, out float s, out float v);
            h = Mathf.Repeat(h + (RuinHash.Value(seed, 42, course * 2 + wythe, key) - 0.5f) * 0.024f, 1f);
            s = Mathf.Clamp01(s + (RuinHash.Value(seed, 43, course * 2 + wythe, key) - 0.5f) * 0.16f);
            v = Mathf.Clamp01(v + (RuinHash.Value(seed, 44, course * 2 + wythe, key) - 0.5f) * 0.14f);
            c = Color.HSVToRGB(h, s, v);
            gray = RuinHash.Value(seed, 45, course * 2 + wythe, key) < 0.03f;
            if (gray) return Weathered * (0.9f + 0.2f * RuinHash.Value(seed, 46, course, key));
            float patch = Noise2(seed, x / 0.6f, y / 0.6f);
            if (patch > 0.68f) c = Color.Lerp(c, Weathered, Mathf.Min(0.85f, (patch - 0.68f) / 0.32f * 1.3f));
            c.a = 1f;
            return c;
        }

        static float Noise2(int seed, float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = RuinHash.Value(seed, 51, ix, iy), b = RuinHash.Value(seed, 51, ix + 1, iy);
            float c = RuinHash.Value(seed, 51, ix, iy + 1), d = RuinHash.Value(seed, 51, ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>Builders that start a fresh mesh whenever the one being filled would pass its triangle cap.</summary>
        public sealed class Chunks
        {
            public const int Cap = 12000;
            public readonly List<LowPolyMeshBuilder> Builders = new List<LowPolyMeshBuilder>();
            readonly System.Random rng;
            Matrix4x4? frame;
            public Chunks(System.Random rng) { this.rng = rng; Builders.Add(new LowPolyMeshBuilder(rng)); }
            public LowPolyMeshBuilder Need(int tris)
            {
                LowPolyMeshBuilder b = Builders[Builders.Count - 1];
                if (b.TriangleCount + tris <= Cap) return b;
                b = new LowPolyMeshBuilder(rng) { Transform = frame };
                Builders.Add(b);
                return b;
            }
            public Matrix4x4? Frame
            {
                get => frame;
                set { frame = value; Builders[Builders.Count - 1].Transform = value; }
            }
            public int Triangles { get { int n = 0; foreach (LowPolyMeshBuilder b in Builders) n += b.TriangleCount; return n; } }
        }

        static void Quad(LowPolyMeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 outward, Color c)
        {
            Vector3 inside = (p0 + p1 + p2 + p3) * 0.25f - outward;
            b.AddTriangleOutward(p0, p1, p2, c, inside);
            b.AddTriangleOutward(p0, p2, p3, c, inside);
        }

        /// <summary>Draws a laid wall into the chunks (their frame set to the wall's).</summary>
        public static void Draw(Chunks chunks, Wall w)
        {
            float h = Half;
            foreach (Brick k in w.Bricks)
            {
                float zf = k.Wythe == 0 ? h : -h, s = k.Wythe == 0 ? 1f : -1f;
                int tris = 2 + (k.Top ? 2 : 0) + (k.Bottom ? 2 : 0) + (k.StartCap ? 2 : 0) + (k.EndCap ? 2 : 0);
                LowPolyMeshBuilder b = chunks.Need(tris);
                Vector3 n = new Vector3(0f, 0f, s);
                Quad(b, new Vector3(k.X0, k.Y0, zf), new Vector3(k.X1, k.Y0, zf), new Vector3(k.X1, k.Y1, zf), new Vector3(k.X0, k.Y1, zf), n, k.Color);
                Color shade = k.Color * 0.92f;
                shade.a = 1f;
                if (k.Top) Quad(b, new Vector3(k.X0, k.Y1, 0f), new Vector3(k.X1, k.Y1, 0f), new Vector3(k.X1, k.Y1, zf), new Vector3(k.X0, k.Y1, zf), Vector3.up, k.Color);
                if (k.Bottom) Quad(b, new Vector3(k.X0, k.Y0, 0f), new Vector3(k.X1, k.Y0, 0f), new Vector3(k.X1, k.Y0, zf), new Vector3(k.X0, k.Y0, zf), Vector3.down, shade);
                if (k.StartCap) Quad(b, new Vector3(k.X0, k.Y0, 0f), new Vector3(k.X0, k.Y1, 0f), new Vector3(k.X0, k.Y1, zf), new Vector3(k.X0, k.Y0, zf), Vector3.left, shade);
                if (k.EndCap) Quad(b, new Vector3(k.X1, k.Y0, 0f), new Vector3(k.X1, k.Y1, 0f), new Vector3(k.X1, k.Y1, zf), new Vector3(k.X1, k.Y0, zf), Vector3.right, shade);
            }
            float ci = h - CoreInset;
            for (int i = 0; i < w.Cores.Count; i++)
            {
                Vector4 c = w.Cores[i];
                Color col = w.CoreColors[i];
                // (Its ends are always behind the end bricks' own ends.)
                LowPolyMeshBuilder b = chunks.Need(6);
                Quad(b, new Vector3(c.x, c.z, ci), new Vector3(c.y, c.z, ci), new Vector3(c.y, c.w, ci), new Vector3(c.x, c.w, ci), Vector3.forward, col);
                Quad(b, new Vector3(c.x, c.z, -ci), new Vector3(c.y, c.z, -ci), new Vector3(c.y, c.w, -ci), new Vector3(c.x, c.w, -ci), Vector3.back, col);
                Quad(b, new Vector3(c.x, c.w, -ci), new Vector3(c.y, c.w, -ci), new Vector3(c.y, c.w, ci), new Vector3(c.x, c.w, ci), Vector3.up, col);
            }
            foreach (Stone s in w.Stones) chunks.Need(12).AddBox(s.Min, s.Size, s.Color, 0f);
            foreach (Voussoir v in w.Arches)
            {
                LowPolyMeshBuilder b = chunks.Need(8);
                Vector3 P(float a, float r, float z) => new Vector3(v.Cx + Mathf.Cos(a) * r, v.Cy + Mathf.Sin(a) * r, z);
                Color shade = v.Color * 0.9f;
                shade.a = 1f;
                // Its face, front and back; its underside (the arch's soffit); its back, against the course ends (mortar).
                Quad(b, P(v.A0, v.R0, h), P(v.A1, v.R0, h), P(v.A1, v.R1, h), P(v.A0, v.R1, h), Vector3.forward, v.Color);
                Quad(b, P(v.A0, v.R0, -h), P(v.A1, v.R0, -h), P(v.A1, v.R1, -h), P(v.A0, v.R1, -h), Vector3.back, v.Color);
                float am = (v.A0 + v.A1) * 0.5f;
                Quad(b, P(v.A0, v.R0, -h), P(v.A1, v.R0, -h), P(v.A1, v.R0, h), P(v.A0, v.R0, h), -new Vector3(Mathf.Cos(am), Mathf.Sin(am), 0f), shade);
                Quad(b, P(v.A0, v.R1, -h), P(v.A1, v.R1, -h), P(v.A1, v.R1, h), P(v.A0, v.R1, h), new Vector3(Mathf.Cos(am), Mathf.Sin(am), 0f), new Color(0.56f, 0.54f, 0.5f));
            }
        }

        /// <summary>
        /// Bricks fallen from a wall: heaped at the foot of a broken end and of a fallen stretch, a few strewn along it,
        /// and one or two left teetering on the broken top. Whole boxes, tipped every way.
        /// </summary>
        public static void Rubble(Chunks chunks, RuinRun run, Wall w, System.Random rng)
        {
            float len = run.Length, h = Half;
            float R(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
            void Loose(Vector3 at, float yaw, float tilt, Color c)
            {
                LowPolyMeshBuilder b = chunks.Need(12);
                Matrix4x4? keep = b.Transform;
                b.Transform = (keep ?? Matrix4x4.identity) * Matrix4x4.TRS(at, Quaternion.Euler(tilt, yaw, R(-12f, 12f)), Vector3.one);
                var size = new Vector3(LowPolyModels.BrickLength * (rng.NextDouble() < 0.3 ? 0.55f : 1f), Course - Joint, LowPolyModels.BrickDepth);
                b.AddBox(-size * 0.5f, size, c, 0f);
                b.Transform = keep;
            }
            Color Any() => BrickColor(run.Seed, rng.Next(1000), 0, rng.Next(1000), R(0f, 10f), R(0f, 3f), out _);
            void Heap(float at, float spread, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    float side = rng.NextDouble() < 0.5 ? -1f : 1f, dist = Mathf.Abs(R(-1f, 1f) * R(0.2f, 1f));
                    var p = new Vector3(Mathf.Clamp(at + R(-spread, spread), -0.3f, len + 0.3f), Course * 0.4f + (dist < 0.25f && i % 3 == 0 ? Course * 0.8f : 0f), side * (h + 0.05f + dist * 0.8f));
                    Loose(p, R(0f, 360f), R(-25f, 25f), Any());
                }
            }
            if (run.StartBreak > 0f) Heap(run.StartBreak * 0.3f, run.StartBreak * 0.6f, 6 + rng.Next(6));
            if (run.EndBreak > 0f) Heap(len - run.EndBreak * 0.3f, run.EndBreak * 0.6f, 6 + rng.Next(6));
            foreach (Vector3 br in run.Breaches) Heap(br.x, br.y * 0.6f, 8 + rng.Next(7));
            int strewn = Mathf.RoundToInt(len * 0.6f);
            for (int i = 0; i < strewn; i++) Heap(R(0f, len), 0.2f, 1);
            // Teetering on the broken top.
            var tops = new List<Brick>();
            foreach (Brick k in w.Bricks) if (k.Top && k.Y1 > 0.8f) tops.Add(k);
            for (int i = 0; i < 2 && tops.Count > 0; i++)
            {
                Brick k = tops[rng.Next(tops.Count)];
                Loose(new Vector3((k.X0 + k.X1) * 0.5f, k.Y1 + Course * 0.5f, (k.Wythe == 0 ? 1f : -1f) * R(0f, 0.06f)), R(-35f, 35f), R(-6f, 6f), Any());
            }
        }

        /// <summary>A whole ruin into its chunks: every wall in its frame, its bricks and its rubble.</summary>
        public static Chunks Build(RuinPlan plan, System.Random rng)
        {
            var chunks = new Chunks(rng);
            foreach (RuinRun run in plan.Runs)
            {
                Vector2 d = run.Dir;
                chunks.Frame = Matrix4x4.TRS(new Vector3(run.A.x, 0f, run.A.y), Quaternion.Euler(0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 0f), Vector3.one);
                Wall w = Lay(run);
                Draw(chunks, w);
                Rubble(chunks, run, w, rng);
            }
            chunks.Frame = null;
            return chunks;
        }
    }
}
