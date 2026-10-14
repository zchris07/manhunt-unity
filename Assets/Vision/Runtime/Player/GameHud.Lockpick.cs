using UnityEngine;
using UnityEngine.UI;
using Vision.Game;
using Vision.UI;

namespace Vision.Player
{
    /// <summary>
    /// Picking the graveyard's lock: a heavy iron dial in the middle of the screen with a ring of graduations, a gilded
    /// mark somewhere on it and a bright tick sweeping round. Stop the tick on the mark (E, Space or a click) five
    /// times, each faster with a narrower mark and turning the other way; two misses and the pins drop, start over.
    /// Five pins under the dial set as you go, two lamps show the misses, and the whole thing answers each press: a
    /// pulse and a flash of gold on the mark, a red jolt for a miss, START OVER when the pins drop, UNLOCKED at the end.
    /// The sim judges each press (<see cref="MatchSim"/>'s PickStrike); this only shows it.
    /// </summary>
    public sealed partial class GameHud
    {
        RectTransform pick, pickDial, pickNeedle, pickZone, pickZoneGlow;
        CanvasGroup pickGroup;
        Image pickNeedleImg, pickZoneImg, pickZoneGlowImg, pickHub;
        readonly Image[] pickPins = new Image[5], pickLamps = new Image[2];
        Text pickTitle, pickStatus, pickHint;
        float pickK, pickHit, pickMiss, pickReset, pickLastResultT = -1f, pickLastT = float.NaN, pickStamp, pickDone;
        int pickLastStage;
        bool pickWas;

        static readonly Color PickGold = new Color(1f, 0.78f, 0.32f), PickIron = new Color(0.62f, 0.6f, 0.58f), PickRed = new Color(1f, 0.25f, 0.2f);

        /// <summary>A sprite drawn by a function of the point in [-1, 1]^2.</summary>
        static Sprite Drawn(int n, System.Func<float, float, Color> f)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = f((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f);
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        static float Band(float r, float a, float b, float soft) => Mathf.Clamp01((r - a) / soft) * Mathf.Clamp01((b - r) / soft);

        Image PickImage(string name, Transform parent, Sprite s, Vector2 size, Color c)
        {
            RectTransform rt = Node(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Where pin <paramref name="i"/> sits along the row (the five pins centred a little left of the lamps).</summary>
        static float PinX(int i) => (i - 2) * 52f - 20f;

        void BuildLockpick(Transform parent)
        {
            pick = Node("Lockpick", (RectTransform)parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(520f, 560f));
            pickGroup = pick.gameObject.AddComponent<CanvasGroup>();
            pickGroup.blocksRaycasts = false;
            pickGroup.interactable = false;

            // A pool of dark behind it, so it reads over anything.
            PickImage("Shade", pick, Drawn(128, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                return new Color(0f, 0f, 0f, 0.78f * Mathf.Clamp01(1f - Mathf.SmoothStep(0.45f, 1f, r)));
            }), new Vector2(620f, 620f), Color.white);

            pickDial = Node("Dial", pick, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(320f, 320f));
            // The face: dark worn iron, a bevelled rim, a sunk inner ring.
            PickImage("Face", pickDial, Drawn(256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                if (r > 1f) return Color.clear;
                float grain = 0.5f + 0.5f * Mathf.Sin(Mathf.Atan2(v, u) * 90f + r * 40f) * 0.15f;
                float shade = Mathf.Lerp(0.16f, 0.09f, r) * (0.92f + grain * 0.16f) + 0.05f * Mathf.Clamp01(v * 0.5f + 0.5f);
                float rim = Band(r, 0.9f, 1f, 0.02f) * (0.18f + 0.14f * v);
                float inner = Band(r, 0.6f, 0.63f, 0.01f) * 0.08f;
                float c = shade + rim + inner;
                return new Color(c, c * 0.97f, c * 0.93f, Mathf.Clamp01((1f - r) / 0.015f));
            }), new Vector2(320f, 320f), Color.white);
            // Graduations: sixty, every fifth long.
            PickImage("Ticks", pickDial, Drawn(512, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Repeat(Mathf.Atan2(u, v) * Mathf.Rad2Deg, 360f);
                float k = Mathf.Repeat(a, 6f);
                float d = Mathf.Min(k, 6f - k);
                bool major = Mathf.RoundToInt(a / 6f) % 5 == 0;
                float inner = major ? 0.7f : 0.76f;
                float s = d * Mathf.Deg2Rad * r, half = major ? 0.009f : 0.006f;
                float on = Mathf.Clamp01((half - s) / 0.004f + 0.5f) * Band(r, inner, 0.84f, 0.01f);
                return new Color(1f, 1f, 1f, on * (major ? 0.55f : 0.3f));
            }), new Vector2(320f, 320f), Color.white);

            // The mark: a gilded arc on the ring, with a soft glow round it.
            Sprite arc = Drawn(256, (u, v) => new Color(1f, 1f, 1f, Band(Mathf.Sqrt(u * u + v * v), 0.69f, 0.88f, 0.015f)));
            Sprite glow = Drawn(128, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(1f - Mathf.Abs(r - 0.79f) / 0.18f) * 0.8f);
            });
            pickZoneGlowImg = PickImage("Mark Glow", pickDial, glow, new Vector2(340f, 340f), new Color(PickGold.r, PickGold.g, PickGold.b, 0.35f));
            pickZoneImg = PickImage("Mark", pickDial, arc, new Vector2(320f, 320f), PickGold);
            foreach (Image im in new[] { pickZoneGlowImg, pickZoneImg })
            {
                im.type = Image.Type.Filled;
                im.fillMethod = Image.FillMethod.Radial360;
                im.fillOrigin = (int)Image.Origin360.Top;
                im.fillClockwise = true;
            }
            pickZone = pickZoneImg.rectTransform;
            pickZoneGlow = pickZoneGlowImg.rectTransform;

