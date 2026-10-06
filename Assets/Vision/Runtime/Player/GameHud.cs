using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vision.World;

namespace Vision.Player
{
    /// <summary>
    /// The game's HUD, built in code with uGUI and scaled from a 1080p reference so it grows with the screen:
    /// health, shield and stamina bars (bottom left), the eight inventory slots with their number keys (bottom
    /// centre), the interaction prompt with a progress bar for held work, the objective (generators, then the gate)
    /// and a compass (top), the minimap and full map (M), short notices, a red edge when hurt and the downed screen.
    /// The main menu (MANHUNT: Testing mode, Quit) opens first. Esc opens the game menu, which does not pause, as in
    /// the original: Resume, Speed mode (V), New map (a new random seed, shown), Look settings, Full screen and Quit to
    /// main menu.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        public SandboxWorld world;
        public VisionDebugHud debugHud;
        [Tooltip("Shown at all; captures hide it for the look comparisons.")]
        public bool visible = true;

        /// <summary>True while a menu is open (the main menu or the Esc menu): the player ignores aim and movement.</summary>
        public static bool MenuOpen => escOpen || MainMenuOpen;
        /// <summary>The title screen is showing.</summary>
        public static bool MainMenuOpen { get; private set; }
        static bool escOpen;

        /// <summary>The original's kicker line and blurb.</summary>
        public const string Kicker = "Crystal Lake · Night shoot";
        public const string Tagline = "Zach Branch plays the masked killer on the new Crystal Lake series. Tonight he stopped acting. Start every generator, power the gate and get out of the woods.";

        static readonly Color Panel = new Color(0.04f, 0.04f, 0.045f, 0.72f);
        static readonly Color Text = new Color(0.86f, 0.85f, 0.80f);
        static readonly Color Muted = new Color(0.62f, 0.61f, 0.57f);
        static readonly Color HealthColor = new Color(0.66f, 0.16f, 0.13f);
        static readonly Color StaminaColor = new Color(0.80f, 0.72f, 0.42f);
        static readonly Color ShieldColor = new Color(0.30f, 0.58f, 0.95f);
        static readonly Color ExhaustedColor = new Color(0.45f, 0.40f, 0.30f);

        Canvas canvas;
        Font font;
        RectTransform healthFill, shieldFill, staminaFill, holdFill;
        Image staminaImage;
        Text healthValue, shieldValue, staminaValue, prompt, objective, compass, notices;
        GameObject promptBox, holdBar, downedScreen, menu, mainMenu, generating;
        Text fullScreenLabel, speedLabel, seedLabel, menuTitle, modeLine;
        MapHud map;
        bool needsMapBind;
        /// <summary>The minimap and full map.</summary>
        public MapHud Map => map;
        readonly Image[] slotIcons = new Image[Inventory.Slots];
        readonly Text[] slotCounts = new Text[Inventory.Slots], slotNames = new Text[Inventory.Slots];
        RawImage vignette;
        readonly List<(string text, float until)> noticeList = new List<(string, float)>();
        float flash;
        PlayerController player;
        PlayerStats stats;

        void Awake()
        {
            escOpen = false;
            MainMenuOpen = false;
            EnsureBuilt();
        }

        void OnEnable() => SandboxWorld.Built += OnBuilt;
        void OnDisable() => SandboxWorld.Built -= OnBuilt;

        void Start()
        {
            // Automated captures go straight in; players start at the title screen.
            if (VisionCapture.Requested) needsMapBind = true;
            else ShowMainMenu();
        }

        void OnBuilt(SandboxWorld w)
        {
            if (w != world) return;
            needsMapBind = true;
            if (GameSession.TestingMode && w.Player != null) GameSession.ApplyTestKit(w.Player.GetComponent<PlayerStats>());
        }

