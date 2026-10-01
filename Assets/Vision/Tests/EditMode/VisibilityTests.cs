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
