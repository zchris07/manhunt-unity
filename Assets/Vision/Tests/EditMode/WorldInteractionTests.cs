using NUnit.Framework;
using UnityEngine;
using Vision.Characters;
using Vision.Effects;
using Vision.Game;
using Vision.World;

namespace Vision.Tests
{
    public class WorldInteractionTests
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
            var fx = Object.FindAnyObjectByType<Vfx>();
            if (fx != null) Object.DestroyImmediate(fx.gameObject);
        }

        static int Secs(float s) => SimRig.Secs(s);

        /// <summary>A match on open ground with a pallet, a door and a window in a line east of the origin.</summary>
        static (MatchSim sim, OpenGeometry geo) Arena()
        {
            var map = new SimMap { HalfExtent = 90f };
            map.Generators.Add(SimRig.U(2000, 2000));
            map.Barricades.Add(new SimMap.BarricadeDef { A = SimRig.U(100, -40), B = SimRig.U(100, 40), Pos = SimRig.U(100, 0) });
            map.Doors.Add(new SimMap.DoorDef { A = SimRig.U(100, 960), B = SimRig.U(100, 1040), StartsOpen = false });
            map.Windows.Add(new SimMap.WindowDef { A = SimRig.U(100, 1960), B = SimRig.U(100, 2040) });
            map.SurvivorSpawns.Add(SimRig.U(-2000, -2000));
            map.HunterSpawns.Add(SimRig.U(2000, -2000));
            var geo = new OpenGeometry();
            return (new MatchSim(map, geo, MatchRules.Resolve(2, 1), false, 5), geo);
        }

        static void Run(MatchSim sim, int ticks, int id = 0, Btn b = Btn.None, float aim = 0f)
        {
            for (int t = 0; t < ticks; t++)
            {
                foreach (SimPlayer p in sim.Order)
                    sim.SubmitInput(p.Id, p.Id == id ? new InputCmd { Buttons = b, Aim = aim, AimDist = 100f } : new InputCmd { Aim = p.Facing, AimDist = 100f });
                sim.Step();
                sim.Events.Clear();
            }
        }

        static void Swipe(MatchSim sim, int id)
        {
            Run(sim, 1, id, Btn.Primary, 0f);
            Run(sim, Secs(Balance.Hunter.Attack.SwingTime + Balance.Hunter.Attack.Windup) + 3, id, Btn.None, 0f);
        }

        [Test]
        public void APallet_SlammedOnZach_StunsHim_AndTwoHitsBreakIt()
        {
            var (sim, _) = Arena();
            SimPlayer zach = sim.AddPlayer(1, "Zach", Role.Hunter, SimRig.U(100, 0));
            SimPlayer s = sim.AddPlayer(2, "S", Role.Survivor, SimRig.U(40, 0));
            sim.AddPlayer(3, "T", Role.Survivor, SimRig.U(-2000, -2000));
            Run(sim, 2);
            Assert.AreEqual(Prompt.DropBarricade, s.Prompt2, "Space: slam the pallet");
            Run(sim, 1, 2, Btn.Space);
            Assert.AreEqual(BarricadeState.Down, sim.Barricades[0]);
            Assert.Greater(zach.StunT, 0f, "Zach was under it: stunned");
            Run(sim, Secs(zach.StunT + Balance.Items.StunImmunity) + 2);
            sim.Teleport(2, SimRig.U(-1500, -1500));
            sim.Teleport(1, SimRig.U(40, 0));
            zach.Facing = 0f;
            Swipe(sim, 1);
            Assert.AreEqual(BarricadeState.Down, sim.Barricades[0], "one hit isn't enough");
            Swipe(sim, 1);
            Assert.AreEqual(BarricadeState.Broken, sim.Barricades[0], "two hits break it");
        }

        [Test]
        public void Zach_BreaksADoorWithTwoSwipes_AndSmashesAWindow()
        {
            var (sim, geo) = Arena();
            SimPlayer zach = sim.AddPlayer(1, "Zach", Role.Hunter, SimRig.U(40, 1000));
            sim.AddPlayer(2, "S", Role.Survivor, SimRig.U(-2000, -2000));
            sim.AddPlayer(3, "T", Role.Survivor, SimRig.U(-2000, -1900));
            zach.Facing = 0f;
            Swipe(sim, 1);
            Assert.IsFalse(sim.DoorBroken[0]);
            Swipe(sim, 1);
            Assert.IsTrue(sim.DoorBroken[0], "a door gives after two swipes");
            sim.Teleport(1, SimRig.U(40, 2000));
            zach.Facing = 0f;
            Swipe(sim, 1);
            Assert.IsTrue(sim.WindowsBroken[0], "the glass goes at once");
        }

        [Test]
        public void Crouching_DropsTheHips_AndBendsTheKnees_FeetStayDown()
        {
            GameObject go = PropFactory.CreateCharacter("C", null, null);
            try
            {
                var anim = go.GetComponent<HumanoidAnimator>();
                for (int i = 0; i < 60; i++) anim.Step(1f / 60f);
                Transform pelvis = anim.bones[(int)Bone.Pelvis], shin = anim.bones[(int)Bone.ShinL], foot = anim.bones[(int)Bone.FootL];
                float standing = pelvis.position.y;
                anim.Crouch = 1f;
                for (int i = 0; i < 60; i++) anim.Step(1f / 60f);
                Assert.AreEqual(standing - 0.27f, pelvis.position.y, 0.03f, "the hips drop");
                Assert.Greater(Vector3.Angle(anim.bones[(int)Bone.ThighL].up, shin.up), 25f, "the knees bend");
                Assert.AreEqual(HumanoidSkeleton.AnkleHeight, foot.position.y, 0.03f, "the feet stay on the ground");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Effects_BurstRingAndRibbon_AreDrawnAndExpire()
        {
            Assert.NotNull(Shader.Find("Vision/Fx"), "the effects shader is in the project");
            Material masked = Vfx.MakeMaterial(Vfx.Blend.Additive, true);
            Assert.IsTrue(masked.IsKeywordEnabled("_VISION_MASKED"), "masked effects hide outside the viewer's light");
            Object.DestroyImmediate(masked);
            Vfx fx = Vfx.Instance;
            fx.Burst(Vector3.zero, 20, 3f, 0.5f, Color.red);
            fx.RingAt(Vector3.zero, 0.1f, 1f, 1f, Color.white, 0.05f, true);
            fx.Ribbon(new[] { Vector3.zero, Vector3.right, Vector3.right * 2f }, new[] { Color.red, Color.red, Color.red }, 0.2f);
            Assert.AreEqual(20, fx.ParticleCount);
            Assert.AreEqual(1, fx.RingCount);
            fx.Step(0.1f, null);
            var meshes = fx.GetComponentsInChildren<MeshFilter>();
            int verts = 0, senses = 0;
            foreach (MeshFilter mf in meshes)
            {
                verts += mf.sharedMesh.vertexCount;
                if (mf.gameObject.layer == Vfx.SensesLayer && mf.sharedMesh.vertexCount > 0) senses++;
            }
            Assert.Greater(verts, 20 * 4, "particles, the ring and the ribbon");
            Assert.AreEqual(1, senses, "the ring is on the senses layer, drawn through the dark");
            fx.Step(1.2f, null);
            Assert.AreEqual(0, fx.ParticleCount, "particles fade away");
            Assert.AreEqual(0, fx.RingCount);
        }

        [Test]
        public void TheView_SlamsPallets_PushesDoors_AndCrouches()
        {
            GameObject go = PropFactory.CreateCharacter("V", null, null);
            try
            {
                var view = go.GetComponent<CharacterView>();
                var layer = go.GetComponent<ActionLayer>();
                var p = new SimPlayer(4, "S", Role.Survivor, Vector2.zero);
                view.OnEvent(new GameEvent { Kind = EventKind.Talk, A = 4, Text = "slam" }, p, null);
                Assert.AreSame(ActionClips.Slam, layer.Current);
                view.OnEvent(new GameEvent { Kind = EventKind.Talk, A = 4, Text = "door" }, p, null);
                Assert.AreSame(ActionClips.Push, layer.Current);
                p.Crouching = true;
                view.Present(p);
                Assert.AreEqual(1f, view.Animator.Crouch);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
