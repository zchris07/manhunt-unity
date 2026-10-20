using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Player;
using Vision.World;

namespace Vision.Tests
{
    /// <summary>
    /// The three brick structures every map has (a big building of hallways, rooms and nooks; a plain rectangle part
    /// fallen open; half a rectangle's frame, two corners at most), each with a stone floor laid within its walls, in
    /// detail and weathered; every brick 10% less saturated; and a bottle breaking on someone sounding like a window's
    /// glass, softer.
    /// </summary>
    public class RuinStructuresAndFloorsTests
    {
        static RuinPlan Plan(RuinArchetype kind, int seed) => RuinPlanner.Generate(new System.Random(seed), kind);

        static float Overlap(Rect a, Rect b) => Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin)) * Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));

        [Test]
        public void TheBigBuilding_HasHallwaysRoomsAndNooks_AndItsFloorFillsItsWalls()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                RuinPlan p = Plan(RuinArchetype.Complex, seed);
                Rect b = p.Bounds;
                Assert.That(Mathf.Max(b.width, b.height), Is.InRange(14f, RuinPlanner.MaxExtent), "large");
                Assert.GreaterOrEqual(p.Hallways.Count, 2, "a main hallway and a cross hallway");
                Assert.IsTrue(p.Hallways.Any(h => h.x >= RuinPlanner.ComplexHallWidth - 1e-3f && h.y >= 8f), "the main one long and wide");
                Assert.GreaterOrEqual(p.Rooms.Count, 5, "four rooms and a hall");
                foreach (Rect r in p.Rooms) Assert.Greater(r.width * r.height, 9f, "rooms worth the name");
                Assert.GreaterOrEqual(p.Nooks.Count, 2, "nooks");
                Assert.GreaterOrEqual(p.Runs.Count(r => !r.Exterior), 9, "walls inside");
                var openings = p.Runs.SelectMany(r => r.Openings).ToList();
                Assert.GreaterOrEqual(openings.Count(o => o.Passage), 7, "a front door, a side door and a door into every room");
                foreach (RuinOpening o in openings.Where(o => o.Passage)) Assert.GreaterOrEqual(o.Width, 1.1f, "wide enough for Zach");
                Assert.GreaterOrEqual(p.Corners, 4);
                // Its floor: a runner down each hallway, a chequer in the hall, the rooms' floors, tiling the inside exactly.
                Assert.IsTrue(p.Floored);
                Assert.AreEqual(2, p.Floor.Count(z => z.Laying == FloorLaying.Runner));
                Assert.AreEqual(1, p.Floor.Count(z => z.Laying == FloorLaying.Checker));
                float area = p.Floor.Sum(z => z.Area.width * z.Area.height);
                Assert.AreEqual(b.width * b.height, area, (b.width + b.height) * LowPolyModels.WallThickness * 1.2f, "the whole inside floored");
                for (int i = 0; i < p.Floor.Count; i++)
                    for (int j = i + 1; j < p.Floor.Count; j++)
                        Assert.Less(Overlap(p.Floor[i].Area, p.Floor[j].Area), 1e-3f, "no stretch laid over another");
                foreach (FloorZone z in p.Floor) Assert.IsTrue(p.Inside(z.Area.center), "inside the walls");
            }
        }

        [Test]
        public void ThePlainRectangle_IsSemiEnclosed_FourWallsWithMuchOfOneFallenOpen()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                RuinPlan p = Plan(RuinArchetype.Enclosure, seed);
                Assert.AreEqual(4, p.Runs.Count, "four walls");
                Assert.AreEqual(4, p.Corners, "a rectangle");
                Assert.IsTrue(p.Runs.All(r => Mathf.Abs(r.Dir.x) < 1e-3f || Mathf.Abs(r.Dir.y) < 1e-3f), "square to each other");
                RuinRun open = p.Runs.FirstOrDefault(r => r.Breaches.Any(br => br.z <= 0.01f));
                Assert.NotNull(open, "one side fallen to the ground");
                // A way in across the fallen stretch, a good width.
                List<RuinSolid> solids = RuinSolids.Of(open);
                float widest = 0f, last = 0f;
                foreach (RuinSolid s in solids.Where(s => !s.Low).OrderBy(s => s.X0)) { widest = Mathf.Max(widest, s.X0 - last); last = s.X1; }
                widest = Mathf.Max(widest, open.Length - last);
                Assert.GreaterOrEqual(widest, 1.5f, "open: walked into");
                foreach (RuinRun r in p.Runs.Where(r => r != open))
                    Assert.Greater(Enumerable.Range(0, 21).Max(k => r.TopAt(r.Length * k / 20f)), 1.2f, "the other three standing");
                Assert.AreEqual(1, p.Floor.Count);
                Assert.IsTrue(p.Floor[0].Exposed);
                Assert.AreEqual(p.Bounds.width * p.Bounds.height, p.Floor[0].Area.width * p.Floor[0].Area.height, (p.Bounds.width + p.Bounds.height) * 0.6f);
            }
        }

        [Test]
        public void HalfAFrame_HasTwoCornersAtMost_AndItsFloorFillsTheRectangle()
        {
            bool sawU = false, sawL = false;
            for (int seed = 1; seed <= 20; seed++)
            {
                RuinPlan p = Plan(RuinArchetype.HalfFrame, seed);
                Assert.That(p.Corners, Is.InRange(1, 2), "two corners at most");
                Assert.That(p.Runs.Count, Is.InRange(2, 3));
                sawU |= p.Corners == 2;
                sawL |= p.Corners == 1;
                Assert.AreEqual(p.Runs.Count - 1, p.Corners, "an open frame: no wall closes it");
                Rect b = p.Bounds, f = p.Floor[0].Area;
                Assert.Greater(Mathf.Max(f.width, f.height), 6f, "bigger than a corner");
                // The floor fills the rectangle the frame would close.
                foreach (RuinRun r in p.Runs)
                {
                    Assert.IsTrue(Mathf.Approximately(r.A.x, f.xMin) || Mathf.Approximately(r.A.x, f.xMax) || Mathf.Approximately(r.A.y, f.yMin) || Mathf.Approximately(r.A.y, f.yMax), "its walls on the floor's edges");
                }
                Assert.AreEqual(f.width * f.height, (b.width - LowPolyModels.WallThickness) * (b.height - LowPolyModels.WallThickness), 0.6f);
            }
            Assert.IsTrue(sawU && sawL, "U-shaped and L-shaped both");
        }

        [Test]
        public void StoneFloors_AreLaidInsideTheWalls_InDetail_AndWeathered()
        {
            var report = new System.Text.StringBuilder();
            foreach (RuinArchetype kind in SandboxWorld.RequiredRuins)
            {
                RuinPlan p = Plan(kind, 3);
                Mesh m = RuinFloors.Build(p, new System.Random(5), 0.03f, out RuinFloors.Laid laid);
                int tris = m.triangles.Length / 3;
                report.Append($"{kind}: {tris} tris, {laid.Stones} stones, {laid.Kerbs} kerbs, {laid.Cracked} cracked, {laid.Chipped} chipped, {laid.Missing} gone, {laid.Sunk} sunk, {laid.Lichen} lichen, {laid.Stains} stains, {laid.Chips} chips, {laid.Thresholds} sills, {laid.Chequer} chequer; ");
                Assert.LessOrEqual(tris, kind == RuinArchetype.Complex ? 24000 : 8000, $"{kind}: within budget");
                Assert.Greater(laid.Stones, p.Floor.Sum(z => z.Area.width * z.Area.height) * 2f, "stones a hand or two across");
                Assert.Greater(laid.Kerbs, 10, "a kerb course");
                Assert.Greater(laid.Stains, 0);
                Assert.Greater(laid.Chips, 5, "grit about");
                // Within its walls, a little proud of the ground and no more.
                foreach (Vector3 v in m.vertices)
                {
                    Assert.IsTrue(p.OnFloor(new Vector2(v.x, v.z), LowPolyModels.WallThickness * 0.5f + 0.01f), $"{kind}: laid within the walls ({v})");
                    Assert.That(v.y, Is.InRange(0.03f, 0.03f + 0.08f));
                }
                // Discoloured: the stones' tops vary, and some carry the damp's green.
                Vector3[] n = m.normals;
                Color[] c = m.colors;
                var tops = Enumerable.Range(0, c.Length).Where(i => n[i].y > 0.95f).Select(i => c[i].gamma).ToList();
                float mean = tops.Average(x => x.grayscale), sd = Mathf.Sqrt(tops.Average(x => (x.grayscale - mean) * (x.grayscale - mean)));
                Assert.Greater(sd, 0.04f, $"{kind}: varied, stone to stone and across each");
                if (p.Floor.Any(z => z.Exposed)) Assert.IsTrue(tops.Any(x => x.g > x.r + 0.02f && x.g > x.b + 0.02f), $"{kind}: moss creeping in");
                // The same every time.
                Mesh again = RuinFloors.Build(Plan(kind, 3), new System.Random(5), 0.03f, out _);
                CollectionAssert.AreEqual(m.vertices, again.vertices);
                Object.DestroyImmediate(m);
                Object.DestroyImmediate(again);
            }
            // Over enough of them, every kind of wear.
            var all = new RuinFloors.Laid();
            for (int seed = 1; seed <= 4; seed++)
            {
                RuinFloors.Build(Plan(RuinArchetype.Complex, seed), new System.Random(seed), 0f, out RuinFloors.Laid l);
                all.Cracked += l.Cracked; all.Chipped += l.Chipped; all.Missing += l.Missing; all.Sunk += l.Sunk; all.Lichen += l.Lichen; all.Thresholds += l.Thresholds; all.Chequer += l.Chequer;
            }
            Assert.Greater(all.Cracked, 0, "cracked stones");
            Assert.Greater(all.Chipped, 0, "chipped corners");
            Assert.Greater(all.Missing, 0, "a stone gone here and there");
            Assert.Greater(all.Sunk, 0, "sunk ones");
            Assert.Greater(all.Lichen, 0, "lichen");
            Assert.Greater(all.Thresholds, 4 * 6, "a sill in every doorway");
            Assert.Greater(all.Chequer, 4 * 20, "the hall's chequer");
            Debug.Log("[Ruins] floors: " + report);
        }

        [Test]
        public void EveryMap_HasTheBigBuilding_ThePlainRectangle_AndTheHalfFrame()
        {
            var go = new GameObject("World");
            try
            {
                var world = go.AddComponent<SandboxWorld>();
                var counts = new Dictionary<int, string>();
                for (int seed = 1; seed <= 24; seed++)
                {
                    world.seed = seed * 7919 + 13;
                    List<RuinArchetype> kinds = world.SitedRuinKinds(new MapLayout(world.seed, world.halfExtent, world.generatorCount));
                    foreach (RuinArchetype k in SandboxWorld.RequiredRuins) Assert.Contains(k, kinds, $"seed {world.seed}: a {k}");
                    Assert.LessOrEqual(kinds.Count, 4);
                    counts[world.seed] = string.Join(",", kinds);
                }
                Debug.Log("[Ruins] sited: " + string.Join("; ", counts.Values.Take(6)));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void EveryBrick_IsTenPercentLessSaturated()
        {
            Assert.AreEqual(0.9f, RuinBricks.Saturation, 1e-6f);
            var c = new Color(0.47f, 0.22f, 0.15f);
            Color.RGBToHSV(c, out float h0, out float s0, out float v0);
            Color.RGBToHSV(RuinBricks.Desaturate(c, RuinBricks.Saturation), out float h1, out float s1, out float v1);
            Assert.AreEqual(s0 * 0.9f, s1, 1e-4f);
            Assert.AreEqual(h0, h1, 1e-4f);
            Assert.AreEqual(v0, v1, 1e-4f);
            // The same wall as before (0.57 on average then).
            var run = new RuinRun { A = Vector2.zero, B = new Vector2(20f, 0f), Height = 2.4f, Seed = 77 };
            float sat = RuinBricks.Lay(run).Bricks.Average(b => { Color.RGBToHSV(b.Color, out _, out float s, out _); return s; });
            Assert.AreEqual(0.57f * 0.9f, sat, 0.01f, "10% less saturated than they were");
        }

        static List<GameEvent> Fly(SimRig rig, int ticks)
        {
            var all = new List<GameEvent>();
            for (int t = 0; t < ticks; t++)
            {
                rig.Sim.Step();
                all.AddRange(rig.Sim.Events);
                rig.Sim.Events.Clear();
            }
            return all;
        }

        [Test]
        public void ABottleBreakingOnSomeone_SoundsLikeAWindowsGlass_AtSixtyPercent()
        {
            Assert.AreEqual(0.6f, Audio.SoundBank.BottleOnBodyVolume, 1e-6f);
            var rig = new SimRig(2, 1);
            SimPlayer zach = rig.P(1), thrower = rig.P(2), other = rig.P(3);
            rig.Place(thrower, 0f, 0f);
            rig.Place(zach, 0f, 300f);
            rig.Place(other, 2000f, 0f);
            rig.Run(2);
            // On Zach.
            rig.Sim.Thrown.Add(new ThrownItem { Id = 950, Pos = SimRig.U(0f, 40f), Dir = Vector2.up, Owner = thrower.Id, Item = ItemType.Bottle });
            List<GameEvent> ev = Fly(rig, SimRig.Secs(1f));
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Noise && e.Text == "glass_body"), "broken on Zach: the window's glass, softer");
            Assert.IsFalse(ev.Any(e => e.Kind == EventKind.Noise && e.Text == "glass"));
            // On a survivor.
            rig.Place(zach, -2000f, 0f);
            rig.Place(other, 300f, 0f);
            rig.Run(2);
            rig.Sim.Thrown.Add(new ThrownItem { Id = 951, Pos = SimRig.U(40f, 0f), Dir = Vector2.right, Owner = thrower.Id, Item = ItemType.Bottle });
            ev = Fly(rig, SimRig.Secs(1f));
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Noise && e.Text == "glass_body"), "broken on a survivor too");
            // Landing on nobody (near the end of its flight, within everyone's hearing): the bottle's own break.
            rig.Sim.Thrown.Add(new ThrownItem { Id = 952, Pos = SimRig.U(0f, -40f), Dir = Vector2.down, Owner = thrower.Id, Item = ItemType.Bottle, Travelled = rig.Map.HalfExtent * 2.9f - 0.5f });
            ev = Fly(rig, SimRig.Secs(1f));
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Noise && e.Text == "glass" && e.F >= 900f), "a bottle smashing on the ground");
            Assert.IsFalse(ev.Any(e => e.Kind == EventKind.Noise && e.Text == "glass_body"));
            // A book on someone is still a book.
            rig.Sim.Thrown.Add(new ThrownItem { Id = 953, Pos = SimRig.U(40f, 0f), Dir = Vector2.right, Owner = thrower.Id, Item = ItemType.Book });
            ev = Fly(rig, SimRig.Secs(1f));
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Noise && e.Text == "book"));
        }
    }
}
