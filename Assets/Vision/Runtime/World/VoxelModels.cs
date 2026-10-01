using UnityEngine;

namespace Vision.World
{
    /// <summary>
    /// Procedural voxel models for the sandbox diorama. Each returns a grid plus its voxel size and
    /// pivot (bottom centre). Colours follow a grimy, desaturated palette; the vision composite
    /// supplies the darkness.
    /// </summary>
    public static class VoxelModels
    {
        public struct Model
        {
            public VoxelGrid Grid;
            public float VoxelSize;
            public Vector3 Pivot;
        }

        public static class Palette
        {
            public static readonly Color Bark = new Color(0.20f, 0.17f, 0.14f);
            public static readonly Color BarkDark = new Color(0.12f, 0.10f, 0.09f);
            public static readonly Color Stone = new Color(0.42f, 0.41f, 0.38f);
            public static readonly Color StoneDark = new Color(0.30f, 0.29f, 0.27f);
            public static readonly Color Plank = new Color(0.36f, 0.27f, 0.19f);
            public static readonly Color PlankDark = new Color(0.22f, 0.16f, 0.11f);
            public static readonly Color Ember = new Color(1.0f, 0.55f, 0.18f);
            public static readonly Color Ash = new Color(0.25f, 0.23f, 0.21f);
            public static readonly Color Glass = new Color(1.0f, 0.85f, 0.45f);
            public static readonly Color Iron = new Color(0.16f, 0.16f, 0.17f);
            public static readonly Color Coat = new Color(0.30f, 0.28f, 0.22f);
            public static readonly Color Rags = new Color(0.14f, 0.12f, 0.12f);
            public static readonly Color Skin = new Color(0.62f, 0.52f, 0.44f);
            public static readonly Color PaleSkin = new Color(0.55f, 0.56f, 0.52f);
            public static readonly Color Crow = new Color(0.06f, 0.06f, 0.07f);
            public static readonly Color Beak = new Color(0.25f, 0.22f, 0.15f);
        }

        static Color Jitter(System.Random rng, Color c, float amount)
        {
            float k = 1f + ((float)rng.NextDouble() * 2f - 1f) * amount;
            return new Color(c.r * k, c.g * k, c.b * k);
        }

        static Model Make(VoxelGrid g, float voxelSize) =>
            new Model { Grid = g, VoxelSize = voxelSize, Pivot = new Vector3(g.SizeX * 0.5f, 0f, g.SizeZ * 0.5f) };

        /// <summary>Gnarled, leafless tree: tapering trunk, roots and wandering branches.</summary>
        public static Model DeadTree(System.Random rng)
        {
            const int w = 56, h = 64;
            var g = new VoxelGrid(w, h, w);
            int cx = w / 2, cz = w / 2;
            int height = 30 + rng.Next(18);
            float leanX = (float)(rng.NextDouble() - 0.5) * 0.25f;
            float leanZ = (float)(rng.NextDouble() - 0.5) * 0.25f;

            for (int y = 0; y < height; y++)
            {
                float r = Mathf.Lerp(2.6f, 0.8f, y / (float)height);
                float px = cx + leanX * y, pz = cz + leanZ * y;
                Disc(g, rng, px, y, pz, r, Palette.Bark);
            }

            int roots = 3 + rng.Next(3);
            for (int i = 0; i < roots; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                Walk(g, rng, cx, 1, cz, new Vector3(Mathf.Cos(a), -0.15f, Mathf.Sin(a)), 5 + rng.Next(5), 1, Palette.BarkDark);
            }

            int branches = 4 + rng.Next(4);
            for (int i = 0; i < branches; i++)
            {
                int y = height / 3 + rng.Next(height * 2 / 3);
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0.35f + (float)rng.NextDouble() * 0.5f, Mathf.Sin(a));
                Branch(g, rng, cx + leanX * y, y, cz + leanZ * y, dir, 10 + rng.Next(12), 2);
            }
            return Make(g, 0.12f);
        }

