using UnityEngine;
using UnityEngine.InputSystem;
using Vision.Rendering;
using Vision.Visibility;

namespace Vision.Player
{
    /// <summary>
    /// FPS and vision stats overlay. F1 polygon outlines, F2 cycles the composite debug view
    /// (final / mask RGB / lit amount / raw scene / shadows), F3 hides this panel.
    /// F4 opens the look panel: live sliders for contrast, saturation, lit and unlit brightness, beam
    /// intensity and the distance blur, each showing its value, with Reset and "Copy values" (to the
    /// clipboard and the log). F5 toggles all camera effects (vignette, grain, flicker, distance blur).
    /// Look values persist between runs.
    /// </summary>
    public sealed class VisionDebugHud : MonoBehaviour
    {
        public VisionMaskRenderer maskRenderer;
        public VisionComposite composite;
        public bool visible = true;
        public bool lookPanel;

        const string PrefsKey = "Vision.Look";
        static Rect panelRect;
        static bool panelOpen;

        float smoothedDelta = 1f / 60f;
        GUIStyle style, valueStyle;
        string copiedNote;
        float copiedUntil;

        /// <summary>True while the pointer (input-system screen position, origin bottom left) is over the look panel.</summary>
        public static bool PointerOverPanel(Vector2 screen) => panelOpen && panelRect.Contains(new Vector2(screen.x, Screen.height - screen.y));

        void Awake()
        {
            if (composite == null) return;
            try
            {
                string json = PlayerPrefs.GetString(PrefsKey, "");
                if (!string.IsNullOrEmpty(json))
                {
                    composite.look = JsonUtility.FromJson<VisionComposite.Look>(json).Clamped();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Vision] Could not load look settings: {e.Message}");
            }
        }

        void SaveLook()
        {
            try
            {
                PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(composite.look));
                PlayerPrefs.Save();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Vision] Could not save look settings: {e.Message}");
            }
        }

        void OnDisable() => panelOpen = false;

        void Update()
        {
            smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, 0.05f);
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame && maskRenderer != null) maskRenderer.drawPolygons = !maskRenderer.drawPolygons;
            if (kb.f2Key.wasPressedThisFrame && composite != null)
                composite.debugView = (VisionComposite.DebugView)(((int)composite.debugView + 1) % 5);
            if (kb.f3Key.wasPressedThisFrame) visible = !visible;
            if (kb.f4Key.wasPressedThisFrame) lookPanel = !lookPanel;
            if (kb.f5Key.wasPressedThisFrame && composite != null)
            {
                composite.look.cameraEffects = !composite.look.cameraEffects;
                SaveLook();
            }
            panelOpen = lookPanel && composite != null;
        }

        void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = new Color(0.85f, 0.85f, 0.8f) } };
            if (panelOpen) DrawLookPanel();
            if (!visible) return;
            string stats = maskRenderer == null ? "" :
                $"polygons {maskRenderer.LastPolygonCount} (lights {maskRenderer.LastLightPolygonCount}/{VisionWorld.Lights.Count})  " +
                $"rays {maskRenderer.LastRayCount}  shadows {maskRenderer.LastShadowCount}  segments {VisionWorld.Occluders.ActiveSegmentCount}  " +
                $"occluder v{VisionWorld.Occluders.Version}  mask {(maskRenderer.MaskTexture != null ? maskRenderer.MaskTexture.width : 0)}px";
            string view = composite != null ? composite.debugView.ToString() : "-";
            GUI.Label(new Rect(12, 8, 1200, 22), $"{1f / smoothedDelta:0} fps  {smoothedDelta * 1000f:0.0} ms   {stats}", style);
            GUI.Label(new Rect(12, 28, 1200, 22),
                $"WASD move  Shift run  mouse aim  E door/shutter  F see-through cone  F1 polygons  F2 view: {view}  F3 hide  " +
                $"F4 look panel  F5 camera effects: {(composite != null && composite.look.cameraEffects ? "on" : "off")}", style);
        }

        void DrawLookPanel()
        {
            valueStyle ??= new GUIStyle(style) { alignment = TextAnchor.MiddleRight };
            const float width = 400f, row = 26f;
            panelRect = new Rect(Screen.width - width - 12f, 56f, width, 12 * row + 20f);
            // Drawn twice for a darker backdrop behind the sliders.
            GUI.Box(panelRect, GUIContent.none);
            GUI.Box(panelRect, GUIContent.none);
            float y = panelRect.y + 8f;
            GUI.Label(new Rect(panelRect.x + 10f, y, width - 20f, row), "Look (F4 to close)", style);
            y += row;

            VisionComposite.Look l = composite.look;
            VisionComposite.Look before = l;
            l.contrast = Slider("Contrast", l.contrast, 0f, 2f, ref y);
            l.saturation = Slider("Saturation", l.saturation, 0f, 2f, ref y);
            l.litBrightness = Slider("Lit brightness", l.litBrightness, 0f, 2f, ref y);
            l.unlitBrightness = Slider("Unlit brightness", l.unlitBrightness, 0f, 2f, ref y);
            l.beamIntensity = Slider("Beam intensity", l.beamIntensity, 0.5f, 2f, ref y);
            l.blurStart = Slider("Blur start (m)", l.blurStart, 0f, 30f, ref y);
            l.blurEnd = Slider("Blur end (m)", l.blurEnd, 0f, 30f, ref y);
            l.blurMaxPixels = Slider("Blur max (px)", l.blurMaxPixels, 0f, 8f, ref y);
            l.cameraEffects = GUI.Toggle(new Rect(panelRect.x + 10f, y, width - 20f, row), l.cameraEffects,
                $" Camera effects (vignette, grain, flicker, blur): {(l.cameraEffects ? "on" : "off")}   F5");
            y += row + 4f;

            if (GUI.Button(new Rect(panelRect.x + 10f, y, 110f, row - 2f), "Reset")) l = VisionComposite.Look.Defaults;
            if (GUI.Button(new Rect(panelRect.x + 130f, y, 130f, row - 2f), "Copy values"))
            {
                string text = l.ToString();
                GUIUtility.systemCopyBuffer = text;
                Debug.Log($"[Vision] Look values: {text}");
                copiedNote = "Copied to clipboard";
                copiedUntil = Time.unscaledTime + 2f;
            }
            if (Time.unscaledTime < copiedUntil) GUI.Label(new Rect(panelRect.x + 270f, y, width - 280f, row), copiedNote, style);

            if (!l.Equals(before))
            {
                composite.look = l.Clamped();
                SaveLook();
            }
        }

        float Slider(string label, float value, float min, float max, ref float y)
        {
            float x = panelRect.x + 10f;
            GUI.Label(new Rect(x, y, 140f, 24f), label, style);
            value = GUI.HorizontalSlider(new Rect(x + 140f, y + 7f, 180f, 16f), value, min, max);
            GUI.Label(new Rect(x + 320f, y, 60f, 24f), value.ToString("0.00"), valueStyle);
            y += 26f;
            return value;
        }
    }
}
