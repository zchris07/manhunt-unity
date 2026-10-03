using NUnit.Framework;
using UnityEngine;
using Vision.Characters;
using Vision.World;

namespace Vision.Tests
{
    public class MannequinTests
    {
        static float Length(Bone a, Bone b) => (HumanoidSkeleton.BindPosition(a) - HumanoidSkeleton.BindPosition(b)).magnitude;

        [Test]
        public void Skeleton_FollowsAnthropometricProportions()
        {
            const float H = HumanoidSkeleton.Height;
            Assert.AreEqual(0.53f * H, HumanoidSkeleton.BindPosition(Bone.ThighR).y, 0.01f, "hip height");
            Assert.AreEqual(0.245f * H, Length(Bone.ThighR, Bone.ShinR), 0.01f, "thigh");
            Assert.AreEqual(0.246f * H, Length(Bone.ShinR, Bone.FootR), 0.01f, "shank");
            Assert.AreEqual(0.186f * H, Length(Bone.UpperArmR, Bone.ForearmR), 0.01f, "upper arm");
            Assert.AreEqual(0.146f * H, Length(Bone.ForearmR, Bone.HandR), 0.01f, "forearm");
            // Glenohumeral joint centre sits ~3 cm below the acromion (0.818 H).
            Assert.AreEqual(0.80f * H, HumanoidSkeleton.BindPosition(Bone.UpperArmR).y, 0.02f, "shoulder joint height");
            Assert.AreEqual(-HumanoidSkeleton.BindPosition(Bone.HandR).x, HumanoidSkeleton.BindPosition(Bone.HandL).x, 1e-5f, "symmetric");
        }

        /// <summary>Closest point on a bone: the segments from its joint to each child joint (or the joint itself).</summary>
        static Vector3 ClosestOnBone(Vector3 p, Bone bone)
        {
            Vector3 a = HumanoidSkeleton.BindPosition(bone);
            Vector3 best = a;
            for (int i = 0; i < HumanoidSkeleton.BoneCount; i++)
            {
                if (i == (int)bone || HumanoidSkeleton.Parents[i] != bone) continue;
                Vector3 ab = HumanoidSkeleton.BindPosition((Bone)i) - a;
                Vector3 q = a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
                if ((p - q).sqrMagnitude < (p - best).sqrMagnitude) best = q;
            }
            return best;
        }

        [Test]
        public void Mannequin_IsLowPolyAndWithinItsTriangleCap()
        {
            Mesh m = MannequinBuilder.Build();
            int tris = m.triangles.Length / 3;
            Assert.LessOrEqual(tris, MannequinBuilder.MaxTriangles);
            Assert.Greater(tris, 150, "enough facets to read as a figure");
            Assert.AreEqual(tris * 3, m.vertexCount, "flat shaded: every triangle has its own vertices");
            MannequinBuilder.Measure(out int measured, out _);
            Assert.AreEqual(tris, measured);
            Object.DestroyImmediate(m);
        }

        [Test]
        public void Mannequin_IsOneDarkGreyWithNoOtherMaterials()
        {
            Mesh m = MannequinBuilder.Build();
            Assert.AreEqual(1, m.subMeshCount);
            Color expected = MannequinBuilder.Grey.linear;
            foreach (Color c in m.colors) Assert.AreEqual(expected, c);
            Color.RGBToHSV(MannequinBuilder.Grey, out _, out float saturation, out float value);
            Assert.Less(saturation, 0.05f, "grey");
            Assert.That(value, Is.InRange(0.15f, 0.4f), "dark");
            Object.DestroyImmediate(m);
        }

