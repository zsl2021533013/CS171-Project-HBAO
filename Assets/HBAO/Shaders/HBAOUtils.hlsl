#ifndef HBAOUtils
#define HBAOUtils

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

#define DIRECTIONS 8
#define STEPS 6

static const half HALF_ZERO = half(0.0);
static const half HALF_HALF = half(0.5);
static const half HALF_ONE = half(1.0);

static const float SKY_DEPTH_VALUE = 0.00001;

inline half RadiusFalloff(float dist, float invRadius2)
{
    return saturate(1.0 - dist * invRadius2);
}

inline float HBAOSample(float3 viewPosition, float3 stepViewPosition, float3 normal, inout half angleBias, float invRadius2)
{
    float3 H = stepViewPosition - viewPosition;
    float dist = length(H);

    float dist_inv = rcp(max(dist, 1e-6));
    float sinBlock = dot(normal, H) * dist_inv;

    float diff = max(sinBlock - angleBias, 0);
    angleBias = saturate(max(sinBlock, angleBias));

    return diff * RadiusFalloff(dist, invRadius2);
}

float2 GetDirection(float alpha, float noise, int d)
{
    float angle = alpha * (d + noise);
    float sin, cos;
    sincos(angle, sin, cos);

    return float2(cos, sin);
}

float3 GetNormalVS(float3 normal)
{
    float3 normalVS = normalize(mul((float3x3)UNITY_MATRIX_V, normal));

    return float3(normalVS.xy, -normalVS.z);
}

float InterleavedGradientNoise(float2 positionSS) //removed frameCount
{
    const float3 magic = float3(0.06711056f, 0.00583715f, 52.9829189f);

    return frac(magic.z * frac(dot(positionSS, magic.xy)));
}

half4 PackAONormal(half ao, half3 n)
{
    n *= HALF_HALF;
    n += HALF_HALF;
    return half4(ao, n);
}

half CalculateDepthFalloff(half linearDepth, half distance)
{
    half falloff = HALF_ONE - linearDepth * half(rcp(distance));

    return falloff * falloff;
}

inline float3 GetPositionVS(float2 uv, float4 depthToView)
{
    float rawDepth = SampleSceneDepth(uv);
    float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

    return float3((uv * depthToView.xy + depthToView.zw) * linearDepth, linearDepth);
}

#endif