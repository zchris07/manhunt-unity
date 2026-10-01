using UnityEngine;
using UnityEngine.InputSystem;
using Vision.Rendering;
using Vision.Visibility;

namespace Vision.Player
{
    /// <summary>
    /// FPS and vision stats overlay. F1 polygon outlines, F2 cycles the composite debug view
    /// (final / mask RGB / lit amount / raw scene), F3 hides this panel.
    /// </summary>
    public sealed class VisionDebugHud : MonoBehaviour
    {
        public VisionMaskRenderer maskRenderer;
        public VisionComposite composite;
        public bool visible = true;

        float smoothedDelta = 1f / 60f;
        GUIStyle style;

        void Update()
        {
            smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, 0.05f);
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame && maskRenderer != null) maskRenderer.drawPolygons = !maskRenderer.drawPolygons;
            if (kb.f2Key.wasPressedThisFrame && composite != null)
                composite.debugView = (VisionComposite.DebugView)(((int)composite.debugView + 1) % 4);
            if (kb.f3Key.wasPressedThisFrame) visible = !visible;
        }

        void OnGUI()
        {
            if (!visible) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = new Color(0.85f, 0.85f, 0.8f) } };
            string stats = maskRenderer == null ? "" :
                $"polygons {maskRenderer.LastPolygonCount} (lights {maskRenderer.LastLightPolygonCount}/{VisionWorld.Lights.Count})  " +
                $"rays {maskRenderer.LastRayCount}  segments {VisionWorld.Occluders.ActiveSegmentCount}  " +
                $"occluder v{VisionWorld.Occluders.Version}  mask {(maskRenderer.MaskTexture != null ? maskRenderer.MaskTexture.width : 0)}px";
            string view = composite != null ? composite.debugView.ToString() : "-";
            GUI.Label(new Rect(12, 8, 1200, 22), $"{1f / smoothedDelta:0} fps  {smoothedDelta * 1000f:0.0} ms   {stats}", style);
            GUI.Label(new Rect(12, 28, 1200, 22),
                $"WASD move  Shift run  mouse aim  E door/shutter  F see-through cone  F1 polygons  F2 view: {view}  F3 hide", style);
        }
    }
}
