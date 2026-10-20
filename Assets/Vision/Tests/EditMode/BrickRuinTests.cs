using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.World;

namespace Vision.Tests
{
    /// <summary>
    /// The brick ruins: bricks at their new size (two to the wall's depth), rust-brown with grey weathering; walls with
    /// windows, doors and arches cut through; fragments, corners and the shells of churches, warehouses and houses, all
    /// upright walls on one flat base, within their triangle budgets, the same from the same seed.
    /// </summary>
    public class BrickRuinTests
    {
        static RuinPlan Plan(RuinArchetype kind, int seed) => RuinPlanner.Generate(new System.Random(seed), kind);

        [Test]
        public void Bricks_AreSixtyPercentSmaller_TwoToTheWall_AndACourseIsWhatItShouldBe()
        {
            Assert.AreEqual(0.2f, LowPolyModels.BrickLength, 1e-5f, "0.5 m less 60%");
            Assert.AreEqual(0.088f, LowPolyModels.BrickCourse, 1e-5f, "0.22 m less 60%");
            Assert.AreEqual(0.144f, LowPolyModels.BrickDepth, 1e-5f, "0.36 m less 60%");
            Assert.AreEqual(0.288f, LowPolyModels.WallThickness, 1e-5f, "two bricks back to back");
            Assert.AreEqual(LowPolyModels.WallThickness, SandboxWorld.RuinThickness);

            // A 2 m wall: 23 courses; its mesh 0.288 m deep.
            var run = new RuinRun { A = Vector2.zero, B = new Vector2(3f, 0f), Height = 2f, Seed = 5 };
            RuinBricks.Wall w = RuinBricks.Lay(run);
            int top = w.Bricks.Max(b => Mathf.RoundToInt((b.Y1) / LowPolyModels.BrickCourse));
            Assert.That(top, Is.InRange(Mathf.RoundToInt(2f / 0.088f) - 4, Mathf.RoundToInt(2f / 0.088f) + 4), "about 23 courses, give or take the broken top");
            var plan = new RuinPlan { Archetype = RuinArchetype.Fragment };
            plan.Runs.Add(run);
            Mesh m = RuinBricks.Build(plan, new System.Random(1)).Builders[0].ToMesh("Brick Wall Ruin");
            float depth = m.vertices.Where(v => Mathf.Abs(v.y - 1f) < 0.5f).Max(v => v.z) - m.vertices.Where(v => Mathf.Abs(v.y - 1f) < 0.5f).Min(v => v.z);
            Assert.AreEqual(0.288f, depth, 0.01f, "the wall itself 0.288 m deep");
            // Running bond: each course half a brick along from the one below (the joints don't line up).
            var front = w.Bricks.Where(b => b.Wythe == 0 && b.Y1 < 1f && b.X0 > 0.3f && b.X1 < 2.7f).ToList();
            var c0 = front.Where(b => Mathf.RoundToInt(b.Y1 / 0.088f) == 3).Select(b => b.X0).ToList();
            var c1 = front.Where(b => Mathf.RoundToInt(b.Y1 / 0.088f) == 4).Select(b => b.X0).ToList();
            float nearest = c1.Average(x => c0.Min(y => Mathf.Abs(x - y)));
            Assert.Greater(nearest, 0.05f, "the joints step half a brick course to course");
        }

        [Test]
        public void TheBricks_AreRustBrown_InVaryingTones_WithGreyPatches_AndGreyMortar()
        {
            var run = new RuinRun { A = Vector2.zero, B = new Vector2(20f, 0f), Height = 2.4f, Seed = 77 };
            RuinBricks.Wall w = RuinBricks.Lay(run);
            var hsv = w.Bricks.Select(b => { Color.RGBToHSV(b.Color, out float h, out float s, out float v); return (h, s, v, b.Gray); }).ToList();
            var colored = hsv.Where(x => !x.Gray).ToList();
            float hue = colored.Average(x => x.h > 0.5f ? x.h - 1f : x.h), sat = hsv.Average(x => x.s);
            float meanV = hsv.Average(x => x.v), sdV = Mathf.Sqrt(hsv.Average(x => (x.v - meanV) * (x.v - meanV)));
            float grayShare = hsv.Count(x => x.Gray) / (float)hsv.Count;
            Debug.Log($"[Ruins] {hsv.Count} bricks: hue {hue:0.000}, saturation {sat:0.00}, value sd {sdV:0.000}, grey {grayShare:P1}");
            Assert.That(hue, Is.InRange(0.02f, 0.06f), "rust-brown red");
            Assert.Greater(sat, 0.35f, "not washed out");
            Assert.That(grayShare, Is.InRange(0.02f, 0.06f), "a few grey bricks");
            Assert.Greater(sdV, 0.04f, "varied tones");
            Assert.IsTrue(hsv.Any(x => !x.Gray && x.s < 0.25f), "grey weathered patches over some");
            foreach (Color c in w.CoreColors)
            {
                Color.RGBToHSV(c, out _, out float s, out _);
                Assert.Less(s, 0.15f, "grey mortar");
            }
        }

