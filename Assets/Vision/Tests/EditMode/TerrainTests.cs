using NUnit.Framework;
using UnityEngine;
using Vision.Characters;
using Vision.Player;
using Vision.World;

namespace Vision.Tests
{
    public class TerrainTests
    {
        static TerrainField Field()
        {
            var f = new TerrainField(1337, 20f);
            f.AddPad(new Rect(3.5f, 2.5f, 11f, 10f), 6f);
            f.AddPad(new Vector2(0f, -5f), 1.5f, 4f);
            return f;
        }

        [Test]
        public void Terrain_IsDeterministicAndContinuous()
        {
            TerrainField a = Field(), b = Field();
            for (float x = -20f; x <= 20f; x += 1.7f)
                for (float z = -20f; z <= 20f; z += 1.3f)
                {
                    Assert.AreEqual(a.Height(x, z), b.Height(x, z));
                    Assert.Less(Mathf.Abs(a.Height(x + 0.01f, z) - a.Height(x, z)), 0.02f, "no steps");
                }
            Assert.AreNotEqual(new TerrainField(1, 20f).Height(3f, 4f), new TerrainField(2, 20f).Height(3f, 4f), "seeded");
        }

        [Test]
        public void Terrain_HasPronouncedHillsAndDitches_WithinTheSlopeLimit()
        {
            TerrainField f = Field();
            float lo = float.MaxValue, hi = float.MinValue, maxSlope = 0f, sum = 0f;
            int n = 0;
            for (float x = -18f; x <= 18f; x += 0.25f)
                for (float z = -18f; z <= 18f; z += 0.25f)
                {
                    float h = f.Height(x, z), s = f.SlopeDeg(x, z);
                    lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
                    maxSlope = Mathf.Max(maxSlope, s); sum += s; n++;
                }
            Assert.Greater(hi - lo, 3f, "several metres of relief");
            Assert.Greater(sum / n, 8f, "slopes everywhere, not a flat field");
            Assert.LessOrEqual(maxSlope, 40f, "steepest slope stays walkable");
        }

        [Test]
        public void Pads_AreFlat_AndTheWallsStandOnLevelGround()
        {
            TerrainField f = Field();
            float cabin = f.Height(9f, 7.5f);
            for (float x = 3.6f; x < 14.4f; x += 0.5f)
                for (float z = 2.6f; z < 12.4f; z += 0.5f)
                    Assert.AreEqual(cabin, f.Height(x, z), 1e-4f, "cabin pad");
            for (float t = -20f; t <= 20f; t += 0.5f)
            {
                Assert.AreEqual(0f, f.Height(t, 19.5f), 1e-4f, "north wall");
                Assert.AreEqual(0f, f.Height(-19.5f, t), 1e-4f, "west wall");
            }
        }

