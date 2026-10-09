using UnityEngine;

namespace Vision.Characters
{
    /// <summary>
    /// Animates a <see cref="HumanoidSkeleton"/> procedurally from a velocity and an aim direction.
    /// The legs face the direction of travel (or keep facing the aim and walk backwards when travel is
    /// more than 100° from it); the spine and chest twist to face the aim exactly, at once, up to ±100° (the legs are
    /// dragged round when the aim swings further), so the body always points where the player aims. Each frame
    /// the <see cref="GaitSolver"/> gives pelvis, foot and arm targets and the legs are solved with
    /// analytic two-bone IK, knees bending forward only. Works under a scaled level root: speeds are
    /// converted to design units with the transform scale.
    /// </summary>
    public sealed class HumanoidAnimator : MonoBehaviour
    {
        /// <summary>World-space ground height and normal under a world point; false where there is no ground data.</summary>
        public delegate bool GroundSampler(Vector3 world, out float height, out Vector3 normal);

        /// <summary>
        /// The ground the feet adapt to (set by the level). Null means flat ground at the character's root,
        /// which reproduces the flat gait exactly.
        /// </summary>
        public static GroundSampler Ground;

        [Tooltip("Largest forward lean into a climb (and back lean downhill), degrees.")]
        public float maxSlopeLean = 20f;
        [Tooltip("Yaw pivot for the whole body (legs face this way).")]
        public Transform body;
        [Tooltip("Bone transforms indexed by Vision.Characters.Bone.")]
        public Transform[] bones;
        [Tooltip("Degrees per second the legs turn toward the travel direction.")]
        public float turnSpeed = 720f;
        public float maxTwist = 100f;
        [Tooltip("Downed: the body lies forward on the ground and the legs crawl.")]
        public bool Prone;
        [Tooltip("Crouching (0-1): the pelvis drops, the knees bend under it and the back leans forward.")]
        public float Crouch;
        float crouch;

        readonly GaitSolver solver = new GaitSolver();
        Vector3 worldVelocity;
        Vector2 aim = Vector2.up;
        float legsYaw;
        bool initialized;

        float pelvisDrop, slopeLean, prone;
        Vector3 bodyRest;

        public GaitSolver Solver => solver;
        /// <summary>World-space ankle targets of the last frame (after terrain adaptation), left then right.</summary>
        public Vector3 LeftAnkleTarget { get; private set; }
        public Vector3 RightAnkleTarget { get; private set; }
        public float LegsYaw => legsYaw;
        /// <summary>How far the chest is turned from the legs toward the aim this frame (degrees); half on the spine, half on the chest.</summary>
        public float Twist { get; private set; }

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
                bodyRest = body.localPosition;
                initialized = true;
            }
            prone = Mathf.MoveTowards(prone, Prone ? 1f : 0f, dt * 2.5f);
            crouch = Mathf.MoveTowards(crouch, Prone ? 0f : Mathf.Clamp01(Crouch), dt * 4f);

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
            // The upper body faces the aim at once: if the aim swings past what the waist can turn, the legs come with it.
            float lag = Mathf.DeltaAngle(legsYaw, aimYaw);
            if (Mathf.Abs(lag) > maxTwist) legsYaw = aimYaw - Mathf.Sign(lag) * maxTwist;

            var forward = new Vector2(Mathf.Sin(legsYaw * Mathf.Deg2Rad), Mathf.Cos(legsYaw * Mathf.Deg2Rad));
            solver.Advance(dt, Vector2.Dot(v, forward));
            body.localRotation = Quaternion.Euler(0f, legsYaw, 0f);
            if (prone > 0f)
            {
                // Lying forward on the ground (raised a little so the chest rests on it rather than in it).
                float s = Mathf.SmoothStep(0f, 1f, prone);
                body.localRotation *= Quaternion.Euler(80f * s, 0f, 0f);
                body.localPosition = bodyRest + Vector3.up * (0.13f * s);
            }
            else body.localPosition = bodyRest;

            // Lean into the slope along the legs' heading (half the slope angle, while moving).
            float grade = 0f;
            Vector3 fwd3 = new Vector3(forward.x, 0f, forward.y) * (0.35f * scale);
            if (Sample(transform.position + fwd3, out float hAhead, out _) && Sample(transform.position - fwd3, out float hBehind, out _))
                grade = (hAhead - hBehind) / (0.7f * scale);
            // Steeper ground, shorter quicker steps: full stride up to 15 degrees, about half by 60.
            float slopeDeg = Mathf.Atan(Mathf.Abs(grade)) * Mathf.Rad2Deg;
            solver.StrideScale = Mathf.Lerp(1f, 0.45f, Mathf.InverseLerp(15f, 60f, slopeDeg));
            float targetLean = Mathf.Clamp(Mathf.Atan(grade) * Mathf.Rad2Deg * 0.5f, -maxSlopeLean, maxSlopeLean) * solver.Moving;
            slopeLean = Mathf.Lerp(slopeLean, targetLean, 1f - Mathf.Exp(-6f * dt));

