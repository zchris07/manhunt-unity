using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vision.Rendering;
using Vision.World;

namespace Vision.Player
{
    /// <summary>
    /// The minimap (top right, north up, about 57 m across like the original's) and the full map (M). Both show the
    /// painted map under the fog of war: black until the player's own light has been there. Supplies, generators
    /// (yellow, green once running) and the gate (grey, yellow with power, green open) appear once seen; an arrow shows
    /// where you face. Testing mode adds Reveal all and click-to-teleport on the full map.
    /// </summary>
    public sealed class MapHud
    {
        /// <summary>Metres the minimap shows across (the original's 950-unit radius at 3 cm).</summary>
        public const float MiniSpan = 57f;
        const float MiniPx = 250f, FullPx = 860f;

        readonly Font font;
        readonly RectTransform miniContent, fullMap, miniArrow, fullArrow;
        readonly RawImage miniArt, miniFog, fullArt, fullFog;
        readonly GameObject full, revealButton, teleportHint;
        readonly Text revealLabel, title;
        readonly Sprite dot, arrow;
        readonly Dictionary<Object, (Image mini, Image full)> icons = new Dictionary<Object, (Image, Image)>();
        readonly HashSet<Object> seen = new HashSet<Object>();

        SandboxWorld world;
        Texture2D art;
        float nextReveal, nextIcons, lastYaw = float.NaN;
        Vector2 lastMe = new Vector2(float.NaN, float.NaN);
        VisionMaskRenderer mask;

        public FogOfWar Fog { get; private set; }
        public bool FullOpen => full.activeSelf;
        public RectTransform FullMapRect => fullMap;

        public MapHud(Transform root, Font font)
        {
            this.font = font;
            dot = MakeSprite(32, (x, y) => Mathf.Clamp01(16f - Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f))));
            arrow = MakeSprite(32, (x, y) =>
            {
                // A chevron pointing up: inside the triangle (16, 30), (3, 2), (29, 2), minus a notch at the base.
                float u = (x + 0.5f - 16f) / 13f, v = (y + 0.5f - 2f) / 28f;
                bool inTri = v >= 0f && v <= 1f && Mathf.Abs(u) <= 1f - v;
                bool notch = v < 0.35f && Mathf.Abs(u) < 0.35f - v;
                return inTri && !notch ? 1f : 0f;
            });

