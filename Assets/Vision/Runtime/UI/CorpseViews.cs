using System.Collections.Generic;
using UnityEngine;
using Vision.Characters;
using Vision.Game;
using Vision.Visibility;
using Vision.World;

namespace Vision.UI
{
    /// <summary>
    /// The bodies lying about (<see cref="MatchSim.Bodies"/>): a sacrificed survivor's or a slain NPC's, each in its own look,
    /// on its back where it fell. Dropped (a death, a ghost knocked out of it) it topples over backwards. Taken by a ghost, it
    /// levitates: it rises off the ground and swings upright into an A pose (arms held a little out from its sides), turned
    /// to face the ghost, who floats up level with it in the same pose; a pale glow grows about it and wisps of light spiral
    /// into it while the ghost drifts slowly into it, until they're one and settle to the ground together; then it's the
    /// player's own figure that walks on in its look. Like any entity it shows only in the viewer's light (its glow lights it
    /// up as it rises).
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class CorpseViews : MonoBehaviour
    {
        public SandboxWorld world;

        public sealed class View
        {
            public int Id;
            public string Look;
            public GameObject Pivot, Go;
            public HumanoidAnimator Animator;
            public Renderer[] Renderers;
            public VisionLight Glow;
            public bool Shown = true;
            /// <summary>How far over it's lying (0 upright, 1 flat on its back), and how high it floats (local units), as drawn.</summary>
            public float Over, Lift;
            /// <summary>Its time since it fell or was taken, run on smoothly between the match's ticks.</summary>
            public float T;
            public ActionLayer Layer;
            /// <summary>Rising (in its A pose), which way it's turned (degrees), and the wisps owed.</summary>
            public bool Rising;
            public float Yaw = float.NaN, Wisps;
        }

        readonly Dictionary<int, View> views = new Dictionary<int, View>();
        readonly List<int> stale = new List<int>();
        readonly HashSet<int> seen = new HashSet<int>();

        public IReadOnlyDictionary<int, View> All => views;
        public View For(int id) => views.TryGetValue(id, out View v) ? v : null;

        static readonly Dictionary<string, CharacterSpec> specs = new Dictionary<string, CharacterSpec>();

        static CharacterSpec SpecOf(string look)
        {
            look = string.IsNullOrEmpty(look) ? "Survivor" : look;
            if (!specs.TryGetValue(look, out CharacterSpec s)) specs[look] = s = look == "Survivor" ? CharacterSpec.Survivor() : CharacterSpec.Npc(look);
            return s;
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        void LateUpdate() => Sync(Time.deltaTime);

        public void Sync(float dt)
        {
            if (world == null) world = GetComponent<SandboxWorld>();
            MatchHost host = world != null ? MatchHost.For(world) : null;
            MatchSim sim = host != null ? host.Sim : null;
            seen.Clear();
            if (sim != null)
                foreach (Body b in sim.Bodies)
                {
                    seen.Add(b.Id);
                    if (!views.TryGetValue(b.Id, out View v) || v.Pivot == null || v.Look != b.Look)
                    {
                        if (v != null) Kill(v.Pivot);
                        v = Create(b);
                    }
                    Drive(v, b, sim, dt);
                }
            stale.Clear();
            foreach (var kv in views) if (!seen.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (int id in stale)
            {
                Kill(views[id].Pivot);
                views.Remove(id);
            }
        }

        View Create(Body b)
        {
            var pivot = new GameObject("Body: " + b.Name);
            pivot.transform.SetParent(world.transform, false);
            GameObject go = PropFactory.CreateCharacter(b.Name, world.entityMaterial, null);
            go.transform.SetParent(pivot.transform, false);
            go.layer = SandboxWorld.CharacterLayer;
            // A body makes no footsteps; its pose is stepped here, upright, before it's laid over (so the feet never reach
            // for the ground at an angle).
            var steps = go.GetComponent<Vision.Audio.Footsteps>();
            if (steps != null) Kill(steps);
            go.GetComponent<CharacterView>().SetSpec(SpecOf(b.Look));
            var anim = go.GetComponent<HumanoidAnimator>();
            anim.enabled = false;
            var layer = go.GetComponent<ActionLayer>();
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(pivot.transform, false);
            var glow = glowGo.AddComponent<VisionLight>();
            glow.isStatic = false;
            glow.range = Scale.D(120f);
            glow.intensity = 0f;
            glow.height = 1.1f;
            glow.flickerAmount = 0.08f;
            glow.enabled = false;
            var v = new View { Id = b.Id, Look = b.Look, Pivot = pivot, Go = go, Animator = anim, Renderers = go.GetComponentsInChildren<Renderer>(true), Glow = glow, Layer = layer };
            views[b.Id] = v;
            return v;
        }

        /// <summary>
        /// Where a body is in its fall or its rise, <paramref name="t"/> s after it last fell or was taken: how far over it
        /// is (1 flat on its back), how high it floats (local units), how bright its glow, and whether it's drawn at all
        /// (not once the ghost is one with it).
        /// </summary>
        public static void Pose(bool taken, float t, out float over, out float lift, out float glow, out bool shown)
        {
            shown = true;
            glow = 0f;
            lift = 0f;
            if (!taken)
            {
                // Toppling over backwards, quickening as it goes.
                float k = Mathf.Clamp01(t / Balance.Ghost.FallTime);
                over = k * k;
                return;
            }
            float m = t / Balance.Ghost.PossessTime;
            if (m >= 1f)
            {
                shown = false;
                over = 0f;
                return;
            }
            m = Mathf.Clamp01(m);
            // Up off the ground, swinging upright over the first two fifths, hovering while the ghost drifts into it, and
            // settling onto its feet as they become one; the glow grows the whole way.
            over = 1f - Smooth(0f, 0.4f, m);
            lift = PossessLift(m);
            glow = Smooth(0f, 0.85f, m);
        }

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        /// <summary>How far through taking a body (0-1) a ghost is with <paramref name="possessLeft"/> s of it to go.</summary>
        public static float PossessProgress(float possessLeft) => Mathf.Clamp01(1f - possessLeft / Balance.Ghost.PossessTime);

        /// <summary>
        /// How high (metres) the body and the ghost taking it hover, <paramref name="m"/> of the way through: up over its
        /// first third, held, and down to the ground over its last seventh as they become one.
        /// </summary>
        public static float PossessLift(float m) => 0.5f * Smooth(0f, 0.33f, m) * (1f - Smooth(0.86f, 1f, m));

        /// <summary>How far (0-1) the ghost has drifted from where it stood into the body, <paramref name="m"/> of the way through.</summary>
        public static float PossessGlide(float m) => Smooth(0.3f, 0.88f, m);

        void Drive(View v, Body b, MatchSim sim, float dt)
        {
            // (The match moves on in ticks: run the time on smoothly between them, never far from it.)
            float simT = sim.Time - b.Since;
            v.T = Mathf.Clamp(v.T + dt, simT, simT + 0.05f);
            Pose(b.Ghost != 0, v.T, out float over, out float lift, out float glow, out bool shown);
            v.Over = over;
            v.Lift = lift;
            if (shown != v.Shown)
            {
                v.Shown = shown;
                foreach (Renderer r in v.Renderers) if (r != null) r.enabled = shown;
            }
            bool lit = shown && glow > 0.01f;
            if (v.Glow.enabled != lit) v.Glow.enabled = lit;
            if (lit) v.Glow.intensity = 0.9f * glow;
            // Rising: the A pose; lying: limp.
            bool rising = shown && b.Ghost != 0;
            if (rising != v.Rising && v.Layer != null)
            {
                v.Rising = rising;
                if (rising) v.Layer.Play(ActionClips.APose, 1f, true);
                else v.Layer.Stop();
            }
            if (!shown) return;
            // Turned as it fell; rising, round to face the ghost coming to it.
            float yaw = Mathf.Atan2(Mathf.Cos(b.Facing), Mathf.Sin(b.Facing)) * Mathf.Rad2Deg;
            if (b.Ghost != 0)
            {
                SimPlayer g = sim.Get(b.Ghost);
                Vector2 to = g != null ? g.Pos - b.Pos : Vector2.zero;
                if (float.IsNaN(v.Yaw)) v.Yaw = yaw;
                if (to.sqrMagnitude > 0.04f) v.Yaw = Mathf.MoveTowardsAngle(v.Yaw, Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg, 120f * dt);
                yaw = v.Yaw;
                Wisps(v, b, over, lift, dt);
            }
            else v.Yaw = float.NaN;
            Vector3 at = new Vector3(b.Pos.x, world.GroundHeight(b.Pos), b.Pos.y);
            // The pose stepped upright where it stands...
            v.Pivot.transform.localPosition = at;
            v.Pivot.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            v.Animator.Drive(Vector3.zero, Vector2.up);
            v.Animator.Step(Mathf.Max(dt, 1f / 60f));
            // ...then the whole figure laid over backwards about its feet (raised by the body's depth so its back rests on
            // the ground), and floated up as it rises.
            v.Pivot.transform.localPosition = at + Vector3.up * (0.13f * over + lift);
            v.Pivot.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-90f * over, 0f, 0f);
        }

        /// <summary>Wisps of pale light spiralling into a body as a ghost merges with it (the middle of the rise).</summary>
        void Wisps(View v, Body b, float over, float lift, float dt)
        {
            float m = Mathf.Clamp01(v.T / Balance.Ghost.PossessTime);
            if (m < 0.25f || m > 0.92f || Vision.Effects.Vfx.Instance == null) return;
            v.Wisps += dt * Mathf.Lerp(18f, 46f, Mathf.InverseLerp(0.25f, 0.88f, m));
            float s = world.transform.lossyScale.x;
            Vector3 centre = world.transform.TransformPoint(new Vector3(b.Pos.x, world.GroundHeight(b.Pos) + lift + 1.05f * (1f - over), b.Pos.y));
            while (v.Wisps >= 1f)
            {
                v.Wisps -= 1f;
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(0.7f, 1.1f) * s;
                var off = new Vector3(Mathf.Cos(a) * r, Random.Range(-0.7f, 0.7f) * s, Mathf.Sin(a) * r);
                // In toward the body and round it.
                Vector3 vel = (-off.normalized * 1.3f + new Vector3(-Mathf.Sin(a), 0.15f, Mathf.Cos(a)) * 1.1f) * s;
                Vision.Effects.Vfx.Instance.Emit(centre + off, vel, Random.Range(0.45f, 0.7f), new Color(0.82f, 0.9f, 1f, 0.9f), Random.Range(0.03f, 0.06f) * s, 0f, 1.8f, true);
            }
        }

        void OnDestroy()
        {
            foreach (View v in views.Values) Kill(v.Pivot);
            views.Clear();
        }
    }
}
