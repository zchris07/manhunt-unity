// Rasterises visibility polygons (world X,Z) into the world-space mask.
// Vertex colour selects the channel and strength, uv0 = polygon origin, uv1 = (range, falloffStart),
// uv2 = (beam direction x, y, half angle, edge softness) for the flashlight cone, zero otherwise.
// falloffStart >= 1 disables the distance falloff (line of sight and the flashlight beam); a zero half angle disables the
// angular falloff, which fades the beam toward the cone's sides.
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
                float4 beam : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 worldXZ : TEXCOORD0;
                float2 origin : TEXCOORD1;
                float2 rangeFalloff : TEXCOORD2;
                float4 beam : TEXCOORD3;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.worldXZ = i.positionOS.xz;
                o.positionCS = VisionMaskClip(VisionMaskUV(i.positionOS.xz));
                o.color = i.color;
                o.origin = i.origin;
                o.rangeFalloff = i.rangeFalloff;
                o.beam = i.beam;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float range = i.rangeFalloff.x;
                float start = i.rangeFalloff.y;
                float falloff = 1.0;
                float d = distance(i.worldXZ, i.origin);
                if (start < 1.0)
                    falloff = 1.0 - smoothstep(range * start, range, d);
                if (i.beam.z > 0.0)
                {
                    float2 to = i.worldXZ - i.origin;
                    float len = length(to);
                    float t = len > 1e-4 ? acos(clamp(dot(to / len, i.beam.xy), -1.0, 1.0)) / i.beam.z : 0.0;
                    falloff *= 1.0 - smoothstep(1.0 - max(i.beam.w, 1e-3), 1.0, t);
                }
                // Alpha carries character shadows; every other polygon writes 0 there.
                return half4(i.color.rgb * falloff, i.color.a * falloff);
            }
            ENDHLSL
        }
    }
}
