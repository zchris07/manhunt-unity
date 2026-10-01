using UnityEngine;

namespace Vision.Player
{
    /// <summary>
    /// Orthographic 2.5D top-down camera, pitched ~70° so the fronts of walls, trunks and characters
    /// show. Follows the target with smoothing and keeps it at the centre of the view.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public sealed class TopDownCamera : MonoBehaviour
    {
        public Transform target;
        [Range(45f, 90f)] public float pitch = 70f;
        [Tooltip("Distance back along the view direction. Ortho, so it only affects clipping and how much " +
                 "of the shadow distance is wasted on empty air; keep it just above the tallest geometry.")]
        public float distance = 16f;
        public float orthographicSize = 7.2f;
        public float smoothTime = 0.12f;

        Camera cam;
        Vector3 velocity;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
        }

        void Start() => Snap();

        public void Snap()
        {
            if (target == null) return;
            Apply();
            transform.position = Desired();
            velocity = Vector3.zero;
        }

        void LateUpdate()
        {
            if (target == null) return;
            Apply();
            transform.position = Vector3.SmoothDamp(transform.position, Desired(), ref velocity, smoothTime);
        }

        void Apply()
        {
            cam.orthographicSize = orthographicSize;
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        Vector3 Desired()
        {
            Vector3 focus = target.position;
            focus.y = 0f;
            return focus - transform.forward * distance;
        }
    }
}