        [Test]
        public void GroundMesh_MatchesTheField_AndNeighbouringChunksShareTheirBorder()
        {
            TerrainField f = Field();
            var grid = new LowPolyMeshBuilder.TerrainGrid(24f, 0.76f, f.Height, (x, z) => 1f, 0.28f, 1337);
            for (int j = 0; j <= grid.Cells; j += 7)
                for (int i = 0; i <= grid.Cells; i += 5)
                {
                    Vector3 v = grid.Vertex(i, j);
                    Assert.AreEqual(f.Height(v.x, v.z), v.y, 1e-5f);
                }
            Mesh a = grid.CollisionMesh(0, 10, 0, 10, "a"), b = grid.CollisionMesh(10, 20, 0, 10, "b");
            Assert.AreEqual(a.vertices[10], b.vertices[0], "chunks meet exactly");
            // Between vertices the flat facets stay close to the smooth field.
            float worst = 0f;
            var col = new GameObject("c").AddComponent<MeshCollider>();
            col.sharedMesh = grid.CollisionMesh(0, grid.Cells, 0, grid.Cells, "all");
            Physics.SyncTransforms();
            for (int k = 0; k < 400; k++)
            {
                var p = new Vector3(-15f + (k % 20) * 1.5f + 0.37f, 50f, -15f + (k / 20) * 1.5f + 0.11f);
                Assert.IsTrue(col.Raycast(new Ray(p, Vector3.down), out RaycastHit hit, 100f));
                worst = Mathf.Max(worst, Mathf.Abs(hit.point.y - f.Height(p.x, p.z)));
            }
            Object.DestroyImmediate(col.sharedMesh);
            Object.DestroyImmediate(col.gameObject);
            Assert.Less(worst, 0.08f, "facets stay within 8 cm of the field");
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        [Test]
        public void SteepSlopes_NeverStopThePlayer()
        {
            var material = new Material(Shader.Find("Vision/LowPoly"));
            GameObject player = PropFactory.CreatePlayer(material);
            try
            {
                var cc = player.GetComponent<CharacterController>();
                Assert.GreaterOrEqual(cc.slopeLimit, 85f, "the controller never refuses a slope");
                Assert.GreaterOrEqual(PlayerController.SlopeFactor(Mathf.Tan(60f * Mathf.Deg2Rad)), 0.65f, "a 60 degree climb keeps moving");
                Assert.GreaterOrEqual(PlayerController.SlopeFactor(100f), 0.65f, "even a near-vertical face");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void SlopeFactor_SlowsClimbsAndSlightlySpeedsDescents()
        {
            Assert.AreEqual(1f, PlayerController.SlopeFactor(0f), 1e-5f);
            Assert.AreEqual(1f - 0.35f * 0.5f, PlayerController.SlopeFactor(Mathf.Tan(30f * Mathf.Deg2Rad)), 1e-4f);
            Assert.Greater(PlayerController.SlopeFactor(-0.5f), 1f);
            Assert.LessOrEqual(PlayerController.SlopeFactor(-5f), 1.08f + 1e-5f);
        }

        /// <summary>A 30° ramp rising toward +Z (world units, scale 1).</summary>
        static float rampDeg = 30f;

        static bool Ramp(Vector3 world, out float height, out Vector3 normal)
        {
            float g = Mathf.Tan(rampDeg * Mathf.Deg2Rad);
            height = world.z * g;
            normal = new Vector3(0f, 1f, -g).normalized;
            return true;
        }

        [Test]
        public void Gait_On30To60DegreeRamps_PlantsFeetOnTheSlope_UpAndDown_WalkingAndSprinting()
        {
            var material = new Material(Shader.Find("Vision/LowPoly"));
            GameObject player = PropFactory.CreatePlayer(material);
            var animator = player.GetComponent<HumanoidAnimator>();
            Transform footL = animator.bones[(int)Bone.FootL], footR = animator.bones[(int)Bone.FootR];
            HumanoidAnimator.Ground = Ramp;
            try
            {
                foreach (float deg in new[] { 30f, 45f, 60f })
                foreach (float dir in new[] { 1f, -1f })
                {
                    rampDeg = deg;
                    foreach (float flat in new[] { 1.6f, 2.6f })
                    {
                        float speed = flat * PlayerController.SlopeFactor(dir * Mathf.Tan(deg * Mathf.Deg2Rad));
                        player.transform.position = new Vector3(0f, 0f, 0f);
                        animator.Solver.Reset();
                        float worstReach = 0f, lowest = float.MaxValue, highestPlanted = float.MinValue;
                        for (int i = 0; i < 300; i++)
                        {
                            Vector3 p = player.transform.position + Vector3.forward * (dir * speed / 60f);
                            Ramp(p, out float h, out _);
                            player.transform.position = new Vector3(p.x, h, p.z);
                            animator.Drive(Vector3.forward * (dir * speed), new Vector2(0f, dir));
                            animator.Step(1f / 60f);
                            if (i < 60) continue;
                            GaitPose pose = animator.Solver.Evaluate();
                            foreach (var (foot, leg, target) in new[] { (footL, pose.Left, animator.LeftAnkleTarget), (footR, pose.Right, animator.RightAnkleTarget) })
                            {
                                Ramp(foot.position, out float g, out _);
                                float aboveGround = foot.position.y - g;
                                lowest = Mathf.Min(lowest, aboveGround);
                                if (!leg.Grounded) continue;
                                worstReach = Mathf.Max(worstReach, (foot.position - target).magnitude);
                                highestPlanted = Mathf.Max(highestPlanted, aboveGround);
                            }
                        }
                        string label = $"{(dir > 0 ? "uphill" : "downhill")} {deg} degrees at {speed:0.00} m/s";
                        Assert.Less(worstReach, deg > 50f ? 0.06f : 0.02f, $"planted feet reach their targets on the slope ({label})");
                        Assert.Greater(lowest, HumanoidSkeleton.AnkleHeight * 0.4f, $"no foot sinks into the slope ({label})");
                        Assert.Less(highestPlanted, deg > 50f ? 0.4f : 0.3f, $"planted feet stay on the slope ({label})");
                    }
                }
            }
            finally
            {
                rampDeg = 30f;
                HumanoidAnimator.Ground = null;
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(material);
            }
        }

    }
}
