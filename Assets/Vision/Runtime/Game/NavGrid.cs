using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vision.Game
{
    /// <summary>
    /// The original's walkability grid (shared/src/map/navgrid.ts): square cells over the map, a cell blocked where a body
    /// of the given radius would touch a collider, and 8-connected A* with no corner cutting. Positions are design units on
    /// the ground plane, the map centred on the origin.
    /// </summary>
    public sealed class NavGrid
    {
        public readonly int Cols, Rows;
        public readonly float Cell, Half;
        public readonly bool[] Blocked;

        // A* scratch, reused between searches.
        float[] g;
        int[] came;
        byte[] closed;
        readonly MinHeap heap = new MinHeap();

        /// <param name="blocked">True where a body centred at the point would hit something.</param>
        public NavGrid(float halfExtent, float cell, Func<Vector2, bool> blocked)
        {
            Half = halfExtent;
            Cell = cell;
            Cols = Mathf.CeilToInt(halfExtent * 2f / cell);
            Rows = Cols;
            Blocked = new bool[Cols * Rows];
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Cols; x++)
                    Blocked[y * Cols + x] = blocked(CenterOf(y * Cols + x));
        }

        public int Index(Vector2 p)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt((p.x + Half) / Cell), 0, Cols - 1);
            int cy = Mathf.Clamp(Mathf.FloorToInt((p.y + Half) / Cell), 0, Rows - 1);
            return cy * Cols + cx;
        }

        public Vector2 CenterOf(int i) => new Vector2((i % Cols + 0.5f) * Cell - Half, (i / Cols + 0.5f) * Cell - Half);

        public bool IsBlocked(Vector2 p) => Blocked[Index(p)];

        /// <summary>The nearest walkable cell within maxRadius cells, or -1.</summary>
        public int NearestWalkable(Vector2 p, int maxRadius = 4)
        {
            int i0 = Index(p);
            if (!Blocked[i0]) return i0;
            int cx = i0 % Cols, cy = i0 / Cols, best = -1, bestD = int.MaxValue;
            for (int dy = -maxRadius; dy <= maxRadius; dy++)
                for (int dx = -maxRadius; dx <= maxRadius; dx++)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= Cols || ny >= Rows) continue;
                    int i = ny * Cols + nx;
                    if (Blocked[i]) continue;
                    int d = dx * dx + dy * dy;
                    if (d < bestD) { bestD = d; best = i; }
                }
            return best;
        }

        /// <summary>Cells reachable from a walkable cell (8-connected, no corner cutting).</summary>
        public bool[] Flood(int start)
        {
            var seen = new bool[Cols * Rows];
            if (start < 0 || Blocked[start]) return seen;
            var queue = new int[Cols * Rows];
            int head = 0, tail = 0;
            queue[tail++] = start;
            seen[start] = true;
            while (head < tail)
            {
                int i = queue[head++], x = i % Cols, y = i / Cols;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= Cols || ny >= Rows) continue;
                        int j = ny * Cols + nx;
                        if (seen[j] || Blocked[j]) continue;
                        if (dx != 0 && dy != 0 && (Blocked[y * Cols + nx] || Blocked[ny * Cols + x])) continue;
                        seen[j] = true;
                        queue[tail++] = j;
                    }
            }
            return seen;
        }

        /// <summary>A* between two points: waypoints (every third cell, then the goal), or null when there is no way.</summary>
        public List<Vector2> FindPath(Vector2 a, Vector2 b, int maxExpanded = 60000)
        {
            int start = NearestWalkable(a), goal = NearestWalkable(b);
            if (start < 0 || goal < 0) return null;
            if (start == goal) return new List<Vector2> { b };
            int n = Cols * Rows;
            if (g == null || g.Length != n)
            {
                g = new float[n];
                came = new int[n];
                closed = new byte[n];
            }
            for (int i = 0; i < n; i++) { g[i] = float.PositiveInfinity; came[i] = -1; closed[i] = 0; }
            heap.Clear();
            int gx = goal % Cols, gy = goal / Cols;
            float H(int i)
            {
                int dx = Mathf.Abs(i % Cols - gx), dy = Mathf.Abs(i / Cols - gy);
                return Mathf.Max(dx, dy) + 0.414f * Mathf.Min(dx, dy);
            }
            g[start] = 0f;
            heap.Push(start, H(start));
            int expanded = 0;
            while (heap.Count > 0)
            {
                int i = heap.Pop();
                if (i == goal) break;
                if (closed[i] != 0) continue;
                closed[i] = 1;
                if (++expanded > maxExpanded) return null;
                int x = i % Cols, y = i / Cols;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= Cols || ny >= Rows) continue;
                        int j = ny * Cols + nx;
                        if (Blocked[j] || closed[j] != 0) continue;
                        if (dx != 0 && dy != 0 && (Blocked[y * Cols + nx] || Blocked[ny * Cols + x])) continue;
                        float ng = g[i] + (dx != 0 && dy != 0 ? 1.414f : 1f);
                        if (ng < g[j])
                        {
                            g[j] = ng;
                            came[j] = i;
                            heap.Push(j, ng + H(j));
                        }
                    }
            }
            if (came[goal] < 0) return null;
            var cells = new List<int>();
            for (int i = goal; i != start && i >= 0; i = came[i]) cells.Add(i);
            cells.Reverse();
            var path = new List<Vector2>();
            // Every third waypoint (and the last) for smooth steering.
            for (int k = 0; k < cells.Count; k++)
                if (k % 3 == 2 || k == cells.Count - 1) path.Add(CenterOf(cells[k]));
            path.Add(b);
            return path;
        }

        sealed class MinHeap
        {
            readonly List<int> ids = new List<int>();
            readonly List<float> pri = new List<float>();
            public int Count => ids.Count;

            public void Clear() { ids.Clear(); pri.Clear(); }

            public void Push(int id, float p)
            {
                int i = ids.Count;
                ids.Add(id);
                pri.Add(p);
                while (i > 0)
                {
                    int parent = (i - 1) >> 1;
                    if (pri[parent] <= p) break;
                    ids[i] = ids[parent];
                    pri[i] = pri[parent];
                    i = parent;
                }
                ids[i] = id;
                pri[i] = p;
            }

            public int Pop()
            {
                int top = ids[0];
                int last = ids.Count - 1;
                int lastId = ids[last];
                float lastP = pri[last];
                ids.RemoveAt(last);
                pri.RemoveAt(last);
                int n = ids.Count;
                if (n > 0)
                {
                    int i = 0;
                    while (true)
                    {
                        int l = i * 2 + 1;
                        if (l >= n) break;
                        int r = l + 1;
                        int c = r < n && pri[r] < pri[l] ? r : l;
                        if (pri[c] >= lastP) break;
                        ids[i] = ids[c];
                        pri[i] = pri[c];
                        i = c;
                    }
                    ids[i] = lastId;
                    pri[i] = lastP;
                }
                return top;
            }
        }
    }

    /// <summary>
    /// Chases a moving point (the original's Chaser): along a walkable path refreshed every second, straight at it only
    /// for the last stretch when nothing is in the way.
    /// </summary>
    public sealed class Chaser
    {
        readonly MatchSim sim;
        readonly float radiusUnits;
        readonly bool doorsOpen;
        List<Vector2> path = new List<Vector2>();
        float pathT;

        /// <param name="doorsOpen">Paths may go through doors (NPCs who open them); otherwise doors stand as they are.</param>
        public Chaser(MatchSim sim, float radiusUnits, bool doorsOpen = true)
        {
            this.sim = sim;
            this.radiusUnits = radiusUnits;
            this.doorsOpen = doorsOpen;
        }

        public void Reset()
        {
            path.Clear();
            pathT = 0f;
        }

        /// <summary>The heading (radians) to take from <paramref name="at"/> toward <paramref name="target"/>.</summary>
        public float Heading(Vector2 at, Vector2 target, float dt, bool stuck)
        {
            if (Vector2.Distance(at, target) < Scale.D(110f) && sim.Geo.LineOfSight(at, target) && !stuck)
            {
                path.Clear();
                return Mathf.Atan2(target.y - at.y, target.x - at.x);
            }
            pathT -= dt;
            if (pathT <= 0f || path.Count < 1)
            {
                pathT = 1f;
                NavGrid nav = sim.Geo.Nav(radiusUnits, doorsOpen);
                path = (nav != null ? nav.FindPath(at, target, 40000) : null) ?? new List<Vector2> { target };
            }
            while (path.Count > 1 && Vector2.Distance(path[0], at) < Scale.D(20f)) path.RemoveAt(0);
            return Mathf.Atan2(path[0].y - at.y, path[0].x - at.x);
        }
    }
}
