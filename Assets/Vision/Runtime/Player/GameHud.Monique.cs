using UnityEngine;
using UnityEngine.UI;
using Vision.Game;
using Vision.UI;

namespace Vision.Player
{
    /// <summary>
    /// Monique's arrow: for its forty seconds a red arrow circles the survivor she gave it to, pointing (on screen, whichever
    /// way the view is turned) at Zach wherever he is, walls or no, with the seconds left under it; it fades in, pulses
    /// gently and fades out over its last two seconds.
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>How far round the survivor (canvas units) the arrow circles.</summary>
        public const float MoniqueArrowRadius = 120f;

        RectTransform moniqueArrow, moniqueArrowIcon;
        CanvasGroup moniqueGroup;
        Text moniqueLabel;
        float moniqueK;
        bool moniqueWanted;

        /// <summary>Whether Monique's arrow is up (fading in or showing; for captures and tests), and what it says.</summary>
        public bool MoniqueArrowShowing => moniqueWanted;
        public string MoniqueArrowText => moniqueLabel != null ? moniqueLabel.text : null;

        void BuildMoniqueArrow(RectTransform parent)
        {
            moniqueArrow = Node("Monique Arrow", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 110f));
            moniqueGroup = moniqueArrow.gameObject.AddComponent<CanvasGroup>();
            moniqueGroup.blocksRaycasts = false;
            moniqueGroup.interactable = false;
            moniqueArrowIcon = Node("Arrow", moniqueArrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(64f, 64f));
            var img = moniqueArrowIcon.gameObject.AddComponent<Image>();
            img.sprite = ArrowSprite();
            img.color = new Color(0.92f, 0.16f, 0.12f);
            img.raycastTarget = false;
            moniqueArrowIcon.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            moniqueLabel = kit.Label(Node("Label", moniqueArrow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(120f, 22f)),
                "", 15, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.8f), UiKit.Face.DisplayBold, 0.12f);
            moniqueLabel.raycastTarget = false;
            moniqueLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            moniqueArrow.gameObject.SetActive(false);
        }

        /// <summary>Zach, if he's in the match and on his feet or not (only his body gone counts as gone).</summary>
        static SimPlayer ZachIn(MatchSim sim)
        {
            if (sim == null) return null;
            foreach (SimPlayer p in sim.Order)
                if (p.Role == Role.Hunter && p.Health != Game.Health.Eliminated) return p;
            return null;
        }

        void RefreshMoniqueArrow(SimPlayer me)
        {
            if (moniqueArrow == null) return;
            MatchSim sim = host != null ? host.Sim : null;
            SimPlayer zach = ZachIn(sim);
            Camera cam = Camera.main;
            bool alive = me.Health == Game.Health.Healthy || me.Health == Game.Health.Wounded || me.Health == Game.Health.Downed;
            bool show = me.Role == Role.Survivor && alive && me.ArrowT > 0f && zach != null && cam != null && world != null
                && !MainMenuOpen && !map.FullOpen && (sim == null || sim.Result == null);
            moniqueWanted = show;
            if (show) moniqueLabel.text = "ZACH " + Mathf.CeilToInt(me.ArrowT) + "s";
            // In over a quarter second, out over the arrow's last two seconds.
            float target = show ? Mathf.Clamp01(me.ArrowT / 2f) : 0f;
            moniqueK = Mathf.MoveTowards(moniqueK, target, Time.unscaledDeltaTime * 4f);
            if (!show) moniqueK = Mathf.Min(moniqueK, target);
            moniqueArrow.gameObject.SetActive(moniqueK > 0f);
            if (moniqueK <= 0f) return;
            moniqueGroup.alpha = moniqueK * (0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f));

            RectTransform parent = (RectTransform)moniqueArrow.parent;
            Vector3 a = world.transform.TransformPoint(new Vector3(me.Pos.x, world.GroundHeight(me.Pos) + 1f, me.Pos.y));
            Vector3 b = world.transform.TransformPoint(new Vector3(zach.Pos.x, world.GroundHeight(zach.Pos) + 1f, zach.Pos.y));
            Vector3 sa = cam.WorldToScreenPoint(a), sb = cam.WorldToScreenPoint(b);
            if (sa.z < 0f) return;
            // Zach behind the camera: flip, so the arrow still points his way across the ground.
            Vector2 d = new Vector2(sb.x - sa.x, sb.y - sa.y);
            if (sb.z < 0f) d = -d;
            if (d.sqrMagnitude < 1e-4f) d = Vector2.up;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, sa, null, out Vector2 at);
            Vector2 dir = d.normalized;
            float bob = 5f * Mathf.Sin(Time.unscaledTime * 6f);
            // (The node is anchored at the parent's centre; the local point is from its pivot.)
            moniqueArrow.anchoredPosition = at - parent.rect.center + dir * (MoniqueArrowRadius + bob);
            moniqueArrowIcon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        }
    }
}
