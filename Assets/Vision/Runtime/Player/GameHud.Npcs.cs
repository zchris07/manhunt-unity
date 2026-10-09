using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vision.Game;
using Vision.UI;

namespace Vision.Player
{
    /// <summary>
    /// Over the NPCs you can see: their name, what they're saying (a speech bubble), and how close Shane or Jaden is to
    /// being set off (an alert bar that turns red, then a red "!" once they're after someone).
    /// </summary>
    public sealed partial class GameHud
    {
        sealed class NpcTag
        {
            public RectTransform Root, Bubble, AlertBack, AlertFill;
            public Text Name, Say, Bang;
        }

        RectTransform npcTagRoot;
        readonly List<NpcTag> npcTags = new List<NpcTag>();

        void BuildNpcTags(Transform parent)
        {
            npcTagRoot = UiKit.Fill("NPC Tags", parent);
            npcTagRoot.SetAsFirstSibling();
        }

        NpcTag MakeTag()
        {
            var t = new NpcTag();
            t.Root = Node("Tag", npcTagRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(260f, 20f));
            t.Name = kit.Label(Node("Name", t.Root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(260f, 18f)), "", 14, TextAnchor.LowerCenter, UiKit.Bone, UiKit.Face.Type, 0.06f);
            t.AlertBack = Node("Alert", t.Root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(60f, 5f));
            UiKit.Box(t.AlertBack, new Color(0f, 0f, 0f, 0.7f));
            t.AlertFill = UiKit.Fill("Fill", t.AlertBack);
            UiKit.Box(t.AlertFill, UiKit.Yellow);
            t.Bang = kit.Label(Node("Bang", t.Root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(40f, 30f)), "!", 28, TextAnchor.LowerCenter, UiKit.Red, UiKit.Face.DisplayBold);
            t.Bubble = Node("Bubble", t.Root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 30f));
            UiKit.Box(t.Bubble, new Color(0.94f, 0.92f, 0.86f, 0.94f));
            t.Say = kit.Label(UiKit.Fill("Text", t.Bubble, 6f), "", 15, TextAnchor.MiddleCenter, UiKit.Hex(0x14140f), UiKit.Face.Mono, 0f, false);
            t.Say.horizontalOverflow = HorizontalWrapMode.Wrap;
            npcTags.Add(t);
            return t;
        }

        /// <summary>Roughly whether the local player can see a point: close by, or in their light with nothing in the way.</summary>
        static bool Sees(MatchSim sim, SimPlayer me, Vector2 at)
        {
            bool zach = me.Role == Role.Hunter;
            float d = Vector2.Distance(me.Pos, at);
            if (d < Scale.D(zach ? Balance.Hunter.Proximity : Balance.Survivor.Proximity) * 1.2f) return true;
            float reach = me.ViewReach > 0f ? Scale.D(me.ViewReach) : Scale.D(Balance.BeamRange);
            if (d > reach) return false;
            float half = (zach ? Balance.Hunter.ConeHalfAngleDeg : Balance.Survivor.ConeHalfAngleDeg);
            Vector2 dir = at - me.Pos;
            if (Mathf.Abs(Mathf.DeltaAngle(me.Facing * Mathf.Rad2Deg, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg)) > half + 6f) return false;
            return me.GogglesOn || (zach && me.HempOn) || sim.Geo.LineOfSight(me.Pos, at);
        }

        void RefreshNpcTags(SimPlayer me)
        {
            MatchSim sim = host != null ? host.Sim : null;
            Camera cam = Camera.main;
            int used = 0;
            if (sim != null && cam != null && world != null && !MainMenuOpen && !map.FullOpen)
            {
                bool testing = sim.TestMode;
                foreach (Npc n in sim.Npcs)
                {
                    if (n.Gone || !n.Alive) continue;
                    if (!testing && !Sees(sim, me, n.Pos)) continue;
                    float hgt = world.GroundHeight(n.Pos);
                    Vector3 wp = world.transform.TransformPoint(new Vector3(n.Pos.x, hgt + 2.05f, n.Pos.y));
                    Vector3 sp = cam.WorldToScreenPoint(wp);
                    if (sp.z < 0f || sp.x < -100f || sp.y < -100f || sp.x > Screen.width + 100f || sp.y > Screen.height + 100f) continue;
                    NpcTag t = used < npcTags.Count ? npcTags[used] : MakeTag();
                    used++;
                    t.Root.gameObject.SetActive(true);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(npcTagRoot, sp, null, out Vector2 local);
                    t.Root.anchoredPosition = local;
                    t.Name.text = n.Name;
                    float alert = n.AlertLevel;
                    bool chasing = (n.Flags & NpcFlags.Chasing) != 0;
                    t.AlertBack.gameObject.SetActive(alert > 0.01f && !chasing);
                    t.AlertFill.anchorMax = new Vector2(Mathf.Clamp01(alert), 1f);
                    t.AlertFill.GetComponent<Image>().color = Color.Lerp(UiKit.Yellow, UiKit.Red, alert);
                    t.Bang.gameObject.SetActive(chasing);
                    bool says = !string.IsNullOrEmpty(n.Say);
                    t.Bubble.gameObject.SetActive(says);
                    if (says)
                    {
                        t.Say.text = n.Say.Replace("NAME", me.Name);
                        float w = Mathf.Min(320f, t.Say.preferredWidth + 18f);
                        t.Bubble.sizeDelta = new Vector2(w, Mathf.Max(30f, t.Say.preferredHeight + 10f));
                        t.Bubble.anchoredPosition = new Vector2(0f, chasing ? 52f : 30f);
                    }
                }
            }
            for (int i = used; i < npcTags.Count; i++) npcTags[i].Root.gameObject.SetActive(false);
        }
    }
}
