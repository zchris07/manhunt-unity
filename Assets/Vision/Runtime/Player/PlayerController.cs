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

        /// <summary>What Interact would do right now ("Pick up Bandage", "Open door"), or null.</summary>
        public string InteractPrompt { get; private set; }

        /// <summary>Short messages for the HUD (pickups, items used, a full inventory).</summary>
        public event System.Action<string> Notice;

        PlayerStats stats;

        CharacterController cc;
        float verticalSpeed;
        InputAction move, sprint, interact, seeThrough, aimPoint, aimStick;
        bool aimingWithStick;
        Vector2 lastPointer;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            stats = GetComponent<PlayerStats>();
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
            bool dead = stats != null && stats.vitals.IsDead;
            if (GameHud.MenuOpen || dead)
            {
                InteractPrompt = null;
                if (animator != null) animator.Drive(Vector3.zero, viewer != null ? viewer.Facing : Vector2.up);
                if (dead && cc.enabled) cc.Move(Vector3.down * (9.81f * Time.deltaTime));
                return;
            }
            Vector2 input = Vector2.ClampMagnitude(MoveOverride ?? move.ReadValue<Vector2>(), 1f);
            bool wantsSprint = (SprintOverride ?? sprint.IsPressed()) && input.sqrMagnitude > 0.01f;
            bool sprinting = wantsSprint && (stats == null || stats.vitals.CanSprint);
            if (stats != null) stats.vitals.Tick(Time.deltaTime, sprinting);
            Vector2 velocity = input * (sprinting ? runSpeed : walkSpeed);
            UpdateInteraction(interact.WasPressedThisFrame());
            if (seeThrough.WasPressedThisFrame() && viewer != null) viewer.seeThroughEnabled = !viewer.seeThroughEnabled;
            UseItemKeys();

            float grade = Grade(velocity);
            velocity *= SlopeFactor(grade);
            // Movement follows the ground: the climb or drop along the way is added to the step, so any grade can be
            // walked up or down without the controller blocking or the player leaving the ground.
            if (cc.isGrounded || grade != 0f)
                verticalSpeed = grade * velocity.magnitude - 1.5f * transform.lossyScale.x;
            else
                verticalSpeed -= 9.81f * Time.deltaTime;
            cc.Move(new Vector3(velocity.x, verticalSpeed, velocity.y) * Time.deltaTime);
            KeepOnGround();

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

        /// <summary>
        /// Slower uphill (by 35% of the sine of the climb, never below 65% of the pace, so even a very steep climb keeps
        /// moving), slightly faster downhill (at most 8%).
        /// </summary>
        public static float SlopeFactor(float grade)
        {
            float sin = Mathf.Sin(Mathf.Atan(grade));
            return sin > 0f ? Mathf.Max(0.65f, 1f - 0.35f * sin) : 1f + Mathf.Min(-sin, 0.5f) * 0.16f;
        }

        /// <summary>Rise over run of the ground along the direction of travel (0 on flat ground or without terrain).</summary>
        float Grade(Vector2 velocity)
        {
            if (velocity.sqrMagnitude < 1e-4f) return 0f;
            float scale = transform.lossyScale.x;
            Vector3 dir = new Vector3(velocity.x, 0f, velocity.y).normalized * (0.4f * scale);
            Vector3 p = transform.position;
            if (!TerrainField.TrySample(p + dir, out float ahead, out _) || !TerrainField.TrySample(p - dir, out float behind, out _)) return 0f;
            return (ahead - behind) / (0.8f * scale);
        }

        /// <summary>Never below the terrain (a steep facet can push the capsule a little under it).</summary>
        void KeepOnGround()
        {
            if (!TerrainField.TrySample(transform.position, out float ground, out _)) return;
            if (transform.position.y >= ground - 0.02f * transform.lossyScale.x) return;
            cc.enabled = false;
            transform.position = new Vector3(transform.position.x, ground, transform.position.z);
            cc.enabled = true;
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

        void UseItemKeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || stats == null) return;
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6 };
            for (int i = 0; i < keys.Length; i++)
            {
                if (!kb[keys[i]].wasPressedThisFrame) continue;
                ItemType? item = stats.inventory.ItemAt(i);
                if (item == null) continue;
                ItemInfo info = Items.Info(item.Value);
                if (stats.UseSlot(i))
                    Notice?.Invoke(info.heal > 0f && info.stamina > 0f ? $"Ate {info.name.ToLower()}" : info.heal > 0f ? $"Used a bandage (+{info.heal:0} health)" : $"Drank water (+{info.stamina:0} stamina)");
                else Notice?.Invoke(info.heal > 0f ? "Already at full health" : "Already rested");
            }
        }

        /// <summary>The nearest thing to interact with in reach: a pickup or a door (whichever is closer).</summary>
        void UpdateInteraction(bool pressed)
        {
            float scale = transform.lossyScale.x;
            Vector3 from = transform.position + Vector3.up * (0.5f * scale);
            float best = interactRange * scale;
            Pickup pickup = null;
            foreach (Pickup p in Pickup.All)
            {
                float d = Vector3.Distance(p.transform.position, from);
                if (d < best) { best = d; pickup = p; }
            }
            Door door = null;
            if (world != null)
                foreach (Door dr in world.Doors)
                {
                    float d = Vector3.Distance(dr.blocker != null ? dr.blocker.bounds.center : dr.transform.position, transform.position + Vector3.up * scale);
                    if (d < best) { best = d; door = dr; pickup = null; }
                }

            if (door != null) InteractPrompt = $"{(door.IsOpen ? "Close" : "Open")} {(door.blocksMovementWhenOpen ? "shutter" : "door")}";
            else if (pickup != null) InteractPrompt = $"Pick up {pickup.Label}";
            else InteractPrompt = null;
            if (!pressed) return;

            if (door != null) door.Toggle();
            else if (pickup != null && stats != null)
            {
                string label = pickup.Label;
                int taken = pickup.TakeInto(stats.inventory);
                Notice?.Invoke(taken > 0 ? $"Picked up {label}" : "Inventory full");
            }
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
