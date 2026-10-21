// The effects' vertex and fragment code, shared by Vision/Fx (drawn over the dark, or only inside the viewer's light) and
// Vision/FxOverGround (the laser and rounds: over the ground, behind whatever stands in front of them).
#ifndef VISION_FX_COMMON
#define VISION_FX_COMMON

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

TEXTURE2D(_VisionMask);
SAMPLER(sampler_VisionMask);
float4 _VisionMaskRect;
float _VisionEntityThreshold;

CBUFFER_START(UnityPerMaterial)
    half4 _Tint;
    // Soft particles: 0 (drawn depth-tested), a sprite fades out as it nears what's behind it; 1 (drawn over the
    // ground), it fades out only once it's that far behind the ground, so no hill ever cuts it off in a hard line.
    float _SoftOver;
CBUFFER_END

// How far (world units) a soft sprite fades over as it meets the scene (Lorach 2007, "Soft Particles").
#define FX_SOFT_DEPTH 0.8

// The scene's eye depth under a pixel (the pitched orthographic camera's, or a perspective one's).
float FxSceneEye(float2 pixel)
{
    float raw = SampleSceneDepth(pixel / _ScaledScreenParams.xy);
    if (unity_OrthoParams.w > 0.5)
    {
        #if UNITY_REVERSED_Z
        raw = 1.0 - raw;
        #endif
        return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
    }
    return LinearEyeDepth(raw, _ZBufferParams);
}

// A point's eye depth: its distance along the camera's view direction.
float FxEye(float3 positionWS)
{
    float3 fwd = -UNITY_MATRIX_V[2].xyz;
    return dot(positionWS - _WorldSpaceCameraPos, fwd);
}

struct Attributes
{
    float4 positionOS : POSITION;
    half4 color : COLOR;
    float2 uv : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    half4 color : COLOR;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float2 uv1 : TEXCOORD2;
};

Varyings FxVert(Attributes i)
{
    Varyings o;
    o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
    o.positionCS = TransformWorldToHClip(o.positionWS);
    o.color = i.color * _Tint;
    o.uv = i.uv;
    o.uv1 = i.uv1;
    return o;
}

float FxHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// A laser sight as Noita draws one: built of the screen's pixels (art pixels a few screen pixels big, as the game's are),
// a faint red haze a few pixels either side that flickers in clumps and now and then drops out, a one-pixel core of red
// pixels each flickering on its own (static), broken into dashes that come and go, the odd pixel flaring white, and dust
// drifting out along it away from the barrel: single pixels that wander a row off the line, fading in, catching the
// light and fading out, in clusters and empty stretches. uv.y runs across (0 to 1), uv1.x is the distance along the beam
// from the barrel (world units), uv1.y a seed per beam.
half4 FxLaser(Varyings i)
{
    float P = max(1.0, floor(_ScreenParams.y / 540.0));
    float across = i.uv.y * 2.0 - 1.0;
    float perAcross = max(length(float2(ddx(across), ddy(across))), 1e-6);
    float perAlong = max(length(float2(ddx(i.uv1.x), ddy(i.uv1.x))), 1e-6);
    float a = floor(i.uv1.x / perAlong / P);         // the art pixel along
    float row = floor(across / perAcross / P + 0.5); // the art pixel across, 0 on the line
    float rows = abs(row);
    float seed = i.uv1.y;
    float tick = floor(_Time.y * 30.0), slow = floor(_Time.y * 12.0);

    // The haze: dim, fading out over three pixels either side, in flickering clumps that sometimes go out.
    float clump = FxHash(float2(floor(a / 8.0) + seed * 13.0, slow));
    float haze = rows <= 3.0 ? 0.2 * (1.0 - rows / 4.0) : 0.0;
    haze *= clump < 0.15 ? 0.0 : lerp(0.45, 1.0, clump);

    // The core: one pixel, every pixel flickering on its own, in dashes that come and go.
    float core = 0.0, white = 0.0;
    if (rows < 0.5)
    {
        float dash = FxHash(float2(floor(a / 5.0) + 17.0 + seed * 31.0, slow * 1.37));
        float px = FxHash(float2(a + seed * 7.0, tick));
        core = dash < 0.25 ? 0.12 : lerp(0.4, 1.0, px);
        if (px > 0.95) white = 0.9;
    }

    // Dust drifting out along the beam: three drifts at different speeds, a mote or none in each stretch of 24 pixels.
    float dust = 0.0;
    float m0 = i.uv1.x / perAlong / P;
    [unroll]
    for (int k = 0; k < 3; k++)
    {
        float m = m0 - _Time.y * (9.0 + 6.0 * k);
        float cell = floor(m / 24.0);
        float h = FxHash(float2(cell + seed * 3.0, k + 1.0));
        if (h < 0.55)
        {
            float at = 2.0 + FxHash(float2(cell, k + 11.0)) * 20.0;
            float side = floor(FxHash(float2(cell, k + 23.0)) * 3.0) - 1.0;
            float phase = frac(_Time.y * (0.35 + 0.4 * FxHash(float2(cell, k + 5.0))) + h * 7.0);
            float lit = sin(phase * 3.14159);
            if (abs(floor(m - cell * 24.0) - floor(at)) < 0.5 && abs(row - side) < 0.5) dust = max(dust, lit * lit);
        }
    }

    half3 rgb = i.color.rgb * (haze + core) + half3(1.0, 0.92, 0.85) * white + half3(1.0, 0.82, 0.7) * dust;
    return half4(rgb, i.color.a);
}

half4 FxFrag(Varyings i) : SV_Target
{
    // uv.x 10 on: the same shapes as soft particles (10 added to their uv.x), faded against the scene's depth.
    bool soft = i.uv.x > 9.5;
    if (soft) i.uv.x -= 10.0;
    // uv.x 8-9: a laser sight.
    if (i.uv.x > 7.5) return FxLaser(i);
    half4 c = i.color;
    // uv.x 6-7: a glow, bright in the middle and falling off smoothly (beams' cores, orbs, flares).
    if (i.uv.x > 5.5)
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
    if (soft) c.a *= saturate((FxSceneEye(i.positionCS.xy) - FxEye(i.positionWS)) / FX_SOFT_DEPTH + _SoftOver);
    #if defined(_VISION_MASKED)
    float2 uv = (i.positionWS.xz - _VisionMaskRect.xy) * _VisionMaskRect.zw;
    float inside = step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0);
    half lit = SAMPLE_TEXTURE2D_LOD(_VisionMask, sampler_VisionMask, uv, 0).b * inside;
    c.a *= step(_VisionEntityThreshold, lit);
    #endif
    return c;
}

#endif
