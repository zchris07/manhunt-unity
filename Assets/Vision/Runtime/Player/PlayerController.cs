using UnityEngine;
using UnityEngine.InputSystem;
using Vision.Visibility;
using Vision.World;

namespace Vision.Player
{
    /// <summary>
    /// WASD movement on the ground plane, aim at the mouse (projected onto the ground), E toggles the
    /// nearest door or shutter, F toggles the see-through cone, Shift runs.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public Camera viewCamera;
        public VisionViewer viewer;
        public Transform body;
        public SandboxWorld world;
        public float walkSpeed = 3.2f;
        public float runSpeed = 5.2f;
        public float interactRange = 2f;

        /// <summary>When set, replaces mouse aim (used by automated captures).</summary>
        public Vector2? AimOverride { get; set; }

        CharacterController cc;
        float verticalSpeed;

        void Awake() => cc = GetComponent<CharacterController>();

        void Update()
        {
            Keyboard kb = Keyboard.current;
            Vector2 move = Vector2.zero;
            bool run = false;
            if (kb != null)
            {
                if (kb.wKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                run = kb.leftShiftKey.isPressed;
                if (kb.eKey.wasPressedThisFrame) ToggleNearestDoor();
                if (kb.fKey.wasPressedThisFrame && viewer != null) viewer.seeThroughEnabled = !viewer.seeThroughEnabled;
            }
            move = Vector2.ClampMagnitude(move, 1f) * (run ? runSpeed : walkSpeed);

            verticalSpeed = cc.isGrounded ? -1f : verticalSpeed - 9.81f * Time.deltaTime;
            cc.Move(new Vector3(move.x, verticalSpeed, move.y) * Time.deltaTime);

            Vector2 aim = AimOverride ?? MouseAim();
            if (aim.sqrMagnitude > 1e-4f)
            {
                aim.Normalize();
                if (viewer != null) viewer.Facing = aim;
                if (body != null) body.rotation = Quaternion.LookRotation(new Vector3(aim.x, 0f, aim.y));
            }
        }

        Vector2 MouseAim()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || viewCamera == null) return Vector2.zero;
            Ray ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return Vector2.zero;
            float t = -ray.origin.y / ray.direction.y;
            Vector3 hit = ray.origin + ray.direction * t;
            return new Vector2(hit.x - transform.position.x, hit.z - transform.position.z);
        }

        public bool ToggleNearestDoor()
        {
            if (world == null) return false;
            Door best = null;
            float bestDist = interactRange;
            foreach (Door door in world.Doors)
            {
                float d = Vector3.Distance(door.blocker != null ? door.blocker.bounds.center : door.transform.position, transform.position + Vector3.up);
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

        /// <summary>Moves the player instantly (CharacterController-safe).</summary>
        public void Teleport(Vector3 position)
        {
            cc.enabled = false;
            transform.position = position;
            cc.enabled = true;
        }
    }
}
