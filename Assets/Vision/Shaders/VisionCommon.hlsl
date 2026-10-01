#ifndef VISION_COMMON_INCLUDED
#define VISION_COMMON_INCLUDED

// World-space vision mask written by VisionMaskRenderer.
// R = light sources, G = 360 line of sight, B = viewer's own light.
TEXTURE2D(_VisionMask);
SAMPLER(sampler_VisionMask);
float4 _VisionMaskRect;        // xy = world X,Z of the mask's min corner, zw = 1 / size
float _VisionEntityThreshold;  // hard cut on B for dynamic objects

float2 VisionMaskUV(float2 worldXZ)
{
    return (worldXZ - _VisionMaskRect.xy) * _VisionMaskRect.zw;
}

// Everything outside the mask square counts as dark.
half4 SampleVisionMask(float2 worldXZ)
{
    float2 uv = VisionMaskUV(worldXZ);
    half4 m = SAMPLE_TEXTURE2D_LOD(_VisionMask, sampler_VisionMask, uv, 0);
    float inside = step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0);
    return m * inside;
}

// Maps a 0..1 mask coordinate to clip space for the mask passes, which draw without camera
// matrices. Flipped where render textures start at the top so sampling at the same uv matches.
float4 VisionMaskClip(float2 uv)
{
    float2 c = uv * 2.0 - 1.0;
    #if UNITY_UV_STARTS_AT_TOP
    c.y = -c.y;
    #endif
    return float4(c, 0.5, 1.0);
}

#endif
