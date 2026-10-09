using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// Footpaths between points of interest, generated from the terrain as the original does: the points are joined by
    /// a minimum spanning tree (plus a few loops), each link routed with A* over a grid whose step cost rises steeply
    /// with slope (so paths wind along contours instead of straight over hills), then smoothed with Chaikin's corner
    /// cutting. With a <see cref="Web"/> the routes also wander (a seeded noise field tugs them aside, and a gentle
    /// meander bends them), and a web of narrower trails grows over the main ones: junctions out in the woods tied into
    /// the network (some by two trails), and cross-links where two paths pass near each other. Every path has its own
    /// width and look (packed dirt, gravel, leaf litter, moss, clay, rutted mud). Each path point keeps a ground height
    /// smoothed along the path, which the terrain flattens to across the path's width. Deterministic: same terrain,
    /// points and seed, same paths.
    /// </summary>
    public sealed class PathNetwork
    {
        public readonly List<List<Vector2>> Paths = new List<List<Vector2>>();
        readonly List<List<float>> heights = new List<List<float>>();
        readonly List<List<float>> arcs = new List<List<float>>();
        /// <summary>The main paths' half-width (the widest; narrower trails keep their own in <see cref="Infos"/>).</summary>
        public float HalfWidth = 0.8f;

        /// <summary>How a path looks: packed dirt, gravel, leaf litter, moss, clay, rutted mud.</summary>
        public enum Style { Dirt, Gravel, Leafy, Mossy, Clay, Mud }

        /// <summary>One path's character: its width (a fraction of <see cref="HalfWidth"/>), its two looks, whether it is a main path.</summary>
        public struct Info
        {
            public float Width;
            public Style Look, Second;
            public bool Main;
            public float Seed;
        }

        public readonly List<Info> Infos = new List<Info>();

        /// <summary>How the web of trails grows over the main paths.</summary>
        public sealed class Web
        {
            public int Seed;
            /// <summary>Extra links that close loops in the spanning tree (the original's three).</summary>
            public int Loops = 3;
            /// <summary>Junctions out in the woods tied into the network.</summary>
            public int Junctions = 11;
            /// <summary>Trails between paths that pass near each other.</summary>
            public int CrossLinks = 7;
            /// <summary>How hard the routes are tugged aside by the noise field, and how far they meander (design units).</summary>
            public float Wander = 1.4f, Meander = 1.6f;
        }

        const float HashCell = 4f;
        readonly Dictionary<Vector2Int, List<(int path, int index)>> hash = new Dictionary<Vector2Int, List<(int, int)>>();

        /// <summary>A path's half-width at a point along it (it swells and narrows a little).</summary>
        public float HalfWidthAt(int path, float along)
        {
            if (path < 0 || path >= Infos.Count) return HalfWidth;
            Info i = Infos[path];
            float n = Mathf.PerlinNoise(along * 0.11f + i.Seed, i.Seed * 0.37f) - 0.5f;
            return HalfWidth * i.Width * (1f + 0.36f * n);
        }

        /// <param name="height">Ground height before the paths flatten it.</param>
        /// <param name="limit">Paths stay within ±limit on both axes.</param>
        /// <param name="blocked">Cells a path must not cross (buildings).</param>
        /// <param name="web">How the trails wander and the web grows over them (null: the plain spanning tree).</param>
        public static PathNetwork Build(IReadOnlyList<Vector2> points, Func<float, float, float> height, float limit, Func<Vector2, bool> blocked, float step = 1f, Web web = null)
        {
            var net = new PathNetwork();
            if (points.Count < 2) return net;
            System.Random rnd = web != null ? new System.Random(web.Seed) : null;
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            Style Pick(params Style[] s) => s[rnd.Next(s.Length)];
            Vector2 noise = web != null ? new Vector2(R(0f, 500f), R(0f, 500f)) : Vector2.zero;
            float wander = web != null ? web.Wander : 0f;

            List<Vector2> Shape(List<Vector2> route, float meander)
            {
                if (web == null || route.Count < 3) return Chaikin(Chaikin(route));
                route = Resample(route, 0.8f);
                route = Meander(route, meander, R(0f, 1000f), blocked);
                return Chaikin(Chaikin(Relax(route, 3)));
            }
            void Link(Vector2 a, Vector2 b, Info info, float meander)
            {
                List<Vector2> route = Route(a, b, height, limit, blocked, step, wander, noise);
                if (route.Count < 2) return;
                net.Add(Shape(route, meander), height, info);
            }
            Info MainInfo() => web == null ? new Info { Width = 1f, Look = Style.Dirt, Second = Style.Dirt, Main = true }
                : new Info { Width = R(0.92f, 1.05f), Look = Pick(Style.Dirt, Style.Dirt, Style.Clay), Second = Pick(Style.Gravel, Style.Mud, Style.Leafy, Style.Mossy), Main = true, Seed = R(0f, 100f) };
            Info TrailInfo() => new Info
            {
                Width = R(0.42f, 0.66f), Look = Pick(Style.Dirt, Style.Leafy, Style.Leafy, Style.Mossy, Style.Gravel, Style.Mud),
                Second = Pick(Style.Leafy, Style.Mossy, Style.Dirt), Seed = R(0f, 100f),
            };

            // The backbone: a spanning tree over the points of interest, and (like the original) a few loops.
            List<(int, int)> edges = SpanningTree(points);
            if (web != null)
            {
                var extra = new List<(float d, int a, int b)>();
                for (int a = 0; a < points.Count; a++)
                    for (int b = a + 1; b < points.Count; b++)
                    {
                        if (edges.Contains((a, b)) || edges.Contains((b, a))) continue;
                        float d = Vector2.Distance(points[a], points[b]);
                        if (d < 78f && !blocked(Vector2.Lerp(points[a], points[b], 0.5f))) extra.Add((d, a, b));
                    }
                extra.Sort((x, y) => x.d.CompareTo(y.d));
                int loops = 0;
                foreach (var (_, a, b) in extra)
                {
                    if (loops >= web.Loops) break;
                    if (rnd.NextDouble() > 0.3) continue;
                    edges.Add((a, b));
                    loops++;
                }
            }
            foreach ((int a, int b) in edges) Link(points[a], points[b], MainInfo(), web != null ? web.Meander : 0f);
            if (web == null) return net;

            // The web: junctions out in the woods, each tied into the network, some by a second trail.
            int mains = net.Paths.Count;
            var junctions = new List<Vector2>();
            for (int tries = 0; tries < 600 && junctions.Count < web.Junctions; tries++)
            {
                var p = new Vector2(R(-limit * 0.88f, limit * 0.88f), R(-limit * 0.88f, limit * 0.88f));
                if (blocked(p)) continue;
                float d = net.Distance(p);
                if (d < 9f || d > 34f) continue;
                if (junctions.Exists(q => Vector2.Distance(q, p) < 18f)) continue;
                junctions.Add(p);
            }
            foreach (Vector2 j in junctions)
            {
                if (!net.Nearest(j, -1, out Vector2 q, out int first)) continue;
                Info info = TrailInfo();
                Link(j, q, info, web.Meander * 0.7f);
                if (rnd.NextDouble() < 0.6)
                {
                    // A second way out: to another path, or to the next junction along.
                    Vector2 other = default;
                    bool found = false;
                    if (net.Nearest(j, first, out Vector2 q2, out _) && Vector2.Distance(q2, j) < 40f && Vector2.Distance(q2, q) > 10f) { other = q2; found = true; }
                    else
                        foreach (Vector2 k in junctions)
                            if (k != j && Vector2.Distance(k, j) < 36f) { other = k; found = true; break; }
                    if (found) Link(j, other, info, web.Meander * 0.7f);
                }
            }

            // Cross-links: where two paths pass close to each other, a short trail joins them.
            var candidates = new List<(Vector2 a, Vector2 b)>();
            for (int i = 0; i < mains; i++)
            {
                List<Vector2> path = net.Paths[i];
                float run = 0f;
                for (int k = 1; k < path.Count; k++)
                {
                    run += Vector2.Distance(path[k - 1], path[k]);
                    if (run < 14f) continue;
                    run = 0f;
                    if (!net.Nearest(path[k], i, out Vector2 q, out _)) continue;
                    float d = Vector2.Distance(q, path[k]);
                    if (d < 9f || d > 26f || blocked(Vector2.Lerp(path[k], q, 0.5f))) continue;
                    candidates.Add((path[k], q));
                }
            }
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int k = rnd.Next(i + 1);
                (candidates[i], candidates[k]) = (candidates[k], candidates[i]);
            }
            var chosen = new List<Vector2>();
            foreach (var (a, b) in candidates)
            {
                if (chosen.Count >= web.CrossLinks) break;
                Vector2 mid = (a + b) * 0.5f;
                if (chosen.Exists(m => Vector2.Distance(m, mid) < 22f)) continue;
                chosen.Add(mid);
                Link(a, b, TrailInfo(), web.Meander * 0.5f);
            }
            return net;
        }

        void Add(List<Vector2> route, Func<float, float, float> height, Info info)
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
            Infos.Add(info);
            var arc = new List<float>(route.Count) { 0f };
            for (int i = 1; i < route.Count; i++) arc.Add(arc[i - 1] + Vector2.Distance(route[i - 1], route[i]));
            arcs.Add(arc);
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

        /// <summary>
        /// Draws every path segment into a distance grid (and the path's height at the nearest point, which path it is and
        /// how far along it): cells within <paramref name="reach"/> of a path get their distance, the rest stay at
        /// <paramref name="far"/>. One pass over the segments instead of a search per point.
        /// </summary>
        public void Rasterize(float min, float step, int n, float reach, float far, float[] distance, float[] height, int[] which = null, float[] along = null)
        {
            for (int k = 0; k < distance.Length; k++) { distance[k] = far; height[k] = 0f; }
            if (which != null) for (int k = 0; k < which.Length; k++) which[k] = -1;
            for (int p = 0; p < Paths.Count; p++)
            {
                List<Vector2> path = Paths[p];
                List<float> hs = heights[p];
                List<float> arc = arcs[p];
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
                            if (which != null) which[k] = p;
                            if (along != null) along[k] = Mathf.Lerp(arc[s], arc[s + 1], t);
                        }
                    }
                }
            }
        }

        /// <summary>Distance to the nearest path centre line (large when none is near), and the path's ground height there.</summary>
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

        /// <summary>The nearest point on any path (but <paramref name="exclude"/>), and which path it is on.</summary>
        public bool Nearest(Vector2 p, int exclude, out Vector2 point, out int path)
        {
            point = p;
            path = -1;
            float best = float.MaxValue;
            for (int i = 0; i < Paths.Count; i++)
            {
                if (i == exclude) continue;
                List<Vector2> pts = Paths[i];
                for (int k = 0; k < pts.Count - 1; k++)
                {
                    Vector2 a = pts[k], ab = pts[k + 1] - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                    Vector2 q = a + ab * t;
                    float d = (q - p).sqrMagnitude;
                    if (d < best) { best = d; point = q; path = i; }
                }
            }
            return path >= 0;
        }

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

        /// <summary>
        /// A* on a grid: cost = step length x (1 + 12 grade² + a wandering tug from a noise field), blocked cells excluded.
        /// </summary>
        static List<Vector2> Route(Vector2 from, Vector2 to, Func<float, float, float> height, float limit, Func<Vector2, bool> blocked, float step,
            float wander = 0f, Vector2 noise = default)
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
                    // A noise field tugs the route aside: it wanders through the woods rather than running straight.
                    float tug = wander > 0f ? wander * Mathf.PerlinNoise(np.x * 0.07f + noise.x, np.y * 0.07f + noise.y) : 0f;
                    float c = cost[cur] + len * (1f + 12f * grade * grade + tug);
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

        /// <summary>Even spacing along a polyline.</summary>
        static List<Vector2> Resample(List<Vector2> pts, float spacing)
        {
            var o = new List<Vector2> { pts[0] };
            float carry = 0f;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                float len = Vector2.Distance(a, b);
                if (len < 1e-5f) continue;
                float t = spacing - carry;
                while (t <= len)
                {
                    o.Add(Vector2.Lerp(a, b, t / len));
                    t += spacing;
                }
                carry = len - (t - spacing);
            }
            if ((o[o.Count - 1] - pts[pts.Count - 1]).sqrMagnitude > 1e-4f) o.Add(pts[pts.Count - 1]);
            return o;
        }

        /// <summary>
        /// A gentle sideways meander along the route (two noise octaves along its length), fading out toward both ends so
        /// it still meets them; a bend that would run into something stays where it was.
        /// </summary>
        static List<Vector2> Meander(List<Vector2> pts, float amp, float seed, Func<Vector2, bool> blocked)
        {
            if (amp <= 0f || pts.Count < 3) return pts;
            float total = 0f;
            var along = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++) along[i] = total += Vector2.Distance(pts[i - 1], pts[i]);
            var o = new List<Vector2>(pts);
            for (int i = 1; i < pts.Count - 1; i++)
            {
                Vector2 d = (pts[i + 1] - pts[i - 1]).normalized;
                var nrm = new Vector2(-d.y, d.x);
                float s = along[i];
                float w = Mathf.Clamp01(s / 6f) * Mathf.Clamp01((total - s) / 6f);
                float bend = (Mathf.PerlinNoise(s * 0.045f + seed, seed * 0.13f) - 0.5f) * 2f + (Mathf.PerlinNoise(s * 0.13f + seed * 1.7f, 3.1f) - 0.5f) * 0.6f;
                Vector2 q = pts[i] + nrm * (amp * bend * w);
                if (!blocked(q)) o[i] = q;
                else if (!blocked(Vector2.Lerp(pts[i], q, 0.5f))) o[i] = Vector2.Lerp(pts[i], q, 0.5f);
            }
            return o;
        }

        /// <summary>Smooths a polyline's wiggles (each inner point pulled toward its neighbours), keeping the ends.</summary>
        static List<Vector2> Relax(List<Vector2> pts, int passes)
        {
            for (int pass = 0; pass < passes; pass++)
            {
                var o = new List<Vector2>(pts);
                for (int i = 1; i < pts.Count - 1; i++) o[i] = (pts[i - 1] + 2f * pts[i] + pts[i + 1]) * 0.25f;
                pts = o;
            }
            return pts;
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
