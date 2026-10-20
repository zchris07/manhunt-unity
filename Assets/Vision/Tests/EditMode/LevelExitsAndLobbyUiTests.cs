using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Net;
using Vision.Player;
using Vision.UI;
using Vision.Visibility;
using Vision.World;

namespace Vision.Tests
{
    /// <summary>
    /// A level built from the lobby's generator slider (two: the building's lone generator lights all of it, and the
    /// graveyard's), with both exits (the second gate's yard beside Miga Sushi), the hatch on the bank by the dock, every
    /// note photo and one by each generator, the extra guns spread out, and wading ripples; the lobby's balance card on
    /// screen (anyone's slider moves show, the host alone resets); a ghost's HUD.
    /// </summary>
    public class LevelExitsAndLobbyUiTests
    {
        GameObject root, hudGo;
        Material mat;
        MatchState saved;

        [SetUp]
        public void SetUp()
        {
            saved = MatchState.Current;
            MatchState.Current = new MatchState();
            root = new GameObject("World");
            mat = new Material(Shader.Find("Vision/LowPoly"));
            hudGo = new GameObject("HUD");
        }

        [TearDown]
        public void TearDown()
        {
            // Out of any lobby (its screen's state is shared), whatever happened.
            if (hudGo != null && hudGo.GetComponent<GameHud>() is GameHud hud && NetSession.Active != null) hud.LeaveOnline();
            NetSession.Active?.Leave();
            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(mat);
            var fx = Object.FindAnyObjectByType<Vision.Effects.Vfx>();
            if (fx != null) Object.DestroyImmediate(fx.gameObject);
            MatchState.Current = saved;
        }

        SandboxWorld NewWorld(int generators = Tuning.DefaultGenerators)
        {
            var world = root.AddComponent<SandboxWorld>();
            world.lowPolyMaterial = world.entityMaterial = world.glowMaterial = mat;
            world.generatorCount = generators;
            world.Generate();
            return world;
        }

        Vector2 Plane(SandboxWorld w, Component c)
        {
            Vector3 l = w.transform.InverseTransformPoint(c.transform.position);
            return new Vector2(l.x, l.z);
        }

