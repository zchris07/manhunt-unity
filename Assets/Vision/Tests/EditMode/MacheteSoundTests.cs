using NUnit.Framework;
using UnityEngine;
using Vision.Audio;
using Vision.UI;

namespace Vision.Tests
{
    public class MacheteSoundTests
    {
        static MatchEffects.Surface Of(string name, string parent = null)
        {
            var go = new GameObject(name);
            GameObject top = go;
            if (parent != null)
            {
                top = new GameObject(parent);
                go.transform.SetParent(top.transform, false);
            }
            var c = go.AddComponent<BoxCollider>();
            try { return MatchEffects.SurfaceOf(c); }
            finally { Object.DestroyImmediate(top); }
        }

        [Test]
        public void TheMachete_SoundsByWhatItStrikes()
        {
            // Wood: evergreens, dead trees, power poles, cabins' doors, crates.
            foreach (string wood in new[] { "Conifer_07", "DeadTree_12", "Power Pole", "Door", "Crate", "Fence", "Log", "Hanging Tree" })
                Assert.AreEqual(MatchEffects.Surface.Wood, Of(wood), wood);
            // Sturdy metal: cars, generators, light towers, lockers, the exit gate.
            foreach (string heavy in new[] { "Car_02", "Light Tower", "Locker", "Exit Gate", "Ambulance", "Fridge" })
                Assert.AreEqual(MatchEffects.Surface.HeavyMetal, Of(heavy), heavy);
            // Other metal: fences, gates, lampposts, barrels.
            foreach (string metal in new[] { "Iron Fence", "Yard Fence", "Graveyard Gates", "LanternPost", "BurningBarrel", "Rack", "Generator" })
                Assert.AreEqual(MatchEffects.Surface.Metal, Of(metal), metal);
            // Walls of a structure (a rock's "wall" is stone, as are the gate's pillars).
            foreach (string wall in new[] { "Outer Wall", "Wall", "Plank Wall" })
                Assert.AreEqual(MatchEffects.Surface.Wall, Of(wall), wall);
            Assert.AreEqual(MatchEffects.Surface.Stone, Of("Rock Wall"));
            Assert.AreEqual(MatchEffects.Surface.Stone, Of("Gate Pillar"));
            Assert.AreEqual(MatchEffects.Surface.Glass, Of("Window"));
            Assert.AreEqual(MatchEffects.Surface.Metal, Of("Barrel", "Building"), "its own name first");

            Assert.AreEqual("machete_wood", MatchEffects.CueFor(MatchEffects.Surface.Wood));
            Assert.AreEqual("metal_heavy", MatchEffects.CueFor(MatchEffects.Surface.HeavyMetal));
            Assert.AreEqual("metal_light", MatchEffects.CueFor(MatchEffects.Surface.Metal));
            Assert.AreEqual("wall_slam", MatchEffects.CueFor(MatchEffects.Surface.Wall));
            Assert.IsNull(MatchEffects.CueFor(MatchEffects.Surface.Stone));
        }

        [Test]
        public void ASecondShotgun_AddsItsShells_ToTheOneCarried_TheLegendaryPumpNeverShares()
        {
            var inv = new Vision.Player.Inventory();
            inv.Add(Vision.Player.ItemType.Shotgun, 1, 4f);
            inv.Add(Vision.Player.ItemType.Shotgun, 1, 3f);
            Assert.AreEqual(1, inv.Count(Vision.Player.ItemType.Shotgun), "still one shotgun");
            int slot = inv.FirstSlotOf(Vision.Player.ItemType.Shotgun);
            Assert.AreEqual(7f, inv.AmountAt(slot), 1e-4f, "its 4 shells and the other's 3");
            inv.Add(Vision.Player.ItemType.Shotgun, 1, 5f, true);
            Assert.AreEqual(2, inv.Count(Vision.Player.ItemType.Shotgun), "the Legendary pump its own gun");
            inv.Add(Vision.Player.ItemType.Shotgun, 1, 2f, true);
            Assert.AreEqual(7f, inv.AmountAt(slot), 1e-4f, "the plain shotgun's shells untouched");
            Assert.AreEqual(2, inv.Count(Vision.Player.ItemType.Shotgun), "a second Legendary pump goes into the first");
            for (int i = 0; i < 16; i++)
                if (inv.GoldenAt(i)) Assert.AreEqual(7f, inv.AmountAt(i), 1e-4f, "its 5 and the other's 2");
        }

        [Test]
        public void TheLoudnesses_AsAsked()
        {
            var c = SoundBank.Cues;
            Assert.AreEqual(0.425f, c["machete_wood"].Volume, 1e-4f, "wood half as loud as it was (0.85)");
            Assert.AreEqual(0.5f, c["shotgun_shot"].Volume, 1e-4f, "the shotgun half as loud as it was (1)");
            Assert.AreEqual(0.9f, c["pistol_shot"].Volume, 1e-4f, "the P250 as it was");
            Assert.AreEqual(c["machete_wood"].Volume, c["metal_heavy"].Volume, "sturdy metal as loud as wood");
            Assert.AreEqual(c["machete_wood"].Volume, c["metal_light"].Volume, "other metal as loud");
            Assert.AreEqual(c["shotgun_shot"].Volume, c["sniper_shot"].Volume, "the 0.50 cal as loud as the shotgun");
            Assert.Less(c["bullet_flesh"].Volume, c["machete_wood"].Volume, "bullets into bodies quiet");
            CollectionAssert.AreEqual(new[] { "local_headshot_1", "local_headshot_2", "local_headshot_3" }, c["bullet_flesh"].Prefer);
            CollectionAssert.AreEqual(new[] { "local_bolt_sniper" }, c["sniper_shot"].Prefer);
            CollectionAssert.AreEqual(new[] { "local_glass_break" }, c["glass"].Prefer);
            Assert.AreEqual(3, c["metal_heavy"].Prefer.Length);
            Assert.AreEqual(9, c["metal_light"].Prefer.Length);
            Assert.AreEqual(3, c["wall_slam"].Prefer.Length);
        }
    }
}
