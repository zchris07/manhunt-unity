using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Game;
using Vision.Player;
using Vision.Visibility;
using Vision.World;

namespace Vision.Tests
{
    /// <summary>
    /// The level's fallen trees, the edge of the woods (the boundary rings and Zach's crossing), the guns' spread and the
    /// 0.50 cal's aim, gas traps set off by shots and throws, and where the supplies spawn.
    /// </summary>
    public class BoundaryAndCombatTests
    {
        static int Secs(float s) => SimRig.Secs(s);

        /// <summary>Runs ticks as SimRig.Run does, keeping every event the match raised.</summary>
        static List<GameEvent> RunCollect(SimRig rig, int ticks, Func<SimPlayer, InputCmd?> input = null)
        {
            var all = new List<GameEvent>();
            for (int t = 0; t < ticks; t++)
            {
                foreach (SimPlayer p in rig.Sim.Order.ToArray())
                {
                    InputCmd cmd = input?.Invoke(p) ?? new InputCmd { Aim = p.Facing, AimDist = 100f, Item = p.SelSlot + 1 };
                    rig.Sim.SubmitInput(p.Id, cmd);
                    Vector2 delta = Movement.Step(p.Move, p.Pos, cmd, rig.Sim.MoveContextFor(p, false), rig.Geo, MatchSim.TickDt, out Gait gait);
                    rig.Sim.SetPose(p.Id, p.Pos + delta, cmd.AimDist > 0f ? cmd.Aim : p.Facing, gait);
                }
                rig.Sim.Step();
                all.AddRange(rig.Sim.Events);
                rig.Sim.Events.Clear();
            }
            return all;
        }

        static bool Fed(List<GameEvent> events, string text) => events.Any(e => e.Kind == EventKind.Feed && e.Text == text);

        // ---------------------------------------------------------------- the edge of the woods

        [Test]
        public void PastTheRings_TheLightDiesOverThreeSeconds_ThenTheDarkBitesThroughTheShield_AndTakesThemAtZero()
        {
            var rig = new SimRig(1, 1);
            SimPlayer z = rig.P(1), s = rig.P(2);
            rig.Place(z, 0f, 0f);
            Assert.AreEqual(90f, rig.Sim.Ring1, 1e-4f, "the first ring is the playable radius");
            Assert.AreEqual(rig.Sim.Ring1 + 12f, rig.Sim.Ring2, 1e-4f);
            Assert.AreEqual(rig.Sim.Ring1 + 24f, rig.Sim.Ring3, 1e-4f);
            rig.Place(s, Scale.ToUnits(rig.Sim.Ring1) + 60f, 0f);
            rig.Run(2);
            Assert.AreEqual(1, s.Zone, "past the first ring");
            Assert.AreEqual(0f, s.ShutdownT, "the light only flickers here");
            s.Shield = 0.5f;
            rig.Place(s, Scale.ToUnits(rig.Sim.Ring2) + 60f, 0f);
            rig.Run(Secs(Balance.Boundary.ShutdownTime) - 2);
            Assert.AreEqual(2, s.Zone);
            Assert.AreEqual(1f, s.Hp, 1e-5f, "no harm while the light is dying");
            List<GameEvent> ev = RunCollect(rig, 3);
            Assert.AreEqual(1f - Balance.Boundary.TickDamage, s.Hp, 1e-4f, "fully dark: 20 health");
            Assert.AreEqual(0.5f, s.Shield, 1e-5f, "straight through the shield");
            Assert.AreEqual(1, s.BoundaryHits, "a red pulse with the bite");
            rig.Run(Secs(Balance.Boundary.TickEvery) + 1);
            Assert.AreEqual(1f - 2f * Balance.Boundary.TickDamage, s.Hp, 1e-3f, "every 3 s");
            ev = RunCollect(rig, Secs(Balance.Boundary.TickEvery) * 3 + 10);
            Assert.AreEqual(Health.Eliminated, s.Health, "gone at zero, without being downed");
            Assert.IsTrue(Fed(ev, "Survivor 1 wandered into the woods"));
        }

