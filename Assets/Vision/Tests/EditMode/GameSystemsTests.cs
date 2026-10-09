using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Player;
using Vision.World;

namespace Vision.Tests
{
    public class GameSystemsTests
    {
        [Test]
        public void Pickup_MovesWhatFitsIntoTheInventory()
        {
            var go = new GameObject("pickup");
            var pickup = go.AddComponent<Pickup>();
            pickup.item = ItemType.MrBeastBar;
            pickup.count = 2;
            var inv = new Inventory();
            Assert.AreEqual("Mr Beast bar x2", pickup.Label);
            Assert.AreEqual(2, pickup.TakeInto(inv));
            Assert.IsTrue(go == null, "taken in full, it is gone");
            Assert.AreEqual(2, inv.Count(ItemType.MrBeastBar));
        }

        [Test]
        public void Generators_StartAfterSeventySeconds_ThenTheLeverOpensTheGate()
        {
            var objects = new GameObject[6];
            try
            {
                var gens = new GeneratorObjective[5];
                for (int i = 0; i < 5; i++)
                {
                    objects[i] = new GameObject("Generator " + i);
                    gens[i] = objects[i].AddComponent<GeneratorObjective>();
                }
                objects[5] = new GameObject("Gate");
                var gate = objects[5].AddComponent<ExitGate>();
                Assert.AreEqual(5, GeneratorObjective.All.Count);
                Assert.IsFalse(gate.PullLever(30f), "no power, the lever does nothing");
                Assert.AreEqual(0f, gate.LeverProgress);

                Assert.IsFalse(gens[0].Repair(35f));
                Assert.AreEqual(0.5f, gens[0].progress, 1e-4f, "halfway after 35 s");
                Assert.IsTrue(gens[0].Repair(35f), "running after 70 s");
                Assert.IsFalse(gens[0].Repair(10f), "only starts once");
                Assert.AreEqual(1, GeneratorObjective.RunningCount);
                for (int i = 1; i < 5; i++) gens[i].Repair(GeneratorObjective.RepairTime);
                Assert.IsTrue(GeneratorObjective.AllRunning);
                Assert.IsTrue(ExitGate.Powered);

                Assert.IsFalse(gate.PullLever(10f));
                Assert.AreEqual(0.5f, gate.LeverProgress, 1e-4f);
                Assert.IsTrue(gate.PullLever(10f), "twenty seconds on the lever");
                Assert.IsTrue(gate.IsOpen);
            }
            finally
            {
                foreach (GameObject go in objects) if (go != null) Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Level_HasTheOriginalsSupplies_FiveGenerators_AndTheHud()
        {
            var root = new GameObject("World");
            var mat = new Material(Shader.Find("Vision/LowPoly"));
            var hudGo = new GameObject("HUD");
            try
            {
                var world = root.AddComponent<SandboxWorld>();
                world.lowPolyMaterial = world.entityMaterial = world.glowMaterial = mat;
                world.Generate();
                // The original's 96, and the kit in a row beside Chris Zelley's ambulance on top.
                ItemType[] kit = { ItemType.MiniShield, ItemType.MiniShield, ItemType.MrBeastBar, ItemType.MrBeastBar, ItemType.Confit };
                Assert.AreEqual(96 + kit.Length, world.Pickups.Count, "the original's 96 supplies and the ambulance kit");
                foreach (ItemType t in Items.All)
                    Assert.AreEqual(Items.Info(t).mapCount + kit.Count(k => k == t), world.Pickups.Count(p => p.item == t), $"{t} at the original's count");
                Rect building = world.Layout.Building;
                int indoors = world.Pickups.Count(p =>
                {
                    Vector3 lp = world.transform.InverseTransformPoint(p.transform.position);
                    return building.Contains(new Vector2(lp.x, lp.z));
                });
                Assert.Greater(indoors, 5, "some lie in the building");
                Assert.AreEqual(5, GeneratorObjective.All.Count, "five generators to start");

                var hud = hudGo.AddComponent<GameHud>();
                hud.world = world;
                MatchHost host = MatchHost.For(world);
                host.Begin();
                SimPlayer me = world.Player.Me;
                Assert.NotNull(me, "the player is in the match, with health, shield, stamina and an inventory");
                me.Shield = 0.25f;
                host.Sim.HurtSurvivor(me, 0.65f, null, "test");
                me.Inv.Add(ItemType.MiniShield, 2);
                hud.Refresh();
                var texts = hudGo.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                Assert.IsTrue(texts.Any(t => t.name == "Health Value" && t.text == "60"), "health shows 60");
                Assert.IsTrue(texts.Any(t => t.name == "Shield Value" && t.text == "0"), "the shield went first");
                Assert.IsTrue(texts.Any(t => t.name == "Name" && t.text == "MINI SHIELD"), "the mini shields are in a slot");
                Assert.AreEqual(8, texts.Count(t => t.name == "Key" && t.gameObject.activeInHierarchy), "eight slots");
                Assert.IsTrue(texts.Any(t => t.name.StartsWith("Objective") && t.text.Contains($"Generators 0/{host.Sim.Bal.RequiredGenerators}")), "the objective counts the generators");
                Assert.AreEqual("NE", GameHud.Cardinal(44f));
                Assert.AreEqual("N", GameHud.Cardinal(350f));
            }
            finally
            {
                Object.DestroyImmediate(hudGo);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mat);
            }
        }
    }
}
