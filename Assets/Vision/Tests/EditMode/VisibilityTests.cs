using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vision.Visibility;

namespace Vision.Tests
{
    public class VisibilityTests
    {
        const float Tol = 1e-3f;

        static int Box(OccluderSet set, float minX, float minY, float maxX, float maxY, bool active = true) =>
            set.Register(new[]
            {
                new Vector2(minX, minY), new Vector2(maxX, minY), new Vector2(maxX, maxY), new Vector2(minX, maxY),
            }, true, active);

        static List<Vector2> Compute(OccluderSet set, ViewQuery q)
        {
            var output = new List<Vector2>();
            new VisibilityComputer(set).Compute(q, output);
            return output;
        }

        [Test]
        public void OpenField_FullCircle_AllPointsAtRange()
        {
            var poly = Compute(new OccluderSet(), ViewQuery.Circle(new Vector2(3f, -2f), 5f));
            Assert.Greater(poly.Count, 100);
            foreach (Vector2 p in poly) Assert.AreEqual(5f, Vector2.Distance(p, new Vector2(3f, -2f)), Tol);
        }

        [Test]
        public void Wall_BlocksRay_AtWallDistance()
        {
            var set = new OccluderSet();
            set.Register(new[] { new Vector2(4f, -10f), new Vector2(4f, 10f) }, false);
            var poly = Compute(set, ViewQuery.Cone(Vector2.zero, 0f, 0.3f, 20f));
            float maxX = float.MinValue;
            foreach (Vector2 p in poly) maxX = Mathf.Max(maxX, p.x);
            Assert.AreEqual(4f, maxX, Tol, "nothing should be visible beyond the wall");
        }

