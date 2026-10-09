using UnityEngine;
using UnityEngine.InputSystem;
using Vision.Characters;
using Vision.Game;
using Vision.Visibility;
using Vision.World;

namespace Vision.Player
{
    /// <summary>
    /// The local player's avatar. Reads the project-wide input actions (keyboard and mouse first, a gamepad too) into the
    /// original's input frame, which the match rules apply (<see cref="MatchHost"/>). Movement is the player's own: the
    /// original's movement step (sprint meter, crouch, crawl, lunge, knockback, slows) runs here and the controller carries
    /// it out against the level's colliders, then reports where the avatar is. When the rules move the player (hiding, being
    /// carried or staked, released) the avatar is put there. Also sets the flashlight for the role and the HUD's prompt.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public Camera viewCamera;
        public VisionViewer viewer;
        public HumanoidAnimator animator;
        public SandboxWorld world;
        [Range(0.1f, 0.9f)] public float stickAimDeadzone = 0.35f;

        /// <summary>When set, replaces mouse aim (used by automated captures).</summary>
        public Vector2? AimOverride { get; set; }

        /// <summary>When set, replaces the move input (x = east, y = north), for automated captures.</summary>
        public Vector2? MoveOverride { get; set; }

        /// <summary>When set, replaces the sprint input.</summary>
        public bool? SprintOverride { get; set; }

        /// <summary>Buttons held down by a capture or test, added to the real input.</summary>
        public Btn ExtraButtons { get; set; }

        /// <summary>Where the player is hiding (null when not hidden).</summary>
        public HidingSpot Hidden
        {
            get
            {
                SimPlayer p = Me;
                return p != null && p.HideSpot >= 0 && world != null && p.HideSpot < world.HidingSpots.Count && p.HideState >= 1 ? world.HidingSpots[p.HideSpot] : null;
            }
        }

        /// <summary>What Interact would do right now ("Hold E to start the generator"), or null.</summary>
        public string InteractPrompt { get; private set; }

        /// <summary>The Space prompt (slam a barricade), or null.</summary>
        public string SpacePrompt { get; private set; }

        /// <summary>Progress (0 to 1) of the timed thing being done (a generator, the lever, a revive, a drink...), or -1.</summary>
        public float HoldProgress { get; private set; } = -1f;

        /// <summary>The selected inventory slot (0-based; -1 none).</summary>
        public int SelectedSlot { get; set; } = -1;

        /// <summary>Short personal messages for the HUD (pickups, items used).</summary>
        public event System.Action<string> Notice;

        public MatchHost Host => host != null ? host : host = MatchHost.For(world);
        public SimPlayer Me => Host != null ? Host.Local : null;

