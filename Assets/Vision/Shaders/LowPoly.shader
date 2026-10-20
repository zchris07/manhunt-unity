// Flat-shaded low-poly material. Colour comes from vertex colours; normals are per-face
// (meshes duplicate vertices), so lighting is faceted. Main light only, quantised into a few steps.
// No shadows are cast or received: the only shadows in the game come from the player's flashlight
// (the vision mask), so there is no ShadowCaster pass.
// With "Entity" enabled the object is a dynamic entity: fragments outside the viewer's own light
// (mask channel B below _VisionEntityThreshold) are discarded, so it is invisible in the dark.
// Each surface writes _StencilRef where it's drawn: 1 for the ground (and fallen logs), 0 for everything else, so the
// laser and rounds (Vision/FxOverGround) know where the ground is the nearest thing on screen.
// With _VISION_STONE (a ruin's stone floor) a stone's grain is laid over the vertex colours, in the mesh's own metres:
// soft mottling a hand's width across and smaller, a fine grain, and the odd dark pit or pale fleck, each fading out
// where it would be finer than a pixel or two (so it never shimmers). With _VISION_WOOD (a cabin's boards, laid along x)
// the wood's grain instead: fine wavering lines along the boards, a broader figure, the odd pore.
Shader "Vision/LowPoly"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _RampSteps ("Light Ramp Steps (0 = smooth)", Float) = 0
        _Emission ("Emission (vertex colour)", Range(0, 2)) = 0
        _Whiten ("Whiten (0 the vertex colours, 1 all the tint: a ghost)", Range(0, 1)) = 0
        [Toggle(_VISION_ENTITY)] _Entity ("Entity (hidden outside the viewer's light)", Float) = 0
        [IntRange] _StencilRef ("Stencil (1 = the ground: the laser and rounds draw over it)", Range(0, 255)) = 0
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
            half _Whiten;
        CBUFFER_END

        void VisionEntityClip(float3 positionWS)
        {
            #if defined(_VISION_ENTITY)
            clip(SampleVisionMask(positionWS.xz).b - _VisionEntityThreshold);
            #endif
        }

        float StoneHash(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float StoneNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(StoneHash(i), StoneHash(i + float2(1, 0)), f.x), lerp(StoneHash(i + float2(0, 1)), StoneHash(i + 1.0), f.x), f.y);
        }

        // How much of a pattern of this many cells a metre to keep: all of it while a cell spans a few pixels, none once finer.
        half StoneKeep(float2 p, float cells)
        {
            float2 w = fwidth(p * cells);
            return saturate(1.5 - max(w.x, w.y) * 1.2);
        }

        // A stone's grain at a point (metres): a brightness factor about 1.
        half StoneGrain(float3 positionOS)
        {
            float2 p = positionOS.xz;
            float mottle = StoneNoise(p * 3.1 + 17.0) * 0.6 + StoneNoise(p * 7.3 + 3.0) * 0.4;
            float grain = StoneNoise(p * 38.0);
            float speck = StoneHash(floor(p * 90.0));
            half k = 1.0 + 0.2 * (mottle - 0.5);
            k += 0.09 * (grain - 0.5) * StoneKeep(p, 38.0);
            half keep = StoneKeep(p, 90.0);
            k *= speck > 0.965 ? lerp(1.0, 0.7, keep) : (speck < 0.02 ? lerp(1.0, 1.14, keep) : 1.0);
            return k;
        }

        // Wood's grain along a board laid along x (metres): fine wavering lines the length of it, a broader figure, and the
        // odd dark pore, the fine parts fading out where they'd be finer than a pixel or two.
        half WoodGrain(float3 positionOS)
        {
            float2 p = positionOS.xz;
            float waver = StoneNoise(float2(p.x * 1.4, p.y * 7.0)) + StoneNoise(float2(p.x * 3.1 + 7.0, p.y * 11.0)) * 0.5;
            float lines = StoneNoise(float2(p.x * 2.4, p.y * 150.0 + waver * 9.0));
            float figure = StoneNoise(float2(p.x * 0.9 + 5.0, p.y * 24.0 + waver * 2.0));
            half keepLines = saturate(1.5 - fwidth(p.y * 150.0) * 1.2);
            half keepPores = saturate(1.5 - max(fwidth(p.x * 60.0), fwidth(p.y * 300.0)) * 1.2);
            half k = 1.0 + 0.16 * (figure - 0.5);
            k += 0.22 * (lines - 0.5) * keepLines;
            k *= StoneHash(floor(float2(p.x * 60.0, p.y * 300.0))) > 0.985 ? lerp(1.0, 0.65, keepPores) : 1.0;
            return k;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            // Every surface marks its pixels: the ground 1, anything else 0 (Vision/FxOverGround reads it).
            Stencil { Ref [_StencilRef] Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _VISION_ENTITY
            #pragma multi_compile_local _ _VISION_STONE _VISION_WOOD
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
                #if defined(_VISION_STONE) || defined(_VISION_WOOD)
                float3 positionOS : TEXCOORD2;
                #endif
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.color = i.color;
                #if defined(_VISION_STONE) || defined(_VISION_WOOD)
                o.positionOS = i.positionOS.xyz;
                #endif
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                VisionEntityClip(i.positionWS);
                float3 n = normalize(i.normalWS);
                Light light = GetMainLight();
                half ndl = saturate(dot(n, light.direction));
                if (_RampSteps >= 1.0) ndl = floor(ndl * _RampSteps + 0.5) / _RampSteps;
                half3 albedo = lerp(i.color.rgb, 1.0, _Whiten) * _Tint.rgb;
                #if defined(_VISION_STONE)
                albedo *= StoneGrain(i.positionOS);
                #elif defined(_VISION_WOOD)
                albedo *= WoodGrain(i.positionOS);
                #endif
                // Sky light falls off on steeper faces, so slopes and hollows read through the facet shading.
                half sky = 0.62 + 0.38 * saturate(n.y);
                half3 c = albedo * (SampleSH(n) * sky + light.color * ndl) + albedo * _Emission;
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
