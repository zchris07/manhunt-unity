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

            // Hidden, they lose the survivor but not the grudge, however long it takes (the old limit was 30 s).
            s.HideState = 2;
            rig.Run(Secs(40f));
            Assert.IsFalse(jaden.Chasing || shane.Chasing, "nobody to chase while they're hidden");
            foreach (Npc n in new Npc[] { jaden, plasma, shane }) Assert.AreEqual(s.Id, n.GrudgeTarget, $"{n.Name} still holds the grudge after 40 s");
            Assert.AreEqual(s.Id, rig.Sim.Vengeance);
            s.HideState = 0;
            rig.Run(3);
            Assert.IsTrue(jaden.Chasing && jaden.Target == s.Id, "out again: Jaden's straight back on them");
            Assert.IsTrue(shane.Chasing && shane.Target == s.Id, "and Shane");
            Assert.AreEqual(s.Id, plasma.Target, "and Plasma");
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
        public void RoundsAndSplatters_FollowTheGround()
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

                // A round's path: over the ground at the barrel's height wherever it goes, never under it.
                var fxGo = new GameObject("Effects");
                var effects = fxGo.AddComponent<MatchEffects>();
                effects.world = world;
                try
                {
                    effects.OnEvent(new GameEvent { Kind = EventKind.Shot, A = me.Id, B = (int)ItemType.Pistol, Pos = me.Pos, F = 0.3f, G = Scale.D(2000f) });
                    Assert.AreEqual(1, effects.RoundCount, "the round in flight");
                    for (float s = 0f; s < 60f; s += 0.5f)
                    {
                        Vector2 at = me.Pos + new Vector2(Mathf.Cos(0.3f), Mathf.Sin(0.3f)) * s;
                        Vector3 p = world.transform.InverseTransformPoint(effects.FlightPoint(me.Pos, new Vector2(Mathf.Cos(0.3f), Mathf.Sin(0.3f)), 1.2f, s));
                        Assert.AreEqual(world.SurfaceHeight(at) + 1.2f, p.y, 1e-3f, "over the ground, at the barrel's height");
                    }
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
