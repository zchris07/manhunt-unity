using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Player;
using Vision.World;

namespace Vision.Tests
{
    public class MenuAndMapTests
    {
        GameObject root, hudGo;
        Material mat;

        [SetUp]
        public void SetUp()
        {
            GameSession.Reset();
            root = new GameObject("World");
            mat = new Material(Shader.Find("Vision/LowPoly"));
            hudGo = new GameObject("HUD");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(mat);
            GameSession.Reset();
        }

        SandboxWorld NewWorld()
        {
            var world = root.AddComponent<SandboxWorld>();
            world.lowPolyMaterial = world.entityMaterial = world.glowMaterial = mat;
            world.Generate();
            return world;
        }

        [Test]
        public void Fog_StartsBlack_AndOpensWhereYouHaveSeen()
        {
            var fog = new FogOfWar(90f);
            Assert.AreEqual(0, fog.SeenCount, "the map starts black");
            var cone = new[] { new Vector2(0f, 0f), new Vector2(-6f, 20f), new Vector2(6f, 20f) };
            fog.Reveal(cone);
            Assert.IsTrue(fog.Seen(new Vector2(0f, 10f)), "inside the beam");
            Assert.IsFalse(fog.Seen(new Vector2(0f, -5f)), "behind you");
            Assert.IsFalse(fog.Seen(new Vector2(10f, 10f)), "beside the beam");
            int n = fog.SeenCount;
            Assert.That(n * fog.Cell * fog.Cell, Is.InRange(100f, 140f), "about the triangle's 120 m²");
            fog.Reveal(cone);
            Assert.AreEqual(n, fog.SeenCount, "seen stays seen");
            fog.RevealAll();
            Assert.AreEqual(fog.Size * fog.Size, fog.SeenCount);
            Assert.AreEqual(0, fog.Texture.GetPixel(100, 100).a, 1e-3f, "revealed cells are clear");
        }

        [Test]
        public void Map_IsPaintedFromTheLevel()
        {
            SandboxWorld world = NewWorld();
            Texture2D map = MapPainter.Paint(world, 512);
            try
            {
                Assert.AreEqual(512, map.width);
                float mpp = MapPainter.MetresPerPixel(world, 512);
                Color At(Vector2 p) => map.GetPixel(Mathf.FloorToInt((p.x + world.halfExtent) / mpp), Mathf.FloorToInt((p.y + world.halfExtent) / mpp));
                // An exterior wall is drawn dark; the lake is water.
                BuildingPlan.WallRun outer = world.Layout.Plan.Walls.First(w => w.Exterior && Vector2.Distance(w.A, w.B) > 3f);
                Color wall = At((outer.A + outer.B) * 0.5f);
                Assert.Less(wall.r + wall.g + wall.b, 0.25f, "walls are dark lines");
                Color lake = At(world.Layout.LakeCentre);
                Assert.Less(lake.r, lake.b + 0.02f, "the lake is blue-dark water");
                Color hallway = At(world.Layout.Plan.Rooms.First(r => r.IsHallway).Area.center);
                Assert.Greater(hallway.r + hallway.g + hallway.b, 0.6f, "the hallways are light");
            }
            finally { Object.DestroyImmediate(map); }
        }

        [Test]
        public void NewMap_ChangesTheSeed_AndRebuildsTheLevel()
        {
            SandboxWorld world = NewWorld();
            int seed = world.seed;
            Vector2 lake = world.Layout.LakeCentre;
            int built = 0;
            void Count(SandboxWorld w) { if (w == world) built++; }
            SandboxWorld.Built += Count;
            try
            {
                world.Regenerate(seed + 1);
                Assert.AreEqual(seed + 1, world.seed);
                Assert.AreEqual(1, built, "the HUD hears about it");
                Assert.AreNotEqual(lake, world.Layout.LakeCentre, "another seed, another map");
                Assert.AreEqual(1, world.GetComponentsInChildren<PlayerController>().Length, "one player, not two");
                Assert.AreEqual(5, world.Generators.Count);
                world.Regenerate();
                Assert.AreNotEqual(seed + 1, world.seed, "a random seed");
            }
            finally { SandboxWorld.Built -= Count; }
        }

        [Test]
        public void Menus_TitleScreen_GameMenuWithoutPause_AndTestingMode()
        {
            SandboxWorld world = NewWorld();
            var hud = hudGo.AddComponent<GameHud>();
            hud.world = world;
            hud.Refresh();
            var texts = hudGo.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(t => t.text).ToList();
            foreach (string label in new[] { "MANHUNT", GameHud.Kicker.ToUpperInvariant(), "Testing mode", "Quit", "Resume", "New map", "Quit to main menu", "Look settings (F4)" })
                Assert.Contains(label, texts, $"has \"{label}\"");
            Assert.IsTrue(texts.Any(t => t.StartsWith("Speed mode")), "a speed mode toggle");
            Assert.IsTrue(hudGo.GetComponentsInChildren<UnityEngine.UI.Slider>(true).Any(sl => sl.minValue <= Scale.HumanPace && sl.maxValue >= 1f), "a pace slider from this game's walk to the original's speeds");

            hud.ShowMainMenu();
            Assert.IsTrue(GameHud.MainMenuOpen);
            Assert.IsTrue(GameHud.MenuOpen, "the player waits on the title screen");
            hud.SetMenu(true);
            Assert.IsTrue(GameHud.MainMenuOpen, "no game menu over the title screen");

            hud.StartTesting(false);
            Assert.IsFalse(GameHud.MainMenuOpen);
            Assert.IsTrue(GameSession.TestingMode);
            SimPlayer me = world.Player.Me;
            Assert.NotNull(me, "the match restarted under testing rules");
            Assert.IsTrue(me.Inv.Infinite, "the testing kit");
            Assert.AreEqual(9, me.Inv.Count(ItemType.Bottle));
            Assert.IsTrue(MatchHost.For(world).Sim.TestMode);

            hud.SetMenu(true);
            Assert.IsTrue(GameHud.MenuOpen);
            Assert.AreEqual(1f, Time.timeScale, "the game menu doesn't pause");
            hud.ToggleSpeedMode();
            Assert.IsTrue(GameSession.SpeedMode);
            hud.Refresh();
            Assert.IsTrue(hudGo.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t => t.text == $"Seed {world.seed}"), "the menu shows the seed");
            hud.SetMenu(false);
            Assert.IsFalse(GameHud.MenuOpen);
            hud.ShowMainMenu();
            Assert.IsFalse(GameSession.TestingMode, "back at the title");
            Assert.IsFalse(GameSession.SpeedMode);
        }

