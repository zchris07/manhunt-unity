using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vision.Visibility
{
    /// <summary>One field-of-view query on the ground plane (2D x,y = world X,Z).</summary>
    public struct ViewQuery
    {
        public Vector2 Origin;
        /// <summary>Facing angle in radians (atan2(y, x)).</summary>
        public float Dir;
        /// <summary>Half of the cone's opening angle in radians. PI or more means a full circle.</summary>
        public float HalfAngle;
        public float Range;
        /// <summary>False for the see-through cone, which ignores every occluder.</summary>
        public bool UseOccluders;

        public bool IsFull => HalfAngle >= Mathf.PI - 1e-5f;

        public static ViewQuery Cone(Vector2 origin, float dir, float halfAngle, float range, bool useOccluders = true) =>
            new ViewQuery { Origin = origin, Dir = dir, HalfAngle = halfAngle, Range = range, UseOccluders = useOccluders };

        public static ViewQuery Circle(Vector2 origin, float range, bool useOccluders = true) =>
            new ViewQuery { Origin = origin, Dir = 0f, HalfAngle = Mathf.PI, Range = range, UseOccluders = useOccluders };
    }

    /// <summary>
    /// Builds 2D field-of-view polygons with an angular sweep. Rays are cast at every nearby segment
    /// endpoint and at +/- epsilon around it (so the polygon wraps around corners), plus evenly spaced
    /// rays that trace the outer arc. Hits are sorted by angle. Each ray is only tested against the
    /// segments whose angular interval covers it (256 angular bins), so cost scales with local density.
    ///
    /// Output: for a cone, the origin followed by the hit points in angular order. For a full circle,
    /// the ring of hit points (the origin is NOT included; triangulate it as a fan around the origin).
    /// </summary>
    public sealed class VisibilityComputer
    {
        const int Bins = 256;
        const float BinWidth = 2f * Mathf.PI / Bins;
        public const float Epsilon = 1e-4f;
        const float ArcStep = 3f * Mathf.Deg2Rad;

        readonly OccluderSet occluders;
        readonly List<int> segIds = new List<int>(512);
        readonly List<int>[] bins = new List<int>[Bins];
        float[] angles = new float[2048];
        int angleCount;

        /// <summary>Diagnostics: rays cast by the last call.</summary>
        public int LastRayCount { get; private set; }

        public VisibilityComputer(OccluderSet occluders)
        {
            this.occluders = occluders;
            for (int i = 0; i < Bins; i++) bins[i] = new List<int>(16);
        }

        public void Compute(in ViewQuery q, List<Vector2> output)
        {
            output.Clear();
            bool full = q.IsFull;
            float start = full ? q.Dir - Mathf.PI : q.Dir - q.HalfAngle;
            float span = full ? 2f * Mathf.PI : 2f * q.HalfAngle;
            float ox = q.Origin.x, oy = q.Origin.y, range = q.Range;
            float range2 = range * range;
            angleCount = 0;
            for (int b = 0; b < Bins; b++) bins[b].Clear();

            float[] seg = null;
            if (q.UseOccluders)
            {
                occluders.Query(ox - range, oy - range, ox + range, oy + range, segIds);
                seg = occluders.Packed;
                for (int i = 0; i < segIds.Count; i++)
                {
                    int o = segIds[i] * 4;
                    float ax = seg[o] - ox, ay = seg[o + 1] - oy, bx = seg[o + 2] - ox, by = seg[o + 3] - oy;
                    if (SegmentDistance2(ax, ay, bx, by) > range2) continue;
                    BinSegment(segIds[i], ax, ay, bx, by);
                    if (ax * ax + ay * ay < range2) AddEndpointRays(Mathf.Atan2(ay, ax), start, span, full);
                    if (bx * bx + by * by < range2) AddEndpointRays(Mathf.Atan2(by, bx), start, span, full);
                }
            }

            int arcRays = Mathf.Max(1, Mathf.CeilToInt(span / ArcStep));
            int arcLast = full ? arcRays - 1 : arcRays;
            for (int i = 0; i <= arcLast; i++) PushAngle(span * i / arcRays);

            Array.Sort(angles, 0, angleCount);

            if (!full) output.Add(q.Origin);
            float prev = float.NegativeInfinity;
            int rays = 0;
            for (int i = 0; i < angleCount; i++)
            {
                float rel = angles[i];
                if (rel - prev < 1e-6f) continue;
                prev = rel;
                float a = start + rel;
                float dx = Mathf.Cos(a), dy = Mathf.Sin(a);
                float t = range;
                if (seg != null)
                {
                    List<int> bin = bins[BinOf(a)];
                    for (int k = 0; k < bin.Count; k++)
                    {
                        int o = bin[k] * 4;
                        float hit = RaySegment(ox, oy, dx, dy, seg[o], seg[o + 1], seg[o + 2], seg[o + 3]);
                        if (hit < t) t = hit;
                    }
                }
                output.Add(new Vector2(ox + dx * t, oy + dy * t));
                rays++;
            }
            LastRayCount = rays;
        }

        void AddEndpointRays(float angle, float start, float span, bool full)
        {
            float rel = NormPositive(angle - start);
            for (int k = -1; k <= 1; k++)
            {
                float r = rel + k * Epsilon;
                if (full)
                {
                    if (r < 0f) r += 2f * Mathf.PI;
                    else if (r >= 2f * Mathf.PI) r -= 2f * Mathf.PI;
                }
                else if (r < 0f || r > span)
                {
                    continue;
                }
                PushAngle(r);
            }
        }

        void PushAngle(float rel)
        {
            if (angleCount == angles.Length) Array.Resize(ref angles, angles.Length * 2);
            angles[angleCount++] = rel;
        }

        /// <summary>Inserts a segment into every angular bin its silhouette covers, seen from the origin.</summary>
        void BinSegment(int id, float ax, float ay, float bx, float by)
        {
            // A segment passing (almost) through the origin can block any direction.
            if (SegmentDistance2(ax, ay, bx, by) < 1e-6f)
            {
                for (int b = 0; b < Bins; b++) bins[b].Add(id);
                return;
            }
            float a1 = NormPositive(Mathf.Atan2(ay, ax));
            float a2 = NormPositive(Mathf.Atan2(by, bx));
            float diff = a2 - a1;
            if (diff > Mathf.PI) diff -= 2f * Mathf.PI;
            else if (diff < -Mathf.PI) diff += 2f * Mathf.PI;
            float lo = diff >= 0f ? a1 : a2;
            float width = Mathf.Abs(diff);
            const float pad = 2e-3f;
            int b0 = Mathf.FloorToInt((lo - pad) / BinWidth);
            int b1 = Mathf.FloorToInt((lo + width + pad) / BinWidth);
            if (b1 - b0 >= Bins) b1 = b0 + Bins - 1;
            for (int b = b0; b <= b1; b++) bins[((b % Bins) + Bins) % Bins].Add(id);
        }

        static int BinOf(float angle)
        {
            int b = (int)(NormPositive(angle) / BinWidth);
            return b >= Bins ? Bins - 1 : b;
        }

        static float NormPositive(float a)
        {
            const float tau = 2f * Mathf.PI;
            a %= tau;
            return a < 0f ? a + tau : a;
        }

        /// <summary>Distance along the ray (o + t*d) to the segment, or +infinity on a miss.</summary>
        public static float RaySegment(float ox, float oy, float dx, float dy, float ax, float ay, float bx, float by)
        {
            float ex = bx - ax, ey = by - ay;
            float denom = dx * ey - dy * ex;
            if (Mathf.Abs(denom) < 1e-9f) return float.PositiveInfinity;
            float wx = ax - ox, wy = ay - oy;
            float t = (wx * ey - wy * ex) / denom;
            float s = (wx * dy - wy * dx) / denom;
            if (t < 0f || s < -1e-6f || s > 1f + 1e-6f) return float.PositiveInfinity;
            return t;
        }

        /// <summary>Squared distance from the origin to segment AB (both relative to the origin).</summary>
        static float SegmentDistance2(float ax, float ay, float bx, float by)
        {
            float ex = bx - ax, ey = by - ay;
            float len2 = ex * ex + ey * ey;
            float s = len2 > 0f ? Mathf.Clamp01(-(ax * ex + ay * ey) / len2) : 0f;
            float px = ax + ex * s, py = ay + ey * s;
            return px * px + py * py;
        }
    }
}
