// The K.P.O.S. beam: a solid cylinder of plasma (the Core) and the ionised sheath round it (the Corona), drawn by the
// over-ground camera (after the darkness, keeping the scene's depth) from a unit cylinder mesh (radius 1, y 0 to 1).
//   Core: opaque and blown out, HDR white emission (so bloom catches it) hiding whatever's behind it; inside, plasma
//   streaming down it (two octaves of value noise scrolled along it), the faintest pink only at its grazing edge.
//   Corona: additive, front faces only, a tight pink-white glow round the core fading to pink at its outer edge. Its
//   brightness follows Beer-Lambert absorption along the view ray's chord through the sheath, 1 - exp(-sigma * L), with
//   L proportional to |N.V| for a cylinder; animated noise breaks its density up so the edge tears and flickers like
//   the ionised air round the reference's beam.
Shader "Vision/StrikeBeam"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Edge ("Edge colour", Color) = (1, 0.42, 0.78, 1)
        _Intensity ("HDR intensity", Float) = 3
        _Alpha ("Alpha (fading in and out)", Float) = 1
        _Density ("Corona density (Beer-Lambert sigma)", Float) = 3.4
        _Flow ("Plasma flow (world units a second, down the beam)", Float) = 9
        _Seed ("Seed", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Toggle] _ZWrite ("Z Write", Float) = 1
        [Toggle(_STRIKE_CORONA)] _Corona ("Corona", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "StrikeBeam"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local __ _STRIKE_CORONA
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _Edge;
                float _Intensity;
                float _Alpha;
                float _Density;
                float _Flow;
                float _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                // uv.x round the beam (0-1), uv.y up it in world units.
                float height = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
                o.uv = float2(i.uv.x, i.uv.y * height);
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Value noise, wrapping round the beam (period px across).
            float Noise(float2 p, float px)
            {
                float2 c = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float x0 = fmod(c.x, px), x1 = fmod(c.x + 1.0, px);
                float a = Hash(float2(x0, c.y)), b = Hash(float2(x1, c.y));
                float d = Hash(float2(x0, c.y + 1.0)), e = Hash(float2(x1, c.y + 1.0));
                return lerp(lerp(a, b, f.x), lerp(d, e, f.x), f.y);
            }

            float Plasma(float2 uv, float speed)
            {
                float t = _Time.y;
                float2 p = float2(uv.x * 8.0, uv.y * 1.6 + t * speed + _Seed * 17.0);
                float n = Noise(p, 8.0) * 0.65 + Noise(p * float2(2.0, 2.3) + 31.7, 16.0) * 0.35;
                return n;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float ndv = abs(dot(n, v));
                #if defined(_STRIKE_CORONA)
                    // The sheath: Beer-Lambert along the chord through it, broken up by ionised turbulence racing up it.
                    float tear = Plasma(float2(i.uv.x, i.uv.y * 0.8), -_Flow * 0.55);
                    float chord = ndv * (0.45 + 1.1 * tear);
                    float glow = 1.0 - exp(-_Density * chord * chord);
                    half3 col = lerp(_Edge.rgb, _Color.rgb, saturate(ndv * ndv * 1.6));
                    return half4(col * glow * _Intensity * _Alpha, 1.0);
                #else
                    // The core: blown-out white, plasma streaming down it, pink only where it turns away at the edge.
                    float plasma = Plasma(i.uv, _Flow);
                    half3 col = lerp(_Edge.rgb * 1.4, _Color.rgb, smoothstep(0.05, 0.45, ndv));
                    col *= _Intensity * (0.82 + 0.36 * plasma);
                    return half4(col * _Alpha, 1.0);
                #endif
            }
            ENDHLSL
        }
    }
}
