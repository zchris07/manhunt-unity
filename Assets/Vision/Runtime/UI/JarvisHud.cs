using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Vision.UI
{
    /// <summary>
    /// The JARVIS screen, for every survivor while it runs. First the boot, two seconds after Tony Stark's JARVIS start-up: a
    /// bright blue light streaks across the middle of the screen, an angular frame draws in round it, "INITIATING J.A.R.V.I.S."
    /// types out and counts 1... 2... 3... with a blinking block cursor, diagnostics flicker under it (network, protocol, memory, CHECKSUM: OK),
    /// a segmented bar fills, and it all breaks up in a glitch. Then, for as long as JARVIS runs: a blue wash and vignette
    /// with scanlines, corner brackets, panels of ticking stats and metrics (uplink, power, core temperature, scan progress,
    /// the session counting down), a pair of rotating range rings, a scrolling hex dump and a bank of meters; and the mouse
    /// becomes a square target reticle with a cross in the middle, like marking an airstrike; and everyone and everything
    /// on the screen is picked out in a recognition box (JarvisHud.Recognition). All of it fades out at the end.
    /// </summary>
    public sealed partial class JarvisHud : MonoBehaviour
    {
        public const float BootTime = 2f, FadeOut = 0.5f;
        static readonly Color Blue = new Color(0.35f, 0.8f, 1f, 1f), Bright = new Color(0.8f, 0.96f, 1f, 1f), Deep = new Color(0.02f, 0.12f, 0.24f, 1f);

        Canvas canvas;
        RectTransform root, boot, hud, reticle, reticleOuter, streak, bar, frame;
        CanvasGroup group, bootGroup, hudGroup;
        UiKit kit;
        Image wash, streakCore, streakGlow;
        RawImage vignette, scan, ringA, ringB, flare;
        Text title, diag, stats, threat, hex, timerText;
        Image[] segs, meters;
        RectTransform[] frameParts, dust;
        Vector2[] dustVel;
        float start = -1f, endAt = -1f, total = 30f, nextTick;
        bool cursorHidden;
        readonly System.Random rng = new System.Random(7);

        /// <summary>Whether the JARVIS screen is up (booting, running or fading out).</summary>
        public bool Showing => start >= 0f;
        /// <summary>Seconds since JARVIS came on (or -1).</summary>
        public float Age => start >= 0f ? Time.unscaledTime - start : -1f;

        static readonly string[] Diagnostics =
        {
            "\\\\NETWARE    196.25.65.9", "PROTOCOL: STARK-7 // HANDSHAKE", "RELEASING CONFIGURATION", "EXTEND SYSTEM MEMORY",
            "RUN SYSTEM TOOL //BACKUP_GHOST", "LOADING THREAT MATRIX", "SAT UPLINK 4/4 LOCKED", "MAPPING TERRAIN MESH", "NEURAL NET: ONLINE",
        };

        void Build()
        {
            if (canvas != null) return;
            kit = new UiKit();
            var go = new GameObject("JARVIS", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root = (RectTransform)go.transform;
            group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            // The whole screen: a blue wash, a vignette darkening to blue at the edges, faint moving scanlines.
            wash = UiKit.Box(UiKit.Fill("Wash", root), A(Deep, 0f));
            vignette = Raw(UiKit.Fill("Vignette", root), Vignette());
            scan = Raw(UiKit.Fill("Scanlines", root), Scanlines());
            scan.uvRect = new Rect(0f, 0f, 1f, 1080f / 4f);

            BuildBoot();
            BuildHud();
            BuildReticle();
            go.SetActive(false);
        }

        static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

        RawImage Raw(RectTransform rt, Texture t)
        {
            var r = rt.gameObject.AddComponent<RawImage>();
            r.texture = t;
            r.raycastTarget = false;
            return r;
        }

        Image Bar(RectTransform parent, string name, Vector2 pos, Vector2 size, Color c, float angle = 0f, Vector2? pivot = null)
        {
            RectTransform rt = UiKit.Node(name, parent, new Vector2(0.5f, 0.5f), pivot ?? new Vector2(0.5f, 0.5f), pos, size);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            return UiKit.Box(rt, c);
        }

        /// <summary>A thin line from a to b (canvas units about the parent's middle), pivoted at a so it can draw in.</summary>
        RectTransform Line(RectTransform parent, Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 d = b - a;
            Image i = Bar(parent, "Line", a, new Vector2(d.magnitude, width), c, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, new Vector2(0f, 0.5f));
            return i.rectTransform;
        }

        void BuildBoot()
        {
            boot = UiKit.Fill("Boot", root);
            bootGroup = boot.gameObject.AddComponent<CanvasGroup>();
            // Dust drifting in the dark.
            dust = new RectTransform[34];
            dustVel = new Vector2[dust.Length];
            for (int i = 0; i < dust.Length; i++)
            {
                float s = 2f + (float)rng.NextDouble() * 3f;
                dust[i] = Bar(boot, "Mote", new Vector2(R(-940f, 940f), R(-520f, 520f)), new Vector2(s, s), A(Blue, R(0.15f, 0.5f))).rectTransform;
                dustVel[i] = new Vector2(R(-14f, 14f), R(-8f, 8f));
            }
            // The angular frame: the top line rising to a raised bar in the middle (the title in it), the bottom one dropping.
            frame = UiKit.Node("Frame", boot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            var pts = new[]
            {
                (new Vector2(-980f, 34f), new Vector2(-600f, 34f)), (new Vector2(-600f, 34f), new Vector2(-540f, 96f)), (new Vector2(-540f, 96f), new Vector2(520f, 96f)),
                (new Vector2(520f, 96f), new Vector2(580f, 34f)), (new Vector2(580f, 34f), new Vector2(980f, 34f)),
                (new Vector2(-980f, -34f), new Vector2(-380f, -34f)), (new Vector2(-380f, -34f), new Vector2(-320f, -96f)), (new Vector2(-320f, -96f), new Vector2(640f, -96f)),
                (new Vector2(640f, -96f), new Vector2(700f, -34f)), (new Vector2(700f, -34f), new Vector2(980f, -34f)),
            };
            frameParts = new RectTransform[pts.Length];
            for (int i = 0; i < pts.Length; i++) frameParts[i] = Line(frame, pts[i].Item1, pts[i].Item2, 2.5f, A(Bright, 0.9f));
            // The light streak across the middle: a soft glow round a white-hot core, a flare where it starts.
            streak = UiKit.Node("Streak", boot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 60f));
            streakGlow = UiKit.Box(UiKit.Fill("Glow", streak), A(Blue, 0.35f));
            streakGlow.rectTransform.offsetMin = new Vector2(0f, 14f);
            streakGlow.rectTransform.offsetMax = new Vector2(0f, -14f);
            RectTransform core = UiKit.Node("Core", streak, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 4f));
            core.anchorMax = new Vector2(1f, 0.5f);
            core.sizeDelta = new Vector2(0f, 4f);
            streakCore = UiKit.Box(core, Bright);
            flare = Raw(UiKit.Node("Flare", boot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-930f, 0f), new Vector2(260f, 200f)), Glow());
            // The title in the raised bar, typing out, its block cursor blinking.
            title = kit.Label(UiKit.Node("Title", boot, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-470f, 64f), new Vector2(980f, 52f)),
                "", 40, TextAnchor.MiddleLeft, Bright, UiKit.Face.Display, 0.08f, false);
            // The diagnostics flickering under the line, and the segmented bar.
            diag = kit.Label(UiKit.Node("Diagnostics", boot, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(-960f, -40f), new Vector2(620f, 60f)),
                "", 15, TextAnchor.UpperLeft, A(Bright, 0.85f), UiKit.Face.Mono, 0f, false);
            bar = UiKit.Node("Progress", boot, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-960f, -64f), new Vector2(900f, 30f));
            segs = new Image[22];
            for (int i = 0; i < segs.Length; i++)
                segs[i] = UiKit.Box(UiKit.Node("Segment", bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 34f, 0f), new Vector2(28f, 30f)), A(Bright, 0f));
        }

        void BuildHud()
        {
            hud = UiKit.Fill("HUD", root);
            hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
            // Corner brackets.
            foreach (var (ax, ay) in new[] { (0f, 0f), (1f, 0f), (0f, 1f), (1f, 1f) })
            {
                RectTransform c = UiKit.Node("Corner", hud, new Vector2(ax, ay), new Vector2(ax, ay), new Vector2(ax < 0.5f ? 36f : -36f, ay < 0.5f ? 36f : -36f), new Vector2(110f, 110f));
                UiKit.Box(UiKit.Node("H", c, new Vector2(ax, ay), new Vector2(ax, ay), Vector2.zero, new Vector2(110f, 3f)), A(Blue, 0.8f));
                UiKit.Box(UiKit.Node("V", c, new Vector2(ax, ay), new Vector2(ax, ay), Vector2.zero, new Vector2(3f, 110f)), A(Blue, 0.8f));
            }
            // Top left: the system and its stats.
            stats = kit.Label(UiKit.Node("Stats", hud, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(64f, -112f), new Vector2(460f, 230f)),
                "", 15, TextAnchor.UpperLeft, A(Bright, 0.9f), UiKit.Face.Mono, 0f, false);
            stats.supportRichText = true;
            stats.lineSpacing = 1.2f;
            // Top right: range rings turning, the threat and the session counting down.
            RectTransform rings = UiKit.Node("Rings", hud, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-170f, 20f), new Vector2(200f, 200f));
            ringA = Raw(UiKit.Fill("Ring A", rings), Ring(true));
            ringB = Raw(UiKit.Fill("Ring B", rings, 34f), Ring(false));
            Bar(rings, "Cross H", Vector2.zero, new Vector2(150f, 1.5f), A(Blue, 0.5f));
            Bar(rings, "Cross V", Vector2.zero, new Vector2(1.5f, 150f), A(Blue, 0.5f));
            threat = kit.Label(UiKit.Node("Threat", hud, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-64f, -352f), new Vector2(360f, 90f)),
                "", 15, TextAnchor.UpperRight, A(Bright, 0.9f), UiKit.Face.Mono, 0f, false);
            timerText = kit.Label(UiKit.Node("Session", hud, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(520f, 40f)),
                "", 22, TextAnchor.MiddleCenter, Bright, UiKit.Face.Display, 0.1f, false);
            // Bottom left: a scrolling hex dump. Right: a bank of meters.
            hex = kit.Label(UiKit.Node("Hex", hud, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(64f, 150f), new Vector2(360f, 220f)),
                "", 13, TextAnchor.LowerLeft, A(Blue, 0.6f), UiKit.Face.Mono, 0f, false);
            RectTransform bank = UiKit.Node("Meters", hud, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-64f, -210f), new Vector2(200f, 120f));
            meters = new Image[14];
            for (int i = 0; i < meters.Length; i++)
            {
                RectTransform m = UiKit.Node("Meter", bank, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(i * 14f, 0f), new Vector2(9f, 120f));
                meters[i] = UiKit.Box(m, A(Blue, 0.7f));
            }
        }

        void BuildReticle()
        {
            reticle = UiKit.Node("Reticle", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
            // The square, the cross through its middle, a dot at the heart; brackets turning round it.
            foreach (var (pos, size) in new[] { (new Vector2(0f, 29f), new Vector2(60f, 2f)), (new Vector2(0f, -29f), new Vector2(60f, 2f)), (new Vector2(29f, 0f), new Vector2(2f, 60f)), (new Vector2(-29f, 0f), new Vector2(2f, 60f)) })
                Bar(reticle, "Side", pos, size, Bright);
            Bar(reticle, "Cross H", Vector2.zero, new Vector2(40f, 1.5f), Bright);
            Bar(reticle, "Cross V", Vector2.zero, new Vector2(1.5f, 40f), Bright);
            Bar(reticle, "Dot", Vector2.zero, new Vector2(5f, 5f), Blue);
            reticleOuter = UiKit.Node("Brackets", reticle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 92f));
            foreach (float ax in new[] { -1f, 1f })
                foreach (float ay in new[] { -1f, 1f })
                {
                    Bar(reticleOuter, "Tick", new Vector2(ax * 40f, ay * 46f), new Vector2(14f, 2f), A(Blue, 0.9f));
                    Bar(reticleOuter, "Tick", new Vector2(ax * 46f, ay * 40f), new Vector2(2f, 14f), A(Blue, 0.9f));
                }
        }

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        /// <summary>
        /// Shows the screen while JARVIS runs: <paramref name="left"/> the seconds it has left (0: off), <paramref name="length"/>
        /// its whole run, <paramref name="menu"/> whether a menu is up (the reticle gives way to the cursor).
        /// </summary>
        /// <param name="skipBoot">Coming back on part way through a run (back in from past the second ring): straight to the screen.</param>
        public void Run(float left, float length, bool menu, bool skipBoot = false)
        {
            float now = Time.unscaledTime;
            if (left > 0f && (start < 0f || endAt >= 0f))
            {
                Build();
                canvas.gameObject.SetActive(true);
                start = skipBoot ? now - BootTime : now;
                endAt = -1f;
                total = Mathf.Max(left, 0.1f);
            }
            if (start < 0f) { ShowCursor(true); return; }
            if (left <= 0f && endAt < 0f) endAt = now;
            float t = now - start;
            float fade = endAt >= 0f ? 1f - Mathf.Clamp01((now - endAt) / FadeOut) : 1f;
            if (fade <= 0f)
            {
                start = -1f;
                canvas.gameObject.SetActive(false);
                ShowCursor(true);
                return;
            }
            group.alpha = fade;
            float dt = Time.unscaledDeltaTime;

            // The wash: deep in the boot, a faint blue tint after; the vignette and scanlines throughout.
            float washA = t < 0.15f ? Mathf.Lerp(0f, 0.5f, t / 0.15f) : t < BootTime ? Mathf.Lerp(0.5f, 0.1f, Ease((t - 1.2f) / 0.8f)) : 0.1f;
            wash.color = A(Deep, washA);
            vignette.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / 0.6f) * 0.6f);
            scan.color = new Color(1f, 1f, 1f, 0.07f);
            scan.uvRect = new Rect(0f, -now * 0.6f, 1f, 1080f / 4f);

            UpdateBoot(t, dt);
            // The running HUD comes up as the boot ends.
            hudGroup.alpha = Ease((t - 1.3f) / 0.7f);
            if (hudGroup.alpha > 0f) UpdateHud(t, left, now);
            UpdateReticle(t, menu, fade);
        }

        void UpdateBoot(float t, float dt)
        {
            bool on = t < BootTime;
            boot.gameObject.SetActive(on);
            if (!on) return;
            // The glitch at the end: flickering out, slices jumping sideways.
            float glitch = Mathf.Clamp01((t - 1.7f) / 0.3f);
            bootGroup.alpha = glitch > 0f ? (1f - glitch) * (Random.value < 0.35f ? 0.3f : 1f) : 1f;
            frame.anchoredPosition = glitch > 0f ? new Vector2(Random.Range(-28f, 28f) * glitch, Random.Range(-4f, 4f) * glitch) : Vector2.zero;
            for (int i = 0; i < dust.Length; i++) dust[i].anchoredPosition += dustVel[i] * dt;
            // The streak flaring across in the first quarter second, then settling as the frame's middle line.
            float sw = Ease(t / 0.25f);
            streak.sizeDelta = new Vector2(1920f * 1.05f * sw, 60f);
            streak.anchoredPosition = new Vector2(0f, 0f);
            float bright = t < 0.35f ? 1f : Mathf.Lerp(1f, 0.55f, (t - 0.35f) / 0.6f);
            streakCore.color = A(Bright, bright);
            streakGlow.color = A(Blue, 0.35f * bright);
            flare.color = A(Blue, 0.55f * bright * (0.8f + 0.2f * Mathf.Sin(t * 40f)));
            // The frame drawing in, piece by piece.
            for (int i = 0; i < frameParts.Length; i++)
            {
                float k = Ease((t - 0.15f - (i % 5) * 0.04f) / 0.2f);
                frameParts[i].localScale = new Vector3(k, 1f, 1f);
            }
            // The title typing out, then counting on (1... 2... 3...), the block cursor blinking after it.
            title.text = BootTitle(t) + (Mathf.Repeat(t, 0.24f) < 0.12f ? " █" : "  ");
            // Diagnostics: a few lines at a time, flickering through, then the checksum.
            if (t < 0.45f) diag.text = "";
            else if (t < 1.3f)
            {
                int k = Mathf.FloorToInt((t - 0.45f) / 0.12f);
                diag.text = Diagnostics[k % Diagnostics.Length] + "\n" + Diagnostics[(k * 3 + 2) % Diagnostics.Length].ToLowerInvariant();
            }
            else diag.text = "CHECKSUM: OK";
            // The segmented bar filling.
            float fill = Mathf.Clamp01((t - 1.05f) / 0.65f) * segs.Length;
            for (int i = 0; i < segs.Length; i++) segs[i].color = A(Bright, Mathf.Clamp01(fill - i) * 0.95f);
        }

        /// <summary>The boot's title <paramref name="t"/> s in: "INITIATING J.A.R.V.I.S." typing out, then 1... 2... 3... a beat apart.</summary>
        public static string BootTitle(float t)
        {
            const string head = "INITIATING J.A.R.V.I.S.";
            int n = Mathf.Clamp(Mathf.RoundToInt((t - 0.3f) / 0.35f * head.Length), 0, head.Length);
            if (n < head.Length) return head.Substring(0, n);
            var sb = new System.Text.StringBuilder(head);
            for (int i = 1; i <= 3; i++)
            {
                // Each count types its digit, then its dots one by one.
                float at = 0.7f + (i - 1) * 0.33f;
                if (t < at) break;
                int dots = Mathf.Clamp(Mathf.FloorToInt((t - at) / 0.07f), 0, 3);
                sb.Append(' ').Append(i).Append('.', dots);
            }
            return sb.ToString();
        }

        void UpdateHud(float t, float left, float now)
        {
            // The rings turn opposite ways; the meters jump about.
            ringA.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -now * 40f);
            ringB.rectTransform.localRotation = Quaternion.Euler(0f, 0f, now * 70f);
            ringA.color = A(Blue, 0.85f);
            ringB.color = A(Bright, 0.7f);
            for (int i = 0; i < meters.Length; i++)
            {
                float h = 0.25f + 0.75f * Mathf.PerlinNoise(now * 2.3f, i * 1.7f);
                meters[i].rectTransform.sizeDelta = new Vector2(9f, 120f * h);
            }
            float secs = Mathf.Max(0f, left);
            timerText.text = $"J.A.R.V.I.S.  ONLINE   {Mathf.FloorToInt(secs / 60f):00}:{secs % 60f:00.0}";
            if (now < nextTick) return;
            nextTick = now + 0.1f;
            float scanPct = Mathf.Clamp01(t / 3f) * 100f;
            int blocks = Mathf.RoundToInt(scanPct / 10f);
            stats.text = "J.A.R.V.I.S. // MK VII  <color=#8fdcff>STARK INDUSTRIES</color>\n" +
                         $"UPLINK ............ SECURE\n" +
                         $"SAT LINK 4/4   {R(40.7f, 40.8f):0.0000}N {R(73.9f, 74.0f):0.0000}W\n" +
                         $"CORE TEMP       {R(36.1f, 36.9f):0.0}°C\n" +
                         $"POWER           {R(98.2f, 99.9f):0.0}%\n" +
                         $"NEURAL NET      {R(41f, 44f):0.0} TFLOPS\n" +
                         $"SCAN  {new string('█', blocks)}{new string('░', 10 - blocks)} {scanPct:0}%";
            threat.text = $"THREAT  .......  ZACH\nVECTOR  {R(0f, 359f):000.0}°  {R(10f, 99f):00.00} m\nSIGNAL  {R(-62f, -48f):0.0} dBm";
            var sb = new System.Text.StringBuilder();
            for (int line = 0; line < 9; line++)
            {
                sb.Append($"0x{rng.Next(0x1000, 0xFFFF):X4}  ");
                for (int b = 0; b < 6; b++) sb.Append($"{rng.Next(0, 256):X2} ");
                sb.Append('\n');
            }
            hex.text = sb.ToString();
        }

        void UpdateReticle(float t, bool menu, float fade)
        {
            bool show = !menu && Mouse.current != null;
            reticle.gameObject.SetActive(show);
            ShowCursor(!show);
            if (!show) return;
            Vector2 mouse = Mouse.current.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, mouse, null, out Vector2 local);
            reticle.anchoredPosition = local;
            // It locks on as JARVIS comes up (shrinking in from wide), the brackets turning slowly round it.
            float lockOn = Ease(t / 0.4f);
            reticle.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, lockOn) * (1f + 0.04f * Mathf.Sin(t * 6f));
            reticleOuter.localRotation = Quaternion.Euler(0f, 0f, t * 25f);
        }

        void ShowCursor(bool visible)
        {
            if (cursorHidden == !visible) return;
            cursorHidden = !visible;
            Cursor.visible = visible;
        }

        void OnDisable()
        {
            if (cursorHidden) Cursor.visible = true;
            cursorHidden = false;
        }

        // ---------------------------------------------------------------- textures

        static Texture2D vignetteTex, scanTex, ringTexA, ringTexB, glowTex;

        /// <summary>A soft white glow, bright in the middle (the streak's flare).</summary>
        static Texture2D Glow()
        {
            if (glowTex != null) return glowTex;
            const int n = 64;
            glowTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 2.2f);
                    glowTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            glowTex.Apply();
            return glowTex;
        }

        static Texture2D Vignette()
        {
            if (vignetteTex != null) return vignetteTex;
            const int n = 128;
            vignetteTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.25f, d)) * 0.75f;
                    vignetteTex.SetPixel(x, y, new Color(0.08f, 0.35f, 0.65f, a));
                }
            vignetteTex.Apply();
            return vignetteTex;
        }

        static Texture2D Scanlines()
        {
            if (scanTex != null) return scanTex;
            scanTex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < 4; y++) scanTex.SetPixel(0, y, y < 1 ? new Color(0.6f, 0.9f, 1f, 1f) : new Color(0f, 0f, 0f, 0f));
            scanTex.Apply();
            return scanTex;
        }

        /// <summary>A range ring: a thin circle broken into dashes (A), or a thick arc with gaps and ticks (B).</summary>
        static Texture2D Ring(bool dashed)
        {
            ref Texture2D cache = ref dashed ? ref ringTexA : ref ringTexB;
            if (cache != null) return cache;
            const int n = 256;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy), ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 180f;
                    float a;
                    if (dashed) a = Mathf.Abs(r - 0.95f) < 0.012f && Mathf.Repeat(ang, 12f) < 7f ? 1f : Mathf.Abs(r - 0.82f) < 0.006f ? 0.6f : 0f;
                    else a = (Mathf.Abs(r - 0.9f) < 0.04f && (ang % 120f) < 85f) || (Mathf.Abs(r - 0.7f) < 0.02f && Mathf.Repeat(ang, 30f) < 3f) ? 1f : 0f;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            cache = t;
            return t;
        }
    }
}
