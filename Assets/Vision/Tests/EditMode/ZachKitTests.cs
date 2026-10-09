using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Effects;
using Vision.Game;
using Vision.UI;
using Vision.World;

namespace Vision.Tests
{
    /// <summary>Zach's kit against the original's tests (abilities.test.ts, penjamin.test.ts), and its effects.</summary>
    public class ZachKitTests
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

        static SimRig Duel(float dist)
        {
            var rig = new SimRig(2, 1);
            rig.Place(rig.P(1), 0f, 0f);
            rig.Place(rig.P(2), dist, 0f);
            rig.Place(rig.P(3), 2500f, 2500f);
            rig.P(1).Facing = 0f;
            return rig;
        }

        [Test]
        public void Lunge_TwoCharges_AFastDash_OneBackEverySevenSeconds()
        {
            SimRig rig = Duel(-2000f);
            SimPlayer h = rig.P(1);
            float x0 = h.Pos.x;
            rig.Tap(1, Btn.Lunge, 0f);
            rig.Run(Secs(Balance.Hunter.Lunge.Duration));
            Assert.Greater(h.Pos.x - x0, Scale.D(Movement.DashDistance(Balance.Hunter.Lunge.Peak, Balance.Hunter.Lunge.Duration)) * Scale.GlobalMove * 0.8f, "a fast dash");
            Assert.AreEqual(1, h.Move.LungeCharges);
            rig.Run(2);
            rig.Tap(1, Btn.Lunge, Mathf.PI);
            Assert.AreEqual(0, h.Move.LungeCharges);
            rig.Run(Secs(Balance.Hunter.Lunge.Duration));
            float x1 = h.Pos.x;
            rig.Run(2);
            rig.Tap(1, Btn.Lunge, 0f);
            rig.Run(5);
            Assert.AreEqual(x1, h.Pos.x, Scale.D(2f), "no charges: no lunge");
            rig.Run(Secs(Balance.Hunter.Lunge.Recharge - 0.7f));
            Assert.AreEqual(1, h.Move.LungeCharges);
            rig.Run(Secs(Balance.Hunter.Lunge.Recharge));
            Assert.AreEqual(2, h.Move.LungeCharges);
        }

        [Test]
        public void Lunge_OnlyHasToTouchASurvivor()
        {
            SimRig rig = Duel(220f * Scale.GlobalMove);
            rig.Tap(1, Btn.Lunge, 0f);
            rig.Run(Secs(Balance.Hunter.Lunge.Duration));
            Assert.AreEqual(Health.Wounded, rig.P(2).Health);
            Assert.AreEqual(0f, rig.P(1).Move.LungeT, "the lunge stops on contact");
        }

        [Test]
        public void TheBurst_FliesThroughEverything_AndScaresOnlyWhoItPasses()
        {
            SimRig rig = Duel(-3000f);
            SimPlayer h = rig.P(1), s = rig.P(2), mate = rig.P(3);
            rig.Place(mate, -1500f, Balance.Hunter.Burst.Width);
            rig.Tap(1, Btn.Secondary, Mathf.PI);
            Assert.Greater(h.BurstCd, Balance.Hunter.Burst.Cooldown - 0.2f);
            rig.Run(Secs(2800f / Balance.Hunter.Burst.Speed - 0.1f));
            Assert.AreEqual(0f, s.ScareT);
            rig.Run(Secs(0.3f));
            Assert.Greater(s.ScareT, 0f, "scared as it passes");
            rig.Run(Secs(2.5f));
            Assert.AreEqual(0f, mate.ScareT, "not the one off to the side");
            int waves = rig.Sim.Bursts.Count;
            rig.Tap(1, Btn.Secondary, Mathf.PI);
            Assert.AreEqual(waves, rig.Sim.Bursts.Count, "on cooldown");
        }

