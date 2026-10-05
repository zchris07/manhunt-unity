using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>Points in a uniform grid, for fast "is anything within r of here" checks while scattering thousands of props.</summary>
    public sealed class PointGrid
    {
        readonly float cell;
        readonly Dictionary<long, List<Vector2>> cells = new Dictionary<long, List<Vector2>>();

        public PointGrid(float cellSize = 4f) => cell = cellSize;

        public int Count { get; private set; }

        long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

        public void Add(Vector2 p)
        {
            long k = Key(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell));
            if (!cells.TryGetValue(k, out var list)) cells[k] = list = new List<Vector2>(4);
            list.Add(p);
            Count++;
        }

        public void Clear()
        {
            cells.Clear();
            Count = 0;
        }

        public bool AnyWithin(Vector2 p, float radius)
        {
            int x0 = Mathf.FloorToInt((p.x - radius) / cell), x1 = Mathf.FloorToInt((p.x + radius) / cell);
            int y0 = Mathf.FloorToInt((p.y - radius) / cell), y1 = Mathf.FloorToInt((p.y + radius) / cell);
            float r2 = radius * radius;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!cells.TryGetValue(Key(x, y), out var list)) continue;
                    foreach (Vector2 q in list) if ((q - p).sqrMagnitude < r2) return true;
                }
            return false;
        }
    }
}
