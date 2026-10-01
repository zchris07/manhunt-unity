using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Vision.Rendering
{
    /// <summary>
    /// Full-screen Render Graph pass that turns the camera image into the final look: lit ground in
    /// full colour scaled by the lit amount, everything else a faint desaturated grey.
    /// lit = max(B, R * smoothstep(G)); out = mix(grey, scene * lit, smoothstep(lit)).
    /// Each pixel's world position is reconstructed from the depth texture so the world-space mask
    /// lines up with the pitched orthographic camera.
    /// </summary>
    public sealed class VisionCompositePass : ScriptableRenderPass
    {
        static readonly int CamPosId = Shader.PropertyToID("_VisCamPos");
        static readonly int CamRightId = Shader.PropertyToID("_VisCamRight");
        static readonly int CamUpId = Shader.PropertyToID("_VisCamUp");
        static readonly int CamFwdId = Shader.PropertyToID("_VisCamFwd");
        static readonly int CamClipId = Shader.PropertyToID("_VisCamClip");

        readonly Material material;

        class PassData
        {
            public TextureHandle source;
            public Material material;
        }

        public VisionCompositePass(Material material)
        {
            this.material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            Camera cam = cameraData.camera;
            Transform t = cam.transform;
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            material.SetVector(CamPosId, t.position);
            material.SetVector(CamRightId, t.right * halfW);
            material.SetVector(CamUpId, t.up * halfH);
            material.SetVector(CamFwdId, t.forward);
            material.SetVector(CamClipId, new Vector4(cam.nearClipPlane, cam.farClipPlane, 0f, 0f));

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc desc = renderGraph.GetTextureDesc(source);
            desc.name = "Vision Composite";
            desc.clearBuffer = false;
            desc.depthBufferBits = DepthBits.None;
            TextureHandle destination = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Vision Composite", out PassData data))
            {
                data.source = source;
                data.material = material;
                builder.UseTexture(source);
                if (resourceData.cameraDepthTexture.IsValid()) builder.UseTexture(resourceData.cameraDepthTexture);
                builder.SetRenderAttachment(destination, 0);
                builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                    Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), d.material, 0));
            }

            resourceData.cameraColor = destination;
        }
    }
}