        MatchHost host;
        CharacterController cc;
        float verticalSpeed;
        InputAction move, sprint, interact, crouch, aimPoint, aimStick, fire, lunge, burst, ability, beam, special, drop, yes, no, switchRole, slotScroll, slotNext, slotPrev;
        readonly InputAction[] slotKeys = new InputAction[Inventory.TestingSlots];
        bool aimingWithStick;
        Vector2 lastPointer;
        uint seq;
        int placeSeen = -1;
        bool hiddenShown;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            InputActionAsset actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError("[Vision] No project-wide input actions asset is assigned.");
                return;
            }
            InputAction A(string name) => actions.FindAction("Player/" + name, false);
            move = A("Move");
            sprint = A("Sprint");
            interact = A("Interact");
            crouch = A("Crouch");
            aimPoint = A("AimPoint");
            aimStick = A("AimStick");
            fire = A("Fire");
            lunge = A("Lunge");
            burst = A("Burst");
            ability = A("Ability");
            beam = A("Beam");
            special = A("Special");
            drop = A("Drop");
            yes = A("Yes");
            no = A("No");
            switchRole = A("SwitchRole");
            slotScroll = A("SlotScroll");
            slotNext = A("SlotNext");
            slotPrev = A("SlotPrev");
            for (int i = 0; i < slotKeys.Length; i++) slotKeys[i] = A("Slot" + (i + 1));
            actions.FindActionMap("Player", true).Enable();
            gameObject.layer = SandboxWorld.CharacterLayer;
        }

        void OnEnable()
        {
            if (Host != null) Host.EventRaised += OnEvent;
        }

        void OnDisable()
        {
            if (host != null) host.EventRaised -= OnEvent;
        }

        void OnEvent(GameEvent e)
        {
            if (e.Kind == EventKind.Item && !string.IsNullOrEmpty(e.Text)) Notice?.Invoke(e.Text);
            if (view != null && host != null && host.Sim != null) view.OnEvent(e, Me, host.Sim);
        }

        CharacterView view;

        /// <summary>The model for the role (survivor or Zach) and the action clips for what the player is doing.</summary>
        void PresentCharacter(SimPlayer p)
        {
            if (view == null) view = GetComponent<CharacterView>();
            if (view == null) return;
            view.SetSpec(p.Role == Role.Hunter ? CharacterSpec.Zach() : CharacterSpec.Survivor());
            view.Present(p);
        }

        static bool Pressed(InputAction a) => a != null && a.WasPressedThisFrame();
        static bool Held(InputAction a) => a != null && a.IsPressed();

        void Update()
        {
            if (move == null) return;
            MatchHost h = Host;
            SimPlayer p = Me;
            if (h == null || p == null) return;
            float dt = Time.deltaTime;
            bool menu = GameHud.MenuOpen;

            // Testing mode: T switches between Zach and a survivor where you stand.
            if (!menu && Pressed(switchRole) && h.Sim.TestMode && h.SwitchRole())
            {
                p = Me;
                Notice?.Invoke(p.Role == Role.Hunter ? "You are Zach" : "You are a survivor");
                RoleChanged?.Invoke(p.Role);
            }
            // Testing: downed survivors can stand back up with R.
            if (!menu && h.Sim.TestMode && !h.Remote && p.Health == Game.Health.Downed && Pressed(beam))
            {
                MatchSim.RestoreSurvivor(p, Balance.Survivor.ReviveHp);
                Notice?.Invoke("Back on your feet");
            }

            InputCmd cmd = ReadInput(p, menu);
            h.Sim.SubmitInput(p.Id, cmd);

            // The rules moved the player (or they are hidden, carried or staked): put the avatar there.
            bool pinned = p.HideState != 0 || p.Health == Game.Health.Carried || p.Health == Game.Health.Staked;
            if (pinned || p.PlaceVersion != placeSeen)
            {
                placeSeen = p.PlaceVersion;
                Vector3 off = PlayerPuppets.StakeOffset(p);
                PlaceAvatar(p.Pos + new Vector2(off.x, off.z));
                verticalSpeed = 0f;
            }
            else MoveAvatar(p, cmd, dt);

            // Report the pose (client-owned movement).
            Vector3 lp = world.transform.InverseTransformPoint(transform.position);
            h.Sim.SetPose(p.Id, new Vector2(lp.x, lp.z), cmd.Aim, p.Gait);
            ViewReach(p);

            UpdateLook(p, cmd);
            PresentCharacter(p);
            UpdateHud(p, h);
        }

        /// <summary>Raised when testing mode switches the local player's role.</summary>
        public event System.Action<Role> RoleChanged;

        InputCmd ReadInput(SimPlayer p, bool menu)
        {
            var cmd = new InputCmd { Seq = ++seq };
            // While the pointer is on the look panel (F4) or a menu is open the light keeps its direction.
            Vector2 aim = AimOverride ?? (menu || VisionDebugHud.PointerOverPanel(aimPoint.ReadValue<Vector2>()) ? Vector2.zero : ReadAim());
            if (aim.sqrMagnitude < 1e-6f && viewer != null) aim = viewer.Facing;
            if (aim.sqrMagnitude > 1e-6f)
            {
                aim.Normalize();
                cmd.Aim = Mathf.Atan2(aim.y, aim.x);
            }
            else cmd.Aim = p.Facing;
            cmd.AimDist = Scale.ToUnits(AimDistance() / Mathf.Max(1e-3f, world.transform.lossyScale.x));
            if (menu)
            {
                cmd.Item = SelectedSlot + 1;
                return cmd;
            }
            Vector2 mv = Vector2.ClampMagnitude(MoveOverride ?? move.ReadValue<Vector2>(), 1f);
            cmd.Move = mv;
            Btn b = ExtraButtons;
            if (SprintOverride ?? Held(sprint)) b |= Btn.Run;
            if (Held(crouch)) b |= Btn.Crouch;
            if (Held(interact)) b |= Btn.Interact;
            // Not while arranging the inventory, or when the click is on the HUD (a slot, a button).
            bool overUi = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (Held(fire) && !GameHud.EditorOpen && !overUi) b |= Btn.Primary;
            if (Held(burst)) b |= Btn.Secondary;
            if (Held(ability)) b |= Btn.Ability;
            if (Held(lunge)) b |= Btn.Lunge;
            if (Held(drop)) b |= Btn.Drop;
            if (Held(beam)) b |= Btn.Beam;
            if (Held(yes)) b |= Btn.Yes;
            if (Held(no)) b |= Btn.No;
            if (Held(special)) b |= p.Role == Role.Hunter ? Btn.Vape : Btn.Space;
            cmd.Buttons = b;

            // Survivors pick a slot with 1-8 (1-12 in testing mode) or the wheel; empty slots are skipped.
            if (p.Role == Role.Survivor)
            {
                int lim = p.Inv.Limit;
                for (int i = 0; i < lim; i++) if (Pressed(slotKeys[i])) SelectedSlot = SelectedSlot == i && p.Inv.CountAt(i) == 0 ? -1 : i;
                float wheel = slotScroll != null ? slotScroll.ReadValue<float>() : 0f;
                int dir = wheel > 0.1f || Pressed(slotPrev) ? -1 : wheel < -0.1f || Pressed(slotNext) ? 1 : 0;
                if (dir != 0) SelectedSlot = NextFilled(p, dir);
                if (SelectedSlot >= lim) SelectedSlot = -1;
                cmd.Item = SelectedSlot + 1;
            }
            return cmd;
        }

        int NextFilled(SimPlayer p, int dir)
        {
            int lim = p.Inv.Limit, s = SelectedSlot < 0 ? (dir > 0 ? -1 : 0) : SelectedSlot;
            for (int k = 0; k < lim; k++)
            {
                s = ((s + dir) % lim + lim) % lim;
                if (p.Inv.CountAt(s) > 0) return s;
            }
            return SelectedSlot;
        }

        void MoveAvatar(SimPlayer p, InputCmd cmd, float dt)
        {
            MatchHost h = Host;
            p.Move.Mode = h.Sim.MoveModeFor(p);
            MoveContext ctx = h.Sim.MoveContextFor(p, GameSession.SpeedMode);
            Vector2 delta = Movement.Step(p.Move, p.Pos, cmd, ctx, h.Geometry, dt, out Game.Gait gait);
            p.Gait = gait;
            float scale = world.transform.lossyScale.x;
            Vector3 step = world.transform.TransformVector(new Vector3(delta.x, 0f, delta.y));
            if (dt > 0f)
            {
                Vector2 vel = new Vector2(step.x, step.z) / dt;
                float grade = Grade(vel);
                step *= SlopeFactor(grade);
                // Movement follows the ground: the climb or drop along the way is added to the step.
                if (cc.isGrounded || grade != 0f) verticalSpeed = grade * vel.magnitude * SlopeFactor(grade) - 1.5f * scale;
                else verticalSpeed -= 9.81f * dt;
            }
            step.y = verticalSpeed * dt;
            cc.Move(step);
            KeepOnGround();
        }

        void PlaceAvatar(Vector2 local)
        {
            Teleport(world.transform.TransformPoint(new Vector3(local.x, 0f, local.y)), false);
        }

        /// <summary>The distance (world units) from the player to the farthest corner of the screen on the ground (Penjamin's reach).</summary>
        void ViewReach(SimPlayer p)
        {
            if (viewCamera == null) return;
            float far = 0f;
            Vector3 at = transform.position;
            foreach (Vector2 c in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
            {
                Ray r = viewCamera.ViewportPointToRay(c);
                if (Mathf.Abs(r.direction.y) < 1e-4f) continue;
                Vector3 hit = r.origin + r.direction * ((at.y - r.origin.y) / r.direction.y);
                far = Mathf.Max(far, new Vector2(hit.x - at.x, hit.z - at.z).magnitude);
            }
            Host.ReportViewReach(Scale.ToUnits(far / Mathf.Max(1e-3f, world.transform.lossyScale.x)));
        }

        float AimDistance()
        {
            if (viewCamera == null || Pointer.current == null) return 0f;
            Vector2 a = PointerAim(aimPoint.ReadValue<Vector2>());
            return a.magnitude;
        }

        /// <summary>The flashlight and body for the role and state: cone, reach, x-ray, crawling, hidden.</summary>
        void UpdateLook(SimPlayer p, InputCmd cmd)
        {
            bool zach = p.Role == Role.Hunter;
            bool downed = p.Health == Game.Health.Downed;
            if (viewer != null)
            {
                viewer.Facing = new Vector2(Mathf.Cos(p.Facing), Mathf.Sin(p.Facing));
                float cone = (zach ? Balance.Hunter.ConeHalfAngleDeg : Balance.Survivor.ConeHalfAngleDeg) * Mathf.Sqrt(p.FovMul);
                if (p.GogglesOn) cone *= Balance.Items.Goggles.ConeMul;
                if (p.DarkT > 0f) cone *= 1f - Balance.Hunter.Vape.ConeCut;
                if (p.HideState == 2)
                {
                    var spot = world.HidingSpots[p.HideSpot];
                    cone = spot.kind == HidingSpot.Kind.Grass ? 85f : Balance.Hiding.PeekHalfAngleDeg;
                }
                viewer.coneHalfAngleDeg = Mathf.Clamp(cone, 5f, 90f);
                viewer.proximityRadius = Scale.D(p.HideState == 2 ? Balance.Hiding.PeekProximity : zach ? Balance.Hunter.Proximity : Balance.Survivor.Proximity) * Mathf.Sqrt(p.FovMul);
                viewer.visionMultiplier = Mathf.Clamp(downed ? Balance.Survivor.DownedVisionMul : 1f, 0.1f, 1f);
                viewer.seeThroughEnabled = p.GogglesOn || (zach && p.HempOn);
            }
            if (animator != null)
            {
                Vector3 moved = cc.enabled ? cc.velocity : Vector3.zero;
                animator.Drive(new Vector3(moved.x, 0f, moved.z), new Vector2(Mathf.Cos(p.Facing), Mathf.Sin(p.Facing)));
            }
            bool hidden = p.HideState == 2 || p.Health == Game.Health.Escaped || p.Health == Game.Health.Eliminated || p.Role == Role.Spectator;
            if (hidden != hiddenShown)
            {
                hiddenShown = hidden;
                foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = !hidden;
            }
        }

        void UpdateHud(SimPlayer p, MatchHost h)
        {
            InteractPrompt = PromptText(p, h.Sim);
            SpacePrompt = p.Prompt2 != Prompt.None ? Texts.PromptLabel(p.Prompt2) : null;
            HoldProgress = -1f;
            switch (p.Action)
            {
                case ActionKind.Repair:
                    if (p.ActionTarget >= 0 && p.ActionTarget < h.Sim.Gens.Length) HoldProgress = h.Sim.Gens[p.ActionTarget].Progress;
                    break;
                case ActionKind.OpenGate:
                    HoldProgress = h.Sim.Gate.Progress;
                    break;
                case ActionKind.None:
                case ActionKind.Attack:
                case ActionKind.Talk:
                    if (p.Prompt == Prompt.Repair && p.PromptTarget >= 0) HoldProgress = h.Sim.Gens[p.PromptTarget].Progress;
                    else if (p.Prompt == Prompt.OpenGate) HoldProgress = h.Sim.Gate.Progress;
                    break;
                default:
                    if (p.ActionDur > 0f) HoldProgress = Mathf.Clamp01(p.ActionT / p.ActionDur);
                    break;
            }
        }

        /// <summary>The prompt line, with what it is about (an item's name, an NPC's name).</summary>
        public static string PromptText(SimPlayer p, MatchSim sim)
        {
            switch (p.Prompt)
            {
                case Prompt.None:
                    return null;
                case Prompt.Loot:
                    return p.PromptTarget >= 0 && p.PromptTarget < sim.Map.Loot.Count ? $"Press E to pick up {Items.Name(sim.Map.Loot[p.PromptTarget].Item, sim.Map.Loot[p.PromptTarget].Golden)}" : Texts.PromptLabel(p.Prompt);
                case Prompt.PickDrop:
                {
                    DropItem d = sim.Drops.Find(x => x.Id == p.PromptTarget);
                    return d != null ? $"Press E to pick up {Items.Name(d.Item, d.Golden)}" : Texts.PromptLabel(p.Prompt);
                }
                case Prompt.NameNpc:
                    return p.PromptTarget >= 0 && p.PromptTarget < sim.Npcs.Count ? sim.Npcs[p.PromptTarget].Name : null;
                case Prompt.NameLoot:
                    return p.PromptTarget >= 0 && p.PromptTarget < sim.Map.Loot.Count ? Items.Name(sim.Map.Loot[p.PromptTarget].Item, sim.Map.Loot[p.PromptTarget].Golden) : null;
                case Prompt.NameDrop:
                    return p.PromptTarget >= 0 && p.PromptTarget < sim.Drops.Count ? Items.Name(sim.Drops[p.PromptTarget].Item, sim.Drops[p.PromptTarget].Golden) : null;
                case Prompt.Hide:
                {
                    var kind = sim.Map.HidingSpots[p.PromptTarget].Kind;
                    return kind == HidingSpot.Kind.Bed ? "Press E to hide under the bed" : $"Press E to hide in the {HideLabel(kind)}";
                }
                case Prompt.GatePowerless:
                {
                    int running = 0;
                    foreach (GenState g in sim.Gens) if (g.Repaired) running++;
                    return $"The gate has no power ({running}/{sim.Bal.RequiredGenerators} generators)";
                }
                default:
                    return Texts.PromptLabel(p.Prompt);
            }
        }

        static string HideLabel(HidingSpot.Kind k) => k switch
        {
            HidingSpot.Kind.Grass => "tall grass",
            HidingSpot.Kind.Wardrobe => "wardrobe",
            HidingSpot.Kind.Locker => "locker",
            HidingSpot.Kind.Bed => "bed",
            _ => "barrel",
        };

        Vector2 ReadAim()
        {
            Vector2 stick = aimStick != null ? aimStick.ReadValue<Vector2>() : Vector2.zero;
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

        /// <summary>Hides in a spot at once (captures and tests): inside it, out of sight.</summary>
        public void EnterHiding(HidingSpot spot)
        {
            SimPlayer p = Me;
            int i = world != null ? world.HidingSpots.IndexOf(spot) : -1;
            if (p == null || i < 0) return;
            Host.Sim.Hiding[i] = p.Id;
            p.HideSpot = i;
            p.HideState = 2;
            Host.Sim.CancelAction(p);
            if (spot.kind != HidingSpot.Kind.Grass) Host.Sim.Place(p, Host.Sim.Map.HidingSpots[i].Pos);
        }

        public void LeaveHiding()
        {
            SimPlayer p = Me;
            if (p == null || p.HideState == 0) return;
            Host.Sim.ExitHiding(p, false);
        }

        public bool ToggleNearestDoor()
        {
            SimPlayer p = Me;
            if (p == null) return false;
            int d = Host.Sim.NearbyDoor(p.Pos, Balance.Reach.Door * 1.5f);
            if (d < 0) return false;
            Host.Sim.SetDoor(d, !Host.Sim.Doors[d]);
            return true;
        }

        /// <summary>Moves the player instantly (CharacterController-safe), standing on the terrain; the match learns where they are.</summary>
        public void Teleport(Vector3 position) => Teleport(position, true);

        void Teleport(Vector3 position, bool tellMatch)
        {
            if (cc == null) cc = GetComponent<CharacterController>();
            cc.enabled = false;
            if (TerrainField.TrySample(position, out float ground, out _)) position.y = ground + 0.05f * transform.lossyScale.x;
            transform.position = position;
            cc.enabled = true;
            if (!tellMatch || Host == null || Host.Sim == null || Me == null) return;
            Vector3 l = world.transform.InverseTransformPoint(position);
            Host.Teleport(new Vector2(l.x, l.z));
            placeSeen = Me.PlaceVersion;
        }
    }
}