        [Test]
        public void ASurvivor_ReachingTheEndOfTheWorld_IsGone()
        {
            var rig = new SimRig(1, 1);
            SimPlayer s = rig.P(2);
            rig.Place(rig.P(1), 0f, 0f);
            rig.Place(s, 0f, Scale.ToUnits(rig.Sim.Ring3) + 10f);
            List<GameEvent> ev = RunCollect(rig, 1);
            Assert.AreEqual(Health.Eliminated, s.Health);
            Assert.IsTrue(Fed(ev, "Survivor 1 wandered into the woods"));
        }

        [Test]
        public void Zach_CrossesTheOuterRing_ToTheFarSide_ForTwenty_AtMostEveryTenSeconds_AndTheDarkNeverHurtsHim()
        {
            var rig = new SimRig(1, 1);
            SimPlayer z = rig.P(1);
            rig.Place(rig.P(2), 0f, 0f);
            rig.Place(z, -(Scale.ToUnits(rig.Sim.Ring3) + 5f), 0f);
            List<GameEvent> ev = RunCollect(rig, 1);
            Assert.Greater(z.Pos.x, rig.Sim.Ring3 - 2f, "out on the left, in on the right");
            Assert.AreEqual(0f, z.Pos.y, 1e-3f);
            Assert.AreEqual(1f - Balance.Boundary.CrossCost / Balance.Hunter.Health.Max, z.Hp, 1e-4f, "20 health");
            Assert.IsTrue(Fed(ev, "Zach crossed over"));
            Assert.AreEqual(Balance.Boundary.CrossLockout, z.CrossCd, 0.05f);
            // Straight back: locked out, the ring stops him like a wall.
            rig.Place(z, Scale.ToUnits(rig.Sim.Ring3) + 5f, 0f);
            rig.Run(1);
            Assert.AreEqual(rig.Sim.Ring3 - Balance.Boundary.WallSkin, z.Pos.magnitude, 0.01f, "stopped at the ring");
            Assert.Greater(z.Pos.x, 0f, "still on this side");
            // The dark never hurts him (he only heals as usual).
            float hp = z.Hp;
            rig.Run(Secs(Balance.Boundary.ShutdownTime + Balance.Boundary.TickEvery * 2f));
            Assert.GreaterOrEqual(z.Hp, hp);
            Assert.AreEqual(Balance.Boundary.ShutdownTime, z.ShutdownT, 1e-3f, "but his light dies too");
            // After the lockout, too hurt to cross: still a wall.
            rig.Run(Secs(Balance.Boundary.CrossLockout));
            z.Hp = Balance.Boundary.CrossCost / Balance.Hunter.Health.Max;
            rig.Place(z, Scale.ToUnits(rig.Sim.Ring3) + 5f, 0f);
            rig.Run(1);
            Assert.Greater(z.Pos.x, 0f, "20 health or less: no crossing");
        }