        [Test]
        public void ALevelWithTwoGenerators_BothExits_TheHatch_EveryNote_TheExtraGuns_AndWadingRipples()
        {
            SandboxWorld world = NewWorld(2);
            MapLayout L = world.Layout;
            Assert.AreEqual(2, L.GeneratorCount);
            Assert.AreEqual(2, world.Generators.Count, "two generators");
            var gens = world.Generators.Select(g => Plane(world, g)).ToList();
            Assert.AreEqual(1, gens.Count(g => L.Building.Contains(g)), "one in the building");
            Assert.AreEqual(1, gens.Count(g => (g - L.Graveyard.center).magnitude < 0.5f), "and the graveyard's");

            // The building's lone generator lights all of it.
            BuildingPower power = root.GetComponentInChildren<BuildingPower>();
            Assert.AreEqual(1, power.generators.Count);
            power.generators[0].SetState(1f, true);
            power.Refresh();
            Assert.IsTrue(power.lights.All(l => l.enabled), "one generator in the building lights the whole building");
            power.generators[0].SetState(0f, false);
            power.Refresh();

            // The second exit: its own yard beside Miga Sushi, a roll-up gate facing in, the lever outside.
            Assert.IsTrue(L.HasSushiGate);
            Assert.NotNull(world.Gate2, "the second gate");
            Assert.Less(Vector2.Distance(L.SushiGate.center, L.Sushi.center), 22f, "beside Miga Sushi");
            Assert.IsFalse(L.SushiGate.Overlaps(L.Sushi), "a structure of its own");
            Assert.Less(L.SushiGate.center.magnitude, L.HalfExtent, "inside the playable circle");
            Assert.IsFalse(L.SushiGate.Contains(L.SushiGateLever), "the lever outside the yard");
            Assert.Greater(Vector2.Dot(L.SushiGateOut(), -L.SushiGate.center.normalized), 0f, "its gate faces the middle of the map");
            Assert.IsTrue(world.Gate2.blocker.enabled, "shut to start");

            // The hatch: on dry ground by the dock.
            Assert.NotNull(world.Hatch);
            Assert.Less(L.LakeDepth(L.Hatch), -1f, "on the bank, not in the water");
            Assert.Less(Vector2.Distance(L.Hatch, L.DockStart), 9f, "near the dock");
            Assert.IsFalse(world.Hatch.Open);

            // Every photo lies somewhere, and one note by each generator.
            int pictures = NotePictures.Count;
            Assert.AreEqual(Mathf.Max(pictures, 2), world.Notes.Count);
            CollectionAssert.IsSubsetOf(Enumerable.Range(0, pictures), world.NoteImages);
            foreach (Vector2 g in gens)
                Assert.IsTrue(world.Notes.Any(n => Vector2.Distance(Plane(world, n), g) < Balance.Notes.BesideGenerator + 0.5f), "a note by every generator");

            // Two golden pumps (Miga Sushi's and one out on the map), five P250s spread about the playable circle.
            Assert.AreEqual(2, world.Pickups.Count(p => p.item == ItemType.Shotgun && p.golden));
            var pistols = world.Pickups.Where(p => p.item == ItemType.Pistol).Select(p => Plane(world, p)).ToList();
            Assert.AreEqual(SandboxWorld.MapPistols, pistols.Count, "five P250s");
            foreach (Vector2 p in pistols) Assert.Less(p.magnitude, L.HalfExtent, "inside the first ring");
            float closest = float.MaxValue;
            for (int i = 0; i < pistols.Count; i++)
                for (int j = i + 1; j < pistols.Count; j++) closest = Mathf.Min(closest, Vector2.Distance(pistols[i], pistols[j]));
            Assert.Greater(closest, 35f, "well spread out, about as far apart as they can be");

            // The match: two generators to run, both gates and the hatch known to it.
            MatchHost host = MatchHost.For(world);
            host.Begin();
            MatchSim sim = host.Sim;
            Assert.AreEqual(2, sim.Bal.RequiredGenerators);
            Assert.IsTrue(sim.Map.HasGate2 && sim.Map.HasHatch);
            Assert.IsTrue(L.SushiGate.Contains(sim.Map.ExitZone2.center), "out through the yard");

            // Wading: anyone moving through the lake leaves ripples.
            var fx = root.AddComponent<MatchEffects>();
            fx.world = world;
            SimPlayer me = host.Local;
            me.Pos = L.LakeCentre;
            fx.Draw();
            me.Pos = L.LakeCentre + new Vector2(0.5f, 0f);
            fx.Draw();
            Assert.Greater(fx.RippleCount, 0, "rings on the water round them");
            int n0 = fx.RippleCount;
            me.Pos = L.Spawn;
            fx.Draw();
            me.Pos = L.Spawn + new Vector2(0.5f, 0f);
            fx.Draw();
            Assert.AreEqual(n0, fx.RippleCount, "none on dry land");
        }