        [Test]
        public void Mannequin_IsPersonSizedAndCorrectlySkinned()
        {
            Mesh m = MannequinBuilder.Build();
            Assert.AreEqual(HumanoidSkeleton.BoneCount, m.bindposes.Length);
            Assert.AreEqual(HumanoidSkeleton.Height, m.bounds.max.y, HumanoidSkeleton.Height * 0.02f, "1.8 m tall");
            Assert.GreaterOrEqual(m.bounds.min.y, -0.001f, "nothing below the soles");
            Assert.Greater(m.bounds.max.z, 0.18f, "feet point forward (+Z)");

            Vector3[] v = m.vertices;
            Vector3[] n = m.normals;
            BoneWeight[] w = m.boneWeights;
            int far = 0, inward = 0;
            for (int i = 0; i < v.Length; i++)
            {
                float sum = w[i].weight0 + w[i].weight1 + w[i].weight2 + w[i].weight3;
                Assert.AreEqual(1f, sum, 1e-3f, "weights sum to one");
                if ((v[i] - ClosestOnBone(v[i], (Bone)w[i].boneIndex0)).magnitude > 0.25f) far++;
            }
            int[] t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 centroid = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f;
                if (Vector3.Dot(n[t[i]], centroid - ClosestOnBone(centroid, (Bone)w[t[i]].boneIndex0)) < 0f) inward++;
            }
            Assert.AreEqual(0, far, "every vertex is bound to a nearby bone");
            Assert.Less(inward, t.Length / 3 / 10, "faces point away from the bones they wrap");
            Object.DestroyImmediate(m);
        }

        [Test]
        public void Shoulders_AreClosedAndJoinedToTheTorso()
        {
            Mesh m = MannequinBuilder.Build();
            int holes = 0;
            foreach (var (a, b) in MannequinBuilder.OpenEdges(m))
            {
                Vector3 mid = (a + b) * 0.5f;
                if (mid.y > 1.3f && Mathf.Abs(mid.x) > 0.1f) holes++;
            }
            Assert.AreEqual(0, holes, "no open edges around the shoulders");
            Object.DestroyImmediate(m);
        }

        [Test]
        public void Mannequin_IsSymmetric()
        {
            Mesh m = MannequinBuilder.Build();
            Assert.AreEqual(-m.bounds.min.x, m.bounds.max.x, 1e-3f);
            Assert.AreEqual(0f, m.bounds.center.x, 1e-3f);
            Object.DestroyImmediate(m);
        }

        [Test]
        public void Characters_ShareTheMannequin_AndCastNoRealShadows()
        {
            var material = new Material(Shader.Find("Vision/LowPoly"));
            Mesh mesh = MannequinBuilder.Build();
            GameObject player = PropFactory.CreatePlayer(material, mesh);
            GameObject wanderer = PropFactory.CreateWanderer(material, mesh);
            foreach (GameObject go in new[] { player, wanderer })
            {
                var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.AreSame(mesh, skin.sharedMesh);
                Assert.AreEqual(HumanoidSkeleton.BoneCount, skin.bones.Length);
                Assert.IsTrue(skin.enabled);
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, skin.shadowCastingMode);
                Assert.IsFalse(skin.receiveShadows);
                Assert.NotNull(go.GetComponent<CharacterShadow>(), "casts a flashlight shadow instead");
            }
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(wanderer);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void Animator_PlacesFeetOnTheirTargets()
        {
            var material = new Material(Shader.Find("Vision/LowPoly"));
            GameObject player = PropFactory.CreatePlayer(material);
            var animator = player.GetComponent<HumanoidAnimator>();
            Transform footL = animator.bones[(int)Bone.FootL], footR = animator.bones[(int)Bone.FootR];
            float worst = 0f, lowest = float.MaxValue;
            foreach (bool sprint in new[] { false, true })
            {
                float speed = sprint ? 2.6f : 1.6f;
                for (int i = 0; i < 240; i++)
                {
                    player.transform.position += Vector3.forward * (speed / 60f);
                    animator.Drive(Vector3.forward * speed, Vector2.up);
                    animator.Step(1f / 60f);
                    GaitPose pose = animator.Solver.Evaluate();
                    Transform body = animator.body;
                    foreach (var (foot, leg) in new[] { (footL, pose.Left), (footR, pose.Right) })
                    {
                        if (leg.Grounded) worst = Mathf.Max(worst, (foot.position - body.TransformPoint(leg.Ankle)).magnitude);
                        lowest = Mathf.Min(lowest, foot.position.y);
                    }
                }
            }
            Assert.Less(worst, 0.01f, "IK reaches grounded foot targets when walking and sprinting");
            Assert.Greater(lowest, HumanoidSkeleton.AnkleHeight * 0.5f, "ankles stay above the ground");
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(material);
        }
    }
}
