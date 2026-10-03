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
        public void Damage_Heal_AndDeath()
        {
            var v = new Vitals();
            int damaged = 0, died = 0;
            v.Damaged += _ => damaged++;
            v.Died += () => died++;
            v.TakeDamage(30f);
            Assert.AreEqual(70f, v.Health);
            v.Heal(100f);
            Assert.AreEqual(100f, v.Health, "clamped");
            v.TakeDamage(250f);
            Assert.IsTrue(v.IsDead);
            Assert.AreEqual(0f, v.Health);
            v.TakeDamage(10f);
            v.Heal(50f);
            Assert.AreEqual(0f, v.Health, "no healing the dead");
            Assert.AreEqual(2, damaged);
            Assert.AreEqual(1, died);
            Assert.IsFalse(v.CanSprint);
        }

        [Test]
        public void Inventory_StacksFillsAndUses()
        {
            var inv = new Inventory();
            Assert.AreEqual(7, inv.Add(ItemType.Water, 7));
            Assert.AreEqual(ItemType.Water, inv.ItemAt(0));
            Assert.AreEqual(5, inv.CountAt(0), "stacks of five");
            Assert.AreEqual(2, inv.CountAt(1));
            for (int i = 0; i < 4; i++) inv.Add(ItemType.Bandage, 5);
            Assert.AreEqual(0, inv.Add(ItemType.CannedFood), "six slots, all full");
            Assert.AreEqual(3, inv.Add(ItemType.Water, 3), "but a stack with room still takes more");
            Assert.IsTrue(inv.Remove(1));
            Assert.AreEqual(4, inv.CountAt(1));
            Assert.AreEqual(9, inv.Count(ItemType.Water), "seven, three more, one used");
        }

        [Test]
        public void UsingItems_HealsAndRestores_ButNotWhenFull()
        {
            var go = new GameObject("p");
            var stats = go.AddComponent<PlayerStats>();
            try
            {
                stats.inventory.Add(ItemType.Bandage, 1);
                stats.inventory.Add(ItemType.Water, 1);
                Assert.IsFalse(stats.UseSlot(0), "a bandage at full health does nothing");
                Assert.AreEqual(1, stats.inventory.CountAt(0), "and is kept");
                stats.vitals.TakeDamage(50f);
                Assert.IsTrue(stats.UseSlot(0));
                Assert.AreEqual(85f, stats.vitals.Health, 1e-3f);
                Assert.IsNull(stats.inventory.ItemAt(0));
                stats.vitals.Tick(3f, true);
                float before = stats.vitals.Stamina;
                Assert.IsTrue(stats.UseSlot(1));
                Assert.AreEqual(Mathf.Min(100f, before + 60f), stats.vitals.Stamina, 1e-3f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Pickup_MovesWhatFitsIntoTheInventory()
        {
            var go = new GameObject("pickup");
            var pickup = go.AddComponent<Pickup>();
            pickup.item = ItemType.CannedFood;
            pickup.count = 2;
            var inv = new Inventory();
            Assert.AreEqual("Canned food x2", pickup.Label);
            Assert.AreEqual(2, pickup.TakeInto(inv));
            Assert.IsTrue(go == null, "taken in full, it is gone");
            Assert.AreEqual(2, inv.Count(ItemType.CannedFood));
        }

        [Test]
        public void Hud_BuildsAndShowsThePlayersState()
        {
            var root = new GameObject("World");
            var mat = new Material(Shader.Find("Vision/LowPoly"));
            var hudGo = new GameObject("HUD");
            try
            {
                var world = root.AddComponent<SandboxWorld>();
                world.lowPolyMaterial = world.entityMaterial = world.glowMaterial = mat;
                world.Generate();
                Assert.Greater(world.Pickups.Count, 10, "supplies are spread around");
                var hud = hudGo.AddComponent<GameHud>();
                hud.world = world;
                var stats = world.Player.GetComponent<PlayerStats>();
                Assert.NotNull(stats, "the player has health, stamina and an inventory");
                stats.vitals.TakeDamage(40f);
                stats.inventory.Add(ItemType.Water, 2);
                hud.Refresh();
                var texts = hudGo.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                bool health = false, water = false;
                foreach (var t in texts)
                {
                    if (t.name == "Health Value" && t.text == "60") health = true;
                    if (t.name == "Name" && t.text == "Water") water = true;
                }
                Assert.IsTrue(health, "health shows 60");
                Assert.IsTrue(water, "the water is in a slot");
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
