using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Vision.Game;
using Vision.UI;
using Vision.World;

namespace Vision.Player
{
    /// <summary>
    /// The K.P.O.S. (K-pop orbital strike) tablet, Daphne's gift: left click with it in hand raises it to the face like a
    /// Modern Warfare killstreak tablet (swinging up from below, settling square), the world going on round it, and its
    /// screen powers on each time (a line of light opening into the picture, a flicker settling, the boot text). Its screen
    /// is the Tab map, red-tinted, with the same fog (only what this player has explored shows), the wheel zooming it about
    /// the pointer and a drag panning it; a marker on every playable character standing in explored ground, where they are
    /// now. Hovering one draws a lock box round it (bright corner brackets, its name); a click (pressed and let go without
    /// dragging) confirms the target: the target stays locked wherever they go, and the screen plays the confirmation (a
    /// white flash and TARGET CONFIRMED stamped across it, shaking; readings, each more ridiculous than the last, typed out
    /// with their numbers scrambling before they settle; three bars charging; a countdown; DEPLOYED), then the tablet
    /// lowers as the beam comes down. It stays in their inventory and recharges for two minutes (its slot counts it down;
    /// raising it before then only says so). Exit (top left), a click anywhere off the tablet, E, or taking another item in hand
    /// lowers it before then, keeping the item. The full map and the menus draw over it. Walking is allowed with it up,
    /// sprinting isn't, and nothing moves while it rises or lowers.
    /// </summary>
    public sealed partial class GameHud
    {
        public enum TabletState { Closed, Raising, Open, Lowering }

        /// <summary>The tablet's state (the controller reads it: no sprint while it's up, no moving while it rises or lowers).</summary>
        public static TabletState Tablet { get; private set; }
        public static bool TabletOpen => Tablet != TabletState.Closed;
        public static bool TabletMoveLocked => Tablet == TabletState.Raising || Tablet == TabletState.Lowering;

        public const float RaiseTime = 0.45f, LowerTime = 0.35f;
        /// <summary>How near the pointer (screen units) a target's marker counts as hovered; the tablet's screen size.</summary>
        public const float HoverRadius = 28f, ScreenW = 1128f, ScreenH = 740f, TabletW = 1480f, TabletH = 1017.5f;
        public const float TabletMaxZoom = 4f;

        RectTransform tabletRoot, tabletBody, tabletScreen, tabletContentRect, tabletMap, tabletMarks, tabletReticle, tabletLock, tabletExit;
        RectTransform tabletPower, tabletPowerLine, tabletRoll;
        RawImage tabletGrain;
        /// <summary>How much stronger than the title screen's the noise on the tablet's screen is.</summary>
        public const float TabletNoise = 1.3f;
        Image tabletPowerLineImg, tabletPowerGlow;
        Text tabletBootTitle, tabletBootLine;
        /// <summary>The screen's power-on: seconds since it was raised, and how long it takes to come on and settle.</summary>
        float powerT;
        public const float PowerOnTime = 1.15f;
        /// <summary>How far (pixels) the pointer can move with the button down before it's a drag (panning) and not a click.</summary>
        public const float DragThreshold = 7f;
        bool tabletPressing, tabletDragging, pressOnExit, pressOffTablet;
        // The confirmation: its panel, the stamp, the readings, the bars, the countdown, DEPLOYED.
        RectTransform tabletConfirm, tabletStamp;
        CanvasGroup tabletConfirmGroup;
        Image tabletFlash;
        Text tabletStampText, tabletReadings, tabletCount, tabletDeployed;
        readonly Image[] tabletBars = new Image[3];
        readonly Text[] tabletBarText = new Text[3];
        float cooldownNoticeAt = -9f;
        /// <summary>When the confirmation's done and the tablet goes down (s after the click): before the beam lands.</summary>
        public const float ConfirmTime = 2.55f;

        /// <summary>The confirmation's readings (label, value), the sillier the further down.</summary>
        public static readonly (string label, string value)[] StrikeReadings =
        {
            ("ORBITAL PLATFORM \"BIAS-1\"", "ONLINE"),
            ("TARGET VIBE CHECK", "FAILED"),
            ("AEGYO FLUX DENSITY", "9,001 kAe"),
            ("FANCHANT RESONANCE", "104.7%"),
            ("LIGHTSTICK ARRAY SYNC", "128/128"),
            ("HYPE BOY COEFFICIENT", "MAXIMUM"),
            ("COOKIE OVEN CORE TEMP", "4,700 K"),
            ("DITTO-LOOP CAPACITORS", "CHARGED"),
            ("BUBBLE GUM REFRACTION", "1.337"),
            ("SUPER SHY STEALTH MODE", "DISABLED"),
            ("CHOREO SYNC ERROR", "0.000 BEATS"),
            ("STREAMING PARTY QUOTA", "EXCEEDED"),
            ("BIAS WRECK PROBABILITY", "100%"),
            ("WIND MACHINE (HAIR)", "CINEMATIC"),
            ("COMEBACK STAGE PYRO", "ARMED"),
            ("PHOTOCARD TRADE VALUE", "PRICELESS"),
            ("COLLATERAL CUTENESS", "ACCEPTABLE"),
            ("TRAINEE MORALE", "IRRELEVANT"),
            ("LEGAL DEPARTMENT", "NOT NOTIFIED"),
            ("SATELLITE MOOD", "SPARKLY"),
        };
        Vector2 pressAt, lastPointer;
        RawImage tabletArt, tabletFog;
        Text tabletTitle, tabletTimer, tabletStatus, tabletDispatch, tabletLockName;
        Image tabletDispatchBg;
        CanvasGroup tabletContent;
        readonly List<(RectTransform node, Image dot, Text name)> tabletMarkers = new List<(RectTransform, Image, Text)>();
        float tabletT, tabletZoom = 1f, dispatchedAt = -1f, inboundAt = -1f;
        Vector2 tabletPan;
        bool tabletCentre;
        /// <summary>How far in it opens (centred on you), and how big it's held (clear of the inventory bar under it).</summary>
        public const float TabletOpenZoom = 2.2f, TabletScale = 0.88f, TabletLift = 58f;
        int hoveredTarget, lockedTarget;

