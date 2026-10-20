using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>What kind of hole a ruin's wall has in it.</summary>
    public enum RuinOpeningKind { Window, Door, Arch }

    /// <summary>
    /// A hole through one wall of a ruin, along the wall (metres from its start): a window (from its sill up to its top,
    /// a stone sill under it and a stone lintel over it), a door (from the ground) or an arch (a round head of radial
    /// bricks springing at <see cref="Top"/>, over a window's sill or from the ground).
    /// </summary>
    public struct RuinOpening
    {
        public RuinOpeningKind Kind;
        public float Center, Width, Sill, Top;

        public float Half => Width * 0.5f;
        /// <summary>An arch's radius and the outside of its ring of radial bricks.</summary>
        public float ArchRadius => Width * 0.5f;
        public float ArchOuter => Width * 0.5f + LowPolyModels.BrickLength;
        /// <summary>From the ground up: a way through (a door, or an arch with no sill).</summary>
        public bool Passage => Sill <= 0.01f;
        /// <summary>The highest point of the hole (an arch's crown).</summary>
        public float Peak => Kind == RuinOpeningKind.Arch ? Top + ArchRadius : Top;
    }

    /// <summary>
    /// One straight wall of a ruin, from <see cref="A"/> to <see cref="B"/> on the plan (metres, the plan's own space),
    /// two bricks thick, standing on the plan's shared base. Its broken top is a height profile along it
    /// (<see cref="TopAt"/>): whole at <see cref="Height"/> (a gable rising over it, if it's a gable end), rough all along,
    /// falling away toward a broken free end, dipping where part of it has fallen (a breach), and a little higher by a
    /// corner (corners stand longest).
    /// </summary>
    public sealed class RuinRun
    {
        public Vector2 A, B;
        public float Height;
        /// <summary>A gable's rise over the middle of the run (0: none).</summary>
        public float Gable;
        /// <summary>A free end broken away: how far in the break reaches, and the height it falls to (0: a whole end).</summary>
        public float StartBreak, EndBreak, StartLow, EndLow;
        /// <summary>Another wall joins it at that end (a corner, or a partition meeting a wall).</summary>
        public bool JoinStart, JoinEnd;
        /// <summary>At a square corner, which courses run through it (0 the even ones, 1 the odd), so the two walls interlock.</summary>
        public int BondStart = -1, BondEnd = -1;
        /// <summary>Where part of the wall has fallen: centre, width and the height it's down to.</summary>
        public readonly List<Vector3> Breaches = new List<Vector3>();
        public readonly List<RuinOpening> Openings = new List<RuinOpening>();
        public bool Exterior = true;
        /// <summary>Its own noise and its bricks' colours.</summary>
        public int Seed;

        /// <summary>How much higher a wall stands by a corner (corners stand longest).</summary>
        public const float CornerRise = 0.7f;

        public float Length => Vector2.Distance(A, B);
        public Vector2 Dir => (B - A).normalized;

        /// <summary>The height the wall still stands to, <paramref name="x"/> metres along it.</summary>
        public float TopAt(float x)
        {
            float len = Length;
            float h = Height;
            if (Gable > 0f) h += Gable * (1f - Mathf.Abs(x / Mathf.Max(0.01f, len) * 2f - 1f));
            // Rough all along: a slow wander and a quicker one.
            h += (Noise(x / 1.4f, 0) - 0.5f) * 0.55f + (Noise(x / 0.45f, 1) - 0.5f) * 0.18f;
            // Corners stand longest.
            if (JoinStart) h += CornerRise * Mathf.Clamp01(1f - x / 1.1f);
            if (JoinEnd) h += CornerRise * Mathf.Clamp01(1f - (len - x) / 1.1f);
            if (StartBreak > 0f && x < StartBreak) h = Mathf.Lerp(StartLow, h, Ease(x / StartBreak)) + (Noise(x / 0.3f, 2) - 0.5f) * 0.2f;
            if (EndBreak > 0f && len - x < EndBreak) h = Mathf.Lerp(EndLow, h, Ease((len - x) / EndBreak)) + (Noise(x / 0.3f, 3) - 0.5f) * 0.2f;
            foreach (Vector3 b in Breaches)
            {
                float d = Mathf.Abs(x - b.x) / Mathf.Max(0.05f, b.y * 0.5f);
                if (d < 1.6f) h = Mathf.Min(h, Mathf.Lerp(b.z, h, Mathf.Clamp01(d - 0.6f)));
            }
            return Mathf.Max(0f, h);
        }

        static float Ease(float t) => t * t * (3f - 2f * t);

        /// <summary>Smooth value noise along the wall, 0-1.</summary>
        float Noise(float x, int channel)
        {
            int i = Mathf.FloorToInt(x);
            float f = x - i;
            float a = RuinHash.Value(Seed, channel, i, 0), b = RuinHash.Value(Seed, channel, i + 1, 0);
            return Mathf.Lerp(a, b, f * f * (3f - 2f * f));
        }

        /// <summary>The opening covering a point of the wall face (along it, height), if any; arches by their round heads.</summary>
        public bool InOpening(float x, float y, float pad = 0f)
        {
            foreach (RuinOpening o in Openings)
                if (Covers(o, x, y, pad)) return true;
            return false;
        }

        public static bool Covers(RuinOpening o, float x, float y, float pad = 0f)
        {
            float dx = Mathf.Abs(x - o.Center);
            if (y < o.Sill - pad) return false;
            if (o.Kind != RuinOpeningKind.Arch) return y < o.Top + pad && dx < o.Half + pad;
            if (y <= o.Top) return dx < o.Half + pad;
            float r = o.ArchRadius + pad, dy = y - o.Top;
            return dx * dx + dy * dy < r * r;
        }
    }

    /// <summary>
    /// The kinds of ruin, from a lone broken wall up to the shell of a building: the three last stand on every map, each
    /// with a stone floor (a big building of hallways, rooms and nooks; a plain rectangle, part of it fallen open; and half
    /// a rectangle's frame, an L or a U).
    /// </summary>
    public enum RuinArchetype { Fragment, Corner, Church, Warehouse, Residential, Complex, Enclosure, HalfFrame }

    /// <summary>How a stretch of a ruin's stone floor is laid.</summary>
    public enum FloorLaying
    {
        /// <summary>Flagstones of mixed lengths in courses (ashlar), the joints staggered.</summary>
        Flags,
        /// <summary>Square tiles in a grid.</summary>
        Grid,
        /// <summary>Square tiles, pale and dark in a chequer, inside a border of plain ones.</summary>
        Checker,
        /// <summary>A hallway's runner: slabs laid across it, staggered, between kerbs down both sides.</summary>
        Runner,
    }

    /// <summary>One stretch of a ruin's floor (a room, a hallway, a hall), on the plan, and how it's laid.</summary>
    public struct FloorZone
    {
        public Rect Area;
        public FloorLaying Laying;
        /// <summary>Worn paler down its middle by feet (0 none, 1 a hallway's).</summary>
        public float Wear;
        /// <summary>Open to the weather (more moss, more damp).</summary>
        public bool Exposed;
        /// <summary>
        /// A diagonal cut through the stretch (the rectangle's half): only the side holding <see cref="Keep"/> is floored,
        /// and its last row of stones runs raggedly along the line from <see cref="CutA"/> to <see cref="CutB"/>.
        /// </summary>
        public bool Cut;
        public Vector2 CutA, CutB, Keep;
        /// <summary>
        /// An outline the stretch is clipped to (wall centre lines: a church's apse), or null: only stones within it (or under
        /// its walls) are laid.
        /// </summary>
        public Vector2[] Clip;

        /// <summary>
        /// Whether a point is inside <see cref="Clip"/> (or within <paramref name="pad"/> of it; a negative pad: at least
        /// that far inside). Always, with no clip.
        /// </summary>
        public bool InClip(Vector2 p, float pad = 0f)
        {
            if (Clip == null || Clip.Length < 3) return true;
            bool inside = false;
            float d = float.MaxValue;
            for (int i = 0, j = Clip.Length - 1; i < Clip.Length; j = i++)
            {
                Vector2 a = Clip[i], b = Clip[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                Vector2 ab = b - a;
                float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                d = Mathf.Min(d, (a + ab * k - p).magnitude);
            }
            return pad >= 0f ? inside || d <= pad : inside && d >= -pad;
        }

        /// <summary>How far inside the kept side a point is, in metres (negative past the cut line); huge if there's no cut.</summary>
        public float Depth(Vector2 p)
        {
            if (!Cut) return float.MaxValue;
            Vector2 d = CutB - CutA, a = CutA;
            float len = Mathf.Max(1e-4f, d.magnitude);
            float Side(Vector2 q) => (d.x * (q.y - a.y) - d.y * (q.x - a.x)) / len;
            return Side(p) * Mathf.Sign(Side(Keep));
        }

        /// <summary>Whether a point is on this stretch (within <paramref name="pad"/> of it, past its cut line too).</summary>
        public bool Keeps(Vector2 p, float pad = 0f) =>
            p.x >= Area.xMin - pad && p.x <= Area.xMax + pad && p.y >= Area.yMin - pad && p.y <= Area.yMax + pad && (!Cut || Depth(p) >= -pad) && InClip(p, pad);
    }

    /// <summary>
    /// A ruin laid out on its own plan (metres, centred near the origin), placed on the map with a yaw: its walls (all
    /// upright, on one flat base: no stairs or ramps), its outline (for a building's shell: the area it encloses), and
    /// the widths of its inner passages and halls (for checks).
    /// </summary>
    public sealed class RuinPlan
    {
        public RuinArchetype Archetype;
        public readonly List<RuinRun> Runs = new List<RuinRun>();
        /// <summary>The enclosed outline of a building's shell (empty for a fragment or a corner).</summary>
        public readonly List<Vector2> Outline = new List<Vector2>();
        /// <summary>Inner hallways: width and length.</summary>
        public readonly List<Vector2> Hallways = new List<Vector2>();
        /// <summary>Gaps left as passages between rooms (their widths).</summary>
        public readonly List<float> Passages = new List<float>();
        /// <summary>The rooms walled off inside (for checks).</summary>
        public readonly List<Rect> Rooms = new List<Rect>();
        /// <summary>Nooks: little bays off a room (their mouths' widths and depths).</summary>
        public readonly List<Vector2> Nooks = new List<Vector2>();
        /// <summary>The stone floor, stretch by stretch (they tile the area inside the walls); none for most ruins.</summary>
        public readonly List<FloorZone> Floor = new List<FloorZone>();

        public bool Structure => Archetype == RuinArchetype.Church || Archetype == RuinArchetype.Warehouse || Archetype == RuinArchetype.Residential
            || Archetype == RuinArchetype.Complex || Archetype == RuinArchetype.Enclosure || Archetype == RuinArchetype.HalfFrame;

        /// <summary>Whether it has a stone floor laid inside its walls.</summary>
        public bool Floored => Floor.Count > 0;

        /// <summary>Whether a plan point is on its floor.</summary>
        public bool OnFloor(Vector2 p, float pad = 0f)
        {
            foreach (FloorZone z in Floor)
                if (z.Keeps(p, pad)) return true;
            return false;
        }

        /// <summary>Square corners where two of its walls meet, bonded.</summary>
        public int Corners
        {
            get
            {
                int n = 0;
                foreach (RuinRun r in Runs) n += (r.BondStart >= 0 ? 1 : 0) + (r.BondEnd >= 0 ? 1 : 0);
                return n / 2;
            }
        }

        /// <summary>
        /// Mirrors the whole plan across its x = 0 line (walls, outline, rooms, floor), for variety: every wall still runs
        /// from its start to its end, so its openings, breaks and bonds stay where they were along it.
        /// </summary>
        public void MirrorX()
        {
            Vector2 M(Vector2 p) => new Vector2(-p.x, p.y);
            Rect MR(Rect r) => Rect.MinMaxRect(-r.xMax, r.yMin, -r.xMin, r.yMax);
            foreach (RuinRun r in Runs) { r.A = M(r.A); r.B = M(r.B); }
            for (int i = 0; i < Outline.Count; i++) Outline[i] = M(Outline[i]);
            for (int i = 0; i < Rooms.Count; i++) Rooms[i] = MR(Rooms[i]);
            for (int i = 0; i < Floor.Count; i++) { FloorZone z = Floor[i]; z.Area = MR(z.Area); z.CutA = M(z.CutA); z.CutB = M(z.CutB); z.Keep = M(z.Keep); if (z.Clip != null) z.Clip = System.Array.ConvertAll(z.Clip, M); Floor[i] = z; }
        }

        /// <summary>The plan's extent (its walls' centre lines, padded by half a wall).</summary>
        public Rect Bounds
        {
            get
            {
                Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = -lo;
                foreach (RuinRun r in Runs)
                {
                    lo = Vector2.Min(lo, Vector2.Min(r.A, r.B));
                    hi = Vector2.Max(hi, Vector2.Max(r.A, r.B));
                }
                float t = LowPolyModels.WallThickness * 0.5f;
                return Rect.MinMaxRect(lo.x - t, lo.y - t, hi.x + t, hi.y + t);
            }
        }

        /// <summary>Whether a plan point is inside the shell's outline.</summary>
        public bool Inside(Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = Outline.Count - 1; i < Outline.Count; j = i++)
            {
                Vector2 a = Outline[i], b = Outline[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }
    }

    /// <summary>Stateless hashes, so a ruin's noise and colours depend only on its seed and where a brick is.</summary>
    public static class RuinHash
    {
        public static uint Hash(int a, int b, int c, int d)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h = (h ^ (uint)c) * 16777619u;
                h = (h ^ (uint)d) * 16777619u;
                h ^= h >> 15;
                h *= 0x2c1b3c6du;
                h ^= h >> 12;
                h *= 0x297a2d39u;
                h ^= h >> 15;
                return h;
            }
        }

        /// <summary>0-1.</summary>
        public static float Value(int a, int b, int c, int d) => (Hash(a, b, c, d) & 0xFFFFFF) / 16777215f;
    }
}
