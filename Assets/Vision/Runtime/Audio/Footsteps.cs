using UnityEngine;
using Vision.Characters;
using Vision.Game;
using Vision.World;

namespace Vision.Audio
{
    /// <summary>
    /// A character's footsteps, on the walk cycle's footfalls: concrete in the building, boards in the cabins, grass and
    /// leaves outside; louder running, softer crouched, heavier for Zach. You always hear your own; anyone else only when
    /// they're close (the original has no footsteps, so they mustn't give away a survivor beyond arm's reach).
    /// </summary>
    public sealed class Footsteps : MonoBehaviour
    {
        /// <summary>How close (original units) someone else must be for their steps to be heard.</summary>
        public const float HearUnits = 200f;

        HumanoidAnimator anim;
        CharacterView view;
        SandboxWorld world;
        bool own;
        float lastPhase;

        void Start()
        {
            anim = GetComponent<HumanoidAnimator>();
            view = GetComponent<CharacterView>();
            world = GetComponentInParent<SandboxWorld>();
            own = GetComponentInParent<Vision.Player.PlayerController>() != null;
        }

        /// <summary>True if the phase passed <paramref name="t"/> going from <paramref name="a"/> to <paramref name="b"/> (wrapping at 1).</summary>
        public static bool Crossed(float a, float b, float t) => b >= a ? a < t && t <= b : t > a || t <= b;

        /// <summary>The step cue for the ground at a point (design units).</summary>
        public static string Surface(SandboxWorld w, Vector2 at)
        {
            if (w.Layout == null) return "step_grass";
            if (w.Layout.Building.Contains(at)) return "step_concrete";
            foreach (MapLayout.Cabin c in w.Layout.Cabins) if (c.Area.Contains(at)) return "step_wood";
            return "step_grass";
        }

        void Update()
        {
            if (anim == null || world == null || anim.Prone || !isActiveAndEnabled) return;
            GaitSolver g = anim.Solver;
            float phase = g.Phase;
            bool fell = g.Moving > 0.35f && (Crossed(lastPhase, phase, 0f) || Crossed(lastPhase, phase, 0.5f));
            lastPhase = phase;
            if (!fell) return;
            Vector3 l = world.transform.InverseTransformPoint(transform.position);
            var plane = new Vector2(l.x, l.z);
            Vector2 at = plane / Scale.Unit;
            AudioManager audio = AudioManager.Instance;
            if (!own && Vector2.Distance(at, audio.Listener) > HearUnits) return;
            bool heavy = view != null && view.Spec != null && view.Spec.Name == "Zach";
            float vol = Mathf.Lerp(0.55f, 1f, g.RunBlend) * (1f - 0.6f * Mathf.Clamp01(anim.Crouch)) * (heavy ? 1.35f : 1f) * (own ? 0.8f : 1f);
            audio.PlayCue(Surface(world, plane), at, vol);
        }
    }
}
