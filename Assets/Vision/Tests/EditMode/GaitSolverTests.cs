using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Vision.Characters;

namespace Vision.Tests
{
    public class GaitSolverTests
    {
        const float Dt = 1f / 240f;
        static readonly Vector3 Heel = new Vector3(0f, -HumanoidSkeleton.AnkleHeight, -0.065f);
        static readonly Vector3 Ball = new Vector3(0f, -HumanoidSkeleton.AnkleHeight, HumanoidSkeleton.FootToBall);
        const float LegLength = HumanoidSkeleton.ThighLength + HumanoidSkeleton.ShinLength;

        static Vector3 Point(LegPose leg, Vector3 offset) => leg.Ankle + Quaternion.Euler(-leg.FootPitch, 0f, 0f) * offset;

        /// <summary>Runs the solver at a constant speed, yielding the pose and the body's ground position each step.</summary>
        static IEnumerable<(GaitPose pose, float bodyZ)> Run(float speed, float seconds)
        {
            var solver = new GaitSolver();
            solver.Reset(0f, speed);
            float z = 0f;
            for (float t = 0f; t < seconds; t += Dt)
            {
                solver.Advance(Dt, speed);
                z += speed * Dt;
                yield return (solver.Evaluate(), z);
            }
        }

        [TestCase(1.6f)]
        [TestCase(2.6f)]
        [TestCase(-1.2f)]
        [TestCase(0.8f)]
        public void FeetDoNotSkate(float speed)
        {
            // While a foot is down, the part of it touching the ground stays put on the ground: the heel
            // during heel strike and flat foot, the ball during flat foot and push-off.
            var last = new Dictionary<int, (bool down, Vector3 heel, Vector3 ball, float pitch)>();
            float worst = 0f;
            foreach (var (pose, z) in Run(speed, 4f))
            {
                for (int side = 0; side < 2; side++)
                {
                    LegPose leg = side == 0 ? pose.Left : pose.Right;
                    Vector3 heel = Point(leg, Heel) + Vector3.forward * z;
                    Vector3 ball = Point(leg, Ball) + Vector3.forward * z;
                    if (last.TryGetValue(side, out var prev) && prev.down && leg.Grounded)
                    {
                        if (leg.FootPitch >= -0.01f && prev.pitch >= -0.01f) worst = Mathf.Max(worst, (heel - prev.heel).magnitude);
                        if (leg.FootPitch <= 0.01f && prev.pitch <= 0.01f) worst = Mathf.Max(worst, (ball - prev.ball).magnitude);
                    }
                    last[side] = (leg.Grounded, heel, ball, leg.FootPitch);
                }
            }
            Assert.Less(worst, 0.0015f, "a grounded foot slid along the ground");
        }

        [TestCase(1.6f)]
        [TestCase(2.6f)]
        [TestCase(-1.2f)]
        public void FeetStayAboveGround_AndLegsCanReach(float speed)
        {
            foreach (var (pose, _) in Run(speed, 3f))
            {
                foreach (var (leg, hip) in new[] { (pose.Left, pose.LeftHip), (pose.Right, pose.RightHip) })
                {
                    Assert.GreaterOrEqual(Point(leg, Heel).y, -0.002f, "heel below ground");
                    Assert.GreaterOrEqual(Point(leg, Ball).y, -0.002f, "ball below ground");
                    float d = (leg.Ankle - hip).magnitude;
                    if (leg.Grounded) Assert.LessOrEqual(d, LegLength + 1e-3f, "grounded leg over-stretched");
                    else Assert.LessOrEqual(d, LegLength + 0.08f, "swing foot far out of reach");
                    // Knee flexion from the law of cosines stays within a human range.
                    float dd = Mathf.Clamp(d, 0.2f, LegLength);
                    float l1 = HumanoidSkeleton.ThighLength, l2 = HumanoidSkeleton.ShinLength;
                    float knee = 180f - Mathf.Acos(Mathf.Clamp((l1 * l1 + l2 * l2 - dd * dd) / (2f * l1 * l2), -1f, 1f)) * Mathf.Rad2Deg;
                    Assert.That(knee, Is.InRange(-0.01f, 140f));
                }
            }
        }

        [Test]
        public void Running_HasFlightPhases_WalkingDoesNot()
        {
            bool walkFlight = false, runFlight = false;
            foreach (var (pose, _) in Run(1.6f, 3f)) walkFlight |= !pose.Left.Grounded && !pose.Right.Grounded;
            foreach (var (pose, _) in Run(2.6f, 3f)) runFlight |= !pose.Left.Grounded && !pose.Right.Grounded;
            Assert.IsFalse(walkFlight, "walking always has a foot on the ground");
            Assert.IsTrue(runFlight, "running has moments with both feet off the ground");
        }

        [TestCase(1.6f)]
        [TestCase(2.6f)]
        public void Arms_SwingOppositeTheSameSideLeg(float speed)
        {
            double sum = 0;
            foreach (var (pose, _) in Run(speed, 2f)) sum += pose.Left.Ankle.z * pose.LeftShoulder + pose.Right.Ankle.z * pose.RightShoulder;
            Assert.Less(sum, 0.0, "the left arm should be back when the left foot is forward");
        }

        [Test]
        public void Cadence_RisesWithSpeed_AndStanceShortens()
        {
            var s = new GaitSolver();
            s.Reset(0f, 1.0f);
            float slow = s.CycleTime, slowStance = s.StanceFraction;
            s.Reset(0f, 1.6f);
            float walk = s.CycleTime;
            s.Reset(0f, 2.6f);
            float run = s.CycleTime, runStance = s.StanceFraction;
            Assert.Greater(slow, walk);
            Assert.Greater(walk, run);
            Assert.That(walk, Is.InRange(0.95f, 1.15f), "walking stride about a second");
            Assert.That(run, Is.InRange(0.65f, 0.85f));
            Assert.Greater(slowStance, 0.55f);
            Assert.Less(runStance, 0.5f);
        }

        [Test]
        public void Walking_PelvisBobsARealisticAmount()
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (var (pose, _) in Run(1.6f, 3f))
            {
                min = Mathf.Min(min, pose.PelvisOffset.y);
                max = Mathf.Max(max, pose.PelvisOffset.y);
            }
            Assert.That(max - min, Is.InRange(0.015f, 0.09f));
        }

        [Test]
        public void Standing_HasBothFeetDown()
        {
            var s = new GaitSolver();
            s.Reset(0f, 0f);
            s.Advance(0.5f, 0f);
            GaitPose p = s.Evaluate();
            Assert.IsTrue(p.Left.Grounded && p.Right.Grounded);
            Assert.AreEqual(HumanoidSkeleton.AnkleHeight, p.Left.Ankle.y, 1e-4f);
        }
    }
}
