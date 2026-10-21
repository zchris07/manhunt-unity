// The laser and rounds in flight: the effects' look (Vision/Fx, unmasked), drawn over the ground but behind whatever stands
// in front of them. The ground marks the pixels where it's the nearest thing with stencil 1 (Vision/LowPoly's
// _StencilRef; everything else drawn writes 0). Where the ground is in front, the effect always shows, so no hill ever
// covers it; anywhere else the nearest thing is a wall, tree, rock, prop or body, and the effect is depth-tested against
// it as drawn, hidden exactly where that thing is nearer. Drawn by the over-ground overlay camera, which keeps the scene's
// depth and stencil and comes after the vision composite, so the darkness never covers it.
Shader "Vision/FxOverGround"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _SoftOver ("Soft particles: fade only behind the ground (drawn over it)", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        // Over the ground: always.
        Pass
        {
            Name "OverGround"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest Always
            Cull Off
            Stencil { Ref 1 Comp Equal }

            HLSLPROGRAM
            #pragma vertex FxVert
            #pragma fragment FxFrag
            #include "VisionFxCommon.hlsl"
            ENDHLSL
        }

        // Over anything else: only where it's nearer than that thing.
        Pass
        {
            Name "BehindProps"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off
            Stencil { Ref 1 Comp NotEqual }

            HLSLPROGRAM
            #pragma vertex FxVert
            #pragma fragment FxFrag
            #include "VisionFxCommon.hlsl"
            ENDHLSL
        }
    }
}
