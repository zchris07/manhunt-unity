// Effects: unlit vertex colour times a tint, alpha-blended or additive, no depth writes, both sides. With _Masked set
// they behave like entities and show only inside the viewer's own light (sparks, splinters, the scent trail); without
// it (the senses overlay) they show everywhere.
Shader "Vision/Fx"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _SoftOver ("Soft particles: fade only behind the ground (drawn over it)", Float) = 0
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
            #pragma vertex FxVert
            #pragma fragment FxFrag
            #pragma shader_feature_local _VISION_MASKED
            #include "VisionFxCommon.hlsl"
            ENDHLSL
        }
    }
}
