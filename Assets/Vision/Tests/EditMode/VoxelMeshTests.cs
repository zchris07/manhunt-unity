using NUnit.Framework;
using UnityEngine;
using Vision.World;

namespace Vision.Tests
{
    public class VoxelMeshTests
    {
        [Test]
        public void SingleVoxel_HasSixFlatFaces()
        {
            var g = new VoxelGrid(1, 1, 1);
            g.Set(0, 0, 0, Color.red);
            var b = new FlatMeshBuilder();
            b.AddVoxels(g, 1f, Vector3.zero);
            Assert.AreEqual(24, b.VertexCount);
            Assert.AreEqual(12, b.TriangleCount);
        }

        [Test]
        public void AdjacentVoxels_ShareNoInnerFaces()
        {
            var g = new VoxelGrid(2, 1, 1);
            g.Set(0, 0, 0, Color.red);
            g.Set(1, 0, 0, Color.red);
            var b = new FlatMeshBuilder();
            b.AddVoxels(g, 1f, Vector3.zero);
            Assert.AreEqual(10 * 4, b.VertexCount);
        }

        [Test]
        public void Faces_PointOutwards()
        {
            var g = new VoxelGrid(1, 1, 1);
            g.Set(0, 0, 0, Color.white);
            var b = new FlatMeshBuilder();
            b.AddVoxels(g, 1f, new Vector3(0.5f, 0.5f, 0.5f));
            Mesh mesh = b.ToMesh("test");
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            int[] t = mesh.triangles;
            Object.DestroyImmediate(mesh);
            for (int i = 0; i < t.Length; i += 3)
            {
                // Unity front faces are clockwise: the winding normal must match the stored normal,
                // and on a cube centred at the origin it must point away from the centre.
                Vector3 winding = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).normalized;
                Assert.Greater(Vector3.Dot(winding, n[t[i]]), 0.99f);
                Assert.Greater(Vector3.Dot(n[t[i]], v[t[i]]), 0f);
            }
        }
    }
}
