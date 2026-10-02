using UnityEngine;

namespace Vision.Characters
{
    /// <summary>Bones of the humanoid rig, in skinning order.</summary>
    public enum Bone
    {
        Pelvis, Spine, Chest, Neck, Head,
        ClavicleL, UpperArmL, ForearmL, HandL,
        ClavicleR, UpperArmR, ForearmR, HandR,
        ThighL, ShinL, FootL, ToeL,
        ThighR, ShinR, FootR, ToeR,
        Thumb1L, Thumb2L, Thumb3L, Index1L, Index2L, Index3L, Middle1L, Middle2L, Middle3L, Ring1L, Ring2L, Ring3L, Pinky1L, Pinky2L, Pinky3L,
        Thumb1R, Thumb2R, Thumb3R, Index1R, Index2R, Index3R, Middle1R, Middle2R, Middle3R, Ring1R, Ring2R, Ring3R, Pinky1R, Pinky2R, Pinky3R,
    }

    /// <summary>
    /// Bind pose of a 1.8 m adult in design units (+X right, +Y up, +Z forward, feet at the origin),
    /// proportioned after Drillis and Contini's segment ratios: hip joint 0.53H, thigh 0.245H, shank
    /// 0.246H, ankle 0.039H, shoulder 0.82H, upper arm 0.186H, forearm 0.146H, hand 0.108H, foot
    /// length 0.152H. Relaxed stance: legs vertical, arms hanging with the hands beside the thighs,
    /// palms facing in. Every bone has identity bind rotation, so animation sets rotations directly.
    /// </summary>
    public static class HumanoidSkeleton
    {
        public const float Height = 1.8f;
        public const float HipHeight = 0.954f;        // 0.53 H
        public const float ThighLength = 0.441f;      // 0.245 H
        public const float ShinLength = 0.443f;       // 0.246 H
        public const float AnkleHeight = 0.07f;       // 0.039 H
        public const float HipHalfWidth = 0.09f;
        public const float FootToBall = 0.17f;
        public const int BoneCount = 51;

        public static readonly Bone[] Parents = BuildParents();

        static Bone[] BuildParents()
        {
            var p = new Bone[BoneCount];
            p[(int)Bone.Pelvis] = Bone.Pelvis; // root of the chain (parent is the skeleton root)
            p[(int)Bone.Spine] = Bone.Pelvis;
            p[(int)Bone.Chest] = Bone.Spine;
            p[(int)Bone.Neck] = Bone.Chest;
            p[(int)Bone.Head] = Bone.Neck;
            foreach (int s in new[] { 0, 1 })
            {
                Bone clav = s == 0 ? Bone.ClavicleL : Bone.ClavicleR;
                p[(int)clav] = Bone.Chest;
                p[(int)clav + 1] = clav;
                p[(int)clav + 2] = clav + 1;
                p[(int)clav + 3] = clav + 2;
                Bone thigh = s == 0 ? Bone.ThighL : Bone.ThighR;
                p[(int)thigh] = Bone.Pelvis;
                p[(int)thigh + 1] = thigh;
                p[(int)thigh + 2] = thigh + 1;
                p[(int)thigh + 3] = thigh + 2;
                Bone hand = s == 0 ? Bone.HandL : Bone.HandR;
                Bone first = s == 0 ? Bone.Thumb1L : Bone.Thumb1R;
                for (int f = 0; f < 5; f++)
                {
                    Bone b1 = first + f * 3;
                    p[(int)b1] = hand;
                    p[(int)b1 + 1] = b1;
                    p[(int)b1 + 2] = b1 + 1;
                }
            }
            return p;
        }

        public static bool IsLeft(Bone b) => b.ToString().EndsWith("L");

