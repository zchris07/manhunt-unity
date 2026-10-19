using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Audio;
using Vision.Game;
using Vision.Player;

namespace Vision.Tests
{
    /// <summary>
    /// Soham slain in a single hit (blowing up as his fuse would, leaving three jars of piss); piss on Zach lasting fifteen
    /// seconds, falling off linearly and stacking without limit, with his yellow tint following it; the "Pissed on" testing
    /// effect; and the generator's sound starting with the victory jingle, 30% louder.
    /// </summary>
    public class SohamPissAndSoundsTests
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

        static Soham AddSoham(SimRig rig, float x, float y)
        {
            var n = new Soham(rig.Sim) { Pos = SimRig.U(x, y) };
            rig.Sim.Npcs.Add(n);
            return n;
        }

        [Test]
        public void Soham_DiesToAnyOneHit_ExplodingLikeHisFuse_AndDropsThreeJarsOfPiss()
        {
            foreach (string how in new[] { "machete", "bottle", "shot", "beam", "vape" })
            {
                var rig = new SimRig();
                SimPlayer zach = rig.P(1), s = rig.P(2);
                rig.Place(zach, 2000f, 2000f);
                rig.Place(s, 2000f, -2000f);
                Soham soham = AddSoham(rig, 0f, 0f);
                rig.Run(1);
                rig.Sim.Events.Clear();
                int drops = rig.Sim.Drops.Count;
                switch (how)
                {
                    case "machete": soham.Slash(rig.Sim, zach, 1); break;
                    case "beam": soham.BeamHit(rig.Sim, zach); break;
                    case "vape": soham.Slash(rig.Sim, zach, 1); break;
                    default: soham.ItemHit(rig.Sim, s, how); break;
                }
                Assert.IsFalse(soham.Alive, how + ": one hit slays him");
                Assert.IsTrue(soham.Gone, "blown apart: no body left");
                Assert.AreEqual(1, rig.Sim.Events.Count(e => e.Kind == EventKind.Explosion), how + ": he explodes, as his fuse would have him");
                var jars = rig.Sim.Drops.Skip(drops).ToList();
                Assert.AreEqual(Balance.Soham.PissDrops, jars.Count, "three jars");
                Assert.AreEqual(3, Balance.Soham.PissDrops);
                foreach (DropItem d in jars)
                {
                    Assert.AreEqual(ItemType.Piss, d.Item);
                    Assert.Less(Vector2.Distance(d.Pos, soham.Pos), Scale.D(40f), "where he stood");
                }
                // Hit again: nothing more.
                soham.Slash(rig.Sim, zach, 2);
                soham.ItemHit(rig.Sim, s, "shot");
                Assert.AreEqual(1, rig.Sim.Events.Count(e => e.Kind == EventKind.Explosion));
                Assert.AreEqual(drops + 3, rig.Sim.Drops.Count);
            }
            // Penjamin's first touch is only a scare: it doesn't slay him.
            var r2 = new SimRig();
            Soham calm = AddSoham(r2, 0f, 0f);
            calm.Provoke(r2.Sim, r2.P(1));
            Assert.IsTrue(calm.Alive);
        }

        [Test]
        public void ThePissOnZach_Lasts15s_FallingOffLinearly_AndStacksWithoutLimit()
        {
            Assert.AreEqual(15f, Balance.Items.Piss.Time);
            var rig = new SimRig();
            SimPlayer zach = rig.P(1);
            rig.Place(rig.P(2), 2000f, 2000f);
            rig.Sim.SoakInPiss(zach);
            Assert.AreEqual(0.5f, MatchSim.PissBonus(zach), 1e-4f, "a fresh splash: +50%");
            Assert.AreEqual(1f, PissLook.Amount(MatchSim.PissBonus(zach)), 1e-4f, "full yellow");
            rig.Run(SimRig.Secs(7.5f));
            Assert.AreEqual(0.25f, MatchSim.PissBonus(zach), 0.01f, "half way: half of it");
            Assert.AreEqual(0.5f, PissLook.Amount(MatchSim.PissBonus(zach)), 0.02f, "and half as yellow");
            // Another splash on top: the old one's half left plus a whole new one.
            rig.Sim.SoakInPiss(zach);
            Assert.AreEqual(0.75f, MatchSim.PissBonus(zach), 0.01f);
            Assert.AreEqual(2, zach.PissStacks.Length);
            for (int i = 0; i < 6; i++) rig.Sim.SoakInPiss(zach);
            Assert.AreEqual(0.75f + 6 * 0.5f, MatchSim.PissBonus(zach), 0.01f, "no limit");
            // The damage it adds.
            float before = zach.Hp;
            rig.Sim.HurtHunter(zach, 10f, null, "bottle");
            Assert.AreEqual(10f * (1f + 3.75f) / Balance.Hunter.Health.Max, before - zach.Hp, 0.003f);
            // The first wears off at fifteen seconds; the rest fifteen after they landed.
            rig.Run(SimRig.Secs(7.6f));
            Assert.AreEqual(7, zach.PissStacks.Length, "the first splash is gone");
            Assert.AreEqual(3.5f * (7.4f / 15f), MatchSim.PissBonus(zach), 0.02f, "the rest falling off together");
            rig.Run(SimRig.Secs(7.5f));
            Assert.AreEqual(0, zach.PissStacks.Length, "all gone");
            Assert.AreEqual(0f, zach.PissT);
            Assert.AreEqual(0f, MatchSim.PissBonus(zach));
            Assert.AreEqual(0f, PissLook.Amount(0f));
            before = zach.Hp;
            rig.Sim.HurtHunter(zach, 10f, null, "bottle");
            Assert.AreEqual(10f / Balance.Hunter.Health.Max, before - zach.Hp, 1e-4f, "no extra damage once it's worn off");
        }

