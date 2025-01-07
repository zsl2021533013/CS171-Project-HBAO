#ifndef SHADOWSHARD_AO_MASTER_HDAO_INCLUDED
#define SHADOWSHARD_AO_MASTER_HDAO_INCLUDED

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

#include "ShaderLibrary/Utils/AOM_Constants.hlsl"
#include "ShaderLibrary/Utils/AOM_Parameters.hlsl"
#include "ShaderLibrary/Utils/AOM_Samplers.hlsl"
#include "ShaderLibrary/Utils/AOM_Functions.hlsl"
#include "ShaderLibrary/Utils/AOM_Noises.hlsl"

#include "ShaderLibrary/Utils/DepthUtils.hlsl"
#include "ShaderLibrary/Utils/ViewPositionReconstruction.hlsl"
#include "ShaderLibrary/Utils/NormalReconstruction.hlsl"

#include "HdaoParameters.hlsl"

#define HDAO_ULTRA_SAMPLE_COUNT                    24
#define HDAO_HIGH_SAMPLE_COUNT                     16
#define HDAO_MEDIUM_SAMPLE_COUNT                   8
#define HDAO_LOW_SAMPLE_COUNT                      4

#if defined( _SAMPLE_COUNT_ULTRA )

#define SAMPLE_COUNT                             HDAO_ULTRA_SAMPLE_COUNT
static const int2                                SamplePattern[SAMPLE_COUNT] =
{
    {0, -9}, {4, -9}, {2, -6}, {6, -6},
    {0, -3}, {4, -3}, {8, -3}, {2, 0},
    {6, 0}, {9, 0}, {4, 3}, {8, 3},
    {2, 6}, {6, 6}, {9, 6}, {4, 9},
    {10, 0}, {-12, 12}, {9, -14}, {-8, -6},
    {11, -7}, {-9, 1}, {-2, -13}, {-7, -3},
};

#elif defined( _SAMPLE_COUNT_HIGH )

#define SAMPLE_COUNT                             HDAO_HIGH_SAMPLE_COUNT
static const int2                                SamplePattern[SAMPLE_COUNT] =
{
    {0, -9}, {4, -9}, {2, -6}, {6, -6},
    {0, -3}, {4, -3}, {8, -3}, {2, 0},
    {6, 0}, {9, 0}, {4, 3}, {8, 3},
    {2, 6}, {6, 6}, {9, 6}, {4, 9},
};

#elif defined( _SAMPLE_COUNT_MEDIUM )

#define SAMPLE_COUNT                             HDAO_MEDIUM_SAMPLE_COUNT
static const int2                                SamplePattern[SAMPLE_COUNT] =
{
    {0, -9}, {2, -6}, {0, -3}, {8, -3},
    {6, 0}, {4, 3}, {2, 6}, {9, 6},
};

#else //if defined( _SAMPLE_COUNT_LOW )

#define SAMPLE_COUNT                             HDAO_LOW_SAMPLE_COUNT
static const int2 SamplePattern[SAMPLE_COUNT] =
{
    {0, -6}, {0, 6}, {0, -6}, {6, 0},
};

#endif

inline real3 GetHdaoPositionVS(real2 uv, real3 normal)
{
    #ifdef _HDAO_USE_NORMALS
    real3 normalVS = GetNormalVS(normal);
    return GetPositionVS(uv, _DepthToViewParams) + normalVS * NORMAL_INTENSITY;
    #else
    return GetPositionVS(uv, _DepthToViewParams);
    #endif
}

inline real3 GetHdaoPositionVS(real2 uv)
{
    #ifdef _HDAO_USE_NORMALS
    real3 normalVS = SampleSceneNormals(uv);
    return GetHdaoPositionVS(uv, normalVS);
    #else
    return GetPositionVS(uv, _DepthToViewParams);
    #endif
}

half4 HDAO(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    real2 uv = input.texcoord;

    real raw_depth_o = SampleDepth(uv);
    if (raw_depth_o < SKY_DEPTH_VALUE)
        return PackAONormal(HALF_ZERO, HALF_ZERO);

    // Early Out for Falloff
    real linearDepth_o = GetLinearEyeDepth(raw_depth_o);
    half half_linear_depth_o = half(linearDepth_o);
    if (half_linear_depth_o > FALLOFF)
        return PackAONormal(HALF_ZERO, HALF_ZERO);

    real3 centerNormal = SampleSceneNormals(uv);
    real3 centerPosition = GetHdaoPositionVS(uv, centerNormal);
    real centerDistance = length(centerPosition);

    real2 rayStart = uv * _SourceSize.xy;
    const half noise = GetNoiseMethod(uv, rayStart * _Downsample) * OFFSET_CORRECTION; // noise * aspectRatio

    half ao = HALF_ZERO;
    half2 acceptRadius = half2(ACCEPT_RADIUS, ACCEPT_RADIUS);
    half2 rejectRadius = half2(REJECT_RADIUS, REJECT_RADIUS);

    UNITY_UNROLL
    for (uint s = 0; s < SAMPLE_COUNT; ++s)
    {
        real2 samplePattern = SamplePattern[s].xy * noise;

        // Sample depth positions
        real2 uv_s0 = (rayStart + samplePattern) * _SourceSize.zw;
        real2 uv_s1 = (rayStart - samplePattern) * _SourceSize.zw;

        real3 positionX = GetHdaoPositionVS(uv_s0);
        real3 positionY = GetHdaoPositionVS(uv_s1);

        real distanceX = length(positionX);
        real distanceY = length(positionY);

        // Detect valleys
        real2 distanceDelta = centerDistance.xx - float2(distanceX, distanceY);
        real2 compare = saturate(rejectRadius - distanceDelta); // removed * 6.0f
        compare = distanceDelta > acceptRadius ? compare : 0.0h;

        // Compute dot product, to scale occlusion based on depth position
        real3 directionX = normalize(positionX - centerPosition);
        real3 directionY = normalize(positionY - centerPosition);
        real directionDot = saturate(dot(directionX, directionY) + 0.9h) * 1.2h;

        // Accumulate weighted occlusion
        ao += compare.x * compare.y * directionDot; // removed pow(directionDot, 3) for optimization
    }

    const half rcp_samples_count = half(rcp(SAMPLE_COUNT));
    half falloff = CalculateDepthFalloff(half_linear_depth_o, FALLOFF);
    ao = PositivePow(ao * rcp_samples_count * INTENSITY * falloff, kContrast);

    // Return the packed ao + normals
    return PackAONormal(ao, centerNormal);
}

#endif
