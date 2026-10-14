using UnityEngine;

namespace Vision.Player
{
    /// <summary>
    /// Orthographic 2.5D top-down camera, pitched 55° (5° flatter than the first 60°) so the fronts of walls, trunks and characters
    /// show clearly. Follows the target with smoothing and keeps it at the centre of the view. Z and C turn the view 45°
    /// at a time (eased over a quarter of a second), so walls never run square to the screen and the doors and windows
    /// on every side of a building show; each match starts turned 45 degrees off the world's grid, walls diagonal.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public sealed class TopDownCamera : MonoBehaviour
    {
        public Transform target;
        [Range(45f, 90f)] public float pitch = 55f;
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

        [Tooltip("Zoom multiplier on the view size (the Hemp Battery zooms out; Waz changes the field of view).")]
        public float zoom = 1f;

        /// <summary>Testing mode: the mouse wheel's own zoom on top (1 outside testing).</summary>
        public static float TestZoom = 1f;
        public const float TestZoomMin = 0.35f, TestZoomMax = 3.2f;

        /// <summary>The step one press of Z or C turns the view, and how long the turn takes.</summary>
        public const float TurnStep = 45f, TurnTime = 0.25f;
        /// <summary>
        /// The view a match starts with: turned 45 degrees off the world's grid, so walls and buildings run diagonally
        /// across the screen (the map's north and the compass stay on the world's grid; Z / C turn it from here).
        /// </summary>
        public const float DefaultYaw = 45f;
        float yawTarget = DefaultYaw, yaw = DefaultYaw, yawVelocity;

        /// <summary>The view's turn about the vertical (degrees clockwise from north, as drawn now; eased toward the target).</summary>
        public float Yaw => yaw;
        public float YawTarget => yawTarget;

        /// <summary>Turns the view by whole steps (negative: left), easing round; presses queue up.</summary>
        public void Turn(int steps) => yawTarget += steps * TurnStep;

        /// <summary>Back to the starting view at once (a new match): 45 degrees off the world's grid.</summary>
        public void ResetYaw()
        {
            yawTarget = yaw = DefaultYaw;
            yawVelocity = 0f;
        }

        Camera cam;
        Vector3 velocity, smoothed;
        float groundY, groundVelocity;
        float shake;

        /// <summary>Shakes the view by up to <paramref name="pixels"/> screen pixels (the original's shake), decaying at 30 px/s.</summary>
        public void Shake(float pixels) => shake = Mathf.Max(shake, pixels);
        public float ShakeAmount => shake;

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
            transform.position = smoothed = Desired();
            velocity = Vector3.zero;
        }

        void LateUpdate()
        {
            if (target == null) return;
            // The turn eases round in about a quarter of a second (SmoothDamp reaches the target in roughly 3x its time).
            yaw = Mathf.SmoothDampAngle(yaw, yawTarget, ref yawVelocity, TurnTime / 3f, Mathf.Infinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, yawTarget)) < 0.05f) { yaw = yawTarget; yawVelocity = 0f; }
            Apply();
            groundY = Mathf.SmoothDamp(groundY, target.position.y, ref groundVelocity, heightSmoothTime);
            // Faster paces (and speed mode, six times as fast) follow tighter, so the lag stays the same distance and the
            // player stays near the centre.
            float follow = smoothTime * Mathf.Clamp(Vision.Game.Scale.HumanPace / Mathf.Max(0.01f, Vision.Game.Scale.Pace), 0.3f, 1f);
            smoothed = Vector3.SmoothDamp(smoothed, Desired(), ref velocity, GameSession.SpeedMode ? follow * 0.3f : follow);
            shake = Mathf.Max(0f, shake - Time.unscaledDeltaTime * 30f);
            Vector3 jolt = Vector3.zero;
            if (shake > 0.3f && Screen.height > 0)
            {
                float unitsPerPixel = cam.orthographicSize * 2f / Screen.height;
                jolt = (transform.right * (Random.value - 0.5f) + transform.up * (Random.value - 0.5f)) * shake * unitsPerPixel;
            }
            transform.position = smoothed + jolt;
        }

        void Apply()
        {
            if (cam == null) cam = GetComponent<Camera>();   // the level may snap the camera before this Awake runs
            cam.orthographicSize = SizeFor(orthographicSize, Screen.height, referenceHeight) * Mathf.Clamp(zoom, 0.3f, 3f) * TestZoom;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        /// <summary>
        /// A move or aim stick direction as the screen shows it (x right, y up the screen) to the ground plane (x east, y
        /// north): up the screen is wherever the view is turned to.
        /// </summary>
        public static Vector2 ScreenToGround(Vector2 v, float yawDeg)
        {
            float a = yawDeg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c + v.y * s, -v.x * s + v.y * c);
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
