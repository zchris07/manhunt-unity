// Final look. Samples the camera image and the world-space vision mask at each pixel's world X,Z
// (reconstructed from depth for the pitched orthographic camera).
//   scene = camera image, blurred with distance from the player (Darkwood-style, a camera effect); gassed, the whole
//           view swims, doubles and goes out of focus, washed out and closing in at the edges
//   lit  = max(B * beam, R * smoothstep(G))
//   lit ground  = scene * lit * litBrightness (light-source light is tinted warm)
//   unlit ground = near-neutral faint grey * unlitBrightness (darkness lifted so the layout stays readable)
//   out  = mix(grey, lit ground, smoothstep(0, 1, lit)), then vignette and grain (camera effects),
//          then global saturation and contrast (the F4 look sliders).
Shader "Hidden/Vision/Composite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "VisionComposite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "VisionCommon.hlsl"

            float4 _VisCamPos;
            float4 _VisCamRight;   // camera right * half width (world units)
            float4 _VisCamUp;      // camera up * half height
            float4 _VisCamFwd;
            float4 _VisCamClip;    // near, far
            float4 _VisDark;       // x = level, y = floor, z = normal offset
            float4 _VisDarkTint;
            float4 _VisSourceTint;
            float4 _VisViewerTint;  // viewer light colour * exposure
            float4 _VisLook;       // x = vignette, y = grain, zw = sightline smoothstep edges
            float _VisDebug;
            float _VisShadow;      // character shadow darkening
            float4 _VisGrade;      // x = contrast, y = saturation, z = lit brightness, w = unlit brightness
            float4 _VisBeam;       // x = flashlight beam intensity, y = back-side light share, z = back-side reach (world), w = blackout (the edge of the woods)
            float4 _VisBlur;       // x = start, y = end (world units from the player), z = max radius px, w = unused
            float4 _VisGas;        // gassed (Penjamin on a survivor, galaxy gas on Zach): x = strength 0-1, y = blur radius px, z = double vision px, w = warp px
            float4 _VisionViewerPos;
            float4 _VisViewer;     // x = the player's own light dimmed evenly (flicker and dying at the edge of the woods)

            // Disc blur: 12 taps on a golden-angle spiral plus the centre.
            half3 DiscBlur(float2 uv, float radiusPx)
            {
                float2 px = radiusPx / _ScreenParams.xy;
                half3 acc = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                [unroll]
                for (int k = 0; k < 12; k++)
                {
                    float a = k * 2.3999632;
                    float r = sqrt((k + 0.5) / 12.0);
                    acc += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(cos(a), sin(a)) * r * px).rgb;
                }
                return acc / 13.0;
            }

            // A wider disc blur for the gas: 16 taps on a golden-angle spiral, turned per pixel so the taps never show as rings.
            half3 GasDisc(float2 uv, float radiusPx, float turn)
            {
                float2 px = radiusPx / _ScreenParams.xy;
                half3 acc = 0;
                [unroll]
                for (int k = 0; k < 16; k++)
                {
                    float a = k * 2.3999632 + turn;
                    float r = sqrt((k + 0.5) / 16.0);
                    acc += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(cos(a), sin(a)) * r * px).rgb;
                }
                return acc / 16.0;
            }

            // Gassed: the view swims (a slow wobble through the whole image), splits into two images drifting apart and back
            // (double vision), and both are thrown well out of focus, the blur breathing in and out.
            half3 GasView(float2 uv, float2 pixel)
            {
                float t = _Time.y;
                float2 px = 1.0 / _ScreenParams.xy;
                float2 warp = float2(sin(uv.y * 7.0 + t * 1.9) + 0.5 * sin(uv.y * 13.0 - t * 1.3),
                                     cos(uv.x * 6.0 + t * 1.6) + 0.5 * cos(uv.x * 11.0 + t * 2.3)) * (_VisGas.w * 0.67) * px;
                float2 c = uv + warp;
                float drift = 0.55 + 0.45 * sin(t * 1.15);
                float2 split = float2(cos(t * 0.7), 0.5 * sin(t * 0.93)) * _VisGas.z * drift * px;
                float radius = _VisGas.y * (0.85 + 0.15 * sin(t * 2.4));
                float turn = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715)))) * 6.2831853;
                return GasDisc(c + split, radius, turn) * 0.56 + GasDisc(c - split, radius, turn + 1.7) * 0.44;
            }

            float3 WorldFromUV(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                raw = 1.0 - raw;
                #endif
                float eye = lerp(_VisCamClip.x, _VisCamClip.y, raw);
                float2 ndc = uv * 2.0 - 1.0;
                return _VisCamPos.xyz + _VisCamRight.xyz * ndc.x + _VisCamUp.xyz * ndc.y + _VisCamFwd.xyz * eye;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half3 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                if (_VisDebug > 2.5 && _VisDebug < 3.5) return half4(scene, 1);

                float3 ws = WorldFromUV(uv);

                // Distance blur: far from the player the scene softens (off with the camera effects).
                float blurPx = _VisBlur.z * smoothstep(_VisBlur.x, _VisBlur.y, distance(ws.xz, _VisionViewerPos.xz));
                if (blurPx > 0.25) scene = DiscBlur(uv, blurPx);
                // Gassed (Penjamin, galaxy gas): the whole view swims, doubles and goes out of focus.
                half gas = _VisGas.x;
                if (gas > 0.002) scene = lerp(scene, GasView(uv, input.positionCS.xy), saturate(gas * 1.6));

                // Sample the mask a little in front of each surface, so a wall face lit from its side
                // picks up that light while wall tops (normal up) stay at their own footprint.
                float3 n = normalize(cross(ddy(ws), ddx(ws)));
                if (dot(n, -_VisCamFwd.xyz) < 0) n = -n;
                float2 sampleXZ = ws.xz + n.xz * _VisDark.z;
                half4 m = SampleVisionMask(sampleXZ);

                // Lit areas show whether or not the player has a line of sight to them.
                half fromSources = m.r;
                half beam = m.b * _VisBeam.x * _VisViewer.x;

                // Upright faces turned away from a light (a trunk's or rock's back side) get part of the light on the
                // object's lit side, found a little way through the object: a natural, soft darkness. The ground
                // (normal up) keeps the full shadow, so the shadow starts dark at the base of the object.
                half upright = saturate((0.65 - n.y) / 0.35);
                if (upright > 0.0 && _VisBeam.y > 0.0)
                {
                    half4 front = SampleVisionMask(ws.xz - normalize(n.xz + 1e-5) * _VisBeam.z);
                    half k = _VisBeam.y * upright;
                    beam = max(beam, front.b * _VisBeam.x * _VisViewer.x * k);
                    fromSources = max(fromSources, front.r * k);
                }
                half lit = max(beam, fromSources);

                if (_VisDebug > 3.5) return half4(m.aaa, 1);
                if (_VisDebug > 1.5) return half4(lit.xxx, 1);
                if (_VisDebug > 0.5) return half4(m.rgb, 1);

                half lum = dot(scene, half3(0.299, 0.587, 0.114));
                half3 grey = (lum * _VisDark.x + _VisDark.y) * _VisDarkTint.rgb * _VisGrade.w;

                // Light-source light is tinted warm; _VisViewerTint carries the exposure.
                half viewerShare = saturate(beam / max(lit, 1e-3));
                half3 tint = _VisViewerTint.rgb * lerp(_VisSourceTint.rgb, 1.0, viewerShare);
                // Character shadows only darken what is already lit; they never change the lit amount.
                half3 litColor = scene * lit * tint * (1.0 - _VisShadow * m.a) * _VisGrade.z;

                half3 c = lerp(grey, litColor, smoothstep(0.0, 1.0, lit));

                float2 v = uv - 0.5;
                v.x *= _ScreenParams.x / _ScreenParams.y;
                c *= saturate(1.0 - _VisLook.x * dot(v, v));
                c += (Hash(uv * _ScreenParams.xy + frac(_Time.y * 7.0) * 61.0) - 0.5) * _VisLook.y;
                c = max(c, 0);

                // Global grade, last: saturation, then contrast pivoting on linear mid-grey.
                half luma = dot(c, half3(0.2126, 0.7152, 0.0722));
                c = max(lerp(luma.xxx, c, _VisGrade.y), 0);
                c = pow(max(c / 0.18, 1e-5), _VisGrade.x) * 0.18;
                // Gassed: washed out, and closing in from the edges.
                if (gas > 0.002)
                {
                    half gl = dot(c, half3(0.2126, 0.7152, 0.0722));
                    c = lerp(c, gl.xxx, 0.4 * gas);
                    c *= 1.0 - 0.6 * gas * smoothstep(0.12, 0.9, dot(v, v) * 1.6);
                }
                // Out past the edge of the woods the whole screen goes dark, every light with it.
                c *= 1.0 - saturate(_VisBeam.w);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
