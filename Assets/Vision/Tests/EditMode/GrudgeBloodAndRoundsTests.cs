using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Characters;
using Vision.Effects;
using Vision.Game;
using Vision.Player;
using Vision.UI;
using Vision.World;
using Plan = Vision.World.BuildingPlan;

namespace Vision.Tests
{
    /// <summary>
    /// Chacko's attackers hunted with no time limit (Jaden and Plasma until they're down, Shane until he's shaken off),
    /// Zach's sprint cut off deep in a full charge, his reeling stun, blood dripping at random and not piling up under
    /// someone standing still, its splatters lying on the ground, rounds flying over the terrain, the gas blur's length,
    /// and a building without ducts or overhead pipes.
    /// </summary>
    public class GrudgeBloodAndRoundsTests
    {
        MatchState saved;

        [SetUp]
        public void SaveState() => saved = MatchState.Current;

        [TearDown]
        public void RestoreState() => MatchState.Current = saved;

        static int Secs(float s) => SimRig.Secs(s);

        static T Add<T>(SimRig rig, T n, float x, float y) where T : Npc
        {
            n.Pos = SimRig.U(x, y);
            rig.Sim.Npcs.Add(n);
            return n;
        }

        [Test]
        public void ChackosAttacker_IsHuntedWithNoTimeLimit_JadenAndPlasmaUntilDown_ShaneUntilShakenOff()
        {
            var rig = new SimRig(2, 1);
            SimPlayer s = rig.P(2), other = rig.P(3), h = rig.P(1);
            rig.Place(h, 2000f, 2000f);
            rig.Place(other, -2000f, 2000f);
            var chacko = new Chacko(rig.Sim);
            rig.Sim.Npcs.Add(chacko);
            rig.Sim.Teleport(s.Id, chacko.Pos + SimRig.U(300f, 0f));
            var jaden = Add(rig, new Jaden(rig.Sim), 0f, 0f);
            var plasma = Add(rig, new Plasma(rig.Sim), 0f, 0f);
            var shane = Add(rig, new Shane(rig.Sim), 0f, 0f);
            jaden.Pos = s.Pos + SimRig.U(250f, 0f);
            plasma.Pos = s.Pos + SimRig.U(-250f, 0f);
            shane.Pos = s.Pos + SimRig.U(0f, 250f);

            chacko.ItemHit(rig.Sim, s, "bottle");
            foreach (Npc n in new Npc[] { jaden, plasma, shane }) Assert.AreEqual(s.Id, n.GrudgeTarget, $"{n.Name} goes after Chacko's attacker");
            Assert.IsTrue(jaden.Chasing && shane.Chasing && plasma.Target == s.Id);

            Assert.AreEqual(s.Id, rig.Sim.Vengeance);
            Assert.IsTrue(plasma.Raging);

            // Nobody else draws Jaden off; Plasma slain doesn't call the others off.
            jaden.ItemHit(rig.Sim, other, "bottle");
            Assert.AreEqual(s.Id, jaden.Target, "Jaden stays on Chacko's attacker");
            rig.Run(Secs(Balance.Plasma.TransformTime + 0.2f));
            plasma.Snipe(rig.Sim, other);
            Assert.IsFalse(plasma.Alive);
            rig.Run(2);
            Assert.AreEqual(s.Id, jaden.GrudgeTarget, "Plasma's death doesn't end Jaden's grudge");
            Assert.AreEqual(s.Id, shane.GrudgeTarget, "or Shane's");

            // Down: Jaden lets them be; Shane keeps on.
            rig.Sim.HurtSurvivor(s, 5f, null, "test");
            Assert.AreEqual(Health.Downed, s.Health);
            rig.Run(2);
            Assert.AreEqual(0, jaden.GrudgeTarget, "Jaden's grudge ends when they're down");
            Assert.AreEqual(s.Id, shane.GrudgeTarget, "Shane's doesn't");
            Assert.IsTrue(shane.Chasing && shane.Target == s.Id, "he keeps tailing them");

            // Two bottles stun and drive him off: that's the end of it.
            shane.ItemHit(rig.Sim, other, "bottle");
            shane.ItemHit(rig.Sim, other, "bottle");
            Assert.AreEqual(0, shane.GrudgeTarget);
            Assert.IsTrue((shane.Flags & NpcFlags.Fleeing) != 0, "shaken off, he runs");
            rig.Run(2);
            Assert.AreEqual(0, rig.Sim.Vengeance, "nobody is after them now");
        }