        /// <summary>Bind position of a joint in character space.</summary>
        public static Vector3 BindPosition(Bone bone)
        {
            switch (bone)
            {
                case Bone.Pelvis: return new Vector3(0f, HipHeight, 0f);
                case Bone.Spine: return new Vector3(0f, 1.06f, -0.012f);
                case Bone.Chest: return new Vector3(0f, 1.24f, -0.012f);
                case Bone.Neck: return new Vector3(0f, 1.49f, -0.018f);
                case Bone.Head: return new Vector3(0f, 1.62f, 0.008f);
            }
            float side = IsLeft(bone) ? -1f : 1f;
            Vector3 r = RightBind(Mirror(bone));
            return new Vector3(r.x * side, r.y, r.z);
        }

        /// <summary>The right-side equivalent of a left bone (or the bone itself).</summary>
        static Bone Mirror(Bone b)
        {
            if (!IsLeft(b)) return b;
            return (Bone)System.Enum.Parse(typeof(Bone), b.ToString().Substring(0, b.ToString().Length - 1) + "R");
        }

        static Vector3 RightBind(Bone b)
        {
            switch (b)
            {
                case Bone.ClavicleR: return new Vector3(0.025f, 1.45f, -0.008f);
                case Bone.UpperArmR: return new Vector3(0.19f, 1.44f, -0.012f);
                case Bone.ForearmR: return new Vector3(0.214f, 1.105f, -0.022f);   // + 0.335 (0.186 H)
                case Bone.HandR: return new Vector3(0.243f, 0.843f, -0.004f);      // + 0.263 (0.146 H)
                case Bone.ThighR: return new Vector3(HipHalfWidth, HipHeight, 0f);
                case Bone.ShinR: return new Vector3(HipHalfWidth, HipHeight - ThighLength, 0f);
                case Bone.FootR: return new Vector3(HipHalfWidth, AnkleHeight, 0f);
                case Bone.ToeR: return new Vector3(HipHalfWidth, 0.022f, FootToBall);
            }
            // Fingers: knuckle line ~0.095 below the wrist; phalanges hang down.
            int f = ((int)b - (int)Bone.Thumb1R) / 3, seg = ((int)b - (int)Bone.Thumb1R) % 3;
            if (f == 0)
            {
                Vector3[] thumb = { new Vector3(0.238f, 0.822f, 0.024f), new Vector3(0.236f, 0.788f, 0.046f), new Vector3(0.236f, 0.761f, 0.058f) };
                return thumb[seg];
            }
            float[] z = { 0f, 0.026f, 0.008f, -0.010f, -0.026f };
            float[][] len = { null, new[] { 0.042f, 0.025f }, new[] { 0.046f, 0.028f }, new[] { 0.043f, 0.026f }, new[] { 0.034f, 0.02f } };
            float y = 0.748f + (f == 4 ? 0.01f : 0f);
            if (seg >= 1) y -= len[f][0];
            if (seg >= 2) y -= len[f][1];
            return new Vector3(0.246f, y, z[f]);
        }

        /// <summary>Length of the last phalanx of a finger (to its tip).</summary>
        public static float FingerTipLength(int finger) => new[] { 0.024f, 0.021f, 0.023f, 0.022f, 0.019f }[finger];

        /// <summary>Creates the bone transforms under <paramref name="root"/> in bind pose, indexed by <see cref="Bone"/>.</summary>
        public static Transform[] Create(Transform root)
        {
            var bones = new Transform[BoneCount];
            for (int i = 0; i < BoneCount; i++)
            {
                var b = (Bone)i;
                var t = new GameObject(b.ToString()).transform;
                Transform parent = i == (int)Bone.Pelvis ? root : bones[(int)Parents[i]];
                t.SetParent(parent, false);
                Vector3 parentPos = i == (int)Bone.Pelvis ? Vector3.zero : BindPosition(Parents[i]);
                t.localPosition = BindPosition(b) - parentPos;
                t.localRotation = Quaternion.identity;
                bones[i] = t;
            }
            return bones;
        }

        /// <summary>Bind poses for a mesh in character space (identity bind rotations).</summary>
        public static Matrix4x4[] BindPoses()
        {
            var poses = new Matrix4x4[BoneCount];
            for (int i = 0; i < BoneCount; i++) poses[i] = Matrix4x4.Translate(-BindPosition((Bone)i));
            return poses;
        }
    }
}
