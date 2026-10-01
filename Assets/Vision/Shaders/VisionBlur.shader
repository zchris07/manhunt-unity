// One direction of a separable 9-tap Gaussian blur over the vision mask.
// Drawn as a procedural full-screen triangle; _VisionBlurStep is the uv step for one tap.
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

            TEXTURE2D(_VisionBlurSource);
            SAMPLER(sampler_VisionBlurSource);
            float4 _VisionBlurStep;

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

            half4 frag(Varyings i) : SV_Target
            {
                static const float w[5] = { 0.2270270, 0.1945946, 0.1216216, 0.0540541, 0.0162162 };
                float2 s = _VisionBlurStep.xy;
                half4 c = SAMPLE_TEXTURE2D_LOD(_VisionBlurSource, sampler_VisionBlurSource, i.uv, 0) * w[0];
                [unroll]
                for (int k = 1; k < 5; k++)
                {
                    c += SAMPLE_TEXTURE2D_LOD(_VisionBlurSource, sampler_VisionBlurSource, i.uv + s * k, 0) * w[k];
                    c += SAMPLE_TEXTURE2D_LOD(_VisionBlurSource, sampler_VisionBlurSource, i.uv - s * k, 0) * w[k];
                }
                return c;
            }
            ENDHLSL
        }
    }
}
