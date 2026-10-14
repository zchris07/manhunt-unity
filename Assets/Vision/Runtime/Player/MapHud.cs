using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vision.Rendering;
using Vision.UI;
using Vision.World;

namespace Vision.Player
{
    /// <summary>
    /// The minimap (top right, about 57 m across like the original's) and the full map (M), both turned with the view
    /// (up on the map is up on the screen). Both show the
    /// painted map under the fog of war: black until the player's own light has been there. Supplies, generators
    /// (yellow, green once running) and the gate (grey, yellow with power, green open) appear once seen; an arrow shows
    /// where you face. Testing mode adds Reveal all and click-to-teleport on the full map.
    /// </summary>
    public sealed class MapHud
    {
        /// <summary>Metres the minimap shows across (the original's 950-unit radius at 3 cm).</summary>
        public const float MiniSpan = 57f;
        /// <summary>Pixel sizes of the minimap view and the full map.</summary>
        public const float MiniPx = 250f, FullPx = 860f;
        /// <summary>
        /// Holding Tab, the wheel zooms the full map in (up to <see cref="MaxZoom"/> times, about the pointer) and dragging
        /// pans it; it stays inside its circle, never showing past the edge of the world. The full map's art is painted again
        /// <see cref="HiResSize"/> pixels square off the main thread after a level is built, so it stays sharp zoomed in.
        /// </summary>
        public const float MaxZoom = 4f;
        public const int HiResSize = 2048;
        /// <summary>The full map is open (anywhere): the wheel is the map's, not the inventory's or the testing camera's.</summary>
        public static bool AnyFullOpen;

        readonly Font font;
        readonly RectTransform miniContent, fullMap, fullZoom, fullPanel, miniArrow, fullArrow, youRing, youLabel, miniSpin;
        /// <summary>How far the full map is zoomed in (1 = the whole world), and where it's panned to (panel units).</summary>
        public float Zoom { get; private set; } = 1f;
        public Vector2 Pan => fullZoom.anchoredPosition;
        float shownZoom = -1f;
        int shownChildren = -1;
        Texture2D hiArt;
        System.Threading.Tasks.Task<Color32[]> hiTask;
        int bindSerial;

        /// <summary>The 3D view's turn (degrees clockwise from north): the minimap turns with it, so its up is the screen's up.</summary>
        public static float ViewYaw;
        float lastViewYaw = float.MaxValue;
        readonly List<(RectTransform mini, RectTransform full, Text name)> npcDots = new List<(RectTransform, RectTransform, Text)>();
        readonly Image youRingImage;
        readonly RawImage miniArt, miniFog, fullArt, fullFog;
        readonly GameObject full, revealButton, teleportHint;
        readonly Text revealLabel;
        readonly Sprite dot, arrow, ring;
        readonly Dictionary<Object, (Graphic mini, Graphic full)> icons = new Dictionary<Object, (Graphic, Graphic)>();
        readonly HashSet<Object> seen = new HashSet<Object>();

        SandboxWorld world;
        Texture2D art;
        float nextReveal, nextIcons, lastYaw;
        Vector2 lastMe;
        // The markers are written whenever this is set (a new level, the map opening) or the player moved or turned.
        bool markerDirty = true;
        VisionMaskRenderer mask;

        public FogOfWar Fog { get; private set; }
        public bool FullOpen => full.activeSelf;
        public RectTransform FullMapRect => fullMap;

