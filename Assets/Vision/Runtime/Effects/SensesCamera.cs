using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Vision.Effects
{
    /// <summary>
    /// Draws the "senses" layer above the vision mask, as the original draws its senses over the darkness: an overlay
    /// camera stacked on the main camera, matching its view every frame, drawing only <see cref="Vfx.SensesLayer"/> (which
    /// the main camera skips). Breathing heard through a wardrobe door, the Soundcloud Burst's wave and the like.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(Camera))]
    public sealed class SensesCamera : MonoBehaviour
    {
        Camera main, overlay;

        public Camera Overlay => overlay;

        void OnEnable() => Ensure();

        public void Ensure()
        {
            if (overlay != null) return;
            main = GetComponent<Camera>();
            main.cullingMask &= ~(1 << Vfx.SensesLayer);
            var go = new GameObject("Senses Camera");
            go.transform.SetParent(transform, false);
            overlay = go.AddComponent<Camera>();
            overlay.cullingMask = 1 << Vfx.SensesLayer;
            overlay.clearFlags = CameraClearFlags.Nothing;
            var data = overlay.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Overlay;
            data.renderPostProcessing = false;
            var mainData = main.GetUniversalAdditionalCameraData();
            if (!mainData.cameraStack.Contains(overlay)) mainData.cameraStack.Add(overlay);
            Match();
        }

        void Match()
        {
            overlay.orthographic = main.orthographic;
            overlay.orthographicSize = main.orthographicSize;
            overlay.fieldOfView = main.fieldOfView;
            overlay.nearClipPlane = main.nearClipPlane;
            overlay.farClipPlane = main.farClipPlane;
        }

        void LateUpdate()
        {
            if (overlay == null) Ensure();
            Match();
        }

        void OnDestroy()
        {
            if (main != null && overlay != null)
            {
                var mainData = main.GetUniversalAdditionalCameraData();
                mainData.cameraStack.Remove(overlay);
            }
        }
    }
}
