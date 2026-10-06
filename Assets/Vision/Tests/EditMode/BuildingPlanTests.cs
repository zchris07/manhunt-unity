using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.World;
using Plan = Vision.World.BuildingPlan;

namespace Vision.Tests
{
    public class BuildingPlanTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 7, 42, 99, 1337, 2024, 31337, 8 };
        static readonly Rect Footprint = new Rect(-18f, -18f, 36f, 36f);

        static IEnumerable<Plan> Plans() => Seeds.Select(s => new Plan(s, Footprint));

        [Test]
        public void Spaces_TileTheFootprint_OnTheGrid()
        {
            foreach (Plan p in Plans())
            {
                float area = p.Rooms.Sum(r => r.FloorArea);
                Assert.AreEqual(p.FootprintArea, area, 0.5f, $"seed {p.Seed}: rooms and hallways cover the building exactly");
                for (int i = 0; i < p.Rooms.Count; i++)
                    for (int j = i + 1; j < p.Rooms.Count; j++)
                    {
                        Rect a = p.Rooms[i].Area, b = p.Rooms[j].Area;
                        float ox = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), oy = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                        Assert.IsFalse(ox > 0.01f && oy > 0.01f, $"seed {p.Seed}: spaces {i} and {j} overlap");
                    }
                float cell = p.Cell;
                Assert.AreEqual(36f / Plan.Cells, cell, 1e-4f);
                foreach (Plan.Room r in p.Rooms)
                {
                    float w = r.Area.width / cell, d = r.Area.height / cell;
                    Assert.AreEqual(Mathf.Round(w), w, 1e-3f, "whole cells");
                    Assert.AreEqual(Mathf.Round(d), d, 1e-3f, "whole cells");
                    if (r.IsHallway) Assert.AreEqual(1f, Mathf.Min(w, d), 1e-3f, $"seed {p.Seed}: a hallway is a corridor one cell wide");
                    else
                    {
                        Assert.GreaterOrEqual(Mathf.Min(w, d), 2f - 1e-3f, $"seed {p.Seed}: a {r.Name} is at least two cells across");
                        Assert.LessOrEqual(Mathf.Max(w, d), Plan.MaxLeaf + 1e-3f, $"seed {p.Seed}: a {r.Name} fits a BSP leaf");
                    }
                }
                Assert.That(p.Rooms.Count(r => !r.IsHallway), Is.InRange(6, 26), $"seed {p.Seed}: rooms");
            }
        }

        [Test]
        public void Corridors_FormAMaze()
        {
            foreach (Plan p in Plans())
            {
                var halls = p.Rooms.Where(r => r.IsHallway).ToList();
                float share = halls.Sum(r => r.FloorArea) / p.FootprintArea;
                Assert.That(share, Is.InRange(0.35f, 0.8f), $"seed {p.Seed}: most of the floor is maze corridor");
                Assert.Greater(halls.Count, p.Rooms.Count(r => !r.IsHallway), $"seed {p.Seed}: more hallways than rooms");
                // Dead ends: a hallway with a single way in or out.
                int deadEnds = halls.Count(h => p.InterfacesOf(h.Id).Count(k => p.Interfaces[k].Open || p.Openings.Any(o => o.Interface == k && o.IsPassage)) == 1);
                Assert.Greater(deadEnds, 2, $"seed {p.Seed}: a maze has dead ends");
                Assert.Greater(p.Openings.Count(o => o.Kind == Plan.OpeningKind.Gap), 10, $"seed {p.Seed}: corridors open into each other");
                Assert.Greater(halls.Max(h => Mathf.Max(h.Area.width, h.Area.height)), 4f * p.Cell, $"seed {p.Seed}: some long corridors");
            }
        }

        [Test]
        public void EveryRoom_IsReachable_WithLoops()
        {
            foreach (Plan p in Plans())
            {
                bool[] reach = p.Reachable();
                for (int i = 0; i < reach.Length; i++) Assert.IsTrue(reach[i], $"seed {p.Seed}: {p.Rooms[i].Name} {i} can be walked to");
                int passages = p.Openings.Count(o => o.IsPassage && !o.Exterior) + p.Interfaces.Count(f => f.Open);
                Assert.Greater(passages, p.Rooms.Count - 1, $"seed {p.Seed}: more links than a tree, so there are loops");
            }
        }

        [Test]
        public void Openings_SitInWalls()
        {
            foreach (Plan p in Plans())
                foreach (Plan.Opening o in p.Openings)
                {
                    Plan.Interface f = p.Interfaces[o.Interface];
                    Assert.IsFalse(f.Open, "no openings in walls that aren't there");
                    foreach (Vector2 end in new[] { o.A, o.B })
                    {
                        float t = Vector2.Dot(end - f.P0, f.Along);
                        Assert.That(t, Is.InRange(-1e-3f, f.Length + 1e-3f), $"seed {p.Seed}: a {o.Kind} within its wall");
                        Assert.Less((f.P0 + f.Along * t - end).magnitude, 1e-3f, "on the wall line");
                    }
                }
        }

        [Test]
        public void Outside_HasTheOriginalsEntrances_WindowsAndTheNorthGate()
        {
            foreach (Plan p in Plans())
            {
                int Count(char side) => p.Entrances.Count(e => e.side == side);
                Assert.AreEqual(2, Count('s'), $"seed {p.Seed}: two doors south");
                Assert.AreEqual(1, Count('n'), $"seed {p.Seed}: one door north");
                Assert.That(Count('e'), Is.InRange(1, 2));
                Assert.That(Count('w'), Is.InRange(1, 2));
                Assert.Greater(p.Openings.Count(o => o.Kind == Plan.OpeningKind.Window || o.Kind == Plan.OpeningKind.BoardedWindow), 6, "windows");
                Plan.Opening gate = p.Openings.Single(o => o.Kind == Plan.OpeningKind.Gate);
                Assert.AreEqual(Footprint.yMax, gate.A.y, 1e-3f, "the gate is in the north wall");
                Assert.That(p.GateX, Is.InRange(Footprint.xMin + 6f, Footprint.xMax - 6f), "with room for the yard");
                Assert.AreEqual(Plan.RoomType.LoadingBay, p.Rooms[p.GateRoom].Type, "it opens from the loading bay");
                Assert.IsTrue(p.Rooms[p.GateRoom].Area.Contains(p.Lever), "the lever is inside, by the gate");
                Assert.LessOrEqual(p.Barricades.Count, Plan.MaxBarricades);
            }
        }

        [Test]
        public void TwoGenerators_InRooms_TakingAtMostThirtyPercent()
        {
            foreach (Plan p in Plans())
            {
                var gens = p.Generators.ToList();
                Assert.AreEqual(2, gens.Count, $"seed {p.Seed}: two generators inside");
                Assert.AreNotEqual(gens[0].Room, gens[1].Room, "in different rooms");
                foreach (Plan.Item g in gens)
                {
                    Plan.Room room = p.Rooms[g.Room];
                    Assert.IsFalse(room.IsHallway, "never in a hallway");
                    Assert.IsTrue(room.HasGenerator);
                    float share = Plan.GeneratorFootprint.x * Plan.GeneratorFootprint.y / room.FloorArea;
                    Assert.LessOrEqual(share, Plan.GeneratorShare + 1e-4f, $"seed {p.Seed}: generator and its working space take {share:P0} of the {room.Name}");
                    Rect fp = g.Footprint;
                    Assert.GreaterOrEqual(fp.xMin - room.Area.xMin, 0.95f, "a walkway all round");
                    Assert.GreaterOrEqual(room.Area.xMax - fp.xMax, 0.95f);
                    Assert.GreaterOrEqual(fp.yMin - room.Area.yMin, 0.95f);
                    Assert.GreaterOrEqual(room.Area.yMax - fp.yMax, 0.95f);
                    Rect space = new Rect(fp.x - 0.6f, fp.y - 0.6f, fp.width + 1.2f, fp.height + 1.2f);
                    foreach (Plan.Item other in p.Items.Where(i => i.Solid && i.Room == g.Room && i.Kind != Plan.Furn.Generator))
                        Assert.IsFalse(other.Footprint.Overlaps(space), $"seed {p.Seed}: a {other.Kind} crowds the generator");
                }
                Assert.Greater((gens[0].Position - gens[1].Position).magnitude, 6f, "spread apart");
            }
        }

        [Test]
        public void Furniture_LeavesDoorwaysAndRoomToMove()
        {
            foreach (Plan p in Plans())
            {
                foreach (Plan.Room room in p.Rooms)
                {
                    var solids = p.Items.Where(i => i.Solid && i.Room == room.Id).ToList();
                    foreach (Plan.Item i in solids)
                    {
                        Rect r = i.Footprint;
                        Assert.IsTrue(r.xMin >= room.Area.xMin && r.xMax <= room.Area.xMax && r.yMin >= room.Area.yMin && r.yMax <= room.Area.yMax,
                                      $"seed {p.Seed}: a {i.Kind} inside its {room.Name}");
                    }
                    for (int a = 0; a < solids.Count; a++)
                        for (int b = a + 1; b < solids.Count; b++)
                            Assert.IsFalse(solids[a].Footprint.Overlaps(solids[b].Footprint), $"seed {p.Seed}: {solids[a].Kind} and {solids[b].Kind} overlap");
                    float covered = solids.Sum(i => i.Footprint.width * i.Footprint.height);
                    Assert.LessOrEqual(covered, room.FloorArea * 0.5f, $"seed {p.Seed}: the {room.Name} keeps half its floor clear");
                }
                // Nothing solid in front of a door.
                foreach (Plan.Opening o in p.Openings.Where(o => o.IsPassage))
                {
                    Plan.Interface f = p.Interfaces[o.Interface];
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector2 inFront = o.Centre + f.Normal * side * 0.6f;
                        foreach (Plan.Item i in p.Items.Where(i => i.Solid))
                            Assert.IsFalse(i.Footprint.Contains(inFront), $"seed {p.Seed}: a {i.Kind} blocks a {o.Kind}");
                    }
                }
            }
        }

        [Test]
        public void Rooms_AreVaried_AndFurnished()
        {
            var types = new HashSet<Plan.RoomType>();
            foreach (Plan p in Plans())
            {
                foreach (Plan.Room r in p.Rooms) types.Add(r.Type);
                Assert.Greater(p.Rooms.Select(r => r.Type).Distinct().Count(), 5, $"seed {p.Seed}: many kinds of room");
                Assert.Greater(p.Items.Count, 150, "full of things");
                Assert.That(p.Items.Count(i => i.Hide && i.Kind == Plan.Furn.Locker && p.Rooms[i.Room].IsHallway), Is.InRange(1, Plan.MaxHallLockers), "lockers in the hallways");
                foreach (Plan.Furn k in new[] { Plan.Furn.Pipes, Plan.Furn.Poster, Plan.Furn.Stain, Plan.Furn.Cobweb })
                    Assert.IsTrue(p.Items.Any(i => i.Kind == k), $"seed {p.Seed}: has {k}");
            }
            foreach (Plan.RoomType t in System.Enum.GetValues(typeof(Plan.RoomType)))
                Assert.IsTrue(types.Contains(t), $"some building has a {t}");
        }

        [Test]
        public void Lamps_AreSparse_SomeDeadSomeFlickering()
        {
            foreach (Plan p in Plans())
            {
                int working = p.Lamps.Count(l => l.Working && l.Kind != Plan.LampKind.Exit);
                Assert.That(working, Is.InRange(6, 16), $"seed {p.Seed}: enough light to find your way, mostly dark");
                foreach (Plan.Lamp l in p.Lamps) Assert.IsTrue(p.Rooms[l.Room].Area.Contains(l.Position), $"seed {p.Seed}: a {l.Kind} lamp inside its room");
                float litArea = p.Rooms.Where(r => p.Lamps.Any(l => l.Room == r.Id && l.Working && l.Kind != Plan.LampKind.Exit)).Sum(r => r.FloorArea);
                Assert.That(litArea / p.FootprintArea, Is.InRange(0.1f, 0.6f), $"seed {p.Seed}: only a fraction of the floor has a working light");
                foreach (Plan.Lamp l in p.Lamps)
                {
                    Plan.Room r = p.Rooms[l.Room];
                    if (l.Kind == Plan.LampKind.Desk) Assert.IsTrue(p.Items.Any(i => i.Kind == Plan.Furn.Desk && i.Room == l.Room && i.Footprint.Contains(l.Position)), "a desk lamp stands on a desk");
                    if (l.Kind == Plan.LampKind.Vending) Assert.IsTrue(p.Items.Any(i => i.Kind == Plan.Furn.Vending && i.Room == l.Room), "a vending glow needs a machine");
                    if (l.Kind == Plan.LampKind.Furnace) Assert.AreEqual(Plan.RoomType.Boiler, r.Type);
                    if (l.Kind == Plan.LampKind.Server) Assert.AreEqual(Plan.RoomType.ServerRoom, r.Type);
                    if (l.Kind == Plan.LampKind.Stage) Assert.AreEqual(Plan.RoomType.StudioSet, r.Type);
                }
            }
            Assert.Greater(Plans().Sum(p => p.Lamps.Count(l => !l.Working)), 5, "dead fixtures too");
            var kinds = Plans().SelectMany(p => p.Lamps.Select(l => l.Kind)).Distinct().ToList();
            Assert.GreaterOrEqual(kinds.Count, 6, "lights that fit their rooms: " + string.Join(", ", kinds));
            Assert.IsTrue(Plans().Any(p => p.Lamps.Any(l => l.Working && l.Flicker > 0.1f)), "some flicker");
            Assert.AreEqual(9f, Plan.LampRange, "the original's 300-unit lamps");
        }

        [Test]
        public void Footprint_IsNotAPerfectRectangle_YetStillAMaze()
        {
            var shapes = new HashSet<int>();
            foreach (Plan p in Plans())
            {
                shapes.Add(p.Notches.Count);
                Assert.GreaterOrEqual(p.Notches.Count, 1, $"seed {p.Seed}: a corner is cut out of the square");
                foreach (Rect n in p.Notches)
                {
                    Assert.IsFalse(p.Rooms.Any(r => r.Area.Overlaps(new Rect(n.x + 0.05f, n.y + 0.05f, n.width - 0.1f, n.height - 0.1f))), $"seed {p.Seed}: no room inside a notch");
                    Assert.Less(p.RoomAt(n.center), 0, "the notch is outside");
                }
                // Every wall to the outside has the outside beyond it, whichever way the notch turns.
                foreach (Plan.Interface f in p.Interfaces.Where(f => f.Exterior))
                {
                    Vector2 mid = (f.P0 + f.P1) * 0.5f;
                    Assert.Less(p.RoomAt(mid + f.Normal * 0.3f), 0, $"seed {p.Seed}: outside beyond an exterior wall");
                    Assert.AreEqual(f.A, p.RoomAt(mid - f.Normal * 0.3f), "and the room inside");
                }
            }
            Assert.GreaterOrEqual(shapes.Count, 2, "L shapes and ones with two cut corners");
        }

        [Test]
        public void Plan_IsDeterministicPerSeed()
        {
            Plan a = new Plan(5, Footprint), b = new Plan(5, Footprint), c = new Plan(6, Footprint);
            Assert.AreEqual(a.Rooms.Count, b.Rooms.Count);
            Assert.AreEqual(a.Items.Count, b.Items.Count);
            Assert.AreEqual(a.GateX, b.GateX);
            for (int i = 0; i < a.Rooms.Count; i++) Assert.AreEqual(a.Rooms[i].Area, b.Rooms[i].Area);
            Assert.IsFalse(a.Rooms.Count == c.Rooms.Count && a.GateX == c.GateX, "another seed, another building");
        }
    }
}