        /// <summary>The target under the pointer and the one locked (0: none), and the tablet's zoom (tests).</summary>
        public int TabletHovered => hoveredTarget;
        public int TabletLocked => lockedTarget;
        public float TabletZoom => tabletZoom;

        /// <summary>
        /// Who the tablet can lock on: every playable character (Zach or a survivor, teammates too, never a ghost, and
        /// never yourself outside testing) standing in ground this player has explored.
        /// </summary>
        public static List<SimPlayer> StrikeTargets(MatchSim sim, FogOfWar fog, SimPlayer me)
        {
            var list = new List<SimPlayer>();
            if (sim == null || me == null) return list;
            foreach (SimPlayer q in sim.Order)
                if ((q.Id != me.Id || sim.TestMode) && MatchSim.Strikeable(q) && (fog == null || fog.Seen(q.Pos))) list.Add(q);
            return list;
        }

        static Texture2D strikeRingTex, gridTex, scanTex;

        static Texture2D StrikeRing()
        {
            if (strikeRingTex != null) return strikeRingTex;
            const int n = 128;
            strikeRingTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Strike Ring", hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2(x - n * 0.5f + 0.5f, y - n * 0.5f + 0.5f).magnitude;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 54f) / 2.2f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            strikeRingTex.SetPixels32(px);
            strikeRingTex.Apply();
            return strikeRingTex;
        }

        static Texture2D Lines(bool grid)
        {
            Texture2D t = grid ? gridTex : scanTex;
            if (t != null) return t;
            const int n = 64;
            t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = grid ? "Strike Grid" : "Strike Scan", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    byte a = grid ? (byte)(x == 0 || y == 0 ? 120 : x % 16 == 0 || y % 16 == 0 ? 40 : 0) : (byte)(y % 4 < 2 ? 70 : 0);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            t.SetPixels32(px);
            t.Apply();
            if (grid) gridTex = t; else scanTex = t;
            return t;
        }

