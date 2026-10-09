using UnityEngine;

namespace Vision.Characters
{
    /// <summary>One leg's target for the frame, in body space (design units, +Z forward, feet at y = 0).</summary>
    public struct LegPose
    {
        /// <summary>Ankle joint target.</summary>
        public Vector3 Ankle;
        /// <summary>Foot pitch in degrees: positive lifts the toes (heel strike), negative lifts the heel (push-off).</summary>
        public float FootPitch;
        /// <summary>Toe pitch in degrees relative to the ground (0 keeps the toes flat while the heel rises).</summary>
        public float ToePitch;
        public bool Grounded;
        /// <summary>The point of the sole touching the ground (heel, flat foot or ball), or the ankle in swing.</summary>
        public Vector3 Contact;
    }

    /// <summary>A full-body gait pose: pelvis, spine, legs and arms. Angles in degrees.</summary>
    public struct GaitPose
    {
        public Vector3 PelvisOffset;
        public float PelvisYaw, PelvisRoll, PelvisPitch;
        public float SpinePitch, ChestYaw, ChestPitch, HeadPitch, HeadYaw;
        public LegPose Left, Right;
        /// <summary>Shoulder flexion (positive swings the arm forward) and elbow flexion.</summary>
        public float LeftShoulder, RightShoulder, LeftElbow, RightElbow;
        public float ArmAbduction;
        /// <summary>0 = straight fingers, 1 = a loose fist.</summary>
        public float FingerCurl;
        /// <summary>Hip joint centres in body space, for reach checks.</summary>
        public Vector3 LeftHip, RightHip;
    }

    /// <summary>
    /// Procedural walking and running, modelled on human gait biomechanics. Each leg alternates
    /// stance and swing. In stance the foot rolls over the heel (heel strike, toes up), lies flat,
    /// then pivots on the ball as the heel rises (push-off); the touching point never moves on the
    /// ground, so feet do not skate. In swing the ankle follows a minimum-jerk path forward with
    /// ground clearance (and a heel kick when running). Pelvis height is the highest the grounded legs
    /// can reach, which produces the natural bob (high at mid-stance when walking); running adds
    /// knee compression in stance and flight phases with no foot down.
    /// Walking at 1.6 m/s: ~1.0 s stride, 60% stance. Running at 2.6 m/s: ~0.75 s stride, 42% stance. Faster, as a sprinter:
    /// quicker strides (down to 0.44 s) and a shorter stance (down to 28%), so the foot's ground contact stays under a metre
    /// at the original game's 4-8 m/s. Above <see cref="MaxSpeed"/> (speed mode) the legs keep that pace and the body slides.
    /// Speeds are design units (metres at a 1.8 m body height); negative speed walks backwards.
    /// </summary>
    public sealed class GaitSolver
    {
        const float L = HumanoidSkeleton.ThighLength + HumanoidSkeleton.ShinLength;
        const float Reach = L * 0.995f;
        static readonly Vector3 HeelOffset = new Vector3(0f, -HumanoidSkeleton.AnkleHeight, -0.065f);
        static readonly Vector3 BallOffset = new Vector3(0f, -HumanoidSkeleton.AnkleHeight, HumanoidSkeleton.FootToBall);

        public float Phase { get; private set; }
        public float Speed { get; private set; }
        public float Acceleration = 14f;
        /// <summary>The fastest the legs animate (m/s); faster movement (speed mode) reuses this stride.</summary>
        public const float MaxSpeed = 8f;
        /// <summary>
        /// Shortens the stride (0.45-1) at the same speed by quickening the steps, as people do on steep ground, so
        /// both feet can still reach the slope.
        /// </summary>
        public float StrideScale = 1f;
        float time;

