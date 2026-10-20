using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Vision.Game;
using Vision.UI;
using Vision.World;

namespace Vision.Player
{
    /// <summary>
    /// The game's HUD, built in code with uGUI in the original's look (bone on black, Oswald and IBM Plex Mono, red
    /// accents) and scaled from a 1080p reference: health, shield and stamina (bottom left), the inventory slots with their
    /// keys (bottom centre), the interaction prompt with a progress bar for held work, the objective and a compass (top),
    /// the minimap and full map (M), the feed, a red edge when hurt, the downed screen and, in testing mode, the TEST
    /// EFFECTS panel. The title screen (the original's landing page) opens first; Esc opens the settings, which do not
    /// pause, as in the original. Match events become pictures, sounds and shakes through <see cref="MatchPresenter"/>.
    /// </summary>
    public sealed partial class GameHud : MonoBehaviour
    {
        public SandboxWorld world;
        public VisionDebugHud debugHud;
        [Tooltip("Shown at all; captures hide it for the look comparisons.")]
        public bool visible = true;

        /// <summary>True while a menu is open (the title screen, the settings or a modal): the player ignores aim and movement.</summary>
        public static bool MenuOpen => escOpen || MainMenuOpen || modalOpen || lobbyOpen;
        /// <summary>The title screen is showing.</summary>
        public static bool MainMenuOpen { get; private set; }
        /// <summary>The full map (Tab) is open, with the inventory editor under it: the mouse drags items there.</summary>
        public static bool FullMapOpen { get; private set; }
        /// <summary>Whether the pointer should show: a menu, the full map or the inventory editor is open.</summary>
        public static bool PointerWanted => MenuOpen || FullMapOpen || EditorOpen;
        static bool escOpen, modalOpen;

        /// <summary>The original's kicker line and blurb.</summary>
        public const string Kicker = "Crystal Lake · Night shoot";
        public const string Tagline = "Zach Branch plays the masked killer on the new <i>Crystal Lake</i> series. Tonight he stopped acting. Start every generator, power the gate and get out of the woods.";

        static readonly Color PanelColor = new Color(6f / 255f, 6f / 255f, 5f / 255f, 0.72f);
        static readonly Color PanelBorder = new Color(217f / 255f, 211f / 255f, 193f / 255f, 0.18f);
        static readonly Color SlotColor = new Color(8f / 255f, 8f / 255f, 7f / 255f, 0.82f);
        static readonly Color SlotBorder = new Color(217f / 255f, 211f / 255f, 193f / 255f, 0.22f);
        static readonly Color SlotBorderSelected = new Color(217f / 255f, 211f / 255f, 193f / 255f, 0.9f);

        UiKit kit;
        Canvas canvas;
        RectTransform root;
        RectTransform staminaFill, holdFill, staminaBox;
        RawImage staminaImage;
        Text staminaLabel, prompt, objectiveTitle, objectiveSub, compass, notices, modeLine;
        GameObject promptBox, holdBar, downedScreen, generating, fxPanel;
        MapHud map;
        bool needsMapBind;
        /// <summary>The minimap and full map.</summary>
        public MapHud Map => map;
        /// <summary>Pictures over the screen (scare, flashes, notes, announcements).</summary>
        public ScreenOverlays Overlays { get; private set; }
        public MatchPresenter Presenter { get; private set; }
        public MatchEffects Effects { get; private set; }
        public ItemViews ItemsInPlay { get; private set; }

        RectTransform strip;
        readonly GameObject[] slotBoxes = new GameObject[Inventory.TestingSlots];
        readonly Image[][] slotFrames = new Image[Inventory.TestingSlots][];
        readonly Image[] slotSelectBars = new Image[Inventory.TestingSlots];
        readonly RawImage[] slotIcons = new RawImage[Inventory.TestingSlots];
        readonly Text[] slotCounts = new Text[Inventory.TestingSlots], slotNames = new Text[Inventory.TestingSlots];
        static readonly string[] SlotKeys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "\\" };
        int shownLimit = -1;
        RawImage vignette;
        int lastBoundaryHits;
        float edgePulse;
        readonly List<(string text, float until)> noticeList = new List<(string, float)>();
        float flash;
        PlayerController player;
        MatchHost host;

