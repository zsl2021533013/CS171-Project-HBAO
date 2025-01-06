#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

#define DIRECTIONS 8
#define STEPS 6

StructuredBuffer<float2> _NoiseCB;

TEXTURE2D(_AOTexture);

float _Intensity;
float _Radius;
float _InvRadius2;
float _MaxRadiusPixels;
float _AngleBias;
float _MaxDistance;

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

float3 GetViewNormal(float2 uv)
{
    float3 N = SampleSceneNormals(uv);
    N = TransformWorldToViewDir(N, true);
    N.y = -N.y;
    N.z = -N.z;

    return N;
}

float Falloff(float distanceSquare)
{
    return 1.0 - distanceSquare * _InvRadius2;
}

float ComputeAO(float3 p, float3 n, float3 s)
{
    float3 v = s - p;
    float VoV = dot(v, v);
    float NoV = dot(n, v) * rsqrt(VoV); // rsqrt: VoV 平方根导数，本质上在求解 N 在 V 上的投影长度

    return saturate(NoV - _AngleBias) * saturate(Falloff(VoV));
}

half4 unity_ambient_occlusion_frag(Varyings i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(i.texcoord);
    half ao = aoFactor.indirectAmbientOcclusion;
    return half4(ao, ao, ao, ao);
}

half4 ambient_occlusion_frag(Varyings i) : SV_Target
{
    float2 uv = i.texcoord;

    float3 viewPos = GetViewPos(uv);
    if (viewPos.z >= _MaxDistance)
    {
        return half4(1, 1, 1, 1);
    }

    float3 nor = GetViewNormal(uv);

    int noiseX = (uv.x * _ScreenSize.x) % 4;
    int noiseY = (uv.y * _ScreenSize.y) % 4;
    int noiseIndex = 4 * noiseY + noiseX;
    float2 rand = _NoiseCB[noiseIndex];

    // 此处除以 viewPos.z 是为了让远处的物体步长更大
    float stepSize = min(_Radius / viewPos.z, _MaxRadiusPixels) / (STEPS + 1.0);
    float stepAng = TWO_PI / DIRECTIONS;

    float ao = 0;

    UNITY_UNROLL
    for (int d = 0; d < DIRECTIONS; ++d)
    {
        float cosAng, sinAng;
        float angle = stepAng * (float(d) + rand.x);
        sincos(angle, sinAng, cosAng);

        float2 direction = float2(cosAng, sinAng);

        float rayPixels = frac(rand.y) * stepSize + 1.0;

        UNITY_UNROLL
        for (int s = 0; s < STEPS; ++s)
        {
            // 乘上 _ScreenSize.zw，即除以屏幕长宽，即将原像素长度重新映射回 [0, 1]
            float2 snappedUV = round(rayPixels * direction) * _ScreenSize.zw + uv;
            float3 tempViewPos = GetViewPos(snappedUV);
            rayPixels += stepSize;
            float tempAO = ComputeAO(viewPos, nor, tempViewPos);
            ao += tempAO;
        }
    }

    //apply bias multiplier
    ao *= _Intensity / (STEPS * DIRECTIONS);

    float distFactor = saturate((viewPos.z - (_MaxDistance - 100)) / 100);

    ao = lerp(saturate(1 - ao), 1, distFactor);

    return (ao, ao, ao, ao);
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
    half4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
    half ao = SAMPLE_TEXTURE2D(_AOTexture, sampler_LinearClamp, uv).r;

    color *= ao;

    return half4(color.r, color.g, color.b, 1);
}
