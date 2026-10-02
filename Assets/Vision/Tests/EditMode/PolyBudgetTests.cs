using NUnit.Framework;
using UnityEngine;
using Vision.Characters;
using Vision.World;

namespace Vision.Tests
{
    public class PolyBudgetTests
    {
        [Test]
        public void ReferenceEdge_ComesFromTheMannequin()
        {
            MannequinBuilder.Measure(out int tris, out float area);
            Assert.LessOrEqual(tris, 200);
            Assert.AreEqual(Mathf.Sqrt(area / (tris * 0.4330127f)), PolyBudget.ReferenceEdge, 1e-5f);
            Assert.That(PolyBudget.ReferenceEdge, Is.InRange(0.05f, 0.3f));
        }

        [Test]
        public void CoarserClasses_GetLargerFacets()
        {
            float previous = 0f;
            foreach (PolyBudget.Class c in System.Enum.GetValues(typeof(PolyBudget.Class)))
            {
                Assert.Greater(PolyBudget.Edge(c), previous, c.ToString());
                previous = PolyBudget.Edge(c);
            }
            Assert.Greater(PolyBudget.Sides(0.5f, PolyBudget.Class.Character), PolyBudget.Sides(0.5f, PolyBudget.Class.Wall));
            Assert.AreEqual(3, PolyBudget.Sides(0.001f, PolyBudget.Class.Prop), "at least a triangle");
            Assert.LessOrEqual(PolyBudget.Sides(100f, PolyBudget.Class.Prop), 12);
            Assert.GreaterOrEqual(PolyBudget.BlobSubdivisions(Vector3.one * 2f, PolyBudget.Class.Rock),
                                  PolyBudget.BlobSubdivisions(Vector3.one * 0.2f, PolyBudget.Class.Rock));
        }

        /// <summary>
        /// The measured average facet of a model must be close to its class's facet size. The budget is a cap:
        /// a flat-faced model (a crate is a few boxes) may use fewer, larger facets, but never finer ones.
        /// </summary>
        static void AssertFacets(Mesh m, PolyBudget.Class c, bool curved = true, float tolerance = 0.6f)
        {
            float edge = PolyBudget.MeasuredEdge(m), target = PolyBudget.Edge(c);
            Assert.That(edge, Is.InRange(target * (1f - tolerance), curved ? target * (1f + tolerance) : float.MaxValue),
                $"{m.name}: {m.triangles.Length / 3} tris, average edge {edge:0.000} vs {c} {target:0.000}");
            Object.DestroyImmediate(m);
        }

        [Test]
        public void Models_FollowTheirClassBudget()
        {
            for (int i = 0; i < 4; i++) AssertFacets(LowPolyModels.DeadTree(new System.Random(1000 + i)), PolyBudget.Class.Tree);
            foreach (float r in PropLibrary.RockRadii) AssertFacets(LowPolyModels.Rock(new System.Random(2000), r), PolyBudget.Class.Rock);
            AssertFacets(LowPolyModels.StoneWall(new System.Random(1), 8f, 1.2f, 0.6f), PolyBudget.Class.Wall);
            AssertFacets(LowPolyModels.PlankWall(new System.Random(1), 8f, 2.4f, 0.3f), PolyBudget.Class.Wall);
            AssertFacets(LowPolyModels.Crate(new System.Random(1), 0.8f), PolyBudget.Class.Prop, false);
        }

        [Test]
        public void Models_AreDeterministic()
        {
            Mesh a = LowPolyModels.DeadTree(new System.Random(7)), b = LowPolyModels.DeadTree(new System.Random(7));
            Assert.AreEqual(a.vertices, b.vertices);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }
    }
}