        void BuildTablet()
        {
            if (tabletRoot != null) return;
            tabletRoot = UiKit.Fill("Orbital Strike Tablet", root);
            tabletRoot.SetAsLastSibling();
            tabletBody = UiKit.Node("Tablet", tabletRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TabletW, TabletH));
            // The screen (its contents clipped to it) under the frame: an opaque black glass backing, the picture on it
            // fading and flickering in as it powers on.
            tabletScreen = UiKit.Node("Screen", tabletBody, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ScreenW, ScreenH));
            UiKit.Box(tabletScreen, new Color(0.012f, 0.008f, 0.008f, 1f));
            // (A stencil mask on the screen's own backing: the zoomed map is clipped to the screen.)
            tabletScreen.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            RectTransform content = tabletContentRect = UiKit.Fill("Content", tabletScreen);
            tabletContent = content.gameObject.AddComponent<CanvasGroup>();
            tabletMap = UiKit.Node("Map", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ScreenH, ScreenH));
            tabletArt = UiKit.Fill("Art", tabletMap).gameObject.AddComponent<RawImage>();
            tabletArt.color = new Color(0.8f, 0.9f, 1f, 1f);
            tabletFog = UiKit.Fill("Fog", tabletMap).gameObject.AddComponent<RawImage>();
            // The tint, a grid, scanlines.
            UiKit.Box(UiKit.Fill("Tint", content), new Color(0.3f, 0.55f, 1f, 0.1f));
            var grid = UiKit.Fill("Grid", content).gameObject.AddComponent<RawImage>();
            grid.texture = Lines(true);
            grid.uvRect = new Rect(0f, 0f, ScreenW / 64f / 2f, ScreenH / 64f / 2f);
            grid.color = new Color(0.5f, 0.75f, 1f, 0.42f);
            var scan = UiKit.Fill("Scan", content).gameObject.AddComponent<RawImage>();
            scan.texture = Lines(false);
            scan.uvRect = new Rect(0f, 0f, 1f, ScreenH / 64f);
            scan.color = new Color(0f, 0f, 0f, 0.35f);
            tabletMarks = UiKit.Fill("Marks", content);
            // The lock box: corner brackets and a name plate.
            tabletLock = UiKit.Node("Lock", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f));
            foreach (Vector2 c in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
            {
                UiKit.Box(UiKit.Node("Bracket", tabletLock, c, c, Vector2.zero, new Vector2(20f, 4f)), new Color(0.92f, 0.96f, 1f, 1f));
                UiKit.Box(UiKit.Node("Bracket", tabletLock, c, c, Vector2.zero, new Vector2(4f, 20f)), new Color(0.92f, 0.96f, 1f, 1f));
            }
            tabletLockName = kit.Label(UiKit.Node("Name", tabletLock, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(320f, 26f)), "", 18, TextAnchor.UpperCenter, new Color(0.92f, 0.96f, 1f, 1f));
            tabletLockName.horizontalOverflow = HorizontalWrapMode.Overflow;
            // The reticle: a ring and a crosshair.
            tabletReticle = UiKit.Node("Reticle", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 96f));
            var ring = UiKit.Fill("Ring", tabletReticle).gameObject.AddComponent<RawImage>();
            ring.texture = StrikeRing();
            ring.color = new Color(0.55f, 0.8f, 1f, 0.9f);
            foreach (var (pos, size) in new[] { (new Vector2(-38f, 0f), new Vector2(26f, 3f)), (new Vector2(38f, 0f), new Vector2(26f, 3f)), (new Vector2(0f, -38f), new Vector2(3f, 26f)), (new Vector2(0f, 38f), new Vector2(3f, 26f)) })
                UiKit.Box(UiKit.Node("Cross", tabletReticle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size), new Color(0.55f, 0.8f, 1f, 0.9f));
            // Along the top: Exit (top left), the title, the timer; along the bottom: the status and DISPATCH.
            RectTransform exit = tabletExit = UiKit.Node("Exit", content, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(120f, 40f));
            UiKit.Box(exit, new Color(0.04f, 0.1f, 0.22f, 0.9f));
            var exitText = kit.Label(UiKit.Fill("Label", exit), "◄ EXIT", 20, TextAnchor.MiddleCenter, new Color(0.85f, 0.93f, 1f, 1f));
            exitText.raycastTarget = false;
            tabletTitle = kit.Label(UiKit.Node("Title", content, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, -18f), new Vector2(620f, 32f)),
                "K-POP ORBITAL STRIKE  //  K.P.O.S.", 22, TextAnchor.MiddleLeft, new Color(0.82f, 0.9f, 1f, 1f));
            tabletTimer = kit.Label(UiKit.Node("Timer", content, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -14f), new Vector2(320f, 40f)),
                "STANDBY", 30, TextAnchor.MiddleRight, new Color(0.85f, 0.93f, 1f, 1f));
            tabletStatus = kit.Label(UiKit.Node("Status", content, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 16f), new Vector2(620f, 140f)),
                "", 17, TextAnchor.LowerLeft, new Color(0.68f, 0.82f, 1f, 1f));
            RectTransform dispatch = UiKit.Node("Dispatch", content, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 16f), new Vector2(240f, 56f));
            tabletDispatchBg = UiKit.Box(dispatch, new Color(0.05f, 0.12f, 0.3f, 0.85f));
            tabletDispatch = kit.Label(UiKit.Fill("Label", dispatch), "DISPATCH", 26, TextAnchor.MiddleCenter, new Color(0.6f, 0.8f, 1f, 1f));
            // The title screen's noise over the picture, 30% stronger: grain moving on every frame, still scanlines, a band
            // of light rolling down.
            tabletGrain = UiKit.Fill("Grain", tabletScreen).gameObject.AddComponent<RawImage>();
            tabletGrain.texture = UiKit.SoftGrain(TabletNoise);
            var tabletLines = UiKit.Fill("Scanlines", tabletScreen).gameObject.AddComponent<RawImage>();
            tabletLines.texture = UiKit.Scanlines(TabletNoise);
            tabletLines.uvRect = new Rect(0f, 0f, 1f, ScreenH / 3f);
            tabletRoll = UiKit.Node("Roll", tabletScreen, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 200f));
            tabletRoll.gameObject.AddComponent<RawImage>().texture = UiKit.RollBand(TabletNoise);
            // The confirmation over the picture (hidden till a target's confirmed).
            tabletConfirm = UiKit.Fill("Confirm", tabletScreen);
            tabletConfirmGroup = tabletConfirm.gameObject.AddComponent<CanvasGroup>();
            UiKit.Box(UiKit.Fill("Shade", tabletConfirm), new Color(0.02f, 0.04f, 0.12f, 0.8f));
            tabletFlash = UiKit.Box(UiKit.Fill("Flash", tabletConfirm), new Color(1f, 0.92f, 0.96f, 0f));
            tabletReadings = kit.Label(UiKit.Node("Readings", tabletConfirm, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -182f), new Vector2(640f, 440f)),
                "", 17, TextAnchor.UpperLeft, new Color(1f, 0.78f, 0.84f, 1f), UiKit.Face.Mono, 0f, false);
            tabletReadings.verticalOverflow = VerticalWrapMode.Overflow;
            string[] bars = { "CAPACITOR CHARGE", "FANDOM SYNC", "BIAS LOCK" };
            for (int i = 0; i < 3; i++)
            {
                RectTransform bar = UiKit.Node("Bar", tabletConfirm, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -212f - i * 74f), new Vector2(380f, 18f));
                UiKit.Box(bar, new Color(0.3f, 0.04f, 0.1f, 0.9f));
                RectTransform fill = UiKit.Fill("Fill", bar);
                fill.anchorMax = new Vector2(0f, 1f);
                tabletBars[i] = UiKit.Box(fill, new Color(1f, 0.4f, 0.72f, 1f));
                tabletBarText[i] = kit.Label(UiKit.Node("Label", bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 34f), new Vector2(0f, 26f)),
                    bars[i], 16, TextAnchor.LowerLeft, new Color(1f, 0.82f, 0.88f, 1f), UiKit.Face.Mono, 0f, false);
            }
            tabletCount = kit.Label(UiKit.Node("Count", tabletConfirm, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-26f, 96f), new Vector2(380f, 110f)),
                "", 64, TextAnchor.LowerRight, new Color(1f, 0.9f, 0.95f, 1f), UiKit.Face.DisplayBold, 0.05f);
            tabletDeployed = kit.Label(UiKit.Node("Deployed", tabletConfirm, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1000f, 50f)),
                "", 30, TextAnchor.MiddleCenter, Color.white, UiKit.Face.DisplayBold, 0.12f);
            tabletStamp = UiKit.Node("Stamp", tabletConfirm, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(900f, 84f));
            UiKit.Frame(tabletStamp, new Color(1f, 0.85f, 0.92f, 1f));
            tabletStampText = kit.Label(UiKit.Fill("Text", tabletStamp), "TARGET CONFIRMED", 52, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.95f, 1f), UiKit.Face.DisplayBold, 0.14f);
            tabletConfirm.gameObject.SetActive(false);
            // The power-on over the picture: a glow, the line of light opening into it, the boot text.
            tabletPower = UiKit.Fill("Power", tabletScreen);
            tabletPowerGlow = UiKit.Box(UiKit.Fill("Glow", tabletPower), new Color(0.55f, 0.75f, 1f, 0f));
            tabletPowerLine = UiKit.Node("Line", tabletPower, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ScreenW, 4f));
            tabletPowerLineImg = UiKit.Box(tabletPowerLine, new Color(1f, 0.93f, 0.95f, 1f));
            tabletBootTitle = kit.Label(UiKit.Node("Boot", tabletPower, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(900f, 60f)),
                "K-POP ORBITAL STRIKE", 46, TextAnchor.MiddleCenter, new Color(0.86f, 0.92f, 1f, 1f));
            tabletBootLine = kit.Label(UiKit.Node("Boot Line", tabletPower, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -26f), new Vector2(900f, 30f)),
                "K.P.O.S.  //  SATELLITE UPLINK  //  INITIALISING", 18, TextAnchor.MiddleCenter, new Color(0.6f, 0.75f, 1f, 1f));
            foreach (Graphic g in tabletScreen.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            // The tablet itself over all of it (its screen window clear).
            var frame = UiKit.Fill("Frame", tabletBody).gameObject.AddComponent<RawImage>();
            frame.texture = Resources.Load<Texture2D>("Overlays/strike_tablet");
            frame.raycastTarget = false;
            if (frame.texture == null) frame.color = new Color(0f, 0f, 0f, 0f);
            tabletRoot.gameObject.SetActive(false);
        }

        /// <summary>Raises the tablet (left click with the orbital strike in hand).</summary>
        public void RaiseTablet()
        {
            EnsureBuilt();
            BuildTablet();
            if (TabletOpen) return;
            Tablet = TabletState.Raising;
            tabletT = 0f;
            hoveredTarget = lockedTarget = 0;
            dispatchedAt = inboundAt = -1f;
            tabletZoom = TabletOpenZoom;
            tabletPan = Vector2.zero;
            tabletCentre = true;
            powerT = 0f;
            tabletPressing = tabletDragging = false;
            tabletConfirm.gameObject.SetActive(false);
            tabletRoot.gameObject.SetActive(true);
        }

        /// <summary>The tablet straight up, its raise skipped (tests and captures).</summary>
        public void TabletUpNow()
        {
            RaiseTablet();
            Tablet = TabletState.Open;
            tabletT = RaiseTime;
            powerT = PowerOnTime;
        }

        /// <summary>Lowers the tablet (Exit, E, or once the strike's come down).</summary>
        public void LowerTablet()
        {
            if (Tablet != TabletState.Raising && Tablet != TabletState.Open) return;
            Tablet = TabletState.Lowering;
            tabletT = 0f;
        }

        /// <summary>Confirms the strike on a target (a click on its marker): true if it's been called.</summary>
        public bool ConfirmStrike(int target)
        {
            if (Tablet != TabletState.Open || lockedTarget != 0 || host == null) return false;
            if (!host.CallStrike(target)) return false;
            lockedTarget = target;
            dispatchedAt = Time.unscaledTime;
            return true;
        }

        /// <summary>A cooldown as m:ss.</summary>
        public static string CooldownText(float seconds)
        {
            int t = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{t / 60}:{t % 60:00}";
        }

        /// <summary>
        /// The confirmation, <paramref name="since"/> s after the click: a white flash and the stamp slamming down, the screen
        /// shaking; the readings typed out one after another (numbers scrambling a moment before they settle, the newest
        /// lines scrolling up); three bars charging; the countdown to impact; DEPLOYED, flashing; then the tablet lowers.
        /// </summary>
        void AnimateConfirm(float since, float left, string targetName)
        {
            if (!tabletConfirm.gameObject.activeSelf) tabletConfirm.gameObject.SetActive(true);
            tabletConfirmGroup.alpha = Mathf.Clamp01(since / 0.08f);
            tabletFlash.color = new Color(1f, 0.92f, 0.96f, 0.85f * Mathf.Clamp01(1f - since / 0.3f));
            // The stamp: slammed down big, settling, jittering now and then.
            float slam = Mathf.Clamp01(since / 0.18f);
            tabletStamp.localScale = Vector3.one * Mathf.Lerp(1.7f, 1f, 1f - (1f - slam) * (1f - slam));
            bool glitch = Mathf.Repeat(since * 7.3f, 1f) < 0.08f;
            tabletStamp.anchoredPosition = new Vector2(glitch ? Random.Range(-14f, 14f) : 0f, -78f);
            tabletStampText.text = since < 1.2f ? "TARGET CONFIRMED" : $"LOCKED: {targetName}";
            // The screen shakes as it slams down, a tremor after.
            float shake = since < 0.35f ? 9f * (1f - since / 0.35f) : 1.2f;
            tabletBody.anchoredPosition += new Vector2(Random.Range(-shake, shake), Random.Range(-shake, shake));
            // The readings, a line every 0.11 s, the last twelve showing.
            int shown = Mathf.Clamp(Mathf.FloorToInt((since - 0.15f) / 0.11f) + 1, 0, StrikeReadings.Length);
            var sb = new System.Text.StringBuilder();
            for (int i = Mathf.Max(0, shown - 12); i < shown; i++)
            {
                var (label, value) = StrikeReadings[i];
                float age = since - 0.15f - i * 0.11f;
                string v = age < 0.22f ? Scramble(value) : value;
                string dots = new string('.', Mathf.Max(2, 27 - label.Length));
                sb.Append(i == shown - 1 && age < 0.22f ? "<color=#ffffff>" : "<color=#ffc6d8>").Append("> ").Append(label).Append(' ').Append(dots).Append(' ').Append(v).Append("</color>\n");
            }
            tabletReadings.text = sb.ToString();
            for (int i = 0; i < 3; i++)
            {
                float k = Smooth((since - 0.3f - i * 0.25f) / (1.4f - i * 0.15f));
                // (Overshooting a little past 100%, as all the best readings do.)
                float pct = k * (i == 0 ? 112f : i == 1 ? 104.7f : 100f);
                tabletBars[i].rectTransform.anchorMax = new Vector2(Mathf.Clamp01(k), 1f);
                tabletBars[i].color = k >= 1f && Mathf.Repeat(since * 6f, 1f) < 0.5f ? Color.white : new Color(1f, 0.4f, 0.72f, 1f);
                string name = i == 0 ? "CAPACITOR CHARGE" : i == 1 ? "FANDOM SYNC" : "BIAS LOCK";
                tabletBarText[i].text = $"{name}  {pct:0.0}%";
            }
            tabletCount.text = since > 0.9f ? $"T-{Mathf.Max(0f, left):0.00}" : "";
            bool deployed = since > ConfirmTime - 0.45f;
            tabletDeployed.text = deployed ? (Mathf.Repeat(since * 8f, 1f) < 0.6f ? "DEPLOYED  //  ENJOY THE SHOW" : "") : since > 1.6f ? "DEPLOYING..." : "";
        }

        static string Scramble(string value)
        {
            var c = value.ToCharArray();
            for (int i = 0; i < c.Length; i++)
                if (char.IsDigit(c[i]) || char.IsLetter(c[i])) c[i] = (char)(char.IsDigit(c[i]) ? '0' + Random.Range(0, 10) : 'A' + Random.Range(0, 26));
            return new string(c);
        }

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        /// <summary>
        /// The screen powering on as it comes up (each time it's raised): black glass, then a line of light drawn across
        /// its middle, opening top and bottom into the picture as a glow fades off it, the picture flickering a little and
        /// settling, the boot text (K-POP ORBITAL STRIKE) showing over it and fading. Lowered, the picture folds back into
        /// the line and goes out.
        /// </summary>
        void AnimatePower()
        {
            float t = powerT;
            float across = Smooth((t - 0.12f) / 0.14f), open = Smooth((t - 0.26f) / 0.2f);
            if (Tablet == TabletState.Lowering)
            {
                float k = Smooth(tabletT / (LowerTime * 0.6f));
                across = 1f - Smooth((k - 0.55f) / 0.45f);
                open = Mathf.Min(open, 1f - k);
            }
            // The picture: squeezed into the line, opening out, a flicker settling as it warms up.
            float settle = Mathf.Clamp01(1f - (t - 0.46f) / (PowerOnTime - 0.46f));
            float flicker = Tablet == TabletState.Lowering ? 0f : settle * (0.28f * Mathf.PerlinNoise(t * 16f, 0.37f) + 0.12f * Mathf.Sin(t * 47f) * Mathf.Sin(t * 11f));
            tabletContent.alpha = Mathf.Clamp01(open * (1f - Mathf.Max(0f, flicker)));
            tabletContentRect.localScale = new Vector3(Mathf.Lerp(0.96f, 1f, open), Mathf.Max(0.004f, open), 1f);
            bool on = t < PowerOnTime || Tablet == TabletState.Lowering;
            tabletPower.gameObject.SetActive(on);
            if (!on) return;
            // The line of light: drawn across, then opening (and fading) into the picture.
            tabletPowerLine.localScale = new Vector3(Mathf.Max(0.001f, across), 1f, 1f);
            tabletPowerLine.sizeDelta = new Vector2(ScreenW, Mathf.Lerp(4f, ScreenH, open));
            tabletPowerLineImg.color = new Color(1f, 0.93f, 0.95f, across * (1f - open) * (1f - open));
            tabletPowerGlow.color = new Color(0.55f, 0.75f, 1f, 0.35f * open * Smooth(1f - (t - 0.46f) / 0.4f));
            // The boot text over the warming picture, fading as it settles.
            float boot = Tablet == TabletState.Lowering ? 0f : open * Smooth((PowerOnTime - 0.05f - t) / 0.3f);
            tabletBootTitle.color = new Color(0.86f, 0.92f, 1f, boot);
            tabletBootLine.color = new Color(0.6f, 0.75f, 1f, boot * (0.75f + 0.25f * Mathf.Sin(t * 30f)));
        }

        static float EaseOutBack(float t) { t = Mathf.Clamp01(t) - 1f; return 1f + t * t * (2.2f * t + 1.2f); }

        /// <summary>Each frame: raising or lowering it, and while it's up, the map, the markers, the lock and the clicks.</summary>
        void UpdateTablet(SimPlayer me)
        {
            float dt = Time.unscaledDeltaTime;
            Mouse mouse = Mouse.current;
            Keyboard kb = Keyboard.current;
            if (!TabletOpen)
            {
                // Left click with the strike in hand raises it (not over the HUD, a menu, the map or the editor).
                Inventory.Slot held = me != null && player != null ? me.Inv.SlotAt(player.SelectedSlot) : null;
                bool overUi = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
                if (held != null && !held.Empty && held.item == ItemType.OrbitalStrike && me.CanAct && me.SilencedT <= 0f && !PointerWanted && !overUi
                    && mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    if (me.StrikeCd > 0f)
                    {
                        if (Time.unscaledTime - cooldownNoticeAt > 1.5f)
                        {
                            cooldownNoticeAt = Time.unscaledTime;
                            Notify($"K.P.O.S. recharging: {CooldownText(me.StrikeCd)}");
                        }
                    }
                    else RaiseTablet();
                }
                return;
            }
            // The full map (Tab) and the menus (Esc) draw over it: hidden while they're open, its clicks set aside.
            bool covered = FullMapOpen || MenuOpen;
            if (tabletRoot.gameObject.activeSelf == covered) tabletRoot.gameObject.SetActive(!covered);
            if (covered) { tabletPressing = tabletDragging = false; return; }
            tabletT += dt;
            if (Tablet != TabletState.Lowering) powerT += dt;
            float e;
            if (Tablet == TabletState.Raising)
            {
                e = EaseOutBack(tabletT / RaiseTime);
                if (tabletT >= RaiseTime) Tablet = TabletState.Open;
            }
            else if (Tablet == TabletState.Lowering)
            {
                float k = Mathf.Clamp01(tabletT / LowerTime);
                e = 1f - k * k * (3f - 2f * k);
                if (tabletT >= LowerTime)
                {
                    Tablet = TabletState.Closed;
                    tabletPressing = tabletDragging = false;
                    tabletRoot.gameObject.SetActive(false);
                    return;
                }
            }
            else e = 1f;
            // Swinging up from below and to the right, settling square to the face.
            tabletBody.anchoredPosition = new Vector2(Mathf.Lerp(180f, 0f, e), Mathf.Lerp(-1150f, TabletLift, e));
            tabletBody.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-11f, 0f, e));
            tabletBody.localScale = Vector3.one * TabletScale * Mathf.Lerp(0.86f, 1f, e);
            AnimatePower();
            // The noise flickering over the screen.
            tabletGrain.uvRect = new Rect(Random.value, Random.value, ScreenW / 384f, ScreenH / 384f);
            tabletRoll.anchoredPosition = new Vector2(0f, Mathf.Lerp(200f, -ScreenH, Mathf.Repeat(Time.unscaledTime / 6f, 1f)));
            if (me == null || map == null || map.Fog == null) return;
            // Down, the tablet gone from their hands before it was used, or another item taken in hand: lowered.
            Inventory.Slot inHand = player != null ? me.Inv.SlotAt(player.SelectedSlot) : null;
            bool holding = inHand != null && !inHand.Empty && inHand.item == ItemType.OrbitalStrike;
            if (Tablet == TabletState.Open && (!me.CanAct || (lockedTarget == 0 && !holding))) LowerTablet();
            if (Tablet == TabletState.Open && kb != null && kb.eKey.wasPressedThisFrame) LowerTablet();

            // The map, as the Tab map: north turned with the view, the wheel zooming it about the pointer, a drag panning it.
            tabletArt.texture = map.ArtTexture;
            tabletFog.texture = map.Fog.Texture;
            Vector2 mp = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(tabletScreen, mp, null, out Vector2 pointer);
            bool inside = Mathf.Abs(pointer.x) < ScreenW * 0.5f && Mathf.Abs(pointer.y) < ScreenH * 0.5f;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(tabletBody, mp, null, out Vector2 onBody);
            // (The tablet's own outline, inside the drop shadow round its painted edge.)
            bool onTablet = Mathf.Abs(onBody.x) < TabletW * 0.5f - 24f && Mathf.Abs(onBody.y) < TabletH * 0.5f - 24f;
            bool onExit = RectTransformUtility.RectangleContainsScreenPoint(tabletExit, mp, null);
            bool open = Tablet == TabletState.Open && mouse != null;
            if (open && mouse.leftButton.wasPressedThisFrame)
            {
                tabletPressing = true;
                tabletDragging = false;
                pressAt = mp;
                lastPointer = pointer;
                pressOnExit = onExit;
                pressOffTablet = !onTablet;
            }
            if (open && tabletPressing && mouse.leftButton.isPressed)
            {
                // Held and moved on the screen: a drag, panning the map, never a click on a target.
                if (!tabletDragging && !pressOnExit && !pressOffTablet && (mp - pressAt).magnitude > DragThreshold * Mathf.Max(1f, Screen.height / 1080f)) tabletDragging = true;
                if (tabletDragging) tabletPan += pointer - lastPointer;
                lastPointer = pointer;
            }
            float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            if (inside && Mathf.Abs(wheel) > 0.01f && Tablet == TabletState.Open)
            {
                float z = Mathf.Clamp(tabletZoom * Mathf.Pow(1.18f, Mathf.Sign(wheel)), 1f, TabletMaxZoom);
                tabletPan = pointer - (pointer - tabletPan) * (z / tabletZoom);
                tabletZoom = z;
            }
            Quaternion turn = Quaternion.Euler(0f, 0f, MapHud.ViewYaw);
            if (tabletCentre)
            {
                // Opening: zoomed in on where you stand.
                tabletCentre = false;
                tabletPan = -(Vector2)(turn * (Vector3)(me.Pos * (ScreenH * tabletZoom / (2f * map.Extent))));
            }
            float px = ScreenH * tabletZoom, lim = px * 0.5f;
            tabletPan = new Vector2(Mathf.Clamp(tabletPan.x, -lim, lim), Mathf.Clamp(tabletPan.y, -lim, lim));
            if (tabletZoom <= 1.001f) tabletPan = Vector2.zero;
            tabletMap.sizeDelta = new Vector2(px, px);
            tabletMap.anchoredPosition = tabletPan;
            tabletMap.localRotation = turn;
            float scale = px / (2f * map.Extent);
            Vector2 OnScreen(Vector2 at) => (Vector2)(turn * (Vector3)(at * scale)) + tabletPan;

            // The markers: everyone it can lock on (and the locked target wherever they go), you as a white arrow.
            MatchSim sim = host != null ? host.Sim : null;
            List<SimPlayer> targets = StrikeTargets(sim, map.Fog, me);
            SimPlayer locked = lockedTarget != 0 && sim != null ? sim.Get(lockedTarget) : null;
            if (locked != null && !targets.Contains(locked)) targets.Add(locked);
            while (tabletMarkers.Count < targets.Count + 1)
            {
                RectTransform node = UiKit.Node("Marker", tabletMarks, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
                Image dot = UiKit.Box(UiKit.Fill("Dot", node), Color.white);
                dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                Text name = kit.Label(UiKit.Node("Name", node, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(240f, 20f)), "", 14, TextAnchor.UpperCenter, Color.white);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                dot.raycastTarget = name.raycastTarget = false;
                tabletMarkers.Add((node, dot, name));
            }
            hoveredTarget = 0;
            float best = HoverRadius;
            for (int i = 0; i < tabletMarkers.Count; i++)
            {
                var (node, dot, name) = tabletMarkers[i];
                // (In testing you're a target yourself: your own marker then, not a second one.)
                bool used = i < targets.Count || (i == targets.Count && !targets.Contains(me));
                node.gameObject.SetActive(used);
                if (!used) continue;
                if (i == targets.Count)
                {
                    // You.
                    node.anchoredPosition = OnScreen(me.Pos);
                    dot.color = new Color(1f, 1f, 1f, 0.9f);
                    node.sizeDelta = new Vector2(12f, 12f);
                    name.text = "YOU";
                    continue;
                }
                SimPlayer q = targets[i];
                Vector2 at = OnScreen(q.Pos);
                node.anchoredPosition = at;
                node.sizeDelta = new Vector2(16f, 16f);
                bool zach = q.Role == Role.Hunter;
                dot.color = q.Id == me.Id ? Color.white : zach ? new Color(1f, 0.15f, 0.12f, 1f) : new Color(0.85f, 0.93f, 1f, 1f);
                name.text = q.Id == me.Id ? "YOU" : zach ? "ZACH" : q.Name.ToUpperInvariant();
                float d = (at - pointer).magnitude;
                if (inside && !tabletDragging && lockedTarget == 0 && d < best) { best = d; hoveredTarget = q.Id; }
            }

            // The lock box: round what's hovered, then round the locked target, tightening as it locks.
            int boxed = lockedTarget != 0 ? lockedTarget : hoveredTarget;
            SimPlayer bq = boxed != 0 && sim != null ? sim.Get(boxed) : null;
            tabletLock.gameObject.SetActive(bq != null);
            if (bq != null)
            {
                tabletLock.anchoredPosition = OnScreen(bq.Pos);
                float pulse = lockedTarget != 0 ? 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 14f) : 1.15f;
                tabletLock.sizeDelta = Vector2.one * 64f * pulse;
                tabletLockName.text = (lockedTarget != 0 ? "LOCKED  " : "TARGET  ") + (bq.Id == me.Id ? "YOU" : bq.Role == Role.Hunter ? "ZACH" : bq.Name.ToUpperInvariant());
            }
            tabletReticle.gameObject.SetActive(inside && lockedTarget == 0 && !tabletDragging);
            tabletReticle.anchoredPosition = pointer;
            tabletReticle.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 30f);

            // Let go without having dragged: a click. On Exit, or off the tablet altogether, it's lowered; on a target, it's
            // confirmed.
            if (open && tabletPressing && mouse.leftButton.wasReleasedThisFrame)
            {
                bool drag = tabletDragging;
                tabletPressing = tabletDragging = false;
                if (!drag)
                {
                    if (pressOnExit && onExit) LowerTablet();
                    else if (pressOffTablet && !onTablet) LowerTablet();
                    else if (!pressOnExit && !pressOffTablet && inside && lockedTarget == 0 && hoveredTarget != 0) ConfirmStrike(hoveredTarget);
                }
            }

            // The timer, the start-up sequence and DISPATCH.
            OrbitalStrike strike = null;
            if (sim != null && lockedTarget != 0)
                foreach (OrbitalStrike s in sim.Strikes) if (s.Owner == me.Id && s.Target == lockedTarget) strike = s;
            if (lockedTarget == 0)
            {
                tabletTimer.text = targets.Count > 0 ? "STANDBY" : "NO TARGETS";
                tabletStatus.text = targets.Count > 0 ? "SELECT A TARGET IN EXPLORED GROUND\n<color=#8ab4ff>WHEEL: ZOOM   DRAG: PAN   E: EXIT</color>" : "NO TARGETS IN EXPLORED GROUND";
                tabletDispatch.text = "DISPATCH";
                tabletDispatchBg.color = hoveredTarget != 0 ? new Color(0.15f, 0.35f, 0.85f, 0.95f) : new Color(0.05f, 0.12f, 0.3f, 0.85f);
                return;
            }
            float since = Time.unscaledTime - dispatchedAt;
            float left = strike != null && !strike.Firing ? strike.Countdown : Mathf.Max(0f, Balance.Items.Orbital.Countdown - since);
            bool firing = strike != null && strike.Firing;
            AnimateConfirm(since, left, locked != null ? (locked.Id == me.Id ? "YOU" : locked.Role == Role.Hunter ? "ZACH" : locked.Name.ToUpperInvariant()) : "TARGET");
            // Done: put down, as the beam comes down.
            if (since >= ConfirmTime) LowerTablet();
            tabletTimer.text = firing ? "STRIKE INBOUND" : $"00:{left:00.0}";
            string name2 = locked != null ? (locked.Id == me.Id ? "YOU" : locked.Role == Role.Hunter ? "ZACH" : locked.Name.ToUpperInvariant()) : "TARGET";
            var lines = new System.Text.StringBuilder();
            lines.Append($"TARGET LOCKED: {name2}");
            if (since > 0.4f) lines.Append("\nUPLINK ESTABLISHED // SAT-7");
            if (since > 0.9f) lines.Append("\nCALIBRATING EMITTER ARRAY");
            if (since > 1.4f) lines.Append($"\nCHARGING  {Mathf.Clamp01((since - 1.4f) / 1.2f) * 100f:0}%");
            if (since > 2.5f) lines.Append("\nDISPATCH AUTHORISED");
            // (The confirmation over the screen says it all now: the old start-up lines give way to it.)
            tabletStatus.text = "";
            tabletDispatch.text = firing ? "DISPATCHED" : "DISPATCHING";
            tabletDispatchBg.color = new Color(0.2f, 0.45f, 1f, 0.6f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8f)));
            // Down it comes: the tablet lowers after a moment.
            if (firing && inboundAt < 0f) inboundAt = Time.unscaledTime;
            if (inboundAt >= 0f && Time.unscaledTime - inboundAt > 1.1f) LowerTablet();
            if (strike == null && since > Balance.Items.Orbital.Countdown + 1.5f) LowerTablet();
        }
    }
}
