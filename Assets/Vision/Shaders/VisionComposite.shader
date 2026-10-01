// Final look. Samples the camera image and the world-space vision mask at each pixel's world X,Z
// (reconstructed from depth for the pitched orthographic camera).
//   lit  = max(B, R * smoothstep(G))
//   lit ground  = scene * lit (light-source light is tinted warm)
//   unlit ground = desaturated, dimmed faint grey (darkness lifted so the layout stays readable)
//   out  = mix(grey, lit ground, smoothstep(0, 1, lit))
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
                if (_VisDebug > 2.5) return half4(scene, 1);

                // Sample the mask a little in front of each surface, so a wall face lit from its side
                // picks up that light while wall tops (normal up) stay at their own footprint.
                float3 ws = WorldFromUV(uv);
                float3 n = normalize(cross(ddy(ws), ddx(ws)));
                if (dot(n, -_VisCamFwd.xyz) < 0) n = -n;
                float2 sampleXZ = ws.xz + n.xz * _VisDark.z;
                half4 m = SampleVisionMask(sampleXZ);

                half sight = smoothstep(_VisLook.z, _VisLook.w, m.g);
                half fromSources = m.r * sight;
                half lit = max(m.b, fromSources);

                if (_VisDebug > 1.5) return half4(lit.xxx, 1);
                if (_VisDebug > 0.5) return half4(m.rgb, 1);

                half lum = dot(scene, half3(0.299, 0.587, 0.114));
                half3 grey = (lum * _VisDark.x + _VisDark.y) * _VisDarkTint.rgb;

                // Light-source light is tinted warm; _VisViewerTint carries the exposure.
                half viewerShare = saturate(m.b / max(lit, 1e-3));
                half3 tint = _VisViewerTint.rgb * lerp(_VisSourceTint.rgb, 1.0, viewerShare);
                half3 litColor = scene * lit * tint;

                half3 c = lerp(grey, litColor, smoothstep(0.0, 1.0, lit));

                float2 v = uv - 0.5;
                v.x *= _ScreenParams.x / _ScreenParams.y;
                c *= saturate(1.0 - _VisLook.x * dot(v, v));
                c += (Hash(uv * _ScreenParams.xy + frac(_Time.y * 7.0) * 61.0) - 0.5) * _VisLook.y;
                return half4(max(c, 0), 1);
            }
            ENDHLSL
        }
    }
}
