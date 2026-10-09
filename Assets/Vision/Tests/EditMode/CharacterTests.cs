using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Vision.Characters;
using Vision.Game;
using Vision.Player;
using Vision.World;

namespace Vision.Tests
{
    public class CharacterTests
    {
        static IEnumerable<CharacterSpec> Everyone()
        {
            yield return CharacterSpec.Survivor();
            yield return CharacterSpec.Zach();
            foreach (string n in Texts.NpcNames) yield return CharacterSpec.Npc(n);
        }

        [Test]
        public void EveryCharacter_IsLowPoly_AtMost1000Triangles_AndFullySkinned()
        {
            foreach (CharacterSpec s in Everyone())
            {
                Mesh m = CharacterBuilder.Build(s);
                try
                {
                    int tris = m.triangles.Length / 3;
                    Assert.LessOrEqual(tris, CharacterBuilder.MaxTriangles, s.Name);
                    Assert.Greater(tris, 800, $"{s.Name}: the finer body, near its 1000-triangle budget");
                    Assert.AreEqual(HumanoidSkeleton.BoneCount, m.bindposes.Length);
                    foreach (BoneWeight w in m.boneWeights)
                    {
                        Assert.AreEqual(1f, w.weight0 + w.weight1 + w.weight2 + w.weight3, 1e-4f, $"{s.Name}: every vertex fully weighted");
                        Assert.That(w.boneIndex0, Is.InRange(0, HumanoidSkeleton.BoneCount - 1));
                    }
                    Bounds b = m.bounds;
                    Assert.That(b.max.y, Is.InRange(1.78f, 1.9f), $"{s.Name}: the head's top near the rig's 1.8 m");
                    Assert.Less(b.min.y, 0.01f, "feet on the ground");
                }
                finally { Object.DestroyImmediate(m); }
            }
        }

        [Test]
        public void Survivor_IsGrey_Zach_IsJasonWithHisMask_AndNpcsDiffer()
        {
            Mesh survivor = CharacterBuilder.Build(CharacterSpec.Survivor());
            foreach (Color c in survivor.colors)
            {
                Color.RGBToHSV(c.gamma, out _, out float sat, out _);
                Assert.Less(sat, 0.05f, "the survivor stays grey");
            }
            var tones = survivor.colors.Select(c => Mathf.Round(c.gamma.r * 100f)).Distinct().Count();
            Assert.GreaterOrEqual(tones, 4, "tonal greys: skin, shirt, trousers, gloves, soles");

            CharacterSpec z = CharacterSpec.Zach();
            Assert.AreEqual(2.0f, z.Height, 1e-4f, "big: about 2 m");
            Assert.That(z.Shoulders, Is.InRange(1.05f, 1.2f), "broad, but not a wardrobe");
            Assert.Greater(z.Arms, 1.3f);
            Mesh zach = CharacterBuilder.Build(z);
            Color white = z.Accent.linear, red = z.Accent2.linear;
            Assert.IsTrue(zach.colors.Any(c => Close(c, white)), "a white hockey mask");
            Assert.IsTrue(zach.colors.Any(c => Close(c, red)), "with red chevrons");
            // The mask covers the face: its triangles sit in front of the head.
            int[] t = zach.triangles;
            Vector3[] v = zach.vertices;
            Color[] col = zach.colors;
            bool front = false;
            for (int i = 0; i < t.Length; i += 3)
                if (Close(col[t[i]], white) && v[t[i]].z > 0.06f && v[t[i]].y > 1.55f) front = true;
            Assert.IsTrue(front, "on the face");

            var looks = new HashSet<string>();
            foreach (string n in Texts.NpcNames)
            {
                Mesh m = CharacterBuilder.Build(CharacterSpec.Npc(n));
                looks.Add(string.Join(",", m.colors.Select(c => ColorUtility.ToHtmlStringRGB(c)).Distinct().OrderBy(x => x)));
                Object.DestroyImmediate(m);
            }
            Assert.AreEqual(Texts.NpcNames.Length, looks.Count, "every NPC looks different");
            Object.DestroyImmediate(survivor);
            Object.DestroyImmediate(zach);
        }

