#ifndef Custom_HBAO
#define Custom_HBAO

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

#define DIRECTIONS 8
#define STEPS 6

StructuredBuffer<float2> _NoiseCB;

float _Intensity;
float _Radius;
float _InvRadius2;
float _MaxRadiusPixels;
float _AngleBias;
float _FallOff;

float Falloff(float distanceSquare)
{
    return 1.0 - distanceSquare * _InvRadius2;
}

float ComputeAO(float3 p, float3 n, float3 s)
{
    float3 v = s - p;
    float VoV = dot(v, v);
    float NoV = dot(n, v) * rsqrt(VoV);

    return saturate(NoV - _AngleBias) * saturate(Falloff(VoV));
}

float3 GetViewPos(float2 uv)
{
    float depth = SampleSceneDepth(uv);
    float2 newUV = float2(uv.x, uv.y);
    newUV = newUV * 2 - 1;
    float4 viewPos = mul(UNITY_MATRIX_I_P, float4(newUV, depth, 1));
    viewPos /= viewPos.w;
    viewPos.z = -viewPos.z;
    return viewPos.xyz;
}

float3 FetchViewNormals(float2 uv)
{
    float3 N = SampleSceneNormals(uv);
    N = TransformWorldToViewDir(N, true);
    N.y = -N.y;
    N.z = -N.z;

    return N;
}

half4 ambient_occlusion_frag(Varyings i) : SV_Target
{
    float2 uv = i.texcoord;

    float3 viewPos = GetViewPos(uv);
    if (viewPos.z >= _FallOff)
    {
        return 1;
    }

    float3 nor = FetchViewNormals(uv);

    int noiseX = (uv.x * _ScreenSize.x - 0.5) % 4;
    int noiseY = (uv.y * _ScreenSize.y - 0.5) % 4;
    int noiseIndex = 4 * noiseY + noiseX;
    float2 rand = _NoiseCB[noiseIndex];

    float stepSize = min(_Radius / viewPos.z, _MaxRadiusPixels) / (STEPS + 1.0);
    float stepAng = TWO_PI / DIRECTIONS;
    
    float ao = 0;

    UNITY_UNROLL
    for (int d = 0; d < DIRECTIONS; ++d)
    {
        float angle = stepAng * (float(d) + rand.x);

        float cosAng, sinAng;
        sincos(angle, sinAng, cosAng);
        float2 direction = float2(cosAng, sinAng);

        float rayPixels = frac(rand.y) * stepSize + 1.0;

        UNITY_UNROLL
        for (int s = 0; s < STEPS; ++s)
        {
            float2 snappedUV = round(rayPixels * direction) * _ScreenSize.zw + uv;
            float3 tempViewPos = GetViewPos(snappedUV);
            rayPixels += stepSize;
            float tempAO = ComputeAO(viewPos, nor, tempViewPos);
            ao += tempAO;
        }
    }

    ao = PositivePow(ao * rcp(DIRECTIONS) * _Intensity, 0.6);
    
    return ao;
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

    return half4(ao, ao, ao, ao);
}

#endif