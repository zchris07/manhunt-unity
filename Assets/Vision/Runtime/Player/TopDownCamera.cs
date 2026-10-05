using UnityEngine;

namespace Vision.Player
{
    /// <summary>
    /// Orthographic 2.5D top-down camera, pitched ~60° so the fronts of walls, trunks and characters
    /// show clearly. Follows the target with smoothing and keeps it at the centre of the view.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public sealed class TopDownCamera : MonoBehaviour
    {
        public Transform target;
        [Range(45f, 90f)] public float pitch = 60f;
        [Tooltip("Distance back along the view direction. Ortho, so it only affects clipping and how much " +
                 "of the shadow distance is wasted on empty air; keep it just above the tallest geometry.")]
        public float distance = 32f;
        [Tooltip("Half the view height at the reference screen height. Taller screens see more, so assets keep their on-screen size.")]
        public float orthographicSize = 7.2f;
        [Tooltip("Screen height in pixels at which the view is exactly orthographicSize.")]
        public float referenceHeight = 900f;
        public float smoothTime = 0.12f;
        [Tooltip("Extra smoothing of the camera's height, so climbing or dropping over steep ground never jolts the view.")]
        public float heightSmoothTime = 0.35f;

        Camera cam;
        Vector3 velocity;
        float groundY, groundVelocity;

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
            groundY = target.position.y;
            groundVelocity = 0f;
            transform.position = Desired();
            velocity = Vector3.zero;
        }

        void LateUpdate()
        {
            if (target == null) return;
            Apply();
            groundY = Mathf.SmoothDamp(groundY, target.position.y, ref groundVelocity, heightSmoothTime);
            transform.position = Vector3.SmoothDamp(transform.position, Desired(), ref velocity, smoothTime);
        }

        void Apply()
        {
            if (cam == null) cam = GetComponent<Camera>();   // the level may snap the camera before this Awake runs
            cam.orthographicSize = SizeFor(orthographicSize, Screen.height, referenceHeight);
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>Pixels per world unit stay constant: the view grows with the screen instead of zooming.</summary>
        public static float SizeFor(float referenceSize, float screenHeight, float referenceHeight) =>
            referenceSize * Mathf.Max(1f, screenHeight) / Mathf.Max(1f, referenceHeight);

        Vector3 Desired()
        {
            Vector3 focus = target.position;
            focus.y = groundY;   // follow the ground the player stands on, eased
            return focus - transform.forward * distance;
        }
    }
}
