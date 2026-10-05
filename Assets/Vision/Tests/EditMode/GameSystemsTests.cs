using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Player;
using Vision.World;

namespace Vision.Tests
{
    public class GameSystemsTests
    {
        [Test]
        public void Sprinting_DrainsStamina_ThenItRefillsAfterAPause()
        {
            var v = new Vitals();
            v.Tick(1f, true);
            Assert.AreEqual(100f - v.sprintDrain, v.Stamina, 1e-3f);
            float drained = v.Stamina;
            v.Tick(v.regenDelay * 0.5f, false);
            Assert.AreEqual(drained, v.Stamina, 1e-3f, "no refill during the pause");
            v.Tick(v.regenDelay, false);
            v.Tick(1f, false);
            Assert.Greater(v.Stamina, drained, "refills after the pause");
            for (int i = 0; i < 100; i++) v.Tick(1f, false);
            Assert.AreEqual(v.maxStamina, v.Stamina, 1e-3f, "never above the maximum");
        }

        [Test]
        public void RunningDry_LocksSprintUntilAQuarterIsBack()
        {
            var v = new Vitals();
            for (int i = 0; i < 20 && !v.Exhausted; i++) v.Tick(1f, true);
            Assert.IsTrue(v.Exhausted);
            Assert.IsFalse(v.CanSprint);
            v.Tick(v.regenDelay, false);
            while (v.Stamina < v.maxStamina * v.recoverFraction - 1f) { v.Tick(0.1f, false); Assert.IsFalse(v.CanSprint, "still locked"); }
            v.Tick(0.5f, false);
            Assert.IsTrue(v.CanSprint, "sprint returns above a quarter");
        }

        [Test]
        public void Damage_TakesTheShieldFirst_ThenHealth_ThenDowns()
        {
            var v = new Vitals();
            int damaged = 0, downed = 0;
            v.Damaged += _ => damaged++;
            v.Downed += () => downed++;
            Assert.AreEqual(1f, v.Health);
            Assert.AreEqual(0f, v.Shield);
            v.AddShield(0.25f);
            v.AddShield(0.25f);
            Assert.AreEqual(0.5f, v.Shield, 1e-5f);
            v.TakeDamage(0.3f);
            Assert.AreEqual(0.2f, v.Shield, 1e-5f, "the shield takes it");
            Assert.AreEqual(1f, v.Health, 1e-5f);
            v.TakeDamage(0.5f);
            Assert.AreEqual(0f, v.Shield, 1e-5f);
            Assert.AreEqual(0.7f, v.Health, 1e-5f, "the rest comes off health");
            v.Heal(1f);
            Assert.AreEqual(1f, v.Health, "clamped at full");
            for (int i = 0; i < 4; i++) v.AddShield(0.25f);
            v.AddShield(0.25f);
            Assert.AreEqual(1f, v.Shield, "shield up to 100%");
            v.TakeDamage(3f);
            Assert.IsTrue(v.IsDowned);
            Assert.AreEqual(0f, v.Health);
            Assert.IsFalse(v.CanSprint, "no sprinting while downed");
            v.Heal(0.5f);
            v.AddShield(0.5f);
            Assert.AreEqual(0f, v.Health, "nothing heals the downed");
            Assert.AreEqual(1, downed);
            v.StandUp();
            Assert.IsFalse(v.IsDowned);
            Assert.AreEqual(Vitals.ReviveHealth, v.Health, 1e-5f, "back up with a third of the bar");
            Assert.AreEqual(3, damaged);
        }

        [Test]
        public void Health_NeverRegenerates()
        {
            var v = new Vitals();
            v.TakeDamage(0.4f);
            for (int i = 0; i < 600; i++) v.Tick(1f, false);
            Assert.AreEqual(0.6f, v.Health, 1e-5f);
        }