        [Test]
        public void TheLobby_ShowsTheBalanceCard_EveryonesSliderMovesShow_AndOnlyTheHostResets()
        {
            SandboxWorld world = NewWorld();
            var hud = hudGo.AddComponent<GameHud>();
            hud.world = world;
            hud.CreateLobby("Host", 0);
            NetSession session = NetSession.Active;
            Assert.NotNull(session, "hosting");
            var labels = new HashSet<string>(Tuning.Knobs.Select(k => k.Label + " Slider"));
            var sliders = hudGo.GetComponentsInChildren<UnityEngine.UI.Slider>(true).Where(s => labels.Contains(s.name)).ToList();
            Assert.AreEqual(Tuning.Knobs.Length, sliders.Count, "a slider for each");
            var texts = hudGo.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(t => t.text).ToList();
            Assert.Contains("BALANCE & DIFFICULTY", texts);
            foreach (Tuning.Knob k in Tuning.Knobs) Assert.Contains(k.Label, texts);
            Assert.IsFalse(hud.TuningResetInteractable, "nothing to reset yet");

            // A slider moved (as a joiner's move arrives at the host): it shows, and the host can reset.
            session.Tune(2, 1.4f);
            Assert.AreEqual(1.4f, hud.TuningShown(2), 1e-4f, "on everyone's screen");
            Assert.IsTrue(hud.TuningResetInteractable, "the host can put them back");
            session.ResetTuning();
            Assert.AreEqual(1f, hud.TuningShown(2), 1e-4f);
            Assert.IsFalse(hud.TuningResetInteractable);
            // The host's lock: on, only the host moves them (the host's own sliders stay live), and back off.
            Assert.IsTrue(hud.TuningLockInteractable, "the host can lock them");
            Assert.AreEqual("Lock sliders", hud.TuningLockText);
            hud.PressTuningLock();
            Assert.IsTrue(session.Lobby.Settings.TuningLocked, "locked");
            hud.Refresh();
            Assert.AreEqual("Unlock sliders", hud.TuningLockText);
            Assert.IsTrue(hud.TuningSlidersInteractable, "the host still moves them");
            Assert.IsTrue(session.CanTune);
            hud.PressTuningLock();
            Assert.IsFalse(session.Lobby.Settings.TuningLocked, "unlocked again");
            hud.LeaveOnline();
        }