        [Test]
        public void PastTheFirstRing_VisionAidsStop_AJarvisEnds_AndZachsBeamWontFire()
        {
            var rig = new SimRig(1, 1, true);
            SimPlayer z = rig.P(1), s = rig.P(2);
            rig.Place(z, 0f, 0f);
            rig.Place(s, 300f, 0f);
            s.Jarvis = 1;
            rig.Tap(2, Btn.Ability);
            Assert.Greater(s.JarvisT, 0f, "JARVIS on inside the ring");
            rig.Place(s, Scale.ToUnits(rig.Sim.Ring1) + 60f, 0f);
            rig.Run(1);
            Assert.AreEqual(0f, s.JarvisT, "it ends as they cross");
            Assert.AreEqual(0f, rig.Sim.RevealT);
            s.Jarvis = 3;
            rig.Tap(2, Btn.Ability);
            Assert.AreEqual(0f, s.JarvisT, "and won't start out here");
            s.Inv.Add(ItemType.Goggles);
            int slot = s.Inv.FirstSlotOf(ItemType.Goggles) + 1;
            rig.Run(3, p => p.Id == 2 ? new InputCmd { Buttons = Btn.Primary, Item = slot, AimDist = 100f } : (InputCmd?)null);
            Assert.IsFalse(s.GogglesOn, "no night vision");
            z.Hemp = 2;
            rig.Tap(1, Btn.Ability);
            Assert.IsTrue(z.HempOn, "the Hemp Battery on inside");
            rig.Place(z, -(Scale.ToUnits(rig.Sim.Ring1) + 60f), 0f);
            rig.Run(1);
            Assert.IsFalse(z.HempOn, "off past the ring");
            rig.Tap(1, Btn.Ability);
            Assert.IsFalse(z.HempOn, "and can't be turned on");
            z.BeamCharges = 1;
            rig.Tap(1, Btn.Beam);
            Assert.AreEqual(0f, z.BeamT, "no Hemp Beam");
        }

        // ---------------------------------------------------------------- guns and traps

        static List<float> Shots(SimRig rig, SimPlayer s, ItemType gun, int shots, bool aiming, float aim)
        {
            var angles = new List<float>();
            for (int i = 0; i < shots; i++)
            {
                if (s.Inv.Count(gun) == 0) s.Inv.Add(gun);
                int slot = s.Inv.FirstSlotOf(gun) + 1;
                Btn held = aiming ? Btn.Lunge : Btn.None;
                // Aim (or not) for a moment, then fire.
                RunCollect(rig, 2, p => p.Id == s.Id ? new InputCmd { Buttons = held, Item = slot, Aim = aim, AimDist = 100f } : (InputCmd?)null);
                List<GameEvent> ev = RunCollect(rig, 1, p => p.Id == s.Id ? new InputCmd { Buttons = held | Btn.Primary, Item = slot, Aim = aim, AimDist = 100f } : (InputCmd?)null);
                foreach (GameEvent e in ev)
                    if (e.Kind == EventKind.Shot && e.A == s.Id) angles.Add(Mathf.DeltaAngle(aim * Mathf.Rad2Deg, e.F * Mathf.Rad2Deg));
                RunCollect(rig, Secs(1.3f), p => p.Id == s.Id ? new InputCmd { Buttons = held, Item = slot, Aim = aim, AimDist = 100f } : (InputCmd?)null);
            }
            return angles;
        }

        [Test]
        public void ThePistol_StraysUpToTwoAndAHalfDegrees_EachWay()
        {
            var rig = new SimRig(1, 1, true);
            rig.Place(rig.P(1), 1800f, 1800f);
            SimPlayer s = rig.P(2);
            rig.Place(s, 0f, 0f);
            List<float> a = Shots(rig, s, ItemType.Pistol, 24, false, 0.4f);
            Assert.AreEqual(24, a.Count);
            Assert.IsTrue(a.All(x => Mathf.Abs(x) <= Balance.Items.Pistol.SpreadDeg + 1e-3f), string.Join(", ", a));
            Assert.AreEqual(2.5f, Balance.Items.Pistol.SpreadDeg, 1e-5f);
            Assert.Greater(a.Max(x => Mathf.Abs(x)), 1f, "it does stray");
        }

