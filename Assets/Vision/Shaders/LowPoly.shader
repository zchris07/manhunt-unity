// Flat-shaded low-poly material. Colour comes from vertex colours; normals are per-face
// (meshes duplicate vertices), so lighting is faceted. Main light only, quantised into a few steps.
// No shadows are cast or received: the only shadows in the game come from the player's flashlight
// (the vision mask), so there is no ShadowCaster pass.
// With "Entity" enabled the object is a dynamic entity: fragments outside the viewer's own light
// (mask channel B below _VisionEntityThreshold) are discarded, so it is invisible in the dark.
Shader "Vision/LowPoly"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _RampSteps ("Light Ramp Steps", Float) = 4
        _Emission ("Emission (vertex colour)", Range(0, 2)) = 0
        [Toggle(_VISION_ENTITY)] _Entity ("Entity (hidden outside the viewer's light)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "VisionCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            float _RampSteps;
            half _Emission;
        CBUFFER_END

        void VisionEntityClip(float3 positionWS)
        {
            #if defined(_VISION_ENTITY)
            clip(SampleVisionMask(positionWS.xz).b - _VisionEntityThreshold);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _VISION_ENTITY
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.color = i.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                VisionEntityClip(i.positionWS);
                float3 n = normalize(i.normalWS);
                Light light = GetMainLight();
                half ndl = saturate(dot(n, light.direction));
                half steps = max(_RampSteps, 1.0);
                ndl = floor(ndl * steps + 0.5) / steps;
                half3 albedo = i.color.rgb * _Tint.rgb;
                half3 c = albedo * (SampleSH(n) + light.color * ndl) + albedo * _Emission;
                return half4(c, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _VISION_ENTITY

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half frag(Varyings i) : SV_Target
            {
                VisionEntityClip(i.positionWS);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _VISION_ENTITY

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                VisionEntityClip(i.positionWS);
                return half4(NormalizeNormalPerPixel(i.normalWS), 0);
            }
            ENDHLSL
        }
    }
}
