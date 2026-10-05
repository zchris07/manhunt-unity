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
    /// and a compass (top), short notices (top right), a red edge when hurt, the downed screen and the pause menu
    /// (Esc): Resume, Look settings, Full screen, Quit.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        public SandboxWorld world;
        public VisionDebugHud debugHud;
        [Tooltip("Shown at all; captures hide it for the look comparisons.")]
        public bool visible = true;

        /// <summary>True while the pause menu is open: the game is paused and ignores aim and movement.</summary>
        public static bool MenuOpen { get; private set; }

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
        GameObject promptBox, holdBar, downedScreen, menu;
        Text fullScreenLabel;
        readonly Image[] slotIcons = new Image[Inventory.Slots];
        readonly Text[] slotCounts = new Text[Inventory.Slots], slotNames = new Text[Inventory.Slots];
        RawImage vignette;
        readonly List<(string text, float until)> noticeList = new List<(string, float)>();
        float flash;
        PlayerController player;
        PlayerStats stats;

        void Awake()
        {
            MenuOpen = false;
            EnsureBuilt();
        }

        void EnsureBuilt()
        {
            if (canvas != null) return;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        void OnDestroy()
        {
            MenuOpen = false;
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

            // Notices, top right.
            notices = Label(Node("Notices", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(520f, 200f)), "", 19, TextAnchor.UpperRight, Text);

            // Downed: a dark wash (you can still crawl), the state and the way back up.
            RectTransform down = Stretch(Node("Downed", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero));
            Box(down, new Color(0.08f, 0f, 0f, 0.35f));
            Label(Node("Title", down, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(600f, 80f)), "You're down", 56, TextAnchor.MiddleCenter, new Color(0.80f, 0.20f, 0.16f));
            Label(Node("Hint", down, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(700f, 40f)), "Crawl to safety   ·   R to get back up (testing)", 24, TextAnchor.MiddleCenter, Text);
            downedScreen = down.gameObject;

            BuildMenu(root);
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
            RectTransform shade = Stretch(Node("Pause", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero));
            var img = Box(shade, new Color(0f, 0f, 0f, 0.6f));
            img.raycastTarget = true;
            RectTransform panel = Node("Panel", shade, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 560f));
            Box(panel, new Color(0.06f, 0.06f, 0.065f, 0.95f));
            Label(Node("Title", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(400f, 50f)), "Paused", 40, TextAnchor.MiddleCenter, Text);
            float y = -100f;
            MenuButton(panel, "Resume", ref y, () => SetMenu(false));
            MenuButton(panel, "Look settings (F4)", ref y, () =>
            {
                if (debugHud != null) debugHud.lookPanel = !debugHud.lookPanel;
            });
            fullScreenLabel = MenuButton(panel, "", ref y, ToggleFullScreen);
            MenuButton(panel, "Quit", ref y, Application.Quit);
            Label(Node("Controls", panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(400f, 150f)),
                "WASD move   Shift sprint   Mouse aim\nE interact (hold on generators and the lever)   F see-through cone\n1-8 use item   Esc pause   F3 stats   F4 look   F5 camera effects",
                16, TextAnchor.LowerCenter, Muted);
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
            MenuOpen = open;
            menu.SetActive(open);
            Time.timeScale = open ? 0f : 1f;
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
            if (player != null || world == null || world.Player == null) return;
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
            if (kb != null && kb.escapeKey.wasPressedThisFrame) SetMenu(!MenuOpen);
            if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame) SetMenu(!MenuOpen);
            if (stats == null) return;
            Refresh();
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
            promptBox.SetActive(!string.IsNullOrEmpty(p) && !v.IsDowned && !MenuOpen);
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
