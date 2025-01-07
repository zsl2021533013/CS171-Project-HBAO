#ifndef SHADOWSHARD_AO_MASTER_HBAO_INCLUDED
#define SHADOWSHARD_AO_MASTER_HBAO_INCLUDED

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

#include "HbaoParameters.hlsl"

inline half RadiusFalloff(real dist)
{
    return saturate(1.0 - dist * INV_RADIUS_SQ);
}

inline real HbaoSample(real3 viewPosition, real3 stepViewPosition, real3 normal, inout half angleBias)
{
    real3 H = stepViewPosition - viewPosition;
    real dist = length(H);

    // Ensure we don't divide by zero in the sinBlock calculation
    real dist_inv = rcp(max(dist, 1e-6));
    real sinBlock = dot(normal, H) * dist_inv;

    real diff = max(sinBlock - angleBias, 0);
    angleBias = saturate(max(sinBlock, angleBias)); // Clamp to prevent overestimation

    return diff * RadiusFalloff(dist);
}

real2 GetDirection(real alpha, real noise, int d)
{
    real angle = alpha * (d + noise);
    real sin, cos;
    sincos(angle, sin, cos);

    return real2(cos, sin);
}

half4 HBAO(Varyings input) : SV_Target
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

    float2 pixelDensity = float2(1.0f, 1.0f);

    #if defined(SUPPORTS_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
    if (_FOVEATED_RENDERING_NON_UNIFORM_RASTER) {
        pixelDensity = RemapFoveatedRenderingDensity(RemapFoveatedRenderingNonUniformToLinear(uv));
    }
    #endif

    const real fovCorrectedradiusSS = clamp(RADIUS * FOV_CORRECTION * rcp(linearDepth_o), SAMPLES, MAX_RADIUS);
    const real stepSize = max(1, fovCorrectedradiusSS * INV_SAMPLE_COUNT_PLUS_ONE);

    const real2 positionSS = GetScreenSpacePosition(uv);
    const half noise = GetNoiseMethod(uv, positionSS);
    const half alpha = TWO_PI / DIRECTIONS;
    const half rcp_directions_count = half(rcp(DIRECTIONS));

    real3 normal = SampleNormal(uv, linearDepth_o, pixelDensity);
    real3 normalVS = GetNormalVS(normal);
    //real3 viewPosition = ReconstructViewPos(uv, linearDepth_o);
    real3 viewPosition = GetPositionVS(uv, _DepthToViewParams);

    half ao = HALF_ZERO;

    UNITY_UNROLL
    for (int d = 0; d < DIRECTIONS; ++d)
    {
        real2 direction = GetDirection(alpha, noise, d);

        real rayPixel = 1.0;
        real angleBias = ANGLE_BIAS;

        UNITY_UNROLL
        for (int s = 0; s < SAMPLES; ++s)
        {
            real2 step_uv = round(rayPixel * direction) * _SourceSize.zw + uv;

            #if defined(SUPPORTS_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
            UNITY_BRANCH if (_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
            {
                step_uv = RemapFoveatedRenderingResolve(step_uv);
            }
            #endif

            //real3 stepViewPosition = ReconstructViewPos(step_uv);
            real3 stepViewPosition = GetPositionVS(step_uv, _DepthToViewParams);

            ao += HbaoSample(viewPosition, stepViewPosition, normalVS, angleBias);
            rayPixel += stepSize;
        }
    }

    half falloff = CalculateDepthFalloff(half_linear_depth_o, FALLOFF);
    ao = PositivePow(ao * rcp_directions_count * INTENSITY * falloff, kContrast);

    // Return the packed ao + normals
    return PackAONormal(ao, normal);
}

#endif
