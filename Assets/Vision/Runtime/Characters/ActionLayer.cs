using System.Collections.Generic;
using UnityEngine;

namespace Vision.Characters
{
    /// <summary>A pose for some bones: local rotations (Euler degrees), plus a pelvis drop (metres, for crouches and kneels).</summary>
    public sealed class ActionPose
    {
        public readonly Dictionary<Bone, Vector3> Rot = new Dictionary<Bone, Vector3>();
        public float Drop;

        public ActionPose R(Bone b, float x, float y = 0f, float z = 0f)
        {
            Rot[b] = new Vector3(x, y, z);
            return this;
        }

        /// <summary>Both arms (or legs) at once, mirrored: Y and Z flip for the left side.</summary>
        public ActionPose Both(Bone right, float x, float y = 0f, float z = 0f)
        {
            Rot[right] = new Vector3(x, y, z);
            Rot[Left(right)] = new Vector3(x, -y, -z);
            return this;
        }

        public ActionPose Lower(float drop)
        {
            Drop = drop;
            return this;
        }

        public static Bone Left(Bone right) => right switch
        {
            Bone.ClavicleR => Bone.ClavicleL, Bone.UpperArmR => Bone.UpperArmL, Bone.ForearmR => Bone.ForearmL, Bone.HandR => Bone.HandL,
            Bone.ThighR => Bone.ThighL, Bone.ShinR => Bone.ShinL, Bone.FootR => Bone.FootL, Bone.ToeR => Bone.ToeL,
            _ => right,
        };

        public ActionPose Copy()
        {
            var p = new ActionPose { Drop = Drop };
            foreach (var kv in Rot) p.Rot[kv.Key] = kv.Value;
            return p;
        }
    }

    /// <summary>
    /// A keyframed action: poses at times (interpolated through with Catmull-Rom, so motion flows through the keys), the
    /// bones it owns, how it blends in and out, whether it loops, and named events (release, hit, footfall) that fire once
    /// when the clip passes them.
    /// </summary>
    public sealed class ActionClip
    {
        public string Name;
        public float Length = 1f;
        public bool Loop;
        public float BlendIn = 0.15f, BlendOut = 0.2f;
        /// <summary>Hold the last pose when a one-shot ends (until stopped) instead of blending out.</summary>
        public bool HoldEnd;
        public readonly List<(float t, ActionPose pose)> Keys = new List<(float, ActionPose)>();
        public readonly List<(float t, string name)> Events = new List<(float, string)>();
        readonly HashSet<Bone> bones = new HashSet<Bone>();

        public IEnumerable<Bone> Bones => bones;
        public bool Owns(Bone b) => bones.Contains(b);
        public bool UsesDrop { get; private set; }

        public ActionClip(string name, float length, bool loop = false)
        {
            Name = name;
            Length = Mathf.Max(0.01f, length);
            Loop = loop;
        }

        public ActionClip Key(float t, ActionPose pose)
        {
            Keys.Add((t, pose));
            Keys.Sort((a, b) => a.t.CompareTo(b.t));
            foreach (Bone b in pose.Rot.Keys) bones.Add(b);
            if (pose.Drop != 0f) UsesDrop = true;
            return this;
        }

        public ActionClip Event(float t, string name)
        {
            Events.Add((t, name));
            return this;
        }

        public ActionClip Blend(float blendIn, float blendOut)
        {
            BlendIn = blendIn;
            BlendOut = blendOut;
            return this;
        }

        static Vector3 Get(ActionPose p, Bone b) => p.Rot.TryGetValue(b, out Vector3 v) ? v : Vector3.zero;

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }

        static float CatmullRom(float p0, float p1, float p2, float p3, float u)
        {
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }

        /// <summary>The bone's rotation (Euler) and the pelvis drop at time t.</summary>
        public Vector3 Sample(Bone b, float t) => SampleWith(t, p => Get(p, b), CatmullRom);

        public float SampleDrop(float t) => SampleWith(t, p => p.Drop, CatmullRom);

