using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vision.Game;
using Vision.UI;

namespace Vision.Player
{
    /// <summary>
    /// The end of the night (the original's results screen): who won and why, how long it took, the generators, and each
    /// player's numbers. Also the match clock under the compass and the pulsing red rings over staked teammates.
    /// </summary>
    public sealed partial class GameHud
    {
        static readonly string[] Outcomes = { "escaped", "Escaped", "eliminated", "Sacrificed", "survived", "Survived", "hunter", "Zach" };

        GameObject results;
        Text resultsBanner, resultsLine, clock;
        RectTransform resultsTable;
        MatchResult shownResult;
        readonly List<RectTransform> auras = new List<RectTransform>();
        RectTransform auraRoot;
        static Texture2D ringTex;

        public bool ResultsShowing => results != null && results.activeSelf;

        void BuildResults(Transform parent)
        {
            RectTransform clockRt = Node("Clock", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(220f, 22f));
            clock = kit.Label(clockRt, "", 15, TextAnchor.MiddleCenter, UiKit.Muted, UiKit.Face.Display, 0.14f);

            auraRoot = UiKit.Fill("Stake Auras", parent);
            auraRoot.SetAsFirstSibling();

            RectTransform shade = UiKit.Fill("Results", parent);
            UiKit.Box(shade, new Color(4f / 255f, 4f / 255f, 3f / 255f, 0.8f), true);
            const float w = 1040f, h = 620f;
            RectTransform panel = kit.Card("Panel", shade, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            resultsBanner = kit.Label(Node("Banner", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(w - 60f, 80f)), "", 64, TextAnchor.MiddleCenter, UiKit.Bone, UiKit.Face.DisplayBold, 0.12f, false);
            var red = resultsBanner.gameObject.AddComponent<Shadow>();
            red.effectColor = UiKit.A(UiKit.Red, 0.8f);
            red.effectDistance = new Vector2(3f, 0f);
            resultsLine = kit.Label(Node("Line", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(w - 60f, 26f)), "", 17, TextAnchor.MiddleCenter, UiKit.Muted, UiKit.Face.Mono, 0f, false);
            resultsTable = Node("Table", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -160f), new Vector2(w - 64f, 360f));
            kit.Button(panel, "Leave", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 28f), new Vector2(200f, 48f), UiKit.ButtonStyle.Normal, ShowMainMenu, 20);
            kit.Button(panel, "Play again", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 28f), new Vector2(240f, 48f), UiKit.ButtonStyle.Primary, () =>
            {
                results.SetActive(false);
                shownResult = null;
                MatchHost.For(world)?.Restart();
            }, 20);
            results = shade.gameObject;
            results.SetActive(false);
        }

        static string Outcome(string o)
        {
            for (int i = 0; i < Outcomes.Length; i += 2) if (Outcomes[i] == o) return Outcomes[i + 1];
            return o;
        }

        static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        /// <summary>Shows the results for a finished match (the original's columns, per role).</summary>
        public void ShowResults(MatchResult r)
        {
            EnsureBuilt();
            if (r == null) return;
            shownResult = r;
            bool hunters = r.Winner == Winner.Hunters;
            resultsBanner.text = hunters ? "ZACH WINS" : "SURVIVORS ESCAPED";
            resultsLine.text = $"{r.Reason}  ·  {Clock(r.DurationSec)}  ·  generators {r.GeneratorsRepaired}/{r.GeneratorsRequired}";
            for (int i = resultsTable.childCount - 1; i >= 0; i--) Destroy(resultsTable.GetChild(i).gameObject);
            string[] heads = { "Player", "Result", "Repair s / hits", "Heals / downs", "Unstakes / stakes", "Stuns / stunned", "Alive / gens hit" };
            float[] xs = { 0f, 180f, 310f, 450f, 590f, 730f, 860f };
            float y = 0f;
            void Row(string[] cells, bool head)
            {
                for (int c = 0; c < cells.Length; c++)
                {
                    Text t = kit.Label(Node(cells[c], resultsTable, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(xs[c], y), new Vector2(c == 0 ? 170f : 130f, 26f)), cells[c],
                        head ? 13 : 16, TextAnchor.MiddleLeft, head ? UiKit.Muted : UiKit.Bone, head ? UiKit.Face.Display : UiKit.Face.Mono, head ? 0.1f : 0f, false);
                    if (head) t.text = cells[c].ToUpperInvariant();
                }
                y -= head ? 34f : 30f;
            }
            Row(heads, true);
            foreach (var (_, name, role, s) in r.Stats)
            {
                bool z = role == Role.Hunter;
                Row(new[]
                {
                    name, Outcome(s.Outcome),
                    z ? s.Hits.ToString() : Mathf.RoundToInt(s.RepairSec).ToString(),
                    z ? s.Downs.ToString() : (s.Heals + s.Revives).ToString(),
                    z ? s.Stakes.ToString() : s.Unstakes.ToString(),
                    z ? s.StunnedTimes.ToString() : s.Stuns.ToString(),
                    z ? s.GensDamaged.ToString() : $"{Mathf.FloorToInt(s.TimeAlive / 60f)}m",
                }, false);
            }
            results.SetActive(true);
            results.transform.SetAsLastSibling();
        }

        static Texture2D Ring()
        {
            if (ringTex != null) return ringTex;
            const int n = 64;
            ringTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.1f);
                    ringTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            ringTex.Apply();
            return ringTex;
        }

        /// <summary>The clock, the results when the match ends, and the stake auras over staked teammates (survivors see them anywhere).</summary>
        void RefreshMatch(SimPlayer me)
        {
            MatchSim sim = host != null ? host.Sim : null;
            if (sim == null) return;
            clock.text = sim.Result != null ? Clock(sim.Result.DurationSec) : Clock(sim.Time);
            if (sim.Result != null && sim.Result != shownResult && !sim.TestMode) ShowResults(sim.Result);

            int used = 0;
            Camera cam = Camera.main;
            if (cam != null && me.Role != Role.Hunter && world != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1000f / 180f);
                foreach (SimPlayer q in sim.Order)
                {
                    if (q.Id == me.Id || q.Role != Role.Survivor || q.Health != Game.Health.Staked) continue;
                    Vector3 wp = world.transform.TransformPoint(new Vector3(q.Pos.x, world.Terrain != null ? world.Terrain.Height(q.Pos.x, q.Pos.y) : 0f, q.Pos.y));
                    Vector3 sp = cam.WorldToScreenPoint(wp);
                    if (sp.z < 0f) continue;
                    if (used == auras.Count)
                    {
                        RectTransform a = Node("Aura", auraRoot, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
                        var img = a.gameObject.AddComponent<RawImage>();
                        img.texture = Ring();
                        img.raycastTarget = false;
                        auras.Add(a);
                    }
                    RectTransform rt = auras[used++];
                    rt.gameObject.SetActive(true);
                    Vector2 local;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(auraRoot, sp, null, out local);
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = local;
                    float size = 70f + pulse * 18f;
                    rt.sizeDelta = new Vector2(size, size * 0.6f);
                    rt.GetComponent<RawImage>().color = new Color(1f, 0x3a / 255f, 0x5a / 255f, 0.55f + pulse * 0.3f);
                }
            }
            for (int i = used; i < auras.Count; i++) auras[i].gameObject.SetActive(false);
        }
    }
}