            Twist = Mathf.Clamp(Mathf.DeltaAngle(legsYaw, aimYaw), -maxTwist, maxTwist);
            Apply(solver.Evaluate(), Twist, scale);
        }

        Transform B(Bone b) => bones[(int)b];

        /// <summary>The shares of the twist toward the aim on the spine and the chest (they add up to all of it).</summary>
        public const float SpineTwist = 0.5f, ChestTwist = 0.5f;

        bool Sample(Vector3 world, out float height, out Vector3 normal)
        {
            if (Ground != null && Ground(world, out height, out normal)) return true;
            height = transform.position.y;
            normal = Vector3.up;
            return Ground == null;
        }

        /// <summary>
        /// How far (world units) the ground under a body-space ankle target is above the character's root, and the
        /// ground normal there. Zero and up on flat ground at the root, so the flat gait is unchanged.
        /// </summary>
        float GroundOffset(Vector3 bodyAnkle, out Vector3 normal)
        {
            Vector3 world = body.TransformPoint(bodyAnkle);
            if (!Sample(world, out float h, out normal)) return 0f;
            return h - transform.position.y;
        }

        void Apply(GaitPose pose, float twist, float scale)
        {
            // Feet follow the ground under them; the pelvis drops by the lowest foot so that leg can still reach.
            Vector3 nL = Vector3.up, nR = Vector3.up;
            float offL = prone > 0f ? 0f : GroundOffset(pose.Left.Ankle, out nL);
            float offR = prone > 0f ? 0f : GroundOffset(pose.Right.Ankle, out nR);
            float maxDrop = 0.5f * HumanoidSkeleton.HipHeight * scale;
            float drop = Mathf.Clamp(-Mathf.Min(offL, offR), 0f, maxDrop);
            pelvisDrop = Ground == null || prone > 0f ? 0f : drop;   // continuous already: the ankle targets move smoothly

            Transform pelvis = B(Bone.Pelvis);
            float ce = crouch * crouch * (3f - 2f * crouch);
            pelvis.localPosition = HumanoidSkeleton.BindPosition(Bone.Pelvis) + pose.PelvisOffset - Vector3.up * (pelvisDrop / scale + 0.27f * ce);
            pelvis.localRotation = Quaternion.Euler(pose.PelvisPitch, pose.PelvisYaw, pose.PelvisRoll);

            // The torso leans into a climb; the neck and head take most of it back so the gaze stays level.
            // The twist is all in the spine and chest, so the chest (and the arms, the light and anything held) faces the aim.
            B(Bone.Spine).localRotation = Quaternion.Euler(pose.SpinePitch - pose.PelvisPitch * 0.7f + slopeLean + 18f * ce, -pose.PelvisYaw + twist * SpineTwist, -pose.PelvisRoll * 0.8f);
            B(Bone.Chest).localRotation = Quaternion.Euler(pose.ChestPitch, pose.ChestYaw + twist * ChestTwist, 0f);
            B(Bone.Neck).localRotation = Quaternion.Euler(pose.HeadPitch * 0.4f - slopeLean * 0.4f, 0f, 0f);
            B(Bone.Head).localRotation = Quaternion.Euler(pose.HeadPitch * 0.6f - slopeLean * 0.4f - 12f * ce, pose.HeadYaw, 0f);

            LeftAnkleTarget = SolveLeg(pose.Left, Bone.ThighL, -1, scale, offL, nL);
            RightAnkleTarget = SolveLeg(pose.Right, Bone.ThighR, 1, scale, offR, nR);

            PoseArm(Bone.ClavicleL, -1, pose.LeftShoulder, pose.LeftElbow, pose.ArmAbduction);
            PoseArm(Bone.ClavicleR, 1, pose.RightShoulder, pose.RightElbow, pose.ArmAbduction);
            PoseFingers(Bone.Thumb1L, -1, pose.FingerCurl);
            PoseFingers(Bone.Thumb1R, 1, pose.FingerCurl);
        }

        Vector3 SolveLeg(LegPose leg, Bone thighBone, int side, float scale, float groundOffset, Vector3 groundNormal)
        {
            Transform thigh = B(thighBone), shin = B(thighBone + 1), foot = B(thighBone + 2), toe = B(thighBone + 3);
            float l1 = HumanoidSkeleton.ThighLength * scale, l2 = HumanoidSkeleton.ShinLength * scale;
            Vector3 hip = thigh.position;
            Vector3 target = body.TransformPoint(leg.Ankle) + Vector3.up * groundOffset;
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
            // A planted foot lies on the slope; a swinging one only partly follows it.
            Quaternion slope = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, groundNormal), leg.Grounded ? 1f : 0.35f);
            foot.rotation = slope * body.rotation * toeOut * Quaternion.Euler(-leg.FootPitch, 0f, 0f);
            toe.rotation = slope * body.rotation * toeOut * Quaternion.Euler(-leg.ToePitch, 0f, 0f);
            return target;
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
