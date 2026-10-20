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
        public void HalfAFrame_IsARectangleCutOnTheDiagonal_OneCornerTwoWalls_AndItsFloorEdgeIsAStaggeredStaircase()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                RuinPlan p = Plan(RuinArchetype.HalfFrame, seed);
                Assert.AreEqual(2, p.Runs.Count, "two walls");
                Assert.AreEqual(1, p.Corners, "one corner: two at most");
                Assert.AreEqual(1, p.Floor.Count);
                FloorZone z = p.Floor[0];
                Assert.IsTrue(z.Cut && z.Exposed);
                Rect f = z.Area;
                Assert.Greater(Mathf.Max(f.width, f.height), 6f);
                // The cut runs corner to corner of the rectangle, away from the walls' meeting point.
                Assert.AreEqual(Mathf.Sqrt(f.width * f.width + f.height * f.height), (z.CutB - z.CutA).magnitude, 1e-3f);
                Assert.Greater(z.Depth(z.Keep), Mathf.Min(f.width, f.height) * 0.5f, "the corner is well inside");
                Assert.IsTrue(p.OnFloor(z.Keep + (f.center - z.Keep) * 0.1f));
                Assert.IsFalse(p.OnFloor(f.center * 2f - z.Keep - (f.center - z.Keep) * 0.2f), "nothing on the far side of the diagonal");

                Mesh m = RuinFloors.Build(p, new System.Random(seed), 0f, out RuinFloors.Laid laid);
                Assert.Greater(laid.Edge, 8, "a row of edge stones along the diagonal");
                // The top faces: none well past the line (the loose stones and grit lie just outside it), the edge ragged.
                Vector3[] vs = m.vertices, ns = m.normals;
                var depths = new List<float>();
                for (int k = 0; k < vs.Length; k++)
                {
                    float dpt = z.Depth(new Vector2(vs[k].x, vs[k].z));
                    Assert.Greater(dpt, -1.3f, "stones and soil within a metre of the line");
                    if (ns[k].y > 0.95f && vs[k].y > RuinFloors.StoneTop && dpt < 0.2f && dpt > -0.7f) depths.Add(dpt);
                }
                Assert.Greater(depths.Count, 10, "stones lying on the line");
                float mean = depths.Average(), sd = Mathf.Sqrt(depths.Average(x => (x - mean) * (x - mean)));
                Assert.Greater(sd, 0.05f, "not perfectly straight");
                // Whole stones sit on top of the ground at the edge: their sides reach below it and show above it.
                Assert.Less(vs.Min(v => v.y), -0.01f, "edge stones' sides go down into the dirt");
                Assert.Greater(vs.Where(v => z.Depth(new Vector2(v.x, v.z)) < 0.5f).Max(v => v.y), RuinFloors.StoneTop + 0.006f, "edge stones stand proud of it");
                Object.DestroyImmediate(m);
            }
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
                Assert.Greater(laid.Stones, p.Floor.Sum(z => z.Area.width * z.Area.height * (z.Cut ? 0.5f : 1f)) * 2f, "stones a hand or two across");
                Assert.Greater(laid.Kerbs, 10, "a kerb course");
                Assert.Greater(laid.Stains, 0);
                Assert.Greater(laid.Chips, 5, "grit about");
                // Within its walls, a little proud of the ground and no more.
                foreach (Vector3 v in m.vertices)
                {
                    Assert.IsTrue(p.OnFloor(new Vector2(v.x, v.z), kind == RuinArchetype.HalfFrame ? 1.3f : LowPolyModels.WallThickness * 0.5f + 0.01f), $"{kind}: laid within the walls ({v})");
                    Assert.That(v.y, Is.InRange(0.03f - 0.045f, 0.03f + 0.08f));
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
            // Every brick's colour comes through the desaturation (none more saturated than its tone allows).
            var run = new RuinRun { A = Vector2.zero, B = new Vector2(20f, 0f), Height = 2.4f, Seed = 77 };
            float sat = RuinBricks.Lay(run).Bricks.Average(b => { Color.RGBToHSV(b.Color, out _, out float s, out _); return s; });
            Debug.Log($"[Ruins] average brick saturation {sat:0.000}");
            Assert.Less(sat, 0.57f * 0.9f + 0.005f, "no more saturated than before the palette was drawn together");
        }

        [Test]
        public void TheBrickPalette_IsDrawnTwentyPercentToward9f6059_BeforeEachBricksOwnShifts()
        {
            Assert.AreEqual(0.2f, RuinBricks.UnifyBy, 1e-6f);
            Assert.AreEqual(new Color(159f / 255f, 96f / 255f, 89f / 255f), RuinBricks.Unify);
            var original = new[] { new Color(0.47f, 0.22f, 0.15f), new Color(0.40f, 0.20f, 0.14f), new Color(0.30f, 0.15f, 0.11f), new Color(0.58f, 0.31f, 0.20f), new Color(0.50f, 0.32f, 0.22f) };
            Color[] now = RuinBricks.Palette;
            Assert.AreEqual(original.Length, now.Length);
            float spreadBefore = 0f, spreadNow = 0f;
            for (int i = 0; i < now.Length; i++)
            {
                Color want = Color.Lerp(original[i], RuinBricks.Unify, 0.2f);
                Assert.AreEqual(want.r, now[i].r, 1e-5f);
                Assert.AreEqual(want.g, now[i].g, 1e-5f);
                Assert.AreEqual(want.b, now[i].b, 1e-5f);
                for (int j = 0; j < now.Length; j++)
                {
                    spreadBefore += Vector3.Distance((Vector4)original[i], (Vector4)original[j]);
                    spreadNow += Vector3.Distance((Vector4)now[i], (Vector4)now[j]);
                }
            }
            Assert.AreEqual(spreadBefore * 0.8f, spreadNow, spreadBefore * 0.01f, "the spread narrowed by a fifth");
        }

        [Test]
        public void EveryWalledShell_HasAStoneFloor_WithinItsWalls()
        {
            foreach (RuinArchetype kind in new[] { RuinArchetype.Church, RuinArchetype.Warehouse, RuinArchetype.Residential, RuinArchetype.Complex, RuinArchetype.Enclosure })
                for (int seed = 1; seed <= 6; seed++)
                {
                    RuinPlan p = Plan(kind, seed);
                    Assert.IsTrue(p.Floored, $"{kind} {seed}: floored");
                    Mesh m = RuinFloors.Build(p, new System.Random(seed), 0f, out RuinFloors.Laid laid);
                    Assert.Greater(laid.Stones, 60, $"{kind} {seed}");
                    foreach (Vector3 v in m.vertices)
                        Assert.IsTrue(p.OnFloor(new Vector2(v.x, v.z), LowPolyModels.WallThickness * 0.5f + 0.01f), $"{kind} {seed}: within its walls ({v})");
                    if (kind == RuinArchetype.Church)
                    {
                        // The apse's floor follows its angled walls: stones in it, none past its walls.
                        FloorZone apse = p.Floor.First(z => z.Clip != null);
                        Vector2 inApse = new Vector2(apse.Area.center.x, apse.Area.yMin + apse.Area.height * 0.4f);
                        Assert.IsTrue(p.OnFloor(inApse));
                        Assert.IsFalse(p.OnFloor(new Vector2(apse.Area.xMax - 0.05f, apse.Area.yMax - 0.05f), 0.1f), "the apse's outside corner bare");
                    }
                    Object.DestroyImmediate(m);
                }
        }

        [Test]
        public void ABrickRuinsWindow_IsVaulted_ItsColliderAlittleWiderThanTheHole_SightUnchanged()
        {
            var run = new RuinRun { A = Vector2.zero, B = new Vector2(6f, 0f), Height = 2.4f, Seed = 12 };
            var win = new RuinOpening { Kind = RuinOpeningKind.Window, Center = 3f, Width = 0.9f, Sill = 0.9f, Top = 1.8f };
            run.Openings.Add(win);
            RuinSolid Window(List<RuinSolid> l) => l.Single(s => s.SeeThrough);
            RuinSolid sight = Window(RuinSolids.Of(run)), body = Window(RuinSolids.Of(run, RuinSolids.WindowVaultPad));
            Assert.AreEqual(win.Width, sight.X1 - sight.X0, 0.06f, "sight: just the hole");
            Assert.AreEqual(win.Width + 2f * RuinSolids.WindowVaultPad, body.X1 - body.X0, 0.06f, "the vault: past it either side");
            Assert.Greater(body.X1 - body.X0, 0.6f + 0.3f, "a body (0.6 m across) fits through");
            // The bricks themselves are laid just as before.
            Assert.IsFalse(RuinBricks.Lay(run).Bricks.Any(b => RuinRun.Covers(win, (b.X0 + b.X1) * 0.5f, (b.Y0 + b.Y1) * 0.5f, -0.02f)));
        }

        [Test]
        public void CabinBoards_AreThirtyPercentSmaller_ButtedTight_NeverGrey_GrimyWithMossMouldAndStainsOverAreas()
        {
            Assert.AreEqual(0.16f * 0.7f, LowPolyModels.PlankWidthMin, 1e-5f);
            Assert.AreEqual(0.22f * 0.7f, LowPolyModels.PlankWidthMax, 1e-5f);
            Assert.AreEqual(1.2f * 0.7f, LowPolyModels.PlankLengthMin, 1e-5f);
            Assert.AreEqual(2.4f * 0.7f, LowPolyModels.PlankLengthMax, 1e-5f);
            foreach (Color c in LowPolyModels.PlankTones)
            {
                Color.RGBToHSV(c, out _, out float sat, out _);
                Assert.Greater(sat, 0.4f, "no grey planks: every one a wood's colour");
            }
            var all = new LowPolyModels.PlankTally();
            for (int seed = 1; seed <= 3; seed++)
            {
                Mesh m = LowPolyModels.BoardFloor(new System.Random(seed), 8.4f, 6f);
                LowPolyModels.PlankTally t = LowPolyModels.LastPlanks;
                Assert.That(t.Count, Is.InRange(8.4f * 6f / 0.24f, 8.4f * 6f / 0.12f), "the plank count up with their size down");
                Assert.Greater(t.Nails, t.Count, "nail heads");
                Assert.Less(m.triangles.Length / 3, 24000, "within budget");
                // Butted tight: the planks' tops cover the floor but for the pieces broken out (no dark line between them).
                Vector3[] v = m.vertices, n = m.normals;
                int[] tri = m.triangles;
                float covered = 0f;
                for (int k = 0; k < tri.Length; k += 3)
                {
                    Vector3 a = v[tri[k]], b2 = v[tri[k + 1]], c = v[tri[k + 2]];
                    if (n[tri[k]].y < 0.99f || a.y < LowPolyModels.CabinFloorTop - 0.004f || a.y > LowPolyModels.CabinFloorTop + 0.0011f) continue;
                    covered += Vector3.Cross(b2 - a, c - a).magnitude * 0.5f;
                }
                Assert.AreEqual(8.4f * 6f, covered, 8.4f * 6f * 0.035f + t.Missing * 0.06f, "the boards cover the floor, edge to edge");
                all.Knots += t.Knots; all.Cracks += t.Cracks; all.Holes += t.Holes; all.Missing += t.Missing; all.Count += t.Count;
                all.Rotten += t.Rotten; all.Mossy += t.Mossy; all.Moldy += t.Moldy; all.Stained += t.Stained;
                Object.DestroyImmediate(m);
            }
            Assert.Greater(all.Knots, 20, "knots");
            Assert.Greater(all.Cracks, 15, "splits");
            Assert.Greater(all.Holes, 0, "knot-holes");
            Assert.Greater(all.Missing, 0, "the odd piece broken out");
            Assert.Greater(all.Mossy, all.Count * 0.08f, "moss over areas of it");
            Assert.Greater(all.Moldy, all.Count * 0.05f, "mould over areas");
            Assert.Greater(all.Stained, all.Count * 0.03f, "water stains");
            Assert.Greater(all.Rotten, all.Count * 0.1f, "rot");
            Debug.Log($"[Ruins] boards: {all.Count} planks, knots {all.Knots}, splits {all.Cracks}, holes {all.Holes}, missing {all.Missing}, rotten {all.Rotten}, mossy {all.Mossy}, mouldy {all.Moldy}, stained {all.Stained}");
        }

        [Test]
        public void NpcPunches_SoundLikeTheMacheteInFlesh_GamerRageHalvedAgain_TheGeneratorHeardFurther()
        {
            foreach (string kind in new[] { "punch", "fist" })
            {
                Assert.AreEqual("slash", Vision.UI.MatchEffects.HitCue(kind, out float v), $"{kind}: the machete into flesh");
                Assert.AreEqual(1f, v, 1e-6f, "at the machete's own volume");
            }
            Assert.AreEqual("body_hit", Vision.UI.MatchEffects.HitCue("slash", out _));
            Assert.IsNull(Vision.UI.MatchEffects.HitCue("bullet", out _));
            Assert.AreEqual(0.9f * 0.4f * 0.5f, Audio.SoundBank.Cues["gamer_smash"].Volume, 1e-6f, "half what it was");
            Assert.AreEqual(3.048f / 0.03f * 1.3f, Balance.Objectives.GeneratorHearRadius, 1e-3f, "30% further");
            Assert.AreEqual(Balance.Objectives.GeneratorHearRadius, Audio.SoundBank.Cues["generator_done"].Radius, 1e-3f);
        }

        [Test]
        public void TheHalo_IsSmallerAndHalfAsBright()
        {
            Assert.Less(Vision.Player.Halo.Radius, 0.15f * 0.8f, "smaller");
            Assert.Less(Vision.Player.Halo.Thickness, 0.022f * 0.8f);
            Assert.LessOrEqual(Vision.Player.Halo.Glow, 1.6f * 0.5f, "half as bright");
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
            // The STUN (BOTTLE) testing effect plays it as a real bottle does, for Zach too.
            {
                var t = new SimRig(1, 1, true);
                t.Sim.PlayTestFx(1, TestFx.Stun);
                Assert.IsTrue(t.Sim.Events.Any(e => e.Kind == EventKind.Noise && e.Text == "glass_body" && (e.To == null || e.To.Contains(1))), "Zach hears it");
            }
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
