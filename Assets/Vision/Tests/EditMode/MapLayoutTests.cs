using NUnit.Framework;
using UnityEngine;
using Vision.World;

namespace Vision.Tests
{
    public class MapLayoutTests
    {
        [Test]
        public void Map_IsTheOriginalAtThreeCentimetresPerUnit()
        {
            var m = new MapLayout(1337);
            Assert.AreEqual(90f, MapLayout.HalfExtentFor(), 1e-4f, "6000 units x 3 cm = 180 m");
            Assert.AreEqual(36f, m.Building.width, 1e-4f, "1200-unit building = 36 m");
            Assert.AreEqual(Vector2.zero, m.Building.center, "in the centre");
            Assert.AreEqual(m.Building.yMax, m.Yard.yMin, 1e-4f, "the exit yard is outside the north wall");
            Assert.That(m.Yard.width, Is.EqualTo(9f).Within(1e-4f));
            Assert.Less(m.Spawn.y, -65f, "survivors spawn in the south");
        }

        [Test]
        public void Layout_FollowsTheOriginalsRules()
        {
            foreach (int seed in new[] { 1, 2, 3, 1337, 99 })
            {
                var m = new MapLayout(seed);
                Assert.That(m.Clearings.Count, Is.InRange(9, 11), $"seed {seed}: spawn plus ~10 clearings");
                for (int i = 1; i < m.Clearings.Count; i++)
                    for (int j = i + 1; j < m.Clearings.Count; j++)
                        Assert.Greater((m.Clearings[i].Centre - m.Clearings[j].Centre).magnitude, 650f * MapLayout.Unit - 1e-3f, "clearings spread out");
                Assert.AreEqual(3, m.Cabins.Count, "three cabins");
                Assert.AreEqual(3, m.WoodsGenerators.Count, "three generators in the woods (two more in the building)");
                Assert.That(m.LakeRadius, Is.InRange(480f * MapLayout.Unit, 580f * MapLayout.Unit));
                Assert.Greater(Mathf.Abs(m.LakeCentre.x), 50f, "lake in a corner");
                Assert.Greater(Mathf.Abs(m.LakeCentre.y), 50f);
                Assert.LessOrEqual(m.GrassPatches.Count, 12);
                Assert.Greater(m.GrassPatches.Count, 6);
                Assert.LessOrEqual(m.Fences.Count, 7);
                Assert.Greater(m.PowerPoles.Count, 4, "a power line crosses the map");
                foreach (Vector2 g in m.WoodsGenerators) Assert.IsFalse(m.Building.Contains(g));
                foreach (Rect site in new[] { m.Graveyard, m.Playground })
                {
                    Assert.IsFalse(site.Overlaps(m.Building), $"seed {seed}: sites stay out of the building");
                    Assert.Less(m.LakeDepth(site.center), -5f, "and out of the lake");
                    Assert.Less(Mathf.Abs(site.center.x), 90f);
                }
                Assert.Less(m.LakeDepth(m.HangingTree), -5f);
            }
        }

        [Test]
        public void Layout_IsDeterministicPerSeed()
        {
            MapLayout a = new MapLayout(42), b = new MapLayout(42), c = new MapLayout(43);
            Assert.AreEqual(a.Spawn, b.Spawn);
            Assert.AreEqual(a.LakeCentre, b.LakeCentre);
            Assert.AreEqual(a.Graveyard, b.Graveyard);
            Assert.IsFalse(a.LakeCentre == c.LakeCentre && a.Spawn == c.Spawn, "another seed, another map");
        }

        [Test]
        public void Lake_IsWaded_ButNotTheDock()
        {
            var m = new MapLayout(1337);
            var f = new TerrainField(1337, 90f);
            f.SetLake(m);
            Assert.IsTrue(f.InWater(m.LakeCentre.x, m.LakeCentre.y), "the middle of the lake is water");
            Assert.Less(f.Height(m.LakeCentre.x, m.LakeCentre.y), f.LakeLevel - 0.5f, "the bed is under the surface");
            Vector2 onDock = Vector2.Lerp(m.DockStart, m.DockEnd, 0.8f);
            Assert.IsFalse(f.InWater(onDock.x, onDock.y), "standing on the dock is dry");
            Vector2 far = m.LakeCentre + (m.LakeCentre.normalized * -1f) * (m.LakeRadius * 2f);
            Assert.IsFalse(f.InWater(far.x, far.y));
        }
    }
}