        [Test]
        public void Cone_RespectsHalfAngleAndStartsAtOrigin()
        {
            var origin = new Vector2(1f, 1f);
            const float dir = 1.2f, half = 0.5f;
            var poly = Compute(new OccluderSet(), ViewQuery.Cone(origin, dir, half, 10f));
            Assert.AreEqual(origin, poly[0]);
            for (int i = 1; i < poly.Count; i++)
            {
                Vector2 d = poly[i] - origin;
                float delta = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, dir * Mathf.Rad2Deg));
                Assert.LessOrEqual(delta, half * Mathf.Rad2Deg + 0.01f);
            }
        }

        [Test]
        public void Corner_EpsilonRays_WrapAroundTheCorner()
        {
            // Seen from the origin, the box's silhouette corner is (7, 1). Rays at that corner and at
            // +/- epsilon must give one hit on the corner and one that slips past to the range circle.
            var set = new OccluderSet();
            Box(set, 5f, 1f, 7f, 4f);
            var poly = Compute(set, ViewQuery.Circle(Vector2.zero, 20f));
            bool cornerHit = false, slipsPast = false;
            float cornerAngle = Mathf.Atan2(1f, 7f);
            foreach (Vector2 p in poly)
            {
                if ((p - new Vector2(7f, 1f)).sqrMagnitude < 1e-4f) cornerHit = true;
                float a = Mathf.Atan2(p.y, p.x);
                if (Mathf.Abs(a - cornerAngle) < 1e-3f && p.magnitude > 19.9f) slipsPast = true;
            }
            Assert.IsTrue(cornerHit, "a ray should end exactly on the corner");
            Assert.IsTrue(slipsPast, "a ray just past the corner should reach the range");
        }

        [Test]
        public void Gap_BetweenTwoBoxes_LetsAShaftThrough()
        {
            var set = new OccluderSet();
            Box(set, 5f, -10f, 6f, -0.5f);
            Box(set, 5f, 0.5f, 6f, 10f);
            var poly = Compute(set, ViewQuery.Circle(Vector2.zero, 20f));
            bool throughGap = false, blockedAbove = false;
            foreach (Vector2 p in poly)
            {
                if (p.x > 15f && Mathf.Abs(p.y) < 2f) throughGap = true;
                if (Mathf.Abs(p.x - 5f) < Tol && p.y > 2f) blockedAbove = true;
            }
            Assert.IsTrue(throughGap);
            Assert.IsTrue(blockedAbove);
        }

        [Test]
        public void SeeThroughCone_IgnoresOccluders()
        {
            var set = new OccluderSet();
            Box(set, 2f, -5f, 3f, 5f);
            var poly = Compute(set, ViewQuery.Cone(Vector2.zero, 0f, 0.3f, 10f, useOccluders: false));
            for (int i = 1; i < poly.Count; i++) Assert.AreEqual(10f, poly[i].magnitude, Tol);
        }

        [Test]
        public void Door_ClosedBlocks_OpenLetsSightThrough_AndBumpsVersion()
        {
            var set = new OccluderSet();
            int door = Box(set, 4f, -1f, 4.2f, 1f);
            var q = ViewQuery.Cone(Vector2.zero, 0f, 0.05f, 10f);

            float Reach() { float m = 0f; foreach (Vector2 p in Compute(set, q)) m = Mathf.Max(m, p.x); return m; }

            Assert.AreEqual(4f, Reach(), Tol);
            int v = set.Version;
            set.SetActive(door, false);
            Assert.Greater(set.Version, v);
            Assert.AreEqual(10f, Reach(), 0.01f);
        }

        [Test]
        public void Occluder_PlacedAfterSpawning_BlocksWhereItStands()
        {
            // Props are spawned at the origin and moved into place afterwards: the footprint must follow (Start refreshes it).
            var go = new GameObject("Rock");
            var occ = go.AddComponent<Occluder>();
            occ.shape = Occluder.Shape.Circle;
            occ.radius = 0.5f;
            var ids = new List<int>();
            try
            {
                occ.Refresh();
                go.transform.position = new Vector3(5000f, 0f, 5000f);
                occ.Refresh();
                Vector2 at = VisionWorld.ToPlane(go.transform.position);
                VisionWorld.Occluders.Query(at.x - 1f, at.y - 1f, at.x + 1f, at.y + 1f, ids);
                Assert.AreEqual(occ.sides, ids.Count, "where it stands");
                VisionWorld.Occluders.Query(-1f, -1f, 1f, 1f, ids);
                float[] seg = VisionWorld.Occluders.Packed;
                foreach (int id in ids) Assert.Greater(Mathf.Abs(seg[id * 4]), 100f, "nothing left behind at the origin");
            }
            finally
            {
                occ.Release();
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void HiddenSegmentCulling_KeepsThePolygon()
        {
            // A dense random field (thousands of segments): culling what is hidden must not change what is visible.
            var set = new OccluderSet();
            var rng = new System.Random(4);
            for (int i = 0; i < 1500; i++)
            {
                float x = (float)rng.NextDouble() * 160f - 80f, y = (float)rng.NextDouble() * 160f - 80f, s = 0.3f + (float)rng.NextDouble() * 1.5f;
                if (Mathf.Abs(x) < 2f && Mathf.Abs(y) < 2f) continue;
                Box(set, x, y, x + s, y + s * 0.6f);
            }
            float Area(List<Vector2> p) { float a = 0f; for (int i = 0; i < p.Count; i++) { Vector2 u = p[i], w = p[(i + 1) % p.Count]; a += u.x * w.y - w.x * u.y; } return Mathf.Abs(a) * 0.5f; }
            int was = VisibilityComputer.CullAbove;
            try
            {
                foreach (ViewQuery q in new[] { ViewQuery.Circle(Vector2.zero, 60f), ViewQuery.Cone(Vector2.zero, 0.7f, 0.5f, 70f), ViewQuery.Cone(new Vector2(10f, -5f), -2.5f, 0.4f, 50f) })
                {
                    VisibilityComputer.CullAbove = int.MaxValue;
                    float exact = Area(Compute(set, q));
                    VisibilityComputer.CullAbove = 100;
                    var vc = new VisibilityComputer(set);
                    var poly = new List<Vector2>();
                    vc.Compute(q, poly);
                    Assert.AreEqual(exact, Area(poly), exact * 0.005f, "the same visible area");
                    Assert.Less(VisibilityComputer.LastKept, VisibilityComputer.LastQueried, "hidden segments are dropped");
                }
            }
            finally { VisibilityComputer.CullAbove = was; }
        }

        [Test]
        public void SpatialHash_QueryReturnsOnlyNearbySegments()
        {
            var set = new OccluderSet();
            Box(set, 0f, 0f, 1f, 1f);
            Box(set, 100f, 100f, 101f, 101f);
            var ids = new List<int>();
            set.Query(-2f, -2f, 2f, 2f, ids);
            Assert.AreEqual(4, ids.Count);
        }

        [Test]
        public void Unregister_RemovesSegments()
        {
            var set = new OccluderSet();
            int h = Box(set, 0f, 0f, 1f, 1f);
            Assert.AreEqual(4, set.ActiveSegmentCount);
            set.Unregister(h);
            Assert.AreEqual(0, set.ActiveSegmentCount);
        }

        [Test]
        public void RaySegment_Basics()
        {
            Assert.AreEqual(3f, VisibilityComputer.RaySegment(0, 0, 1, 0, 3, -1, 3, 1), Tol);
            Assert.IsTrue(float.IsPositiveInfinity(VisibilityComputer.RaySegment(0, 0, -1, 0, 3, -1, 3, 1)));
            Assert.IsTrue(float.IsPositiveInfinity(VisibilityComputer.RaySegment(0, 0, 1, 0, 3, 1, 3, 2)));
        }
    }
}