        [Test]
        public void AThrownJarOfPiss_OnZach_Stacks_TheSameAsTheTestingEffect()
        {
            var rig = new SimRig(1, 1, true);
            SimPlayer zach = rig.P(1), s = rig.P(2);
            rig.Place(zach, 0f, 0f);
            rig.Place(s, -200f, 0f);
            rig.Run(1);
            for (int i = 0; i < 2; i++)
            {
                s.Inv.Clear();
                s.Inv.Add(ItemType.Piss, 1);
                int slot = s.Inv.FirstSlotOf(ItemType.Piss) + 1;
                rig.Run(1, p => p.Id == s.Id ? new InputCmd { Item = slot, AimDist = 100f } : (InputCmd?)null);
                rig.Tap(s.Id, Btn.Primary, 0f, slot);
                rig.Run(SimRig.Secs(1f));
            }
            Assert.AreEqual(2, zach.PissStacks.Length, "two jars, two splashes");
            Assert.Greater(MatchSim.PissBonus(zach), 0.9f);

            // Testing: "Pissed on", on Zach (stacking) and on a survivor (just the piss in the eyes).
            Assert.IsTrue(MatchSim.TestFxButtons.Any(b => b.fx == TestFx.Piss && b.label == "Pissed on"));
            rig.Sim.PlayTestFx(zach.Id, TestFx.Piss);
            Assert.AreEqual(3, zach.PissStacks.Length);
            Assert.AreEqual(Balance.Items.Piss.BlurTime, zach.PissBlurT, 1e-4f);
            rig.Sim.PlayTestFx(s.Id, TestFx.Piss);
            Assert.AreEqual(0, s.PissStacks.Length, "a survivor takes no extra damage");
            Assert.AreEqual(Balance.Items.Piss.BlurTime, s.PissBlurT, 1e-4f, "but gets it in the eyes");
        }

        [Test]
        public void ZachsYellowTint_FollowsThePiss_AndComesOffWithIt()
        {
            var go = new GameObject("Zach");
            var mat = new Material(Shader.Find("Vision/LowPoly"));
            try
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.transform.SetParent(go.transform, false);
                var r = body.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                var block = new MaterialPropertyBlock();
                PissLook.Set(go, 1f);
                r.GetPropertyBlock(block);
                Color full = block.GetColor("_Tint");
                Assert.AreEqual(PissLook.Yellow.b, full.b, 0.01f, "yellow");
                Assert.Greater(full.r, full.b + 0.5f);
                PissLook.Set(go, 0.5f);
                r.GetPropertyBlock(block);
                Assert.AreEqual(Color.Lerp(Color.white, PissLook.Yellow, 0.5f).b, block.GetColor("_Tint").b, 0.01f, "half as yellow");
                PissLook.Set(go, 0f);
                Assert.IsFalse(r.HasPropertyBlock(), "the tint comes off");
                Assert.AreSame(mat, r.sharedMaterial, "the material never touched");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(mat);
            }
        }

        [Test]
        public void TheGeneratorsSound_Starts_WithTheVictoryJingle_30PercentLouder()
        {
            SoundBank.Cue gen = SoundBank.Cues["generator_done"];
            Assert.AreEqual(0.55f * 1.3f, gen.Volume, 1e-4f, "30% louder");
            Assert.AreEqual(0.2f, gen.DecayTo, 1e-4f, "falling off to a fifth of that (so its end is 30% louder too)");
            Assert.Greater(gen.Start, 2f, "from the engine catching, past the file's silence and faint crank");
            Assert.IsTrue(gen.Warm, "decoded ahead, so it never waits on loading");
            Assert.IsTrue(SoundBank.Cues["victory_royale"].Warm);
        }
    }
}
