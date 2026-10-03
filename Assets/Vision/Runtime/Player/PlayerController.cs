using UnityEngine;
using UnityEngine.InputSystem;
using Vision.Characters;
using Vision.Visibility;
using Vision.World;

namespace Vision.Player
{
    /// <summary>
    /// Reads the project-wide input actions (Assets/InputSystem_Actions, "Player" map), so keyboard,
    /// mouse and gamepad all work and bindings can be rebound at runtime.
    /// Move on the ground plane, Sprint, Interact toggles the nearest door or shutter, SeeThrough toggles
    /// the see-through cone. Aim follows the pointer projected onto the ground, or the right stick
    /// while it is deflected; whichever moved last wins.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public Camera viewCamera;
        public VisionViewer viewer;
        public HumanoidAnimator animator;
        public SandboxWorld world;
        [Tooltip("World units per second. At 2x world scale this is half of the original on-screen pace.")]
        public float walkSpeed = 3.2f;
        public float runSpeed = 5.2f;
        [Tooltip("Design units; multiplied by the transform scale.")]
        public float interactRange = 2f;
        [Range(0.1f, 0.9f)] public float stickAimDeadzone = 0.35f;

        /// <summary>When set, replaces mouse aim (used by automated captures).</summary>
        public Vector2? AimOverride { get; set; }

        /// <summary>When set, replaces the move input (x = right, y = forward), for automated captures.</summary>
        public Vector2? MoveOverride { get; set; }

        /// <summary>When set, replaces the sprint input.</summary>
        public bool? SprintOverride { get; set; }

        CharacterController cc;
        float verticalSpeed;
        InputAction move, sprint, interact, seeThrough, aimPoint, aimStick;
        bool aimingWithStick;
        Vector2 lastPointer;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            InputActionAsset actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError("[Vision] No project-wide input actions asset is assigned.");
                return;
            }
            move = actions.FindAction("Player/Move", true);
            sprint = actions.FindAction("Player/Sprint", true);
            interact = actions.FindAction("Player/Interact", true);
            seeThrough = actions.FindAction("Player/SeeThrough", true);
            aimPoint = actions.FindAction("Player/AimPoint", true);
            aimStick = actions.FindAction("Player/AimStick", true);
            actions.FindActionMap("Player", true).Enable();
        }

        void Update()
        {
            if (move == null) return;
            Vector2 input = Vector2.ClampMagnitude(MoveOverride ?? move.ReadValue<Vector2>(), 1f);
            bool sprinting = SprintOverride ?? sprint.IsPressed();
            Vector2 velocity = input * (sprinting ? runSpeed : walkSpeed);
            if (interact.WasPressedThisFrame()) ToggleNearestDoor();
            if (seeThrough.WasPressedThisFrame() && viewer != null) viewer.seeThroughEnabled = !viewer.seeThroughEnabled;

            velocity *= SlopeSpeedFactor(velocity);
            // Pressed into the ground hard enough to follow a 45° descent instead of bouncing off it.
            verticalSpeed = cc.isGrounded ? -(velocity.magnitude + 1f) : verticalSpeed - 9.81f * Time.deltaTime;
            cc.Move(new Vector3(velocity.x, verticalSpeed, velocity.y) * Time.deltaTime);

            // While the pointer is on the look panel (F4) the light keeps its direction.
            Vector2 aim = AimOverride ?? (VisionDebugHud.PointerOverPanel(aimPoint.ReadValue<Vector2>()) ? Vector2.zero : ReadAim());
            if (aim.sqrMagnitude > 1e-4f)
            {
                aim.Normalize();
                if (viewer != null) viewer.Facing = aim;
            }
            if (animator != null)
            {
                Vector3 moved = cc.velocity;
                animator.Drive(new Vector3(moved.x, 0f, moved.z), viewer != null ? viewer.Facing : aim);
            }
        }

        Vector2 ReadAim()
        {
            Vector2 stick = aimStick.ReadValue<Vector2>();
            Vector2 pointer = aimPoint.ReadValue<Vector2>();
            if (stick.magnitude > stickAimDeadzone) aimingWithStick = true;
            else if ((pointer - lastPointer).sqrMagnitude > 4f) aimingWithStick = false;
            lastPointer = pointer;

            // Holding the last stick direction when it is released keeps the light where it was.
            if (aimingWithStick) return stick.magnitude > stickAimDeadzone ? stick : Vector2.zero;
            return PointerAim(pointer);
        }

        /// <summary>Slower uphill (by 35% of the sine of the climb), slightly faster downhill (at most 8%).</summary>
        public static float SlopeFactor(float grade)
        {
            float sin = Mathf.Sin(Mathf.Atan(grade));
            return sin > 0f ? 1f - 0.35f * sin : 1f + Mathf.Min(-sin, 0.5f) * 0.16f;
        }

        float SlopeSpeedFactor(Vector2 velocity)
        {
            if (velocity.sqrMagnitude < 1e-4f) return 1f;
            float scale = transform.lossyScale.x;
            Vector3 dir = new Vector3(velocity.x, 0f, velocity.y).normalized * (0.4f * scale);
            Vector3 p = transform.position;
            if (!TerrainField.TrySample(p + dir, out float ahead, out _) || !TerrainField.TrySample(p - dir, out float behind, out _)) return 1f;
            return SlopeFactor((ahead - behind) / (0.8f * scale));
        }

        Vector2 PointerAim(Vector2 screen)
        {
            if (viewCamera == null || Pointer.current == null) return Vector2.zero;
            Ray ray = viewCamera.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return Vector2.zero;
            // Aim on the horizontal plane through the player's feet, so hills do not skew the direction.
            float t = (transform.position.y - ray.origin.y) / ray.direction.y;
            Vector3 hit = ray.origin + ray.direction * t;
            return new Vector2(hit.x - transform.position.x, hit.z - transform.position.z);
        }

        public bool ToggleNearestDoor()
        {
            if (world == null) return false;
            Door best = null;
            float scale = transform.lossyScale.x;
            float bestDist = interactRange * scale;
            foreach (Door door in world.Doors)
            {
                float d = Vector3.Distance(door.blocker != null ? door.blocker.bounds.center : door.transform.position, transform.position + Vector3.up * scale);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = door;
                }
            }
            if (best == null) return false;
            best.Toggle();
            return true;
        }

        /// <summary>Moves the player instantly (CharacterController-safe), standing on the terrain when there is one.</summary>
        public void Teleport(Vector3 position)
        {
            cc.enabled = false;
            if (TerrainField.TrySample(position, out float ground, out _)) position.y = ground + 0.05f * transform.lossyScale.x;   // on the ground under the point
            transform.position = position;
            cc.enabled = true;
        }
    }
}
