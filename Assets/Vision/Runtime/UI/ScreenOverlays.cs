using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Vision.UI
{
    /// <summary>
    /// Pictures over the whole screen, above the game and the HUD, as the original shows them: the Soundcloud Burst scare
    /// (black, the image covering the screen, shaking), a flashed picture (The Grapes of Wrath hitting Zach shakes; slaying
    /// Waz doesn't), each fading in and out, and a note (an old photo printed on torn, yellowed paper, tilted, until clicked
    /// or a key is pressed). Also the big announcements (JARVIS ONLINE, HEMP BATTERY ACTIVATED).
    /// </summary>
    public sealed class ScreenOverlays : MonoBehaviour
    {
        Canvas canvas;
        RectTransform root, announcements;
        UiKit kit;

        RectTransform scare, scareImg, flash, flashImg, note, paper, photoRt, burnRt, big, center;
        RawImage scareRaw, flashRaw, photo, burn;
        Image scareBack;
        CanvasGroup scareGroup, flashGroup, noteGroup, bigGroup;
        Text bigText, noteHint, centerText;
        float scareStart = -1f, scareEnd, scareFade, flashStart = -1f, flashEnd, flashFade, noteStart = -1f, bigStart, bigEnd = -1f, centerEnd = -1f;
        bool flashShake;
        int noteShown = -1;

        public bool ScareShowing => scare != null && scare.gameObject.activeSelf;
        public bool FlashShowing => flash != null && flash.gameObject.activeSelf;
        public bool NoteShowing => note != null && note.gameObject.activeSelf;
        public int NoteIndex => NoteShowing ? noteShown : -1;
        public string BigShowing => big != null && big.gameObject.activeSelf ? bigText.text : null;
        public string CenterShowing => center != null && center.gameObject.activeSelf ? centerText.text : null;

        public static Texture2D Picture(string name) => Resources.Load<Texture2D>("Images/" + name);

        void Awake() => EnsureBuilt();

        void EnsureBuilt()
        {
            if (canvas != null) return;
            kit = new UiKit();
            var go = new GameObject("Screen Overlays", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            root = (RectTransform)go.transform;

            // The scare: black, the picture covering everything.
            scare = UiKit.Fill("Scare", root);
            scareBack = UiKit.Box(scare, Color.black);
            scareGroup = scare.gameObject.AddComponent<CanvasGroup>();
            scareGroup.blocksRaycasts = false;
            scareImg = UiKit.Node("Picture", scare, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            scareRaw = scareImg.gameObject.AddComponent<RawImage>();
            scareRaw.raycastTarget = false;
            scareRaw.texture = Picture("scare");
            scare.gameObject.SetActive(false);

            // A flashed picture: shown whole ("contain") over the game.
            flash = UiKit.Fill("Flash", root);
            flashGroup = flash.gameObject.AddComponent<CanvasGroup>();
            flashGroup.blocksRaycasts = false;
            flashImg = UiKit.Node("Picture", flash, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            flashRaw = flashImg.gameObject.AddComponent<RawImage>();
            flashRaw.raycastTarget = false;
            flash.gameObject.SetActive(false);

            // A note: dim backdrop, torn paper, the photo, burnt edges, and how to put it down.
            note = UiKit.Fill("Note", root);
            var dim = note.gameObject.AddComponent<RawImage>();
            dim.texture = NoteBackdrop();
            dim.raycastTarget = true;
            noteGroup = note.gameObject.AddComponent<CanvasGroup>();
            var click = note.gameObject.AddComponent<Button>();
            click.transition = Selectable.Transition.None;
            click.onClick.AddListener(HideNote);
            paper = UiKit.Node("Paper", note, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(600f, 800f));
            var paperShadow = UiKit.Node("Shadow", paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), Vector2.one);
            paperShadow.anchorMin = Vector2.zero;
            paperShadow.anchorMax = Vector2.one;
            paperShadow.offsetMin = new Vector2(-50f, -68f);
            paperShadow.offsetMax = new Vector2(50f, 32f);
            var ps = paperShadow.gameObject.AddComponent<RawImage>();
            ps.texture = UiKit.SoftShadow();
            ps.color = new Color(0f, 0f, 0f, 0.85f);
            ps.raycastTarget = false;
            var paperImg = UiKit.Fill("Sheet", paper).gameObject.AddComponent<RawImage>();
            paperImg.texture = PaperTexture();
            paperImg.raycastTarget = false;
            photoRt = UiKit.Node("Photo", paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            photo = photoRt.gameObject.AddComponent<RawImage>();
            photo.color = new Color(1f, 1f, 1f, 0.95f);
            photo.raycastTarget = false;
            burnRt = UiKit.Fill("Burn", paper);
            burn = burnRt.gameObject.AddComponent<RawImage>();
            burn.texture = BurnTexture();
            burn.raycastTarget = false;
            noteHint = kit.Label(UiKit.Node("Hint", note, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(800f, 24f)),
                "CLICK TO PUT IT DOWN", 15, TextAnchor.MiddleCenter, new Color(214f / 255f, 190f / 255f, 140f / 255f, 0.45f), UiKit.Face.Type, 0.2f, false);
            note.gameObject.SetActive(false);

            // Announcements sit with the HUD, under the menus (their own canvas, just below the HUD's).
            var annGo = new GameObject("Announcements", typeof(RectTransform));
            annGo.transform.SetParent(transform, false);
            var annCanvas = annGo.AddComponent<Canvas>();
            annCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            annCanvas.sortingOrder = 9;
            var annScaler = annGo.AddComponent<CanvasScaler>();
            annScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            annScaler.referenceResolution = new Vector2(1920f, 1080f);
            annScaler.matchWidthOrHeight = 0.5f;
            announcements = (RectTransform)annGo.transform;

            // Big announcements (JARVIS ONLINE), a third of the way down: bone, widely tracked, red and cyan fringes.
            big = UiKit.Node("Big", announcements, new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 120f));
            bigGroup = big.gameObject.AddComponent<CanvasGroup>();
            bigGroup.blocksRaycasts = false;
            bigText = kit.Label(UiKit.Fill("Text", big), "", 72, TextAnchor.MiddleCenter, UiKit.Bone, UiKit.Face.Display, 0.35f, false);
            Fringe(bigText, 3f, 0.8f, 0.4f);
            big.gameObject.SetActive(false);

            // Short centre messages (STUNNED, You are down, THE GATE IS OPEN), a fifth of the way down.
            center = UiKit.Node("Center", announcements, new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 60f));
            centerText = kit.Label(UiKit.Fill("Text", center), "", 40, TextAnchor.MiddleCenter, UiKit.Bone, UiKit.Face.Display, 0.08f, false);
            Fringe(centerText, 2f, 0.8f, 0f);
            center.gameObject.SetActive(false);
        }

        Vector2 Screen => root.rect.size;

        // ---------------------------------------------------------------- the API

        /// <summary>The Soundcloud Burst hit you: the picture fades in over a black screen, shakes, then fades out.</summary>
        public void JumpScare(float seconds, float fade)
        {
            EnsureBuilt();
            scareStart = Time.unscaledTime;
            scareEnd = scareStart + seconds;
            scareFade = Mathf.Max(0.01f, fade);
            scare.gameObject.SetActive(true);
            scare.SetAsLastSibling();
            Update();
        }

        /// <summary>A picture over the screen for <paramref name="seconds"/>, fading in and out over <paramref name="fade"/>.</summary>
        public void FlashImage(Texture picture, float seconds, float fade, bool shake)
        {
            EnsureBuilt();
            if (picture == null) return;
            flashRaw.texture = picture;
            flashShake = shake;
            flashStart = Time.unscaledTime;
            flashEnd = flashStart + seconds;
            flashFade = Mathf.Max(0.01f, fade);
            flash.gameObject.SetActive(true);
            flash.SetAsLastSibling();
            Update();
        }

        /// <summary>Reads note <paramref name="index"/> (0-3): its photo on old paper fills the screen until put down.</summary>
        public void ShowNote(int index)
        {
            EnsureBuilt();
            Texture2D t = Picture($"note-{index + 1}");
            if (t == null) return;
            photo.texture = t;
            noteShown = index;
            noteStart = Time.unscaledTime;
            // Each note lies a little differently on the table.
            paper.localRotation = Quaternion.Euler(0f, 0f, -(((index * 37) % 5) - 2.2f));
            note.gameObject.SetActive(true);
            note.SetAsLastSibling();
            Layout();
        }

        public void HideNote()
        {
            if (note == null) return;
            note.gameObject.SetActive(false);
            noteShown = -1;
        }

        /// <summary>A big, punchy announcement for a few seconds.</summary>
        public void Big(string text, float seconds = 3f)
        {
            EnsureBuilt();
            bigText.text = text.ToUpperInvariant();
            bigStart = Time.unscaledTime;
            bigEnd = bigStart + seconds;
            big.gameObject.SetActive(true);
        }

        /// <summary>A short message in the middle of the upper screen.</summary>
        public void Center(string text, float seconds = 3.5f)
        {
            EnsureBuilt();
            centerText.text = text;
            centerEnd = Time.unscaledTime + seconds;
            center.gameObject.SetActive(true);
        }

        /// <summary>The theme's chromatic fringe: a red copy to the right, a faint cyan one to the left, a dark glow.</summary>
        static void Fringe(Text t, float px, float red, float cyan)
        {
            var glow = t.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            glow.effectDistance = new Vector2(2f, 2f);
            var r = t.gameObject.AddComponent<Shadow>();
            r.effectColor = UiKit.A(UiKit.Red, red);
            r.effectDistance = new Vector2(px, 0f);
            if (cyan <= 0f) return;
            var c = t.gameObject.AddComponent<Shadow>();
            c.effectColor = new Color(90f / 255f, 160f / 255f, 170f / 255f, cyan);
            c.effectDistance = new Vector2(-px, 0f);
        }

        public void ClearAll()
        {
            if (canvas == null) return;
            scare.gameObject.SetActive(false);
            flash.gameObject.SetActive(false);
            big.gameObject.SetActive(false);
            center.gameObject.SetActive(false);
            HideNote();
        }

        // ---------------------------------------------------------------- per frame

        /// <summary>The original's scare-shake keyframes: small jumps and a 2% zoom, looping.</summary>
        static Vector3 ShakeAt(float t, float period)
        {
            float u = Mathf.Repeat(t / period, 1f) * 4f;
            Vector2[] p = { Vector2.zero, new Vector2(4f, -3f), new Vector2(-3f, 4f), new Vector2(3f, 3f), Vector2.zero };
            float[] s = { 1f, 1.02f, 1.02f, 1.02f, 1f };
            int i = Mathf.Min(3, Mathf.FloorToInt(u));
            float f = u - i;
            Vector2 at = Vector2.Lerp(p[i], p[i + 1], f);
            return new Vector3(at.x, -at.y, Mathf.Lerp(s[i], s[i + 1], f));
        }

        static float FadeAt(float now, float start, float end, float fade) =>
            Mathf.Clamp01(Mathf.Min((now - start) / fade, (end - now) / fade));

        void Update()
        {
            if (canvas == null) return;
            float now = Time.unscaledTime;
            Vector2 screen = Screen;
            if (scare.gameObject.activeSelf)
            {
                if (now >= scareEnd) scare.gameObject.SetActive(false);
                else
                {
                    scareGroup.alpha = Mathf.SmoothStep(0f, 1f, FadeAt(now, scareStart, scareEnd, scareFade));
                    Vector3 sh = ShakeAt(now - scareStart, 0.18f);
                    scareImg.sizeDelta = Cover(scareRaw.texture, screen) * sh.z;
                    scareImg.anchoredPosition = new Vector2(sh.x, sh.y);
                }
            }
            if (flash.gameObject.activeSelf)
            {
                if (now >= flashEnd) flash.gameObject.SetActive(false);
                else
                {
                    flashGroup.alpha = Mathf.SmoothStep(0f, 1f, FadeAt(now, flashStart, flashEnd, flashFade));
                    Vector3 sh = flashShake ? ShakeAt(now - flashStart, 0.22f) : new Vector3(0f, 0f, 1f);
                    flashImg.sizeDelta = Contain(flashRaw.texture, screen) * sh.z;
                    flashImg.anchoredPosition = new Vector2(sh.x, sh.y);
                }
            }
            if (note.gameObject.activeSelf)
            {
                float k = Mathf.Clamp01((now - noteStart) / 0.6f);
                float e = 1f - (1f - k) * (1f - k);
                noteGroup.alpha = e;
                paper.localScale = Vector3.one * Mathf.Lerp(1.04f, 1f, e);
                // The burnt edges flicker (steps, as the original's keyframes).
                float c = Mathf.Repeat(now - noteStart, 3.4f) / 3.4f;
                burn.color = new Color(1f, 1f, 1f, c < 0.47f ? 1f : c < 0.5f ? 0.92f : c < 0.53f ? 0.8f : c < 0.56f ? 0.95f : 1f);
                Layout();
                Keyboard kb = Keyboard.current;
                if (now - noteStart > 0.15f && kb != null && kb.anyKey.wasPressedThisFrame) HideNote();
            }
            if (big.gameObject.activeSelf)
            {
                float left = bigEnd - now;
                if (left <= 0f) big.gameObject.SetActive(false);
                else
                {
                    // Pops in (0.35 s, overshooting) and fades as it ends.
                    float k = Mathf.Clamp01((now - bigStart) / 0.35f);
                    float pop = 1f + 2.70158f * Mathf.Pow(k - 1f, 3f) + 1.70158f * Mathf.Pow(k - 1f, 2f);
                    big.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, pop);
                    bigGroup.alpha = Mathf.Min(k / 0.6f, Mathf.Clamp01(left / 0.35f));
                }
            }
            if (center.gameObject.activeSelf && now >= centerEnd) center.gameObject.SetActive(false);
        }

        void Layout()
        {
            Vector2 screen = Screen;
            if (photo.texture == null) return;
            float pad = 0.032f * Mathf.Min(screen.x, screen.y);
            Vector2 max = new Vector2(screen.x * 0.9f - 2f * pad, screen.y * 0.86f - 2f * pad);
            Vector2 size = Contain(photo.texture, max);
            photoRt.sizeDelta = size;
            paper.sizeDelta = size + Vector2.one * (2f * pad);
        }

        static Vector2 Contain(Texture t, Vector2 box)
        {
            if (t == null) return box;
            float k = Mathf.Min(box.x / t.width, box.y / t.height);
            return new Vector2(t.width * k, t.height * k);
        }

        static Vector2 Cover(Texture t, Vector2 box)
        {
            if (t == null) return box;
            float k = Mathf.Max(box.x / t.width, box.y / t.height);
            return new Vector2(t.width * k, t.height * k);
        }

        // ---------------------------------------------------------------- textures

        static Texture2D noteBackdrop, paperTex, burnTex;

        static Texture2D NoteBackdrop()
        {
            if (noteBackdrop != null) return noteBackdrop;
            const int n = 96;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            Color inner = new Color(14f / 255f, 10f / 255f, 6f / 255f, 0.82f), outer = new Color(2f / 255f, 1f / 255f, 1f / 255f, 0.97f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    t.SetPixel(x, y, Color.Lerp(inner, outer, Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.414f)));
                }
            t.Apply();
            return noteBackdrop = t;
        }

        /// <summary>The original's torn edge (its clip-path), as a polygon in 0-1 coordinates, y down.</summary>
        static readonly Vector2[] Torn =
        {
            new Vector2(0.005f, 0.015f), new Vector2(0.06f, 0.003f), new Vector2(0.14f, 0.014f), new Vector2(0.27f, 0.002f), new Vector2(0.41f, 0.011f),
            new Vector2(0.55f, 0.001f), new Vector2(0.70f, 0.013f), new Vector2(0.84f, 0.002f), new Vector2(0.94f, 0.012f), new Vector2(0.996f, 0.006f),
            new Vector2(0.99f, 0.12f), new Vector2(0.998f, 0.27f), new Vector2(0.989f, 0.44f), new Vector2(0.997f, 0.61f), new Vector2(0.99f, 0.78f),
            new Vector2(0.998f, 0.93f), new Vector2(0.986f, 0.996f), new Vector2(0.90f, 0.987f), new Vector2(0.76f, 0.998f), new Vector2(0.61f, 0.988f),
            new Vector2(0.47f, 0.999f), new Vector2(0.32f, 0.989f), new Vector2(0.18f, 0.998f), new Vector2(0.07f, 0.988f), new Vector2(0.003f, 0.995f),
            new Vector2(0.01f, 0.86f), new Vector2(0.001f, 0.70f), new Vector2(0.01f, 0.52f), new Vector2(0.002f, 0.35f), new Vector2(0.011f, 0.18f),
        };

        static bool Inside(Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = Torn.Length - 1; i < Torn.Length; j = i++)
            {
                Vector2 a = Torn[i], b = Torn[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>Yellowed paper: a diagonal gradient, a warm highlight top left, a brown shadow bottom right, grain, torn edges.</summary>
        static Texture2D PaperTexture()
        {
            if (paperTex != null) return paperTex;
            const int n = 384;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var rng = new System.Random(9);
            Color c0 = UiKit.Hex(0xd9c590), c1 = UiKit.Hex(0xbfa66c), c2 = UiKit.Hex(0xa88a52);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = 1f - (y + 0.5f) / n;
                    if (!Inside(new Vector2(u, v))) { t.SetPixel(x, y, Color.clear); continue; }
                    float g = (u + v) * 0.5f;
                    Color c = g < 0.55f ? Color.Lerp(c0, c1, g / 0.55f) : Color.Lerp(c1, c2, (g - 0.55f) / 0.45f);
                    float hl = Mathf.Clamp01(1f - Mathf.Sqrt(Sq((u - 0.2f) / 0.55f) + Sq((v - 0.15f) / 0.55f)));
                    c = Color.Lerp(c, new Color(1f, 238f / 255f, 190f / 255f), hl * 0.5f);
                    float sh = Mathf.Clamp01(1f - Mathf.Sqrt(Sq((u - 0.85f) / 0.5f) + Sq((v - 0.9f) / 0.5f)));
                    c = Color.Lerp(c, new Color(110f / 255f, 70f / 255f, 25f / 255f), sh * 0.55f);
                    float noise = (float)rng.NextDouble();
                    c *= 0.9f + 0.1f * noise;
                    c.a = 1f;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return paperTex = t;
        }

        static float Sq(float v) => v * v;

        /// <summary>Over the photo: coffee stains, fold lines, brown speckle and the burnt edge (an inset shadow).</summary>
        static Texture2D BurnTexture()
        {
            if (burnTex != null) return burnTex;
            const int n = 384;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var rng = new System.Random(4);
            Color brown = new Color(60f / 255f, 35f / 255f, 10f / 255f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = 1f - (y + 0.5f) / n;
                    if (!Inside(new Vector2(u, v))) { t.SetPixel(x, y, Color.clear); continue; }
                    float a = 0f;
                    a = Mathf.Max(a, 0.38f * Mathf.Clamp01(1f - Mathf.Sqrt(Sq(u - 0.12f) + Sq(v - 0.8f)) / 0.14f));
                    a = Mathf.Max(a, 0.30f * Mathf.Clamp01(1f - Mathf.Sqrt(Sq(u - 0.78f) + Sq(v - 0.18f)) / 0.12f));
                    a = Mathf.Max(a, 0.18f * Mathf.Clamp01(1f - Mathf.Sqrt(Sq(u - 0.6f) + Sq(v - 0.6f)) / 0.22f));
                    if (Mathf.Abs(u - 0.5f) < 0.004f) a = Mathf.Max(a, 0.4f);
                    if (Mathf.Abs(v - 0.5f) < 0.004f) a = Mathf.Max(a, 0.35f);
                    a = Mathf.Max(a, (float)rng.NextDouble() * 0.16f);
                    // The burnt edge: dark brown, deepest at the paper's edge.
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    float burnK = Mathf.Clamp01(1f - edge / 0.09f);
                    Color c = Color.Lerp(brown, new Color(20f / 255f, 8f / 255f, 2f / 255f), burnK);
                    a = Mathf.Max(a, 0.85f * burnK * burnK);
                    c.a = a;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return burnTex = t;
        }
    }
}
