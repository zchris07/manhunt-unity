using NUnit.Framework;
using UnityEngine;
using Vision.World;

namespace Vision.Tests
{
    public class LowPolyMeshTests
    {
        static Color White => Color.white;

        /// <summary>Every triangle's winding normal must match its stored normal; when convex, point away from the centre.</summary>
        static void AssertFacesOutward(Mesh mesh, bool convex)
        {
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            int[] t = mesh.triangles;
            Vector3 centre = mesh.bounds.center;
            Assert.Greater(t.Length, 0);
            for (int i = 0; i < t.Length; i += 3)
            {
                // Unity front faces are clockwise: the winding normal is Cross(b - a, c - a).
                Vector3 winding = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).normalized;
                Assert.Greater(Vector3.Dot(winding, n[t[i]]), 0.99f, "stored normal must match winding");
                if (convex)
                    Assert.Greater(Vector3.Dot(n[t[i]], (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f - centre), 0f, "face must point outward");
            }
        }

        [Test]
        public void Triangles_HaveUnsharedVerticesAndFlatNormals()
        {
            var b = new LowPolyMeshBuilder();
            b.AddBox(Vector3.zero, Vector3.one, White);
            Assert.AreEqual(12, b.TriangleCount);
            Assert.AreEqual(b.TriangleCount * 3, b.VertexCount);
            Mesh m = b.ToMesh("box");
            for (int i = 0; i < m.triangles.Length; i += 3)
                Assert.AreEqual(m.normals[m.triangles[i]], m.normals[m.triangles[i + 2]]);
            Object.DestroyImmediate(m);
        }

        [Test]
        public void Box_FacesPointOutward()
        {
            var b = new LowPolyMeshBuilder();
            b.AddBox(new Vector3(-1f, 0f, -2f), new Vector3(2f, 1f, 4f), White);
            Mesh m = b.ToMesh("box");
            AssertFacesOutward(m, true);
            Object.DestroyImmediate(m);
        }

        [Test]
        public void Cone_And_Frustum_FacesPointOutward()
        {
            var b = new LowPolyMeshBuilder();
            b.AddCone(Vector3.zero, new Vector3(0f, 2f, 0f), 0.5f, 6, White);
            Mesh cone = b.ToMesh("cone");
            AssertFacesOutward(cone, true);
            Object.DestroyImmediate(cone);

            b = new LowPolyMeshBuilder();
            b.AddFrustum(Vector3.zero, new Vector3(0.2f, 2f, 0f), 0.4f, 0.2f, 7, White);
            Mesh frustum = b.ToMesh("frustum");
            AssertFacesOutward(frustum, true);
            Object.DestroyImmediate(frustum);
        }

        [Test]
        public void Blob_IsClosedFacetedAndOutward()
        {
            var b = new LowPolyMeshBuilder(new System.Random(3));
            b.AddBlob(Vector3.zero, new Vector3(1f, 0.7f, 1.2f), 1, 0.15f, _ => White);
            Assert.AreEqual(80, b.TriangleCount, "one subdivision of an icosahedron is 80 faces");
            Mesh m = b.ToMesh("blob");
            AssertFacesOutward(m, true);
            Object.DestroyImmediate(m);
        }

        [Test]
        public void FlatBottomBlob_SitsOnTheGround()
        {
            var b = new LowPolyMeshBuilder(new System.Random(4));
            b.AddBlob(Vector3.zero, Vector3.one, 1, 0.1f, _ => White, true);
            Mesh m = b.ToMesh("rock");
            Assert.GreaterOrEqual(m.bounds.min.y, -1e-4f);
            AssertFacesOutward(m, false);
            Object.DestroyImmediate(m);
        }