        static void Branch(VoxelGrid g, System.Random rng, float x, float y, float z, Vector3 dir, int length, int depth)
        {
            Vector3 p = new Vector3(x, y, z);
            for (int s = 0; s < length; s++)
            {
                dir += new Vector3((float)rng.NextDouble() - 0.5f, ((float)rng.NextDouble() - 0.35f) * 0.6f, (float)rng.NextDouble() - 0.5f) * 0.45f;
                dir.Normalize();
                p += dir;
                int thick = s < length / 3 && depth >= 2 ? 1 : 0;
                Blob(g, p, thick, Jitter(rng, Palette.Bark, 0.12f));
                if (depth > 0 && rng.NextDouble() < 0.12)
                    Branch(g, rng, p.x, p.y, p.z, Vector3.Slerp(dir, Random3(rng), 0.6f), length / 2, depth - 1);
            }
        }

        static Vector3 Random3(System.Random rng) =>
            new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble(), (float)rng.NextDouble() * 2f - 1f).normalized;

        static void Walk(VoxelGrid g, System.Random rng, float x, float y, float z, Vector3 dir, int length, int thick, Color c)
        {
            Vector3 p = new Vector3(x, y, z);
            for (int s = 0; s < length; s++)
            {
                p += dir;
                p.y = Mathf.Max(0f, p.y);
                Blob(g, p, s < length / 2 ? thick : 0, Jitter(rng, c, 0.1f));
            }
        }

        static void Blob(VoxelGrid g, Vector3 p, int radius, Color c)
        {
            int x0 = Mathf.RoundToInt(p.x), y0 = Mathf.RoundToInt(p.y), z0 = Mathf.RoundToInt(p.z);
            for (int dy = -radius; dy <= radius; dy++)
                for (int dz = -radius; dz <= radius; dz++)
                    for (int dx = -radius; dx <= radius; dx++)
                        if (dx * dx + dy * dy + dz * dz <= radius * radius) g.Set(x0 + dx, y0 + dy, z0 + dz, c);
        }

        static void Disc(VoxelGrid g, System.Random rng, float cx, int y, float cz, float r, Color c)
        {
            int ri = Mathf.CeilToInt(r);
            for (int dz = -ri; dz <= ri; dz++)
                for (int dx = -ri; dx <= ri; dx++)
                    if (dx * dx + dz * dz <= r * r)
                        g.Set(Mathf.RoundToInt(cx) + dx, y, Mathf.RoundToInt(cz) + dz, Jitter(rng, c, 0.1f));
        }

        /// <summary>Lumpy boulder.</summary>
        public static Model Rock(System.Random rng, float radiusMetres)
        {
            const float vs = 0.12f;
            int r = Mathf.Max(3, Mathf.RoundToInt(radiusMetres / vs));
            int size = r * 2 + 2;
            int height = Mathf.RoundToInt(r * (0.7f + (float)rng.NextDouble() * 0.4f));
            var g = new VoxelGrid(size, height + 1, size);
            float ox = (float)rng.NextDouble() * 10f, oz = (float)rng.NextDouble() * 10f;
            for (int y = 0; y <= height; y++)
            {
                for (int z = 0; z < size; z++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f - size * 0.5f) / r;
                        float dz = (z + 0.5f - size * 0.5f) / r;
                        float dy = y / (float)Mathf.Max(1, height);
                        float noise = Mathf.PerlinNoise(ox + x * 0.35f, oz + z * 0.35f + y * 0.2f) * 0.35f;
                        if (dx * dx + dz * dz + dy * dy * 0.9f < 0.8f + noise)
                            g.Set(x, y, z, Jitter(rng, y > height * 0.6f ? Palette.Stone : Palette.StoneDark, 0.12f));
                    }
                }
            }
            return Make(g, vs);
        }

        /// <summary>Wall section of vertical planks with dark seams (length along X).</summary>
        public static Model PlankWall(System.Random rng, float length, float height, float thickness)
        {
            const float vs = 0.1f;
            int lx = Mathf.Max(1, Mathf.RoundToInt(length / vs));
            int ly = Mathf.Max(1, Mathf.RoundToInt(height / vs));
            int lz = Mathf.Max(1, Mathf.RoundToInt(thickness / vs));
            var g = new VoxelGrid(lx, ly, lz);
            int plankWidth = 3;
            for (int x = 0; x < lx; x++)
            {
                bool seam = x % plankWidth == 0;
                Color plank = Jitter(rng, Palette.Plank, 0.18f);
                int top = ly - (rng.NextDouble() < 0.15 ? 1 : 0);
                for (int y = 0; y < top; y++)
                    for (int z = 0; z < lz; z++)
                        g.Set(x, y, z, seam ? Palette.PlankDark : Jitter(rng, plank, 0.05f));
            }
            // A dark horizontal beam near the top.
            for (int x = 0; x < lx; x++)
                for (int z = 0; z < lz; z++)
                    g.Set(x, ly - 3, z, Palette.PlankDark);
            return Make(g, vs);
        }

        /// <summary>Low stone wall (length along X).</summary>
        public static Model StoneWall(System.Random rng, float length, float height, float thickness)
        {
            const float vs = 0.2f;
            int lx = Mathf.Max(1, Mathf.RoundToInt(length / vs));
            int ly = Mathf.Max(1, Mathf.RoundToInt(height / vs));
            int lz = Mathf.Max(1, Mathf.RoundToInt(thickness / vs));
            var g = new VoxelGrid(lx, ly + 1, lz);
            for (int x = 0; x < lx; x++)
            {
                int top = ly - (rng.NextDouble() < 0.3 ? 1 : 0) + (rng.NextDouble() < 0.1 ? 1 : 0);
                for (int y = 0; y < top; y++)
                    for (int z = 0; z < lz; z++)
                        g.Set(x, y, z, Jitter(rng, (x + y) % 3 == 0 ? Palette.StoneDark : Palette.Stone, 0.15f));
            }
            return Make(g, vs);
        }

        /// <summary>Door or shutter panel (width along X).</summary>
        public static Model Panel(System.Random rng, float width, float height, float thickness)
        {
            const float vs = 0.08f;
            int lx = Mathf.Max(1, Mathf.RoundToInt(width / vs));
            int ly = Mathf.Max(1, Mathf.RoundToInt(height / vs));
            int lz = Mathf.Max(1, Mathf.RoundToInt(thickness / vs));
            var g = new VoxelGrid(lx, ly, lz);
            for (int x = 0; x < lx; x++)
            {
                Color plank = Jitter(rng, Palette.Plank, 0.15f) * 1.1f;
                for (int y = 0; y < ly; y++)
                {
                    bool brace = y == 2 || y == ly - 3 || Mathf.Abs(y - ly / 2) < 1;
                    for (int z = 0; z < lz; z++)
                        g.Set(x, y, z, x % 4 == 0 ? Palette.PlankDark : brace ? Palette.PlankDark * 1.2f : plank);
                }
            }
            return Make(g, vs);
        }

        public static Model Crate(System.Random rng, float size)
        {
            const float vs = 0.08f;
            int n = Mathf.Max(3, Mathf.RoundToInt(size / vs));
            var g = new VoxelGrid(n, n, n);
            for (int y = 0; y < n; y++)
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        int edges = (x == 0 || x == n - 1 ? 1 : 0) + (y == 0 || y == n - 1 ? 1 : 0) + (z == 0 || z == n - 1 ? 1 : 0);
                        g.Set(x, y, z, edges >= 2 ? Palette.PlankDark : Jitter(rng, Palette.Plank, 0.12f));
                    }
            return Make(g, vs);
        }

        /// <summary>Ring of stones, crossed logs and glowing embers.</summary>
        public static Model Campfire(System.Random rng)
        {
            const float vs = 0.08f;
            const int n = 14;
            var g = new VoxelGrid(n, 5, n);
            float c = n * 0.5f - 0.5f;
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (z - c) * (z - c));
                    if (d > 5f && d < 6.6f && rng.NextDouble() < 0.85)
                    {
                        g.Set(x, 0, z, Jitter(rng, Palette.Stone, 0.2f));
                        if (rng.NextDouble() < 0.5) g.Set(x, 1, z, Jitter(rng, Palette.StoneDark, 0.2f));
                    }
                    else if (d <= 4f)
                    {
                        g.Set(x, 0, z, rng.NextDouble() < 0.5 ? Palette.Ember : Palette.Ash);
                    }
                }
            for (int i = 1; i < n - 1; i++)
            {
                g.Set(i, 1, i, Palette.BarkDark);
                g.Set(n - 1 - i, 2, i, Palette.Bark);
            }
            for (int i = 0; i < 6; i++)
                g.Set(Mathf.RoundToInt(c) + rng.Next(-2, 3), 2 + rng.Next(2), Mathf.RoundToInt(c) + rng.Next(-2, 3), Palette.Ember);
            return Make(g, vs);
        }

        /// <summary>Iron post with a hanging lantern.</summary>
        public static Model LanternPost(System.Random rng)
        {
            const float vs = 0.08f;
            var g = new VoxelGrid(9, 24, 5);
            g.FillBox(1, 0, 1, 2, 22, 2, Palette.Iron);
            g.FillBox(1, 21, 1, 7, 22, 2, Palette.Iron);
            g.FillBox(5, 15, 0, 8, 19, 3, Palette.Iron);
            g.FillBox(6, 16, 1, 7, 18, 2, Palette.Glass);
            g.Set(6, 20, 1, Palette.Iron);
            return Make(g, vs);
        }

        /// <summary>A blocky humanoid: boots, legs, coat, arms and head.</summary>
        public static Model Humanoid(System.Random rng, Color coat, Color skin)
        {
            const float vs = 0.09f;
            var g = new VoxelGrid(8, 20, 5);
            Color boots = Palette.Iron;
            Color trousers = coat * 0.6f;
            g.FillBox(1, 0, 1, 2, 1, 3, boots);
            g.FillBox(5, 0, 1, 6, 1, 3, boots);
            g.FillBox(1, 2, 1, 2, 7, 3, trousers);
            g.FillBox(5, 2, 1, 6, 7, 3, trousers);
            g.FillBox(1, 7, 1, 6, 14, 3, coat);
            g.FillBox(0, 9, 1, 0, 14, 2, coat * 0.85f);
            g.FillBox(7, 9, 1, 7, 14, 2, coat * 0.85f);
            g.FillBox(0, 8, 1, 0, 8, 2, skin);
            g.FillBox(7, 8, 1, 7, 8, 2, skin);
            g.FillBox(2, 15, 1, 5, 18, 4, skin);
            g.FillBox(2, 19, 1, 5, 19, 4, coat * 0.5f);
            // Eyes face +Z (forward).
            g.Set(2, 17, 4, Palette.Crow);
            g.Set(5, 17, 4, Palette.Crow);
            for (int i = 0; i < 10; i++) g.Set(rng.Next(1, 7), rng.Next(7, 14), 3, Jitter(rng, coat, 0.25f));
            return Make(g, vs);
        }

        public static Model Crow(System.Random rng)
        {
            const float vs = 0.05f;
            var g = new VoxelGrid(7, 5, 9);
            g.FillBox(2, 1, 2, 4, 3, 6, Palette.Crow);
            g.FillBox(3, 3, 6, 3, 3, 7, Palette.Crow);
            g.Set(3, 3, 8, Palette.Beak);
            g.FillBox(0, 2, 3, 1, 2, 5, Palette.Crow * 1.4f);
            g.FillBox(5, 2, 3, 6, 2, 5, Palette.Crow * 1.4f);
            g.FillBox(3, 1, 0, 3, 2, 1, Palette.Crow);
            g.Set(3, 0, 4, Palette.Beak);
            return Make(g, vs);
        }
    }
}