        [Test]
        public void Zach_CantSprint_PastHalfAFullCharge_ButCanStillLunge()
        {
            var rig = new SimRig(1, 1);
            SimPlayer z = rig.P(1);
            rig.Place(rig.P(2), 2500f, 0f);
            InputCmd? Charge(SimPlayer p, Btn extra = Btn.None) => p.Id == 1 ? new InputCmd { Move = Vector2.right, Buttons = Btn.Primary | Btn.Run | extra, Aim = 0f, AimDist = 100f } : (InputCmd?)null;
            float half = Balance.Hunter.Attack.ChargeMax * 0.5f;
            rig.Run(Secs(half - 0.2f), p => Charge(p));
            Assert.GreaterOrEqual(z.ChargeT, 0f, "charging");
            Assert.IsTrue(z.Move.Sprinting, "sprinting early in the charge");
            rig.Run(Secs(0.4f), p => Charge(p));
            Assert.Greater(z.ChargeT, half);
            Assert.IsFalse(z.Move.Sprinting, "no sprint past half a full charge");
            int charges = z.Move.LungeCharges;
            rig.Run(1, p => Charge(p, Btn.Lunge));
            Assert.AreEqual(charges - 1, z.Move.LungeCharges, "the lunge still goes");
            Assert.Greater(z.Move.LungeT, 0f);
        }

