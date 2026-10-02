using UnityEngine;

namespace Vision.Characters
{
    /// <summary>
    /// Animates a <see cref="HumanoidSkeleton"/> procedurally from a velocity and an aim direction.
    /// The legs face the direction of travel (or keep facing the aim and walk backwards when travel is
    /// more than 100° from it); the spine, neck and head twist toward the aim, up to ±80°. Each frame
    /// the <see cref="GaitSolver"/> gives pelvis, foot and arm targets and the legs are solved with
    /// analytic two-bone IK, knees bending forward only. Works under a scaled level root: speeds are
    /// converted to design units with the transform scale.
    /// </summary>
    public sealed class HumanoidAnimator : MonoBehaviour
    {
        [Tooltip("Yaw pivot for the whole body (legs face this way).")]
        public Transform body;
        [Tooltip("Bone transforms indexed by Vision.Characters.Bone.")]
        public Transform[] bones;
        [Tooltip("Degrees per second the legs turn toward the travel direction.")]
        public float turnSpeed = 420f;
        public float maxTwist = 80f;

        readonly GaitSolver solver = new GaitSolver();
        Vector3 worldVelocity;
        Vector2 aim = Vector2.up;
        float legsYaw;
        bool initialized;

        public GaitSolver Solver => solver;
        public float LegsYaw => legsYaw;

        /// <summary>Called by the controller each frame: world velocity and aim direction on the ground plane (x, z).</summary>
        public void Drive(Vector3 velocity, Vector2 aimDirection)
        {
            worldVelocity = velocity;
            if (aimDirection.sqrMagnitude > 1e-6f) aim = aimDirection.normalized;
        }

        void LateUpdate() => Step(Time.deltaTime);

        public void Step(float dt)
        {
            if (body == null || bones == null || bones.Length < HumanoidSkeleton.BoneCount) return;
            float scale = Mathf.Max(1e-4f, transform.lossyScale.x);
            var v = new Vector2(worldVelocity.x, worldVelocity.z) / scale;
            float speed = v.magnitude;
            float aimYaw = Mathf.Atan2(aim.x, aim.y) * Mathf.Rad2Deg;
            if (!initialized)
            {
                legsYaw = aimYaw;
                initialized = true;
            }

            float targetYaw = legsYaw;
            if (speed > 0.15f)
            {
                float moveYaw = Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg;
                targetYaw = Mathf.Abs(Mathf.DeltaAngle(moveYaw, aimYaw)) <= 100f ? moveYaw : moveYaw + 180f;
            }
            else if (Mathf.Abs(Mathf.DeltaAngle(legsYaw, aimYaw)) > 60f)
            {
                targetYaw = aimYaw;
            }
            legsYaw = Mathf.MoveTowardsAngle(legsYaw, targetYaw, turnSpeed * dt);

            var forward = new Vector2(Mathf.Sin(legsYaw * Mathf.Deg2Rad), Mathf.Cos(legsYaw * Mathf.Deg2Rad));
            solver.Advance(dt, Vector2.Dot(v, forward));
            body.localRotation = Quaternion.Euler(0f, legsYaw, 0f);
            Apply(solver.Evaluate(), Mathf.Clamp(Mathf.DeltaAngle(legsYaw, aimYaw), -maxTwist, maxTwist), scale);
        }

        Transform B(Bone b) => bones[(int)b];

