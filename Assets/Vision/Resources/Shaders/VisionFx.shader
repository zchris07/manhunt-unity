// Effects: unlit vertex colour times a tint, alpha-blended or additive, no depth writes, both sides. With _Masked set
// they behave like entities and show only inside the viewer's own light (sparks, splinters, the scent trail); without
// it (the senses overlay) they show everywhere.
Shader "Vision/Fx"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        [Toggle(_VISION_MASKED)] _Masked ("Only inside the viewer's light", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Z Test", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Fx"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _VISION_MASKED
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_VisionMask);
            SAMPLER(sampler_VisionMask);
            float4 _VisionMaskRect;
            float _VisionEntityThreshold;

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = i.color * _Tint;
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = i.color;
                // uv.x 6-7: a glow, bright in the middle and falling off smoothly (beams' cores, orbs, flares).
                if (i.uv.x > 5.5)
                {
                    float2 d = i.uv - float2(6.5, 0.5);
                    float r = saturate(1.0 - length(d) * 2.0);
                    c.a *= r * r * (0.35 + 0.65 * r);
                }
                // uv.x 4-5: a soft ribbon, strongest along its middle and fading to nothing at both edges (uv.y across).
                else if (i.uv.x > 3.5)
                {
                    float across = abs(i.uv.y * 2.0 - 1.0);
                    float edge = saturate(1.0 - across);
                    c.a *= edge * edge * (3.0 - 2.0 * edge);
                }
                // uv.x 2-3: a soft round particle: fade to the edge.
                else if (i.uv.x > 1.5)
                {
                    float2 d = i.uv - float2(2.5, 0.5);
                    c.a *= saturate(1.0 - length(d) * 2.0);
                }
                #if defined(_VISION_MASKED)
                float2 uv = (i.positionWS.xz - _VisionMaskRect.xy) * _VisionMaskRect.zw;
                float inside = step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0);
                half lit = SAMPLE_TEXTURE2D_LOD(_VisionMask, sampler_VisionMask, uv, 0).b * inside;
                c.a *= step(_VisionEntityThreshold, lit);
                #endif
                return c;
            }
            ENDHLSL
        }
    }
}
