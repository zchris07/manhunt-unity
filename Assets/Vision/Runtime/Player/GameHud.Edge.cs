using UnityEngine;
using UnityEngine.UI;
using Vision.Game;
using Vision.UI;

namespace Vision.Player
{
    /// <summary>
    /// The warning at the edge of the woods: a survivor past the first ring sees DON'T WANDER OFF in the upper middle of the
    /// screen and, under it, a big arrow pointing (on screen, whichever way the view is turned) the way back inside the
    /// playable circle, both pulsing gently, until they're back in.
    /// </summary>
    public sealed partial class GameHud
    {
        RectTransform wander, wanderArrow;
        CanvasGroup wanderGroup;
        float wanderK;

        void BuildWander(Transform parent)
        {
            wander = Node("Wander", (RectTransform)parent, new Vector2(0.5f, 0.84f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 190f));
            wanderGroup = wander.gameObject.AddComponent<CanvasGroup>();
            wanderGroup.blocksRaycasts = false;
            wanderGroup.interactable = false;
            RectTransform label = Node("Text", wander, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(900f, 56f));
            Text t = kit.Label(label, "DON'T WANDER OFF", 44, TextAnchor.MiddleCenter, new Color(0.95f, 0.88f, 0.78f), UiKit.Face.DisplayBold, 0.12f);
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            wanderArrow = Node("Arrow", wander, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 62f), new Vector2(96f, 96f));
            var img = wanderArrow.gameObject.AddComponent<Image>();
            img.sprite = ArrowSprite();
            img.color = new Color(0.95f, 0.88f, 0.78f);
            img.raycastTarget = false;
            wanderArrow.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            wander.gameObject.SetActive(false);
        }

        /// <summary>A bold arrow pointing up: a head and a shaft.</summary>
        static Sprite ArrowSprite()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n;
                    // The head: a triangle over the top 45%; the shaft below it.
                    bool head = v > 0.55f && Mathf.Abs(u) < (1f - v) / 0.45f * 0.42f;
                    bool shaft = v >= 0.06f && v <= 0.58f && Mathf.Abs(u) < 0.13f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, head || shaft ? 1f : 0f));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        void RefreshWander(SimPlayer me)
        {
            if (wander == null) return;
            // Only once the light is dying (past the second ring); between the rings there's no warning, only the flicker.
            bool show = me != null && me.Role == Role.Survivor && me.Zone >= 2 && !MainMenuOpen
                && (me.Health == Game.Health.Healthy || me.Health == Game.Health.Wounded || me.Health == Game.Health.Downed);
            wanderK = Mathf.MoveTowards(wanderK, show ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            wander.gameObject.SetActive(wanderK > 0f);
            if (wanderK <= 0f) return;
            wanderGroup.alpha = wanderK * (0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 5f));
            Camera cam = Camera.main;
            if (cam == null || world == null || me.Pos.sqrMagnitude < 1e-6f) return;
            // The way back in is toward the map's centre: that direction on screen, however the view is turned.
            Vector2 inward = -me.Pos.normalized;
            Vector3 a = world.transform.TransformPoint(new Vector3(me.Pos.x, world.GroundHeight(me.Pos), me.Pos.y));
            Vector2 to = me.Pos + inward * 4f;
            Vector3 b = world.transform.TransformPoint(new Vector3(to.x, world.GroundHeight(to), to.y));
            Vector3 sa = cam.WorldToScreenPoint(a), sb = cam.WorldToScreenPoint(b);
            Vector2 d = new Vector2(sb.x - sa.x, sb.y - sa.y);
            if (d.sqrMagnitude < 1e-4f) return;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
            wanderArrow.localRotation = Quaternion.Euler(0f, 0f, ang);
            wanderArrow.anchoredPosition = new Vector2(0f, 62f) + d.normalized * (6f * Mathf.Sin(Time.unscaledTime * 6f));
        }
    }
}
