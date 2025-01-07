#ifndef HBAO
#define HBAO

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

#include "HBAOUtils.hlsl"

float _Intensity;
float _Radius;
float _InvRadius2;
float _MaxRadius;
float _AngleBias;
float _FallOff;
float _FOVCorrection;
float4 _DepthToViewParams;

half4 ambient_occlusion_frag(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float2 uv = input.texcoord;

    float rawDepth = SampleSceneDepth(uv);
    if (rawDepth < SKY_DEPTH_VALUE)
    {
        return PackAONormal(HALF_ZERO, HALF_ZERO);
    }

    // Early Out for Falloff
    float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

    if (linearDepth > _FallOff)
    {
        return PackAONormal(HALF_ZERO, HALF_ZERO);
    }

    const float fovCorrectedradiusSS = clamp(_Radius * _FOVCorrection * rcp(linearDepth), STEPS, _MaxRadius);
    const float stepSize = max(1, fovCorrectedradiusSS * rcp(STEPS));

    const float2 positionSS = uv * _ScreenSize.xy;
    const half noise = InterleavedGradientNoise(positionSS);
    const half alpha = TWO_PI / DIRECTIONS;
    const half rcp_directions_count = half(rcp(DIRECTIONS));

    float3 normal = SampleSceneNormals(uv);
    float3 normalVS = GetNormalVS(normal);
    float3 viewPosition = GetPositionVS(uv, _DepthToViewParams);

    half ao = HALF_ZERO;

    UNITY_UNROLL
    for (int d = 0; d < DIRECTIONS; ++d)
    {
        float2 direction = GetDirection(alpha, noise, d);

        float rayPixel = 1.0;
        float angleBias = _AngleBias;

        UNITY_UNROLL
        for (int s = 0; s < STEPS; ++s)
        {
            float2 step_uv = round(rayPixel * direction) * _ScreenSize.zw + uv;
            
            float3 stepViewPosition = GetPositionVS(step_uv, _DepthToViewParams);

            ao += HBAOSample(viewPosition, stepViewPosition, normalVS, angleBias, _InvRadius2);
            rayPixel += stepSize;
        }
    }

    half falloff = CalculateDepthFalloff(linearDepth, _FallOff);
    ao = PositivePow(ao * rcp_directions_count * _Intensity * falloff, 0.6);

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