            // Minimap.
            RectTransform frame = Node("Minimap", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(MiniPx + 8f, MiniPx + 8f));
            Image(frame, new Color(0.04f, 0.04f, 0.045f, 0.85f));
            RectTransform view = Node("View", frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MiniPx, MiniPx));
            view.gameObject.AddComponent<Canvas>();   // its own canvas: the moving map never rebuilds the rest of the HUD
            view.gameObject.AddComponent<RectMask2D>();
            Image(view, new Color(0.02f, 0.02f, 0.03f, 1f));
            miniContent = Node("Content", view, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            miniArt = Raw(Stretch(Node("Art", miniContent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)));
            miniFog = Raw(Stretch(Node("Fog", miniContent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)));
            miniArrow = Node("You", view, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
            Image(miniArrow, new Color(1f, 0.91f, 0.55f)).sprite = arrow;
            Label(Node("Hint", frame, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(MiniPx, 20f)), "M · map", 15, TextAnchor.UpperCenter, new Color(0.62f, 0.61f, 0.57f));

            // Full map.
            RectTransform shade = Stretch(Node("Full Map", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero));
            shade.gameObject.AddComponent<Canvas>();
            shade.gameObject.AddComponent<GraphicRaycaster>();
            Image(shade, new Color(0f, 0f, 0f, 0.7f));
            RectTransform panel = Node("Panel", shade, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(FullPx + 40f, FullPx + 90f));
            Image(panel, new Color(0.05f, 0.05f, 0.055f, 0.96f));
            title = Label(Node("Title", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(FullPx, 34f)), "MAP", 28, TextAnchor.MiddleCenter, new Color(0.86f, 0.85f, 0.80f));
            fullMap = Node("Map", panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(FullPx, FullPx));
            fullArt = Raw(Stretch(Node("Art", fullMap, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)));
            fullArt.raycastTarget = true;
            fullFog = Raw(Stretch(Node("Fog", fullMap, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)));
            fullArrow = Node("You", fullMap, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            Image(fullArrow, new Color(1f, 0.91f, 0.55f)).sprite = arrow;
            Label(Node("Legend", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -16f), new Vector2(420f, 26f)),
                "<color=#ffe88c>▲</color> you   <color=#ffd23a>■</color> generator   <color=#4cff6a>●</color> supply   gate ▬", 16, TextAnchor.MiddleLeft, new Color(0.62f, 0.61f, 0.57f)).supportRichText = true;
            RectTransform reveal = Node("Reveal all", panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -12f), new Vector2(170f, 34f));
            Image(reveal, new Color(0.16f, 0.16f, 0.17f, 1f)).raycastTarget = true;
            reveal.gameObject.AddComponent<Button>().onClick.AddListener(RevealAll);
            revealLabel = Label(Stretch(Node("Text", reveal, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)), "Reveal all", 18, TextAnchor.MiddleCenter, new Color(0.86f, 0.85f, 0.80f));
            revealButton = reveal.gameObject;
            teleportHint = Label(Node("Teleport", panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(FullPx, 18f)), "Testing: click the map to teleport there", 15, TextAnchor.MiddleCenter, new Color(0.62f, 0.61f, 0.57f)).gameObject;
            fullMap.gameObject.AddComponent<MapClick>().map = this;
            full = shade.gameObject;
            full.SetActive(false);
        }

        // ---------------------------------------------------------------- binding

        /// <summary>Paints the map for a newly generated level and starts a fresh fog.</summary>
        public void Bind(SandboxWorld w)
        {
            world = w;
            Kill(art);
            art = MapPainter.Paint(w);
            if (Fog != null) Kill(Fog.Texture);
            Fog = new FogOfWar(w.halfExtent);
            miniArt.texture = fullArt.texture = art;
            miniFog.texture = fullFog.texture = Fog.Texture;
            foreach (var pair in icons.Values)
            {
                if (pair.mini != null) Kill(pair.mini.gameObject);
                if (pair.full != null) Kill(pair.full.gameObject);
            }
            icons.Clear();
            seen.Clear();
            float px = MiniPx * 2f * w.halfExtent / MiniSpan;
            miniContent.sizeDelta = new Vector2(px, px);
        }

        public SandboxWorld World => world;

        public void Toggle() => full.SetActive(!full.activeSelf);
        public void SetOpen(bool open) => full.SetActive(open);

        public void RevealAll()
        {
            if (Fog == null || !GameSession.TestingMode) return;
            Fog.RevealAll();
            Fog.Apply();
        }

        /// <summary>Map coordinates of the player.</summary>
        Vector2 PlayerOnMap(out Vector2 facing)
        {
            facing = Vector2.up;
            if (world == null || world.Player == null) return Vector2.zero;
            Vector3 lp = world.transform.InverseTransformPoint(world.Player.transform.position);
            if (world.Player.viewer != null) facing = world.Player.viewer.Facing;
            return new Vector2(lp.x, lp.z);
        }

        Vector2 ToMap(Vector2 plane)
        {
            Vector3 lp = world.transform.InverseTransformPoint(new Vector3(plane.x, 0f, plane.y));
            return new Vector2(lp.x, lp.z);
        }

        readonly List<Vector2> scratch = new List<Vector2>(1024);

        /// <summary>Marks what the player's light shows right now.</summary>
        public void RevealFromVision()
        {
            if (Fog == null || world == null) return;
            if (mask == null) mask = Object.FindAnyObjectByType<VisionMaskRenderer>();
            if (mask == null) return;
            foreach (IReadOnlyList<Vector2> poly in new[] { mask.ConePolygon, mask.ProximityPolygon })
            {
                scratch.Clear();
                foreach (Vector2 v in poly) scratch.Add(ToMap(v));
                Fog.Reveal(scratch);
            }
        }

        // ---------------------------------------------------------------- per frame

        public void Update(bool visible)
        {
            if (world == null || Fog == null) return;
            float now = Time.unscaledTime;
            if (now >= nextReveal)
            {
                nextReveal = now + 0.12f;
                RevealFromVision();
                Fog.Apply();
            }
            if (full.activeSelf)
            {
                revealButton.SetActive(GameSession.TestingMode);
                teleportHint.SetActive(GameSession.TestingMode);
                revealLabel.text = Fog.AllRevealed ? "All revealed" : "Reveal all";
                title.text = GameSession.TestingMode ? $"MAP   ·   seed {world.seed}" : "MAP";
            }

            Vector2 me = PlayerOnMap(out Vector2 facing);
            float miniScale = MiniPx / MiniSpan, fullScale = FullPx / (2f * world.halfExtent);
            float yaw = -Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg;
            // Only touch the UI when something moved (every change re-batches the canvas).
            if ((me - lastMe).sqrMagnitude > 0.0004f || Mathf.Abs(Mathf.DeltaAngle(yaw, lastYaw)) > 0.5f)
            {
                lastMe = me;
                lastYaw = yaw;
                miniContent.anchoredPosition = -me * miniScale;
                miniArrow.localRotation = fullArrow.localRotation = Quaternion.Euler(0f, 0f, yaw);
                fullArrow.anchoredPosition = me * fullScale;
            }

            if (now >= nextIcons)
            {
                nextIcons = now + 0.25f;
                UpdateIcons(miniScale, fullScale);
            }
        }

        void UpdateIcons(float miniScale, float fullScale)
        {
            var alive = new HashSet<Object>();
            void Show(Object key, Vector2 at, Color color, Vector2 size, bool round)
            {
                alive.Add(key);
                if (!seen.Contains(key))
                {
                    if (!Fog.Seen(at)) return;
                    seen.Add(key);
                }
                if (!icons.TryGetValue(key, out var pair))
                {
                    pair = (Icon(miniContent, round), Icon(fullMap, round));
                    icons[key] = pair;
                }
                pair.mini.color = pair.full.color = color;
                pair.mini.rectTransform.anchoredPosition = at * miniScale;
                pair.mini.rectTransform.sizeDelta = size;
                pair.full.rectTransform.anchoredPosition = at * fullScale;
                pair.full.rectTransform.sizeDelta = size * 1.3f;
            }
            foreach (Pickup p in world.Pickups)
            {
                if (p == null) continue;
                Vector3 lp = world.transform.InverseTransformPoint(p.transform.position);
                Show(p, new Vector2(lp.x, lp.z), Items.Info(p.item).color * 1.4f, new Vector2(7f, 7f), true);
            }
            foreach (GeneratorObjective g in GeneratorObjective.All)
            {
                if (g == null || !g.transform.IsChildOf(world.transform)) continue;
                Vector3 lp = world.transform.InverseTransformPoint(g.transform.position);
                Show(g, new Vector2(lp.x, lp.z), g.Running ? new Color(0.3f, 1f, 0.42f) : new Color(1f, 0.82f, 0.23f), new Vector2(10f, 10f), false);
            }
            if (world.Gate != null && world.Layout.Plan != null)
            {
                var at = new Vector2(world.Layout.Plan.GateX, world.Layout.Plan.Bounds.yMax);
                Color c = world.Gate.IsOpen ? new Color(0.3f, 1f, 0.42f) : ExitGate.Powered ? new Color(1f, 0.82f, 0.23f) : new Color(0.54f, 0.54f, 0.63f);
                Show(world.Gate, at, c, new Vector2(16f, 6f), false);
            }
            // Taken supplies disappear.
            var gone = new List<Object>();
            foreach (var key in icons.Keys) if (key == null || !alive.Contains(key)) gone.Add(key);
            foreach (Object key in gone)
            {
                var pair = icons[key];
                if (pair.mini != null) Kill(pair.mini.gameObject);
                if (pair.full != null) Kill(pair.full.gameObject);
                icons.Remove(key);
            }
            miniArrow.SetAsLastSibling();
            fullArrow.SetAsLastSibling();
        }

        /// <summary>Testing mode: a click on the full map moves the player there.</summary>
        public void ClickAt(Vector2 local)
        {
            if (!GameSession.TestingMode || world == null || world.Player == null) return;
            float scale = FullPx / (2f * world.halfExtent);
            Vector2 at = local / scale;
            world.Player.Teleport(world.transform.TransformPoint(new Vector3(at.x, 0f, at.y)));
            world.Player.LeaveHiding();
        }

        // ---------------------------------------------------------------- ui helpers

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        Image Icon(RectTransform parent, bool round)
        {
            RectTransform rt = Node("Icon", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f));
            Image img = Image(rt, Color.white);
            if (round) img.sprite = dot;
            return img;
        }

        static RectTransform Node(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static Image Image(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static RawImage Raw(RectTransform rt)
        {
            var img = rt.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            return img;
        }

        Text Label(RectTransform rt, string text, int size, TextAnchor align, Color color)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static Sprite MakeSprite(int n, System.Func<int, int, float> alpha)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(x, y)));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    /// <summary>Forwards clicks on the full map (in its local coordinates, centre = map origin).</summary>
    public sealed class MapClick : MonoBehaviour, IPointerClickHandler
    {
        public MapHud map;

        public void OnPointerClick(PointerEventData e)
        {
            var rt = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out Vector2 local))
                map?.ClickAt(local - rt.rect.center);
        }
    }
}