        [Test]
        public void TheFiftyCal_AimedWithRightClick_IsExact_SlowsThemThirtyPercent_FromTheHipStraysUpToFiveDegrees()
        {
            var rig = new SimRig(1, 1, true);
            rig.Place(rig.P(1), 1800f, 1800f);
            SimPlayer s = rig.P(2);
            rig.Place(s, 0f, 0f);
            List<float> hip = Shots(rig, s, ItemType.Sniper, 12, false, 1.1f);
            Assert.AreEqual(12, hip.Count);
            Assert.IsTrue(hip.All(x => Mathf.Abs(x) <= Balance.Items.Sniper.HipSpreadDeg + 1e-3f), string.Join(", ", hip));
            Assert.Greater(hip.Max(x => Mathf.Abs(x)), 1f, "from the hip it strays");
            List<float> aimed = Shots(rig, s, ItemType.Sniper, 6, true, 1.1f);
            Assert.AreEqual(6, aimed.Count);
            Assert.IsTrue(aimed.All(x => Mathf.Abs(x) < 1e-3f), "aimed, it goes exactly where it points");
            // Aiming slows them 30%.
            s.Inv.Add(ItemType.Sniper);
            int slot = s.Inv.FirstSlotOf(ItemType.Sniper) + 1;
            float Walk(bool aim)
            {
                RunCollect(rig, Secs(0.5f), p => p.Id == s.Id ? new InputCmd { Buttons = aim ? Btn.Lunge : Btn.None, Item = slot, Move = Vector2.right, Aim = 0f, AimDist = 100f } : (InputCmd?)null);
                float x0 = s.Pos.x;
                RunCollect(rig, Secs(1f), p => p.Id == s.Id ? new InputCmd { Buttons = aim ? Btn.Lunge : Btn.None, Item = slot, Move = Vector2.right, Aim = 0f, AimDist = 100f } : (InputCmd?)null);
                return s.Pos.x - x0;
            }
            float free = Walk(false), aiming = Walk(true);
            Assert.IsTrue(s.Aiming);
            Assert.AreEqual(Balance.Items.Sniper.AimSlowMul, aiming / free, 0.03f, "30% slower while aiming");
        }

        [Test]
        public void AnArmedGasTrap_BurstsWhenShotOrHitByAThrownItem_ButNotBeforeItsArmed()
        {
            var rig = new SimRig(1, 1, true);
            rig.Place(rig.P(1), 1800f, 1800f);
            SimPlayer s = rig.P(2);
            rig.Place(s, 0f, 0f);
            Vector2 at = SimRig.U(200f, 0f);
            rig.Sim.Traps.Add(new Trap { Id = 900, Pos = at, Owner = 2, ArmT = 5f });
            Shots(rig, s, ItemType.Pistol, 1, false, 0f);
            Assert.AreEqual(1, rig.Sim.Traps.Count, "not yet armed: the shot passes over it");
            rig.Sim.Traps[0].ArmT = 0f;
            // A pistol round that strays could miss a small trap: aim a shotgun's spread of pellets at it instead.
            s.Inv.Add(ItemType.Shotgun);
            int slot = s.Inv.FirstSlotOf(ItemType.Shotgun) + 1;
            List<GameEvent> ev = RunCollect(rig, 1, p => p.Id == 2 ? new InputCmd { Buttons = Btn.Primary, Item = slot, Aim = 0f, AimDist = 100f } : (InputCmd?)null);
            Assert.AreEqual(0, rig.Sim.Traps.Count, "shot: it bursts");
            Assert.AreEqual(1, rig.Sim.Gases.Count);
            Assert.IsTrue(ev.Any(e => e.Kind == EventKind.Gas));
            // A thrown bottle.
            rig.Sim.Traps.Add(new Trap { Id = 901, Pos = SimRig.U(0f, 500f), Owner = 2, ArmT = 0f });
            rig.Sim.Thrown.Add(new ThrownItem { Id = 950, Pos = SimRig.U(0f, 60f), Dir = Vector2.up, Owner = 2, Item = ItemType.Bottle });
            rig.Run(Secs(1f));
            Assert.AreEqual(0, rig.Sim.Traps.Count, "hit by a bottle: it bursts");
            Assert.AreEqual(0, rig.Sim.Thrown.Count, "and the bottle lands there");
        }

        // ---------------------------------------------------------------- the level

