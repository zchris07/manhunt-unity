using UnityEngine;
using Vision.Characters;

namespace Vision.World
{
    /// <summary>A figure that walks a loop of waypoints (for testing entity occlusion), animated by its gait.</summary>
    public sealed class Wanderer : MonoBehaviour
    {
        public Vector3[] waypoints;
        [Tooltip("World units per second (the player's walking pace).")]
        public float speed = 3.2f;
        public HumanoidAnimator animator;
        [Tooltip("Damage when it walks into the player (design units of reach), at most once per cooldown.")]
        public float touchDamage = 10f;
        public float touchReach = 0.6f;
        public float touchCooldown = 1f;

        Vision.Player.PlayerStats target;
        float nextTouch;

        int next;
        Vector3 heading = Vector3.forward;

        void Update()
        {
            Vector3 velocity = Vector3.zero;
            if (waypoints != null && waypoints.Length > 0)
            {
                Vector3 to = waypoints[next] - transform.position;
                to.y = 0f;
                if (to.magnitude < 0.15f) next = (next + 1) % waypoints.Length;
                else
                {
                    Vector3 step = Vector3.ClampMagnitude(to.normalized * speed * Time.deltaTime, to.magnitude);
                    Vector3 next3 = transform.position + step;
                    next3.y = TerrainField.WorldHeight(next3, next3.y);
                    transform.position = next3;
                    velocity = to.normalized * speed;
                    heading = Vector3.Slerp(heading, to.normalized, 1f - Mathf.Exp(-8f * Time.deltaTime));
                }
            }
            if (animator != null) animator.Drive(velocity, new Vector2(heading.x, heading.z));
            Touch();
        }

        void Touch()
        {
            if (touchDamage <= 0f || Time.time < nextTouch) return;
            if (target == null) target = FindAnyObjectByType<Vision.Player.PlayerStats>();
            if (target == null) return;
            Vector3 d = target.transform.position - transform.position;
            d.y = 0f;
            if (d.magnitude > touchReach * transform.lossyScale.x) return;
            target.vitals.TakeDamage(touchDamage);
            nextTouch = Time.time + touchCooldown;
        }

        void OnDisable()
        {
            if (animator != null) animator.Drive(Vector3.zero, new Vector2(heading.x, heading.z));
        }
    }
}