        [Test]
        public void Openings_AreCutThrough_ArchesHaveAnOddRingWithAKeystone_AndLintelsAndSillsAreStone()
        {
            var run = new RuinRun { A = Vector2.zero, B = new Vector2(9f, 0f), Height = 3.6f, Seed = 9 };
            run.Openings.Add(new RuinOpening { Kind = RuinOpeningKind.Window, Center = 1.6f, Width = 1f, Sill = 0.9f, Top = 2f });
            run.Openings.Add(new RuinOpening { Kind = RuinOpeningKind.Door, Center = 4.2f, Width = 1f, Sill = 0f, Top = 2.1f });
            run.Openings.Add(new RuinOpening { Kind = RuinOpeningKind.Arch, Center = 7f, Width = 1.2f, Sill = 0f, Top = 1.8f });
            RuinBricks.Wall w = RuinBricks.Lay(run);
            foreach (RuinOpening o in run.Openings)
            {
                // No brick inside the hole.
                Assert.IsFalse(w.Bricks.Any(b => RuinRun.Covers(o, (b.X0 + b.X1) * 0.5f, (b.Y0 + b.Y1) * 0.5f, -0.02f)), $"nothing laid in the {o.Kind}");
            }
            Assert.AreEqual(3, w.Stones.Count, "the window's sill and lintel, the door's lintel");
            int ring = w.Arches.Count;
            Assert.AreEqual(1, ring % 2, "an odd number of radial bricks");
            Assert.Greater(ring, 15);
            var crown = w.Arches[ring / 2];
            Assert.AreEqual(Mathf.PI * 0.5f, (crown.A0 + crown.A1) * 0.5f, 1e-3f, "a keystone at the crown");

            // Play: through the door and the arch, not the window (but seen through it).
            List<RuinSolid> solids = RuinSolids.Of(run);
            foreach (RuinOpening o in run.Openings.Where(o => o.Passage))
            {
                Assert.IsFalse(solids.Any(s => s.X1 > o.Center - o.Half + 0.05f && s.X0 < o.Center + o.Half - 0.05f), $"a way through the {o.Kind}");
                float gap = solids.Where(s => s.X0 >= o.Center).Min(s => s.X0) - solids.Where(s => s.X1 <= o.Center).Max(s => s.X1);
                Assert.GreaterOrEqual(gap, 0.9f, "wide enough to walk through");
            }
            RuinOpening win = run.Openings[0];
            Assert.IsTrue(solids.Any(s => s.SeeThrough && s.X0 <= win.Center && s.X1 >= win.Center), "a window: solid, seen through");
            Assert.IsFalse(solids.Any(s => !s.SeeThrough && s.X0 < win.Center && s.X1 > win.Center), "and nothing blocking sight across it");
        }