        [Test]
        public void FallenTrees_FromTheBakedPropLibrary_AreLow_SeenAndVaultedOver_AndTheMapIsACircleWithNoWall()
        {
            // The game builds its level from the baked prop library, whose copies are named after their slot
            // (DeadTree_33...), not "Fallen Tree": they must still be found and made low.
            var lib = UnityEditor.AssetDatabase.LoadAssetAtPath<PropLibrary>("Assets/Vision/Prefabs/PropLibrary.asset");
            Assert.IsNotNull(lib, "the baked prop library");
            var root = new GameObject("World");
            var mat = new Material(Shader.Find("Vision/LowPoly"));
            try
            {
                var world = root.AddComponent<SandboxWorld>();
                world.lowPolyMaterial = world.entityMaterial = world.glowMaterial = mat;
                world.library = lib;
                world.Generate();
                // The library's fallen-tree slots (their meshes are named after the slot, not "Fallen").
                var fallenMeshes = new HashSet<Mesh>();
                for (int size = 0; size < 3; size++)
                    fallenMeshes.Add(lib.trees[(int)LowPolyModels.DeadTreeKind.Fallen * 3 + size].GetComponent<MeshFilter>().sharedMesh);
                int fallen = 0;
                foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null || !fallenMeshes.Contains(mf.sharedMesh)) continue;
                    var occ = mf.GetComponentInParent<Occluder>();
                    if (occ == null) continue;
                    fallen++;
                    Assert.IsTrue(occ.low, $"{occ.name}: seen over");
                    Assert.AreEqual(SandboxWorld.LowLayer, occ.gameObject.layer, $"{occ.name}: on the low layer (vaulted, shot over)");
                }
                Assert.Greater(fallen, 0, "the map has fallen trees");
                Assert.AreEqual(world.Layout.Logs.Count + fallen, world.Lows.Count, "every log and fallen tree is a low obstacle");

                // A circle: no wall round it, the woods going on into the dark to the end of the world.
                Assert.IsFalse(root.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Stone Wall")), "no wall round the map");
                Assert.AreEqual(world.halfExtent + 24f, world.WorldRadius, 1e-4f);
                bool beyondRing = false;
                foreach (var (p, _, _) in world.MapTrees)
                {
                    Assert.LessOrEqual(p.magnitude, world.WorldRadius, "nothing past the end of the world");
                    if (p.magnitude > world.halfExtent + 2f) beyondRing = true;
                }
                Assert.IsTrue(beyondRing, "the woods go on past the playable ring");

                // Supplies: none within 10 m of the first ring; a quarter out in the woods, 20 m or more from any path;
                // the rest where the original puts them (on the paths' sides now 2-12 m out).
                var outdoors = new List<Vector2>();
                foreach (Pickup p in world.Pickups)
                {
                    Vector3 lp = world.transform.InverseTransformPoint(p.transform.position);
                    var at = new Vector2(lp.x, lp.z);
                    if (p.transform.parent != null && p.transform.parent.name != "Supplies") continue;
                    Assert.LessOrEqual(at.magnitude, world.halfExtent - SandboxWorld.PickupEdgeMargin + 0.01f, $"{p.name} keeps 10 m inside the first ring");
                    outdoors.Add(at);
                }
                int wild = outdoors.Count(p => world.Terrain.PathClearance(p.x, p.y) >= SandboxWorld.WildFromPaths - 0.01f);
                int all = Items.All.Sum(t => Items.Info(t).mapCount);
                Assert.GreaterOrEqual(wild, Mathf.RoundToInt(all * SandboxWorld.WildShare), "a quarter out in the woods, away from the paths");
                // Spread over the map: some in each quarter.
                for (int q = 0; q < 4; q++)
                    Assert.IsTrue(outdoors.Any(p => (p.x >= 0f) == (q % 2 == 0) && (p.y >= 0f) == (q < 2)), $"quarter {q}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(mat);
            }
        }
    }
}
