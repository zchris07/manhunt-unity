using NUnit.Framework;
using UnityEngine;
using Vision.Effects;
using Vision.Game;
using Vision.Player;
using Vision.UI;
using Vision.World;

namespace Vision.Tests
{
    /// <summary>Every item, against the original's tests (abilities.test.ts), and how items show in the world.</summary>
    public class ItemTests
    {
        MatchState saved;

        [SetUp]
        public void SetUp()
        {
            saved = MatchState.Current;
            MatchState.Current = new MatchState();
        }

        [TearDown]
        public void TearDown()
        {
            MatchState.Current = saved;
            foreach (var fx in Object.FindObjectsByType<Vfx>(FindObjectsSortMode.None)) Object.DestroyImmediate(fx.gameObject);
        }

        static int Secs(float s) => SimRig.Secs(s);

        /// <summary>A survivor at the origin, Zach some way east of them, a teammate far off.</summary>
        static SimRig Duel(float dist)
        {
            var rig = new SimRig(2, 1);
            rig.Place(rig.P(2), 0f, 0f);
            rig.Place(rig.P(1), dist, 0f);
            rig.Place(rig.P(3), -2500f, 2500f);
            return rig;
        }

        static void Use(SimRig rig, SimPlayer s, ItemType item)
        {
            int slot = s.Inv.FirstSlotOf(item) + 1;
            rig.Run(1, p => p.Id == s.Id ? new InputCmd { Item = slot, AimDist = 100f } : (InputCmd?)null);
            rig.Tap(s.Id, Btn.Primary, 0f, slot);
        }

        [Test]
        public void ABottle_StunsZach_AndImmunityStopsAChainStun()
        {
            SimRig rig = Duel(200f);
            SimPlayer s = rig.P(2), h = rig.P(1);
            s.Inv.Add(ItemType.Bottle, 2);
            Use(rig, s, ItemType.Bottle);
            rig.Run(Secs(0.4f));
            Assert.Greater(h.StunT, 0f);
            Assert.Greater(h.ImmuneT, Balance.Items.StunImmunity);
            Assert.AreEqual(1, s.Inv.Count(ItemType.Bottle));
            Use(rig, s, ItemType.Bottle);
            rig.Run(Secs(0.4f));
            Assert.AreEqual(0, s.Inv.Count(ItemType.Bottle));
            Assert.AreEqual(1, s.Stats.Stuns, "the second bounced off his immunity");
        }

        [Test]
        public void TheShotgun_StunsAndShovesZach_ReloadsTwoSeconds_SixShells()
        {
            SimRig rig = Duel(200f);
            SimPlayer s = rig.P(2), h = rig.P(1);
            s.Inv.Add(ItemType.Shotgun);
            float x0 = h.Pos.x;
            Use(rig, s, ItemType.Shotgun);
            Assert.Greater(h.StunT, 0f);
            rig.Run(Secs(0.5f));
            Assert.Greater(Scale.ToUnits(h.Pos.x - x0), 40f, "shoved back");
            Assert.AreEqual(5f, s.Inv.AmountAt(0), 1e-4f);
            Assert.Greater(s.ReloadT, 1f);
            Use(rig, s, ItemType.Shotgun);
            Assert.AreEqual(5f, s.Inv.AmountAt(0), 1e-4f, "still reloading");
            for (int i = 0; i < 5; i++)
            {
                rig.Run(Secs(Balance.Items.Shotgun.Reload));
                Use(rig, s, ItemType.Shotgun);
            }
            Assert.AreEqual(0, s.Inv.Count(ItemType.Shotgun), "six shells, then it's gone");
        }

        [Test]
        public void NightVision_OnlyWhileHeld_UsedUpForGood()
        {
            SimRig rig = Duel(2000f);
            SimPlayer s = rig.P(2);
            s.Inv.Add(ItemType.Goggles);
            int slot = s.Inv.FirstSlotOf(ItemType.Goggles) + 1;
            InputCmd? Held(SimPlayer p) => p.Id == 2 ? new InputCmd { Buttons = Btn.Primary, Item = slot, AimDist = 100f } : (InputCmd?)null;
            rig.Run(2, Held);
            Assert.IsTrue(s.GogglesOn);
            rig.Run(Secs(1f), Held);
            rig.Run(1, p => p.Id == 2 ? new InputCmd { Item = slot, AimDist = 100f } : (InputCmd?)null);
            Assert.IsFalse(s.GogglesOn, "off when let go");
            float left = s.Inv.AmountAt(0);
            Assert.Less(left, Balance.Items.Goggles.Meter);
            rig.Run(Secs(3f));
            Assert.AreEqual(left, s.Inv.AmountAt(0), 1e-4f, "no drain while off");
            s.Inv.SetAmountAt(0, 0.5f);
            rig.Run(Secs(1f), Held);
            Assert.IsFalse(s.GogglesOn);
            Assert.AreEqual(0, s.Inv.Count(ItemType.Goggles), "used up");
        }

        [Test]
        public void DoctorPepper_AddsTwoSecondsToTheMeterForTwentySeconds()
        {
            SimRig rig = Duel(2000f);
            SimPlayer s = rig.P(2);
            s.Inv.Add(ItemType.DoctorPepper);
            Use(rig, s, ItemType.DoctorPepper);
            Assert.Greater(s.Move.BoostT, Balance.Items.Energy.Duration - 0.2f);
            Assert.Greater(MoveState.MaxStamina(Role.Survivor, s.Move.BoostT), Balance.Survivor.StaminaMax + 1.9f);
            Assert.AreEqual(0, s.Inv.Count(ItemType.DoctorPepper));
        }

