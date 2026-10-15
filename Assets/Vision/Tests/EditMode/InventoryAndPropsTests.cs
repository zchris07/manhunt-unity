using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Player;
using Vision.World;

namespace Vision.Tests
{
    /// <summary>
    /// Dragging a slot off the bar drops its whole stack; the items' slot names; the galaxy gas trap's and Penjamin's
    /// sizes; and rocks the body can't climb (an upright wall round each, no slope a body walks up).
    /// </summary>
    public class InventoryAndPropsTests
    {
        MatchState saved;

        [SetUp]
        public void SaveState()
        {
            saved = MatchState.Current;
            MatchState.Current = new MatchState();
        }

        [TearDown]
        public void RestoreState() => MatchState.Current = saved;

        [Test]
        public void DraggingASlotOffTheBar_DropsItsWholeStack()
        {
            var rig = new SimRig(1, 1);
            SimPlayer s = rig.P(2);
            rig.Place(s, 0f, 0f);
            for (int i = 0; i < 3; i++) rig.Sim.AddItem(s, ItemType.Bottle);
            int slot = s.Inv.FirstSlotOf(ItemType.Bottle);
            Assert.AreEqual(3, s.Inv.CountAt(slot));
            rig.Sim.DropSlot(s.Id, slot);
            Assert.AreEqual(0, s.Inv.CountAt(slot), "the whole stack");
            Assert.AreEqual(3, rig.Sim.Drops.Count(d => d.Item == ItemType.Bottle), "on the ground");
            rig.Sim.DropSlot(rig.P(1).Id, 0);
            Assert.AreEqual(3, rig.Sim.Drops.Count, "Zach has no inventory to drop");
        }