            // The tick: a bright tapered blade from the hub out across the ring, its tip hot.
            pickNeedle = Node("Needle", pickDial, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 320f));
            pickNeedleImg = PickImage("Blade", pickNeedle, Drawn(256, (u, v) =>
            {
                if (v < 0.12f || v > 0.95f) return Color.clear;
                float w = Mathf.Lerp(0.035f, 0.012f, (v - 0.12f) / 0.83f);
                float a = Mathf.Clamp01((w - Mathf.Abs(u)) / 0.01f);
                float hot = Mathf.SmoothStep(0.6f, 0.9f, v);
                return new Color(1f, 1f - hot * 0.1f, 1f - hot * 0.3f, a * Mathf.Lerp(0.75f, 1f, hot));
            }), new Vector2(320f, 320f), Color.white);
            // The hub, with a keyhole.
            pickHub = PickImage("Hub", pickDial, Drawn(128, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                if (r > 1f) return Color.clear;
                bool hole = (u * u + (v - 0.18f) * (v - 0.18f) < 0.07f) || (v < 0.18f && v > -0.5f && Mathf.Abs(u) < 0.1f + (0.18f - v) * 0.12f);
                float c = hole ? 0.02f : Mathf.Lerp(0.42f, 0.22f, r) + 0.12f * v;
                return new Color(c * 1.05f, c * 0.95f, c * 0.7f, Mathf.Clamp01((1f - r) / 0.04f));
            }), new Vector2(76f, 76f), Color.white);

            pickTitle = kit.Label(Node("Title", pick, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(520f, 44f)),
                "PICK THE LOCK", 34, TextAnchor.MiddleCenter, new Color(0.95f, 0.88f, 0.76f), UiKit.Face.DisplayBold, 0.16f);
            pickTitle.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            pickStatus = kit.Label(Node("Status", pickDial, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(420f, 40f)),
                "", 28, TextAnchor.MiddleCenter, PickRed, UiKit.Face.DisplayBold, 0.12f);
            pickStatus.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            // Five pins under the dial, two miss lamps beside them.
            Sprite pinSprite = Drawn(64, (u, v) =>
            {
                float dx = Mathf.Max(0f, Mathf.Abs(u) - 0.55f), dy = Mathf.Max(0f, Mathf.Abs(v) - 0.25f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float edge = Mathf.Clamp01((0.4f - d) / 0.06f);
                return new Color(1f, 1f, 1f, edge);
            });
            Sprite lamp = Drawn(64, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                return new Color(1f, 1f, 1f, Mathf.Clamp01((0.8f - r) / 0.08f));
            });
            RectTransform row = Node("Pins", pick, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(300f, 30f));
            for (int i = 0; i < pickPins.Length; i++)
            {
                pickPins[i] = PickImage($"Pin {i + 1}", row, pinSprite, new Vector2(44f, 20f), Color.white);
                pickPins[i].rectTransform.anchoredPosition = new Vector2(PinX(i), 0f);
            }
            for (int i = 0; i < 2; i++)
            {
                pickLamps[i] = PickImage($"Miss {i + 1}", row, lamp, new Vector2(20f, 20f), Color.white);
                pickLamps[i].rectTransform.anchoredPosition = new Vector2(122f + i * 26f, 0f);
            }
            pickHint = kit.Label(Node("Hint", pick, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(520f, 30f)),
                "E  /  SPACE  /  CLICK  as the tick crosses the gold", 15, TextAnchor.MiddleCenter, new Color(0.85f, 0.8f, 0.72f, 0.85f), UiKit.Face.Mono, 0.06f);
            pick.gameObject.SetActive(false);
        }

        void RefreshLockpick(SimPlayer me, MatchSim sim)
        {
            if (pick == null) return;
            bool picking = me != null && sim != null && me.Action == ActionKind.PickLock && !MainMenuOpen;
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            // It ended on the last pin: UNLOCKED, then away.
            if (pickWas && !picking && me != null && me.PickStage >= Balance.Graveyard.PickChecks) pickDone = 1.1f;
            pickWas = picking;
            pickDone = Mathf.Max(0f, pickDone - dt);
            bool show = picking || pickDone > 0f;
            pickK = Mathf.MoveTowards(pickK, show ? 1f : 0f, dt * (show ? 6f : 3.5f));
            pick.gameObject.SetActive(pickK > 0f);
            if (pickK <= 0f || me == null) return;
            pickGroup.alpha = pickK;

            // Each press, as the sim saw it.
            if (me.PickResultT != pickLastResultT)
            {
                if (pickLastResultT >= 0f || picking)
                {
                    if (me.PickResult == 1) pickHit = 1f;
                    else if (me.PickResult == -1) pickMiss = 1f;
                    else if (me.PickResult == -2) { pickMiss = 1f; pickReset = 1f; }
                }
                pickLastResultT = me.PickResultT;
            }
            pickHit = Mathf.Max(0f, pickHit - dt * 3f);
            pickMiss = Mathf.Max(0f, pickMiss - dt * 2.6f);
            pickReset = Mathf.Max(0f, pickReset - dt * 0.9f);

            int stage = Mathf.Clamp(me.PickStage, 0, Balance.Graveyard.PickChecks - 1);
            // The tick, carried on smoothly between the match's 30 Hz steps.
            if (me.PickT != pickLastT)
            {
                pickLastT = me.PickT;
                pickStamp = now;
            }
            float t = me.PickT + Mathf.Min(0.12f, now - pickStamp);
            float needle = picking && t > 0f ? Mathf.Repeat(MatchSim.PickSpeed(stage) * t * me.PickDir, 360f) : 0f;
            pickNeedle.localRotation = Quaternion.Euler(0f, 0f, -needle);
            float w = MatchSim.PickWidth(stage);
            pickZoneImg.fillAmount = w / 360f;
            pickZone.localRotation = Quaternion.Euler(0f, 0f, -(me.PickZone - w * 0.5f));
            pickZoneGlowImg.fillAmount = (w + 14f) / 360f;
            pickZoneGlow.localRotation = Quaternion.Euler(0f, 0f, -(me.PickZone - (w + 14f) * 0.5f));
            bool waiting = picking && t <= 0f;
            float breathe = 0.5f + 0.5f * Mathf.Sin(now * 6f);
            pickZoneImg.color = Color.Lerp(PickGold, Color.white, pickHit * 0.9f) * new Color(1f, 1f, 1f, waiting ? 0.55f + 0.3f * breathe : 1f);
            pickZoneGlowImg.color = new Color(PickGold.r, PickGold.g, PickGold.b, 0.25f + 0.25f * breathe + 0.5f * pickHit);
            pickNeedleImg.color = Color.Lerp(Color.Lerp(Color.white, PickGold, pickHit), PickRed, pickMiss);

            // A pulse on the mark, a jolt for a miss; UNLOCKED swells as it goes.
            float shake = Mathf.Sin(now * 70f) * 10f * pickMiss * pickMiss;
            float done = pickDone > 0f ? 1f - pickDone / 1.1f : 0f;
            pickDial.anchoredPosition = new Vector2(shake, 10f);
            pickDial.localScale = Vector3.one * (1f + 0.07f * pickHit * pickHit + 0.12f * done);
            pick.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, Mathf.SmoothStep(0f, 1f, pickK));
            pickHub.color = Color.Lerp(Color.white, PickGold, pickHit + (pickDone > 0f ? 1f : 0f));

            int passed = pickDone > 0f ? Balance.Graveyard.PickChecks : me.PickStage;
            if (me.PickStage != pickLastStage) pickLastStage = me.PickStage;
            for (int i = 0; i < pickPins.Length; i++)
            {
                bool set = i < passed;
                bool current = i == passed && picking;
                Color c = set ? PickGold : current ? Color.Lerp(PickIron, Color.white, 0.25f + 0.35f * breathe) : new Color(0.3f, 0.29f, 0.28f);
                // Pins dropping: they fall away red as it starts over.
                if (pickReset > 0f && !set) c = Color.Lerp(c, PickRed, pickReset);
                pickPins[i].color = c;
                float drop = pickReset > 0f ? -14f * pickReset * Mathf.Sin(pickReset * Mathf.PI) : 0f;
                pickPins[i].rectTransform.anchoredPosition = new Vector2(PinX(i), (set ? 4f : 0f) + drop);
            }
            for (int i = 0; i < pickLamps.Length; i++)
            {
                bool lit = i < me.PickMiss || (pickReset > 0.5f);
                pickLamps[i].color = lit ? Color.Lerp(PickRed, Color.white, pickMiss * 0.4f) : new Color(0.22f, 0.12f, 0.11f);
            }
            if (pickDone > 0f)
            {
                pickStatus.text = "UNLOCKED";
                pickStatus.color = PickGold;
            }
            else if (pickReset > 0f)
            {
                pickStatus.text = "START OVER";
                pickStatus.color = new Color(PickRed.r, PickRed.g, PickRed.b, Mathf.Clamp01(pickReset * 2f));
            }
            else if (pickMiss > 0f)
            {
                pickStatus.text = "MISSED";
                pickStatus.color = new Color(PickRed.r, PickRed.g, PickRed.b, pickMiss);
            }
            else pickStatus.text = "";
            pickTitle.text = pickDone > 0f ? "THE LOCK GIVES" : "PICK THE LOCK";
        }
    }
}
