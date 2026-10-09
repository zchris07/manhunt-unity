using UnityEngine;
using UnityEngine.UI;
using Vision.Audio;
using Vision.Game;
using Vision.UI;

namespace Vision.Player
{
    /// <summary>The title screen (the original's landing page), the settings (Esc) and the How to play modal.</summary>
    public sealed partial class GameHud
    {
        public static readonly (string key, string action)[] SurvivorControls =
        {
            ("W A S D", "Move"), ("Mouse", "Aim"), ("Shift", "Sprint"), ("C / Ctrl", "Crouch"), ("E", "Interact"),
            ("Left mouse", "Use item"), ("Wheel / 1-8", "Select item"), ("G", "Drop item"), ("Tab", "Arrange inventory"),
            ("Q", "JARVIS"), ("Space", "Barricade · hold breath"), ("M", "Map"),
        };

        public static readonly (string key, string action)[] HunterControls =
        {
            ("W A S D", "Move"), ("Shift", "Sprint"), ("Mouse", "Look"), ("Left mouse", "Machete (hold to charge)"), ("Right mouse", "Lunge"),
            ("F", "Soundcloud Burst"), ("Q", "Hemp Battery (toggle)"), ("R", "Hemp Beam (once you have it)"),
            ("Space", "Penjamin (50 Nic once Chacko gives it)"), ("E", "Interact"), ("M", "Map"),
        };

        public static readonly (string key, string action)[] GeneralControls =
        {
            ("Esc", "Settings"), ("T", "Switch role (testing)"), ("V", "Speed mode (testing)"), ("R", "Get back up (testing)"), ("Click / ← →", "Switch spectate target"),
        };

        const string NameKey = "manhunt.name";

        GameObject menu, mainMenu, howTo;
        Text menuTitle, seedLabel, speedLabel, fullScreenLabel, paceLabel, landingError, title;
        Slider paceSlider, masterSlider, sfxSlider, ambienceSlider;
        GameObject survivorTable, hunterTable;
        InputField nameField, roomField;
        RawImage grain;
        bool played;
        float grainStep;

        // ---------------------------------------------------------------- the title screen