        [Test]
        public void TheHempBattery_TenSecondsOfUse_FortyToRefill_LockedWhenDrainedDry()
        {
            SimRig rig = Duel(3000f);
            SimPlayer h = rig.P(1);
            h.Hemp = 1;
            Assert.AreEqual(Balance.Hunter.Hemp.Duration, h.HempLeft);
            rig.Tap(1, Btn.Ability);
            Assert.IsTrue(h.HempOn);
            rig.Run(Secs(3f));
            Assert.AreEqual(7f, h.HempLeft, 0.5f);
            rig.Tap(1, Btn.Ability);
            Assert.IsFalse(h.HempOn);
            float low = h.HempLeft;
            rig.Run(Secs(4f));
            Assert.AreEqual(4f * Balance.Hunter.Hemp.Duration / Balance.Hunter.Hemp.Recover, h.HempLeft - low, 0.3f, "refills while off");
            rig.Tap(1, Btn.Ability);
            rig.Run(Secs(Balance.Hunter.Hemp.Duration + 0.5f));
            Assert.IsFalse(h.HempOn, "off by itself when dry");
            Assert.Greater(h.HempLock, 0f);
            rig.Tap(1, Btn.Ability);
            Assert.IsFalse(h.HempOn, "locked");
            rig.Run(Secs(Balance.Hunter.Hemp.Lockout));
            rig.Tap(1, Btn.Ability);
            Assert.IsTrue(h.HempOn);
        }

        [Test]
        public void Penjamin_TwoCharges_AndCloseUp_ItSlowsHurtsAndDazes()
        {
            SimRig rig = Duel(60f);
            SimPlayer h = rig.P(1), s = rig.P(2);
            rig.Tap(1, Btn.Vape, 0f);
            Assert.AreEqual(1, rig.Sim.Vapes.Count);
            Assert.AreEqual(1, h.VapeCharges);
            Assert.Greater(h.VapeCd, Balance.Hunter.Vape.Cooldown - 0.2f);
            rig.Run(Secs(1f));
            Assert.Greater(s.VapeT, 0f, "dizzy");
            Assert.Greater(s.VapeSlow, 0.18f);
            Assert.That(1f - s.Hp, Is.InRange(0.04f, 0.07f), "5% of health a second close up");
            rig.Run(2);
            rig.Tap(1, Btn.Vape, 0f);
            Assert.AreEqual(0, h.VapeCharges);
            rig.Run(2);
            rig.Tap(1, Btn.Vape, 0f);
            Assert.AreEqual(2, rig.Sim.Vapes.Count, "no third cloud");
        }

        [Test]
        public void TheGoldenPump_TakesTheMachetesPlace_UntilItsShotsAreSpent()
        {
            SimRig rig = Duel(3000f);
            SimPlayer h = rig.P(1);
            h.Pump = 2;
            rig.Tap(1, Btn.Primary, 0f);
            Assert.AreEqual(1, h.Pump, "a shot");
            Assert.AreEqual(-1f, h.ChargeT, "no machete while he has it");
            rig.Run(Secs(Balance.Items.ZachPump.Reload) + 2);
            rig.Tap(1, Btn.Primary, 0f);
            Assert.AreEqual(0, h.Pump);
        }

        [Test]
        public void TheEffects_DrawTheSwingTheBurstAndTheShots()
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
                host.SwitchRole();
                SimPlayer z = host.Local;
                var fxGo = new GameObject("Effects");
                var effects = fxGo.AddComponent<MatchEffects>();
                effects.world = world;
                effects.OnEvent(new GameEvent { Kind = EventKind.Swing, A = z.Id, B = 2, F = 1f });
                Assert.AreEqual(1, effects.ArcCount, "the swing's smear");
                effects.OnEvent(new GameEvent { Kind = EventKind.Talk, A = z.Id, Text = "burst", Pos = z.Pos, F = 0f });
                Assert.AreEqual(1, effects.WaveCount, "the Burst's wave");
                effects.OnEvent(new GameEvent { Kind = EventKind.Shot, A = z.Id, B = (int)Vision.Player.ItemType.Shotgun, Pos = z.Pos, F = 0f, G = 1f, Text = "hit|0:300,30:280,-30:290" });
                Assert.AreEqual(3, effects.TracerCount, "a tracer per pellet");
                Assert.Greater(Vfx.Instance.ParticleCount, 0, "and a muzzle flash");
                z.ChargeT = 0.5f;
                z.HempOn = true;
                effects.Draw();
                Vfx.Instance.Step(0.016f, null);
                int verts = Vfx.Instance.GetComponentsInChildren<MeshFilter>().Sum(m => m.sharedMesh.vertexCount);
                Assert.Greater(verts, 100, "charge ring, battery disc, the wave's lens");
                Object.DestroyImmediate(fxGo);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mat);
            }
        }
    }
}
