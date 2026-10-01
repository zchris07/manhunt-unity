using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vision.World
{
    /// <summary>
    /// Accumulates flat-shaded geometry: every face gets its own vertices with the face normal and a
    /// single colour, so lighting is faceted. Used for voxel models, boxes and ground tiles.
    /// </summary>
    public sealed class FlatMeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        // Linear-space vertex colours (authored as sRGB, converted on the way in).
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();

        public int VertexCount => vertices.Count;
        public int TriangleCount => triangles.Count / 3;

        static readonly Vector3[] FaceNormals =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
        };

        // Corners of each face of a unit cube at the origin, wound clockwise seen from outside.
        static readonly Vector3[][] FaceCorners =
        {
            new[] { new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1) }, // +X
            new[] { new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0) }, // -X
            new[] { new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) }, // +Y
            new[] { new Vector3(0, 0, 1), new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1) }, // -Y
            new[] { new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1) }, // +Z
            new[] { new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0) }, // -Z
        };

        static readonly Vector3Int[] FaceDirs =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0), new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        public void Clear()
        {
            vertices.Clear();
            normals.Clear();
            colors.Clear();
            triangles.Clear();
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color32 color)
        {
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            Color linear = ((Color)color).linear;
            for (int k = 0; k < 4; k++)
            {
                normals.Add(normal);
                colors.Add(linear);
            }
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
        }

        /// <summary>Adds an axis-aligned box (all six faces).</summary>
        public void AddBox(Vector3 min, Vector3 size, Color32 color)
        {
            for (int f = 0; f < 6; f++) AddCubeFace(f, min, size, color);
        }

        void AddCubeFace(int face, Vector3 origin, Vector3 size, Color32 color)
        {
            Vector3[] c = FaceCorners[face];
            AddQuad(origin + Vector3.Scale(c[0], size), origin + Vector3.Scale(c[1], size),
                    origin + Vector3.Scale(c[2], size), origin + Vector3.Scale(c[3], size), FaceNormals[face], color);
        }

        /// <summary>
        /// Adds a voxel grid. Only faces next to empty space are emitted. <paramref name="pivot"/> is the
        /// grid-space point (in voxels) that lands on the local origin.
        /// </summary>
        public void AddVoxels(VoxelGrid grid, float voxelSize, Vector3 pivot, Vector3 offset = default)
        {
            var size = Vector3.one * voxelSize;
            for (int y = 0; y < grid.SizeY; y++)
            {
                for (int z = 0; z < grid.SizeZ; z++)
                {
                    for (int x = 0; x < grid.SizeX; x++)
                    {
                        if (!grid.IsSolid(x, y, z)) continue;
                        Color32 color = grid.Get(x, y, z);
                        Vector3 origin = offset + (new Vector3(x, y, z) - pivot) * voxelSize;
                        for (int f = 0; f < 6; f++)
                        {
                            Vector3Int d = FaceDirs[f];
                            if (grid.IsSolid(x + d.x, y + d.y, z + d.z)) continue;
                            AddCubeFace(f, origin, size, color);
                        }
                    }
                }
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