        T SampleWith<T>(float t, System.Func<ActionPose, T> get, System.Func<T, T, T, T, float, T> spline)
        {
            int n = Keys.Count;
            if (n == 1) return get(Keys[0].pose);
            if (Loop) t = Mathf.Repeat(t, Length);
            else t = Mathf.Clamp(t, Keys[0].t, Keys[n - 1].t);
            int i = 0;
            if (Loop && t >= Keys[n - 1].t)
            {
                // Wrap from the last key to the first.
                float span = Length - Keys[n - 1].t + Keys[0].t;
                float u = span > 1e-5f ? (t - Keys[n - 1].t) / span : 0f;
                return spline(get(Keys[n - 2].pose), get(Keys[n - 1].pose), get(Keys[0].pose), get(Keys[1 % n].pose), u);
            }
            if (Loop && t < Keys[0].t)
            {
                float span = Length - Keys[n - 1].t + Keys[0].t;
                float u = span > 1e-5f ? (t + Length - Keys[n - 1].t) / span : 0f;
                return spline(get(Keys[n - 2].pose), get(Keys[n - 1].pose), get(Keys[0].pose), get(Keys[1 % n].pose), u);
            }
            while (i < n - 2 && t > Keys[i + 1].t) i++;
            float t0 = Keys[i].t, t1 = Keys[i + 1].t;
            float uu = t1 > t0 ? Mathf.Clamp01((t - t0) / (t1 - t0)) : 0f;
            T k1 = get(Keys[i].pose), k2 = get(Keys[i + 1].pose);
            T k0 = i > 0 ? get(Keys[i - 1].pose) : Loop ? get(Keys[n - 1].pose) : k1;
            T k3 = i + 2 < n ? get(Keys[i + 2].pose) : Loop ? get(Keys[0].pose) : k2;
            return spline(k0, k1, k2, k3, uu);
        }
    }

    /// <summary>
    /// Plays action clips on top of the procedural gait (it runs after <see cref="HumanoidAnimator"/> each frame): the
    /// clip's bones blend from the gait to the clip with an eased weight, so the legs keep walking under an upper-body
    /// action and full-body actions take over smoothly; events fire once as the clip passes them. Hits add a damped
    /// spring reaction (a flinch from the hit's direction) on the spine, chest and head. Props hang on hand sockets.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class ActionLayer : MonoBehaviour
    {
        public HumanoidAnimator animator;
        public Transform[] bones;
        /// <summary>Raised when a clip passes one of its events.</summary>
        public event System.Action<ActionClip, string> EventFired;

        ActionClip current;
        float time, weight, speed = 1f;
        // Clips still fading out (several when actions follow each other quickly), each from where it was.
        struct Fade { public ActionClip Clip; public float Time, Weight; }
        readonly List<Fade> fades = new List<Fade>(4);
        bool stopping;
        // The reaction spring: pitch (forward/back) and roll (side to side), degrees.
        Vector2 react, reactVel;

        public ActionClip Current => stopping ? null : current;
        public float Time => time;
        public float Weight => weight;
        public Vector2 Reaction => react;

        Transform socketR, socketL;
        MeshFilter propR, propL;
        public PropKind HeldRight { get; private set; }
        public PropKind HeldLeft { get; private set; }

        void Awake() => Wire();

        public void Wire()
        {
            if (animator == null) animator = GetComponent<HumanoidAnimator>();
            if (bones == null || bones.Length == 0) bones = animator != null ? animator.bones : null;
        }

        /// <summary>Plays a clip (restarting it if <paramref name="restart"/>); the one before fades out.</summary>
        public void Play(ActionClip clip, float playSpeed = 1f, bool restart = false, float startAt = 0f)
        {
            if (clip == null) { Stop(); return; }
            if (clip == current && !stopping && !restart)
            {
                speed = playSpeed;
                return;
            }
            if (current != null && weight > 0.001f)
            {
                if (fades.Count >= 3) fades.RemoveAt(0);
                fades.Add(new Fade { Clip = current, Time = time, Weight = weight });
            }
            current = clip;
            time = Mathf.Max(0f, startAt);
            weight = 0f;
            speed = playSpeed;
            stopping = false;
        }

        /// <summary>Blends the current clip out.</summary>
        public void Stop()
        {
            if (current != null) stopping = true;
        }

        public bool IsPlaying(ActionClip clip) => current == clip && !stopping;

        /// <summary>A hit from <paramref name="fromLocal"/> (body space, x right, y forward): the body flinches away.</summary>
        public void React(Vector2 fromLocal, float strength)
        {
            Vector2 d = fromLocal.sqrMagnitude > 1e-6f ? fromLocal.normalized : new Vector2(0f, 1f);
            // Hit from the front pushes the chest back (negative pitch); from the right, it rolls left.
            reactVel += new Vector2(-d.y, d.x) * (420f * strength);
        }

        // ---------------------------------------------------------------- props

        Transform Socket(bool left)
        {
            if (bones == null) return null;
            ref Transform s = ref left ? ref socketL : ref socketR;
            if (s != null) return s;
            Transform hand = bones[(int)(left ? Bone.HandL : Bone.HandR)];
            if (hand == null) return null;
            s = new GameObject(left ? "Socket L" : "Socket R").transform;
            s.SetParent(hand, false);
            // In the closed fist: a little below the wrist, toward the palm.
            s.localPosition = new Vector3(left ? 0.008f : -0.008f, -0.078f, 0.012f);
            return s;
        }

        /// <summary>Puts a prop in a hand (None empties it). The prop uses the character's material.</summary>
        public void Hold(PropKind kind, bool left = false)
        {
            if ((left ? HeldLeft : HeldRight) == kind) return;
            Transform s = Socket(left);
            if (s == null) return;
            ref MeshFilter mf = ref left ? ref propL : ref propR;
            if (mf == null)
            {
                var go = new GameObject("Prop");
                go.transform.SetParent(s, false);
                go.layer = gameObject.layer;
                mf = go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>();
                var skin = GetComponentInChildren<SkinnedMeshRenderer>();
                if (skin != null) r.sharedMaterial = skin.sharedMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            mf.sharedMesh = PropModels.Get(kind);
            mf.gameObject.SetActive(kind != PropKind.None);
            if (left) HeldLeft = kind;
            else HeldRight = kind;
        }

        public Transform PropTransform(bool left = false)
        {
            MeshFilter mf = left ? propL : propR;
            return mf != null ? mf.transform : null;
        }

        // ---------------------------------------------------------------- per frame

        void LateUpdate() => Step(UnityEngine.Time.deltaTime);

        public void Step(float dt)
        {
            if (bones == null || bones.Length < HumanoidSkeleton.BoneCount) Wire();
            if (bones == null || bones.Length < HumanoidSkeleton.BoneCount) return;
            Advance(dt);
            foreach (Fade f in fades) ApplyClip(f.Clip, f.Time, f.Weight);
            ApplyClip(current, time, weight);
            ApplyReaction(dt);
        }

        void Advance(float dt)
        {
            for (int i = fades.Count - 1; i >= 0; i--)
            {
                Fade f = fades[i];
                f.Time += dt;
                f.Weight = Mathf.MoveTowards(f.Weight, 0f, dt / Mathf.Max(0.01f, f.Clip.BlendOut));
                if (f.Weight <= 0f) fades.RemoveAt(i);
                else fades[i] = f;
            }
            if (current == null) return;
            float before = time;
            time += dt * speed;
            if (!stopping) FireEvents(current, before, time);
            bool ended = !current.Loop && time >= current.Length;
            if (ended && current.HoldEnd) time = current.Length;
            else if (ended) stopping = true;
            if (stopping)
            {
                weight = Mathf.MoveTowards(weight, 0f, dt / Mathf.Max(0.01f, current.BlendOut));
                if (weight <= 0f)
                {
                    current = null;
                    stopping = false;
                }
            }
            else weight = Mathf.MoveTowards(weight, 1f, dt / Mathf.Max(0.01f, current.BlendIn));
        }

        void FireEvents(ActionClip clip, float from, float to)
        {
            if (clip.Events.Count == 0 || to <= from) return;
            foreach (var (t, name) in clip.Events)
            {
                if (clip.Loop)
                {
                    // Every pass of the loop.
                    float a = from / clip.Length, b = to / clip.Length;
                    float e = t / clip.Length;
                    for (float k = Mathf.Floor(a); k <= Mathf.Floor(b); k++)
                        if (k + e > a && k + e <= b) EventFired?.Invoke(clip, name);
                }
                else if (t > from && t <= to) EventFired?.Invoke(clip, name);
            }
        }

        static float Ease(float w) => w * w * (3f - 2f * w);

        void ApplyClip(ActionClip clip, float t, float w)
        {
            if (clip == null || w <= 0f) return;
            float e = Ease(w);
            foreach (Bone b in clip.Bones)
            {
                Transform tr = bones[(int)b];
                if (tr == null) continue;
                Quaternion target = Quaternion.Euler(clip.Sample(b, t));
                tr.localRotation = Quaternion.Slerp(tr.localRotation, target, e);
            }
            if (clip.UsesDrop)
            {
                Transform pelvis = bones[(int)Bone.Pelvis];
                pelvis.localPosition -= Vector3.up * clip.SampleDrop(t) * e;
            }
        }

        void ApplyReaction(float dt)
        {
            // A stiff, well-damped spring: a quick flinch that settles in about half a second. Integrated in small
            // semi-implicit steps, so a long frame (a hitch while a sound loads) can't blow it up and leave the arms flung
            // into a pose they never come back from; and kept within a believable flinch.
            const float k = 180f, c = 22f, step = 1f / 240f, limit = 35f;
            if (float.IsNaN(react.x) || float.IsNaN(react.y) || float.IsNaN(reactVel.x) || float.IsNaN(reactVel.y)) { react = reactVel = Vector2.zero; }
            reactVel = Vector2.ClampMagnitude(reactVel, 900f);
            for (float left = Mathf.Min(dt, 0.25f); left > 1e-6f; left -= step)
            {
                float h = Mathf.Min(step, left);
                reactVel += (-k * react - c * reactVel) * h;
                react += reactVel * h;
            }
            react = Vector2.ClampMagnitude(react, limit);
            if (react.sqrMagnitude < 1e-4f && reactVel.sqrMagnitude < 1e-3f) { react = Vector2.zero; reactVel = Vector2.zero; return; }
            Quaternion q(float s) => Quaternion.Euler(react.x * s, 0f, react.y * s);
            bones[(int)Bone.Spine].localRotation *= q(0.35f);
            bones[(int)Bone.Chest].localRotation *= q(0.4f);
            bones[(int)Bone.Head].localRotation *= q(0.5f);
            bones[(int)Bone.UpperArmL].localRotation *= Quaternion.Euler(react.x * 0.5f, 0f, -Mathf.Abs(react.y) * 0.4f);
            bones[(int)Bone.UpperArmR].localRotation *= Quaternion.Euler(react.x * 0.5f, 0f, Mathf.Abs(react.y) * 0.4f);
        }
    }
}
