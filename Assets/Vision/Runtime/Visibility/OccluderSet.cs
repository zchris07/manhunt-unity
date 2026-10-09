using System.Collections.Generic;
using UnityEngine;

namespace Vision.Visibility
{
    /// <summary>
    /// All occluder segments on the ground plane (world X,Z mapped to 2D x,y), grouped by owner so a
    /// door or window can be switched on and off as a unit. A uniform spatial hash keeps queries local.
    /// Every change bumps <see cref="Version"/>, which is how cached light polygons know to rebuild.
    /// </summary>
    public sealed class OccluderSet
    {
        const float CellSize = 4f;

        struct Owner
        {
            public int First;
            public int Count;
            public bool Active;
            public bool Alive;
        }

        readonly List<Vector4> segments = new List<Vector4>();
        readonly List<Owner> owners = new List<Owner>();

        // Rebuilt lazily after any change: only the active segments, packed as ax,ay,bx,by.
        float[] packed = new float[0];
        int packedCount;
        int[] stamps = new int[0];
        int stamp;
        readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
        readonly Stack<List<int>> cellPool = new Stack<List<int>>();
        bool dirty = true;

        /// <summary>Incremented on every add, remove or open/close.</summary>
        public int Version { get; private set; }

        /// <summary>Active segments as a flat [ax, ay, bx, by, ...] array; index i starts at 4*i.</summary>
        public float[] Packed
        {
            get
            {
                if (dirty) Rebuild();
                return packed;
            }
        }

        public int ActiveSegmentCount
        {
            get
            {
                if (dirty) Rebuild();
                return packedCount;
            }
        }

        /// <summary>Registers a polyline (or a closed loop) of 2D points and returns its owner handle.</summary>
        public int Register(IReadOnlyList<Vector2> points, bool closedLoop, bool active = true)
        {
            int first = segments.Count;
            int n = points.Count;
            int segCount = closedLoop ? n : n - 1;
            for (int i = 0; i < segCount; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % n];
                segments.Add(new Vector4(a.x, a.y, b.x, b.y));
            }
            owners.Add(new Owner { First = first, Count = segCount, Active = active, Alive = true });
            Touch();
            return owners.Count - 1;
        }

        public void SetActive(int handle, bool active)
        {
            if (handle < 0 || handle >= owners.Count) return;
            Owner o = owners[handle];
            if (o.Active == active) return;
            o.Active = active;
            owners[handle] = o;
            Touch();
        }

        public bool IsActive(int handle) => handle >= 0 && handle < owners.Count && owners[handle].Active && owners[handle].Alive;

        public void Unregister(int handle)
        {
            if (handle < 0 || handle >= owners.Count) return;
            Owner o = owners[handle];
            if (!o.Alive) return;
            o.Alive = false;
            owners[handle] = o;
            Touch();
        }

        public void Clear()
        {
            segments.Clear();
            owners.Clear();
            Touch();
        }

        void Touch()
        {
            Version++;
            dirty = true;
        }

        /// <summary>Collects the packed indices of active segments whose bounds overlap the box.</summary>
        public void Query(float minX, float minY, float maxX, float maxY, List<int> result)
        {
            result.Clear();
            if (dirty) Rebuild();
            if (++stamp == int.MaxValue)
            {
                System.Array.Clear(stamps, 0, stamps.Length);
                stamp = 1;
            }
            int cx0 = Cell(minX), cx1 = Cell(maxX), cy0 = Cell(minY), cy1 = Cell(maxY);
            for (int cy = cy0; cy <= cy1; cy++)
            {
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    if (!grid.TryGetValue(Key(cx, cy), out List<int> cell)) continue;
                    for (int i = 0; i < cell.Count; i++)
                    {
                        int id = cell[i];
                        if (stamps[id] == stamp) continue;
                        stamps[id] = stamp;
                        result.Add(id);
                    }
                }
            }
        }

        readonly List<int> castIds = new List<int>(256);

        /// <summary>
        /// Distance from <paramref name="origin"/> along the unit direction <paramref name="dir"/> to the first active segment,
        /// or <paramref name="max"/> if nothing is in the way (plane coordinates).
        /// </summary>
        public float Raycast(Vector2 origin, Vector2 dir, float max)
        {
            Vector2 end = origin + dir * max;
            Query(Mathf.Min(origin.x, end.x), Mathf.Min(origin.y, end.y), Mathf.Max(origin.x, end.x), Mathf.Max(origin.y, end.y), castIds);
            float best = max;
            float[] seg = packed;
            for (int i = 0; i < castIds.Count; i++)
            {
                int o = castIds[i] * 4;
                float t = VisibilityComputer.RaySegment(origin.x, origin.y, dir.x, dir.y, seg[o], seg[o + 1], seg[o + 2], seg[o + 3]);
                if (t < best) best = t;
            }
            return best;
        }

        /// <summary>True if any active segment crosses the segment from a to b.</summary>
        public bool Blocks(Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1e-5f) return false;
            return Raycast(a, d / len, len) < len - 1e-4f;
        }

        void Rebuild()
        {
            dirty = false;
            int count = 0;
            for (int o = 0; o < owners.Count; o++)
                if (owners[o].Alive && owners[o].Active) count += owners[o].Count;

            if (packed.Length < count * 4) packed = new float[count * 4];
            if (stamps.Length < count) stamps = new int[count];
            packedCount = count;

            foreach (var cell in grid.Values)
            {
                cell.Clear();
                cellPool.Push(cell);
            }
            grid.Clear();

            int k = 0;
            for (int o = 0; o < owners.Count; o++)
            {
                Owner owner = owners[o];
                if (!owner.Alive || !owner.Active) continue;
                for (int s = owner.First; s < owner.First + owner.Count; s++)
                {
                    Vector4 seg = segments[s];
                    packed[k * 4] = seg.x;
                    packed[k * 4 + 1] = seg.y;
                    packed[k * 4 + 2] = seg.z;
                    packed[k * 4 + 3] = seg.w;
                    int cx0 = Cell(Mathf.Min(seg.x, seg.z)), cx1 = Cell(Mathf.Max(seg.x, seg.z));
                    int cy0 = Cell(Mathf.Min(seg.y, seg.w)), cy1 = Cell(Mathf.Max(seg.y, seg.w));
                    for (int cy = cy0; cy <= cy1; cy++)
                    {
                        for (int cx = cx0; cx <= cx1; cx++)
                        {
                            long key = Key(cx, cy);
                            if (!grid.TryGetValue(key, out List<int> cell))
                            {
                                cell = cellPool.Count > 0 ? cellPool.Pop() : new List<int>();
                                grid.Add(key, cell);
                            }
                            cell.Add(k);
                        }
                    }
                    k++;
                }
            }
        }

        static int Cell(float v) => Mathf.FloorToInt(v / CellSize);

        static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
    }
}