        public MapHud(Transform root, Font font)
        {
            this.font = font;
            dot = MakeSprite(32, (x, y) => Mathf.Clamp01(16f - Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f))));
            ring = MakeSprite(64, (x, y) =>
            {
                float r = Mathf.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f));
                return Mathf.Clamp01(2.2f - Mathf.Abs(r - 28f)) + 0.35f * Mathf.Clamp01(1f - r / 28f);
            });
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
            // The map turns about the player (the centre of the minimap) with the view.
            miniSpin = Node("Spin", view, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            miniContent = Node("Content", miniSpin, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            miniArt = Raw(Stretch(Node("Art", miniContent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)));
            miniFog = Raw(Stretch(Node("Fog", miniContent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)));
            miniArrow = Node("You", view, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
            Image(miniArrow, new Color(1f, 0.91f, 0.55f)).sprite = arrow;
            Label(Node("Hint", frame, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(MiniPx, 20f)), "hold TAB · map", 15, TextAnchor.UpperCenter, new Color(0.62f, 0.61f, 0.57f));

            // Full map.
            RectTransform shade = Stretch(Node("Full Map", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero));
            shade.gameObject.AddComponent<Canvas>();
            shade.gameObject.AddComponent<GraphicRaycaster>();
            Image(shade, new Color(0f, 0f, 0f, 0.7f));
            // Just the round map, floating over the dimmed game: no box round it, no border, no title or legend. Sized and
            // placed every frame it's open to fit between the compass and the bars at the bottom (see FitFull).
            fullPanel = Node("Panel", shade, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(FullPx, FullPx));
            RectTransform panel = fullPanel;
            // A soft shadow falling off round the circle, lifting it off the game behind.
            Image shadow = Image(Node("Shadow", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(FullPx * 1.16f, FullPx * 1.16f)), new Color(0f, 0f, 0f, 0.75f));
            shadow.sprite = MakeSprite(256, (x, y) =>
            {
                float r = Mathf.Sqrt((x - 127.5f) * (x - 127.5f) + (y - 127.5f) * (y - 127.5f)) / 128f;
                float k = Mathf.Clamp01((1f - r) / (1f - 0.8f));
                return k * k * (3f - 2f * k);
            });
            shadow.raycastTarget = false;
            // A circle: the map turns with the view inside it, so whatever shape the generated world is (and however it's
            // turned) only the round map shows, never a square's corners.
            RectTransform disc = Node("Disc", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(FullPx, FullPx));
            Sprite circle = MakeSprite(256, (x, y) => Mathf.Clamp01(128f - Mathf.Sqrt((x - 127.5f) * (x - 127.5f) + (y - 127.5f) * (y - 127.5f))));
            Image discMask = Image(disc, Color.white);
            discMask.sprite = circle;
            disc.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            fullZoom = Node("Zoom", disc, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(FullPx, FullPx));
            fullMap = Node("Map", fullZoom, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(FullPx, FullPx));
            fullArt = Raw(Stretch(Node("Art", fullMap, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)));
            fullArt.raycastTarget = true;
            fullFog = Raw(Stretch(Node("Fog", fullMap, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)));
            // You, on the full map: a pulsing ring and a label so you're easy to find, as in the original.
            youRing = Node("You Ring", fullMap, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f));
            youRingImage = Image(youRing, new Color(1f, 0.91f, 0.55f, 0.8f));
            youRingImage.sprite = ring;
            youLabel = Label(Node("You Label", fullMap, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(90f, 24f)), "YOU", 20, TextAnchor.LowerCenter, new Color(1f, 0.91f, 0.55f)).rectTransform;
            var labelOutline = youLabel.gameObject.AddComponent<Outline>();
            labelOutline.effectColor = Color.black;
            labelOutline.effectDistance = new Vector2(1.5f, -1.5f);
            fullArrow = Node("You", fullMap, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            Image(fullArrow, new Color(1f, 0.91f, 0.55f)).sprite = arrow;
            fullArrow.gameObject.AddComponent<Outline>().effectColor = Color.black;
            // The map's edge fades softly into the dark (inside the circle, over the map), in place of a border.
            Image edge = Image(Stretch(Node("Edge", disc, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)), new Color(0f, 0f, 0f, 0.8f));
            edge.sprite = MakeSprite(256, (x, y) =>
            {
                float r = Mathf.Sqrt((x - 127.5f) * (x - 127.5f) + (y - 127.5f) * (y - 127.5f)) / 128f;
                float k = Mathf.Clamp01((r - 0.86f) / 0.14f);
                return k * k;
            });
            edge.raycastTarget = false;
            // Testing mode: the NPCs, as the original shows them (made as needed, see NpcDot).
            RectTransform reveal = Node("Reveal all", panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -12f), new Vector2(170f, 34f));
            Image(reveal, new Color(0.16f, 0.16f, 0.17f, 1f)).raycastTarget = true;
            reveal.gameObject.AddComponent<Button>().onClick.AddListener(RevealAll);
            revealLabel = Label(Stretch(Node("Text", reveal, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)), "Reveal all", 18, TextAnchor.MiddleCenter, new Color(0.86f, 0.85f, 0.80f));
            revealButton = reveal.gameObject;
            teleportHint = Label(Node("Teleport", panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(FullPx, 22f)), "Testing: click the map to teleport there", 18, TextAnchor.MiddleCenter, new Color(0.62f, 0.61f, 0.57f)).gameObject;
            fullMap.gameObject.AddComponent<MapClick>().map = this;
            full = shade.gameObject;
            full.SetActive(false);
        }

        // ---------------------------------------------------------------- binding

        /// <summary>Paints the map for a newly generated level and starts a fresh fog.</summary>
        public void Bind(SandboxWorld w)
        {
            world = w;
            BoundLayout = w.Layout;
            Kill(art);
            art = MapPainter.Paint(w);
            // The full map's big copy, painted off the main thread and swapped in when it's ready.
            Kill(hiArt);
            hiArt = null;
            int serial = ++bindSerial;
            hiTask = Application.isPlaying ? System.Threading.Tasks.Task.Run(() => MapPainter.PaintPixels(w, HiResSize)) : null;
            if (Fog != null) Kill(Fog.Texture);
            Fog = new FogOfWar(w.MapExtent);
            miniArt.texture = fullArt.texture = art;
            miniFog.texture = fullFog.texture = Fog.Texture;
            foreach (var pair in icons.Values)
            {
                if (pair.mini != null) Kill(pair.mini.gameObject);
                if (pair.full != null) Kill(pair.full.gameObject);
            }
            icons.Clear();
            seen.Clear();
            float px = MiniPx * 2f * w.MapExtent / MiniSpan;
            miniContent.sizeDelta = new Vector2(px, px);
            markerDirty = true;
            if (GameSession.TestingMode) Fog.RevealAll();
        }

        public SandboxWorld World => world;
        /// <summary>The player's marker on the full map (arrow), its pulsing ring and label, and the scrolling minimap content.</summary>
        public RectTransform YouArrow => fullArrow;
        public RectTransform YouRing => youRing;
        public RectTransform YouLabel => youLabel;
        public RectTransform MiniContent => miniContent;
        /// <summary>The level the maps were painted from (a New map makes a new one).</summary>
        public MapLayout BoundLayout { get; private set; }

        public void Toggle() => SetOpen(!full.activeSelf);

        /// <summary>Clear of the compass and clock at the top, and of the stamina bar and inventory at the bottom (canvas units).</summary>
        public const float FullTop = 104f, FullBottom = 172f;

        /// <summary>
        /// Sizes the full map to the screen: as big as fits (never past its own size) in the band between the compass and
        /// the bars at the bottom, centred in it, the testing hint beneath it.
        /// </summary>
        void FitFull()
        {
            var area = ((RectTransform)full.transform).rect;
            float hint = GameSession.TestingMode ? 34f : 0f;
            float band = area.height - FullTop - FullBottom - hint;
            float k = Mathf.Clamp(Mathf.Min(band, area.width - 80f) / FullPx, 0.3f, 1f);
            fullPanel.localScale = new Vector3(k, k, 1f);
            fullPanel.anchoredPosition = new Vector2(0f, (FullBottom + hint - FullTop) * 0.5f);
        }

        /// <summary>The full map's on-screen box (canvas units, tests and captures).</summary>
        public Rect FullBox
        {
            get
            {
                float k = fullPanel.localScale.x;
                Vector2 c = fullPanel.anchoredPosition;
                return new Rect(c.x - FullPx * k * 0.5f, c.y - FullPx * k * 0.5f, FullPx * k, FullPx * k);
            }
        }

        public void SetOpen(bool open)
        {
            full.SetActive(open);
            AnyFullOpen = open;
            if (open)
            {
                FitFull();
                // Each time it opens: the whole world, centred.
                Zoom = 1f;
                fullZoom.localScale = Vector3.one;
                fullZoom.anchoredPosition = Vector2.zero;
            }
            markerDirty = true;
        }

        public void RevealAll()
        {
            if (Fog == null || !GameSession.TestingMode) return;
            Fog.RevealAll();
            Fog.Apply();
        }

        (RectTransform mini, RectTransform full, Text name) NpcDot(int i)
        {
            while (npcDots.Count <= i)
            {
                // In the minimap's content, so the dots turn and scroll with the map like everything else on it.
                RectTransform mini = Node("NPC", miniContent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(9f, 9f));
                Image(mini, new Color(0.71f, 0.54f, 1f)).sprite = dot;
                RectTransform fullDot = Node("NPC", fullMap, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 12f));
                Image(fullDot, new Color(0.71f, 0.54f, 1f)).sprite = dot;
                Text name = Label(Node("Name", fullDot, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(160f, 18f)), "", 14, TextAnchor.LowerCenter, new Color(0.85f, 0.78f, 1f));
                name.gameObject.AddComponent<Shadow>().effectColor = Color.black;
                npcDots.Add((mini, fullDot, name));
            }
            return npcDots[i];
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
            // Lit areas on screen count as seen too (light shows whether or not you have a line of sight to it).
            Rect view = mask.MaskRect;
            int version = Vision.Visibility.VisionWorld.Occluders.Version;
            foreach (Vision.Visibility.VisionLight light in Vision.Visibility.VisionWorld.Lights)
            {
                if (light == null || !view.Contains(light.PlanePosition)) continue;
                scratch.Clear();
                foreach (Vector2 v in light.GetPolygon(mask.Computer, version)) scratch.Add(ToMap(v));
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
                // Testing mode shows the whole map; otherwise the fog opens where the player has looked.
                if (GameSession.TestingMode) { if (!Fog.AllRevealed) Fog.RevealAll(); }
                else RevealFromVision();
                Fog.Apply();
            }
            if (hiTask != null && hiTask.IsCompleted)
            {
                if (hiTask.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                {
                    hiArt = MapPainter.ToTexture(hiTask.Result, HiResSize, true);
                    fullArt.texture = hiArt;
                }
                hiTask = null;
            }
            if (full.activeSelf)
            {
                revealButton.SetActive(false);
                teleportHint.SetActive(GameSession.TestingMode);
                FitFull();
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null)
                {
                    float wheel = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(wheel) > 0.01f) ZoomBy(wheel > 0f ? 1.18f : 1f / 1.18f, mouse.position.ReadValue());
                }
                KeepMarkersSized();
            }

            Vector2 me = PlayerOnMap(out Vector2 facing);
            float miniScale = MiniPx / MiniSpan, fullScale = FullPx / (2f * world.MapExtent);
            float yaw = -Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg;
            // Only touch the UI when something moved (every change re-batches the canvas), or the map was rebuilt or opened.
            if (markerDirty || (me - lastMe).sqrMagnitude > 0.0004f || Mathf.Abs(Mathf.DeltaAngle(yaw, lastYaw)) > 0.5f || Mathf.Abs(Mathf.DeltaAngle(ViewYaw, lastViewYaw)) > 0.1f)
            {
                markerDirty = false;
                lastMe = me;
                lastYaw = yaw;
                lastViewYaw = ViewYaw;
                miniContent.anchoredPosition = -me * miniScale;
                miniSpin.localRotation = Quaternion.Euler(0f, 0f, ViewYaw);
                UprightPictures();
                miniArrow.localRotation = Quaternion.Euler(0f, 0f, yaw + ViewYaw);
                // The full map turns with the view too; its labels stay upright and above what they name.
                Quaternion upright = Quaternion.Euler(0f, 0f, -ViewYaw);
                fullMap.localRotation = Quaternion.Euler(0f, 0f, ViewYaw);
                fullArrow.localRotation = Quaternion.Euler(0f, 0f, yaw);
                fullArrow.anchoredPosition = youRing.anchoredPosition = me * fullScale;
                youLabel.localRotation = upright;
                youLabel.anchoredPosition = me * fullScale + (Vector2)(upright * new Vector3(0f, 38f / Zoom, 0f));
            }
            if (full.activeSelf)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(now * 4f);
                youRing.sizeDelta = Vector2.one * (40f + pulse * 22f);
                youRingImage.color = new Color(1f, 0.91f, 0.55f, 0.9f - pulse * 0.5f);
            }

            // Testing mode: where every NPC is (the dead lie where they fell; the gone aren't shown).
            Vision.Game.MatchHost host = GameSession.TestingMode ? Vision.Game.MatchHost.For(world) : null;
            int shown = 0;
            if (host != null && host.Sim != null)
                foreach (Vision.Game.Npc n in host.Sim.Npcs)
                {
                    if (n.Gone) continue;
                    var d = NpcDot(shown++);
                    // Within the minimap's corners however it is turned (the mask clips the rest).
                    d.mini.gameObject.SetActive((n.Pos - me).sqrMagnitude < MiniSpan * MiniSpan * 0.5f);
                    d.full.gameObject.SetActive(true);
                    d.mini.anchoredPosition = n.Pos * miniScale;
                    d.full.anchoredPosition = n.Pos * fullScale;
                    // A round dot: turning it against the map keeps its name upright above it.
                    d.full.localRotation = Quaternion.Euler(0f, 0f, -ViewYaw);
                    string label = n.Alive ? n.Name : n.Name + " (dead)";
                    if (d.name.text != label) d.name.text = label;
                }
            for (int i = shown; i < npcDots.Count; i++)
            {
                npcDots[i].mini.gameObject.SetActive(false);
                npcDots[i].full.gameObject.SetActive(false);
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
            void Show(Object key, Vector2 at, Color color, Vector2 size, bool round, bool always = false, Texture2D picture = null)
            {
                alive.Add(key);
                if (!seen.Contains(key) && !always)
                {
                    if (!Fog.Seen(at)) return;
                    seen.Add(key);
                }
                if (!icons.TryGetValue(key, out var pair))
                {
                    pair = picture != null ? (Picture(miniContent, picture), Picture(fullMap, picture)) : ((Graphic)Icon(miniContent, round), Icon(fullMap, round));
                    icons[key] = pair;
                }
                pair.mini.color = pair.full.color = color;
                pair.mini.rectTransform.anchoredPosition = at * miniScale;
                pair.mini.rectTransform.sizeDelta = size;
                pair.full.rectTransform.anchoredPosition = at * fullScale;
                pair.full.rectTransform.sizeDelta = size * 1.3f;
            }
            // Each item by its own picture (the inventory's icon), upright however the map is turned.
            foreach (Pickup p in world.Pickups)
            {
                if (p == null) continue;
                Vector3 lp = world.transform.InverseTransformPoint(p.transform.position);
                Show(p, new Vector2(lp.x, lp.z), Color.white, new Vector2(16f, 16f), true, false, ItemIcons.For(p.item, p.golden));
            }
            // The notes, by their own picture (a sheet of old paper with a photo pinned to it), once seen.
            Texture2D note = ItemIcons.Get("note");
            foreach (Transform n in world.Notes)
            {
                if (n == null) continue;
                Vector3 lp = world.transform.InverseTransformPoint(n.position);
                Show(n, new Vector2(lp.x, lp.z), Color.white, new Vector2(19f, 19f), true, false, note);
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
            // Zach knows where every stake in play stands.
            Vision.Game.MatchHost host = Vision.Game.MatchHost.For(world);
            if (host != null && host.Local != null && host.Local.Role == Vision.Game.Role.Hunter)
                foreach (Transform t in world.Stakes)
                {
                    if (t == null || !t.gameObject.activeSelf) continue;
                    Vector3 lp = world.transform.InverseTransformPoint(t.position);
                    Show(t, new Vector2(lp.x, lp.z), new Color(1f, 0.23f, 0.35f), new Vector2(8f, 8f), false, true);
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
            foreach (var d in npcDots) d.mini.SetAsLastSibling();
            miniArrow.SetAsLastSibling();
            foreach (var d in npcDots) d.full.SetAsLastSibling();
            youRing.SetAsLastSibling();
            youLabel.SetAsLastSibling();
            fullArrow.SetAsLastSibling();
        }

        /// <summary>Zooms the full map by <paramref name="factor"/> about a point on the screen (it stays under the pointer).</summary>
        public void ZoomBy(float factor, Vector2 screen)
        {
            var disc = (RectTransform)fullZoom.parent;
            float z = Mathf.Clamp(Zoom * factor, 1f, MaxZoom);
            if (Mathf.Approximately(z, Zoom)) return;
            Vector2 c = Vector2.zero;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(disc, screen, null, out Vector2 local)) c = local - disc.rect.center;
            Vector2 o = fullZoom.anchoredPosition;
            fullZoom.anchoredPosition = c - (c - o) * (z / Zoom);
            Zoom = z;
            fullZoom.localScale = new Vector3(z, z, 1f);
            ClampPan();
            markerDirty = true;
        }

        /// <summary>Moves the zoomed full map by a drag (screen pixels), kept inside its circle.</summary>
        public void PanBy(Vector2 from, Vector2 to)
        {
            var disc = (RectTransform)fullZoom.parent;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(disc, from, null, out Vector2 a)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(disc, to, null, out Vector2 b)) return;
            fullZoom.anchoredPosition += b - a;
            ClampPan();
        }

        /// <summary>The world's circle always covers the round view: the pan never shows past the edge of the world.</summary>
        void ClampPan()
        {
            float limit = (Zoom - 1f) * FullPx * 0.5f;
            fullZoom.anchoredPosition = Vector2.ClampMagnitude(fullZoom.anchoredPosition, limit);
        }

        /// <summary>The markers stay the same size on screen however far the map is zoomed in (only where they sit scales).</summary>
        void KeepMarkersSized()
        {
            int n = fullMap.childCount;
            if (Mathf.Approximately(shownZoom, Zoom) && n == shownChildren) return;
            shownZoom = Zoom;
            shownChildren = n;
            Vector3 k = Vector3.one / Zoom;
            foreach (Transform c in fullMap)
                if (c != fullArt.transform && c != fullFog.transform) c.localScale = k;
        }

        /// <summary>Testing mode: a click on the full map moves the player there.</summary>
        public void ClickAt(Vector2 local)
        {
            if (!GameSession.TestingMode || world == null || world.Player == null) return;
            float scale = FullPx / (2f * world.MapExtent);
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

        /// <summary>An item's picture on a map, kept upright as the map turns (see <see cref="UprightPictures"/>).</summary>
        RawImage Picture(RectTransform parent, Texture2D texture)
        {
            RectTransform rt = Node("Item", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
            RawImage img = rt.gameObject.AddComponent<RawImage>();
            img.texture = texture;
            img.raycastTarget = false;
            rt.localRotation = Quaternion.Euler(0f, 0f, -ViewYaw);
            return img;
        }

        /// <summary>Turns the item pictures back against the maps' turn, so they read upright.</summary>
        void UprightPictures()
        {
            Quaternion q = Quaternion.Euler(0f, 0f, -ViewYaw);
            foreach (var pair in icons.Values)
            {
                if (pair.mini is RawImage) pair.mini.rectTransform.localRotation = q;
                if (pair.full is RawImage) pair.full.rectTransform.localRotation = q;
            }
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
    public sealed class MapClick : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler
    {
        public MapHud map;

        // Dragging pans the zoomed map (a drag never counts as a click, so it never teleports).
        public void OnBeginDrag(PointerEventData e) { }

        public void OnDrag(PointerEventData e) => map?.PanBy(e.position - e.delta, e.position);

        public void OnPointerClick(PointerEventData e)
        {
            var rt = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out Vector2 local))
                map?.ClickAt(local - rt.rect.center);
        }
    }
}
