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
        InputAction turnLeft, turnRight;
        TopDownCamera rig;
        MatchSim turnedFor;

        /// <summary>The view's current turn (degrees clockwise from north), 0 without a camera rig.</summary>
        public float ViewYaw
        {
            get
            {
                if (rig == null && viewCamera != null) rig = viewCamera.GetComponent<TopDownCamera>();
                return rig != null ? rig.Yaw : 0f;
            }
        }

        /// <summary>Z and C turn the view 45 degrees left or right; every match starts facing north.</summary>
        void UpdateTurn(MatchHost h, bool menu)
        {
            if (rig == null && viewCamera != null) rig = viewCamera.GetComponent<TopDownCamera>();
            if (rig == null) return;
            if (h.Sim != turnedFor)
            {
                turnedFor = h.Sim;
                rig.ResetYaw();
            }
            if (menu) return;
            if (Pressed(turnLeft)) rig.Turn(-1);
            if (Pressed(turnRight)) rig.Turn(1);
            MapHud.ViewYaw = rig.Yaw;
        }

        bool hiddenShown;
        // The see-through light's growth (0-1), and whether the one fading is the Hemp Battery's (it fades out as slowly as in).
        float xrayK;
        bool xrayHemp;
        /// <summary>Night vision's widening of the beam (0 normal, 1 its full 20% wider), grown in as the see-through light lengthens.</summary>
        float goggleK;
        // Penjamin's darkness (0-1), eased in and out over the original's 0.6 s: the beam narrows and the dark goes black.
        float darkK;
        // The gas blur (0-1), how long since Zach left the galaxy gas, and how long a survivor's blur has left.
        float gasBlurK, gasLeftT = 99f, revealK, jarvisK, jarvisAge;
        /// <summary>How long JARVIS's light takes to spread out from the player over the whole screen.</summary>
        public const float RevealSpread = 0.8f;
        Vision.Rendering.VisionComposite composite;

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
            turnLeft = A("TurnLeft");
            turnRight = A("TurnRight");
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

        void OnEnable() => Subscribe();

        void OnDisable()
        {
            if (subscribed != null) subscribed.EventRaised -= OnEvent;
            subscribed = null;
        }

        /// <summary>
        /// Hears the match's events (the player's own one-shot clips: swings, throws, recoils...). Also called every frame:
        /// the player is enabled before the level hands it its world, when there is no match to listen to yet.
        /// </summary>
        void Subscribe()
        {
            MatchHost h = Host;
            if (h == subscribed) return;
            if (subscribed != null) subscribed.EventRaised -= OnEvent;
            subscribed = h;
            if (h != null) h.EventRaised += OnEvent;
        }

        MatchHost subscribed;

        void OnEvent(GameEvent e)
        {
            if (e.Kind == EventKind.Item && !string.IsNullOrEmpty(e.Text)) Notice?.Invoke(e.Text);
            if (view != null && host != null && host.Sim != null) view.OnEvent(e, Me, host.Sim);
        }

        CharacterView view;
        /// <summary>The player's own character (its bones, for effects that come out of a hand).</summary>
        public CharacterView View => view;

        /// <summary>The model for the role (survivor or Zach) and the action clips for what the player is doing.</summary>
        void PresentCharacter(SimPlayer p)
        {
            if (view == null) view = GetComponent<CharacterView>();
            if (view == null) return;
            view.SetSpec(SpecFor(p));
            view.Present(p);
        }

        static readonly System.Collections.Generic.Dictionary<string, CharacterSpec> specs = new System.Collections.Generic.Dictionary<string, CharacterSpec>();

        /// <summary>
        /// The model a player shows: Zach's, a survivor's, or for a ghost taking a body (merging with it, and in it) the look
        /// of whoever it was, a survivor's or the NPC's own (so the ghost's figure fits the body's exactly as they merge).
        /// </summary>
        public static CharacterSpec SpecFor(SimPlayer p)
        {
            bool taking = p.Reanimated || (p.Ghost && p.Body != 0);
            string look = p.Role == Role.Hunter ? "Zach" : taking && !string.IsNullOrEmpty(p.BodyLook) ? p.BodyLook : "Survivor";
            if (!specs.TryGetValue(look, out CharacterSpec s))
            {
                s = look == "Zach" ? CharacterSpec.Zach() : look == "Survivor" ? CharacterSpec.Survivor() : CharacterSpec.Npc(look);
                specs[look] = s;
            }
            return s;
        }

        /// <summary>Drawn as a ghost (glowing white, through anything): out of a body, or still merging with one.</summary>
        public static bool GhostForm(SimPlayer p) => p.FreeGhost || (p.Ghost && p.PossessT > 0f);

        /// <summary>A ghost knocked out of a body: they see everything (the whole screen lit, through walls, the map revealed, the wheel zooms).</summary>
        public static bool Seer(SimPlayer p) => p != null && p.FreeGhost && p.Spent;

        static bool Pressed(InputAction a) => a != null && a.WasPressedThisFrame();
        static bool Held(InputAction a) => a != null && a.IsPressed();

        void Update()
        {
            if (move == null) return;
            Subscribe();
            MatchHost h = Host;
            SimPlayer p = Me;
            if (h == null || p == null) return;
            float dt = Time.deltaTime;
            bool menu = GameHud.MenuOpen;

            UpdateTurn(h, menu);
            // Testing mode (and a ghost knocked out of a body): the mouse wheel zooms the view in and out (not over the HUD
            // or a menu).
            bool zoom = GameSession.TestingMode || Seer(p);
            if (zoom && !menu && slotScroll != null && !MapHud.AnyFullOpen)
            {
                float w = slotScroll.ReadValue<float>();
                bool overHud = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
                if (Mathf.Abs(w) > 0.1f && !overHud)
                    TopDownCamera.TestZoom = Mathf.Clamp(TopDownCamera.TestZoom * (w > 0f ? 0.88f : 1f / 0.88f), TopDownCamera.TestZoomMin, TopDownCamera.TestZoomMax);
            }
            else if (!zoom) TopDownCamera.TestZoom = 1f;
            // Every body runs into the graveyard's fence, Zach's too.
            if (cc != null && cc.excludeLayers.value != 0) cc.excludeLayers = 0;

            // Testing mode: T goes from Zach to a survivor, to that survivor's ghost (their body at their feet), and back to
            // Zach, where you stand.
            if (!menu && Pressed(switchRole) && h.Sim.TestMode && h.CycleRole())
            {
                p = Me;
                Notice?.Invoke(p.Role == Role.Hunter ? "You are Zach" : p.Ghost ? "You are a ghost: your body is at your feet (E takes it)" : "You are a survivor");
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

            // The rules moved the player (or they are carried or staked): put the avatar there.
            bool pinned = p.Health == Game.Health.Carried || p.Health == Game.Health.Staked;
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

        Vision.UI.ScreenOverlays overlays;
        /// <summary>E was pressed to put a note down and is still held.</summary>
        bool noteDownHeld;

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
            // Up the screen is wherever the view is turned (Z / C).
            if (MoveOverride == null) mv = TopDownCamera.ScreenToGround(mv, ViewYaw);
            cmd.Move = mv;
            Btn b = ExtraButtons;
            if (SprintOverride ?? Held(sprint)) b |= Btn.Run;
            if (Held(crouch)) b |= Btn.Crouch;
            // Reading a note, E puts it down (and that press does nothing else, held or not, until it's let go).
            if (overlays == null) overlays = FindAnyObjectByType<Vision.UI.ScreenOverlays>();
            bool reading = overlays != null && overlays.NoteShowing;
            if (reading && interact.WasPressedThisFrame())
            {
                overlays.HideNote();
                noteDownHeld = true;
            }
            if (noteDownHeld && !Held(interact)) noteDownHeld = false;
            if (Held(interact) && !reading && !noteDownHeld) b |= Btn.Interact;
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

            // Survivors pick a slot with 1-8 (1-12 in testing mode) or the wheel; empty slots are skipped. (A ghost has none.)
            if (p.Role == Role.Survivor && !p.Ghost)
            {
                int lim = p.Inv.Limit;
                for (int i = 0; i < lim; i++) if (Pressed(slotKeys[i])) SelectedSlot = SelectedSlot == i && p.Inv.CountAt(i) == 0 ? -1 : i;
                // In testing mode the wheel zooms the view instead.
                // Holding Tab, the wheel zooms the full map instead.
                float wheel = slotScroll != null && !GameSession.TestingMode && !MapHud.AnyFullOpen ? slotScroll.ReadValue<float>() : 0f;
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
            // A ghost out of a body passes through walls and anything solid; taking one, it glides into it.
            if (GhostForm(p))
            {
                Vector3 l0 = world.transform.InverseTransformPoint(transform.position);
                Vector2 at = new Vector2(l0.x, l0.z) + delta;
                Body b = p.PossessT > 0f ? h.Sim.BodyOf(p) : null;
                if (b != null)
                {
                    // Floating behind it, facing the way it does, then drifting slowly into it (run on smoothly between the
                    // match's ticks).
                    if (mergingInto != b.Id)
                    {
                        mergingInto = b.Id;
                        mergeFrom = new Vector2(l0.x, l0.z);
                        mergeM = 0f;
                    }
                    float mSim = Vision.UI.CorpseViews.PossessProgress(p.PossessT);
                    mergeM = Mathf.Clamp(mergeM + dt / Balance.Ghost.PossessTime, mSim, mSim + 0.03f);
                    at = Vector2.Lerp(mergeFrom, b.Pos, Vision.UI.CorpseViews.PossessGlide(mergeM));
                    p.Facing = b.Facing;
                }
                else mergingInto = 0;
                // (Kept to the playable circle, as the match keeps it.)
                if (at.magnitude > h.Sim.Ring1) at = at.normalized * h.Sim.Ring1;
                GhostTo(at);
                return;
            }
            float scale = world.transform.lossyScale.x;
            Vector3 step = world.transform.TransformVector(new Vector3(delta.x, 0f, delta.y));
            // On the lake dock's boards: flat, solid ground (the dock has no collider of its own: the avatar is held at its
            // deck, below).
            Vector3 l1 = world.transform.InverseTransformPoint(transform.position);
            bool dock = world.OnDock(new Vector2(l1.x, l1.z), out _);
            if (dock) verticalSpeed = 0f;
            else if (dt > 0f)
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
            OntoOrOffDock();
            // The outer ring of the woods stops Zach like a wall while he can't cross it.
            if (p.Role == Role.Hunter && MatchSim.CrossingBlocked(p))
            {
                Vector3 lp = world.transform.InverseTransformPoint(transform.position);
                var flat = new Vector2(lp.x, lp.z);
                float limit = h.Sim.Ring3 - Balance.Boundary.WallSkin;
                if (flat.magnitude > limit)
                {
                    flat = flat.normalized * limit;
                    Teleport(world.transform.TransformPoint(new Vector3(flat.x, lp.y, flat.y)), false);
                }
            }
            KeepOnGround();
        }

        /// <summary>The avatar was on the dock's boards last frame (stepping off them drops it into the water at once).</summary>
        bool onDock;

        /// <summary>
        /// The lake dock underfoot: on its boards the avatar stands at its deck (snapped straight up onto it out of the
        /// water, or from the shore); stepping off, it's straight down into the water (or onto the ground) with no fall.
        /// </summary>
        void OntoOrOffDock()
        {
            Vector3 l = world.transform.InverseTransformPoint(transform.position);
            var at = new Vector2(l.x, l.z);
            if (world.OnDock(at, out float deck))
            {
                // On the boards as on any ground: the controller's own small gap over what it stands on (its skin).
                if (cc == null) cc = GetComponent<CharacterController>();
                deck += cc.skinWidth * transform.lossyScale.y / Mathf.Max(1e-4f, world.transform.lossyScale.y);
                onDock = true;
                verticalSpeed = 0f;
                if (Mathf.Abs(l.y - deck) > 0.005f) SetHeight(l, deck);
                return;
            }
            if (!onDock) return;
            onDock = false;
            verticalSpeed = 0f;
            SetHeight(l, world.GroundHeight(at));
        }

        void SetHeight(Vector3 local, float y)
        {
            if (cc == null) cc = GetComponent<CharacterController>();
            cc.enabled = false;
            transform.position = world.transform.TransformPoint(new Vector3(local.x, y, local.z));
            cc.enabled = true;
        }

        /// <summary>The body this ghost is merging with (0: none), where it started from, and how far through it is (0-1).</summary>
        int mergingInto;
        Vector2 mergeFrom;
        float mergeM;

        /// <summary>A ghost's avatar put straight there, on the ground, whatever is in the way.</summary>
        void GhostTo(Vector2 local)
        {
            Vector3 w = world.transform.TransformPoint(new Vector3(local.x, world.GroundHeight(local), local.y));
            cc.enabled = false;
            transform.position = w;
            cc.enabled = true;
            verticalSpeed = 0f;
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
            // The ground the screen shows, as a rectangle on the level's plane (the camera is orthographic, so its edges
            // meet the ground square): across from the left edge's middle to the right's, up from the bottom's to the top's.
            Vector2? OnGround(Vector2 vp)
            {
                Ray ray = viewCamera.ViewportPointToRay(vp);
                if (Mathf.Abs(ray.direction.y) < 1e-4f) return null;
                Vector3 hitAt = world.transform.InverseTransformPoint(ray.origin + ray.direction * ((at.y - ray.origin.y) / ray.direction.y));
                return new Vector2(hitAt.x, hitAt.z);
            }
            Vector2? eL = OnGround(new Vector2(0f, 0.5f)), eR = OnGround(new Vector2(1f, 0.5f)), eB = OnGround(new Vector2(0.5f, 0f)), eT = OnGround(new Vector2(0.5f, 1f)), eC = OnGround(new Vector2(0.5f, 0.5f));
            if (!eL.HasValue || !eR.HasValue || !eB.HasValue || !eT.HasValue || !eC.HasValue) return;
            Vector2 across = eR.Value - eL.Value, upward = eT.Value - eB.Value;
            Host.ReportView(new Vector2(across.magnitude * 0.5f, upward.magnitude * 0.5f), Mathf.Atan2(across.y, across.x), eC.Value - p.Pos);
        }

        float AimDistance()
        {
            if (viewCamera == null || Pointer.current == null) return 0f;
            Vector2 a = PointerAim(aimPoint.ReadValue<Vector2>());
            return a.magnitude;
        }

        float flickT, edgeDark, woodsT;
        Vision.Visibility.VisionLight halo;

        /// <summary>Zach's halo in the dark past the second ring: a small, faint light of his own, coming up as his light dies.</summary>
        void Halo(float k)
        {
            if (k <= 0.001f)
            {
                if (halo != null && halo.enabled) halo.enabled = false;
                return;
            }
            if (halo == null)
            {
                var go = new GameObject("Halo");
                go.transform.SetParent(transform, false);
                halo = go.AddComponent<Vision.Visibility.VisionLight>();
                halo.isStatic = false;
                halo.height = 1.2f;
                halo.flickerAmount = 0.03f;
            }
            halo.range = Balance.Boundary.HaloRadius * world.transform.lossyScale.x / Mathf.Max(0.01f, transform.lossyScale.x);
            halo.intensity = Balance.Boundary.HaloIntensity * k;
            if (!halo.enabled) halo.enabled = true;
        }

        /// <summary>
        /// The light at the edge of the woods (MatchSim.Boundary), as a brightness for the whole of the player's light at once
        /// (the beam and the light round them dim together, evenly; the beam never shortens). Past the first ring it's
        /// plainly dying: it wavers, sags deep every second or so, sputters on and off through each sag, and blinks nearly
        /// out now and then, more and deeper further out; past the second it grows fainter and fainter over 3 s while it
        /// flickers faster, deeper and more erratically, until it's out; a survivor's whole screen goes black with it at the
        /// end (<see cref="edgeDark"/>).
        /// Returns the light's brightness (0-1).
        /// </summary>
        /// <summary>How far between the first ring and the second they are (0 at the first, 1 at the second).</summary>
        float EdgeDepth(SimPlayer p)
        {
            if (world == null) return 1f;
            return Mathf.Clamp01((p.Pos.magnitude - world.halfExtent - Balance.Boundary.FirstRing) / Balance.Boundary.InnerGap);
        }

        /// <summary>
        /// How far night vision's widening has grown (0-1) at <paramref name="k"/> of the way through the see-through light's
        /// fade-in: the light lengthens on 1-(1-k)^2 from 35% of its reach to all of it, and passes the side of the screen at
        /// about 60% of the way (<see cref="GoggleScreenShare"/>); the beam's angle has opened all the way by then, eased.
        /// </summary>
        public static float GoggleWiden(float k)
        {
            k = Mathf.Clamp01(k);
            float reach = 1f - (1f - k) * (1f - k);
            float w = Mathf.Clamp01(reach / GoggleScreenShare);
            return w * w * (3f - 2f * w);
        }

        /// <summary>Share of the see-through light's growth at which it reaches the side of a landscape screen.</summary>
        public const float GoggleScreenShare = 0.6f;

        float BoundaryLight(SimPlayer p, float dt)
        {
            if (p.LostInWoods)
            {
                // Gone into the woods: their light stays out where they fell (no lit spot to come back to); the screen holds
                // black while "Sacrificed" is written out, then lifts to the unlit dark so they can watch the rest.
                woodsT += dt;
                edgeDark = 1f - Mathf.Clamp01((woodsT - Balance.Boundary.SacrificedHold) / 1f);
                return 0f;
            }
            woodsT = 0f;
            float dying = Mathf.Clamp01(p.ShutdownT / Balance.Boundary.ShutdownTime);
            // The screen goes black only as the light finally gives out.
            edgeDark = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, dying));
            if (p.Zone == 0 && dying <= 0f) return 1f;
            // Between the rings the flicker grows with how far out they are: faint just past the first ring, at its full
            // strength by the second (where the dying takes over from it).
            float deep = EdgeDepth(p);
            // Time runs faster through the noise as it dies (and a little faster further out): it quickens and turns erratic.
            flickT += dt * (Mathf.Lerp(1.6f, 2.2f, deep) + 2.5f * dying);
            float waver = Mathf.PerlinNoise(flickT * 1.3f, 0.37f);
            // Sags: a smooth noise crossing a threshold, often (about every second), more often and deeper further out and
            // as it dies; and a fine tremor once it's dying.
            float sagNoise = Mathf.PerlinNoise(flickT * 2.6f, 4.1f);
            float from = Mathf.Lerp(0.5f, 0.42f, deep) - 0.12f * dying, to = Mathf.Lerp(0.62f, 0.54f, deep) - 0.08f * dying;
            float sag = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, sagNoise));
            // Through a sag the bulb sputters, cutting nearly out and back in a few times.
            float sputter = sag * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.6f, Mathf.PerlinNoise(flickT * 14f, 2.3f)));
            // And now and then, sag or not, it blinks almost out for a moment.
            float blink = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 0.78f, Mathf.PerlinNoise(flickT * 5.3f, 7.7f)));
            float tremor = (Mathf.PerlinNoise(flickT * 6.1f, 9.7f) - 0.5f) * 0.5f * dying;
            float wave = Mathf.Lerp(0.12f, 0.18f, deep);
            float depth = Mathf.Lerp(Mathf.Lerp(0.5f, 0.7f, deep), 0.85f, dying);
            float flick = (1f - wave + wave * waver) * (1f - depth * sag) * (1f - 0.8f * sputter) * (1f - 0.85f * blink) * (1f + tremor);
            // Fainter and fainter until it's out at 3 s.
            float envelope = 1f - Mathf.SmoothStep(0f, 1f, dying);
            return Mathf.Clamp01(flick * envelope);
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
                // Night vision widens the beam as it comes on (never snapping to it): the angle grows out over the same
                // 0.5 s the see-through light takes to lengthen out past the edge of the screen, on the same curve, and
                // narrows back as quickly as the light goes.
                goggleK = p.GogglesOn ? Mathf.Min(1f, goggleK + Time.deltaTime / Balance.Xray.FadeIn) : Mathf.Max(0f, goggleK - Time.deltaTime / Balance.Xray.FadeOut);
                cone *= Mathf.Lerp(1f, Balance.Items.Goggles.ConeMul, GoggleWiden(goggleK));
                // Galaxy gas narrows Zach's cone by 30%.
                if (zach && p.Gassed) cone *= 1f - Balance.Items.Trap.ZachConeCut;
                float dt0 = Time.deltaTime;
                // Penjamin or 50 Nic: the narrowed light (and the dark) ease in, and out of the gas widen back linearly over
                // its five-second linger, back to normal exactly when the slow and blur end.
                float gasLeft = zach ? 0f : MatchSim.VapeLeft(p);
                darkK = Mathf.MoveTowards(darkK, gasLeft > 0f ? 1f : 0f, dt0 / Balance.Hunter.Vape.DarkEase);
                float darkE = Mathf.Min(darkK * darkK * (3f - 2f * darkK), gasLeft);
                cone *= 1f - Balance.Hunter.Vape.ConeCut * darkE;
                if (composite == null) composite = FindAnyObjectByType<Vision.Rendering.VisionComposite>();
                if (composite != null) composite.fogScale = 1f - darkE;
                // The gas blur: strong the moment you're gassed, held while you're in it, fading as the effect wears off
                // (over three seconds: Penjamin's two-second after-effect on a survivor and a second more; three seconds
                // after Zach leaves the galaxy gas).
                gasLeftT = zach && p.Gassed ? 0f : gasLeftT + dt0;
                float blurWant = zach ? Mathf.Clamp01(1f - gasLeftT / Balance.Items.Trap.BlurAfter)
                    : gasLeft;
                gasBlurK = blurWant > gasBlurK ? Mathf.MoveTowards(gasBlurK, blurWant, dt0 / 0.15f) : blurWant;
                if (composite != null)
                {
                    composite.gasBlur = gasBlurK;
                    // Piss in the eyes: blurred and yellowed, falling off linearly over two seconds.
                    composite.piss = Mathf.Clamp01(p.PissBlurT / Balance.Items.Piss.BlurTime);
                    // JARVIS (its user): everything outlined in blue, scanned, as long as it runs.
                    bool jarvisOn = !zach && MatchSim.JarvisActive(p);
                    jarvisK = jarvisOn ? Mathf.Min(1f, jarvisK + dt0 / RevealSpread) : Mathf.Max(0f, jarvisK - dt0 / 0.5f);
                    jarvisAge = jarvisOn ? jarvisAge + dt0 : 0f;
                    composite.jarvis = jarvisK;
                    composite.jarvisTime = jarvisAge;
                }
                viewer.coneHalfAngleDeg = Mathf.Clamp(cone, 5f, 90f);
                viewer.reachScreenEdge = true;
                viewer.proximityRadius = Scale.D(zach ? Balance.Hunter.Proximity : Balance.Survivor.Proximity) * Mathf.Sqrt(p.FovMul);
                // The edge of the woods dims the whole light evenly; the beam keeps its full reach until the light is out.
                float light = BoundaryLight(p, dt0);
                bool lightOut = p.LostInWoods || p.ShutdownT >= Balance.Boundary.ShutdownTime;
                viewer.visionMultiplier = lightOut ? 0f : Mathf.Clamp(downed ? Balance.Survivor.DownedVisionMul : 1f, 0.1f, 1f);
                if (lightOut) viewer.proximityRadius = 0f;
                if (composite != null) composite.viewerLight = light;
                // A survivor's screen goes black; Zach's surroundings go pitch dark but a small halo stays round him.
                if (composite != null) composite.blackout = zach ? 0f : edgeDark;
                if (zach && composite != null) composite.fogScale *= 1f - edgeDark;
                Vision.Effects.SensesCamera.Blackout = zach ? 0f : edgeDark;
                Halo(zach && !p.LostInWoods ? edgeDark : 0f);
                // Goggles and the Hemp Battery: the see-through light grows in over 0.5 s, out in 1/6 s (the battery's as slowly as in).
                // A ghost knocked out of a body sees through everything.
                bool seer = Seer(p);
                bool xrayOn = p.GogglesOn || (zach && p.HempOn) || seer;
                if (xrayOn) xrayHemp = zach && p.HempOn;
                xrayK = xrayOn ? Mathf.Min(1f, xrayK + dt0 / Balance.Xray.FadeIn) : Mathf.Max(0f, xrayK - dt0 / (xrayHemp ? Balance.Xray.FadeIn : Balance.Xray.FadeOut));
                viewer.seeThroughEnabled = xrayK > 0f;
                viewer.seeThroughK = xrayK;
                viewer.seeThroughStrength = Balance.Xray.Brightness;
                // JARVIS: while it runs, every survivor sees everything on their screen (Zach doesn't): ten seconds for
                // everyone, its whole run for the survivor running it; the light spreads out from the player over the
                // screen in 0.8 s as it comes on.
                // And a ghost knocked out of a body: their whole screen lit, for good.
                bool reveal = seer || (Host != null && !zach && !p.Ghost && (MatchSim.JarvisActive(p) || (Host.Sim.RevealT > 0f && p.Zone < 2 && p.Role == Role.Survivor)));
                revealK = reveal ? Mathf.Min(1f, revealK + dt0 / RevealSpread) : 0f;
                viewer.revealAll = reveal;
                viewer.revealGrow = revealK * revealK * (3f - 2f * revealK);
            }
            if (animator != null)
            {
                Vector3 moved = cc.enabled ? cc.velocity : Vector3.zero;
                // Merging with a body: no walk at all (it floats, in its A pose, level with the body).
                bool merging = p.Ghost && p.Body != 0 && p.PossessT > 0f && mergingInto != 0;
                if (merging) moved = Vector3.zero;
                animator.Drive(new Vector3(moved.x, 0f, moved.z), new Vector2(Mathf.Cos(p.BodyFacing), Mathf.Sin(p.BodyFacing)));
                animator.Lift = merging ? Vision.UI.CorpseViews.PossessLift(mergeM) * world.transform.lossyScale.y / Mathf.Max(1e-4f, transform.lossyScale.y)
                    : p.Move.Climbing ? world.VaultLift(p.Pos, p.RadiusD, Host != null ? Host.Sim.WindowsBroken : null) * world.transform.lossyScale.y / Mathf.Max(1e-4f, transform.lossyScale.y) : 0f;
            }
            bool hidden = !p.Ghost && (p.Health == Game.Health.Escaped || p.Health == Game.Health.Eliminated || p.Role == Role.Spectator);
            if (hidden != hiddenShown)
            {
                hiddenShown = hidden;
                foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = !hidden;
            }
            // Back as a ghost: glowing white (in a body, its owner's look), and silent.
            bool ghostForm = GhostForm(p);
            GhostLook.Set(gameObject, ghostForm, world != null ? world.lowPolyMaterial : null);
            // Back in a body: a small golden halo over its head.
            Vision.Player.Halo.Set(gameObject, MatchSim.InBody(p), world != null ? world.entityMaterial : null);
            // Zach soaked in piss: yellow, fading with it.
            PissLook.Set(gameObject, p.Role == Role.Hunter ? PissLook.Amount(MatchSim.PissBonus(p)) : 0f);
            if (steps == null) steps = GetComponent<Vision.Audio.Footsteps>();
            if (steps != null && steps.enabled == ghostForm) steps.enabled = !ghostForm;
        }

        Vision.Audio.Footsteps steps;

        void UpdateHud(SimPlayer p, MatchHost h)
        {
            // Picking a lock, the dial says it all.
            InteractPrompt = p.Action == ActionKind.PickLock ? null : PromptText(p, h.Sim);
            SpacePrompt = p.Prompt2 != Prompt.None ? Texts.PromptLabel(p.Prompt2) : null;
            HoldProgress = -1f;
            switch (p.Action)
            {
                case ActionKind.Repair:
                    if (p.ActionTarget >= 0 && p.ActionTarget < h.Sim.Gens.Length) HoldProgress = h.Sim.Gens[p.ActionTarget].Progress;
                    break;
                case ActionKind.OpenGate:
                    HoldProgress = h.Sim.GateAt(Mathf.Clamp(p.ActionTarget, 0, 1)).Progress;
                    break;
                case ActionKind.None:
                case ActionKind.Attack:
                case ActionKind.Talk:
                    if (p.Prompt == Prompt.Repair && p.PromptTarget >= 0) HoldProgress = h.Sim.Gens[p.PromptTarget].Progress;
                    else if (p.Prompt == Prompt.OpenGate) HoldProgress = h.Sim.GateAt(Mathf.Clamp(p.PromptTarget, 0, 1)).Progress;
                    // Taking a body: how far it's risen and merged.
                    else if (p.Ghost && p.Body != 0 && p.PossessT > 0f) HoldProgress = 1f - p.PossessT / Balance.Ghost.PossessTime;
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
                case Prompt.Possess:
                {
                    Body b = sim.BodyById(p.PromptTarget);
                    return b != null ? $"Press E to possess {b.Name}'s body" : Texts.PromptLabel(p.Prompt);
                }
                case Prompt.Repair when p.Ghost:
                    return "Hold E to help repair the generator";
                case Prompt.OpenGate when p.Ghost:
                    return "Hold E to help open the gate";
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

        Vector2 ReadAim()
        {
            Vector2 stick = aimStick != null ? aimStick.ReadValue<Vector2>() : Vector2.zero;
            Vector2 pointer = aimPoint.ReadValue<Vector2>();
            if (stick.magnitude > stickAimDeadzone) aimingWithStick = true;
            else if ((pointer - lastPointer).sqrMagnitude > 4f) aimingWithStick = false;
            lastPointer = pointer;
            // Holding the last stick direction when it is released keeps the light where it was.
            if (aimingWithStick) return stick.magnitude > stickAimDeadzone ? TopDownCamera.ScreenToGround(stick, ViewYaw) : Vector2.zero;
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
