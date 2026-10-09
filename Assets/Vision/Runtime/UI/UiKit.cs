using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vision.UI
{
    /// <summary>
    /// The original's interface look ("clinical, grimy, industrial"): bone text on near-black, Oswald for headings and
    /// buttons, Special Elite for typewritten labels, IBM Plex Mono for body text, red accents, square corners, thin
    /// borders and registration marks on cards. Builds uGUI elements in code in that style.
    /// </summary>
    public sealed class UiKit
    {
        public static readonly Color Bg = Hex(0x060605), Bg2 = Hex(0x0d0d0b), CardBg = Hex(0x11110f), CardBg2 = Hex(0x1a1a17);
        public static readonly Color Line = Hex(0x3a3832), Ink = Color.black, Bone = Hex(0xd9d3c1), Muted = Hex(0x8a8574);
        public static readonly Color Red = Hex(0xb3121b), Red2 = Hex(0x7a0a10), Yellow = Hex(0xc9b26a), Green = Hex(0x8fae7a);
        public static readonly Color Cyan = Hex(0x7aa6a8), Purple = Hex(0x8a5ad0), Orange = Hex(0xb0703a);
        public static readonly Color ButtonBg = Hex(0x14140f), ButtonHover = Hex(0x1e1d18), FieldBg = Hex(0x0b0b09);
        public static readonly Color PrimaryText = Hex(0xf0e8d8);

        public enum Face { Mono, Display, DisplayBold, Type }
        public enum ButtonStyle { Normal, Primary, Secondary, Ghost, Link }

        public readonly Font Mono, Display, DisplayBold, Type;

        public UiKit()
        {
            Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Mono = Resources.Load<Font>("Fonts/IBMPlexMono-Regular") ?? fallback;
            Display = Resources.Load<Font>("Fonts/Oswald-Medium") ?? fallback;
            DisplayBold = Resources.Load<Font>("Fonts/Oswald-Bold") ?? fallback;
            Type = Resources.Load<Font>("Fonts/SpecialElite-Regular") ?? fallback;
        }

        public Font FontFor(Face f) => f switch { Face.Display => Display, Face.DisplayBold => DisplayBold, Face.Type => Type, _ => Mono };

        public static Color Hex(int rgb, float a = 1f) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        public static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ---------------------------------------------------------------- layout primitives

        public static RectTransform Node(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
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

        /// <summary>A child filling its parent, inset by <paramref name="inset"/> pixels.</summary>
        public static RectTransform Fill(string name, Transform parent, float inset = 0f)
        {
            RectTransform rt = Node(name, parent, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return Stretch(rt, inset);
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Box(RectTransform rt, Color color, bool raycast = false)
        {
            var img = rt.gameObject.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>A 1-pixel frame: four thin images along the edges (the fill stays clear).</summary>
        public static Image[] Frame(RectTransform rt, Color color, float width = 1f)
        {
            var edges = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                RectTransform e = Node("Edge", rt, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                bool horizontal = i < 2;
                e.anchorMin = horizontal ? new Vector2(0f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 0f);
                e.anchorMax = horizontal ? new Vector2(1f, i == 0 ? 0f : 1f) : new Vector2(i == 2 ? 0f : 1f, 1f);
                e.pivot = new Vector2(horizontal ? 0.5f : (i == 2 ? 0f : 1f), horizontal ? (i == 0 ? 0f : 1f) : 0.5f);
                e.sizeDelta = horizontal ? new Vector2(0f, width) : new Vector2(width, 0f);
                e.anchoredPosition = Vector2.zero;
                edges[i] = Box(e, color);
            }
            return edges;
        }

        /// <summary>A dashed frame (the ghost button): short dashes along each edge, laid out for the rect's size.</summary>
        public static void DashedFrame(RectTransform rt, Vector2 size, Color color, float width = 2f, float dash = 7f, float gap = 5f)
        {
            void Run(Vector2 from, Vector2 dir, float length, bool horizontal)
            {
                for (float d = 0f; d < length - 1f; d += dash + gap)
                {
                    float len = Mathf.Min(dash, length - d);
                    RectTransform e = Node("Dash", rt, Vector2.zero, Vector2.zero, from + dir * d, horizontal ? new Vector2(len, width) : new Vector2(width, len));
                    Box(e, color);
                }
            }
            Run(Vector2.zero, Vector2.right, size.x, true);
            Run(new Vector2(0f, size.y - width), Vector2.right, size.x, true);
            Run(Vector2.zero, Vector2.up, size.y, false);
            Run(new Vector2(size.x - width, 0f), Vector2.up, size.y, false);
        }

        /// <summary>Registration marks in a card's corners, like a clinical form.</summary>
        public static void RegistrationMarks(RectTransform rt, float inset = 6f, float len = 10f)
        {
            Color c = A(Muted, 0.5f);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 anchor = new Vector2(corner % 2, corner / 2);
                Vector2 sign = new Vector2(corner % 2 == 0 ? 1f : -1f, corner / 2 == 0 ? 1f : -1f);
                RectTransform h = Node("Mark", rt, anchor, anchor, sign * inset + new Vector2(sign.x * len * 0.5f, 0f), new Vector2(len, 1f));
                Box(h, c);
                RectTransform v = Node("Mark", rt, anchor, anchor, sign * inset + new Vector2(0f, sign.y * len * 0.5f), new Vector2(1f, len));
                Box(v, c);
            }
        }

        // ---------------------------------------------------------------- text

        public Text Label(RectTransform rt, string text, int size, TextAnchor align, Color color, Face face = Face.Mono, float tracking = 0f, bool shadow = true)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = FontFor(face);
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (tracking != 0f) rt.gameObject.AddComponent<Tracking>().em = tracking;
            if (shadow)
            {
                var s = rt.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0f, 0f, 0f, 0.8f);
                s.effectDistance = new Vector2(1f, -1.5f);
            }
            return t;
        }

        /// <summary>Wrapped body text in a fixed-width block.</summary>
        public Text Paragraph(RectTransform rt, string text, int size, Color color, Face face = Face.Mono, float lineSpacing = 1.2f)
        {
            Text t = Label(rt, text, size, TextAnchor.UpperLeft, color, face, 0f, false);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.lineSpacing = lineSpacing;
            return t;
        }

        // ---------------------------------------------------------------- panels

        /// <summary>The card: a dark gradient, a thin border, a black outline and registration marks.</summary>
        public RectTransform Card(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            RectTransform rt = Node(name, parent, anchor, pivot, pos, size);
            var shadow = Node("Shadow", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), size + new Vector2(24f, 24f));
            var shadowImg = shadow.gameObject.AddComponent<RawImage>();
            shadowImg.texture = SoftShadow();
            shadowImg.color = new Color(0f, 0f, 0f, 0.75f);
            shadowImg.raycastTarget = false;
            RectTransform outline = Fill("Outline", rt, -1f);
            Box(outline, Ink);
            RectTransform body = Fill("Body", rt);
            var g = body.gameObject.AddComponent<RawImage>();
            g.texture = VerticalGradient(Hex(0x13130f), Hex(0x0c0c0a));
            g.raycastTarget = true;
            Frame(rt, Line);
            RegistrationMarks(rt);
            return rt;
        }

        // ---------------------------------------------------------------- controls

        /// <summary>A button in one of the original's styles (Oswald, uppercase, tracked out).</summary>
        public Button Button(RectTransform parent, string label, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, ButtonStyle style, UnityAction onClick, int fontSize = 20)
        {
            RectTransform rt = Node(label, parent, anchor, pivot, pos, size);
            Color bg = style switch { ButtonStyle.Primary => Red2, ButtonStyle.Normal => ButtonBg, _ => Color.clear };
            Color hoverBg = style switch { ButtonStyle.Primary => Red, ButtonStyle.Normal => ButtonHover, ButtonStyle.Link => Color.clear, _ => A(ButtonHover, 0.6f) };
            Color border = style switch { ButtonStyle.Primary => Red, ButtonStyle.Link => Color.clear, _ => Line };
            Color hoverBorder = style switch { ButtonStyle.Primary => Hex(0xe0303a), ButtonStyle.Ghost => Purple, ButtonStyle.Link => Color.clear, _ => Bone };
            Color text = style switch { ButtonStyle.Primary => PrimaryText, ButtonStyle.Ghost => Muted, ButtonStyle.Link => Muted, _ => Bone };
            Color hoverText = style == ButtonStyle.Primary ? Color.white : Bone;

            Image bgImg = Box(rt, bg, true);
            Image[] frame = null;
            var dashes = new List<Image>();
            if (style == ButtonStyle.Ghost)
            {
                DashedFrame(rt, size, Line);
                foreach (Transform c in rt) if (c.name == "Dash") dashes.Add(c.GetComponent<Image>());
            }
            else if (style != ButtonStyle.Link) frame = Frame(rt, border);
            if (style == ButtonStyle.Primary)
            {
                var glow = Node("Glow", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size + new Vector2(36f, 36f));
                glow.SetAsFirstSibling();
                var gi = glow.gameObject.AddComponent<RawImage>();
                gi.texture = SoftShadow();
                gi.color = A(Red, 0.22f);
                gi.raycastTarget = false;
            }
            Text t;
            if (style == ButtonStyle.Link)
            {
                t = Label(Fill("Text", rt), label, fontSize, TextAnchor.MiddleCenter, text, Face.Mono, 0f, false);
                var underline = Node("Underline", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -fontSize * 0.62f), new Vector2(t.preferredWidth, 1f));
                Box(underline, text);
            }
            else t = Label(Fill("Text", rt), label.ToUpperInvariant(), fontSize, TextAnchor.MiddleCenter, text, Face.Display, 0.14f, false);

            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = bgImg;
            if (onClick != null) button.onClick.AddListener(onClick);
            var hover = rt.gameObject.AddComponent<HoverStyle>();
            hover.background = bgImg;
            hover.normalBg = bg;
            hover.hoverBg = hoverBg;
            hover.frame = frame;
            hover.dashes = dashes.ToArray();
            hover.normalBorder = border;
            hover.hoverBorder = hoverBorder;
            hover.label = t;
            hover.normalText = text;
            hover.hoverText = hoverText;
            return button;
        }

        /// <summary>A text field: near-black, a thin border that turns bone when focused, Plex Mono.</summary>
        public InputField Field(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, string placeholder, int maxLength)
        {
            RectTransform rt = Node(name, parent, anchor, pivot, pos, size);
            Image bg = Box(rt, FieldBg, true);
            Image[] frame = Frame(rt, Line);
            RectTransform area = Fill("Text Area", rt, 0f);
            area.offsetMin = new Vector2(12f, 4f);
            area.offsetMax = new Vector2(-12f, -4f);
            Text ph = Label(Fill("Placeholder", area), placeholder, 18, TextAnchor.MiddleLeft, Hex(0x5a5648), Face.Mono, 0f, false);
            ph.fontStyle = FontStyle.Normal;
            Text text = Label(Fill("Text", area), "", 18, TextAnchor.MiddleLeft, Bone, Face.Mono, 0f, false);
            text.supportRichText = false;
            var field = rt.gameObject.AddComponent<InputField>();
            field.targetGraphic = bg;
            field.textComponent = text;
            field.placeholder = ph;
            field.characterLimit = maxLength;
            field.caretColor = Bone;
            field.customCaretColor = true;
            field.selectionColor = A(Red, 0.45f);
            field.transition = Selectable.Transition.None;
            var focus = rt.gameObject.AddComponent<FocusFrame>();
            focus.field = field;
            focus.frame = frame;
            return field;
        }

        /// <summary>A field label: small, bold, uppercase, tracked out, muted.</summary>
        public Text FieldLabel(RectTransform parent, string text, Vector2 anchor, Vector2 pivot, Vector2 pos, float width)
        {
            Text t = Label(Node(text + " Label", parent, anchor, pivot, pos, new Vector2(width, 20f)), text.ToUpperInvariant(), 14, TextAnchor.MiddleLeft, Muted, Face.Mono, 0.1f, false);
            t.fontStyle = FontStyle.Bold;
            return t;
        }

        /// <summary>A slider: a dark track with a thin border, a red fill and a bone handle.</summary>
        public Slider Slider(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, float width, float min, float max, float value, UnityAction<float> changed)
        {
            RectTransform track = Node(name, parent, anchor, pivot, pos, new Vector2(width, 10f));
            Image trackImg = Box(track, FieldBg, true);
            Frame(track, Line);
            RectTransform fillArea = Fill("Fill Area", track, 1f);
            RectTransform fill = Fill("Fill", fillArea);
            Box(fill, Red);
            RectTransform handleArea = Fill("Handle Area", track);
            handleArea.offsetMin = new Vector2(7f, 0f);
            handleArea.offsetMax = new Vector2(-7f, 0f);
            RectTransform handle = Node("Handle", handleArea, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 24f));
            Image handleImg = Box(handle, Bone, true);
            var slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            var colors = slider.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            slider.colors = colors;
            if (changed != null) slider.onValueChanged.AddListener(changed);
            _ = trackImg;
            return slider;
        }

        /// <summary>A key cap: bone on black text, Oswald.</summary>
        public RectTransform Kbd(RectTransform parent, string key, Vector2 pos, float height = 24f)
        {
            Text probe = null;
            RectTransform rt = Node("Key " + key, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(40f, height));
            Box(rt, Bone);
            probe = Label(Fill("Text", rt), key, 15, TextAnchor.MiddleCenter, Hex(0x0a0a08), Face.Display, 0.08f, false);
            rt.sizeDelta = new Vector2(Mathf.Max(28f, probe.preferredWidth * 1.1f + 14f), height);
            return rt;
        }

        /// <summary>A two-column table of key caps and what they do; returns its height.</summary>
        public float ControlsTable(RectTransform parent, (string key, string action)[] rows, Vector2 topLeft, float keyColumn, float rowHeight = 30f, int fontSize = 16)
        {
            float y = topLeft.y;
            foreach (var (key, action) in rows)
            {
                Kbd(parent, key, new Vector2(topLeft.x, y));
                Label(Node(action, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(topLeft.x + keyColumn, y), new Vector2(400f, 24f)), action, fontSize, TextAnchor.MiddleLeft, Bone, Face.Mono, 0f, false);
                y -= rowHeight;
            }
            return topLeft.y - y;
        }

        /// <summary>A heading: Oswald, uppercase, widely tracked.</summary>
        public Text Heading(RectTransform parent, string text, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor align = TextAnchor.MiddleLeft)
        {
            return Label(Node(text, parent, anchor, pivot, pos, size), text.ToUpperInvariant(), fontSize, align, Bone, Face.Display, 0.2f, false);
        }

        // ---------------------------------------------------------------- textures

        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        static Texture2D Cached(string key, System.Func<Texture2D> make)
        {
            if (cache.TryGetValue(key, out Texture2D t) && t != null) return t;
            t = make();
            t.hideFlags = HideFlags.HideAndDontSave;
            cache[key] = t;
            return t;
        }

        public static Texture2D VerticalGradient(Color top, Color bottom) => Cached($"vg{top}{bottom}", () =>
        {
            var t = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++) t.SetPixel(0, y, Color.Lerp(bottom, top, y / 31f));
            t.Apply();
            return t;
        });

        /// <summary>A soft rounded blur, for drop shadows and glows.</summary>
        public static Texture2D SoftShadow() => Cached("shadow", () =>
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - n * 0.5f) - n * 0.18f) / (n * 0.32f);
                    float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - n * 0.5f) - n * 0.18f) / (n * 0.32f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d))));
                }
            t.Apply();
            return t;
        });

        /// <summary>The screens' backdrop: #060605 with a dim warm glow at 50% 40% and a dark red one rising from below.</summary>
        public static Texture2D Backdrop() => Cached("backdrop", () =>
        {
            const int w = 192, h = 108;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color bg = Hex(0x060605);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = 1f - (y + 0.5f) / h;   // v from the top, as in CSS
                    float d1 = Mathf.Sqrt(Sq((u - 0.5f) / 0.5f) + Sq((v - 0.4f) / 0.6f)) / 0.6f;
                    float d2 = Mathf.Sqrt(Sq((u - 0.5f) / 0.5f) + Sq((v - 1.2f) / 1.2f)) / 0.55f;
                    Color c = bg;
                    c = Color.Lerp(c, Hex(0x5a080c), 0.25f * Mathf.Clamp01(1f - d2));
                    c = Color.Lerp(c, Hex(0x28261e), 0.35f * Mathf.Clamp01(1f - d1));
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        });

        static float Sq(float v) => v * v;

        /// <summary>The landing's strip of dark pines along the bottom (the original's SVG, greyed down).</summary>
        public static Texture2D Pines() => Cached("pines", () =>
        {
            const int w = 800, h = 320;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            // Triangles (x0, apex x, apex y, x1) in the SVG's 400 x 160 box; y down from the top.
            float[,] back = { { 20, 45, 60, 70 }, { 60, 95, 30, 130 }, { 120, 140, 80, 160 }, { 150, 185, 20, 220 }, { 210, 235, 70, 260 }, { 250, 290, 10, 330 }, { 320, 345, 60, 370 }, { 355, 385, 40, 415 }, { -20, 5, 50, 30 } };
            float[,] front = { { 0, 25, 90, 50 }, { 90, 115, 70, 140 }, { 170, 200, 60, 230 }, { 270, 300, 80, 330 }, { 340, 370, 95, 400 } };
            void Paint(float[,] tris, Color c)
            {
                for (int i = 0; i < tris.GetLength(0); i++)
                {
                    float x0 = tris[i, 0], ax = tris[i, 1], ay = tris[i, 2], x1 = tris[i, 3];
                    for (int y = 0; y < h; y++)
                    {
                        float sy = (1f - (y + 0.5f) / h) * 160f;   // SVG y
                        if (sy < ay) continue;
                        float k = (sy - ay) / (160f - ay);
                        float l = ax + (x0 - ax) * k, r = ax + (x1 - ax) * k;
                        int a = Mathf.Max(0, Mathf.CeilToInt(l / 400f * w - 0.5f)), b = Mathf.Min(w - 1, Mathf.FloorToInt(r / 400f * w - 0.5f));
                        for (int x = a; x <= b; x++) px[y * w + x] = c;
                    }
                }
            }
            // Greyscale, 40% brightness (the theme's filter); the RawImage sets the 25% opacity.
            float G(int rgb) { Color c = Hex(rgb); return (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) * 0.4f; }
            float gb = G(0x0d2616) * 1.9f, gf = G(0x071a0e) * 1.9f;
            Paint(back, new Color(gb, gb, gb, 0.95f));
            Paint(front, new Color(gf, gf, gf, 0.95f));
            t.SetPixels32(px);
            t.Apply();
            return t;
        });

        /// <summary>Film grain (warm noise) and scanlines (a dark line every 3 px), tiled over the screens.</summary>
        public static Texture2D Grain() => Cached("grain", () =>
        {
            const int n = 192;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var rng = new System.Random(3);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float noise = (float)rng.NextDouble();
                    bool scan = y % 3 == 0;
                    Color c = noise > 0.5f ? new Color(0.8f, 0.78f, 0.7f, (noise - 0.5f) * 0.12f) : new Color(0f, 0f, 0f, (0.5f - noise) * 0.10f);
                    if (scan) c = new Color(0f, 0f, 0f, Mathf.Max(c.a, 0.16f));
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        });
    }

    /// <summary>Spreads a single line of legacy text out by a share of its font size (CSS letter-spacing).</summary>
    [DisallowMultipleComponent]
    public sealed class Tracking : BaseMeshEffect
    {
        public float em = 0.1f;
        readonly List<UIVertex> verts = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || em == 0f) return;
            var text = GetComponent<Text>();
            if (text == null) return;
            verts.Clear();
            vh.GetUIVertexStream(verts);
            int quads = verts.Count / 6;
            if (quads < 2) return;
            // Whitespace makes no glyph quad, so each quad is matched to its character to keep the gaps between words.
            string plain = text.supportRichText ? System.Text.RegularExpressions.Regex.Replace(text.text, "<[^>]*>", "") : text.text;
            var index = new int[quads];
            int q0 = 0;
            for (int c = 0; c < plain.Length && q0 < quads; c++)
                if (!char.IsWhiteSpace(plain[c])) index[q0++] = c;
            if (q0 < quads) for (int q = 0; q < quads; q++) index[q] = q;   // a line break or something unexpected: plain spacing
            float step = em * text.fontSize;
            float total = step * index[quads - 1];
            TextAnchor a = text.alignment;
            float shift = a == TextAnchor.UpperCenter || a == TextAnchor.MiddleCenter || a == TextAnchor.LowerCenter ? -total * 0.5f
                : a == TextAnchor.UpperRight || a == TextAnchor.MiddleRight || a == TextAnchor.LowerRight ? -total : 0f;
            // Glyphs in reading order: by line (top first), then left to right.
            for (int q = 0; q < quads; q++)
            {
                float dx = shift + step * index[q];
                for (int k = 0; k < 6; k++)
                {
                    UIVertex v = verts[q * 6 + k];
                    v.position.x += dx;
                    verts[q * 6 + k] = v;
                }
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }
    }

    /// <summary>Hover colours for a button: background, border and label (uGUI's tint can't change a border).</summary>
    public sealed class HoverStyle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Image background;
        public Image[] frame, dashes;
        public Text label;
        public Color normalBg, hoverBg, normalBorder, hoverBorder, normalText, hoverText;
        bool over, down;
        RectTransform labelRt;

        public void OnPointerEnter(PointerEventData e) { over = true; Apply(); }
        public void OnPointerExit(PointerEventData e) { over = false; down = false; Apply(); }
        public void OnPointerDown(PointerEventData e) { down = true; Apply(); }
        public void OnPointerUp(PointerEventData e) { down = false; Apply(); }

        void OnDisable() { over = down = false; Apply(); }

        void Apply()
        {
            var b = GetComponent<Button>();
            bool interactable = b == null || b.interactable;
            bool hot = over && interactable;
            if (background != null) background.color = hot ? hoverBg : normalBg;
            if (frame != null) foreach (Image i in frame) if (i != null) i.color = hot ? hoverBorder : normalBorder;
            if (dashes != null) foreach (Image i in dashes) if (i != null) i.color = hot ? hoverBorder : normalBorder;
            if (label != null)
            {
                label.color = hot ? hoverText : interactable ? normalText : UiKit.A(normalText, 0.45f);
                if (labelRt == null) labelRt = label.rectTransform;
                labelRt.anchoredPosition = new Vector2(0f, down && hot ? -1f : 0f);
            }
        }
    }

    /// <summary>A text field's border turns bone while it has focus.</summary>
    public sealed class FocusFrame : MonoBehaviour
    {
        public InputField field;
        public Image[] frame;
        bool shown;

        void Update()
        {
            bool f = field != null && field.isFocused;
            if (f == shown) return;
            shown = f;
            foreach (Image i in frame) if (i != null) i.color = f ? UiKit.Bone : UiKit.Line;
        }
    }
}
