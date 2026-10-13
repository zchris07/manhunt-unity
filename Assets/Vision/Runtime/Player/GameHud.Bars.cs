using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vision.Game;
using Vision.UI;

namespace Vision.Player
{
    /// <summary>
    /// Health over heads, after the original's (client EntityLayer HealthBar): over every player you can see (yourself
    /// included), a 5-unit-tall bar 38 units wide (46 for Zach) with an ink border, a pale trail draining behind fresh damage,
    /// ticks at the thirds, and a green fill; a survivor with shield has a thin blue shield bar above it, ticked at the
    /// quarters, gone again once the shield is used up. A player's bars are opaque for five seconds after their health or shield last
    /// changed, then fade to 35%. Over the NPCs the viewer's role can slay, the same health bar, always opaque. Sized from
    /// the world, so they keep their size against the models as the camera zooms.
    /// </summary>
    public sealed partial class GameHud
    {
        sealed class HeadBar
        {
            public RectTransform Root, Health, Fill, Trail, Shield, ShieldFill;
            public Image FillImage;
            public CanvasGroup Group;
            public float TrailK = 1f, LastHp = -1f, LastShield = -1f, ChangedAt = -99f;
            public int Owner;
        }

        static readonly Color BarInk = new Color(0f, 0f, 0f, 0.8f), BarBack = UiKit.Hex(0x241a18), BarTrail = new Color(0xe8 / 255f, 0xd8 / 255f, 0xb0 / 255f, 0.85f);
        static readonly Color BarHealth = UiKit.Hex(0x4cbf4a), BarShield = UiKit.Hex(0x3a8cff);
        /// <summary>Seconds a player's bars stay opaque after a change, and how faint they fade.</summary>
        const float BarHoldSec = 5f, BarFaded = 0.35f;
        /// <summary>The original's bars are sized to its disc of a body; against these slimmer models they're drawn at 60%.</summary>
        const float BarScale = 0.6f;

        RectTransform headBarRoot;
        readonly List<HeadBar> headBars = new List<HeadBar>();

        void BuildHeadBars(Transform parent)
        {
            headBarRoot = UiKit.Fill("Head Bars", parent);
            headBarRoot.SetAsFirstSibling();
        }

        HeadBar MakeHeadBar(RectTransform parent)
        {
            var b = new HeadBar();
            b.Root = Node("Bars", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(46f, 14f));
            b.Group = b.Root.gameObject.AddComponent<CanvasGroup>();
            b.Group.blocksRaycasts = false;
            b.Group.interactable = false;
            b.Health = Track(b.Root, "Health", 0f, 3, out b.Fill, out b.Trail, out b.FillImage);
            b.Shield = Track(b.Root, "Shield", 1f, 4, out b.ShieldFill, out _, out Image shieldImage);
            shieldImage.color = BarShield;
            return b;
        }

        /// <summary>One bar of a head bar: an ink border round a dark track, a pale trail, the fill, and tick marks.</summary>
        RectTransform Track(RectTransform parent, string name, float anchorY, int ticks, out RectTransform fill, out RectTransform trail, out Image fillImage)
        {
            RectTransform border = Node(name, parent, new Vector2(0.5f, anchorY), new Vector2(0.5f, anchorY), Vector2.zero, new Vector2(48f, 7f));
            UiKit.Box(border, BarInk);
            RectTransform back = UiKit.Fill("Track", border, 1f);
            UiKit.Box(back, BarBack);
            trail = UiKit.Fill("Trail", back);
            UiKit.Box(trail, BarTrail);
            fill = UiKit.Fill("Fill", back);
            fillImage = UiKit.Box(fill, BarHealth);
            for (int i = 1; i < ticks; i++)
            {
                RectTransform tick = Node("Tick", back, new Vector2(i / (float)ticks, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1f, 0f));
                tick.anchorMax = new Vector2(i / (float)ticks, 1f);
                UiKit.Box(tick, new Color(0f, 0f, 0f, 0.55f));
            }
            return border;
        }

        /// <summary>Places and sizes a head bar over a world point; false if it's off screen.</summary>
        bool PlaceBar(HeadBar b, RectTransform parent, Camera cam, Vector3 worldAt, float widthUnits, bool shield)
        {
            Vector3 sp = cam.WorldToScreenPoint(worldAt);
            if (sp.z < 0f || sp.x < -100f || sp.y < -100f || sp.x > Screen.width + 100f || sp.y > Screen.height + 100f) return false;
            float s = world.transform.lossyScale.x;
            Vector3 sp2 = cam.WorldToScreenPoint(worldAt + cam.transform.right * Scale.D(widthUnits) * s * BarScale);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, sp, null, out Vector2 a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, sp2, null, out Vector2 c);
            float w = Mathf.Max(24f, Vector2.Distance(a, c)), unit = w / widthUnits;
            float h = Mathf.Max(4f, 5f * unit), hs = Mathf.Max(3f, 3f * unit), gap = Mathf.Max(2f, 3f * unit);
            b.Root.anchoredPosition = a;
            b.Health.sizeDelta = new Vector2(w + 2f, h + 2f);
            b.Health.anchoredPosition = Vector2.zero;
            b.Shield.gameObject.SetActive(shield);
            b.Shield.sizeDelta = new Vector2(w + 2f, hs + 2f);
            b.Shield.anchorMin = b.Shield.anchorMax = new Vector2(0.5f, 0f);
            b.Shield.pivot = new Vector2(0.5f, 0f);
            b.Shield.anchoredPosition = new Vector2(0f, h + 2f + gap);
            b.Root.sizeDelta = new Vector2(w + 2f, h + 2f + (shield ? gap + hs + 2f : 0f));
            return true;
        }