        [Test]
        public void EveryKindOfRuin_IsUprightWalls_WithinItsBudget_AndTheSameFromTheSameSeed()
        {
            var report = new System.Text.StringBuilder();
            foreach (RuinArchetype kind in System.Enum.GetValues(typeof(RuinArchetype)))
            {
                int worst = 0;
                for (int seed = 1; seed <= 6; seed++)
                {
                    RuinPlan plan = Plan(kind, seed);
                    Assert.AreEqual(kind, plan.Archetype);
                    Assert.Greater(plan.Runs.Count, 0);
                    Rect b = plan.Bounds;
                    Assert.LessOrEqual(Mathf.Max(b.width, b.height), RuinPlanner.MaxExtent, $"{kind}: no bigger than 18 m or so");
                    foreach (RuinRun r in plan.Runs)
                    {
                        Assert.Greater(r.Length, 0.5f);
                        foreach (RuinOpening o in r.Openings)
                        {
                            Assert.GreaterOrEqual(o.Center - o.Half, 0.55f, "openings clear of the wall's ends");
                            Assert.LessOrEqual(o.Center + o.Half, r.Length - 0.55f);
                            if (o.Kind == RuinOpeningKind.Window) Assert.GreaterOrEqual(o.Sill, 0.6f, "a window's sill off the ground");
                            if (o.Kind == RuinOpeningKind.Door) Assert.AreEqual(0f, o.Sill);
                        }
                    }
                    RuinBricks.Chunks chunks = RuinBricks.Build(plan, new System.Random(seed));
                    int tris = chunks.Triangles;
                    worst = Mathf.Max(worst, tris);
                    foreach (LowPolyMeshBuilder c in chunks.Builders) Assert.LessOrEqual(c.TriangleCount, RuinBricks.Chunks.Cap, "each chunk within its cap");
                    // (The big building, one to a map, has more wall than any other.)
                    int budget = kind == RuinArchetype.Complex ? 90000 : plan.Structure ? 40000 : kind == RuinArchetype.Corner ? 10000 : 6000;
                    Assert.LessOrEqual(tris, budget, $"{kind} seed {seed}: {tris} triangles");
                    // The same seed, the same ruin.
                    RuinBricks.Chunks again = RuinBricks.Build(Plan(kind, seed), new System.Random(seed));
                    Assert.AreEqual(Hash(chunks), Hash(again), $"{kind} seed {seed}: the same every time");
                }
                report.Append($"{kind} up to {worst} tris; ");
            }
            Debug.Log("[Ruins] " + report);
        }

        static long Hash(RuinBricks.Chunks c)
        {
            long h = 17;
            foreach (LowPolyMeshBuilder b in c.Builders)
            {
                Mesh m = b.ToMesh("x");
                foreach (Vector3 v in m.vertices) h = h * 31 + Mathf.RoundToInt(v.x * 1000f) * 7 + Mathf.RoundToInt(v.y * 1000f) * 13 + Mathf.RoundToInt(v.z * 1000f);
                Object.DestroyImmediate(m);
            }
            return h;
        }

        [Test]
        public void Buildings_HaveHallwaysPassagesAndNooks_OfTheirSizes()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                RuinPlan house = Plan(RuinArchetype.Residential, seed);
                Assert.Greater(house.Outline.Count, 5, "an L or a T");
                Assert.AreEqual(1, house.Hallways.Count, "a hallway from the front door");
                Assert.AreEqual(RuinPlanner.HallWidth, house.Hallways[0].x, 1e-4f);
                Assert.LessOrEqual(house.Hallways[0].y, RuinPlanner.HallMaxLength);
                foreach (float p in house.Passages) Assert.That(p, Is.InRange(RuinPlanner.PassageMin - 1e-4f, RuinPlanner.PassageMax + 1e-4f), "a passage between rooms");
                Assert.IsTrue(house.Runs.Any(r => !r.Exterior), "rooms inside");
                Assert.IsTrue(house.Runs.SelectMany(r => r.Openings).Any(o => o.Kind == RuinOpeningKind.Door), "a front door");

                RuinPlan church = Plan(RuinArchetype.Church, seed);
                Assert.IsTrue(church.Runs.SelectMany(r => r.Openings).Count(o => o.Kind == RuinOpeningKind.Arch) >= 4, "arched door and windows");
                Assert.IsTrue(church.Runs.Any(r => r.Gable > 0f), "a gable end");
                Assert.IsTrue(church.Runs.Any(r => Mathf.Abs(r.Dir.x) > 0.2f && Mathf.Abs(r.Dir.y) > 0.2f), "the apse's angled walls");

                RuinPlan shed = Plan(RuinArchetype.Warehouse, seed);
                Assert.IsTrue(shed.Runs.SelectMany(r => r.Openings).Any(o => o.Kind == RuinOpeningKind.Door && o.Width >= 3f), "a wide door");
                Assert.IsTrue(shed.Runs.SelectMany(r => r.Openings).Count(o => o.Kind == RuinOpeningKind.Window && o.Sill >= 1.4f) >= 4, "a row of high windows");
            }
        }
    }
}
