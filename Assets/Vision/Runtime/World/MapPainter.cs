using System.Collections.Generic;
using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// Paints the level as a map from above, once per generated level: the ground's colours with hill shading, the lake,
    /// paths, every tree and rock as a dot, the building's rooms and walls, the cabins, fences, the yard, the dock, the
    /// graveyard and power poles. Row 0 is the south edge (north up), each pixel <see cref="MetresPerPixel"/> wide.
    /// </summary>
    public static class MapPainter
    {
        static readonly Color32 Wall = new Color32(14, 13, 12, 255);
        static readonly Color Water = new Color(0.10f, 0.15f, 0.19f);
        static readonly Color Evergreen = new Color(0.13f, 0.22f, 0.14f);
        static readonly Color DeadTree = new Color(0.32f, 0.29f, 0.25f);
        static readonly Color Rock = new Color(0.45f, 0.44f, 0.42f);

        public static float MetresPerPixel(SandboxWorld world, int size) => 2f * world.halfExtent / size;

        public static Texture2D Paint(SandboxWorld world, int size = 768)
        {
            float ext = world.halfExtent, mpp = 2f * ext / size;
            var px = new Color32[size * size];
            TerrainField terrain = world.Terrain;
            MapLayout layout = world.Layout;
            var sun = new Vector3(-0.5f, 0.75f, 0.45f).normalized;
            System.Threading.Tasks.Parallel.For(0, size, j =>
            {
                float z = -ext + (j + 0.5f) * mpp;
                for (int i = 0; i < size; i++)
                {
                    float x = -ext + (i + 0.5f) * mpp;
                    Color c = world.GroundColor(x, z);
                    // Paths and trails stand out a little, so the web of them reads on the map.
                    if (terrain.Paths != null)
                    {
                        float clear = terrain.PathClearance(x, z);
                        if (clear < 0.25f) c = Color.Lerp(c, new Color(0.66f, 0.56f, 0.42f), 0.35f * Mathf.Clamp01((0.25f - clear) / 0.4f));
                    }
                    if (layout.LakeDepth(new Vector2(x, z)) > 0f && terrain.InWater(x, z)) c = Water;
                    else
                    {
                        float shade = Vector3.Dot(terrain.Normal(x, z), sun);
                        c *= 0.75f + 0.55f * shade;
                    }
                    px[j * size + i] = (Color)(c * 1.15f);
                }
            });

            void Disc(Vector2 p, float r, Color32 c)
            {
                float cx = (p.x + ext) / mpp, cy = (p.y + ext) / mpp, rr = Mathf.Max(0.7f, r / mpp);
                int i0 = Mathf.Max(0, Mathf.FloorToInt(cx - rr)), i1 = Mathf.Min(size - 1, Mathf.CeilToInt(cx + rr));
                int j0 = Mathf.Max(0, Mathf.FloorToInt(cy - rr)), j1 = Mathf.Min(size - 1, Mathf.CeilToInt(cy + rr));
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        float dx = i + 0.5f - cx, dy = j + 0.5f - cy;
                        if (dx * dx + dy * dy <= rr * rr) px[j * size + i] = c;
                    }
            }
            void Line(Vector2 a, Vector2 b, float width, Color32 c)
            {
                float len = Vector2.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / (mpp * 0.5f)));
                for (int k = 0; k <= steps; k++) Disc(Vector2.Lerp(a, b, k / (float)steps), width * 0.5f, c);
            }
            void Fill(Rect r, Color32 c)
            {
                int i0 = Mathf.Max(0, Mathf.FloorToInt((r.xMin + ext) / mpp)), i1 = Mathf.Min(size - 1, Mathf.CeilToInt((r.xMax + ext) / mpp) - 1);
                int j0 = Mathf.Max(0, Mathf.FloorToInt((r.yMin + ext) / mpp)), j1 = Mathf.Min(size - 1, Mathf.CeilToInt((r.yMax + ext) / mpp) - 1);
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++) px[j * size + i] = c;
            }
            void Outline(Rect r, float width, Color32 c)
            {
                Line(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), width, c);
                Line(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), width, c);
                Line(new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), width, c);
                Line(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), width, c);
            }

            foreach ((Vector2 p, float r) in world.MapRocks) Disc(p, r, (Color32)Rock);
            foreach ((Vector2 p, float r, bool dead) in world.MapTrees)
            {
                Color c = dead ? DeadTree : Evergreen;
                Disc(p, r, (Color32)(c * (0.85f + 0.3f * Mathf.PerlinNoise(p.x * 0.7f, p.y * 0.7f))));
            }

            // The building: room floors by kind, then the walls with their doorways left open.
            BuildingPlan plan = layout.Plan;
            if (plan != null)
            {
                foreach (BuildingPlan.Room r in plan.Rooms)
                {
                    Color floor = r.IsHallway ? new Color(0.42f, 0.41f, 0.37f)
                        : r.Type == BuildingPlan.RoomType.Restroom || r.Type == BuildingPlan.RoomType.LockerRoom ? new Color(0.40f, 0.43f, 0.41f)
                        : r.Type == BuildingPlan.RoomType.Office || r.Type == BuildingPlan.RoomType.ServerRoom ? new Color(0.27f, 0.27f, 0.31f)
                        : r.Type == BuildingPlan.RoomType.StudioSet ? new Color(0.16f, 0.16f, 0.16f)
                        : new Color(0.35f, 0.35f, 0.34f);
                    Fill(r.Area, (Color32)floor);
                }
                foreach (BuildingPlan.Item item in plan.Items)
                    if (item.Solid && item.Tall) Fill(item.Footprint, new Color32(60, 52, 44, 255));
                foreach (BuildingPlan.WallRun w in plan.Walls) Line(w.A, w.B, w.Exterior ? 0.45f : 0.3f, Wall);
                foreach (BuildingPlan.Opening o in plan.Openings)
                    if (o.Kind == BuildingPlan.OpeningKind.Window || o.Kind == BuildingPlan.OpeningKind.BoardedWindow)
                        Line(o.A, o.B, 0.3f, new Color32(90, 110, 120, 255));
            }
            Rect yard = layout.Yard;
            Line(new Vector2(yard.xMin, yard.yMin), new Vector2(yard.xMin, yard.yMax), 0.2f, new Color32(140, 140, 140, 255));
            Line(new Vector2(yard.xMin, yard.yMax), new Vector2(yard.xMax, yard.yMax), 0.2f, new Color32(140, 140, 140, 255));
            Line(new Vector2(yard.xMax, yard.yMax), new Vector2(yard.xMax, yard.yMin), 0.2f, new Color32(140, 140, 140, 255));

            foreach (MapLayout.Cabin c in layout.Cabins)
            {
                Fill(c.Area, new Color32(84, 64, 46, 255));
                Outline(c.Area, 0.35f, Wall);
            }
            foreach (MapLayout.Segment f in layout.Fences)
            {
                Vector2 ab = f.B - f.A;
                Line(f.A, f.A + ab * f.GapStart, 0.25f, new Color32(96, 72, 48, 255));
                Line(f.A + ab * f.GapEnd, f.B, 0.25f, new Color32(96, 72, 48, 255));
            }
            Line(layout.DockStart, layout.DockEnd, layout.DockHalfWidth * 2f, new Color32(110, 92, 70, 255));
            Outline(layout.Graveyard, 0.2f, new Color32(40, 40, 42, 255));
            Outline(layout.Playground, 0.15f, new Color32(120, 70, 50, 255));
            foreach (Vector2 pole in layout.PowerPoles) Disc(pole, 0.35f, new Color32(60, 46, 34, 255));

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Map", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }

    /// <summary>
    /// What the player has seen, as in the original: the map is black until your own light has been on it. A grid of
    /// cells about 0.7 m across over the whole map, filled from the beam's and the proximity's visibility polygons, and
    /// a texture of it (black where unseen) laid over the painted map.
    /// </summary>
    public sealed class FogOfWar
    {
        public readonly int Size;
        public readonly float HalfExtent, Cell;
        readonly byte[] seen;
        readonly Color32[] pixels;
        public readonly Texture2D Texture;
        bool dirty;
        public bool AllRevealed { get; private set; }

        public FogOfWar(float halfExtent, int size = 256)
        {
            Size = size;
            HalfExtent = halfExtent;
            Cell = 2f * halfExtent / size;
            seen = new byte[size * size];
            pixels = new Color32[size * size];
            for (int k = 0; k < pixels.Length; k++) pixels[k] = new Color32(4, 4, 6, 255);
            Texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Fog", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            dirty = true;
            Apply();
        }

        int Index(Vector2 p)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt((p.x + HalfExtent) / Cell), 0, Size - 1);
            int j = Mathf.Clamp(Mathf.FloorToInt((p.y + HalfExtent) / Cell), 0, Size - 1);
            return j * Size + i;
        }

        public bool Seen(Vector2 p) => seen[Index(p)] != 0;

        public int SeenCount
        {
            get
            {
                int n = 0;
                foreach (byte b in seen) if (b != 0) n++;
                return n;
            }
        }

        void Mark(int k)
        {
            if (seen[k] != 0) return;
            seen[k] = 1;
            pixels[k] = new Color32(0, 0, 0, 0);
            dirty = true;
        }

        public void RevealAll()
        {
            for (int k = 0; k < seen.Length; k++) Mark(k);
            AllRevealed = true;
            Apply();
        }

        /// <summary>Marks every cell whose centre is inside the polygon (map coordinates), with a scanline fill.</summary>
        public void Reveal(IReadOnlyList<Vector2> poly)
        {
            if (poly == null || poly.Count < 3) return;
            float yMin = float.MaxValue, yMax = float.MinValue;
            foreach (Vector2 v in poly) { yMin = Mathf.Min(yMin, v.y); yMax = Mathf.Max(yMax, v.y); }
            int j0 = Mathf.Max(0, Mathf.FloorToInt((yMin + HalfExtent) / Cell)), j1 = Mathf.Min(Size - 1, Mathf.CeilToInt((yMax + HalfExtent) / Cell));
            var xs = new List<float>(16);
            for (int j = j0; j <= j1; j++)
            {
                float z = -HalfExtent + (j + 0.5f) * Cell;
                xs.Clear();
                for (int a = 0, b = poly.Count - 1; a < poly.Count; b = a++)
                {
                    Vector2 p = poly[a], q = poly[b];
                    if ((p.y <= z && q.y > z) || (q.y <= z && p.y > z))
                        xs.Add(p.x + (z - p.y) / (q.y - p.y) * (q.x - p.x));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int i0 = Mathf.Max(0, Mathf.CeilToInt((xs[k] + HalfExtent) / Cell - 0.5f));
                    int i1 = Mathf.Min(Size - 1, Mathf.FloorToInt((xs[k + 1] + HalfExtent) / Cell - 0.5f));
                    for (int i = i0; i <= i1; i++) Mark(j * Size + i);
                }
            }
        }

        /// <summary>Uploads the texture if anything changed.</summary>
        public void Apply()
        {
            if (!dirty) return;
            dirty = false;
            Texture.SetPixels32(pixels);
            Texture.Apply(false, false);
        }
    }
}
