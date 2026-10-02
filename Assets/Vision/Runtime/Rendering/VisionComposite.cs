using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Vision.Rendering
{
    /// <summary>
    /// Owns the composite material and its look parameters, and injects <see cref="VisionCompositePass"/>
    /// into the target camera every frame (no renderer-asset edits needed).
    /// </summary>
    public sealed class VisionComposite : MonoBehaviour
    {
        public enum DebugView { Final = 0, MaskRgb = 1, LitAmount = 2, SceneOnly = 3 }

        public Camera targetCamera;
        public Shader compositeShader;

        [Header("Unlit ground (faint grey, readable)")]
        [Tooltip("How much of the scene's luminance survives in the dark (the ~20% lift).")]
        [Range(0f, 1f)] public float darkLevel = 0.2f;
        [Range(0f, 0.1f)] public float darkFloor = 0.01f;
        public Color darkTint = new Color(0.82f, 0.86f, 0.92f);

        [Header("Lit ground")]
        [Tooltip("Colour of the viewer's own light (flashlight, proximity).")]
        public Color viewerTint = new Color(1f, 0.94f, 0.82f);
        [Tooltip("Colour multiplied into ground lit by light sources (R) rather than the viewer (B).")]
        public Color sourceTint = new Color(1f, 0.72f, 0.42f);
        [Range(0.5f, 3f)] public float exposure = 1.35f;
        [Tooltip("smoothstep(min, max, G) gates light sources by line of sight.")]
        public Vector2 sightlineEdge = new Vector2(0.15f, 0.6f);
        [Tooltip("Normal offset (metres) for the mask lookup so walls facing a light pick it up.")]
        [Range(0f, 0.5f)] public float normalOffset = 0.3f;

        [Header("Film")]
        [Range(0f, 1.5f)] public float vignette = 0.9f;
        [Range(0f, 0.2f)] public float grain = 0.03f;

        public DebugView debugView = DebugView.Final;

        static readonly int DarkId = Shader.PropertyToID("_VisDark");
        static readonly int DarkTintId = Shader.PropertyToID("_VisDarkTint");
        static readonly int SourceTintId = Shader.PropertyToID("_VisSourceTint");
        static readonly int ViewerTintId = Shader.PropertyToID("_VisViewerTint");
        static readonly int LookId = Shader.PropertyToID("_VisLook");
        static readonly int DebugId = Shader.PropertyToID("_VisDebug");

        Material material;
        VisionCompositePass pass;

        void OnEnable()
        {
            material = CoreUtils.CreateEngineMaterial(compositeShader);
            pass = new VisionCompositePass(material);
            RenderPipelineManager.beginCameraRendering += Inject;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Inject;
            CoreUtils.Destroy(material);
            material = null;
            pass = null;
        }

        void Inject(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != targetCamera || pass == null) return;
            material.SetVector(DarkId, new Vector4(darkLevel, darkFloor, normalOffset, 0f));
            material.SetColor(DarkTintId, darkTint);
            material.SetColor(SourceTintId, sourceTint);
            material.SetColor(ViewerTintId, viewerTint * exposure);
            material.SetVector(LookId, new Vector4(vignette, grain, sightlineEdge.x, sightlineEdge.y));
            material.SetFloat(DebugId, (float)debugView);
            cam.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
        }
    }
}