        [Test]
        public void Models_AreDeterministic()
        {
            Mesh a = LowPolyModels.DeadTree(LowPolyModels.DeadTreeKind.Elm, 1);
            Mesh b = LowPolyModels.DeadTree(LowPolyModels.DeadTreeKind.Elm, 1);
            Assert.AreEqual(a.vertexCount, b.vertexCount);
            Assert.AreEqual(a.bounds, b.bounds);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        [Test]
        public void DeadTrees_TwelveDistinctDesigns_EachInThreeSizes()
        {
            var shapes = new System.Collections.Generic.HashSet<string>();
            for (int k = 0; k < LowPolyModels.DeadTreeKinds; k++)
            {
                var kind = (LowPolyModels.DeadTreeKind)k;
                float previous = 0f;
                for (int size = 0; size < 3; size++)
                {
                    Mesh m = LowPolyModels.DeadTree(kind, size);
                    AssertFacesOutward(m, false);
                    Assert.GreaterOrEqual(m.bounds.min.y, -0.12f, $"{m.name} sits on the ground");
                    float extent = Mathf.Max(m.bounds.size.x, m.bounds.size.y, m.bounds.size.z);
                    Assert.Greater(extent, previous, $"{m.name}: each size step is bigger");
                    previous = extent;
                    if (size == 1)
                    {
                        Assert.That(m.triangles.Length / 3, Is.InRange(40, 2600), $"{m.name} triangle count");
                        // Designs differ in proportions, not only in size.
                        shapes.Add($"{m.bounds.size.x / m.bounds.size.y:0.0}x{m.bounds.size.z / m.bounds.size.y:0.0}x{m.triangles.Length / 100}");
                    }
                    Object.DestroyImmediate(m);
                }
            }
            Assert.GreaterOrEqual(shapes.Count, 11, "twelve designs with their own shape");
            Mesh fallen = LowPolyModels.DeadTree(LowPolyModels.DeadTreeKind.Fallen, 1);
            Assert.Greater(fallen.bounds.size.x, 2.5f * fallen.bounds.size.y, "the fallen tree lies along the ground (its root plate stands on edge)");
            Object.DestroyImmediate(fallen);
            Mesh snag = LowPolyModels.DeadTree(LowPolyModels.DeadTreeKind.PineSnag, 1);
            Mesh stump = LowPolyModels.DeadTree(LowPolyModels.DeadTreeKind.Stump, 1);
            Assert.Greater(snag.bounds.size.y, 2.5f * stump.bounds.size.y, "tall snags and short stumps");
            Object.DestroyImmediate(snag);
            Object.DestroyImmediate(stump);
        }

        [Test]
        public void Walls_MatchRequestedDimensions()
        {
            Mesh plank = LowPolyModels.PlankWall(new System.Random(1), 4f, 2.4f, 0.3f);
            Assert.AreEqual(4f, plank.bounds.size.x, 0.1f);
            Assert.AreEqual(2.4f, plank.bounds.max.y, 0.05f);
            Assert.AreEqual(0f, plank.bounds.min.y, 1e-3f);
            Assert.That(plank.bounds.size.z, Is.InRange(0.28f, 0.4f));
            Mesh stone = LowPolyModels.StoneWall(new System.Random(2), 4f, 1.2f, 0.6f);
            Assert.AreEqual(4f, stone.bounds.size.x, 0.12f);
            Assert.That(stone.bounds.size.z, Is.InRange(0.5f, 0.7f));
            Object.DestroyImmediate(plank);
            Object.DestroyImmediate(stone);
        }

        [Test]
        public void Ground_CoversRequestedExtent_AndFacesUp()
        {
            var b = new LowPolyMeshBuilder(new System.Random(9));
            b.AddFacetedGround(10f, 0.7f, (x, z) => 0f, (x, z) => White, (x, z) => 1f, 0.28f, 0.03f);
            Mesh m = b.ToMesh("ground");
            Assert.AreEqual(20f, m.bounds.size.x, 0.01f);
            Assert.AreEqual(20f, m.bounds.size.z, 0.01f);
            foreach (Vector3 n in m.normals) Assert.Greater(n.y, 0.9f);
            Object.DestroyImmediate(m);
        }
    }
}
