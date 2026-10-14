using UnityEngine;
using UnityEngine.UI;
using Vision.Game;
using Vision.UI;

namespace Vision.Player
{
    /// <summary>
    /// A named place's banner (Miga Sushi, the Diddy Chamber; <see cref="World.SandboxWorld.Places"/>): walking in, its name
    /// fades up in the upper middle of the screen, letter-spaced between two thin rules, holds a moment and fades away.
    /// Only on the way in (walking about inside doesn't bring it back).
    /// </summary>
    public sealed partial class GameHud
    {
        RectTransform place;
        CanvasGroup placeGroup;
        Text placeText;
        string placeIn;
        float placeT = -1f;

        const float PlaceIn = 0.5f, PlaceHold = 2.2f, PlaceOut = 0.9f;

        void BuildPlaceBanner(Transform parent)
        {
            place = Node("Place", (RectTransform)parent, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 90f));
            placeGroup = place.gameObject.AddComponent<CanvasGroup>();
            placeGroup.blocksRaycasts = false;
            placeGroup.interactable = false;
            placeText = kit.Label(Node("Name", place, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 60f)),
                "", 46, TextAnchor.MiddleCenter, new Color(0.96f, 0.9f, 0.8f), UiKit.Face.DisplayBold, 0.3f);
            placeText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.75f);
            foreach (float y in new[] { 38f, -38f })
            {
                RectTransform rule = Node("Rule", place, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(420f, 2f));
                UiKit.Box(rule, new Color(0.96f, 0.9f, 0.8f, 0.7f));
            }
            place.gameObject.SetActive(false);
        }

        void RefreshPlaceBanner(SimPlayer me)
        {
            if (place == null) return;
            string inside = null;
            if (me != null && world != null && !MainMenuOpen)
                foreach (var (name, area) in world.Places)
                    if (area.Contains(me.Pos)) { inside = name; break; }
            if (inside != null && inside != placeIn)
            {
                placeText.text = inside;
                placeT = 0f;
            }
            placeIn = inside;
            if (placeT < 0f) return;
            placeT += Time.unscaledDeltaTime;
            float a = placeT < PlaceIn ? placeT / PlaceIn : placeT < PlaceIn + PlaceHold ? 1f : 1f - (placeT - PlaceIn - PlaceHold) / PlaceOut;
            if (a <= 0f && placeT > PlaceIn)
            {
                placeT = -1f;
                place.gameObject.SetActive(false);
                return;
            }
            place.gameObject.SetActive(true);
            placeGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(a));
            place.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, Mathf.Clamp01(placeT / PlaceIn));
        }
    }
}
