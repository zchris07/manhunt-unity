// Rasterises visibility polygons (world X,Z) into the world-space mask.
// Vertex colour selects the channel and strength, uv0 = polygon origin, uv1 = (range, falloffStart).
// falloffStart >= 1 disables the distance falloff (line of sight).
Shader "Hidden/Vision/Mask"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "VisionMask"
            ZWrite Off
            ZTest Always
            Cull Off
            BlendOp Max
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "VisionCommon.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 origin : TEXCOORD0;
                float2 rangeFalloff : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 worldXZ : TEXCOORD0;
                float2 origin : TEXCOORD1;
                float2 rangeFalloff : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.worldXZ = i.positionOS.xz;
                o.positionCS = VisionMaskClip(VisionMaskUV(i.positionOS.xz));
                o.color = i.color;
                o.origin = i.origin;
                o.rangeFalloff = i.rangeFalloff;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float range = i.rangeFalloff.x;
                float start = i.rangeFalloff.y;
                float falloff = 1.0;
                if (start < 1.0)
                {
                    float d = distance(i.worldXZ, i.origin);
                    falloff = 1.0 - smoothstep(range * start, range, d);
                }
                return half4(i.color.rgb * falloff, 1.0);
            }
            ENDHLSL
        }
    }
}
