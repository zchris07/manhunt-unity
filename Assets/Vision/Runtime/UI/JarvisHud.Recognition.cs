using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vision.UI
{
    /// <summary>
    /// JARVIS's recognition, like face recognition software's overlay: everyone and everything it picks out on the screen
    /// (players, NPCs, weapons, items, generators, traps) gets a box round it with bright brackets at its corners and a
    /// faint outline between them, a name plate under it in the box's colour (the name typing out as it locks on) and a
    /// line of readout over it (a match percentage, THREAT for Zach, what an item is, how far a generator's got). Each box
    /// locks on as it's picked out (closing in from wide, a scan line sweeping down it), and follows its target round the
    /// screen. Cyan for people, red for Zach, orange for weapons, green for items, amber for generators.
    /// </summary>
    public sealed partial class JarvisHud
    {
        /// <summary>What a recognised thing is (its box's colour and readout).</summary>
        public enum ScanKind { Person, Threat, Weapon, Item, Generator }

        /// <summary>
        /// Something JARVIS picks out: a key to follow it by, its name and readout, its kind, and its shape in the level's own
        /// space (metres): an upright cylinder standing on <see cref="Base"/> as wide and as tall as the body or the thing
        /// (its box on the screen hugs that, as a 3D box's corners never would under the tilted camera).
        /// </summary>
        public struct Scan
        {
            public int Key;
            public string Name, Detail;
            public ScanKind Kind;
            public Vector3 Base;
            public float Radius, Height;
            public float Distance;
            /// <summary>The thing's own model, if it has one: its box fits the model's outline exactly (else the cylinder).</summary>
            public GameObject Model;

            /// <summary>Its bounding corners (for checks).</summary>
            public Vector3 Lo => Base - new Vector3(Radius, 0f, Radius);
            public Vector3 Hi => Base + new Vector3(Radius, Height, Radius);
        }

        /// <summary>The box's margin round what it frames (canvas units), and its least size.</summary>
        public const float Margin = 3f, MinBox = 16f;

        /// <summary>The most boxes up at once (the nearest first).</summary>
        public const int MaxLocks = 16;
        /// <summary>How long a box takes to lock on, and its name to type out.</summary>
        public const float LockTime = 0.35f, TypeTime = 0.45f;

        sealed class Lock
        {
            public RectTransform Box, Plate, ScanLine;
            public Image[] Corners, Edges;
            public Image PlateBg, ScanImage;
            public Text Name, Detail;
            public int Key;
            public string Full;
            public float PlateWidth;
            public float Since;
            public bool Used;
        }

        RectTransform recognition;
        readonly List<Lock> locks = new List<Lock>();
        readonly Dictionary<int, Lock> byKey = new Dictionary<int, Lock>();

        /// <summary>How many boxes are up, and what they say (tests).</summary>
        public int LockCount { get { int n = 0; foreach (Lock l in locks) if (l.Used) n++; return n; } }
        public IEnumerable<string> LockedNames { get { foreach (Lock l in locks) if (l.Used) yield return l.Full; } }

        public static Color KindColor(ScanKind k) => k switch
        {
            ScanKind.Threat => new Color(1f, 0.22f, 0.18f, 1f),
            ScanKind.Weapon => new Color(1f, 0.62f, 0.25f, 1f),
            ScanKind.Item => new Color(0.5f, 1f, 0.68f, 1f),
            ScanKind.Generator => new Color(1f, 0.85f, 0.3f, 1f),
            _ => Blue,
        };

        Lock NewLock()
        {
            RectTransform box = UiKit.Node("Lock", recognition, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            var l = new Lock { Box = box, Corners = new Image[8], Edges = new Image[4] };
            // The faint outline between the corners.
            Vector2[] edgeMin = { new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 0f) };
            Vector2[] edgeMax = { new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            for (int i = 0; i < 4; i++)
            {
                RectTransform e = UiKit.Node("Edge", box, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                e.anchorMin = edgeMin[i];
                e.anchorMax = edgeMax[i];
                e.sizeDelta = i < 2 ? new Vector2(0f, 1.5f) : new Vector2(1.5f, 0f);
                e.anchoredPosition = Vector2.zero;
                l.Edges[i] = UiKit.Box(e, Color.white);
            }
            // The brackets: an L at each corner, a bar along and a bar up from it.
            Vector2[] corners = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            for (int i = 0; i < 4; i++)
            {
                Vector2 c = corners[i];
                l.Corners[i * 2] = UiKit.Box(UiKit.Node("Bracket", box, c, c, Vector2.zero, new Vector2(20f, 4f)), Color.white);
                l.Corners[i * 2 + 1] = UiKit.Box(UiKit.Node("Bracket", box, c, c, Vector2.zero, new Vector2(4f, 20f)), Color.white);
            }
            // The scan line sweeping down it as it locks on.
            l.ScanLine = UiKit.Node("Scan", box, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 2f));
            l.ScanLine.anchorMin = new Vector2(0f, 1f);
            l.ScanLine.anchorMax = new Vector2(1f, 1f);
            l.ScanLine.sizeDelta = new Vector2(0f, 2f);
            l.ScanImage = UiKit.Box(l.ScanLine, Color.white);
            // The name plate under it, and the readout over it.
            l.Plate = UiKit.Node("Plate", box, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, -5f), new Vector2(120f, 24f));
            l.PlateBg = UiKit.Box(l.Plate, Color.white);
            l.Name = kit.Label(UiKit.Fill("Name", l.Plate), "", 17, TextAnchor.MiddleLeft, Color.white, UiKit.Face.Mono, 0f, false);
            l.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
            l.Name.rectTransform.offsetMin = new Vector2(8f, 0f);
            l.Detail = kit.Label(UiKit.Node("Detail", box, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 5f), new Vector2(360f, 18f)), "", 14, TextAnchor.LowerLeft, Color.white, UiKit.Face.Mono, 0f, true);
            l.Detail.horizontalOverflow = HorizontalWrapMode.Overflow;
            foreach (Graphic g in box.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            locks.Add(l);
            return l;
        }

        /// <summary>
        /// Marks what JARVIS picks out this frame (the nearest <see cref="MaxLocks"/> on the screen), its boxes projected
        /// through <paramref name="cam"/> from <paramref name="space"/> (the level); null or empty clears them.
        /// </summary>
        public void Recognise(List<Scan> scans, Camera cam, Transform space)
        {
            if (recognition == null)
            {
                if (hud == null) return;
                recognition = UiKit.Fill("Recognition", hud);
                recognition.SetAsFirstSibling();
            }
            foreach (Lock l in locks) l.Used = false;
            float now = Time.unscaledTime;
            if (scans != null && cam != null && space != null)
            {
                scans.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                int shown = 0;
                foreach (Scan s in scans)
                {
                    if (shown >= MaxLocks) break;
                    if (!ScreenRect(s, cam, space, out Rect screen)) continue;
                    if (!byKey.TryGetValue(s.Key, out Lock l) || l.Key != s.Key)
                    {
                        l = null;
                        foreach (Lock f in locks) if (!f.Used && !byKey.ContainsValue(f)) { l = f; break; }
                        if (l == null) l = NewLock();
                        l.Key = s.Key;
                        l.Since = now;
                        byKey[s.Key] = l;
                    }
                    l.Used = true;
                    shown++;
                    Place(l, s, screen, now);
                }
            }
            // Boxes no longer wanted: hidden, and free for something else.
            var drop = new List<int>();
            foreach (var kv in byKey) if (!kv.Value.Used) drop.Add(kv.Key);
            foreach (int k in drop) byKey.Remove(k);
            foreach (Lock l in locks) if (l.Box.gameObject.activeSelf != l.Used) l.Box.gameObject.SetActive(l.Used);
        }

        /// <summary>
        /// A scan's box on the screen (pixels): its cylinder's outline (rings round its foot, its middle and its top),
        /// or false if it's behind the camera or off the screen.
        /// </summary>
        public static bool ScreenRect(Scan s, Camera cam, Transform space, out Rect r)
        {
            r = default;
            Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = -lo;
            if (s.Model != null && ModelOutline(s.Model, cam, ref lo, ref hi))
            {
                if (hi.x < 0f || hi.y < 0f || lo.x > cam.pixelWidth || lo.y > cam.pixelHeight) return false;
                r = Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y);
                return true;
            }
            const int around = 12;
            for (int ring = 0; ring < 3; ring++)
            {
                float y = s.Height * ring * 0.5f;
                for (int i = 0; i < around; i++)
                {
                    float a = i * Mathf.PI * 2f / around;
                    var p = s.Base + new Vector3(Mathf.Cos(a) * s.Radius, y, Mathf.Sin(a) * s.Radius);
                    Vector3 sp = cam.WorldToScreenPoint(space.TransformPoint(p));
                    if (sp.z <= 0f) return false;
                    lo = Vector2.Min(lo, sp);
                    hi = Vector2.Max(hi, sp);
                }
            }
            if (hi.x < 0f || hi.y < 0f || lo.x > cam.pixelWidth || lo.y > cam.pixelHeight) return false;
            r = Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y);
            return true;
        }

        static readonly Dictionary<Mesh, Vector3[]> meshPoints = new Dictionary<Mesh, Vector3[]>();

        /// <summary>
        /// A model's outline on the screen: every vertex of its drawn meshes projected (a big mesh sampled), or false if it
        /// has none to read or any of it is behind the camera.
        /// </summary>
        static bool ModelOutline(GameObject model, Camera cam, ref Vector2 lo, ref Vector2 hi)
        {
            bool any = false;
            foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = mf.sharedMesh;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mesh == null || mr == null || !mr.enabled || !mesh.isReadable) continue;
                if (!meshPoints.TryGetValue(mesh, out Vector3[] pts)) meshPoints[mesh] = pts = mesh.vertices;
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                int step = Mathf.Max(1, pts.Length / 600);
                for (int i = 0; i < pts.Length; i += step)
                {
                    Vector3 sp = cam.WorldToScreenPoint(m.MultiplyPoint3x4(pts[i]));
                    if (sp.z <= 0f) return false;
                    lo = Vector2.Min(lo, sp);
                    hi = Vector2.Max(hi, sp);
                    any = true;
                }
            }
            return any;
        }

        void Place(Lock l, Scan s, Rect screen, float now)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen.min, null, out Vector2 a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen.max, null, out Vector2 b);
            Vector2 size = Vector2.Max(b - a + new Vector2(Margin, Margin) * 2f, new Vector2(MinBox, MinBox));
            float age = now - l.Since, k = Ease(age / LockTime);
            // Locking on: closing in from wide, brightening.
            l.Box.anchoredPosition = (a + b) * 0.5f;
            l.Box.sizeDelta = size * Mathf.Lerp(1.6f, 1f, k);
            Color c = KindColor(s.Kind);
            float arm = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.26f, 9f, 30f);
            for (int i = 0; i < 8; i++)
            {
                l.Corners[i].rectTransform.sizeDelta = i % 2 == 0 ? new Vector2(arm, 4f) : new Vector2(4f, arm);
                l.Corners[i].color = A(c, 0.35f + 0.65f * k);
            }
            foreach (Image e in l.Edges) e.color = A(c, 0.28f * k);
            // The scan line sweeping down it as it locks, then again now and then.
            float sweep = age < 0.6f ? age / 0.6f : Mathf.Repeat(age - 0.6f, 2.6f) / 0.6f;
            bool sweeping = sweep < 1f;
            l.ScanImage.color = A(c, sweeping ? 0.55f * (1f - sweep * 0.5f) : 0f);
            l.ScanLine.anchoredPosition = new Vector2(0f, -l.Box.sizeDelta.y * Mathf.Clamp01(sweep));
            // The name typing out on its plate; the readout over the box.
            if (l.Full != s.Name)
            {
                // The plate fits the whole name (measured once, before it types out).
                l.Full = s.Name;
                l.Name.text = s.Name;
                l.PlateWidth = l.Name.preferredWidth + 18f;
            }
            int n = Mathf.Clamp(Mathf.CeilToInt(age / TypeTime * s.Name.Length), 0, s.Name.Length);
            l.Name.text = s.Name.Substring(0, n);
            l.Plate.sizeDelta = new Vector2(Mathf.Max(l.Box.sizeDelta.x, l.PlateWidth), 24f);
            l.PlateBg.color = A(c * 0.75f, 0.85f * k);
            l.Name.color = A(Color.white, k);
            l.Detail.text = s.Detail;
            l.Detail.color = A(c, k);
        }
    }
}
