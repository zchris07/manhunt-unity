using System.Linq;
using NUnit.Framework;
using UnityEngine;
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
        public void TestingKit_IsTheOriginals_AndNeverRunsOut()
        {
            var go = new GameObject("p");
            try
            {
                var stats = go.AddComponent<PlayerStats>();
                stats.inventory.Add(ItemType.Confit, 3);
                GameSession.ApplyTestKit(stats);
                Assert.AreEqual(9, stats.inventory.Count(ItemType.Bottle));
                Assert.AreEqual(9, stats.inventory.Count(ItemType.Book));
                Assert.AreEqual(0, stats.inventory.Count(ItemType.Confit), "the kit replaces what you had");
                for (int i = 0; i < Inventory.Slots; i++) Assert.NotNull(stats.inventory.ItemAt(i), "all eight slots filled");
                stats.vitals.TakeDamage(0.5f);
                int beast = Enumerable.Range(0, Inventory.Slots).First(i => stats.inventory.ItemAt(i) == ItemType.MrBeastBar);
                for (int k = 0; k < 5; k++) stats.UseSlot(beast);
                Assert.AreEqual(1, stats.inventory.Count(ItemType.MrBeastBar), "never used up");
                Assert.AreEqual(1f, stats.vitals.Health, 1e-4f);
                Assert.AreEqual(2f, GameSession.SpeedMultiplier, "speed mode: +100%");
            }
            finally { Object.DestroyImmediate(go); }
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

            hud.ShowMainMenu();
            Assert.IsTrue(GameHud.MainMenuOpen);
            Assert.IsTrue(GameHud.MenuOpen, "the player waits on the title screen");
            hud.SetMenu(true);
            Assert.IsTrue(GameHud.MainMenuOpen, "no game menu over the title screen");

            hud.StartTesting(false);
            Assert.IsFalse(GameHud.MainMenuOpen);
            Assert.IsTrue(GameSession.TestingMode);
            var stats = world.Player.GetComponent<PlayerStats>();
            Assert.IsTrue(stats.inventory.Infinite, "the testing kit");
            Assert.AreEqual(9, stats.inventory.Count(ItemType.Bottle));

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
