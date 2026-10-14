// Volumetric gas (after Black Ops Cold War's smoke): each cloud is a box drawn over the scene, inside which a ray is
// marched through a participating medium. Its density is the cloud's shape (a ground-hugging blob, a cone, a small
// puff), eroded at its edges by a tiling 3D Perlin-Worley noise that churns and drifts, so the surface billows like
// cauliflower and frays into wisps. Light is absorbed as it passes (Beer-Lambert); each sample is lit by a short march
// toward a key light above (the tops bright, the undersides darker) with a forward-scattering phase, plus ambient. No
// depth test: the gas never interacts with what's in the scene, it just covers the screen where it is. With _Masked
// set it shows only inside the viewer's own light, as entities do.
Shader "Vision/Gas"
{
    Properties
    {
        [Toggle(_VISION_MASKED)] _Masked ("Only inside the viewer's light", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Gas"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma shader_feature_local _VISION_MASKED
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE3D(_GasNoise);
            SAMPLER(sampler_GasNoise);
            TEXTURE2D(_VisionMask);
            SAMPLER(sampler_VisionMask);
            float4 _VisionMaskRect;
            float _VisionEntityThreshold;

            // Per cloud (a property block each draw).
            float4 _GasTintA, _GasTintB;   // rgb tints (lit colour), a = overall opacity
            float4 _GasShape;              // x = kind (0 blob, 1 cone), y = extinction per metre, z = noise scale (per metre), w = erosion
            float4 _GasMotion;             // xyz = drift (metres per second, world), w = seed
            float4 _GasCone;               // x = radius at the base (fraction of the end's), y = soft start, z = soft end, w = patchiness
            float4 _GasEdge;               // x = how ragged a cone's edges are, y = how much bigger the box is than the cone

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(443.897, 441.423));
                p += dot(p, p.yx + 19.19);
                return frac((p.x + p.y) * p.x);
            }

            // The cloud's shape in its box (object space, -0.5..0.5 on each axis): 1 deep inside, 0 outside. A cone's radius
            // is scaled by `wob` (its ragged, wobbling edge).
            float Shape(float3 o, float wob)
            {
                if (_GasShape.x < 0.5)
                {
                    // A blob sitting on the ground: round in plan, rising to a domed top, flatter underneath.
                    float3 q = o * 2.0;
                    q.y = q.y < 0.0 ? q.y * 1.6 : q.y;
                    return saturate((1.0 - length(q)) * 1.35);
                }
                // A cone along +x from its tip at the origin (x = -0.5) to its open end (x = 0.5).
                float u = o.x + 0.5;
                float r = lerp(_GasCone.x, 1.0, u) * 0.5 * wob / max(_GasEdge.y, 1.0);
                float d = length(o.yz) / max(r, 1e-3);
                float ends = smoothstep(0.0, _GasCone.y, u) * smoothstep(1.0, 1.0 - _GasCone.z, u);
                return saturate((1.0 - d) * 1.35) * ends;
            }

            // Billowing density: the shape eaten away at its edges by the churning noise.
            float Density(float3 o, float3 w, float t)
            {
                float3 drift = _GasMotion.xyz * t;
                // A cone's edge pushed in and out along its length and round it by a slow noise that rolls with the gas,
                // so its outline is ragged and wobbles instead of running straight.
                float wob = 1.0;
                if (_GasShape.x > 0.5 && _GasEdge.x > 0.0)
                {
                    float3 pe = (w - drift * 0.8) * _GasShape.z * 0.45 + _GasMotion.w * 0.211 + float3(0.0, t * 0.03, 0.0);
                    // (Alpha reads back as stored; the colour channels come back linearised from the sRGB volume, undone.)
                    float e = SAMPLE_TEXTURE3D_LOD(_GasNoise, sampler_GasNoise, pe, 0).a;
                    float e2 = pow(SAMPLE_TEXTURE3D_LOD(_GasNoise, sampler_GasNoise, pe * 2.3 + 5.1, 0).b, 0.4545);
                    wob = 1.0 + _GasEdge.x * ((e - 0.5) * 2.2 + (e2 - 0.5) * 0.9);
                }
                float s = Shape(o, wob);
                if (s <= 0.0) return 0.0;
                // The noise is carried along with the drift (sampled where it was drift ago), so the billows roll the way
                // the gas is blowing: Penjamin's out along the aim, whichever way that is.
                float3 p = (w - drift) * _GasShape.z + _GasMotion.w * 0.137;
                // A slow warp of the coordinates so the billows roll over each other (churning upward, not off in a
                // fixed direction of its own).
                float3 warp = SAMPLE_TEXTURE3D_LOD(_GasNoise, sampler_GasNoise, p * 0.31 + float3(0.0, t * 0.013, 0.0), 0).gba - 0.5;
                float4 n = SAMPLE_TEXTURE3D_LOD(_GasNoise, sampler_GasNoise, p + warp * 0.35, 0);
                float billow = n.r;
                // Fine detail fraying the outer edges into wisps.
                float detail = SAMPLE_TEXTURE3D_LOD(_GasNoise, sampler_GasNoise, p * 3.1 - drift * _GasShape.z * 0.6, 0).g;
                // Cauliflower billows: the noise eats the shape away from its edges inward, the finer detail fraying
                // what's left near the surface into wisps.
                float d = saturate((s * 1.15 - (1.0 - billow) * _GasShape.w) * 2.2);
                d = saturate(d - (1.0 - detail) * 0.5 * (1.0 - s * s));
                // Patchy: a much larger, slower pattern (riding the drift with the billows) splits the cloud into thick
                // pockets and thin stretches, with clear gaps right through it between them.
                // (The smooth warp noise, broad: about a quarter of the cloud thins to gaps, a fifth thickens into pockets.)
                float clump = SAMPLE_TEXTURE3D_LOD(_GasNoise, sampler_GasNoise, p * 0.12 + float3(7.3, 2.1, 5.7) + float3(t * 0.004, 0.0, 0.0), 0).a;
                float pockets = smoothstep(0.36, 0.5, clump) * lerp(0.75, 1.6, smoothstep(0.52, 0.66, clump));
                return d * lerp(1.0, pockets, _GasCone.w);
            }

            half4 frag(Varyings i) : SV_Target
            {
                // The view ray, in the world and in the cloud's box.
                float3 dirW;
                float3 originW;
                if (unity_OrthoParams.w > 0.5)
                {
                    dirW = -normalize(UNITY_MATRIX_V[2].xyz);
                    originW = i.positionWS - dirW * 500.0;
                }
                else
                {
                    originW = _WorldSpaceCameraPos;
                    dirW = normalize(i.positionWS - originW);
                }
                float3 o = mul(unity_WorldToObject, float4(originW, 1.0)).xyz;
                float3 d = mul((float3x3)unity_WorldToObject, dirW);
                // Where the ray crosses the box (slabs).
                float3 inv = 1.0 / (abs(d) > 1e-6 ? d : 1e-6);
                float3 t0 = (-0.5 - o) * inv, t1 = (0.5 - o) * inv;
                float3 tmin = min(t0, t1), tmax = max(t0, t1);
                float enter = max(max(tmin.x, tmin.y), tmin.z), exit = min(min(tmax.x, tmax.y), tmax.z);
                if (exit <= enter) discard;
                enter = max(enter, 0.0);

                const int Steps = 40;
                float stepLen = (exit - enter) / Steps;
                float jitter = Hash(i.positionCS.xy + frac(_Time.y) * 17.0);
                float time = _Time.y;
                // A key light low and to one side, so every lobe has a bright face and a shaded one.
                float3 lightW = normalize(float3(0.75, 0.75, 0.35));
                float3 lightO = mul((float3x3)unity_WorldToObject, lightW);
                // Forward scattering toward the light (Henyey-Greenstein, g = 0.35), plus a little back.
                float cosA = dot(-dirW, lightW);
                float g = 0.35;
                float hg = (1.0 - g * g) / pow(abs(1.0 + g * g - 2.0 * g * cosA), 1.5) / (4.0 * PI);
                float phase = 0.75 + hg * 2.0;

                float sigma = _GasShape.y;
                float T = 1.0;
                float3 col = 0.0;
                [loop]
                for (int k = 0; k < Steps; k++)
                {
                    float t = enter + (k + jitter) * stepLen;
                    float3 po = o + d * t;
                    float3 pw = originW + dirW * t;
                    float dens = Density(po, pw, time);
                    if (dens <= 0.001) continue;
                    // Light reaching here: a short march toward the key light through the same cloud.
                    // Steps of 0.6 m toward the light (in the box, the same move scaled by its size).
                    float shadow = 0.0;
                    const float ls = 0.6;
                    [unroll]
                    for (int j = 1; j <= 3; j++)
                        shadow += Density(po + lightO * (ls * j), pw + lightW * (ls * j), time);
                    float lightT = exp(-shadow * sigma * ls * 0.8);
                    // Lit tops, darker undersides; the tint shifts through the cloud.
                    float3 tint = lerp(_GasTintA.rgb, _GasTintB.rgb, saturate(SAMPLE_TEXTURE3D_LOD(_GasNoise, sampler_GasNoise, pw * _GasShape.z * 0.5 + 3.7, 0).b * 1.6 - 0.3));
                    float powder = 1.0 - exp(-dens * sigma * stepLen * 4.0);
                    // Ambient from the sky, and the key light where it gets through: bright tops, grey undersides.
                    float3 lit = tint * (0.24 + 0.72 * lightT * phase * lerp(0.65, 1.0, powder));
                    // Denser cores a little darker, so the lobes stand out from each other.
                    lit *= lerp(1.0, 0.72, dens);
                    float a = 1.0 - exp(-dens * sigma * stepLen);
                    #if defined(_VISION_MASKED)
                    // Only inside the viewer's light, fading softly at its edge.
                    float2 muv = (pw.xz - _VisionMaskRect.xy) * _VisionMaskRect.zw;
                    float minside = step(0.0, muv.x) * step(0.0, muv.y) * step(muv.x, 1.0) * step(muv.y, 1.0);
                    half mlit = SAMPLE_TEXTURE2D_LOD(_VisionMask, sampler_VisionMask, muv, 0).b * minside;
                    a *= smoothstep(_VisionEntityThreshold * 0.5, _VisionEntityThreshold * 1.5 + 0.05, mlit);
                    #endif
                    col += T * a * lit;
                    T *= 1.0 - a;
                    if (T < 0.01) break;
                }
                float alpha = (1.0 - T) * _GasTintA.a;
                col *= _GasTintA.a;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
