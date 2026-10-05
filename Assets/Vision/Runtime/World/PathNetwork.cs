using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// Footpaths between points of interest, generated from the terrain: the points are joined by a minimum
    /// spanning tree, each link is routed with A* over a grid whose step cost rises steeply with slope (so
    /// paths wind along contours instead of straight over hills), then smoothed with Chaikin's corner cutting.
    /// Each path point keeps a ground height smoothed along the path, which the terrain flattens to across the
    /// path's width. Deterministic: same terrain and points, same paths.
    /// </summary>
    public sealed class PathNetwork
    {
        public readonly List<List<Vector2>> Paths = new List<List<Vector2>>();
        readonly List<List<float>> heights = new List<List<float>>();
        public float HalfWidth = 0.8f;

        const float HashCell = 4f;
        readonly Dictionary<Vector2Int, List<(int path, int index)>> hash = new Dictionary<Vector2Int, List<(int, int)>>();

        /// <param name="height">Ground height before the paths flatten it.</param>
        /// <param name="limit">Paths stay within ±limit on both axes.</param>
        /// <param name="blocked">Cells a path must not cross (buildings).</param>
        public static PathNetwork Build(IReadOnlyList<Vector2> points, Func<float, float, float> height, float limit, Func<Vector2, bool> blocked, float step = 1f)
        {
            var net = new PathNetwork();
            if (points.Count < 2) return net;
            foreach ((int a, int b) in SpanningTree(points))
            {
                List<Vector2> route = Route(points[a], points[b], height, limit, blocked, step);
                if (route.Count < 2) continue;
                route = Chaikin(Chaikin(route));
                net.Add(route, height);
            }
            return net;
        }

        void Add(List<Vector2> route, Func<float, float, float> height)
        {
            var h = new List<float>(route.Count);
            foreach (Vector2 p in route) h.Add(height(p.x, p.y));
            // Smooth the heights along the path so it climbs evenly.
            for (int pass = 0; pass < 4; pass++)
            {
                var s = new List<float>(h);
                for (int i = 1; i < h.Count - 1; i++) s[i] = (h[i - 1] + 2f * h[i] + h[i + 1]) * 0.25f;
                h = s;
            }
            int id = Paths.Count;
            Paths.Add(route);
            heights.Add(h);
            for (int i = 0; i < route.Count - 1; i++)
            {
                Vector2 a = route[i], b = route[i + 1];
                Vector2Int lo = Cell(Vector2.Min(a, b) - Vector2.one * HashCell), hi = Cell(Vector2.Max(a, b) + Vector2.one * HashCell);
                for (int y = lo.y; y <= hi.y; y++)
                    for (int x = lo.x; x <= hi.x; x++)
                    {
                        var k = new Vector2Int(x, y);
                        if (!hash.TryGetValue(k, out var list)) hash[k] = list = new List<(int, int)>();
                        list.Add((id, i));
                    }
            }
        }

        static Vector2Int Cell(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / HashCell), Mathf.FloorToInt(p.y / HashCell));

        /// <summary>Distance to the nearest path centre line (large when none is near), and the path's ground height there.</summary>
        /// <summary>
        /// Draws every path segment into a distance grid (and the path's height at the nearest point): cells within
        /// <paramref name="reach"/> of a path get their distance, the rest stay at <paramref name="far"/>. One pass over
        /// the segments instead of a search per point.
        /// </summary>
        public void Rasterize(float min, float step, int n, float reach, float far, float[] distance, float[] height)
        {
            for (int k = 0; k < distance.Length; k++) { distance[k] = far; height[k] = 0f; }
            for (int p = 0; p < Paths.Count; p++)
            {
                List<Vector2> path = Paths[p];
                List<float> hs = heights[p];
                for (int s = 0; s < path.Count - 1; s++)
                {
                    Vector2 a = path[s], b = path[s + 1], ab = b - a;
                    float len2 = Mathf.Max(1e-6f, ab.sqrMagnitude);
                    int i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach - min) / step));
                    int i1 = Mathf.Min(n - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + reach - min) / step));
                    int j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - reach - min) / step));
                    int j1 = Mathf.Min(n - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + reach - min) / step));
                    for (int j = j0; j <= j1; j++)
                    {
                        float z = min + j * step;
                        for (int i = i0; i <= i1; i++)
                        {
                            var q = new Vector2(min + i * step, z);
                            float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2);
                            float d = (q - (a + ab * t)).magnitude;
                            int k = j * n + i;
                            if (d >= distance[k]) continue;
                            distance[k] = d;
                            height[k] = Mathf.Lerp(hs[s], hs[s + 1], t);
                        }
                    }
                }
            }
        }

        public float Distance(float x, float z, out float pathHeight)
        {
            pathHeight = 0f;
            float best = float.MaxValue;
            var p = new Vector2(x, z);
            if (!hash.TryGetValue(Cell(p), out var list)) return best;
            foreach ((int path, int i) in list)
            {
                Vector2 a = Paths[path][i], b = Paths[path][i + 1];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                float d = (p - (a + ab * t)).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    pathHeight = Mathf.Lerp(heights[path][i], heights[path][i + 1], t);
                }
            }
            return Mathf.Sqrt(best);
        }

        public float Distance(Vector2 p) => Distance(p.x, p.y, out _);

        /// <summary>Total length of all paths.</summary>
        public float Length
        {
            get
            {
                float l = 0f;
                foreach (var path in Paths)
                    for (int i = 0; i < path.Count - 1; i++) l += Vector2.Distance(path[i], path[i + 1]);
                return l;
            }
        }

        // ------------------------------------------------------------------ construction

        static List<(int, int)> SpanningTree(IReadOnlyList<Vector2> pts)
        {
            var edges = new List<(int, int)>();
            var inTree = new bool[pts.Count];
            inTree[0] = true;
            for (int added = 1; added < pts.Count; added++)
            {
                float best = float.MaxValue;
                int ba = -1, bb = -1;
                for (int a = 0; a < pts.Count; a++)
                {
                    if (!inTree[a]) continue;
                    for (int b = 0; b < pts.Count; b++)
                    {
                        if (inTree[b]) continue;
                        float d = (pts[a] - pts[b]).sqrMagnitude;
                        if (d < best) { best = d; ba = a; bb = b; }
                    }
                }
                inTree[bb] = true;
                edges.Add((ba, bb));
            }
            return edges;
        }

        /// <summary>A* on a grid: cost = step length x (1 + 12 grade²), blocked cells excluded.</summary>
        static List<Vector2> Route(Vector2 from, Vector2 to, Func<float, float, float> height, float limit, Func<Vector2, bool> blocked, float step)
        {
            int n = Mathf.CeilToInt(2f * limit / step) + 1;
            Vector2Int ToCell(Vector2 p) => new Vector2Int(Mathf.Clamp(Mathf.RoundToInt((p.x + limit) / step), 0, n - 1), Mathf.Clamp(Mathf.RoundToInt((p.y + limit) / step), 0, n - 1));
            Vector2 ToPoint(int i, int j) => new Vector2(-limit + i * step, -limit + j * step);
            Vector2Int s = ToCell(from), g = ToCell(to);
            var cost = new float[n * n];
            var parent = new int[n * n];
            var closed = new bool[n * n];
            var h = new float[n * n];
            for (int k = 0; k < cost.Length; k++) { cost[k] = float.MaxValue; parent[k] = -1; h[k] = float.NaN; }
            float H(int i, int j)
            {
                int k = j * n + i;
                if (float.IsNaN(h[k])) h[k] = height(-limit + i * step, -limit + j * step);
                return h[k];
            }
            var open = new MinHeap();
            int start = s.y * n + s.x, goal = g.y * n + g.x;
            cost[start] = 0f;
            open.Push(start, Vector2.Distance(from, to));
            int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 }, dy = { 0, 0, 1, -1, 1, -1, 1, -1 };
            while (open.Count > 0)
            {
                int cur = open.Pop();
                if (closed[cur]) continue;
                closed[cur] = true;
                if (cur == goal) break;
                int ci = cur % n, cj = cur / n;
                for (int d = 0; d < 8; d++)
                {
                    int ni = ci + dx[d], nj = cj + dy[d];
                    if (ni < 0 || nj < 0 || ni >= n || nj >= n) continue;
                    int nk = nj * n + ni;
                    if (closed[nk]) continue;
                    Vector2 np = ToPoint(ni, nj);
                    if (nk != goal && blocked(np)) continue;
                    float len = d < 4 ? step : step * 1.41421356f;
                    float grade = (H(ni, nj) - H(ci, cj)) / len;
                    float c = cost[cur] + len * (1f + 12f * grade * grade);
                    if (c >= cost[nk]) continue;
                    cost[nk] = c;
                    parent[nk] = cur;
                    open.Push(nk, c + Vector2.Distance(np, to));
                }
            }
            var route = new List<Vector2>();
            if (parent[goal] < 0 && goal != start) return route;
            for (int k = goal; k >= 0; k = parent[k]) route.Add(ToPoint(k % n, k / n));
            route.Reverse();
            route[0] = from;
            route[route.Count - 1] = to;
            return route;
        }

        static List<Vector2> Chaikin(List<Vector2> pts)
        {
            if (pts.Count < 3) return pts;
            var o = new List<Vector2>(pts.Count * 2) { pts[0] };
            for (int i = 0; i < pts.Count - 1; i++)
            {
                o.Add(Vector2.Lerp(pts[i], pts[i + 1], 0.25f));
                o.Add(Vector2.Lerp(pts[i], pts[i + 1], 0.75f));
            }
            o.Add(pts[pts.Count - 1]);
            return o;
        }

        sealed class MinHeap
        {
            readonly List<(int id, float key)> items = new List<(int, float)>();
            public int Count => items.Count;

            public void Push(int id, float key)
            {
                items.Add((id, key));
                int i = items.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (items[p].key <= items[i].key) break;
                    (items[p], items[i]) = (items[i], items[p]);
                    i = p;
                }
            }

            public int Pop()
            {
                int top = items[0].id;
                items[0] = items[items.Count - 1];
                items.RemoveAt(items.Count - 1);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < items.Count && items[l].key < items[m].key) m = l;
                    if (r < items.Count && items[r].key < items[m].key) m = r;
                    if (m == i) break;
                    (items[m], items[i]) = (items[i], items[m]);
                    i = m;
                }
                return top;
            }
        }
    }
}