        void Awake()
        {
            escOpen = false;
            modalOpen = false;
            editorOpen = false;
            MainMenuOpen = false;
            // The pace the player chose last time (captures keep the original's).
            if (!VisionCapture.Requested) MatchState.Current.SetPace(MatchState.SavedPace(), false);
            EnsureBuilt();
        }

        void OnEnable() => SandboxWorld.Built += OnBuilt;
        void OnDisable() => SandboxWorld.Built -= OnBuilt;

        void Start()
        {
            // Automated captures go straight in; players start at the title screen.
            if (VisionCapture.Requested || VisionPerf.Requested) needsMapBind = true;
            else ShowMainMenu();
        }

        void OnBuilt(SandboxWorld w)
        {
            if (w != world) return;
            needsMapBind = true;
        }

        void EnsureBuilt()
        {
            if (canvas != null) return;
            kit = new UiKit();
            Build();
        }

        void OnDestroy()
        {
            escOpen = false;
            modalOpen = false;
            MainMenuOpen = false;
            Time.timeScale = 1f;
            if (host != null) host.EventRaised -= OnMatchEvent;
        }

        // ---------------------------------------------------------------- building

        static RectTransform Node(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size) => UiKit.Node(name, parent, anchor, pivot, pos, size);

        /// <summary>The HUD's panel: translucent black, a faint bone border and a red bar down the left edge.</summary>
        static void HudPanel(RectTransform rt)
        {
            UiKit.Box(rt, PanelColor);
            UiKit.Frame(rt, PanelBorder);
            RectTransform bar = Node("Accent", rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(3f, 0f));
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(0f, 1f);
            bar.sizeDelta = new Vector2(3f, 0f);
            UiKit.Box(bar, UiKit.Red);
        }

        static Texture2D HGradient(Color a, Color b)
        {
            var t = new Texture2D(32, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int x = 0; x < 32; x++) t.SetPixel(x, 0, Color.Lerp(a, b, x / 31f));
            t.Apply();
            return t;
        }

        Texture2D staminaTex, staminaLocked, staminaBoost;

