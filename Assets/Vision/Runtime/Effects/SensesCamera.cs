using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Vision.Effects
{
    /// <summary>
    /// Draws the "senses" layer above the vision mask, as the original draws its senses over the darkness: an overlay
    /// camera stacked on the main camera, matching its view every frame, drawing only <see cref="Vfx.SensesLayer"/> (which
    /// the main camera skips). The Soundcloud Burst's wave and the like.
    /// Before it, a second overlay draws <see cref="Vfx.OverGroundLayer"/> (the laser and rounds in flight): it keeps the
    /// scene's depth and stencil, so they show over the ground (which marks its pixels) and behind whatever else is nearer,
    /// and it comes after the vision composite, so the darkness never covers them.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(Camera))]
    public sealed class SensesCamera : MonoBehaviour
    {
        Camera main, overlay, overGround;

        public Camera Overlay => overlay;
        public Camera OverGround => overGround;

        void OnEnable() => Ensure();

        public void Ensure()
        {
            if (overlay != null) return;
            main = GetComponent<Camera>();
            main.cullingMask &= ~((1 << Vfx.SensesLayer) | (1 << Vfx.OverGroundLayer));
            var mainData = main.GetUniversalAdditionalCameraData();

            overGround = Make("Over Ground Camera", Vfx.OverGroundLayer);
            // The scene's depth and stencil stay: what it draws is judged against them (Vision/FxOverGround). (URP's
            // Clear Depth has no setter in code, only its serialized field.)
            typeof(UniversalAdditionalCameraData).GetField("m_ClearDepth", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(overGround.GetUniversalAdditionalCameraData(), false);
            overlay = Make("Senses Camera", Vfx.SensesLayer);
            if (!mainData.cameraStack.Contains(overGround)) mainData.cameraStack.Add(overGround);
            if (!mainData.cameraStack.Contains(overlay)) mainData.cameraStack.Add(overlay);
            Match();
        }

        Camera Make(string name, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            Camera cam = go.AddComponent<Camera>();
            cam.cullingMask = 1 << layer;
            cam.clearFlags = CameraClearFlags.Nothing;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Overlay;
            data.renderPostProcessing = false;
            return cam;
        }

        void Match()
        {
            Match(overlay);
            Match(overGround);
        }

        void Match(Camera c)
        {
            c.orthographic = main.orthographic;
            c.orthographicSize = main.orthographicSize;
            c.fieldOfView = main.fieldOfView;
            c.nearClipPlane = main.nearClipPlane;
            c.farClipPlane = main.farClipPlane;
        }

        /// <summary>Past the edge of the woods the screen goes black: the senses go with everything else.</summary>
        public static float Blackout;

        void LateUpdate()
        {
            if (overlay == null) Ensure();
            Match();
            overlay.enabled = overGround.enabled = Blackout < 0.9f;
        }

        void OnDestroy()
        {
            if (main != null && overlay != null)
            {
                var mainData = main.GetUniversalAdditionalCameraData();
                mainData.cameraStack.Remove(overlay);
                mainData.cameraStack.Remove(overGround);
            }
        }
    }
}
