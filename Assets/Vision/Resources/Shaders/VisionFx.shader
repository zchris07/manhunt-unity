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

            float Hash2(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 f = frac(p), w = f * f * (3.0 - 2.0 * f);
                float2 n = floor(p);
                float a = Hash2(n), b = Hash2(n + float2(1, 0)), c = Hash2(n + float2(0, 1)), d = Hash2(n + float2(1, 1));
                return lerp(lerp(a, b, w.x), lerp(c, d, w.x), w.y);
            }

            // Three octaves of value noise (and two for the finer churn): enough for billows, cheap for big clouds.
            float Fbm(float2 p)
            {
                float v = 0.5 * ValueNoise(p);
                p = p * 2.03 + 17.1;
                v += 0.25 * ValueNoise(p);
                p = p * 2.03 + 17.1;
                return (v + 0.125 * ValueNoise(p)) / 0.875;
            }

            float Fbm2(float2 p)
            {
                return (0.5 * ValueNoise(p) + 0.25 * ValueNoise(p * 2.03 + 17.1)) / 0.75;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = i.color;
                // uv.x 10-11: a wisp of gas, a long thin streak (uv.x along it, tail to head; uv.y's fraction across, its
                // whole part the seed): fine strands drawn out along it and drifting toward its head, the streak bending
                // gently and slowly changing its bend, tapering away at both ends (the tail longer) and fading to its edges.
                if (i.uv.x > 9.5)
                {
                    float seed = floor(i.uv.y);
                    float along = i.uv.x - 10.0;
                    float across = frac(i.uv.y) * 2.0 - 1.0;
                    float t = _Time.y;
                    float bend = (ValueNoise(float2(along * 2.2 + seed * 1.37, t * 0.25 + seed * 0.71)) - 0.5) * 0.9;
                    float y = across - bend * sin(along * 3.14159);
                    float ends = smoothstep(0.0, 0.4, along) * smoothstep(1.0, 0.72, along);
                    float edge = saturate(1.0 - abs(y) * 1.15);
                    edge *= edge;
                    float2 p = float2(along * 2.4 - t * 0.32, y * 6.5) + float2(seed * 3.17, seed * 1.93);
                    float s = Fbm(p);
                    float s2 = ValueNoise(float2(along * 5.0 - t * 0.55, y * 15.0) + seed * 2.3);
                    float dens = saturate((s * 0.85 + s2 * 0.4 - 0.42) * 2.2) * edge * ends;
                    clip(dens - 0.002);
                    c.rgb *= lerp(0.82, 1.15, s2);
                    c.a *= dens;
                }
                // uv.x 8-9: a puff of smoke or gas (uv.y's whole part is its seed): billowing, ragged at the edges, lighter
                // and darker as it folds, slowly churning (each puff is set clear of the ground and of solid things on
                // the CPU, see Vfx.Step, and its lower edge is soft, so it never shows a hard line cutting into them).
                else if (i.uv.x > 7.5)
                {
                    float seed = floor(i.uv.y);
                    float2 q = float2(i.uv.x - 8.0, frac(i.uv.y)) - 0.5;
                    float r = length(q) * 2.0;
                    // Outside the puff's circle there is nothing to draw.
                    clip(1.0 - r);
                    float t = _Time.y;
                    float2 p = q * 2.6 + float2(seed * 7.13, seed * 3.71);
                    float n = Fbm(p + float2(t * 0.09, -t * 0.06));
                    float n2 = Fbm2(p * 2.2 + float2(-t * 0.13, t * 0.08) + 5.2);
                    float body = saturate(1.0 - r);
                    float dens = saturate((body * (0.4 + 1.1 * n) - 0.35 * n2) * 1.25);
                    dens *= smoothstep(0.0, 0.35, body);
                    c.rgb *= lerp(0.7, 1.2, saturate(n2 * 0.6 + body * 0.5));
                    c.a *= dens * smoothstep(-0.5, -0.2, q.y);
                }
                // uv.x 6-7: a glow, bright in the middle and falling off smoothly (beams' cores, orbs, flares).
                else if (i.uv.x > 5.5)
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