        [Test]
        public void Inventory_EightSlots_UnlimitedStacks_WeaponsAlone()
        {
            var inv = new Inventory();
            Assert.AreEqual(8, Inventory.Slots);
            Assert.AreEqual(50, inv.Add(ItemType.Bottle, 50));
            Assert.AreEqual(50, inv.CountAt(0), "one unlimited stack");
            Assert.IsNull(inv.ItemAt(1));
            Assert.AreEqual(2, inv.Add(ItemType.Shotgun, 2));
            Assert.AreEqual(ItemType.Shotgun, inv.ItemAt(1));
            Assert.AreEqual(ItemType.Shotgun, inv.ItemAt(2), "each shotgun takes a slot");
            Assert.AreEqual(1, inv.CountAt(1));
            foreach (ItemType t in new[] { ItemType.Book, ItemType.Goggles, ItemType.Confit, ItemType.MrBeastBar, ItemType.Trap }) inv.Add(t, 1);
            Assert.AreEqual(0, inv.Add(ItemType.MiniShield), "eight slots, all taken");
            Assert.AreEqual(3, inv.Add(ItemType.MrBeastBar, 3), "but a stack always takes more");
            Assert.IsTrue(inv.Remove(0));
            Assert.AreEqual(49, inv.Count(ItemType.Bottle));
            inv.Infinite = true;
            Assert.IsTrue(inv.Remove(0));
            Assert.AreEqual(49, inv.Count(ItemType.Bottle), "testing mode never runs out");
        }

        [Test]
        public void Supplies_HealAsTheOriginal()
        {
            var go = new GameObject("p");
            var stats = go.AddComponent<PlayerStats>();
            try
            {
                stats.inventory.Add(ItemType.Confit, 1);
                stats.inventory.Add(ItemType.MrBeastBar, 2);
                stats.inventory.Add(ItemType.MiniShield, 2);
                stats.inventory.Add(ItemType.Bottle, 1);
                Assert.AreEqual(PlayerStats.UseResult.AlreadyFull, stats.UseSlot(0), "confit at full health does nothing");
                Assert.AreEqual(1, stats.inventory.CountAt(0), "and is kept");
                stats.vitals.TakeDamage(0.7f);
                Assert.AreEqual(PlayerStats.UseResult.Used, stats.UseSlot(1));
                Assert.AreEqual(0.5f, stats.vitals.Health, 1e-5f, "Mr Beast bar: +20%");
                Assert.AreEqual(PlayerStats.UseResult.Used, stats.UseSlot(0));
                Assert.AreEqual(1f, stats.vitals.Health, 1e-5f, "confit: full");
                Assert.IsNull(stats.inventory.ItemAt(0));

                Assert.AreEqual(PlayerStats.UseResult.Drinking, stats.UseSlot(2));
                Assert.IsFalse(stats.Tick(1f, false));
                Assert.IsFalse(stats.Tick(0.5f, true), "moving spills it");
                Assert.AreEqual(0f, stats.vitals.Shield);
                Assert.AreEqual(2, stats.inventory.CountAt(2), "and keeps the bottle");
                stats.UseSlot(2);
                Assert.IsFalse(stats.Tick(1.5f, false));
                Assert.IsTrue(stats.Tick(0.6f, false), "two seconds standing still");
                Assert.AreEqual(0.25f, stats.vitals.Shield, 1e-5f, "+25% shield");
                Assert.AreEqual(1, stats.inventory.CountAt(2));

                Assert.AreEqual(PlayerStats.UseResult.NotYet, stats.UseSlot(3), "bottles are only collected for now");
            }
            finally { Object.DestroyImmediate(go); }
        }

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
                Assert.AreEqual(86, world.Pickups.Count, "the original's 86 supplies");
                foreach (ItemType t in Items.All)
                    Assert.AreEqual(Items.Info(t).mapCount, world.Pickups.Count(p => p.item == t), $"{t} at the original's count");
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
                var stats = world.Player.GetComponent<PlayerStats>();
                Assert.NotNull(stats, "the player has health, shield, stamina and an inventory");
                stats.vitals.AddShield(0.25f);
                stats.vitals.TakeDamage(0.65f);
                stats.inventory.Add(ItemType.MiniShield, 2);
                hud.Refresh();
                var texts = hudGo.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                Assert.IsTrue(texts.Any(t => t.name == "Health Value" && t.text == "60"), "health shows 60");
                Assert.IsTrue(texts.Any(t => t.name == "Shield Value" && t.text == "0"), "the shield went first");
                Assert.IsTrue(texts.Any(t => t.name == "Name" && t.text == "Mini shield"), "the mini shields are in a slot");
                Assert.AreEqual(8, texts.Count(t => t.name == "Key"), "eight slots");
                Assert.IsTrue(texts.Any(t => t.name == "Objective" && t.text.Contains("Generators 0/5")), "the objective counts the generators");
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
