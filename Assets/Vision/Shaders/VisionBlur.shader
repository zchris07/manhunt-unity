// One direction of a separable 9-tap Gaussian blur over the vision mask, with a radius that grows with
// distance from the light: a penumbra. Shadows of trees, rocks, walls and characters are sharp near the
// light and soften farther away. B, G and A (the viewer's light, line of sight, character shadows) blur
// with distance from the viewer; R (light sources) with distance from the nearest light source.
// Drawn as a procedural full-screen triangle over the mask.
Shader "Hidden/Vision/Blur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "VisionBlur"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "VisionCommon.hlsl"

            #define MAX_BLUR_LIGHTS 8

            TEXTURE2D(_VisionBlurSource);
            SAMPLER(sampler_VisionBlurSource);
            float4 _VisionBlurStep;      // xy = unit direction in uv per texel, z = base radius (texels), w = texel size (world)
            float4 _VisionBlurPenumbra;  // x = growth (world blur per world unit from the light), y = max (world), z = taps per radius, w = light count
            float4 _VisionBlurOrigins[MAX_BLUR_LIGHTS + 1];   // [0] = viewer, then light sources (world X,Z)

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(uint id : SV_VertexID)
            {
                Varyings o;
                float2 uv = float2((id << 1) & 2, id & 2);   // (0,0) (2,0) (0,2)
                o.uv = uv;
                o.positionCS = VisionMaskClip(uv);
                return o;
            }

            // Tap spacing in texels for a penumbra at this distance from its light.
            float StepTexels(float distance)
            {
                float world = min(_VisionBlurPenumbra.x * distance, _VisionBlurPenumbra.y);
                return max(_VisionBlurStep.z, world / _VisionBlurStep.w * _VisionBlurPenumbra.z);
            }

            half4 Blur(float2 uv, float2 step)
            {
                static const float w[5] = { 0.2270270, 0.1945946, 0.1216216, 0.0540541, 0.0162162 };
                half4 c = SAMPLE_TEXTURE2D_LOD(_VisionBlurSource, sampler_VisionBlurSource, uv, 0) * w[0];
                [unroll]
                for (int k = 1; k < 5; k++)
                {
                    c += SAMPLE_TEXTURE2D_LOD(_VisionBlurSource, sampler_VisionBlurSource, uv + step * k, 0) * w[k];
                    c += SAMPLE_TEXTURE2D_LOD(_VisionBlurSource, sampler_VisionBlurSource, uv - step * k, 0) * w[k];
                }
                return c;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 world = _VisionMaskRect.xy + i.uv / _VisionMaskRect.zw;
                float dViewer = distance(world, _VisionBlurOrigins[0].xy);
                float dLight = 1e4;
                int lights = (int)_VisionBlurPenumbra.w;
                for (int k = 0; k < MAX_BLUR_LIGHTS; k++)
                    if (k < lights) dLight = min(dLight, distance(world, _VisionBlurOrigins[k + 1].xy));

                half4 viewerBlur = Blur(i.uv, _VisionBlurStep.xy * StepTexels(dViewer));
                half4 lightBlur = Blur(i.uv, _VisionBlurStep.xy * StepTexels(lights > 0 ? dLight : dViewer));
                return half4(lightBlur.r, viewerBlur.g, viewerBlur.b, viewerBlur.a);
            }
            ENDHLSL
        }
    }
}
