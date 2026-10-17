using UnityEngine;
using UnityEngine.UI;
using Vision.Game;
using Vision.UI;

namespace Vision.Player
{
    /// <summary>
    /// A ghost's HUD: in place of the inventory, GHOST over their one power, the Ghost Pulse, with its key (left click or F)
    /// and its cooldown filling back up.
    /// </summary>
    public sealed partial class GameHud
    {
        RectTransform ghostPanel, ghostFill;
        Text ghostLabel;

        /// <summary>Whether the ghost's panel is up (captures and tests), and what it says.</summary>
        public bool GhostPanelShowing => ghostPanel != null && ghostPanel.gameObject.activeSelf;
        public string GhostPanelText => ghostLabel != null ? ghostLabel.text : null;

        void BuildGhost(Transform parent)
        {
            ghostPanel = Node("Ghost", (RectTransform)parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(460f, 74f));
            HudPanel(ghostPanel);
            kit.Label(Node("Title", ghostPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(440f, 28f)),
                "GHOST", 24, TextAnchor.MiddleCenter, new Color(0.88f, 0.94f, 1f), UiKit.Face.DisplayBold, 0.3f, false);
            ghostLabel = kit.Label(Node("Pulse", ghostPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(440f, 22f)),
                "", 15, TextAnchor.MiddleCenter, UiKit.Bone, UiKit.Face.Mono, 0.06f, false);
            RectTransform track = Node("Track", ghostPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(400f, 4f));
            UiKit.Box(track, new Color(1f, 1f, 1f, 0.08f));
            ghostFill = UiKit.Fill("Fill", track);
            UiKit.Box(ghostFill, new Color(0.9f, 0.2f, 0.18f, 0.9f));
            ghostPanel.gameObject.SetActive(false);
        }

        void RefreshGhost(SimPlayer me)
        {
            if (ghostPanel == null) return;
            bool show = me != null && me.Ghost && !MainMenuOpen && !map.FullOpen;
            if (ghostPanel.gameObject.activeSelf != show) ghostPanel.gameObject.SetActive(show);
            if (!show) return;
            bool ready = me.GhostCd <= 0f;
            ghostLabel.text = ready ? "LEFT CLICK / F  ·  GHOST PULSE READY" : $"GHOST PULSE  ·  {Mathf.CeilToInt(me.GhostCd)}s";
            ghostFill.anchorMax = new Vector2(ready ? 1f : 1f - Mathf.Clamp01(me.GhostCd / Balance.Ghost.Cooldown), 1f);
        }
    }
}