        void Build()
        {
            var canvasGo = new GameObject("Game HUD Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }
            root = (RectTransform)canvasGo.transform;

            staminaTex = HGradient(UiKit.Bone, UiKit.Bone);
            staminaLocked = HGradient(UiKit.Red, UiKit.Red);
            staminaBoost = HGradient(UiKit.Cyan, UiKit.Cyan);

            // Hurt vignette under everything else.
            vignette = UiKit.Fill("Hurt", root).gameObject.AddComponent<RawImage>();
            vignette.texture = RadialTexture();
            vignette.color = new Color(0.55f, 0.02f, 0.02f, 0f);
            vignette.raycastTarget = false;

            // Health and shield are over the players' heads (GameHud.Bars); stamina is the original's wide sprint bar, centred
            // just above the inventory: 460 x 14 on a near-black track, cyan when boosted, red when locked, labelled inside.
            staminaBox = Node("Stamina", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(460f, 14f));
            UiKit.Box(staminaBox, UiKit.Hex(0x0a0a08));
            UiKit.Frame(staminaBox, UiKit.Line);
            staminaFill = UiKit.Fill("Fill", staminaBox, 1f);
            staminaImage = staminaFill.gameObject.AddComponent<RawImage>();
            staminaImage.texture = staminaTex;
            staminaImage.raycastTarget = false;
            staminaLabel = kit.Label(Node("Label", staminaBox, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(460f, 16f)), "", 12, TextAnchor.LowerCenter, UiKit.Bone, UiKit.Face.Display, 0.14f);
            var labelShadow = staminaLabel.gameObject.AddComponent<Outline>();
            labelShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            labelShadow.effectDistance = new Vector2(1f, -1f);
            BuildHeadBars(root);
            BuildWander(root);
            BuildMoniqueArrow(headBarRoot);
            BuildGhost(root);
            BuildPlaceBanner(root);
            BuildLockpick(root);
            BuildSkillCheck(root);

            // Inventory, bottom centre: eight slots, twelve in testing mode; the selected one is outlined (left mouse uses it).
            strip = Node("Inventory", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(Inventory.Slots * (SlotSize + SlotGap), 86f));
            // Drawn above the full map, so items can be dragged about while Tab holds it open.
            var stripCanvas = strip.gameObject.AddComponent<Canvas>();
            stripCanvas.overrideSorting = true;
            stripCanvas.sortingOrder = 30;
            strip.gameObject.AddComponent<GraphicRaycaster>();
            for (int i = 0; i < Inventory.TestingSlots; i++)
            {
                RectTransform slot = Node($"Slot {i + 1}", strip, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * (SlotSize + SlotGap), 0f), new Vector2(SlotSize, SlotSize));
                slot.gameObject.AddComponent<RectMask2D>();
                UiKit.Box(slot, SlotColor);
                slotFrames[i] = UiKit.Frame(slot, SlotBorder);
                RectTransform sel = Node("Selected", slot, new Vector2(0f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 3f));
                sel.anchorMax = new Vector2(1f, 0f);
                slotSelectBars[i] = UiKit.Box(sel, UiKit.Red);
                slotBoxes[i] = slot.gameObject;
                // The item's picture, as the original's (its own art); the slot is empty without one.
                slotIcons[i] = Node("Icon", slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 5f), new Vector2(48f, 48f)).gameObject.AddComponent<RawImage>();
                slotIcons[i].raycastTarget = false;
                slotIcons[i].enabled = false;
                RectTransform key = Node("Key Cap", slot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(5f, -4f), new Vector2(16f, 16f));
                UiKit.Box(key, UiKit.Bone);
                kit.Label(Node("Key", key, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f)), SlotKeys[i], 13, TextAnchor.MiddleCenter, UiKit.Hex(0x0a0a08), UiKit.Face.Display, 0f, false);
                slotCounts[i] = kit.Label(Node("Count", slot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -3f), new Vector2(34f, 18f)), "", 15, TextAnchor.UpperRight, UiKit.Bone, UiKit.Face.Display);
                RectTransform nameRt = Node("Name", slot, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(-6f, 16f));
                nameRt.anchorMax = new Vector2(1f, 0f);
                slotNames[i] = kit.Label(nameRt, "", 12, TextAnchor.LowerCenter, UiKit.Muted, UiKit.Face.Display, 0.06f);
                slotNames[i].horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            BuildHunterHud(root);
            BuildInventoryEditor();

            // Interaction prompt above the inventory.
            RectTransform promptRt = Node("Prompt", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 158f), new Vector2(560f, 42f));
            HudPanel(promptRt);
            prompt = kit.Label(UiKit.Fill("Text", promptRt), "", 19, TextAnchor.MiddleCenter, UiKit.Bone);
            promptBox = promptRt.gameObject;
            RectTransform hold = Node("Hold", promptRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(540f, 6f));
            UiKit.Box(hold, new Color(0.04f, 0.04f, 0.035f, 0.95f));
            holdFill = UiKit.Fill("Fill", hold);
            UiKit.Box(holdFill, UiKit.Bone);
            holdBar = hold.gameObject;

            // Objective (top left) and compass (top centre).
            RectTransform obj = Node("Objective", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(520f, 66f));
            HudPanel(obj);
            objectiveTitle = kit.Label(Node("Objective", obj, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(490f, 28f)), "", 21, TextAnchor.MiddleLeft, UiKit.Bone, UiKit.Face.Display, 0.08f);
            objectiveSub = kit.Label(Node("Objective Detail", obj, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -38f), new Vector2(490f, 22f)), "", 15, TextAnchor.MiddleLeft, UiKit.Muted);
            RectTransform comp = Node("Compass", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(220f, 38f));
            UiKit.Box(comp, PanelColor);
            UiKit.Frame(comp, PanelBorder);
            compass = kit.Label(UiKit.Fill("Text", comp), "", 20, TextAnchor.MiddleCenter, UiKit.Bone, UiKit.Face.Display, 0.12f);

            // Testing mode and speed mode under the objective; the feed on the right, under the minimap.
            modeLine = kit.Label(Node("Mode", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -98f), new Vector2(560f, 22f)), "", 14, TextAnchor.MiddleLeft, UiKit.Red, UiKit.Face.Type, 0.2f);
            notices = kit.Label(Node("Notices", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -310f), new Vector2(560f, 220f)), "", 16, TextAnchor.UpperRight, UiKit.Bone);
            notices.lineSpacing = 1.3f;

            // Downed: a dark wash (you can still crawl) and the way back up; "You are down" comes from the presenter.
            RectTransform down = UiKit.Fill("Downed", root);
            UiKit.Box(down, new Color(0.08f, 0f, 0f, 0.35f));
            kit.Label(Node("Hint", down, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 40f)), "CRAWL TO SAFETY   ·   R TO GET BACK UP (TESTING)", 18, TextAnchor.MiddleCenter, UiKit.Bone, UiKit.Face.Display, 0.14f);
            downedScreen = down.gameObject;

            BuildNpcTags(root);
            BuildFxPanel(root);
            map = new MapHud(root, kit.Mono);
            BuildResults(root);
            BuildMenu(root);
            BuildMainMenu(root);
            BuildLobby(root);
            BuildHowToPlay(root);

            RectTransform gen = UiKit.Fill("Generating", root);
            UiKit.Box(gen, new Color(0f, 0f, 0f, 0.85f), true);
            kit.Label(Node("Text", gen, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 60f)), "BUILDING A NEW MAP...", 32, TextAnchor.MiddleCenter, UiKit.Bone, UiKit.Face.Display, 0.2f);
            generating = gen.gameObject;
            generating.SetActive(false);

            Overlays = GetComponent<ScreenOverlays>() ?? gameObject.AddComponent<ScreenOverlays>();
            Presenter = GetComponent<MatchPresenter>() ?? gameObject.AddComponent<MatchPresenter>();
            Presenter.world = world;
            Presenter.overlays = Overlays;
            Effects = GetComponent<MatchEffects>() ?? gameObject.AddComponent<MatchEffects>();
            Effects.world = world;
            ItemsInPlay = GetComponent<ItemViews>() ?? gameObject.AddComponent<ItemViews>();
            ItemsInPlay.world = world;
        }

        static void SetFill(RectTransform fill, float k)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(k), 1f);
            fill.offsetMax = new Vector2(k >= 0.999f ? -1f : 0f, -1f);
        }

        static Texture2D RadialTexture()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, r))));
                }
            tex.Apply();
            return tex;
        }

        /// <summary>Testing mode: a button for every stun and flash effect, played on yourself, and one to respawn the NPCs.</summary>
        void BuildFxPanel(Transform parent)
        {
            RectTransform panel = Node("Test Effects", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -140f), new Vector2(240f, 480f));
            kit.Label(Node("Title", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(240f, 18f)), "TEST EFFECTS", 12, TextAnchor.MiddleLeft, UiKit.Hex(0xb8b0a0), UiKit.Face.Mono, 0.2f);
            float y = -22f;
            foreach (var (fx, label) in MatchSim.TestFxButtons)
            {
                TestFx f = fx;
                kit.Button(panel, label, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(230f, 28f), UiKit.ButtonStyle.Normal, () => PlayTestFx(f), 12);
                y -= 32f;
            }
            kit.Button(panel, "Respawn NPCs", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(230f, 28f), UiKit.ButtonStyle.Normal, () => MatchHost.For(world)?.RespawnNpcs(), 12);
            y -= 32f;
            kit.Button(panel, "Spawn survivor dummy", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(230f, 28f), UiKit.ButtonStyle.Normal, () => MatchHost.For(world)?.SpawnDummy(Role.Survivor), 12);
            y -= 32f;
            kit.Button(panel, "Spawn Zach dummy", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(230f, 28f), UiKit.ButtonStyle.Normal, () => MatchHost.For(world)?.SpawnDummy(Role.Hunter), 12);
            y -= 32f;
            kit.Button(panel, "Clear dummies", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(230f, 28f), UiKit.ButtonStyle.Normal, () => MatchHost.For(world)?.ClearDummies(), 12);
            fxPanel = panel.gameObject;
            fxPanel.SetActive(false);
        }

        public void PlayTestFx(TestFx fx)
        {
            MatchHost h = MatchHost.For(world);
            if (h == null || h.Sim == null) return;
            h.PlayTestFx(fx);
        }

        // ---------------------------------------------------------------- behaviour

        /// <summary>Shows a short message in the feed (top right) for a few seconds.</summary>
        public void Notify(string text)
        {
            noticeList.Add((text, Time.unscaledTime + 7f));
            if (noticeList.Count > 6) noticeList.RemoveAt(0);
        }

        void Bind()
        {
            if (world == null || world.Player == null) return;
            if (Presenter != null && Presenter.world == null) Presenter.world = world;
            if (Effects != null && Effects.world == null) Effects.world = world;
            if (ItemsInPlay != null && ItemsInPlay.world == null) ItemsInPlay.world = world;
            if (player != world.Player)
            {
                if (player != null) player.Notice -= Notify;
                player = world.Player;
                player.Notice += Notify;
            }
            MatchHost h = player.Host;
            if (h == host) return;
            if (host != null) host.EventRaised -= OnMatchEvent;
            host = h;
            if (host != null) host.EventRaised += OnMatchEvent;
        }

        void OnMatchEvent(GameEvent e)
        {
            switch (e.Kind)
            {
                case EventKind.Feed:
                case EventKind.Result:
                    if (!string.IsNullOrEmpty(e.Text)) Notify(e.Text);
                    break;
                case EventKind.Hit:
                    if (host != null && e.A == host.LocalId) flash = 1f;
                    break;
                case EventKind.Jarvis:
                    // JARVIS shows the whole map to the survivor who set it going, and no one else.
                    if (host != null && host.Local != null && e.A == host.LocalId && host.Local.JarvisT > 0f) map.RevealForJarvis();
                    break;
                case EventKind.Talk:
                    // A ghost is one with a body: whose it was, across everyone's screen.
                    if (e.Text == "reanimated" && host != null && host.Sim != null)
                    {
                        string name = host.Sim.BodyById(e.B)?.Name ?? host.Sim.Get(e.A)?.BodyName;
                        if (!string.IsNullOrEmpty(name)) Banner($"{name} has been reanimated");
                    }
                    break;
            }
        }

        void Update()
        {
            Bind();
            UpdateOnline();
            canvas.enabled = visible;
            Keyboard kb = Keyboard.current;
            SyncMap();
            FullMapOpen = map != null && map.FullOpen;
            bool esc = (kb != null && kb.escapeKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (esc && Overlays != null && Overlays.NoteShowing) Overlays.HideNote();
            else if (esc && modalOpen) CloseModal();
            else if (!MainMenuOpen)
            {
                if (esc && map.FullOpen) map.SetOpen(false);
                else if (esc) SetMenu(!escOpen);
                if (kb != null && !escOpen && kb.vKey.wasPressedThisFrame) ToggleSpeedMode();
            }
            UpdateMenus();
            map.Update(visible && !MainMenuOpen);
            // The inventory draws above the full map (its own canvas, sorted on top), so it's hidden outright on the main
            // menu and in the lobby rather than left to be covered.
            bool bar = !MainMenuOpen && !lobbyOpen && player != null && player.Me != null && !player.Me.Ghost;
            if (strip.gameObject.activeSelf != bar) strip.gameObject.SetActive(bar);
            if (player == null || player.Me == null) return;
            Refresh();
        }

        /// <summary>
        /// Paints the maps for the level when it is new to them: the first level (generated before the HUD woke), and every
        /// New map after it.
        /// </summary>
        public void SyncMap()
        {
            EnsureBuilt();
            if (world == null || world.Layout == null) return;
            if (needsMapBind || map.BoundLayout != world.Layout)
            {
                needsMapBind = false;
                map.Bind(world);
            }
        }

        /// <summary>Copies the game state into the widgets (also called by captures and tests).</summary>
        public void Refresh()
        {
            EnsureBuilt();
            Bind();
            RefreshMenus();
            SimPlayer me = player != null ? player.Me : null;
            if (me == null) return;
            bool zach = me.Role == Role.Hunter;
            bool downed = me.Health == Game.Health.Downed;

            // Stamina (the original's SPRINT bar).
            float maxStamina = MoveState.MaxStamina(me.Role, me.Move.BoostT);
            bool sprintLocked = me.Move.StaminaLock > 0f || me.Move.SprintBlocked;
            staminaLabel.text = sprintLocked ? "SPRINT LOCKED " + Mathf.Max(me.Move.StaminaLock, 0f).ToString("0.0") + "s" : "SPRINT " + me.Move.Stamina.ToString("0.0") + "s";
            SetFill(staminaFill, me.Move.Stamina / maxStamina);
            staminaImage.texture = sprintLocked ? staminaLocked : me.Move.BoostT > 0f ? staminaBoost : staminaTex;

            // Inventory (survivors).
            Inventory inv = me.Inv;
            int limit = zach ? 0 : inv.Limit;
            if (limit != shownLimit)
            {
                shownLimit = limit;
                for (int i = 0; i < Inventory.TestingSlots; i++) slotBoxes[i].SetActive(i < limit);
            }
            for (int i = 0; i < limit; i++)
            {
                ItemType? item = inv.ItemAt(i);
                bool selected = i == player.SelectedSlot;
                foreach (Image e in slotFrames[i]) e.color = selected ? SlotBorderSelected : SlotBorder;
                slotSelectBars[i].enabled = selected;
                bool golden = inv.GoldenAt(i);
                Texture2D icon = item.HasValue ? ItemIcons.For(item.Value, golden) : null;
                slotIcons[i].enabled = icon != null;
                if (icon != null && slotIcons[i].texture != icon) slotIcons[i].texture = icon;
                int count = inv.CountAt(i);
                // A gun (and the Hemp Beam) shows its rounds left out of its capacity: 5/5, 4/5...
                string rounds = inv.RoundsAt(i);
                slotCounts[i].text = !item.HasValue ? "" : inv.Infinite ? "∞" : rounds ?? (count > 1 ? count.ToString() : "");
                slotNames[i].text = item.HasValue ? (golden ? "LEGENDARY PUMP" : Items.Short(item.Value).ToUpperInvariant()) : "";
            }
            LayoutSlots(inv, limit);

            RefreshSurvivorAbilities(me, strip.sizeDelta.x);
            UpdateEditor(me);

            // The prompt, with the Space action beside it.
            string p = player.InteractPrompt;
            string space = player.SpacePrompt;
            bool showPrompt = (!string.IsNullOrEmpty(p) || !string.IsNullOrEmpty(space)) && !MenuOpen && !map.FullOpen && !editorOpen;
            promptBox.SetActive(showPrompt);
            prompt.text = !string.IsNullOrEmpty(p) && !string.IsNullOrEmpty(space) ? $"{p}   ·   {space}" : !string.IsNullOrEmpty(p) ? p : space ?? "";
            holdBar.SetActive(showPrompt && player.HoldProgress >= 0f);
            SetFill(holdFill, player.HoldProgress);
            if (player.HoldProgress >= 0f && !showPrompt && me.ActionDur > 0f && !MenuOpen)
            {
                promptBox.SetActive(true);
                prompt.text = Texts.ActionLabel(me.Action);
                holdBar.SetActive(true);
            }

            string objectiveText = host != null && host.Sim != null ? ObjectiveText(host.Sim, me) : ObjectiveText(world);
            int nl = objectiveText.IndexOf('\n');
            objectiveTitle.text = (nl < 0 ? objectiveText : objectiveText.Substring(0, nl)).ToUpperInvariant();
            objectiveSub.text = nl < 0 ? "" : StripColor(objectiveText.Substring(nl + 1));

            Vector2 f = player.viewer != null ? player.viewer.Facing : Vector2.up;
            float heading = Mathf.Repeat(Mathf.Atan2(f.x, f.y) * Mathf.Rad2Deg, 360f);
            compass.text = $"{Cardinal(heading)}   {Mathf.RoundToInt(heading):000}°";

            float now = Time.unscaledTime;
            noticeList.RemoveAll(n => n.until < now);
            var sb = new System.Text.StringBuilder();
            foreach (var n in noticeList)
            {
                // The feed fades over its last quarter, as the original's.
                float a = Mathf.Clamp01((n.until - now) / 1.75f);
                sb.Append("<color=#d9d3c1").Append(Mathf.RoundToInt(a * 255f).ToString("x2")).Append('>').Append(n.text).Append("</color>\n");
            }
            notices.text = sb.ToString();

            flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime * 2.5f);
            // Each bite of the dark past the edge of the woods: the screen's edges pulse red.
            if (me.BoundaryHits != lastBoundaryHits)
            {
                if (me.BoundaryHits > lastBoundaryHits) edgePulse = 1f;
                lastBoundaryHits = me.BoundaryHits;
            }
            edgePulse = Mathf.Max(0f, edgePulse - Time.unscaledDeltaTime * 1.2f);
            float hurt = zach ? 0f : Mathf.Clamp01(1f - me.Hp / 0.45f);
            float pulse = edgePulse * edgePulse * (3f - 2f * edgePulse);
            // (Night vision puts no green halo round the screen.)
            vignette.color = new Color(Mathf.Lerp(0.55f, 0.85f, pulse), 0.02f, 0.02f, Mathf.Clamp01(Mathf.Max(hurt * 0.85f + flash * 0.5f, pulse)));
            downedScreen.SetActive(downed);
            fxPanel.SetActive(host != null && host.Sim != null && host.Sim.TestMode && me.Role != Role.Spectator && !MainMenuOpen);
            if (host != null && host.Sim != null) RefreshHunter(me, host.Sim);
            RefreshNpcTags(me);
            RefreshHeadBars(me);
            RefreshWander(me);
            RefreshMoniqueArrow(me);
            RefreshGhost(me);
            RefreshPlaceBanner(me);
            RefreshLockpick(me, host != null ? host.Sim : null);
            RefreshSkillCheck(me, host != null ? host.Sim : null);
            RefreshMatch(me);
        }

        static string StripColor(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "");

        /// <summary>The objective line from the level alone (before the match runs).</summary>
        public static string ObjectiveText(SandboxWorld world)
        {
            int running = GeneratorObjective.RunningCount, total = GeneratorObjective.All.Count;
            if (world != null && world.Gate != null && world.Gate.IsOpen)
                return "The gate is open: get out through the yard\n<color=#9c9b91>North side of the building</color>";
            if (total > 0 && running >= total)
                return $"Pull the lever at the north gate\n<color=#9c9b91>Generators {running}/{total}</color>";
            return $"Start the generators\n<color=#9c9b91>Generators {running}/{total}</color>";
        }

        /// <summary>The objective line from the match: survivors repair, then open the gate, then escape; Zach hunts.</summary>
        public static string ObjectiveText(MatchSim sim, SimPlayer me)
        {
            int running = 0;
            foreach (GenState g in sim.Gens) if (g.Repaired) running++;
            int need = sim.Bal.RequiredGenerators;
            string gens = $"<color=#9c9b91>Generators {Mathf.Min(running, need)}/{need}</color>";
            if (sim.Result != null) return $"{sim.Result.Reason}\n<color=#9c9b91>{(sim.Result.Winner == Winner.Hunters ? "Zach wins" : "Survivors win")}</color>";
            if (me.Ghost && me.Body != 0 && me.PossessT > 0f) return $"Taking the body: it rises\n<color=#9c9b91>{me.BodyName}</color>";
            if (me.Ghost && me.Body != 0) return $"Help your team: hold E at a generator or a gate a survivor is working\n<color=#9c9b91>Reanimated{(string.IsNullOrEmpty(me.BodyName) ? "" : " as " + me.BodyName)}: no items, no fighting</color>";
            if (me.Ghost && me.Spent) return "You see everything now\n<color=#9c9b91>Knocked out of your body: you can't take another</color>";
            if (me.Ghost) return "Find a body, help your team: E by a body possesses it\n<color=#9c9b91>You pass through walls; only other ghosts can see you</color>";
            if (me.Role == Role.Hunter)
                return sim.HatchOpen ? $"The hatch is open by the dock: slam it shut (E)\n{gens}" : $"Hunt them down: carry the downed to a stake\n{gens}";
            if (me.Role == Role.Spectator) return "Spectating";
            if (sim.HatchOpen) return "The hatch is open by the lake's dock: jump in to escape\n<color=#9c9b91>Or open a gate</color>";
            if (sim.Gate.Open || sim.Gate2.Open)
                return "A gate is open: get out through its yard\n<color=#9c9b91>The building's north side, or beside Miga Sushi</color>";
            if (sim.Gate.Powered) return "Hold E at a gate's lever to open it\n<color=#9c9b91>The building's north side, or beside Miga Sushi</color>";
            return $"Repair the generators\n{gens}";
        }

        public static string Cardinal(float heading)
        {
            string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return names[Mathf.RoundToInt(Mathf.Repeat(heading, 360f) / 45f) % 8];
        }
    }
}
