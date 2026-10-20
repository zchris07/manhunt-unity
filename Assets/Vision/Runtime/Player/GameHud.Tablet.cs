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
    /// The orbital strike's tablet (Daphne's gift): left click with it in hand raises it to the face like a Modern
    /// Warfare killstreak tablet (swinging up from below, settling square), the world going on round it. Its screen is the
    /// Tab map, red-tinted, with the same fog (only what this player has explored shows) and the wheel zooming it about the
    /// pointer; a marker on every playable character standing in explored ground, where they are now. Hovering one draws
    /// a lock box round it (bright corner brackets, its name); clicking confirms the target: the item's used up, and a
    /// countdown and start-up sequence run on the screen (target locked, uplink, calibrating, charging, dispatch) while the
    /// target stays locked wherever they go; the strike comes down and the tablet lowers. The Exit button (top left) or E
    /// lowers it before then, keeping the item. Walking is allowed with it up, sprinting isn't, and nothing moves while it
    /// rises or lowers.
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

        RectTransform tabletRoot, tabletBody, tabletScreen, tabletMap, tabletMarks, tabletReticle, tabletLock;
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
        /// Who the tablet can lock on: every playable character (Zach or a survivor, teammates too, never yourself or a
        /// ghost) standing in ground this player has explored.
        /// </summary>
        public static List<SimPlayer> StrikeTargets(MatchSim sim, FogOfWar fog, SimPlayer me)
        {
            var list = new List<SimPlayer>();
            if (sim == null || me == null) return list;
            foreach (SimPlayer q in sim.Order)
                if (q.Id != me.Id && MatchSim.Strikeable(q) && (fog == null || fog.Seen(q.Pos))) list.Add(q);
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
            // The screen (its contents clipped to it) under the frame.
            tabletScreen = UiKit.Node("Screen", tabletBody, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ScreenW, ScreenH));
            UiKit.Box(tabletScreen, new Color(0.05f, 0.01f, 0.01f, 1f));
            // (A stencil mask on the screen's own backing: the zoomed map is clipped to the screen.)
            tabletScreen.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            tabletContent = tabletScreen.gameObject.AddComponent<CanvasGroup>();
            tabletMap = UiKit.Node("Map", tabletScreen, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ScreenH, ScreenH));
            tabletArt = UiKit.Fill("Art", tabletMap).gameObject.AddComponent<RawImage>();
            tabletArt.color = new Color(1f, 0.38f, 0.34f, 1f);
            tabletFog = UiKit.Fill("Fog", tabletMap).gameObject.AddComponent<RawImage>();
            // The tint, a grid, scanlines.
            UiKit.Box(UiKit.Fill("Tint", tabletScreen), new Color(1f, 0.08f, 0.06f, 0.12f));
            var grid = UiKit.Fill("Grid", tabletScreen).gameObject.AddComponent<RawImage>();
            grid.texture = Lines(true);
            grid.uvRect = new Rect(0f, 0f, ScreenW / 64f / 2f, ScreenH / 64f / 2f);
            grid.color = new Color(1f, 0.3f, 0.25f, 0.5f);
            var scan = UiKit.Fill("Scan", tabletScreen).gameObject.AddComponent<RawImage>();
            scan.texture = Lines(false);
            scan.uvRect = new Rect(0f, 0f, 1f, ScreenH / 64f);
            scan.color = new Color(0f, 0f, 0f, 0.35f);
            tabletMarks = UiKit.Fill("Marks", tabletScreen);
            // The lock box: corner brackets and a name plate.
            tabletLock = UiKit.Node("Lock", tabletScreen, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f));
            foreach (Vector2 c in new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) })
            {
                UiKit.Box(UiKit.Node("Bracket", tabletLock, c, c, Vector2.zero, new Vector2(20f, 4f)), new Color(1f, 0.92f, 0.9f, 1f));
                UiKit.Box(UiKit.Node("Bracket", tabletLock, c, c, Vector2.zero, new Vector2(4f, 20f)), new Color(1f, 0.92f, 0.9f, 1f));
            }
            tabletLockName = kit.Label(UiKit.Node("Name", tabletLock, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(320f, 26f)), "", 18, TextAnchor.UpperCenter, new Color(1f, 0.92f, 0.9f, 1f));
            tabletLockName.horizontalOverflow = HorizontalWrapMode.Overflow;
            // The reticle: a ring and a crosshair.
            tabletReticle = UiKit.Node("Reticle", tabletScreen, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 96f));
            var ring = UiKit.Fill("Ring", tabletReticle).gameObject.AddComponent<RawImage>();
            ring.texture = StrikeRing();
            ring.color = new Color(1f, 0.32f, 0.28f, 0.9f);
            foreach (var (pos, size) in new[] { (new Vector2(-38f, 0f), new Vector2(26f, 3f)), (new Vector2(38f, 0f), new Vector2(26f, 3f)), (new Vector2(0f, -38f), new Vector2(3f, 26f)), (new Vector2(0f, 38f), new Vector2(3f, 26f)) })
                UiKit.Box(UiKit.Node("Cross", tabletReticle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size), new Color(1f, 0.32f, 0.28f, 0.9f));
            // Along the top: Exit (top left), the title, the timer; along the bottom: the status and DISPATCH.
            RectTransform exit = UiKit.Node("Exit", tabletScreen, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(120f, 40f));
            UiKit.Box(exit, new Color(0.25f, 0.03f, 0.03f, 0.9f));
            var exitText = kit.Label(UiKit.Fill("Label", exit), "◄ EXIT", 20, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.82f, 1f));
            exitText.raycastTarget = false;
            exit.gameObject.AddComponent<Button>().onClick.AddListener(LowerTablet);
            tabletTitle = kit.Label(UiKit.Node("Title", tabletScreen, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, -18f), new Vector2(620f, 32f)),
                "ORBITAL STRIKE  //  K-POP UPLINK", 22, TextAnchor.MiddleLeft, new Color(1f, 0.82f, 0.78f, 1f));
            tabletTimer = kit.Label(UiKit.Node("Timer", tabletScreen, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -14f), new Vector2(320f, 40f)),
                "STANDBY", 30, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.8f, 1f));
            tabletStatus = kit.Label(UiKit.Node("Status", tabletScreen, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 16f), new Vector2(620f, 140f)),
                "", 17, TextAnchor.LowerLeft, new Color(1f, 0.72f, 0.66f, 1f));
            RectTransform dispatch = UiKit.Node("Dispatch", tabletScreen, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 16f), new Vector2(240f, 56f));
            tabletDispatchBg = UiKit.Box(dispatch, new Color(0.3f, 0.04f, 0.04f, 0.85f));
            tabletDispatch = kit.Label(UiKit.Fill("Label", dispatch), "DISPATCH", 26, TextAnchor.MiddleCenter, new Color(1f, 0.6f, 0.55f, 1f));
            foreach (Graphic g in tabletScreen.GetComponentsInChildren<Graphic>(true))
                if (g.gameObject != exit.gameObject) g.raycastTarget = false;
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
            tabletRoot.gameObject.SetActive(true);
        }

        /// <summary>The tablet straight up, its raise skipped (tests and captures).</summary>
        public void TabletUpNow()
        {
            RaiseTablet();
            Tablet = TabletState.Open;
            tabletT = RaiseTime;
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
                    RaiseTablet();
                return;
            }
            tabletT += dt;
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
                    tabletRoot.gameObject.SetActive(false);
                    return;
                }
            }
            else e = 1f;
            // Swinging up from below and to the right, settling square to the face.
            tabletBody.anchoredPosition = new Vector2(Mathf.Lerp(180f, 0f, e), Mathf.Lerp(-1150f, TabletLift, e));
            tabletBody.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-11f, 0f, e));
            tabletBody.localScale = Vector3.one * TabletScale * Mathf.Lerp(0.86f, 1f, e);
            tabletContent.alpha = Tablet == TabletState.Raising ? Mathf.Clamp01((tabletT / RaiseTime - 0.55f) / 0.45f) * (Random.value < 0.15f ? 0.6f : 1f) : 1f;
            if (me == null || map == null || map.Fog == null) return;
            // Down, or the tablet gone from their hands before it was used: lowered.
            bool hasItem = me.Inv.FirstSlotOf(ItemType.OrbitalStrike) >= 0;
            if (Tablet == TabletState.Open && (!me.CanAct || (lockedTarget == 0 && !hasItem))) LowerTablet();
            if (Tablet == TabletState.Open && kb != null && kb.eKey.wasPressedThisFrame) LowerTablet();

            // The map, as the Tab map: north turned with the view, the wheel zooming it about the pointer.
            tabletArt.texture = map.ArtTexture;
            tabletFog.texture = map.Fog.Texture;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(tabletScreen, mouse != null ? mouse.position.ReadValue() : Vector2.zero, null, out Vector2 pointer);
            bool inside = Mathf.Abs(pointer.x) < ScreenW * 0.5f && Mathf.Abs(pointer.y) < ScreenH * 0.5f;
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
                bool used = i <= targets.Count;
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
                dot.color = zach ? new Color(1f, 0.15f, 0.12f, 1f) : new Color(1f, 0.75f, 0.7f, 1f);
                name.text = zach ? "ZACH" : q.Name.ToUpperInvariant();
                float d = (at - pointer).magnitude;
                if (inside && lockedTarget == 0 && d < best) { best = d; hoveredTarget = q.Id; }
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
                tabletLockName.text = (lockedTarget != 0 ? "LOCKED  " : "TARGET  ") + (bq.Role == Role.Hunter ? "ZACH" : bq.Name.ToUpperInvariant());
            }
            tabletReticle.gameObject.SetActive(inside && lockedTarget == 0);
            tabletReticle.anchoredPosition = pointer;
            tabletReticle.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 30f);

            // A click on a target confirms it.
            if (Tablet == TabletState.Open && lockedTarget == 0 && hoveredTarget != 0 && mouse != null && mouse.leftButton.wasPressedThisFrame)
                ConfirmStrike(hoveredTarget);

            // The timer, the start-up sequence and DISPATCH.
            OrbitalStrike strike = null;
            if (sim != null && lockedTarget != 0)
                foreach (OrbitalStrike s in sim.Strikes) if (s.Owner == me.Id && s.Target == lockedTarget) strike = s;
            if (lockedTarget == 0)
            {
                tabletTimer.text = targets.Count > 0 ? "STANDBY" : "NO TARGETS";
                tabletStatus.text = targets.Count > 0 ? "SELECT A TARGET IN EXPLORED GROUND\n<color=#ff8a80>WHEEL: ZOOM   E: EXIT</color>" : "NO TARGETS IN EXPLORED GROUND";
                tabletDispatch.text = "DISPATCH";
                tabletDispatchBg.color = hoveredTarget != 0 ? new Color(0.75f, 0.1f, 0.1f, 0.95f) : new Color(0.3f, 0.04f, 0.04f, 0.85f);
                return;
            }
            float since = Time.unscaledTime - dispatchedAt;
            float left = strike != null && !strike.Firing ? strike.Countdown : Mathf.Max(0f, Balance.Items.Orbital.Countdown - since);
            bool firing = strike != null && strike.Firing;
            tabletTimer.text = firing ? "STRIKE INBOUND" : $"00:{left:00.0}";
            string name2 = locked != null ? (locked.Role == Role.Hunter ? "ZACH" : locked.Name.ToUpperInvariant()) : "TARGET";
            var lines = new System.Text.StringBuilder();
            lines.Append($"TARGET LOCKED: {name2}");
            if (since > 0.4f) lines.Append("\nUPLINK ESTABLISHED // SAT-7");
            if (since > 0.9f) lines.Append("\nCALIBRATING EMITTER ARRAY");
            if (since > 1.4f) lines.Append($"\nCHARGING  {Mathf.Clamp01((since - 1.4f) / 1.2f) * 100f:0}%");
            if (since > 2.5f) lines.Append("\nDISPATCH AUTHORISED");
            tabletStatus.text = lines.ToString();
            tabletDispatch.text = firing ? "DISPATCHED" : "DISPATCHING";
            tabletDispatchBg.color = new Color(0.85f, 0.12f, 0.12f, 0.6f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8f)));
            // Down it comes: the tablet lowers after a moment.
            if (firing && inboundAt < 0f) inboundAt = Time.unscaledTime;
            if (inboundAt >= 0f && Time.unscaledTime - inboundAt > 1.1f) LowerTablet();
            if (strike == null && since > Balance.Items.Orbital.Countdown + 1.5f) LowerTablet();
        }
    }
}