        void EnsureBuilt()
        {
            if (canvas != null) return;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        void OnDestroy()
        {
            escOpen = false;
            MainMenuOpen = false;
            Time.timeScale = 1f;
        }

        // ---------------------------------------------------------------- building

        RectTransform Node(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        Image Box(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text Label(RectTransform rt, string text, int size, TextAnchor align, Color color)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

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
            Transform root = canvasGo.transform;

            // Hurt vignette under everything else.
            vignette = Stretch(Node("Hurt", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)).gameObject.AddComponent<RawImage>();
            vignette.texture = RadialTexture();
            vignette.color = new Color(0.55f, 0.02f, 0.02f, 0f);
            vignette.raycastTarget = false;

            // Health and stamina, bottom left.
            RectTransform vitalsBox = Node("Vitals", root, Vector2.zero, Vector2.zero, new Vector2(28f, 28f), new Vector2(360f, 128f));
            Box(vitalsBox, Panel);
            healthFill = Bar(vitalsBox, "Health", new Vector2(14f, -16f), HealthColor, out healthValue, out _);
            shieldFill = Bar(vitalsBox, "Shield", new Vector2(14f, -52f), ShieldColor, out shieldValue, out _);
            staminaFill = Bar(vitalsBox, "Stamina", new Vector2(14f, -88f), StaminaColor, out staminaValue, out staminaImage);

            // Inventory, bottom centre.
            RectTransform strip = Node("Inventory", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(Inventory.Slots * 78f + 10f, 92f));
            Box(strip, Panel);
            for (int i = 0; i < Inventory.Slots; i++)
            {
                RectTransform slot = Node($"Slot {i + 1}", strip, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f + i * 78f, 0f), new Vector2(70f, 74f));
                Box(slot, new Color(0.12f, 0.12f, 0.13f, 0.9f));
                slotIcons[i] = Box(Node("Icon", slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(34f, 34f)), Color.clear);
                Label(Node("Key", slot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(5f, -3f), new Vector2(20f, 18f)), (i + 1).ToString(), 15, TextAnchor.UpperLeft, Muted);
                slotCounts[i] = Label(Node("Count", slot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-5f, -3f), new Vector2(30f, 18f)), "", 15, TextAnchor.UpperRight, Text);
                slotNames[i] = Label(Node("Name", slot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(68f, 16f)), "", 12, TextAnchor.LowerCenter, Muted);
            }

            // Interaction prompt above the inventory.
            RectTransform promptRt = Node("Prompt", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(420f, 40f));
            Box(promptRt, Panel);
            prompt = Label(Stretch(Node("Text", promptRt, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)), "", 22, TextAnchor.MiddleCenter, Text);
            promptBox = promptRt.gameObject;
            RectTransform hold = Node("Hold", promptRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(400f, 8f));
            Box(hold, new Color(0.15f, 0.15f, 0.16f, 0.95f));
            holdFill = Stretch(Node("Fill", hold, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero));
            Box(holdFill, StaminaColor);
            holdBar = hold.gameObject;

            // Objective (top left) and compass (top centre).
            RectTransform obj = Node("Objective", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(560f, 64f));
            objective = Label(obj, "", 20, TextAnchor.UpperLeft, Text);
            RectTransform comp = Node("Compass", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(260f, 40f));
            Box(comp, Panel);
            compass = Label(Stretch(Node("Text", comp, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)), "", 22, TextAnchor.MiddleCenter, Text);

            // Testing mode and speed mode under the objective; notices on the right, under the minimap.
            modeLine = Label(Node("Mode", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -78f), new Vector2(560f, 24f)), "", 16, TextAnchor.UpperLeft, new Color(0.95f, 0.78f, 0.36f));
            notices = Label(Node("Notices", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -310f), new Vector2(520f, 200f)), "", 19, TextAnchor.UpperRight, Text);

            // Downed: a dark wash (you can still crawl), the state and the way back up.
            RectTransform down = Stretch(Node("Downed", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero));
            Box(down, new Color(0.08f, 0f, 0f, 0.35f));
            Label(Node("Title", down, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(600f, 80f)), "You're down", 56, TextAnchor.MiddleCenter, new Color(0.80f, 0.20f, 0.16f));
            Label(Node("Hint", down, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(700f, 40f)), "Crawl to safety   ·   R to get back up (testing)", 24, TextAnchor.MiddleCenter, Text);
            downedScreen = down.gameObject;

            map = new MapHud(root, font);
            BuildMenu(root);
            BuildMainMenu(root);

            RectTransform gen = Stretch(Node("Generating", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero));
            Box(gen, new Color(0f, 0f, 0f, 0.85f));
            Label(Node("Text", gen, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 60f)), "Building a new map...", 34, TextAnchor.MiddleCenter, Text);
            generating = gen.gameObject;
            generating.SetActive(false);
        }

        void BuildMainMenu(Transform root)
        {
            RectTransform screen = Stretch(Node("Main Menu", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero));
            var img = Box(screen, new Color(0.02f, 0.02f, 0.025f, 0.94f));
            img.raycastTarget = true;
            Label(Node("Kicker", screen, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(900f, 40f)), Kicker.ToUpperInvariant(), 24, TextAnchor.MiddleCenter, new Color(0.75f, 0.30f, 0.24f));
            Label(Node("Title", screen, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1200f, 150f)), "MANHUNT", 140, TextAnchor.MiddleCenter, new Color(0.88f, 0.86f, 0.80f));
            Text blurb = Label(Node("Tagline", screen, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(760f, 80f)), Tagline, 21, TextAnchor.MiddleCenter, Muted);
            blurb.horizontalOverflow = HorizontalWrapMode.Wrap;
            float y = -60f;
            RectTransform buttons = Node("Buttons", screen, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(360f, 200f));
            y = 0f;
            MenuButton(buttons, "Testing mode", ref y, () => StartTesting(true));
            MenuButton(buttons, "Quit", ref y, Application.Quit);
            mainMenu = screen.gameObject;
            mainMenu.SetActive(false);
        }

        /// <summary>Shows the title screen (the level keeps running behind it).</summary>
        public void ShowMainMenu()
        {
            EnsureBuilt();
            SetMenu(false);
            map?.SetOpen(false);
            MainMenuOpen = true;
            mainMenu.SetActive(true);
            GameSession.Reset();
        }

        bool played;

        /// <summary>Testing mode: the original's kit, never used up, no win condition. From the title screen after a game, a fresh map.</summary>
        public void StartTesting(bool fromMenu)
        {
            EnsureBuilt();
            MainMenuOpen = false;
            mainMenu.SetActive(false);
            GameSession.TestingMode = true;
            if (fromMenu && played) { NewMap(); return; }
            played = true;
            if (world != null && world.Player != null) GameSession.ApplyTestKit(world.Player.GetComponent<PlayerStats>());
            Notify("Testing mode: every item, never used up");
        }

        /// <summary>Regenerates the whole map with a new random seed.</summary>
        public void NewMap()
        {
            if (world == null) return;
            played = true;
            SetMenu(false);
            StartCoroutine(NewMapRoutine());
        }

        System.Collections.IEnumerator NewMapRoutine()
        {
            generating.SetActive(true);
            yield return null;
            yield return null;
            world.Regenerate();
            yield return null;
            generating.SetActive(false);
            Notify($"New map · seed {world.seed}");
        }

        public void ToggleSpeedMode()
        {
            if (!GameSession.TestingMode) return;
            GameSession.SpeedMode = !GameSession.SpeedMode;
            Notify(GameSession.SpeedMode ? "Speed mode on: full sprint, +500% speed" : "Speed mode off");
        }

        RectTransform Bar(RectTransform parent, string name, Vector2 pos, Color color, out Text value, out Image fillImage)
        {
            Label(Node(name + " Label", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(90f, 26f)), name, 18, TextAnchor.MiddleLeft, Text);
            RectTransform back = Node(name + " Bar", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), pos + new Vector2(96f, -5f), new Vector2(190f, 16f));
            Box(back, new Color(0.15f, 0.15f, 0.16f, 0.95f));
            RectTransform fill = Node("Fill", back, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            fillImage = Box(fill, color);
            value = Label(Node(name + " Value", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), pos + new Vector2(294f, 0f), new Vector2(50f, 26f)), "", 18, TextAnchor.MiddleRight, Text);
            return fill;
        }

        void BuildMenu(Transform root)
        {
            // The game menu doesn't pause (the world goes on, as in the original); it only takes the player's input.
            RectTransform shade = Stretch(Node("Menu", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero));
            var img = Box(shade, new Color(0f, 0f, 0f, 0.45f));
            img.raycastTarget = true;
            RectTransform panel = Node("Panel", shade, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 640f));
            Box(panel, new Color(0.06f, 0.06f, 0.065f, 0.95f));
            menuTitle = Label(Node("Title", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(420f, 50f)), "Testing mode", 38, TextAnchor.MiddleCenter, Text);
            seedLabel = Label(Node("Seed", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(420f, 24f)), "", 18, TextAnchor.MiddleCenter, Muted);
            float y = -110f;
            MenuButton(panel, "Resume", ref y, () => SetMenu(false));
            speedLabel = MenuButton(panel, "", ref y, ToggleSpeedMode);
            MenuButton(panel, "New map", ref y, NewMap);
            MenuButton(panel, "Look settings (F4)", ref y, () =>
            {
                if (debugHud != null) debugHud.lookPanel = !debugHud.lookPanel;
            });
            fullScreenLabel = MenuButton(panel, "", ref y, ToggleFullScreen);
            MenuButton(panel, "Quit to main menu", ref y, ShowMainMenu);
            Label(Node("Controls", panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(440f, 150f)),
                "WASD move   Shift sprint   Mouse aim   E interact (hold on generators and the lever)\nF see-through cone   1-8 use item   M map   V speed mode   R get up\nEsc menu   F3 stats   F4 look   F5 camera effects",
                15, TextAnchor.LowerCenter, Muted);
            menu = shade.gameObject;
            menu.SetActive(false);
        }

        Text MenuButton(RectTransform panel, string text, ref float y, UnityEngine.Events.UnityAction action)
        {
            RectTransform rt = Node(text, panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(320f, 52f));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.16f, 0.16f, 0.17f, 1f);
            var button = rt.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.35f, 1.3f, 1.2f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            button.onClick.AddListener(action);
            y -= 66f;
            return Label(Stretch(Node("Text", rt, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)), text, 24, TextAnchor.MiddleCenter, Text);
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

        // ---------------------------------------------------------------- behaviour

        public void SetMenu(bool open)
        {
            EnsureBuilt();
            if (MainMenuOpen) open = false;
            escOpen = open;
            menu.SetActive(open);
            if (!open && debugHud != null) debugHud.lookPanel = false;
        }

        void ToggleFullScreen()
        {
            bool full = Screen.fullScreenMode != FullScreenMode.Windowed;
            if (full) Screen.SetResolution(Mathf.RoundToInt(Display.main.systemWidth * 0.75f), Mathf.RoundToInt(Display.main.systemHeight * 0.75f), FullScreenMode.Windowed);
            else Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
        }

        /// <summary>Shows a short message at the top right for a few seconds.</summary>
        public void Notify(string text)
        {
            noticeList.Add((text, Time.unscaledTime + 3f));
            if (noticeList.Count > 4) noticeList.RemoveAt(0);
        }

        void Bind()
        {
            if (world == null || world.Player == null || player == world.Player) return;
            if (player != null) player.Notice -= Notify;
            player = world.Player;
            stats = player.GetComponent<PlayerStats>();
            player.Notice += Notify;
            if (stats != null) stats.vitals.Damaged += _ => flash = 1f;
        }

        void Update()
        {
            Bind();
            canvas.enabled = visible;
            Keyboard kb = Keyboard.current;
            SyncMap();
            if (!MainMenuOpen)
            {
                bool esc = (kb != null && kb.escapeKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
                if (esc && map.FullOpen) map.SetOpen(false);
                else if (esc) SetMenu(!escOpen);
                if (kb != null && !escOpen && kb.mKey.wasPressedThisFrame) map.Toggle();
                if (kb != null && !escOpen && kb.vKey.wasPressedThisFrame) ToggleSpeedMode();
            }
            map.Update(visible);
            if (stats == null) return;
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
            if (stats == null) return;
            Vitals v = stats.vitals;
            healthFill.anchorMax = new Vector2(Mathf.Clamp01(v.Health), 1f);
            shieldFill.anchorMax = new Vector2(Mathf.Clamp01(v.Shield), 1f);
            staminaFill.anchorMax = new Vector2(Mathf.Clamp01(v.Stamina / v.maxStamina), 1f);
            staminaImage.color = v.Exhausted ? ExhaustedColor : StaminaColor;
            healthValue.text = Mathf.CeilToInt(v.Health * 100f - 0.01f).ToString();
            shieldValue.text = Mathf.CeilToInt(v.Shield * 100f - 0.01f).ToString();
            staminaValue.text = Mathf.CeilToInt(v.Stamina).ToString();

            Inventory inv = stats.inventory;
            for (int i = 0; i < Inventory.Slots; i++)
            {
                ItemType? item = inv.ItemAt(i);
                slotIcons[i].color = item.HasValue ? Items.Info(item.Value).color : Color.clear;
                slotCounts[i].text = item.HasValue && inv.CountAt(i) > 1 ? inv.CountAt(i).ToString() : "";
                slotNames[i].text = item.HasValue ? Items.Short(item.Value) : "";
            }

            string p = player.InteractPrompt;
            promptBox.SetActive(!string.IsNullOrEmpty(p) && !v.IsDowned && !MenuOpen && !map.FullOpen);
            prompt.text = string.IsNullOrEmpty(p) ? "" : $"[E]  {p}";
            holdBar.SetActive(player.HoldProgress >= 0f);
            holdFill.anchorMax = new Vector2(Mathf.Clamp01(player.HoldProgress), 1f);
            if (stats.DrinkLeft > 0f)
            {
                promptBox.SetActive(true);
                prompt.text = "Drinking a mini shield...";
                holdBar.SetActive(true);
                holdFill.anchorMax = new Vector2(1f - stats.DrinkLeft / PlayerStats.ShieldDrinkTime, 1f);
            }

            objective.supportRichText = true;
            objective.text = ObjectiveText(world);
            modeLine.text = !GameSession.TestingMode ? "" : GameSession.SpeedMode ? "TESTING MODE   ·   speed mode on (V)" : "TESTING MODE";
            if (world != null) seedLabel.text = $"Seed {world.seed}";
            speedLabel.text = GameSession.SpeedMode ? "Speed mode: on (V)" : "Speed mode: off (V)";
            menuTitle.text = GameSession.TestingMode ? "Testing mode" : "Menu";

            Vector2 f = player.viewer != null ? player.viewer.Facing : Vector2.up;
            float heading = Mathf.Repeat(Mathf.Atan2(f.x, f.y) * Mathf.Rad2Deg, 360f);
            compass.text = $"{Cardinal(heading)}   {Mathf.RoundToInt(heading):000}°";

            float now = Time.unscaledTime;
            noticeList.RemoveAll(n => n.until < now);
            var sb = new System.Text.StringBuilder();
            foreach (var n in noticeList) sb.AppendLine(n.text);
            notices.text = sb.ToString();

            flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime * 2.5f);
            float hurt = Mathf.Clamp01(1f - v.Health / 0.45f);
            vignette.color = new Color(0.55f, 0.02f, 0.02f, Mathf.Clamp01(hurt * 0.85f + flash * 0.5f));
            downedScreen.SetActive(v.IsDowned);
            if (fullScreenLabel != null) fullScreenLabel.text = Screen.fullScreenMode == FullScreenMode.Windowed ? "Full screen: off" : "Full screen: on";
        }

        /// <summary>The objective line: start the generators, then pull the gate lever, then get out.</summary>
        public static string ObjectiveText(SandboxWorld world)
        {
            int running = GeneratorObjective.RunningCount, total = GeneratorObjective.All.Count;
            if (world != null && world.Gate != null && world.Gate.IsOpen)
                return "The gate is open: get out through the yard\n<color=#9c9b91>North side of the building</color>";
            if (total > 0 && running >= total)
                return $"Pull the lever at the north gate\n<color=#9c9b91>Generators {running}/{total}</color>";
            return $"Start the generators\n<color=#9c9b91>Generators {running}/{total}</color>";
        }

        public static string Cardinal(float heading)
        {
            string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return names[Mathf.RoundToInt(Mathf.Repeat(heading, 360f) / 45f) % 8];
        }
    }
}
