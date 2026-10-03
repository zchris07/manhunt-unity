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
        }

        void OnDisable()
        {
            if (animator != null) animator.Drive(Vector3.zero, new Vector2(heading.x, heading.z));
        }
    }
}