        static bool Close(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

        [Test]
        public void Props_AreSeparate_About120Triangles()
        {
            foreach (PropKind k in System.Enum.GetValues(typeof(PropKind)))
            {
                if (k == PropKind.None) continue;
                Mesh m = PropModels.Build(k);
                int tris = m.triangles.Length / 3;
                Assert.Greater(tris, 0, k.ToString());
                Assert.LessOrEqual(tris, PropModels.MaxTriangles, k.ToString());
                Assert.Less(m.bounds.size.magnitude, 1.4f, $"{k} fits in a hand");
                Object.DestroyImmediate(m);
            }
            Assert.AreEqual(PropKind.GoldenPump, PropModels.ForItem(ItemType.Shotgun, true));
            Assert.AreEqual(PropKind.Sniper, PropModels.ForItem(ItemType.Sniper, false));
        }

        // ---------------------------------------------------------------- the animation lab

        static GameObject NewCharacter(out ActionLayer layer, out CharacterView view)
        {
            GameObject go = PropFactory.CreateCharacter("Lab", null, null);
            layer = go.GetComponent<ActionLayer>();
            view = go.GetComponent<CharacterView>();
            layer.Wire();
            return go;
        }

        /// <summary>A throw or a light slash whips the arm faster than anything else (a real throwing arm passes 5000 deg/s).</summary>
        static bool Whip(ActionClip c) => c == ActionClips.Throw || c == ActionClips.Swing || c == ActionClips.SwingBack;

        static bool Fast(ActionClip c) => c.Name.StartsWith("Swing") || c.Name.StartsWith("Recoil") || c == ActionClips.Throw || c == ActionClips.Burst || c == ActionClips.Lunge || c == ActionClips.Slam || c == ActionClips.Push || c == ActionClips.Cringe;

        [Test]
        public void EveryClip_MovesWithoutPops_BlendsInAndOut_AndFiresEachEventOnce()
        {
            GameObject go = NewCharacter(out ActionLayer layer, out _);
            var animator = go.GetComponent<HumanoidAnimator>();
            try
            {
                const float dt = 1f / 240f;
                var problems = new List<string>();
                foreach (ActionClip clip in ActionClips.All)
                {
                    var fired = new Dictionary<string, int>();
                    void OnFire(ActionClip c, string n) { if (c == clip) fired[n] = fired.TryGetValue(n, out int k) ? k + 1 : 1; }
                    layer.EventFired += OnFire;
                    // Let the gait settle, play the clip once through (or one loop), then stop and blend out.
                    // Clips that always follow another (a recoil follows the aim, the staking follows the carry) start from it.
                    ActionClip prelude = clip == ActionClips.RecoilLong || clip == ActionClips.RecoilPump ? ActionClips.AimLong : clip == ActionClips.RecoilRifle ? ActionClips.AimRifle
                        : clip == ActionClips.RecoilPistol ? ActionClips.AimPistol : clip == ActionClips.StakeBody ? ActionClips.Carry : null;
                    if (prelude != null) layer.Play(prelude, 1f, true);
                    else layer.Stop();
                    for (int i = 0; i < 180; i++) { animator.Step(dt); layer.Step(dt); }
                    layer.Play(clip, 1f, true);
                    var last = new Quaternion[HumanoidSkeleton.BoneCount];
                    for (int i = 0; i < last.Length; i++) last[i] = layer.bones[i].localRotation;
                    float worst = 0f;
                    string worstBone = "";
                    int frames = Mathf.CeilToInt((clip.Length + clip.BlendOut + 0.3f) / dt);
                    for (int f = 0; f < frames; f++)
                    {
                        if (clip.Loop && f * dt >= clip.Length) layer.Stop();
                        animator.Step(dt);
                        layer.Step(dt);
                        for (int i = 0; i < 21; i++)   // the body bones (fingers follow the hand)
                        {
                            float a = Quaternion.Angle(last[i], layer.bones[i].localRotation);
                            if (a > worst) { worst = a; worstBone = ((Bone)i).ToString(); }
                            last[i] = layer.bones[i].localRotation;
                        }
                    }
                    layer.EventFired -= OnFire;
                    float limit = (Whip(clip) ? 3200f : Fast(clip) ? 2600f : 1000f) * dt;
                    if (worst >= limit) problems.Add($"{clip.Name}: {worstBone} moves {worst / dt:0} deg/s");
                    foreach (var (_, name) in clip.Events)
                        if ((fired.TryGetValue(name, out int n) ? n : 0) != 1) problems.Add($"{clip.Name}: \"{name}\" fired {n} times");
                    if (layer.Current != null) problems.Add($"{clip.Name} never ends");
                }
                Assert.IsEmpty(problems, string.Join("; ", problems));
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>Where the right and left hands end up (body space: x right, y up, z forward) in a clip's key pose.</summary>
        static (Vector3 r, Vector3 l, Vector3 propDir, Vector3 fingers) HandsIn(ActionClip clip, float t, PropKind prop) => HandsIn(clip, t, prop, out _);

        static (Vector3 r, Vector3 l, Vector3 propDir, Vector3 fingers) HandsIn(ActionClip clip, float t, PropKind prop, out Vector3 muzzle)
        {
            GameObject go = NewCharacter(out ActionLayer layer, out _);
            var animator = go.GetComponent<HumanoidAnimator>();
            try
            {
                if (prop != PropKind.None) layer.Hold(prop);
                layer.Play(clip, 0f, true, t);
                for (int i = 0; i < 120; i++) { animator.Step(0.01f); layer.Step(0.01f); }
                Transform root = go.transform;
                Vector3 r = root.InverseTransformPoint(layer.bones[(int)Bone.HandR].position);
                Vector3 l = root.InverseTransformPoint(layer.bones[(int)Bone.HandL].position);
                Transform pt = layer.PropTransform();
                Vector3 d = pt != null ? root.InverseTransformDirection(pt.forward) : Vector3.zero;
                // The fingers point along the hand bone (its -Y).
                Vector3 f = root.InverseTransformDirection(-layer.bones[(int)Bone.HandR].up);
                muzzle = layer.Muzzle(out Vector3 m) ? root.InverseTransformPoint(m) : Vector3.zero;
                return (r, l, d, f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void KeyPoses_PutTheHandsWhereTheActionNeedsThem()
        {
            var problems = new List<string>();
            void Check(string what, bool ok, Vector3 v) { if (!ok) problems.Add($"{what}: {v}"); }
            var lever = HandsIn(ActionClips.Lever, 0f, PropKind.None);
            Check("lever: hands forward and up", lever.r.z > 0.3f && lever.r.y > 1.3f && lever.l.z > 0.3f, lever.r);
            var push = HandsIn(ActionClips.Push, 0.18f, PropKind.None);
            Check("push: hand out in front", push.r.z > 0.45f && Mathf.Abs(push.r.x) < 0.35f, push.r);
            var aim = HandsIn(ActionClips.AimLong, 0f, PropKind.Shotgun, out Vector3 shotgunMuzzle);
            Check("shotgun: points straight ahead", aim.propDir.z > 0.95f, aim.propDir);
            Check("shotgun: shouldered on the right, not held out to the side", aim.r.x < 0.2f && aim.r.y > 1.25f && aim.r.z < 0.4f, aim.r);
            Check("shotgun: the support hand out under the forend", aim.l.z > aim.r.z + 0.1f && aim.l.x > -0.05f, aim.l);
            Check("shotgun: the round leaves from the barrel, well out in front", shotgunMuzzle.z > 0.7f, shotgunMuzzle);
            var rifle = HandsIn(ActionClips.AimRifle, 0f, PropKind.Sniper, out Vector3 rifleMuzzle);
            Check("0.50 cal: points straight ahead", rifle.propDir.z > 0.95f, rifle.propDir);
            Check("0.50 cal: the support hand far out along it", rifle.l.z > rifle.r.z + 0.18f, rifle.l);
            Check("0.50 cal: the muzzle a long way out", rifleMuzzle.z > 1f, rifleMuzzle);
            var pistol = HandsIn(ActionClips.AimPistol, 0f, PropKind.Pistol, out Vector3 pistolMuzzle);
            Check("pistol: points straight ahead", pistol.propDir.z > 0.95f, pistol.propDir);
            Check("pistol: out in front at chest height", pistol.r.z > 0.35f && pistol.r.y > 1.25f && Mathf.Abs(pistol.r.x) < 0.15f, pistol.r);
            Check("pistol: both hands on the grip (Weaver), not both arms out", (pistol.l - pistol.r).magnitude < 0.12f, pistol.l - pistol.r);
            Check("pistol: the muzzle past the hands", pistolMuzzle.z > pistol.r.z + 0.1f, pistolMuzzle);
            var drink = HandsIn(ActionClips.Drink, 0f, PropKind.Can);
            Check("drink: hand at the mouth", Mathf.Abs(drink.r.y - 1.58f) < 0.15f && drink.r.z > 0.02f && Mathf.Abs(drink.r.x) < 0.15f, drink.r);
            var repair = HandsIn(ActionClips.Repair, 0f, PropKind.None);
            Check("repair: hands low in front", repair.r.z > 0.2f && repair.r.y < 0.9f, repair.r);
            var throwRelease = HandsIn(ActionClips.Throw, 0.1f, PropKind.Bottle);
            Check("throw windup: hand up behind the shoulder", throwRelease.r.y > 1.5f && throwRelease.r.z < 0.15f, throwRelease.r);
            var staked = HandsIn(ActionClips.Staked, 0f, PropKind.None);
            Check("staked: hands overhead", staked.r.y > 1.75f && staked.l.y > 1.75f, staked.r);
            var stunned = HandsIn(ActionClips.Stunned, 0f, PropKind.None);
            Check("stunned: hands at the head", stunned.r.y > 1.45f && Mathf.Abs(stunned.r.x) < 0.25f, stunned.r);
            var charge = HandsIn(ActionClips.Charge, 0f, PropKind.Machete);
            Check("charge: machete raised over the shoulder", charge.r.y > 1.6f && charge.r.x > 0f, charge.r);
            var wind = HandsIn(ActionClips.Swing, 0.075f, PropKind.Machete);
            Check("forehand: drawn back out to his right", wind.r.x > 0.3f && wind.r.z < 0.25f, wind.r);
            var strike = HandsIn(ActionClips.Swing, 0.15f, PropKind.Machete);
            Check("forehand: the hand comes across in front at chest height", strike.r.z > 0.35f && strike.r.x < 0.25f && strike.r.y > 1.1f, strike.r);
            var follow = HandsIn(ActionClips.Swing, 0.24f, PropKind.Machete);
            Check("forehand: across to his left", follow.r.x < -0.2f, follow.r);
            Check("forehand: a flat slash (the hand stays level)", Mathf.Abs(follow.r.y - wind.r.y) < 0.2f && Mathf.Abs(strike.r.y - wind.r.y) < 0.2f, new Vector3(wind.r.y, strike.r.y, follow.r.y));
            var carry = HandsIn(ActionClips.Carry, 0f, PropKind.None);
            Check("carry: left hand up on the shoulder", carry.l.y > 1.45f && carry.l.x < 0f, carry.l);
            var beam = HandsIn(ActionClips.Beam, 0f, PropKind.None);
            Check("beam: the right arm thrust out front", beam.r.z > 0.5f && beam.r.y > 1.2f, beam.r);
            Check("beam: the palm faces forward (fingers up)", beam.fingers.y > 0.5f, beam.fingers);
            Check("beam: the left arm drawn back at the side", beam.l.z < 0.15f && beam.l.x < -0.1f, beam.l);
            var eyes = HandsIn(ActionClips.CoverEyes, 0f, PropKind.None);
            Check("Penjamin: right hand over the eyes", eyes.r.y > 1.45f && eyes.r.y < 1.72f && eyes.r.z > 0.04f && Mathf.Abs(eyes.r.x) < 0.16f, eyes.r);
            Check("Penjamin: left hand over the eyes", eyes.l.y > 1.45f && eyes.l.y < 1.72f && eyes.l.z > 0.04f && Mathf.Abs(eyes.l.x) < 0.16f, eyes.l);
            var back = HandsIn(ActionClips.SwingBack, 0.06f, PropKind.Machete);
            Check("backhand: starts off to his left, where the forehand ended", back.r.x < -0.2f && back.r.y > 1.2f, back.r);
            var backStrike = HandsIn(ActionClips.SwingBack, 0.195f, PropKind.Machete);
            Check("backhand: cut back across to his right", backStrike.r.x > 0.3f && backStrike.r.z > 0.2f, backStrike.r);
            Assert.IsEmpty(problems, string.Join("; ", problems));
        }

        [Test]
        public void TheBody_FacesTheAim_AtOnce_EvenMidAction_AndTheBeamLeavesTheLegsWalking()
        {
            GameObject go = NewCharacter(out ActionLayer layer, out _);
            var animator = go.GetComponent<HumanoidAnimator>();
            try
            {
                Vector3 ChestForward() => go.transform.InverseTransformDirection(layer.bones[(int)Bone.Chest].forward);
                animator.Drive(Vector3.zero, new Vector2(0f, 1f));
                for (int i = 0; i < 60; i++) { animator.Step(1f / 60f); layer.Step(1f / 60f); }
                // Swing the aim right round behind: on the very next frame the chest faces it.
                foreach (float deg in new[] { 170f, -95f, 60f })
                {
                    var aim = new Vector2(Mathf.Sin(deg * Mathf.Deg2Rad), Mathf.Cos(deg * Mathf.Deg2Rad));
                    animator.Drive(Vector3.zero, aim);
                    animator.Step(1f / 60f);
                    layer.Step(1f / 60f);
                    Vector3 f = ChestForward();
                    float err = Vector2.Angle(new Vector2(f.x, f.z), aim);
                    Assert.Less(err, 3f, $"chest faces an aim of {deg} deg the same frame");
                }
                // Mid-action too: the clip's torso turns on top of the aim (the strike faces the aim within its own windup).
                layer.Play(ActionClips.AimPistol, 1f, true);
                var walkAim = new Vector2(1f, 0f);
                animator.Drive(new Vector3(0f, 0f, 1f), walkAim);
                for (int i = 0; i < 40; i++) { animator.Step(1f / 60f); layer.Step(1f / 60f); }
                Vector3 c = ChestForward();
                Assert.Less(Vector2.Angle(new Vector2(c.x, c.z), walkAim), 14f, "aiming a pistol sideways while walking forward, the chest is on the aim");
            }
            finally { Object.DestroyImmediate(go); }

            foreach (Bone b in new[] { Bone.ThighL, Bone.ThighR, Bone.ShinL, Bone.ShinR, Bone.FootL, Bone.FootR })
                Assert.IsFalse(ActionClips.Beam.Owns(b), $"the beam leaves {b} to the walk");
            Assert.IsFalse(ActionClips.Beam.UsesDrop, "and doesn't crouch the walk");
            var p = new SimPlayer(1, "S", Role.Survivor, Vector2.zero) { Facing = 0f, BeamT = 1f, BeamAng = 1.2f };
            Assert.AreEqual(1.2f, p.BodyFacing, 1e-5f, "while beaming the body faces the beam");
            p.BeamT = 0f;
            Assert.AreEqual(0f, p.BodyFacing, 1e-5f);
        }

        [Test]
        public void TheView_PicksClipsAndProps_FromTheMatch()
        {
            GameObject go = NewCharacter(out ActionLayer layer, out CharacterView view);
            try
            {
                var p = new SimPlayer(1, "S", Role.Survivor, Vector2.zero);
                view.SetSpec(CharacterSpec.Survivor());
                view.Present(p);
                Assert.AreEqual(PropKind.Flashlight, layer.HeldRight, "a flashlight in hand");
                p.Action = ActionKind.Repair;
                view.Present(p);
                Assert.AreSame(ActionClips.Repair, layer.Current, "kneeling at the generator");
                p.Action = ActionKind.None;
                p.Inv.Add(ItemType.Shotgun);
                p.SelSlot = 0;
                view.Present(p);
                Assert.AreEqual(PropKind.Shotgun, layer.HeldRight);
                Assert.AreSame(ActionClips.AimLong, layer.Current);
                view.OnEvent(new GameEvent { Kind = EventKind.Shot, A = 1, B = (int)ItemType.Shotgun }, p, null);
                Assert.AreSame(ActionClips.RecoilLong, layer.Current, "the kick");
                for (int i = 0; i < 100; i++) layer.Step(0.01f);
                p.Inv.Add(ItemType.Sniper);
                p.SelSlot = 1;
                view.Present(p);
                Assert.AreEqual(PropKind.Sniper, layer.HeldRight);
                Assert.AreSame(ActionClips.AimRifle, layer.Current, "the 0.50 cal shouldered, cheek on the scope");
                p.SelSlot = -1;
                // Night vision: worn over the eyes, the arms left alone (the light stays in hand).
                p.Inv.Add(ItemType.Goggles);
                p.SelSlot = 2;
                p.GogglesOn = true;
                view.Present(p);
                Assert.AreEqual(PropKind.Goggles, layer.Worn, "the goggles on the face");
                Assert.AreEqual(PropKind.Flashlight, layer.HeldRight, "the light still in hand");
                Assert.IsNull(layer.Current, "no arm raised for the goggles");
                p.GogglesOn = false;
                view.Present(p);
                Assert.AreEqual(PropKind.None, layer.Worn);
                p.SelSlot = -1;
                p.Health = Game.Health.Downed;
                for (int i = 0; i < 100; i++) layer.Step(0.01f);
                view.Present(p);
                Assert.IsTrue(view.Animator.Prone);
                Assert.AreSame(ActionClips.Crawl, layer.Current);

                var z = new SimPlayer(2, "Z", Role.Hunter, Vector2.zero);
                view.SetSpec(CharacterSpec.Zach());
                Assert.AreEqual(2f / 1.8f, go.transform.localScale.x, 1e-4f, "Zach's size");
                view.Present(z);
                Assert.AreEqual(PropKind.Machete, layer.HeldRight, "his machete");
                z.ChargeT = 0.3f;
                view.Present(z);
                Assert.AreSame(ActionClips.Charge, layer.Current);
                view.OnEvent(new GameEvent { Kind = EventKind.Swing, A = 2, B = 2, F = -1f }, z, null);
                Assert.AreSame(ActionClips.SwingHeavy, layer.Current, "a heavy swing");
                Vector2 before = layer.Reaction;
                view.OnEvent(new GameEvent { Kind = EventKind.Hit, A = 2, F = 0.3f }, z, null);
                layer.Step(0.03f);
                Assert.Greater((layer.Reaction - before).magnitude, 0.5f, "a hit makes him flinch");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
