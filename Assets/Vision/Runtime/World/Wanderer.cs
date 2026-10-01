using UnityEngine;

namespace Vision.World
{
    /// <summary>Dummy dynamic entity that walks a loop of waypoints, for testing entity occlusion.</summary>
    public sealed class Wanderer : MonoBehaviour
    {
        public Vector3[] waypoints;
        public float speed = 1.4f;
        public float bobAmount = 0.04f;
        public Transform body;

        int next;
        float bob;

        void Update()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            Vector3 target = waypoints[next];
            Vector3 pos = transform.position;
            Vector3 to = target - pos;
            to.y = 0f;
            if (to.magnitude < 0.1f)
            {
                next = (next + 1) % waypoints.Length;
                return;
            }
            Vector3 step = to.normalized * speed * Time.deltaTime;
            transform.position = pos + Vector3.ClampMagnitude(step, to.magnitude);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 6f * Time.deltaTime);
            bob += Time.deltaTime * speed * 6f;
            if (body != null) body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(bob)) * bobAmount, 0f);
        }
    }
}