        [Test]
        public void AGasTrap_IsSetInTwoSeconds_ArmsBurstsNearZach_AndSlowsHim()
        {
            SimRig rig = Duel(3000f);
            SimPlayer s = rig.P(2), h = rig.P(1);
            s.Inv.Add(ItemType.Trap);
            Use(rig, s, ItemType.Trap);
            Assert.AreEqual(0, rig.Sim.Traps.Count);
            Assert.AreEqual(ActionKind.Plant, s.Action);
            int slot = s.Inv.FirstSlotOf(ItemType.Trap) + 1;
            rig.Run(Secs(Balance.Items.Trap.PlantTime) + 1, p => p.Id == 2 ? new InputCmd { Item = slot, AimDist = 100f } : (InputCmd?)null);
            Assert.AreEqual(1, rig.Sim.Traps.Count, "planted");
            rig.Run(Secs(Balance.Items.Trap.ArmTime) + 2);
            rig.Place(s, -800f, 0f);
            Vector2 trap = rig.Sim.Traps[0].Pos;
            rig.Place(h, Scale.ToUnits(trap.x) + Balance.Items.Trap.TriggerRadius - 20f, Scale.ToUnits(trap.y));
            rig.Run(Secs(Balance.Items.Trap.SpreadTime) + 2);
            Assert.AreEqual(0, rig.Sim.Traps.Count);
            Assert.AreEqual(1, rig.Sim.Gases.Count, "a cloud of galaxy gas");
            Assert.IsTrue(h.Gassed, "and Zach is in it");
        }

        [Test]
        public void G_DropsAnItem_ThatAnyoneCanPickUp_AndTabSwapsSlots()
        {
            SimRig rig = Duel(3000f);
            SimPlayer s = rig.P(2), mate = rig.P(3);
            s.Inv.Add(ItemType.Bottle, 3);
            s.Inv.Add(ItemType.Book);
            int slot = s.Inv.FirstSlotOf(ItemType.Bottle) + 1;
            rig.Run(1, p => p.Id == 2 ? new InputCmd { Item = slot, AimDist = 100f } : (InputCmd?)null);
            rig.Tap(2, Btn.Drop, 0f, slot);
            Assert.AreEqual(2, s.Inv.Count(ItemType.Bottle), "one dropped");
            Assert.AreEqual(1, rig.Sim.Drops.Count);
            rig.Sim.MoveSlot(2, 0, 1);
            Assert.AreEqual(ItemType.Book, s.Inv.ItemAt(0), "swapped");
            Assert.AreEqual(ItemType.Bottle, s.Inv.ItemAt(1));
            DropItem d = rig.Sim.Drops[0];
            rig.Place(mate, Scale.ToUnits(d.Pos.x) + 20f, Scale.ToUnits(d.Pos.y));
            rig.Run(2);
            Assert.AreEqual(Prompt.PickDrop, mate.Prompt);
            rig.Tap(3, Btn.Interact);
            rig.Run(Secs(1f));
            Assert.AreEqual(1, mate.Inv.Count(ItemType.Bottle), "picked up");
            Assert.AreEqual(0, rig.Sim.Drops.Count);
        }

        [Test]
        public void ItemsInPlay_AreDrawn_Thrown_Traps_AndDrops()
        {
            var root = new GameObject("World");
            var mat = new Material(Shader.Find("Vision/LowPoly"));
            try
            {
                MatchState.Current.TestingMode = true;
                var world = root.AddComponent<SandboxWorld>();
                world.lowPolyMaterial = world.entityMaterial = world.glowMaterial = mat;
                world.Generate();
                MatchHost host = MatchHost.For(world);
                host.Begin();
                MatchSim sim = host.Sim;
                SimPlayer me = host.Local;
                sim.Thrown.Add(new ThrownItem { Id = 900, Pos = me.Pos, Dir = Vector2.right, Owner = me.Id, Item = ItemType.Book });
                sim.Traps.Add(new Trap { Id = 901, Pos = me.Pos + Vector2.up });
                sim.Drops.Add(new DropItem { Id = 902, Pos = me.Pos + Vector2.left, Item = ItemType.Sniper });
                sim.Gases.Add(new GasCloud { Id = 903, Pos = me.Pos + Vector2.down, Age = 1f });
                var go = new GameObject("Items");
                var views = go.AddComponent<ItemViews>();
                views.world = world;
                views.Sync(0.016f);
                Assert.AreEqual(1, views.ThrownCount);
                Assert.AreEqual(1, views.TrapCount);
                Assert.AreEqual(1, views.DropCount);
                sim.Thrown.Clear();
                sim.Drops.Clear();
                views.Sync(0.016f);
                Assert.AreEqual(0, views.ThrownCount, "gone when the match drops it");
                Assert.AreEqual(0, views.DropCount);
                Object.DestroyImmediate(go);
                foreach (ItemType t in Items.All)
                {
                    Mesh m = LowPolyModels.Item(new System.Random(1), t, t == ItemType.Shotgun);
                    Assert.Greater(m.vertexCount, 0, t.ToString());
                    Object.DestroyImmediate(m);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mat);
            }
        }
    }
}