        /// <summary>0 = walking, 1 = running, from speed.</summary>
        public float RunBlend => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.85f, 2.45f, Mathf.Abs(Speed)));
        /// <summary>0 = standing, 1 = full gait.</summary>
        public float Moving => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.5f, Mathf.Abs(Speed)));

        public float CycleTime
        {
            get
            {
                float v = Mathf.Abs(Speed);
                float walk = Mathf.Clamp(1.22f - 0.14f * v, 0.9f, 1.3f);
                float run = v <= 2.6f ? Mathf.Clamp(0.86f - 0.04f * v, 0.6f, 0.8f) : Mathf.Max(0.44f, 0.756f - 0.075f * (v - 2.6f));
                return Mathf.Lerp(walk, run, RunBlend) * Mathf.Clamp(StrideScale, 0.45f, 1f);
            }
        }

        public float StanceFraction => Mathf.Lerp(0.60f, 0.42f - Mathf.Clamp((Mathf.Abs(Speed) - 2.6f) * 0.035f, 0f, 0.15f), RunBlend);

        public void Reset(float phase = 0f, float speed = 0f)
        {
            Phase = phase;
            Speed = speed;
            time = 0f;
        }

        /// <summary>Moves time forward with the requested speed (approached with limited acceleration).</summary>
        public void Advance(float dt, float targetSpeed)
        {
            Speed = Mathf.MoveTowards(Speed, Mathf.Clamp(targetSpeed, -MaxSpeed, MaxSpeed), Acceleration * dt);
            time += dt;
            if (Moving > 0f) Phase = Mathf.Repeat(Phase + dt / CycleTime, 1f);
        }

        public GaitPose Evaluate()
        {
            float s = RunBlend;
            float m = Moving;
            float v = Mathf.Abs(Speed);
            bool backwards = Speed < 0f;
            float phase = backwards ? Mathf.Repeat(1f - Phase, 1f) : Phase;

            GaitPose gait = EvaluateGait(phase, v, s);
            if (backwards)
            {
                // Time-reversed gait, mirrored front to back, while the body moves backwards.
                gait.PelvisYaw = -gait.PelvisYaw;
                gait.ChestYaw = -gait.ChestYaw;
            }
            GaitPose idle = Idle();
            return m >= 0.999f ? gait : Blend(idle, gait, m);
        }

        GaitPose EvaluateGait(float phase, float v, float s)
        {
            float beta = StanceFraction;
            float T = CycleTime;
            float travel = v * T * beta;
            var pose = new GaitPose();

            pose.Left = Leg(phase, -1, beta, travel, s);
            pose.Right = Leg(Mathf.Repeat(phase + 0.5f, 1f), 1, beta, travel, s);

            // Pelvis: lateral shift toward the stance leg, obliquity, transverse rotation, tilt.
            float c = Mathf.Cos(2f * Mathf.PI * (phase - beta * 0.5f));
            float sway = Mathf.Lerp(0.022f, 0.008f, s);
            pose.PelvisRoll = -Mathf.Lerp(3f, 4f, s) * c;
            pose.PelvisYaw = Mathf.Lerp(4f, 8f, s) * Mathf.Cos(2f * Mathf.PI * phase);
            pose.PelvisPitch = Mathf.Lerp(2f, 7f, s);

            float baseHeight = HumanoidSkeleton.HipHeight - Mathf.Lerp(0.012f, 0.06f, s);
            float height = baseHeight;
            if (s > 0f)
            {
                // Running: compress at mid-stance, rise in flight.
                float run;
                float uL = phase / beta, uR = Mathf.Repeat(phase + 0.5f, 1f) / beta;
                if (uL < 1f) run = baseHeight - 0.035f * Mathf.Sin(Mathf.PI * uL);
                else if (uR < 1f) run = baseHeight - 0.035f * Mathf.Sin(Mathf.PI * uR);
                else
                {
                    // Flight between one leg's toe-off and the other's strike: an arc above stance height.
                    float k = (Mathf.Repeat(phase, 0.5f) - beta) / Mathf.Max(0.0001f, 0.5f - beta);
                    run = baseHeight + 0.03f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(k));
                }
                height = Mathf.Lerp(baseHeight, run, s);
            }

            // The pelvis may not rise above what the grounded legs can reach.
            Vector3 pelvisBase = new Vector3(-sway * c, 0f, 0f);
            for (int iter = 0; iter < 2; iter++)
            {
                Quaternion rot = Quaternion.Euler(pose.PelvisPitch, pose.PelvisYaw, pose.PelvisRoll);
                Vector3 pelvis = pelvisBase + Vector3.up * height;
                pose.LeftHip = pelvis + rot * new Vector3(-HumanoidSkeleton.HipHalfWidth, 0f, 0f);
                pose.RightHip = pelvis + rot * new Vector3(HumanoidSkeleton.HipHalfWidth, 0f, 0f);
                height = Mathf.Min(height, MaxHeight(pose.Left, pose.LeftHip, pelvis), MaxHeight(pose.Right, pose.RightHip, pelvis));
            }
            pose.PelvisOffset = new Vector3(pelvisBase.x, height - HumanoidSkeleton.HipHeight, 0f);
            {
                Quaternion rot = Quaternion.Euler(pose.PelvisPitch, pose.PelvisYaw, pose.PelvisRoll);
                Vector3 pelvis = pelvisBase + Vector3.up * height;
                pose.LeftHip = pelvis + rot * new Vector3(-HumanoidSkeleton.HipHalfWidth, 0f, 0f);
                pose.RightHip = pelvis + rot * new Vector3(HumanoidSkeleton.HipHalfWidth, 0f, 0f);
            }
            // A fast stride drops the pelvis; the swinging foot then folds in under it (more knee) rather than over-reach.
            pose.Left = ClampSwing(pose.Left, pose.LeftHip);
            pose.Right = ClampSwing(pose.Right, pose.RightHip);

            // Upper body: lean, chest counter-rotation, steady head.
            float lean = Mathf.Lerp(3f, 14f, s);
            pose.SpinePitch = lean * 0.4f;
            pose.ChestPitch = lean * 0.3f;
            pose.ChestYaw = -1.25f * pose.PelvisYaw;
            pose.HeadPitch = -(lean * 0.7f + pose.PelvisPitch * 0.8f);
            pose.HeadYaw = -(pose.PelvisYaw + pose.ChestYaw) * 0.9f;

            // Arms swing opposite the same-side leg.
            float armAmp = Mathf.Lerp(20f, 48f, s);
            float armBase = Mathf.Lerp(0f, 12f, s);
            pose.LeftShoulder = armBase - armAmp * Mathf.Cos(2f * Mathf.PI * phase);
            pose.RightShoulder = armBase - armAmp * Mathf.Cos(2f * Mathf.PI * (phase + 0.5f));
            float e0 = Mathf.Lerp(12f, 82f, s), e1 = Mathf.Lerp(16f, 22f, s);
            pose.LeftElbow = e0 + e1 * Mathf.Max(0f, (pose.LeftShoulder - armBase) / armAmp);
            pose.RightElbow = e0 + e1 * Mathf.Max(0f, (pose.RightShoulder - armBase) / armAmp);
            pose.ArmAbduction = Mathf.Lerp(6f, 4f, s);
            pose.FingerCurl = Mathf.Lerp(0.3f, 0.75f, s);
            return pose;
        }

        static LegPose ClampSwing(LegPose leg, Vector3 hip)
        {
            if (leg.Grounded) return leg;
            Vector3 d = leg.Ankle - hip;
            if (d.sqrMagnitude <= Reach * Reach) return leg;
            leg.Ankle = hip + d.normalized * Reach;
            leg.Contact = leg.Ankle;
            return leg;
        }

        static float MaxHeight(LegPose leg, Vector3 hip, Vector3 pelvis)
        {
            if (!leg.Grounded) return float.PositiveInfinity;
            float dx = leg.Ankle.x - hip.x, dz = leg.Ankle.z - hip.z;
            float horizontal2 = dx * dx + dz * dz;
            if (horizontal2 >= Reach * Reach) return leg.Ankle.y + (pelvis.y - hip.y);
            // Height of the hip above the ankle that keeps the leg within reach, converted to pelvis height.
            return leg.Ankle.y + Mathf.Sqrt(Reach * Reach - horizontal2) + (pelvis.y - hip.y) - 0.002f;
        }

        /// <summary>One leg at its own phase (stance first, then swing).</summary>
        LegPose Leg(float phase, int side, float beta, float travel, float s)
        {
            float heelEnd = Mathf.Lerp(0.10f, 0.04f, s);
            float toeStart = Mathf.Lerp(0.50f, 0.45f, s);
            float strike = Mathf.Lerp(12f, 3f, s);
            float pushOff = Mathf.Lerp(55f, 42f, s);
            float z0 = Mathf.Lerp(0.06f, 0.0f, s);
            float x = side * HumanoidSkeleton.HipHalfWidth * Mathf.Lerp(0.95f, 0.55f, s);
            float flatMid = (heelEnd + toeStart) * 0.5f;
            // Flat-foot ankle position on the ground, in the frame of the body at the start of stance.
            var flat = new Vector3(x, HumanoidSkeleton.AnkleHeight, z0 + travel * flatMid);

            LegPose StanceAt(float u)
            {
                var p = new LegPose { Grounded = true };
                Vector3 bodyShift = new Vector3(0f, 0f, travel * u);
                Vector3 ankleGround;
                if (u < heelEnd)
                {
                    p.FootPitch = strike * (1f - u / heelEnd);
                    Vector3 heel = flat + HeelOffset;
                    ankleGround = heel - Pitch(p.FootPitch) * HeelOffset;
                    p.Contact = heel - bodyShift;
                    p.ToePitch = p.FootPitch;
                }
                else if (u < toeStart)
                {
                    p.FootPitch = 0f;
                    ankleGround = flat;
                    p.Contact = flat + new Vector3(0f, -HumanoidSkeleton.AnkleHeight, 0f) - bodyShift;
                    p.ToePitch = 0f;
                }
                else
                {
                    float k = (u - toeStart) / (1f - toeStart);
                    p.FootPitch = -pushOff * k * k * (3f - 2f * k);
                    Vector3 ball = flat + BallOffset;
                    ankleGround = ball - Pitch(p.FootPitch) * BallOffset;
                    p.Contact = ball - bodyShift;
                    p.ToePitch = 0f;
                }
                p.Ankle = ankleGround - bodyShift;
                return p;
            }

            if (phase < beta) return StanceAt(phase / beta);

            // Swing: from toe-off to the next heel strike.
            float w = (phase - beta) / (1f - beta);
            LegPose from = StanceAt(1f), to = StanceAt(0f);
            float e = w * w * w * (10f - 15f * w + 6f * w * w);
            var swing = new LegPose { Grounded = false };
            float clearance = Mathf.Lerp(0.09f, 0.30f, s) * Mathf.Sin(Mathf.PI * Mathf.Pow(w, 0.75f));
            float kick = 0.14f * s * Mathf.Pow(Mathf.Sin(Mathf.PI * w), 2f) * (1f - w);
            swing.Ankle = Vector3.Lerp(from.Ankle, to.Ankle, e) + new Vector3(0f, clearance, -kick);
            swing.FootPitch = Mathf.Lerp(from.FootPitch, to.FootPitch, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(w / 0.6f)));
            swing.ToePitch = swing.FootPitch;
            swing.Contact = swing.Ankle;
            return swing;
        }

        static Quaternion Pitch(float degrees) => Quaternion.Euler(-degrees, 0f, 0f);

        GaitPose Idle()
        {
            float breath = Mathf.Sin(time * 1.6f);
            var p = new GaitPose
            {
                PelvisOffset = new Vector3(0.006f * Mathf.Sin(time * 0.45f), -0.008f + 0.002f * breath, 0f),
                SpinePitch = 1f,
                ChestPitch = 0.8f * breath,
                HeadPitch = -1f,
                LeftShoulder = 2f,
                RightShoulder = 2f,
                LeftElbow = 9f,
                RightElbow = 9f,
                ArmAbduction = 5f,
                FingerCurl = 0.35f,
            };
            p.Left = new LegPose { Ankle = new Vector3(-0.11f, HumanoidSkeleton.AnkleHeight, 0.02f), Grounded = true, Contact = new Vector3(-0.11f, 0f, 0.02f) };
            p.Right = new LegPose { Ankle = new Vector3(0.11f, HumanoidSkeleton.AnkleHeight, 0.0f), Grounded = true, Contact = new Vector3(0.11f, 0f, 0f) };
            Vector3 pelvis = new Vector3(p.PelvisOffset.x, HumanoidSkeleton.HipHeight + p.PelvisOffset.y, 0f);
            p.LeftHip = pelvis + new Vector3(-HumanoidSkeleton.HipHalfWidth, 0f, 0f);
            p.RightHip = pelvis + new Vector3(HumanoidSkeleton.HipHalfWidth, 0f, 0f);
            return p;
        }

        static LegPose Blend(LegPose a, LegPose b, float t) => new LegPose
        {
            Ankle = Vector3.Lerp(a.Ankle, b.Ankle, t),
            FootPitch = Mathf.Lerp(a.FootPitch, b.FootPitch, t),
            ToePitch = Mathf.Lerp(a.ToePitch, b.ToePitch, t),
            Grounded = t < 0.5f ? a.Grounded : b.Grounded,
            Contact = Vector3.Lerp(a.Contact, b.Contact, t),
        };

        static GaitPose Blend(GaitPose a, GaitPose b, float t) => new GaitPose
        {
            PelvisOffset = Vector3.Lerp(a.PelvisOffset, b.PelvisOffset, t),
            PelvisYaw = Mathf.Lerp(a.PelvisYaw, b.PelvisYaw, t),
            PelvisRoll = Mathf.Lerp(a.PelvisRoll, b.PelvisRoll, t),
            PelvisPitch = Mathf.Lerp(a.PelvisPitch, b.PelvisPitch, t),
            SpinePitch = Mathf.Lerp(a.SpinePitch, b.SpinePitch, t),
            ChestYaw = Mathf.Lerp(a.ChestYaw, b.ChestYaw, t),
            ChestPitch = Mathf.Lerp(a.ChestPitch, b.ChestPitch, t),
            HeadPitch = Mathf.Lerp(a.HeadPitch, b.HeadPitch, t),
            HeadYaw = Mathf.Lerp(a.HeadYaw, b.HeadYaw, t),
            Left = Blend(a.Left, b.Left, t),
            Right = Blend(a.Right, b.Right, t),
            LeftShoulder = Mathf.Lerp(a.LeftShoulder, b.LeftShoulder, t),
            RightShoulder = Mathf.Lerp(a.RightShoulder, b.RightShoulder, t),
            LeftElbow = Mathf.Lerp(a.LeftElbow, b.LeftElbow, t),
            RightElbow = Mathf.Lerp(a.RightElbow, b.RightElbow, t),
            ArmAbduction = Mathf.Lerp(a.ArmAbduction, b.ArmAbduction, t),
            FingerCurl = Mathf.Lerp(a.FingerCurl, b.FingerCurl, t),
            LeftHip = Vector3.Lerp(a.LeftHip, b.LeftHip, t),
            RightHip = Vector3.Lerp(a.RightHip, b.RightHip, t),
        };
    }
}