        [Test]
        public void TheSettingsControls_ScrollInsideThePanel_NeverRunningOutOfIt()
        {
            SandboxWorld world = NewWorld();
            var hud = hudGo.AddComponent<GameHud>();
            hud.world = world;
            hud.StartTesting(false);
            hud.SetMenu(true);
            foreach (bool hunter in new[] { false, true })
            {
                UnityEngine.UI.ScrollRect list = hud.ControlsList(hunter);
                Assert.NotNull(list, "a scrolling list");
                var box = (RectTransform)list.transform;
                var panel = (RectTransform)box.parent;
                Assert.LessOrEqual(-box.anchoredPosition.y + box.rect.height, panel.rect.height - 31f, "the list ends inside the panel");
                Assert.LessOrEqual(box.anchoredPosition.x + box.rect.width, panel.rect.width, "and inside it across");
                Assert.Greater(list.content.rect.height, list.viewport.rect.height, "more keys than fit: it scrolls");
                Assert.IsTrue(list.vertical && !list.horizontal);
                Assert.NotNull(list.viewport.GetComponent<UnityEngine.UI.RectMask2D>(), "what's scrolled out of the box is hidden");
                foreach (UnityEngine.UI.Text t in list.content.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                {
                    var r = (RectTransform)t.transform;
                    if (r.parent != list.content) continue;
                    Assert.LessOrEqual(r.anchoredPosition.x + r.rect.width, list.content.rect.width + 0.5f, t.text + " within the list's width");
                }
            }
        }

        [Test]
        public void TheLakeDock_IsSolidUnderfoot_SteppingOffDropsIntoTheWaterAtOnce_AndOutOfItSnapsBackUp()
        {
            SandboxWorld world = NewWorld();
            MapLayout L = world.Layout;
            Vector2 a = L.DockStart, b = L.DockEnd, side = new Vector2(-(b - a).normalized.y, (b - a).normalized.x);
            Vector2 mid = Vector2.Lerp(a, b, 0.6f);
            Assert.IsTrue(world.OnDock(mid, out float deck), "on the boards");
            float deckLine = Mathf.Max(world.Terrain.Height(L.DockStart.x, L.DockStart.y), world.Terrain.LakeLevel) + SandboxWorld.DockDeckAbove;
            Assert.AreEqual(deckLine + SandboxWorld.DockBoardTop, deck, 1e-4f, "standing on the tops of the boards, not sunk into them");
            float bed = world.Terrain.Height(mid.x, mid.y);
            Assert.Greater(deck, bed + 0.5f, "the deck stands well above the lake bed");
            Assert.AreEqual(deck, world.GroundHeight(mid), 1e-4f, "the ground a body stands on there is the deck");
            Assert.IsFalse(world.Terrain.InWater(mid.x, mid.y), "and it's dry");
            Vector2 offSide = mid + side * (L.DockHalfWidth + 0.4f);
            Assert.IsFalse(world.OnDock(offSide, out _));
            Assert.AreEqual(world.Terrain.Height(offSide.x, offSide.y), world.GroundHeight(offSide), 1e-4f, "a step off: the water's depth");
            // Up from the bank: a ramp of boards from the ground inland of the dock rising to its deck.
            Vector2 along = (b - a).normalized;
            Vector2 foot = a - along * (SandboxWorld.DockRampLength - 0.05f), halfway = a - along * SandboxWorld.DockRampLength * 0.5f;
            Assert.IsTrue(world.OnDock(foot, out float footDeck), "the ramp's foot, on the bank");
            Assert.AreEqual(world.DockRampFoot() + SandboxWorld.DockBoardTop, footDeck, 0.05f, "at the ground there");
            Assert.IsTrue(world.OnDock(halfway, out float midDeck));
            Assert.AreEqual((footDeck + deck) * 0.5f, midDeck, 0.06f, "rising evenly");
            Assert.IsTrue(world.OnDock(a + along * 0.02f, out float topDeck));
            Assert.AreEqual(deck, topDeck, 1e-3f, "and level with the deck where it meets it");
            Assert.IsFalse(world.OnDock(a - along * (SandboxWorld.DockRampLength + 0.3f), out _), "beyond its foot, the bank");
            Assert.IsTrue(world.Terrain.InWater(offSide.x, offSide.y), "wading");
            Assert.IsFalse(world.OnDock(b + (b - a).normalized * 0.4f, out _), "past its end, the water");

            // The player: in the water beside it, then onto it (snapped straight up), then off it (straight down).
            PlayerController pc = world.Player;
            var onto = typeof(PlayerController).GetMethod("OntoOrOffDock", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            float Y() => world.transform.InverseTransformPoint(pc.transform.position).y;
            void Put(Vector2 at, float y) => pc.transform.position = world.transform.TransformPoint(new Vector3(at.x, y, at.y));
            Put(offSide, world.GroundHeight(offSide));
            onto.Invoke(pc, null);
            Assert.AreEqual(world.GroundHeight(offSide), Y(), 1e-3f, "in the water, on the bed");
            Put(mid, world.GroundHeight(offSide));
            onto.Invoke(pc, null);
            var cc = pc.GetComponent<CharacterController>();
            float skin = cc.skinWidth * pc.transform.lossyScale.y / world.transform.lossyScale.y;
            Assert.AreEqual(deck + skin, Y(), 1e-3f, "climbing out onto the dock: straight up onto the tops of its boards, as on any ground");
            Put(offSide, deck);
            onto.Invoke(pc, null);
            Assert.AreEqual(world.Terrain.Height(offSide.x, offSide.y), Y(), 1e-3f, "stepping off: straight down into the water, no fall");
            Assert.AreEqual("step_wood", Vision.Audio.Footsteps.Surface(world, mid), "boards underfoot");

            // The legs stand on the boards too: the ground the feet reach for on the dock is its deck (a foot swung just
            // past its edge too), never the lake bed under it, which would bend the legs down through the boards.
            float WorldY(Vector2 at, float y) => world.transform.TransformPoint(new Vector3(at.x, y, at.y)).y;
            Vector3 W(Vector2 at) => world.transform.TransformPoint(new Vector3(at.x, 0f, at.y));
            Assert.IsTrue(world.FeetGround(W(mid), out float feet, out Vector3 up));
            Assert.AreEqual(WorldY(mid, deck), feet, 1e-3f, "the feet on the tops of the boards");
            Assert.Greater(up.y, 0.99f, "level");
            Vector2 swung = mid + side * (L.DockHalfWidth + SandboxWorld.DockFeetMargin * 0.6f);
            world.FeetGround(W(swung), out float swungY, out _);
            Assert.AreEqual(WorldY(swung, deck), swungY, 1e-3f, "a foot just past the edge still on the boards");
            world.FeetGround(W(offSide), out float wading, out _);
            Assert.AreEqual(WorldY(offSide, world.Terrain.Height(offSide.x, offSide.y)), wading, 1e-3f, "off it, the lake bed");
        }

        [Test]
        public void Survivors_MarkTheStakesTheyFind_OnTheMap_AndTheHeadBarsHaveNoSegmentMarks()
        {
            SandboxWorld world = NewWorld();
            var hud = hudGo.AddComponent<GameHud>();
            hud.world = world;
            hud.StartTesting(false);
            MatchHost host = MatchHost.For(world);
            if (host.Local.Role == Role.Hunter) host.CycleRole();
            Assert.AreEqual(Role.Survivor, host.Local.Role);
            hud.Refresh();
            hud.SyncMap();
            hud.Map.RefreshIcons();
            // (Testing mode shows the whole map: every stake found.)
            var stakes = world.Stakes.Where(t => t != null && t.gameObject.activeSelf).ToList();
            Assert.Greater(stakes.Count, 0);
            foreach (Transform t in stakes) Assert.IsTrue(hud.Map.Marks(t), "a stake found: marked on the survivor's map");
            Assert.IsFalse(MapHud.StakesAlwaysKnown(Role.Survivor), "a survivor marks each only once it's found");
            Assert.IsTrue(MapHud.StakesAlwaysKnown(Role.Hunter), "Zach knows them all from the start");
            // The bars over heads: no segment marks on health or shield.
            Assert.AreEqual(0, hudGo.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Tick" && t.parent != null && t.parent.name == "Track"));
        }

        [Test]
        public void ZachSoakedInPiss_DripsIt_AndLeavesTinyPuddlesWhereHeWalks()
        {
            SandboxWorld world = NewWorld();
            var hud = hudGo.AddComponent<GameHud>();
            hud.world = world;
            hud.StartTesting(false);
            MatchHost host = MatchHost.For(world);
            if (host.Local.Role != Role.Hunter) host.SwitchRole();
            SimPlayer zach = host.Local;
            Assert.AreEqual(Role.Hunter, zach.Role);
            var fxGo = new GameObject("Effects");
            var effects = fxGo.AddComponent<MatchEffects>();
            effects.world = world;
            try
            {
                effects.Draw();
                Assert.AreEqual(0, effects.PissPuddles, "dry: nothing left behind");
                host.Sim.SoakInPiss(zach);
                Vector2 start = zach.Pos;
                for (int i = 0; i < 8; i++)
                {
                    host.Sim.Teleport(zach.Id, start + new Vector2(0.6f * i, 0f));
                    effects.Draw();
                }
                Assert.GreaterOrEqual(effects.PissPuddles, 6, "a tiny puddle every step or so");
                Mesh mesh = effects.PissPuddleMesh;
                Assert.NotNull(mesh);
                Assert.Greater(mesh.triangles.Length / 3, 6 * 5, "a blot for each");
                Assert.Less(mesh.bounds.size.z, 0.6f, "tiny ones (strung out along his way, but no wider than a few centimetres across it)");
                // Worn off: no more.
                zach.PissStacks = new float[0];
                int left = effects.PissPuddles;
                host.Sim.Teleport(zach.Id, start + new Vector2(0f, 3f));
                effects.Draw();
                Assert.AreEqual(left, effects.PissPuddles, "dry again: no more puddles");
            }
            finally { Object.DestroyImmediate(fxGo); }
        }

        [Test]
        public void TheCabins_HaveFloorsOfBoardsOrStone_AndAreFurnished_WithoutCrowdingTheDoorway()
        {
            SandboxWorld world = NewWorld();
            MapLayout L = world.Layout;
            Assert.AreEqual(L.Cabins.Count, world.CabinStoneFloors.Count, "a floor for every cabin");
            var all = root.GetComponentsInChildren<Transform>(true);
            int floors = all.Count(t => t.name == "Stone Floor" || t.name == "Board Floor");
            Assert.AreEqual(L.Cabins.Count, floors);
            string[] fittings = { "Stove", "Shelf", "Crate", "Barrel", "Rug", "Chair", "Papers", "Bottles", "Debris", "Blood", "Wall Stain", "Wallpaper", "Poster", "Mirror", "Clock", "Cobweb" };
            for (int i = 0; i < L.Cabins.Count; i++)
            {
                MapLayout.Cabin c = L.Cabins[i];
                var inside = all.Where(t => fittings.Contains(t.name) && c.Area.Contains(Plane(world, t))).ToList();
                var kinds = inside.Select(t => t.name).Distinct().ToList();
                Debug.Log($"[Ruins] cabin {i} ({(world.CabinStoneFloors[i] ? "stone" : "boards")}): {string.Join(", ", inside.GroupBy(t => t.name).Select(g => g.Key + (g.Count() > 1 ? " x" + g.Count() : "")))}");
                Assert.GreaterOrEqual(kinds.Count, 5, "furnished, and with signs of what happened");
                Assert.LessOrEqual(inside.Count(t => t.name != "Wallpaper" && t.name != "Wall Stain"), 16, "but not too cluttered");
                Assert.AreEqual(world.CabinStoneFloors[i] ? "step_concrete" : "step_wood", Vision.Audio.Footsteps.Surface(world, c.Area.center), "footsteps on its floor");
                // The way in from the door kept clear of anything solid.
                Vector2 inward = c.DoorSide switch { 0 => Vector2.down, 1 => Vector2.left, 2 => Vector2.up, _ => Vector2.right };
                Vector2 door = c.Area.center - inward * (c.DoorSide % 2 == 0 ? c.Area.height : c.Area.width) * 0.5f;
                foreach (BoxCollider box in root.GetComponentsInChildren<BoxCollider>(true).Where(b => fittings.Contains(b.name)))
                    Assert.Greater((Plane(world, box) - (door + inward * 0.8f)).magnitude, 0.8f, $"{box.name} clear of the doorway");
            }
        }

        [Test]
        public void BrickRuins_StandAboutTheMap_Solid_BlockingSight_StruckAsBrick_AndTheWorkSitesGrowNothing()
        {
            SandboxWorld world = NewWorld();
            MapLayout L = world.Layout;
            Assert.AreEqual(SandboxWorld.RuinCount, world.Ruins.Count, "a dozen ruins");
            Assert.GreaterOrEqual(world.MapRuins.Count, SandboxWorld.RuinCount, "each wall on the map");
            Debug.Log("[Ruins] placed: " + string.Join(", ", world.Ruins.Select(r => r.kind)) + "; " + world.RuinReport);
            Assert.IsTrue(world.Ruins.Any(r => r.plan.Structure), "the shell of a building among them");
            Assert.IsTrue(world.Ruins.Any(r => !r.plan.Structure), "and broken walls and corners");
            // The three every map has, each with its stone floor: laid on level ground (nothing poking through), nothing
            // growing on it, footsteps on stone.
            foreach (RuinArchetype kind in SandboxWorld.RequiredRuins)
            {
                var r = world.Ruins.First(x => x.kind == kind);
                MeshFilter floor = r.root.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name == "Stone Floor Ruin");
                Assert.NotNull(floor, $"{kind}: its stone floor");
                Assert.Greater(floor.sharedMesh.triangles.Length / 3, 300);
                Assert.AreSame(world.StoneMaterial, floor.GetComponent<MeshRenderer>().sharedMaterial, "the stone material");
                float top = world.Terrain.Height(r.at.x, r.at.y) + RuinFloors.StoneTop;
                foreach (FloorZone z in r.plan.Floor)
                {
                    Rect a = RuinFloors.Inset(z.Area);
                    for (float u = 0.1f; u < 1f; u += 0.2f)
                        for (float v = 0.1f; v < 1f; v += 0.2f)
                        {
                            Vector2 p = r.at + RotatePlan(new Vector2(Mathf.Lerp(a.xMin, a.xMax, u), Mathf.Lerp(a.yMin, a.yMax, v)), r.yaw);
                            Assert.IsTrue(world.OnRuinFloor(p), $"{kind}: on its floor");
                            Assert.Less(world.SurfaceHeight(p), top - 0.006f, $"{kind}: the ground stays under the floor");
                            Assert.AreEqual("step_concrete", Vision.Audio.Footsteps.Surface(world, p), "footsteps on stone");
                        }
                }
            }
            Assert.IsFalse(world.OnRuinFloor(L.Spawn), "nowhere else");
            var chunks = new List<Transform>();
            foreach (var r in world.Ruins)
            {
                var mine = r.root.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name == "Brick Wall Ruin").ToList();
                Assert.Greater(mine.Count, 0, "its brickwork");
                chunks.AddRange(mine.Select(m => m.transform));
                foreach (MeshFilter m in mine)
                {
                    Assert.LessOrEqual(m.sharedMesh.triangles.Length / 3, 12000, "each piece within its cap");
                    Assert.Greater(m.sharedMesh.bounds.size.y, 0.7f, "still standing in part");
                }
                var cols = r.root.GetComponentsInChildren<BoxCollider>(true);
                Assert.Greater(cols.Length, 0, "solid");
                Assert.Greater(r.root.GetComponentsInChildren<Vision.Visibility.Occluder>(true).Length, 0, "it blocks sight");
                foreach (BoxCollider c in cols)
                {
                    Assert.AreEqual(Vision.UI.MatchEffects.Surface.Brick, Vision.UI.MatchEffects.SurfaceOf(c), "the machete strikes brick");
                    Assert.Greater(Vector3.Dot(c.transform.up, world.transform.up), 0.999f, "upright walls only: no ramps");
                }
                // One flat base for the whole ruin.
                Assert.IsTrue(mine.All(m => Mathf.Abs(m.transform.position.y - r.root.transform.position.y) < 1e-4f));
                foreach (RuinRun run in r.plan.Runs)
                    for (float k = 0f; k <= 1.001f; k += 0.25f)
                    {
                        Vector2 p = r.at + RotatePlan(Vector2.Lerp(run.A, run.B, k), r.yaw);
                        Assert.Less(p.magnitude, L.HalfExtent, "on the playable map");
                        Assert.Less(L.LakeDepth(p), -1f, "not in the lake");
                        Assert.Greater(world.Terrain.PathClearance(p.x, p.y), 0.5f, "off the paths");
                        Assert.IsFalse(world.OnWorkSite(p), "not on a generator's work site");
                    }
            }
            // Every wall on the map is a wall of brick there.
            float s = world.transform.lossyScale.x;
            foreach (var (a, b) in world.MapRuins)
            {
                Vector2 mid = (a + b) * 0.5f;
                Vector3 w = world.transform.TransformPoint(new Vector3(mid.x, 0f, mid.y));
                Assert.IsTrue(chunks.Any(c =>
                {
                    Bounds wb = c.GetComponent<MeshRenderer>().bounds;
                    w.y = wb.center.y;
                    return wb.SqrDistance(w) < 0.25f * s * s;
                }), "a wall's brickwork where the map shows it");
            }
            // Ruins apart from one another.
            for (int i = 0; i < world.Ruins.Count; i++)
                for (int j = i + 1; j < world.Ruins.Count; j++)
                    foreach (RuinRun ra in world.Ruins[i].plan.Runs)
                        foreach (RuinRun rb in world.Ruins[j].plan.Runs)
                        {
                            Vector2 a0 = world.Ruins[i].at + RotatePlan(ra.A, world.Ruins[i].yaw), a1 = world.Ruins[i].at + RotatePlan(ra.B, world.Ruins[i].yaw);
                            Vector2 b0 = world.Ruins[j].at + RotatePlan(rb.A, world.Ruins[j].yaw), b1 = world.Ruins[j].at + RotatePlan(rb.B, world.Ruins[j].yaw);
                            for (float k = 0f; k <= 1.001f; k += 0.1f)
                                Assert.Greater(SimMap.SegmentDistance(Vector2.Lerp(a0, a1, k), b0, b1), 1f, "ruins don't run into each other");
                        }

            // The same seed, the same ruins (every player's level alike).
            var first = world.MapRuins.ToList();
            world.Generate();
            CollectionAssert.AreEqual(first, world.MapRuins);

            // Outdoor generators' work sites: nothing grows there.
            int sites = 0;
            foreach (GeneratorObjective g in GeneratorObjective.All.Where(g => g.transform.IsChildOf(root.transform)))
            {
                Vector2 at = Plane(world, g);
                bool grave = L.Graveyard.width > 0f && (at - L.Graveyard.center).magnitude < 0.5f;
                if (L.Building.Contains(at) || grave) continue;
                sites++;
                Assert.IsTrue(world.OnWorkSite(at) && world.OnWorkSite(at + new Vector2(SandboxWorld.WorkSiteClear - 0.5f, 0f)), "its ground kept bare");
                Assert.IsFalse(world.OnWorkSite(at + new Vector2(SandboxWorld.WorkSiteClear + 0.5f, 0f)));
            }
            Assert.Greater(sites, 0);
        }

        static Vector2 RotatePlan(Vector2 p, float yaw) => new Vector2(p.x * Mathf.Cos(yaw) - p.y * Mathf.Sin(yaw), p.x * Mathf.Sin(yaw) + p.y * Mathf.Cos(yaw));

        [Test]
        public void AGhost_SeesItsPanel_InPlaceOfTheInventory_ThroughFindingABodyToBeingKnockedOutOfIt()
        {
            SandboxWorld world = NewWorld();
            var hud = hudGo.AddComponent<GameHud>();
            hud.world = world;
            hud.StartTesting(false);
            MatchHost host = MatchHost.For(world);
            // A ghost comes back in Miga Sushi or the Diddy Chamber.
            Assert.AreEqual(2, host.Sim.Map.GhostSpawns.Count, "two places for a ghost to come back");
            foreach (Vector2 at in host.Sim.Map.GhostSpawns)
                Assert.IsTrue(world.Places.Any(pl => pl.area.Contains(at)), "inside Miga Sushi or the Diddy Chamber");
            hud.Refresh();
            Assert.IsFalse(hud.GhostPanelShowing);
            SimPlayer me = host.Local;
            host.Sim.Eliminate(me, "stake");
            host.Sim.MakeGhost(me);
            hud.Refresh();
            Assert.IsTrue(hud.GhostPanelShowing, "the ghost's panel");
            StringAssert.Contains("find a body, help your team", hud.GhostPanelText);
            StringAssert.Contains("Find a body", GameHud.ObjectiveText(host.Sim, me));

            // Taking their own body: it rises, then they're in it.
            Body body = host.Sim.Bodies.First(b => b.Of == me.Id);
            host.Sim.Teleport(me.Id, body.Pos + new Vector2(1f, 0f));
            Assert.IsTrue(host.Sim.Possess(me, body));
            hud.Refresh();
            StringAssert.Contains("rises", hud.GhostPanelText);
            for (int i = 0; i < SimRig.Secs(Balance.Ghost.PossessTime + 0.1f); i++) host.Sim.Step();
            hud.Refresh();
            Assert.IsTrue(MatchSim.InBody(me));
            StringAssert.Contains("you cannot use items or fight, but you can help your team repair generators", hud.GhostPanelText);
            hud.Banner($"{body.Name} has been reanimated");
            Assert.AreEqual($"{body.Name} has been reanimated", hud.BannerText, "the banner across the screen");

            // Knocked out of it: they see everything now.
            host.Sim.KnockOut(me, null, "slash");
            hud.Refresh();
            StringAssert.Contains("can't take another", hud.GhostPanelText);
            Assert.IsTrue(PlayerController.Seer(me));
        }
    }
}