        void BuildMainMenu(Transform parent)
        {
            RectTransform screen = UiKit.Fill("Main Menu", parent);
            var back = screen.gameObject.AddComponent<RawImage>();
            back.texture = UiKit.Backdrop();
            back.raycastTarget = true;
            // Dark pines along the bottom.
            RectTransform pines = Node("Pines", screen, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            pines.anchorMin = Vector2.zero;
            pines.anchorMax = new Vector2(1f, 0.38f);
            pines.offsetMin = pines.offsetMax = Vector2.zero;
            var pinesImg = pines.gameObject.AddComponent<RawImage>();
            pinesImg.texture = UiKit.Pines();
            pinesImg.color = new Color(1f, 1f, 1f, 0.25f * 4f);
            pinesImg.raycastTarget = false;

            RectTransform grid = Node("Landing", screen, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1180f, 470f));

            // The hero: kicker, title, tagline.
            RectTransform hero = Node("Hero", grid, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(650f, 470f));
            Text kicker = kit.Label(Node("Kicker", hero, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -40f), new Vector2(600f, 30f)), Kicker.ToUpperInvariant(), 17, TextAnchor.MiddleLeft, UiKit.Red, UiKit.Face.Type, 0.3f, false);
            float kw = kicker.preferredWidth + (Kicker.Length - 1) * 0.3f * 17f + 26f;
            RectTransform kbox = Node("Kicker Frame", hero, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -38f), new Vector2(kw, 34f));
            UiKit.Frame(kbox, UiKit.Red);
            title = kit.Label(Node("Title", hero, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(-4f, -86f), new Vector2(660f, 140f)), "MANHUNT", 124, TextAnchor.MiddleLeft, UiKit.Bone, UiKit.Face.DisplayBold, 0.18f, false);
            var dark = title.gameObject.AddComponent<Outline>();
            dark.effectColor = new Color(0f, 0f, 0f, 0.6f);
            dark.effectDistance = new Vector2(4f, 4f);
            var red = title.gameObject.AddComponent<Shadow>();
            red.effectColor = UiKit.A(UiKit.Red, 0.7f);
            red.effectDistance = new Vector2(3f, 0f);
            var cyan = title.gameObject.AddComponent<Shadow>();
            cyan.effectColor = new Color(90f / 255f, 160f / 255f, 170f / 255f, 0.25f);
            cyan.effectDistance = new Vector2(-3f, 0f);
            kit.Paragraph(Node("Tagline", hero, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -250f), new Vector2(560f, 160f)), Tagline, 19, UiKit.Muted, UiKit.Face.Mono, 1.35f);

            // The card.
            const float cw = 470f, ch = 452f;
            RectTransform card = kit.Card("Card", grid, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(cw, ch));
            float y = -28f;
            kit.FieldLabel(card, "Your name", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), cw - 56f);
            y -= 26f;
            nameField = kit.Field(card, "Name", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(cw - 56f, 46f), "2-16 characters", 16);
            nameField.text = SavedName();
            y -= 62f;
            kit.Button(card, "Create lobby", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(cw - 56f, 54f), UiKit.ButtonStyle.Primary, OnCreate, 22);
            y -= 74f;
            // "or", between two rules.
            RectTransform lineL = Node("Rule", card, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(28f, y - 8f), new Vector2(cw * 0.5f - 56f, 1f));
            UiKit.Box(lineL, UiKit.Line);
            RectTransform lineR = Node("Rule", card, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-28f, y - 8f), new Vector2(cw * 0.5f - 56f, 1f));
            UiKit.Box(lineR, UiKit.Line);
            kit.Label(Node("Or", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, y - 8f), new Vector2(60f, 20f)), "OR", 13, TextAnchor.MiddleCenter, UiKit.Muted, UiKit.Face.Mono, 0.12f, false);
            y -= 28f;
            roomField = kit.Field(card, "Room", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(cw - 56f - 120f, 46f), "Room code (IP:port)", 200);
            kit.Button(card, "Join", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, y), new Vector2(110f, 46f), UiKit.ButtonStyle.Secondary, OnJoin, 20);
            y -= 60f;
            kit.Button(card, "Testing mode", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(cw - 56f, 46f), UiKit.ButtonStyle.Ghost, () => StartTesting(true), 20);
            y -= 56f;
            landingError = kit.Label(Node("Error", card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(cw - 56f, 22f)), "", 15, TextAnchor.MiddleLeft, UiKit.Red, UiKit.Face.Mono, 0f, false);
            landingError.fontStyle = FontStyle.Bold;
            y -= 34f;
            RectTransform links = Node("Links", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(cw - 56f, 24f));
            kit.Button(links, "How to play", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-140f, 0f), new Vector2(120f, 24f), UiKit.ButtonStyle.Link, () => OpenModal(howTo), 15);
            kit.Button(links, "Look settings", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(130f, 24f), UiKit.ButtonStyle.Link, () =>
            {
                if (debugHud != null) debugHud.lookPanel = !debugHud.lookPanel;
            }, 15);
            kit.Button(links, "Quit", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(130f, 0f), new Vector2(60f, 24f), UiKit.ButtonStyle.Link, Application.Quit, 15);

            // Grain and scanlines over the screen.
            grain = UiKit.Fill("Grain", screen).gameObject.AddComponent<RawImage>();
            grain.texture = UiKit.Grain();
            grain.raycastTarget = false;

            mainMenu = screen.gameObject;
            mainMenu.SetActive(false);
        }

        static string SavedName()
        {
            try { return PlayerPrefs.GetString(NameKey, ""); }
            catch { return ""; }
        }

        /// <summary>The original's name rule: letters, digits, spaces and _ . - ', 2-16 of them.</summary>
        public static string SanitizeName(string raw)
        {
            if (raw == null) return "";
            var sb = new System.Text.StringBuilder();
            bool space = false;
            foreach (char c in raw.Normalize(System.Text.NormalizationForm.FormKC).Trim())
            {
                if (char.IsWhiteSpace(c)) { space = true; continue; }
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-' || c == '\'')) continue;
                if (space && sb.Length > 0) sb.Append(' ');
                space = false;
                sb.Append(c);
            }
            string s = sb.ToString();
            return s.Length > 16 ? s.Substring(0, 16) : s;
        }

        /// <summary>The name in the field if it is valid (saved for next time); otherwise says what is wrong and returns null.</summary>
        string ValidName()
        {
            string n = SanitizeName(nameField != null ? nameField.text : "");
            if (n.Length < 2)
            {
                ShowLandingError("Pick a name: 2-16 letters or numbers.", false);
                if (nameField != null) nameField.ActivateInputField();
                return null;
            }
            try { PlayerPrefs.SetString(NameKey, n); } catch { }
            return n;
        }

        void ShowLandingError(string text, bool busy)
        {
            if (landingError == null) return;
            landingError.text = text;
            landingError.color = busy ? UiKit.Yellow : UiKit.Red;
        }

        void OnCreate()
        {
            if (ValidName() == null) return;
            ShowLandingError("Online play isn't ready yet: Testing mode works now.", true);
        }

        void OnJoin()
        {
            if (ValidName() == null) return;
            ShowLandingError(string.IsNullOrWhiteSpace(roomField.text) ? "Type the host's IP:port to join." : "Online play isn't ready yet: Testing mode works now.", true);
        }

        /// <summary>Shows the title screen (the level keeps running behind it).</summary>
        public void ShowMainMenu()
        {
            EnsureBuilt();
            SetMenu(false);
            CloseModal();
            map?.SetOpen(false);
            MainMenuOpen = true;
            mainMenu.SetActive(true);
            ShowLandingError("", false);
            GameSession.Reset();
            Overlays?.ClearAll();
            AudioManager.Instance.StopAll();
        }

        /// <summary>Testing mode: the original's kit, never used up, no win condition. From the title screen after a game, a fresh map.</summary>
        public void StartTesting(bool fromMenu)
        {
            EnsureBuilt();
            string name = fromMenu ? ValidName() : null;
            if (fromMenu && name == null) return;
            MainMenuOpen = false;
            mainMenu.SetActive(false);
            CloseModal();
            GameSession.TestingMode = true;
            MatchHost h = MatchHost.For(world);
            if (h != null && name != null) h.LocalName = name;
            if (fromMenu && played) { NewMap(); return; }
            played = true;
            // The match restarts under testing rules: every item and ability, never used up, T switches roles.
            h?.Restart();
            Notify("Testing mode: every item, never used up · T switches to Zach");
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

        // ---------------------------------------------------------------- the settings (Esc)

        void BuildMenu(Transform parent)
        {
            // The settings don't pause (the world goes on, as in the original); they only take the player's input.
            RectTransform shade = UiKit.Fill("Menu", parent);
            UiKit.Box(shade, new Color(4f / 255f, 4f / 255f, 3f / 255f, 0.8f), true);
            const float w = 1060f, h = 800f;
            RectTransform panel = kit.Card("Panel", shade, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            menuTitle = kit.Heading(panel, "Settings", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -26f), new Vector2(600f, 44f), 34);
            seedLabel = kit.Label(Node("Seed", panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -32f), new Vector2(400f, 30f)), "", 15, TextAnchor.MiddleRight, UiKit.Muted, UiKit.Face.Type, 0.12f, false);

            // Left: the game.
            float y = -98f;
            const float bw = 460f;
            kit.Button(panel, "Resume", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, y), new Vector2(bw, 48f), UiKit.ButtonStyle.Primary, () => SetMenu(false), 20);
            y -= 60f;
            speedLabel = kit.Button(panel, "Speed mode", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, y), new Vector2(bw, 48f), UiKit.ButtonStyle.Normal, ToggleSpeedMode, 20).GetComponentInChildren<Text>();
            y -= 66f;
            kit.FieldLabel(panel, "Pace", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, y), 120f);
            paceLabel = kit.Label(Node("Pace Value", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f + 120f, y), new Vector2(bw - 120f, 20f)), "", 15, TextAnchor.MiddleRight, UiKit.Bone, UiKit.Face.Mono, 0f, false);
            paceSlider = kit.Slider(panel, "Pace Slider", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, y - 32f), bw, Scale.MinPace, Scale.MaxPace, MatchState.Current.Pace, v => MatchState.Current.SetPace(v, true));
            y -= 70f;
            kit.Button(panel, "New map", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, y), new Vector2(bw, 48f), UiKit.ButtonStyle.Normal, NewMap, 20);
            y -= 60f;
            kit.Button(panel, "Look settings (F4)", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, y), new Vector2(bw, 48f), UiKit.ButtonStyle.Normal, () =>
            {
                if (debugHud != null) debugHud.lookPanel = !debugHud.lookPanel;
            }, 20);
            y -= 60f;
            fullScreenLabel = kit.Button(panel, "Full screen", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, y), new Vector2(bw, 48f), UiKit.ButtonStyle.Normal, ToggleFullScreen, 20).GetComponentInChildren<Text>();
            y -= 60f;
            kit.Button(panel, "How to play", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, y), new Vector2(bw, 48f), UiKit.ButtonStyle.Normal, () => OpenModal(howTo), 20);
            y -= 60f;
            kit.Button(panel, "Quit to main menu", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, y), new Vector2(bw, 48f), UiKit.ButtonStyle.Secondary, ShowMainMenu, 20);

            // Right: sound and the controls for your role.
            float x = 32f + bw + 48f, rw = w - x - 32f;
            float ry = -98f;
            AudioManager.Volumes vol = AudioManager.Load();
            void VolumeRow(string label, float value, UnityEngine.Events.UnityAction<float> set, out Slider s, ref float yy)
            {
                kit.FieldLabel(panel, label, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, yy), rw);
                s = kit.Slider(panel, label + " Slider", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, yy - 30f), rw, 0f, 1f, value, set);
                yy -= 62f;
            }
            VolumeRow("Master volume", vol.Master, v => SetVolume(0, v), out masterSlider, ref ry);
            VolumeRow("Soundcloud Burst", vol.Sfx, v => SetVolume(1, v), out sfxSlider, ref ry);
            VolumeRow("Sexton's reel", vol.Ambience, v => SetVolume(2, v), out ambienceSlider, ref ry);
            ry -= 6f;
            survivorTable = ControlsBlock(panel, "Survivor", SurvivorControls, new Vector2(x, ry), rw);
            hunterTable = ControlsBlock(panel, "Zach Branch (hunter)", HunterControls, new Vector2(x, ry), rw);
            hunterTable.SetActive(false);
            menu = shade.gameObject;
            menu.SetActive(false);
        }

        /// <summary>A heading, a table of keys for a role, then the keys that always work.</summary>
        GameObject ControlsBlock(RectTransform parent, string heading, (string, string)[] rows, Vector2 at, float width)
        {
            RectTransform block = Node(heading, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), at, new Vector2(width, 480f));
            kit.Heading(block, heading, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(width, 26f), 18);
            float h = kit.ControlsTable(block, rows, new Vector2(0f, -32f), 150f, 25f, 15);
            kit.Heading(block, "Always", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -38f - h), new Vector2(width, 26f), 18);
            kit.ControlsTable(block, GeneralControls, new Vector2(0f, -68f - h), 150f, 25f, 15);
            return block.gameObject;
        }

        void SetVolume(int which, float v)
        {
            AudioManager.Volumes cur = AudioManager.Instance.Volume;
            if (which == 0) cur.Master = v;
            else if (which == 1) cur.Sfx = v;
            else cur.Ambience = v;
            AudioManager.Instance.SetVolumes(cur);
        }

        public void SetMenu(bool open)
        {
            EnsureBuilt();
            if (MainMenuOpen) open = false;
            escOpen = open;
            menu.SetActive(open);
            if (open)
            {
                AudioManager.Volumes v = AudioManager.Instance.Volume;
                masterSlider.SetValueWithoutNotify(v.Master);
                sfxSlider.SetValueWithoutNotify(v.Sfx);
                ambienceSlider.SetValueWithoutNotify(v.Ambience);
                RefreshMenus();
            }
            if (!open && debugHud != null) debugHud.lookPanel = false;
        }

        void ToggleFullScreen()
        {
            bool full = Screen.fullScreenMode != FullScreenMode.Windowed;
            if (full) Screen.SetResolution(Mathf.RoundToInt(Display.main.systemWidth * 0.75f), Mathf.RoundToInt(Display.main.systemHeight * 0.75f), FullScreenMode.Windowed);
            else Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
        }

        /// <summary>The pace line: a share of the original's speeds, with the original's and this game's earlier walk marked.</summary>
        public static string PaceText(float pace)
        {
            string tag = Mathf.Abs(pace - 1f) < 0.02f ? " (original)" : Mathf.Abs(pace - Scale.HumanPace) < 0.02f ? " (walking pace)" : "";
            return $"{Mathf.RoundToInt(pace * 100f)}% of the original{tag}";
        }

        // ---------------------------------------------------------------- How to play

        void BuildHowToPlay(Transform parent)
        {
            RectTransform shade = UiKit.Fill("How To Play", parent);
            UiKit.Box(shade, new Color(4f / 255f, 4f / 255f, 3f / 255f, 0.8f), true);
            var closeOnShade = shade.gameObject.AddComponent<Button>();
            closeOnShade.transition = Selectable.Transition.None;
            closeOnShade.onClick.AddListener(CloseModal);
            const float w = 1040f, h = 720f;
            RectTransform panel = kit.Card("Panel", shade, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            kit.Heading(panel, "How to play", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -26f), new Vector2(600f, 44f), 34);
            kit.Label(Node("Note 1", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -80f), new Vector2(w - 64f, 24f)), "Survivors: start every generator, open the gate, escape.", 16, TextAnchor.MiddleLeft, UiKit.Muted, UiKit.Face.Mono, 0f, false);
            kit.Label(Node("Note 2", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -106f), new Vector2(w - 64f, 24f)), "Zach: hunt them down.", 16, TextAnchor.MiddleLeft, UiKit.Muted, UiKit.Face.Mono, 0f, false);
            float colW = (w - 64f - 40f) * 0.5f;
            kit.Heading(panel, "Survivor", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -148f), new Vector2(colW, 26f), 18);
            float sh = kit.ControlsTable(panel, SurvivorControls, new Vector2(32f, -180f), 140f, 27f, 15);
            kit.Heading(panel, "Zach Branch (hunter)", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f + colW + 40f, -148f), new Vector2(colW, 26f), 18);
            kit.ControlsTable(panel, HunterControls, new Vector2(32f + colW + 40f, -180f), 140f, 27f, 15);
            kit.Heading(panel, "Always", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -192f - sh), new Vector2(colW, 26f), 18);
            kit.ControlsTable(panel, GeneralControls, new Vector2(32f, -224f - sh), 140f, 27f, 15);
            kit.Button(panel, "Close", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 28f), new Vector2(150f, 46f), UiKit.ButtonStyle.Normal, CloseModal, 18);
            howTo = shade.gameObject;
            howTo.SetActive(false);
        }

        /// <summary>Opens the How to play modal (the title screen's link, or the settings' button).</summary>
        public void OpenHowToPlay() => OpenModal(howTo);

        public bool HowToPlayOpen => howTo != null && howTo.activeSelf;

        void OpenModal(GameObject modal)
        {
            if (modal == null) return;
            CloseModal();
            modal.SetActive(true);
            modal.transform.SetAsLastSibling();
            modalOpen = true;
        }

        void CloseModal()
        {
            if (howTo != null) howTo.SetActive(false);
            modalOpen = false;
        }

        // ---------------------------------------------------------------- per frame

        void UpdateMenus()
        {
            if (mainMenu == null || !mainMenu.activeSelf) return;
            float t = Time.unscaledTime;
            // The title's 6 s flicker: a dip at 94%, a stutter at 97%.
            float c = Mathf.Repeat(t, 6f) / 6f;
            float alpha = c >= 0.94f && c < 0.95f ? 0.6f : c >= 0.97f && c < 0.98f ? 0.75f : 1f;
            title.color = UiKit.A(UiKit.Bone, alpha);
            title.rectTransform.anchoredPosition = new Vector2(c >= 0.97f && c < 0.98f ? -2f : -4f, -86f);
            // Grain jumps four times every half second.
            if (t >= grainStep)
            {
                grainStep = t + 0.125f;
                Rect r = root.rect;
                grain.uvRect = new Rect(Random.value, Random.value, r.width / 192f, r.height / 192f);
            }
        }

        void RefreshMenus()
        {
            if (menu == null) return;
            menuTitle.text = GameSession.TestingMode ? "TESTING MODE" : "SETTINGS";
            if (world != null) seedLabel.text = $"SEED {world.seed}";
            speedLabel.text = (GameSession.SpeedMode ? "Speed mode: on (V)" : "Speed mode: off (V)").ToUpperInvariant();
            fullScreenLabel.text = (Screen.fullScreenMode == FullScreenMode.Windowed ? "Full screen: off" : "Full screen: on").ToUpperInvariant();
            paceLabel.text = PaceText(MatchState.Current.Pace);
            if (!Mathf.Approximately(paceSlider.value, MatchState.Current.Pace)) paceSlider.SetValueWithoutNotify(MatchState.Current.Pace);
            bool zach = host != null && host.Local != null && host.Local.Role == Role.Hunter;
            survivorTable.SetActive(!zach);
            hunterTable.SetActive(zach);
        }
    }
}