        void Apply(GaitPose pose, float twist, float scale)
        {
            Transform pelvis = B(Bone.Pelvis);
            pelvis.localPosition = HumanoidSkeleton.BindPosition(Bone.Pelvis) + pose.PelvisOffset;
            pelvis.localRotation = Quaternion.Euler(pose.PelvisPitch, pose.PelvisYaw, pose.PelvisRoll);

            B(Bone.Spine).localRotation = Quaternion.Euler(pose.SpinePitch - pose.PelvisPitch * 0.7f, -pose.PelvisYaw + twist * 0.35f, -pose.PelvisRoll * 0.8f);
            B(Bone.Chest).localRotation = Quaternion.Euler(pose.ChestPitch, pose.ChestYaw + twist * 0.35f, 0f);
            B(Bone.Neck).localRotation = Quaternion.Euler(pose.HeadPitch * 0.4f, twist * 0.15f, 0f);
            B(Bone.Head).localRotation = Quaternion.Euler(pose.HeadPitch * 0.6f, pose.HeadYaw + twist * 0.15f, 0f);

            SolveLeg(pose.Left, Bone.ThighL, -1, scale);
            SolveLeg(pose.Right, Bone.ThighR, 1, scale);

            PoseArm(Bone.ClavicleL, -1, pose.LeftShoulder, pose.LeftElbow, pose.ArmAbduction);
            PoseArm(Bone.ClavicleR, 1, pose.RightShoulder, pose.RightElbow, pose.ArmAbduction);
            PoseFingers(Bone.Thumb1L, -1, pose.FingerCurl);
            PoseFingers(Bone.Thumb1R, 1, pose.FingerCurl);
        }

        void SolveLeg(LegPose leg, Bone thighBone, int side, float scale)
        {
            Transform thigh = B(thighBone), shin = B(thighBone + 1), foot = B(thighBone + 2), toe = B(thighBone + 3);
            float l1 = HumanoidSkeleton.ThighLength * scale, l2 = HumanoidSkeleton.ShinLength * scale;
            Vector3 hip = thigh.position;
            Vector3 target = body.TransformPoint(leg.Ankle);
            Vector3 toTarget = target - hip;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(l1 - l2) + 0.01f * scale, l1 + l2 - 1e-4f * scale);
            Vector3 dir = toTarget.sqrMagnitude > 1e-10f ? toTarget.normalized : -body.up;
            Vector3 pole = body.forward;
            Vector3 p = pole - dir * Vector3.Dot(pole, dir);
            p = p.sqrMagnitude > 1e-8f ? p.normalized : body.forward;
            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 thighDir = dir * cosA + p * sinA;
            Vector3 knee = hip + thighDir * l1;
            Vector3 shinDir = (hip + dir * d - knee).normalized;

            Quaternion toeOut = Quaternion.Euler(0f, side * 6f, 0f);
            thigh.rotation = AlongDown(thighDir, p) * toeOut;
            shin.rotation = AlongDown(shinDir, p) * toeOut;
            foot.rotation = body.rotation * toeOut * Quaternion.Euler(-leg.FootPitch, 0f, 0f);
            toe.rotation = body.rotation * toeOut * Quaternion.Euler(-leg.ToePitch, 0f, 0f);
        }

        /// <summary>A rotation whose -Y axis points along <paramref name="down"/> and whose +Z leans toward <paramref name="front"/>.</summary>
        static Quaternion AlongDown(Vector3 down, Vector3 front)
        {
            Vector3 f = front - down * Vector3.Dot(front, down);
            if (f.sqrMagnitude < 1e-8f) f = Vector3.Cross(down, Vector3.right);
            return Quaternion.LookRotation(f.normalized, -down);
        }

        void PoseArm(Bone clavicle, int side, float shoulder, float elbow, float abduction)
        {
            B(clavicle).localRotation = Quaternion.Euler(0f, 0f, side * shoulder * 0.04f);
            B(clavicle + 1).localRotation = Quaternion.Euler(-shoulder, -side * Mathf.Max(0f, shoulder) * 0.12f, side * abduction);
            B(clavicle + 2).localRotation = Quaternion.Euler(-elbow, 0f, 0f);
            B(clavicle + 3).localRotation = Quaternion.Euler(-elbow * 0.12f, 0f, 0f);
        }

        void PoseFingers(Bone thumb1, int side, float curl)
        {
            float[] angles = { 70f, 90f, 60f };
            for (int f = 0; f < 5; f++)
            {
                for (int k = 0; k < 3; k++)
                {
                    float a = (f == 0 ? angles[k] * 0.35f : angles[k]) * curl;
                    B(thumb1 + f * 3 + k).localRotation = Quaternion.Euler(0f, 0f, -side * a);
                }
            }
        }
    }
}