        [Test]
        public void Maps_BindToALevelBuiltBeforeTheHud_AndToEveryNewMap()
        {
            // The level generates in Awake, before the HUD can hear about it: the maps must still be painted.
            SandboxWorld world = NewWorld();
            var hud = hudGo.AddComponent<GameHud>();
            hud.world = world;
            hud.SyncMap();
            Assert.AreSame(world.Layout, hud.Map.BoundLayout, "the first level is on the map");
            Assert.NotNull(hud.Map.Fog);
            world.Regenerate(world.seed + 3);
            hud.SyncMap();
            Assert.AreSame(world.Layout, hud.Map.BoundLayout, "and a new map replaces it");
            Assert.AreEqual(0, hud.Map.Fog.SeenCount, "under fresh fog");
        }

        [Test]
        public void YouMarker_FollowsThePlayer_OnBothMaps()
        {
            SandboxWorld world = NewWorld();
            var hud = hudGo.AddComponent<GameHud>();
            hud.world = world;
            hud.Refresh();
            hud.SyncMap();
            MapHud map = hud.Map;
            float full = MapHud.FullPx / (2f * world.halfExtent), mini = MapHud.MiniPx / MapHud.MiniSpan;

            void CheckAt(Vector2 at, string when)
            {
                world.Player.Teleport(world.transform.TransformPoint(new Vector3(at.x, 0f, at.y)));
                map.Update(true);
                Assert.That(Vector2.Distance(map.YouArrow.anchoredPosition, at * full), Is.LessThan(0.6f), $"{when}: the arrow is where the player is");
                Assert.That(Vector2.Distance(map.YouRing.anchoredPosition, at * full), Is.LessThan(0.6f), $"{when}: and so is the ring");
                Assert.That(map.YouLabel.anchoredPosition.y, Is.GreaterThan(at.y * full + 20f), $"{when}: the label sits above it");
                Assert.That(Vector2.Distance(map.MiniContent.anchoredPosition, -at * mini), Is.LessThan(0.6f), $"{when}: the minimap is centred on the player");
            }

            CheckAt(new Vector2(30f, -40f), "first frame");
            CheckAt(new Vector2(-55f, 62f), "after a teleport");
            map.Bind(world);
            CheckAt(new Vector2(-55.5f, 62f), "after the map is rebuilt");
            world.Regenerate(world.seed + 7);
            hud.SyncMap();
            CheckAt(new Vector2(12f, 70f), "after a new map");
        }

        [Test]
        public void TestingMode_RevealsTheWholeMap()
        {
            SandboxWorld world = NewWorld();
            var hud = hudGo.AddComponent<GameHud>();
            hud.world = world;
            hud.Refresh();
            hud.SyncMap();
            Assert.AreEqual(0, hud.Map.Fog.SeenCount, "a normal match starts under fog");
            hud.StartTesting(false);
            hud.Map.Update(true);
            Assert.IsTrue(hud.Map.Fog.AllRevealed, "testing mode shows everything");
            world.Regenerate(world.seed + 1);
            hud.SyncMap();
            Assert.IsTrue(hud.Map.Fog.AllRevealed, "and again after a new map");
        }

        [Test]
        public void Minimap_ShowsThingsOnlyOnceSeen()
        {
            SandboxWorld world = NewWorld();
            var hud = hudGo.AddComponent<GameHud>();
            hud.world = world;
            hud.Refresh();
            hud.Map.Bind(world);
            Assert.AreEqual(0, hud.Map.Fog.SeenCount, "a fresh map is black");
            GameSession.TestingMode = true;
            hud.Map.RevealAll();
            Assert.AreEqual(hud.Map.Fog.Size * hud.Map.Fog.Size, hud.Map.Fog.SeenCount, "testing: reveal all");
            hud.Map.Update(true);
            int icons = hudGo.GetComponentsInChildren<UnityEngine.UI.Image>(true).Count(i => i.name == "Icon" && (i.transform.parent.name == "Content" || i.transform.parent.name == "Map"));
            int gens = GeneratorObjective.All.Count(g => g != null && g.transform.IsChildOf(world.transform));
            Assert.AreEqual(5, gens);
            Assert.AreEqual(2 * (world.Pickups.Count + gens + 1), icons, "every supply, generator and the gate, on the minimap and the full map");
        }
    }
}
