using UnityEngine;
using UnityEngine.UI;
using Vision.Game;
using Vision.UI;

namespace Vision.Player
{
    /// <summary>
    /// A generator's skill check: while repairing, a small dial flashes up in the middle of the screen, a gilded mark on its
    /// ring and a fast tick sweeping round it once. Space or a click on the mark takes four seconds off the repair
    /// (+4 s flashes in gold); a miss, or letting the tick get all the way round, costs nothing. The sim judges the press
    /// (<see cref="MatchSim"/>'s CheckStrike); this only shows it, in the lock pick's style.
    /// </summary>
    public sealed partial class GameHud
    {
        RectTransform chk, chkDial, chkNeedle, chkZone, chkZoneGlow;
        CanvasGroup chkGroup;
        Image chkNeedleImg, chkZoneImg, chkZoneGlowImg, chkHub;
        Text chkStatus, chkHint;
        float chkK, chkHit, chkMiss, chkLastResultT = -1f, chkLastT = float.NaN, chkStamp;

        void BuildSkillCheck(Transform parent)
        {
            chk = Node("Skill Check", (RectTransform)parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(300f, 330f));
            chkGroup = chk.gameObject.AddComponent<CanvasGroup>();
            chkGroup.blocksRaycasts = false;
            chkGroup.interactable = false;
            PickImage("Shade", chk, Drawn(128, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                return new Color(0f, 0f, 0f, 0.65f * Mathf.Clamp01(1f - Mathf.SmoothStep(0.4f, 1f, r)));
            }), new Vector2(360f, 360f), Color.white);
            chkDial = Node("Dial", chk, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(210f, 210f));
            PickImage("Face", chkDial, Drawn(192, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                if (r > 1f) return Color.clear;
                float shade = Mathf.Lerp(0.15f, 0.08f, r) + 0.05f * Mathf.Clamp01(v * 0.5f + 0.5f);
                float rim = Band(r, 0.9f, 1f, 0.02f) * (0.18f + 0.14f * v);
                float c = shade + rim;
                return new Color(c, c * 0.97f, c * 0.93f, Mathf.Clamp01((1f - r) / 0.02f));
            }), new Vector2(210f, 210f), Color.white);
            Sprite arc = Drawn(192, (u, v) => new Color(1f, 1f, 1f, Band(Mathf.Sqrt(u * u + v * v), 0.66f, 0.9f, 0.02f)));
            Sprite glow = Drawn(96, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(1f - Mathf.Abs(r - 0.78f) / 0.2f) * 0.8f);
            });
            chkZoneGlowImg = PickImage("Mark Glow", chkDial, glow, new Vector2(228f, 228f), new Color(PickGold.r, PickGold.g, PickGold.b, 0.4f));
            chkZoneImg = PickImage("Mark", chkDial, arc, new Vector2(210f, 210f), PickGold);
            foreach (Image im in new[] { chkZoneGlowImg, chkZoneImg })
            {
                im.type = Image.Type.Filled;
                im.fillMethod = Image.FillMethod.Radial360;
                im.fillOrigin = (int)Image.Origin360.Top;
                im.fillClockwise = true;
            }
            chkZone = chkZoneImg.rectTransform;
            chkZoneGlow = chkZoneGlowImg.rectTransform;
            chkNeedle = Node("Needle", chkDial, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210f, 210f));
            chkNeedleImg = PickImage("Blade", chkNeedle, Drawn(192, (u, v) =>
            {
                if (v < 0.1f || v > 0.96f) return Color.clear;
                float w = Mathf.Lerp(0.05f, 0.02f, (v - 0.1f) / 0.86f);
                float a = Mathf.Clamp01((w - Mathf.Abs(u)) / 0.012f);
                return new Color(1f, 1f, 1f, a);
            }), new Vector2(210f, 210f), Color.white);
            chkHub = PickImage("Hub", chkDial, Drawn(64, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float c = Mathf.Lerp(0.42f, 0.22f, r) + 0.1f * v;
                return new Color(c * 1.05f, c * 0.95f, c * 0.7f, Mathf.Clamp01((1f - r) / 0.06f));
            }), new Vector2(40f, 40f), Color.white);
            chkStatus = kit.Label(Node("Status", chk, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -105f), new Vector2(300f, 40f)),
                "", 30, TextAnchor.MiddleCenter, PickGold, UiKit.Face.DisplayBold, 0.12f);
            chkStatus.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            chkHint = kit.Label(Node("Hint", chk, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(300f, 28f)),
                "SPACE  /  CLICK", 16, TextAnchor.MiddleCenter, new Color(0.95f, 0.88f, 0.76f), UiKit.Face.Mono, 0.1f);
            chk.gameObject.SetActive(false);
        }

        void RefreshSkillCheck(SimPlayer me, MatchSim sim)
        {
            if (chk == null) return;
            bool up = me != null && sim != null && me.Action == ActionKind.Repair && me.CheckOn && !MainMenuOpen;
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            if (me != null && me.CheckResultT != chkLastResultT)
            {
                // Each press (or a sweep that ran out) as the sim judged it, shown while they're still repairing.
                if (chkLastResultT >= 0f && me.Action == ActionKind.Repair)
                {
                    if (me.CheckResult == 1) chkHit = 1f;
                    else chkMiss = 1f;
                }
                chkLastResultT = me.CheckResultT;
            }
            chkHit = Mathf.Max(0f, chkHit - dt * 1.6f);
            chkMiss = Mathf.Max(0f, chkMiss - dt * 2.2f);
            bool show = up || chkHit > 0.3f || chkMiss > 0.4f;
            chkK = Mathf.MoveTowards(chkK, show ? 1f : 0f, dt * (show ? 10f : 6f));
            chk.gameObject.SetActive(chkK > 0f);
            if (chkK <= 0f || me == null) return;
            chkGroup.alpha = chkK;

            if (me.CheckT != chkLastT)
            {
                chkLastT = me.CheckT;
                chkStamp = now;
            }
            float t = me.CheckT + Mathf.Min(0.1f, now - chkStamp);
            float needle = up && t > 0f ? Mathf.Repeat(Balance.SkillCheck.Speed * t * me.CheckDir, 360f) : 0f;
            chkNeedle.localRotation = Quaternion.Euler(0f, 0f, -needle);
            float w = Balance.SkillCheck.Width;
            chkZoneImg.fillAmount = w / 360f;
            chkZone.localRotation = Quaternion.Euler(0f, 0f, -(me.CheckZone - w * 0.5f));
            chkZoneGlowImg.fillAmount = (w + 12f) / 360f;
            chkZoneGlow.localRotation = Quaternion.Euler(0f, 0f, -(me.CheckZone - (w + 12f) * 0.5f));
            bool waiting = up && t <= 0f;
            float breathe = 0.5f + 0.5f * Mathf.Sin(now * 14f);
            chkZoneImg.color = Color.Lerp(PickGold, Color.white, chkHit) * new Color(1f, 1f, 1f, waiting ? 0.5f + 0.4f * breathe : 1f);
            chkZoneGlowImg.color = new Color(PickGold.r, PickGold.g, PickGold.b, 0.3f + 0.3f * breathe + 0.4f * chkHit);
            chkNeedleImg.color = Color.Lerp(Color.Lerp(Color.white, PickGold, chkHit), PickRed, chkMiss);
            chkHub.color = Color.Lerp(Color.white, PickGold, chkHit);
            chkDial.localScale = Vector3.one * (1f + 0.12f * chkHit * chkHit);
            chkDial.anchoredPosition = new Vector2(Mathf.Sin(now * 70f) * 7f * chkMiss * chkMiss, 14f);
            chk.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, Mathf.SmoothStep(0f, 1f, chkK));
            if (chkHit > 0f)
            {
                chkStatus.text = $"-{Balance.SkillCheck.Bonus:0} s";
                chkStatus.color = new Color(PickGold.r, PickGold.g, PickGold.b, Mathf.Clamp01(chkHit * 2f));
            }
            else if (chkMiss > 0f)
            {
                chkStatus.text = "MISSED";
                chkStatus.color = new Color(PickRed.r, PickRed.g, PickRed.b, Mathf.Clamp01(chkMiss * 2f));
            }
            else chkStatus.text = "";
            chkHint.enabled = up;
        }
    }
}
