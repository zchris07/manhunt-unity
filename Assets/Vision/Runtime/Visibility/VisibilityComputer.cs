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

        static readonly Unity.Profiling.ProfilerMarker QueryMarker = new Unity.Profiling.ProfilerMarker("Vision.VC.Query"),
            BinMarker = new Unity.Profiling.ProfilerMarker("Vision.VC.Bin"), CullMarker = new Unity.Profiling.ProfilerMarker("Vision.VC.Cull"),
            RayMarker = new Unity.Profiling.ProfilerMarker("Vision.VC.Rays");
        /// <summary>Diagnostics: segments near the last query, and how many survived the hidden-segment cull.</summary>
        public static int LastQueried, LastKept;

        readonly OccluderSet occluders;
        readonly List<int> segIds = new List<int>(512);
        readonly List<int> kept = new List<int>(512);
        /// <summary>Above this many nearby segments a coarse pass first drops the ones hidden behind nearer ones.</summary>
        public static int CullAbove = 400;
        const int CoarseRays = 1024;
        readonly float[] coarse = new float[CoarseRays];
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
                // A cone only needs the box round itself: the origin, its two edges and any compass extreme inside it.
                float minX = ox, maxX = ox, minY = oy, maxY = oy;
                if (full) { minX -= range; maxX += range; minY -= range; maxY += range; }
                else
                {
                    void Extend(float ang) { float px = ox + Mathf.Cos(ang) * range, py = oy + Mathf.Sin(ang) * range; minX = Mathf.Min(minX, px); maxX = Mathf.Max(maxX, px); minY = Mathf.Min(minY, py); maxY = Mathf.Max(maxY, py); }
                    Extend(start);
                    Extend(start + span);
                    for (int k = 0; k < 4; k++)
                        if (NormPositive(k * 0.5f * Mathf.PI - start) <= span) Extend(k * 0.5f * Mathf.PI);
                }
                QueryMarker.Begin();
                occluders.Query(minX, minY, maxX, maxY, segIds);
                QueryMarker.End();
                BinMarker.Begin();
                seg = occluders.Packed;
                kept.Clear();
                for (int i = 0; i < segIds.Count; i++)
                {
                    int o = segIds[i] * 4;
                    if (SegmentDistance2(seg[o] - ox, seg[o + 1] - oy, seg[o + 2] - ox, seg[o + 3] - oy) <= range2) kept.Add(segIds[i]);
                }
                for (int i = 0; i < kept.Count; i++)
                {
                    int o = kept[i] * 4;
                    BinSegment(kept[i], seg[o] - ox, seg[o + 1] - oy, seg[o + 2] - ox, seg[o + 3] - oy);
                }
                BinMarker.End();
                LastQueried = segIds.Count;
                CullMarker.Begin();
                if (kept.Count > CullAbove) CullHidden(ox, oy, range, seg, full ? 0f : start, full ? 2f * Mathf.PI : span);
                CullMarker.End();
                LastKept = kept.Count;
                for (int i = 0; i < kept.Count; i++)
                {
                    int o = kept[i] * 4;
                    float ax = seg[o] - ox, ay = seg[o + 1] - oy, bx = seg[o + 2] - ox, by = seg[o + 3] - oy;
                    if (ax * ax + ay * ay < range2) AddEndpointRays(Mathf.Atan2(ay, ax), start, span, full);
                    if (bx * bx + by * by < range2) AddEndpointRays(Mathf.Atan2(by, bx), start, span, full);
                }
            }

            int arcRays = Mathf.Max(1, Mathf.CeilToInt(span / ArcStep));
            int arcLast = full ? arcRays - 1 : arcRays;
            for (int i = 0; i <= arcLast; i++) PushAngle(span * i / arcRays);

            RayMarker.Begin();
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
            RayMarker.End();
        }

        /// <summary>
        /// Drops from <see cref="kept"/> every segment that lies wholly behind what a fan of coarse rays already hits: its
        /// corners would only cast rays that stop short of it. Cheap (a thousand rays) and it keeps the exact pass to the
        /// segments that can shape the polygon, so cost follows what is visible rather than everything within range.
        /// The bins stay as they are (hidden segments never shorten a ray).
        /// </summary>
        void CullHidden(float ox, float oy, float range, float[] seg, float from, float span)
        {
            const float step = 2f * Mathf.PI / CoarseRays;
            // Only the directions the query covers get a coarse ray; the rest count as open (nothing is dropped there).
            for (int r = 0; r < CoarseRays; r++)
            {
                float a = (r + 0.5f) * step;
                if (NormPositive(a - from) > span + 2f * step) { coarse[r] = range; continue; }
                float dx = Mathf.Cos(a), dy = Mathf.Sin(a), t = range;
                List<int> bin = bins[BinOf(a)];
                for (int k = 0; k < bin.Count; k++)
                {
                    int o = bin[k] * 4;
                    float hit = RaySegment(ox, oy, dx, dy, seg[o], seg[o + 1], seg[o + 2], seg[o + 3]);
                    if (hit < t) t = hit;
                }
                coarse[r] = t;
            }
            int w = 0;
            for (int i = 0; i < kept.Count; i++)
            {
                int o = kept[i] * 4;
                float ax = seg[o] - ox, ay = seg[o + 1] - oy, bx = seg[o + 2] - ox, by = seg[o + 3] - oy;
                float a1 = NormPositive(Mathf.Atan2(ay, ax)), a2 = NormPositive(Mathf.Atan2(by, bx));
                float diff = a2 - a1;
                if (diff > Mathf.PI) diff -= 2f * Mathf.PI;
                else if (diff < -Mathf.PI) diff += 2f * Mathf.PI;
                float lo = diff >= 0f ? a1 : a2;
                // The coarse rays either side of the segment's silhouette (one more each way for safety).
                int r0 = Mathf.FloorToInt(lo / step - 0.5f) - 1, r1 = Mathf.CeilToInt((lo + Mathf.Abs(diff)) / step - 0.5f) + 1;
                if (r1 - r0 >= CoarseRays) { kept[w++] = kept[i]; continue; }
                float reach = 0f;
                for (int r = r0; r <= r1; r++) reach = Mathf.Max(reach, coarse[((r % CoarseRays) + CoarseRays) % CoarseRays]);
                float near = Mathf.Sqrt(SegmentDistance2(ax, ay, bx, by));
                if (near <= reach + 0.05f) kept[w++] = kept[i];
            }
            kept.RemoveRange(w, kept.Count - w);
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
