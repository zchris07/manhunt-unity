using UnityEngine;

namespace Vision.World
{
    /// <summary>A dense voxel volume. A voxel with alpha 0 is empty; otherwise its RGB is its colour.</summary>
    public sealed class VoxelGrid
    {
        public readonly int SizeX, SizeY, SizeZ;
        readonly Color32[] cells;

        public VoxelGrid(int sizeX, int sizeY, int sizeZ)
        {
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            cells = new Color32[sizeX * sizeY * sizeZ];
        }

        public bool InBounds(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < SizeX && y < SizeY && z < SizeZ;

        public bool IsSolid(int x, int y, int z) => InBounds(x, y, z) && cells[Index(x, y, z)].a != 0;

        public Color32 Get(int x, int y, int z) => cells[Index(x, y, z)];

        public void Set(int x, int y, int z, Color c)
        {
            if (!InBounds(x, y, z)) return;
            Color32 c32 = c;
            c32.a = 255;
            cells[Index(x, y, z)] = c32;
        }

        public void Clear(int x, int y, int z)
        {
            if (InBounds(x, y, z)) cells[Index(x, y, z)] = default;
        }

        public void FillBox(int x0, int y0, int z0, int x1, int y1, int z1, Color c)
        {
            for (int z = z0; z <= z1; z++)
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        Set(x, y, z, c);
        }

        int Index(int x, int y, int z) => (y * SizeZ + z) * SizeX + x;
    }
}
