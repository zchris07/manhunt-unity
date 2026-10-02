// Final look. Samples the camera image and the world-space vision mask at each pixel's world X,Z
// (reconstructed from depth for the pitched orthographic camera).
//   scene = camera image, blurred with distance from the player (Darkwood-style, a camera effect)
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
            float4 _VisBeam;       // x = flashlight beam intensity
            float4 _VisBlur;       // x = start, y = end (world units from the player), z = max radius px
            float4 _VisionViewerPos;

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

                // Sample the mask a little in front of each surface, so a wall face lit from its side
                // picks up that light while wall tops (normal up) stay at their own footprint.
                float3 n = normalize(cross(ddy(ws), ddx(ws)));
                if (dot(n, -_VisCamFwd.xyz) < 0) n = -n;
                float2 sampleXZ = ws.xz + n.xz * _VisDark.z;
                half4 m = SampleVisionMask(sampleXZ);

                half sight = smoothstep(_VisLook.z, _VisLook.w, m.g);
                half fromSources = m.r * sight;
                half beam = m.b * _VisBeam.x;
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
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