        [Test]
        public void ZachStunned_ReelsOnTheSpot()
        {
            GameObject go = PropFactory.CreateCharacter("Lab", null, null);
            try
            {
                var layer = go.GetComponent<ActionLayer>();
                var view = go.GetComponent<CharacterView>();
                layer.Wire();
                view.SetSpec(CharacterSpec.Zach());
                var z = new SimPlayer(2, "Z", Role.Hunter, Vector2.zero) { StunT = 1.4f };
                view.Present(z);
                Assert.AreSame(ActionClips.Reel, layer.Current, "stunned, he reels");
                foreach (Bone b in new[] { Bone.ThighL, Bone.ThighR, Bone.ShinL, Bone.ShinR, Bone.Spine, Bone.Head, Bone.UpperArmR, Bone.UpperArmL })
                    Assert.IsTrue(ActionClips.Reel.Owns(b), $"his whole body is in it ({b})");
                Assert.IsTrue(ActionClips.Reel.UsesDrop, "on buckling knees");
                Assert.Greater(Quaternion.Angle(Quaternion.Euler(ActionClips.Reel.Sample(Bone.Head, 0f)), Quaternion.Euler(ActionClips.Reel.Sample(Bone.Head, 0.9f))), 20f, "the head lolls round");
                Assert.Greater(Mathf.Abs(ActionClips.Reel.Sample(Bone.ShinR, 0.45f).x - ActionClips.Reel.Sample(Bone.ShinR, 1.35f).x), 15f, "one knee gives, then the other");
                z.StunT = 0f;
                view.Present(z);
                Assert.AreNotSame(ActionClips.Reel, layer.Current);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Blood_DripsAtRandomMoments_AndStopsPilingUpUnderSomeoneStandingStill()
        {
            var rig = new SimRig(1, 1);
            SimPlayer s = rig.P(2);
            rig.Place(rig.P(1), 2000f, 2000f);
            rig.Sim.HurtSurvivor(s, 0.4f, null, "test");
            Assert.AreEqual(Health.Wounded, s.Health);
            rig.Run(Secs(8f));
            List<TrailPoint> drops = rig.Sim.Trails.Where(t => t.Kind == 1 && t.Who == s.Id).OrderBy(t => t.T).ToList();
            Assert.AreEqual(Balance.Trails.BloodPoolMax, drops.Count, "standing still, only a few drops land under them");
            foreach (TrailPoint t in drops) Assert.LessOrEqual(Vector2.Distance(t.Pos, s.Pos), Scale.D(Balance.Trails.BloodSpread) + 1e-4f, "about where they stand");
            var gaps = new List<float>();
            for (int i = 1; i < drops.Count; i++) gaps.Add(drops[i].T - drops[i - 1].T);
            Assert.Greater(gaps.Max() - gaps.Min(), 0.08f, "at random moments, not like clockwork");
            foreach (float g in gaps) Assert.That(g, Is.InRange(Balance.Trails.BloodEveryMin - 0.04f, Balance.Trails.BloodEveryMax + 0.04f));

            // Moving on, they drip again.
            float walked = rig.Sim.Time;
            rig.Run(Secs(4f), p => p.Id == 2 ? new InputCmd { Move = Vector2.right, Aim = 0f, AimDist = 100f } : (InputCmd?)null);
            Assert.GreaterOrEqual(rig.Sim.Trails.Count(t => t.Kind == 1 && t.Who == s.Id && t.T > walked), 5, "a trail as they walk");
        }

        [Test]
        public void TheScentTrail_KnowsHowFarAlongItEachPointIs_ForItsRepeatingSwell()
        {
            var rig = new SimRig(1, 1);
            SimPlayer s = rig.P(2);
            rig.Place(rig.P(1), 2000f, 2000f);
            rig.Run(Secs(3f), p => p.Id == 2 ? new InputCmd { Move = Vector2.right, Buttons = Btn.Run, Aim = 0f, AimDist = 100f } : (InputCmd?)null);
            List<TrailPoint> scent = rig.Sim.Trails.Where(t => t.Kind == 0 && t.Who == s.Id).OrderBy(t => t.Id).ToList();
            Assert.Greater(scent.Count, 10);
            for (int i = 1; i < scent.Count; i++)
                Assert.AreEqual(Vector2.Distance(scent[i].Pos, scent[i - 1].Pos), scent[i].Dist - scent[i - 1].Dist, 1e-3f, "measured along the trail");
            Assert.AreEqual(2f, Balance.Trails.ScentWaveSprintSec, "peak to valley: two seconds of sprinting");
        }

        [Test]
        public void TheGasBlur_LastsASecondLonger()
        {
            Assert.AreEqual(Balance.Hunter.Vape.AfterTime + 1f, Balance.Hunter.Vape.BlurTime, 1e-5f, "a survivor's blur from Penjamin");
            Assert.AreEqual(3f, Balance.Items.Trap.BlurAfter, 1e-5f, "Zach's after the galaxy gas: three seconds, not two");
            Assert.Greater(Vision.Rendering.VisionComposite.GasBlurPx, 9f, "and stronger");
        }

        [Test]
        public void TheBuilding_HasNoDuctsPipesOrVents_OverTheDoorsAndWindows()
        {
            foreach (int seed in new[] { 1, 2, 3, 7, 42, 99 })
            {
                var p = new Plan(seed, new Rect(-18f, -18f, 36f, 36f));
                Assert.Greater(p.Items.Count, 100);
                foreach (Plan.Item i in p.Items)
                    if (i.Elevation > 2.05f) Assert.AreEqual(Plan.Furn.Cobweb, i.Kind, $"seed {seed}: nothing overhead but cobwebs in the corners ({i.Kind})");
            }
            string[] names = System.Enum.GetNames(typeof(Plan.Furn));
            foreach (string gone in new[] { "Duct", "Pipes", "VentGrille" }) Assert.IsFalse(names.Contains(gone), $"no {gone} in the game");
        }

        [Test]
        public void LinesDrawnOverTheTerrain_AreCoveredOnlyByWhatStandsUpright_NeverByTheGroundOrWhatsBuriedInIt()
        {
            var made = new List<GameObject>();
            try
            {
                var camGo = new GameObject("Cover Camera");
                made.Add(camGo);
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 20f;
                camGo.transform.SetPositionAndRotation(new Vector3(0f, 30f, -30f), Quaternion.Euler(45f, 0f, 0f));
                Vector3 back = -camGo.transform.forward;
                GameObject Box(Vector3 at, Vector3 size, int layer)
                {
                    var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    g.transform.position = at;
                    g.transform.localScale = size;
                    g.layer = layer;
                    made.Add(g);
                    return g;
                }
                // A hill of ground with a tree's buried foot in it, right in front of a point of the line under the hill.
                Box(new Vector3(0f, -5f, 0f), new Vector3(40f, 10f, 40f), SandboxWorld.GroundLayer);
                Vector3 under = new Vector3(0f, -3f, 0f);
                Box(under + back * 1.5f, new Vector3(0.6f, 0.6f, 0.6f), 0);
                Physics.SyncTransforms();
                Assert.IsTrue(Vfx.Covered(cam, under), "judged from where it is, the buried foot covers it");
                Assert.IsFalse(Vfx.Covered(cam, under, 0.5f), "lifted to just over the ground, nothing stands in front of it");
                Assert.IsTrue(Vfx.UnderGround(cam, under), "the rise would hide it: it's drawn over the terrain");
                // A wall standing on the ground in front of it does cover it; a fallen log lying there doesn't.
                Vector3 lifted = under + back * ((0.5f - under.y) / back.y);
                Box(lifted + back * 3f, new Vector3(4f, 3f, 0.4f), 12);
                Physics.SyncTransforms();
                Assert.IsFalse(Vfx.Covered(cam, under, 0.5f), "a fallen log never covers it");
                Box(lifted + back * 3f, new Vector3(4f, 3f, 0.4f), 0);
                Physics.SyncTransforms();
                Assert.IsTrue(Vfx.Covered(cam, under, 0.5f), "something upright in front does");
            }
            finally
            {
                foreach (GameObject g in made) Object.DestroyImmediate(g);
            }
        }

        [Test]
        public void TheScentIsJoinedInTheOrderItWasLaid_SoRunningThenWalkingNeverCrossesItself()
        {
            // Running east for five seconds, then walking on east: the walking points are stamped four seconds older
            // (they fade sooner), so by time they fall among the running ones, somewhere else.
            var pts = new List<TrailPoint>();
            int id = 0;
            float dist = 0f;
            for (float t = 0f; t < 5f; t += 0.12f, dist += 0.6f) pts.Add(new TrailPoint { Id = id++, Pos = new Vector2(dist, 0f), T = t, Who = 1, Dist = dist });
            for (float t = 5f; t < 8f; t += 0.12f, dist += 0.2f) pts.Add(new TrailPoint { Id = id++, Pos = new Vector2(dist, 0f), T = t - Balance.Trails.WalkHeadStart, Who = 1, Dist = dist });
            var byTime = pts.OrderBy(p => p.T).ToList();
            Assert.IsTrue(Enumerable.Range(1, byTime.Count - 1).Any(i => byTime[i].Pos.x < byTime[i - 1].Pos.x), "joined by time it doubled back on itself");
            var shuffled = pts.OrderBy(p => (p.Id * 7919) % 101).ToList();
            MatchPresenter.OrderTrail(shuffled);
            for (int i = 1; i < shuffled.Count; i++)
                Assert.Greater(shuffled[i].Pos.x, shuffled[i - 1].Pos.x, "joined in the order it was laid: straight on, never back");
        }

        [Test]
        public void RoundsFlyLevel_AndSplattersLieOnTheGround()
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
                SimPlayer me = host.Local;

                // Blood: tiny splatters of different shapes, every corner on the drawn ground.
                var splats = new BloodSplats();
                splats.Begin();
                var rng = new System.Random(5);
                var sizes = new HashSet<int>();
                for (int id = 1; id <= 24; id++)
                {
                    Vector2 at = me.Pos + new Vector2((float)rng.NextDouble() * 40f - 20f, (float)rng.NextDouble() * 40f - 20f);
                    splats.Draw(world, id, at, 0.2f);
                    IReadOnlyList<Vector3> corners = splats.Corners(id);
                    Assert.Greater(corners.Count, 8);
                    sizes.Add(corners.Count);
                    foreach (Vector3 w in corners)
                    {
                        Vector3 local = world.transform.InverseTransformPoint(w);
                        var xz = new Vector2(local.x, local.z);
                        Assert.AreEqual(world.SurfaceHeight(xz) + 0.006f, local.y, 1e-3f, "on the ground's facets");
                        Assert.Less(Vector2.Distance(xz, at), 0.2f, "a miniature splatter, a few centimetres across");
                    }
                }
                splats.End();
                Assert.AreEqual(24, splats.Count);
                Assert.GreaterOrEqual(sizes.Count, 4, "splatters of different shapes");
                splats.Begin();
                splats.End();
                Assert.AreEqual(0, splats.Count, "forgotten once their drops are gone");

                // A round's path: perfectly straight and level at the barrel's height, whatever the ground does below.
                var fxGo = new GameObject("Effects");
                var effects = fxGo.AddComponent<MatchEffects>();
                effects.world = world;
                try
                {
                    effects.OnEvent(new GameEvent { Kind = EventKind.Shot, A = me.Id, B = (int)ItemType.Pistol, Pos = me.Pos, F = 0.3f, G = Scale.D(2000f) });
                    Assert.AreEqual(1, effects.RoundCount, "the round in flight");
                    var (a0, a1) = effects.RoundLine(0);
                    Assert.AreEqual(a0.y, a1.y, 1e-4f, "level: the same height all the way");
                    Assert.Greater(Vector3.Distance(a0, a1), 10f, "out to where it stopped");
                }
                finally { Object.DestroyImmediate(fxGo); }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mat);
            }
        }
    }
}