        [Test]
        public void TheBeam_RunsOffTheScreen_AtEveryFacing_FromAnywhereOnIt_HoweverTheViewIsTurned()
        {
            var go = new GameObject("Beam Camera");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 7.2f * Vision.World.WorldScale.S;
                cam.aspect = 16f / 10f;
                float half = 50f * Mathf.Deg2Rad;
                int checks = 0;
                foreach (float yaw in new[] { 0f, 45f, 90f, 135f, 200f, 315f })
                {
                    go.transform.SetPositionAndRotation(Quaternion.Euler(55f, yaw, 0f) * new Vector3(0f, 0f, -60f), Quaternion.Euler(55f, yaw, 0f));
                    Vector2 focus = Vision.Rendering.VisionMaskRenderer.GroundFocus(cam);
                    foreach (Vector2 off in new[] { Vector2.zero, new Vector2(4f, -3f), new Vector2(-6f, 5f), new Vector2(9f, 2f) })
                    {
                        Vector2 origin = focus + off;
                        for (float deg = 0f; deg < 360f; deg += 5f)
                        {
                            float dir = deg * Mathf.Deg2Rad;
                            float reach = Vision.Rendering.VisionMaskRenderer.ConeReach(cam, origin, dir, half);
                            for (int i = 0; i <= 60; i++)
                            {
                                float a = dir - half + 2f * half * i / 60f;
                                float exit = Vision.Rendering.VisionMaskRenderer.BeamReach(cam, origin, new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                                Assert.GreaterOrEqual(reach, exit, $"yaw {yaw}, from {off}, facing {deg}: every ray of the cone runs off the screen");
                                checks++;
                            }
                        }
                    }
                }
                Assert.Greater(checks, 10000);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ClimbingOverSomething_NoItemsGunsOrAbilities()
        {
            var rig = new SimRig(1, 1);
            rig.Geo.Lows.Add((SimRig.U(100, -80), SimRig.U(100, 80), Scale.D(30f)));
            SimPlayer s = rig.P(2), zach = rig.P(1);
            rig.Place(s, 0f, 0f);
            rig.Place(zach, 0f, -2000f);
            rig.Sim.AddItem(s, ItemType.Bottle);
            int slot = s.Inv.FirstSlotOf(ItemType.Bottle) + 1;
            InputCmd Walk(Btn b) => new InputCmd { Move = Vector2.right, Buttons = b, Aim = 0f, AimDist = 100f, Item = slot };
            for (int i = 0; i < SimRig.Secs(3f) && !s.Move.Climbing; i++) rig.Run(1, p => p.Id == s.Id ? Walk(Btn.None) : (InputCmd?)null);
            Assert.IsTrue(s.Move.Climbing, "on the log");
            rig.Run(1, p => p.Id == s.Id ? Walk(Btn.Primary) : (InputCmd?)null);
            Assert.AreEqual(0, rig.Sim.Thrown.Count, "nothing thrown while climbing");

            rig.Place(s, 0f, -2000f);
            rig.Place(zach, 0f, 0f);
            for (int i = 0; i < SimRig.Secs(3f) && !zach.Move.Climbing; i++) rig.Run(1, p => p.Id == zach.Id ? Walk(Btn.None) : (InputCmd?)null);
            Assert.IsTrue(zach.Move.Climbing);
            rig.Run(1, p => p.Id == zach.Id ? Walk(Btn.Primary) : (InputCmd?)null);
            Assert.Less(zach.ChargeT, 0f, "no swing started while climbing");
            int charges = zach.VapeCharges;
            rig.Run(1, p => p.Id == zach.Id ? Walk(Btn.Vape) : (InputCmd?)null);
            Assert.AreEqual(0, rig.Sim.Vapes.Count, "no Penjamin");
            Assert.AreEqual(charges, zach.VapeCharges);
        }

        [Test]
        public void SlotNames()
        {
            Assert.AreEqual("Grapes of Wrath", Items.Short(ItemType.Book));
            Assert.AreEqual("Galaxy gas trap", Items.Short(ItemType.Trap));
            Assert.AreEqual("Mr Beast bar", Items.Short(ItemType.MrBeastBar));
            Assert.AreEqual("Night vision goggles", Items.Short(ItemType.Goggles));
            Assert.AreEqual("Legendary pump", Items.Name(ItemType.Shotgun, true));
        }

        [Test]
        public void GasSizes()
        {
            Assert.AreEqual(Balance.HunterWidth * 5f * 0.2f, Balance.Items.Trap.TriggerRadius, 1e-4f, "the trap's trigger 80% smaller");
            Assert.AreEqual(Balance.HunterWidth * 10f * 0.65f * 0.85f * 0.85f, Balance.Items.Trap.GasRadius, 1e-4f, "its cloud 15% smaller, and 15% smaller again");
            Assert.AreEqual(10f * 0.8f * 0.6f * 0.7f * 1.5f, Balance.Hunter.Vape.HalfAngleDeg, 1e-4f, "Penjamin 30% narrower again, then 50% wider");
        }

        [Test]
        public void Rocks_StandBehindUprightWalls_TheBodyCantClimb()
        {
            var root = new GameObject("World");
            var mat = new Material(Shader.Find("Vision/LowPoly"));
            try
            {
                var world = root.AddComponent<SandboxWorld>();
                world.lowPolyMaterial = world.entityMaterial = world.glowMaterial = mat;
                world.Generate();
                var rocks = root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Rock") && t.name != "Rock Wall" && t.GetComponent<MeshFilter>() != null).ToList();
                var walls = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Rock Wall").ToList();
                Assert.Greater(rocks.Count, 0);
                Assert.AreEqual(rocks.Count, walls.Count, "a wall round every rock");
                foreach (Transform r in rocks)
                    Assert.IsFalse(r.GetComponents<Collider>().Any(c => c.enabled), "the rock's own tilted collider is off");
                foreach (Transform w in walls)
                {
                    var c = w.GetComponent<MeshCollider>();
                    Assert.IsTrue(c != null && c.convex && c.enabled);
                    Assert.Less(Vector3.Angle(world.transform.up, w.up), 0.01f, "standing straight up");
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