        static void SetBar(HeadBar b, float hp, float shield, float dt)
        {
            hp = Mathf.Clamp01(hp);
            // The trail drains toward fresh damage at 0.6 a second, as the original's.
            b.TrailK = hp < b.TrailK ? Mathf.Max(hp, b.TrailK - 0.6f * dt) : hp;
            b.Fill.anchorMax = new Vector2(hp, 1f);
            b.Trail.anchorMax = new Vector2(b.TrailK, 1f);
            b.ShieldFill.anchorMax = new Vector2(Mathf.Clamp01(shield), 1f);
        }

        void RefreshHeadBars(SimPlayer me)
        {
            MatchSim sim = host != null ? host.Sim : null;
            Camera cam = Camera.main;
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            int used = 0;
            // Gone dark past the edge of the woods: nothing shows over heads either.
            if (sim != null && cam != null && world != null && !MainMenuOpen && !map.FullOpen && Vision.Effects.SensesCamera.Blackout < 0.9f)
            {
                var puppets = world.GetComponent<PlayerPuppets>();
                foreach (SimPlayer p in sim.Order)
                {
                    if (p.Role != Role.Survivor && p.Role != Role.Hunter) continue;
                    bool zach = p.Role == Role.Hunter;
                    if (p.Health == Game.Health.Escaped || p.Health == Game.Health.Eliminated || p.HideState == 2) continue;
                    // As the original: not while staked or carried (survivors), nor while Zach is down.
                    if (!zach && (p.Health == Game.Health.Staked || p.Health == Game.Health.Carried)) continue;
                    if (zach && p.KnockT > 0f) continue;
                    if (p.Id != me.Id)
                    {
                        PlayerPuppets.Puppet pup = puppets != null ? puppets.For(p.Id) : null;
                        if (pup == null || !pup.Visible) continue;
                    }
                    HeadBar b = BarFor(p.Id, ref used);
                    float top = zach ? 2.5f : 2.05f;
                    Vector3 wp = world.transform.TransformPoint(new Vector3(p.Pos.x, world.GroundHeight(p.Pos) + top, p.Pos.y));
                    // The shield bar only while there is shield.
                    if (!PlaceBar(b, headBarRoot, cam, wp, zach ? 46f : 38f, !zach && p.Shield > 1e-4f)) { used--; continue; }
                    b.Root.gameObject.SetActive(true);
                    // Opaque while health or shield changes, fading to 35% five seconds after the last change.
                    if (b.LastHp < 0f || Mathf.Abs(p.Hp - b.LastHp) > 1e-4f || Mathf.Abs(p.Shield - b.LastShield) > 1e-4f)
                    {
                        if (b.LastHp >= 0f) b.ChangedAt = now;
                        b.LastHp = p.Hp;
                        b.LastShield = p.Shield;
                    }
                    float target = now - b.ChangedAt < BarHoldSec ? 1f : BarFaded;
                    b.Group.alpha = Mathf.MoveTowards(b.Group.alpha, target, dt * 3f);
                    SetBar(b, p.Hp, p.Shield, dt);
                }
            }
            for (int i = used; i < headBars.Count; i++) headBars[i].Root.gameObject.SetActive(false);
        }

        /// <summary>The bar slot for a player (kept per player while in use, so its trail and fade carry on).</summary>
        HeadBar BarFor(int owner, ref int used)
        {
            for (int i = used; i < headBars.Count; i++)
                if (headBars[i].Owner == owner)
                {
                    (headBars[used], headBars[i]) = (headBars[i], headBars[used]);
                    return headBars[used++];
                }
            HeadBar b = used < headBars.Count ? headBars[used] : null;
            if (b == null)
            {
                b = MakeHeadBar(headBarRoot);
                b.Group.alpha = BarFaded;
                headBars.Add(b);
            }
            if (b.Owner != owner)
            {
                b.Owner = owner;
                b.LastHp = -1f;
                b.TrailK = 1f;
                b.ChangedAt = -99f;
                b.Group.alpha = BarFaded;
            }
            used++;
            return b;
        }
    }
}
