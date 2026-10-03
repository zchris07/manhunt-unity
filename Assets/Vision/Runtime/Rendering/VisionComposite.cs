using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vision.Visibility;

namespace Vision.Rendering
{
    /// <summary>
    /// Owns the composite material and its look parameters, and injects <see cref="VisionCompositePass"/>
    /// into the target camera every frame (no renderer-asset edits needed).
    /// The "Look" values are live-tunable in game (F4 panel in <see cref="Vision.Player.VisionDebugHud"/>);
    /// at their defaults the image is the tuned look.
    /// </summary>
    public sealed class VisionComposite : MonoBehaviour
    {
        public enum DebugView { Final = 0, MaskRgb = 1, LitAmount = 2, SceneOnly = 3, Shadows = 4 }

        public Camera targetCamera;
        public Shader compositeShader;

        /// <summary>Live-tunable look values (the F4 panel). <see cref="Defaults"/> is the tuned look.</summary>
        [System.Serializable]
        public struct Look
        {
            [Tooltip("Global contrast around mid-grey (1 = unchanged).")]
            [Range(0f, 2f)] public float contrast;
            [Tooltip("Global saturation of every colour (1 = unchanged, 0 = greyscale).")]
            [Range(0f, 2f)] public float saturation;
            [Tooltip("Multiplier on everything lit (flashlight, campfires, lanterns).")]
            [Range(0f, 2f)] public float litBrightness;
            [Tooltip("Multiplier on the unlit ground and objects.")]
            [Range(0f, 2f)] public float unlitBrightness;
            [Tooltip("Strength of the flashlight beam (only the viewer's own light).")]
            [Range(0.5f, 2f)] public float beamIntensity;
            [Tooltip("Distance from the player (world units) where the distance blur starts.")]
            [Range(0f, 30f)] public float blurStart;
            [Tooltip("Distance from the player (world units) where the distance blur is full.")]
            [Range(0f, 30f)] public float blurEnd;
            [Tooltip("Full distance-blur radius in pixels (assets keep their pixel size at any resolution, so this does too).")]
            [Range(0f, 8f)] public float blurMaxPixels;
            [Tooltip("Camera effects: vignette, film grain, light flicker and the distance blur.")]
            public bool cameraEffects;

            public static Look Defaults => new Look
            {
                contrast = 1f, saturation = 1f, litBrightness = 1f, unlitBrightness = 1f, beamIntensity = 1.15f,
                blurStart = 5f, blurEnd = 13f, blurMaxPixels = 3f, cameraEffects = true,
            };

            /// <summary>Keeps every value inside its slider range (and the blur end past its start).</summary>
            public Look Clamped()
            {
                Look l = this;
                l.contrast = Mathf.Clamp(l.contrast, 0f, 2f);
                l.saturation = Mathf.Clamp(l.saturation, 0f, 2f);
                l.litBrightness = Mathf.Clamp(l.litBrightness, 0f, 2f);
                l.unlitBrightness = Mathf.Clamp(l.unlitBrightness, 0f, 2f);
                l.beamIntensity = Mathf.Clamp(l.beamIntensity, 0.5f, 2f);
                l.blurStart = Mathf.Clamp(l.blurStart, 0f, 30f);
                l.blurEnd = Mathf.Clamp(l.blurEnd, l.blurStart + 0.01f, 30.01f);
                l.blurMaxPixels = Mathf.Clamp(l.blurMaxPixels, 0f, 8f);
                return l;
            }

            /// <summary>All values as text, for pasting back.</summary>
            public override string ToString() =>
                $"contrast {contrast:0.00}, saturation {saturation:0.00}, lit brightness {litBrightness:0.00}, " +
                $"unlit brightness {unlitBrightness:0.00}, beam intensity {beamIntensity:0.00}, blur start {blurStart:0.00}, " +
                $"blur end {blurEnd:0.00}, blur max px {blurMaxPixels:0.00}, camera effects {(cameraEffects ? "on" : "off")}";
        }

        [Header("Look (live sliders, F4)")]
        public Look look = Look.Defaults;

        [Header("Unlit ground (faint grey, readable)")]
        [Tooltip("How much of the scene's luminance survives in the dark (the ~20% lift).")]
        [Range(0f, 1f)] public float darkLevel = 0.2f;
        [Range(0f, 0.1f)] public float darkFloor = 0.01f;
        [Tooltip("Near-neutral so unlit areas carry almost no colour.")]
        public Color darkTint = new Color(0.99f, 1f, 1.01f);

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

        [Header("Back sides of upright objects")]
        [Tooltip("Share of the light on an object's lit side that reaches its back (upright faces turned away from the light): a natural darkness instead of a solid black silhouette.")]
        [Range(0f, 1f)] public float backLight = 0.45f;
        [Tooltip("How far through the object (world units) the back face looks for the light on its other side; wider than a trunk, narrower than a wall is thick plus the light's edge.")]
        [Range(0f, 4f)] public float backLightReach = 1.8f;

        [Tooltip("How much a character shadow darkens lit ground (soft, partly transparent).")]
        [Range(0f, 1f)] public float shadowStrength = 0.45f;

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
        static readonly int ShadowId = Shader.PropertyToID("_VisShadow");
        static readonly int GradeId = Shader.PropertyToID("_VisGrade");
        static readonly int BeamId = Shader.PropertyToID("_VisBeam");
        static readonly int BlurId = Shader.PropertyToID("_VisBlur");

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
            Look l = look.Clamped();
            bool fx = l.cameraEffects;
            VisionLight.FlickerEnabled = fx;
            material.SetVector(GradeId, new Vector4(l.contrast, l.saturation, l.litBrightness, l.unlitBrightness));
            material.SetVector(BeamId, new Vector4(l.beamIntensity, backLight, backLightReach, 0f));
            material.SetVector(BlurId, new Vector4(l.blurStart, l.blurEnd, fx ? l.blurMaxPixels : 0f, 0f));
            material.SetVector(DarkId, new Vector4(darkLevel, darkFloor, normalOffset, 0f));
            material.SetColor(DarkTintId, darkTint);
            material.SetColor(SourceTintId, sourceTint);
            material.SetColor(ViewerTintId, viewerTint * exposure);
            material.SetVector(LookId, new Vector4(fx ? vignette : 0f, fx ? grain : 0f, sightlineEdge.x, sightlineEdge.y));
            material.SetFloat(DebugId, (float)debugView);
            material.SetFloat(ShadowId, shadowStrength);
            cam.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
        }
    }
}
