using UnityEngine;
using UnityEngine.UI;
using Vision.Game;
using Vision.UI;

namespace Vision.Player
{
    /// <summary>
    /// A ghost's HUD, in place of the inventory: GHOST over "find a body, help your team" (and how to take one), the body
    /// rising as they take it, REANIMATED over what they can and can't do in it, and once knocked out of one, what they
    /// see now. And the banner on everyone's screen when a body walks again.
    /// </summary>
    public sealed partial class GameHud
    {
        RectTransform ghostPanel, ghostFill;
        Text ghostTitle, ghostLabel, ghostHint;

        /// <summary>Whether the ghost's panel is up (captures and tests), and what it says.</summary>
        public bool GhostPanelShowing => ghostPanel != null && ghostPanel.gameObject.activeSelf;
        public string GhostPanelText => ghostLabel != null ? ghostTitle.text + "\n" + ghostLabel.text + "\n" + ghostHint.text : null;

        void BuildGhost(Transform parent)
        {
            ghostPanel = Node("Ghost", (RectTransform)parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(680f, 92f));
            HudPanel(ghostPanel);
            ghostTitle = kit.Label(Node("Title", ghostPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(600f, 28f)),
                "GHOST", 24, TextAnchor.MiddleCenter, new Color(0.88f, 0.94f, 1f), UiKit.Face.DisplayBold, 0.3f, false);
            ghostLabel = kit.Label(Node("Line", ghostPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(600f, 24f)),
                "", 17, TextAnchor.MiddleCenter, UiKit.Bone, UiKit.Face.Display, 0.04f, false);
            ghostHint = kit.Label(Node("Hint", ghostPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(600f, 20f)),
                "", 13, TextAnchor.MiddleCenter, UiKit.Hex(0x9c9b91), UiKit.Face.Mono, 0.06f, false);
            RectTransform track = Node("Track", ghostPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(560f, 4f));
            UiKit.Box(track, new Color(1f, 1f, 1f, 0.08f));
            ghostFill = UiKit.Fill("Fill", track);
            UiKit.Box(ghostFill, new Color(0.82f, 0.9f, 1f, 0.9f));
            ghostPanel.gameObject.SetActive(false);
        }

        void RefreshGhost(SimPlayer me)
        {
            if (ghostPanel == null) return;
            bool show = me != null && me.Ghost && !MainMenuOpen && !map.FullOpen;
            if (ghostPanel.gameObject.activeSelf != show) ghostPanel.gameObject.SetActive(show);
            if (!show) return;
            float fill = 0f;
            if (me.Body != 0 && me.PossessT > 0f)
            {
                ghostTitle.text = "TAKING THE BODY";
                ghostLabel.text = string.IsNullOrEmpty(me.BodyName) ? "it rises..." : $"{me.BodyName} rises...";
                ghostHint.text = "";
                fill = 1f - me.PossessT / Balance.Ghost.PossessTime;
            }
            else if (me.Body != 0)
            {
                ghostTitle.text = "REANIMATED";
                ghostLabel.text = "you cannot use items or fight, but you can help your team repair generators";
                ghostHint.text = "HOLD E TO HELP A SURVIVOR'S REPAIR OR GATE  ·  ZACH'S BLOWS KNOCK YOU OUT";
            }
            else if (me.Spent)
            {
                ghostTitle.text = "GHOST";
                ghostLabel.text = "knocked out of your body: you can't take another";
                ghostHint.text = "YOU SEE EVERYTHING NOW  ·  THE WHEEL ZOOMS";
            }
            else
            {
                ghostTitle.text = "GHOST";
                ghostLabel.text = "find a body, help your team";
                ghostHint.text = "PRESS E BY A BODY TO POSSESS IT  ·  YOU PASS THROUGH WALLS";
            }
            ghostFill.anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);
        }

        /// <summary>A line across the upper middle of every screen (whose body walks again), as a place's name is shown.</summary>
        public void Banner(string text)
        {
            if (place == null) return;
            placeText.text = text;
            placeText.fontSize = 26;
            placeT = 0f;
            bannerUp = true;
            place.gameObject.SetActive(true);
            placeGroup.alpha = 0f;
        }

        /// <summary>The banner's text while it's up (tests and captures).</summary>
        public string BannerText => place != null && place.gameObject.activeSelf ? placeText.text : null;
    }
}
