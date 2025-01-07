#ifndef Custom_HBAO
#define Custom_HBAO

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

#include "Assets/ShadowShard/AmbientOcclusionMaster/Shaders/ShaderLibrary/Utils/AOM_Constants.hlsl"
#include "Assets/ShadowShard/AmbientOcclusionMaster/Shaders/ShaderLibrary/Utils/AOM_Parameters.hlsl"
#include "Assets/ShadowShard/AmbientOcclusionMaster/Shaders/ShaderLibrary/Utils/AOM_Samplers.hlsl"
#include "Assets/ShadowShard/AmbientOcclusionMaster/Shaders/ShaderLibrary/Utils/AOM_Functions.hlsl"
#include "Assets/ShadowShard/AmbientOcclusionMaster/Shaders/ShaderLibrary/Utils/AOM_Noises.hlsl"

#include "Assets/ShadowShard/AmbientOcclusionMaster/Shaders/ShaderLibrary/Utils/DepthUtils.hlsl"
#include "Assets/ShadowShard/AmbientOcclusionMaster/Shaders/ShaderLibrary/Utils/ViewPositionReconstruction.hlsl"
#include "Assets/ShadowShard/AmbientOcclusionMaster/Shaders/ShaderLibrary/Utils/NormalReconstruction.hlsl"

#define DIRECTIONS 8
#define STEPS 6

StructuredBuffer<float2> _NoiseCB;

float _Intensity;
float _Radius;
float _InvRadius2;
float _MaxRadius;
float _AngleBias;
float _FallOff;
float _FOVCorrection;
float4 _DepthToViewParams;

inline half RadiusFalloff(real dist)
{
    return saturate(1.0 - dist * _InvRadius2);
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

half4 ambient_occlusion_frag(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    real2 uv = input.texcoord;

    real raw_depth_o = SampleDepth(uv);
    if (raw_depth_o < SKY_DEPTH_VALUE)
    {
        return PackAONormal(HALF_ZERO, HALF_ZERO);
    }

    // Early Out for Falloff
    real linearDepth_o = GetLinearEyeDepth(raw_depth_o);
    half half_linear_depth_o = half(linearDepth_o);

    if (half_linear_depth_o > _FallOff)
    {
        return PackAONormal(HALF_ZERO, HALF_ZERO);
    }

    float2 pixelDensity = float2(1.0f, 1.0f);

    #if defined(SUPPORTS_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
    if (_FOVEATED_RENDERING_NON_UNIFORM_RASTER) {
        pixelDensity = RemapFoveatedRenderingDensity(RemapFoveatedRenderingNonUniformToLinear(uv));
    }
    #endif

    const real fovCorrectedradiusSS = clamp(_Radius * _FOVCorrection * rcp(linearDepth_o), STEPS, _MaxRadius);
    const real stepSize = max(1, fovCorrectedradiusSS * rcp(STEPS));

    const real2 positionSS = GetScreenSpacePosition(uv);
    const half noise = GetNoiseMethod(uv, positionSS);
    const half alpha = TWO_PI / DIRECTIONS;
    const half rcp_directions_count = half(rcp(DIRECTIONS));

    real3 normal = SampleSceneNormals(uv);
    real3 normalVS = GetNormalVS(normal);
    real3 viewPosition = GetPositionVS(uv, _DepthToViewParams);

    half ao = HALF_ZERO;

    UNITY_UNROLL
    for (int d = 0; d < DIRECTIONS; ++d)
    {
        real2 direction = GetDirection(alpha, noise, d);

        real rayPixel = 1.0;
        real angleBias = _AngleBias;

        UNITY_UNROLL
        for (int s = 0; s < STEPS; ++s)
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

    half falloff = CalculateDepthFalloff(half_linear_depth_o, _FallOff);
    ao = PositivePow(ao * rcp_directions_count * _Intensity * falloff, kContrast);

    // Return the packed ao + normals
    return PackAONormal(ao, normal);
}

half4 blur_frag(Varyings i) : SV_Target
{
    float2 uv = i.texcoord;
    float centerAO = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).r;
    float blurredAO = 0.0;
    float weightSum = 0.0;

    UNITY_UNROLL
    for (int x = -2; x <= 2; ++x)
    {
        UNITY_UNROLL
        for (int y = -2; y <= 2; ++y)
        {
            float2 offset = float2(x, y);
            float2 sampleUV = uv + offset * _BlitTexture_TexelSize;
            float sampleAO = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, sampleUV).r;

            // 计算空间权重（使用 sigma 为 1 的高斯函数）
            float spatialWeight = exp(-dot(offset, offset) / 2);

            // 计算范围权重（使用 sigma 为 1 的高斯函数）
            float rangeWeight = exp(-pow(sampleAO - centerAO, 2) / 2);

            // 综合权重
            float weight = spatialWeight * rangeWeight;

            blurredAO += sampleAO * weight;
            weightSum += weight;
        }
    }

    blurredAO /= weightSum;

    return half4(blurredAO, blurredAO, blurredAO, 1);
}

half4 combine_frag(Varyings i) : SV_Target
{
    float2 uv = i.texcoord;
    half ao = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).r;

    return half4(0, 0, 0, 1 - ao);
}

#endif
