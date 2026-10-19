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

    /// <summary>The kinds of ruin, from a lone broken wall up to the shell of a building.</summary>
    public enum RuinArchetype { Fragment, Corner, Church, Warehouse, Residential }

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

        public bool Structure => Archetype == RuinArchetype.Church || Archetype == RuinArchetype.Warehouse || Archetype == RuinArchetype.Residential;

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
