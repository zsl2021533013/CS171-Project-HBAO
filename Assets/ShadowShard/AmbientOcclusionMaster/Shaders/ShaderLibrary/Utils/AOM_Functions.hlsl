#ifndef SHADOWSHARD_AO_MASTER_FUNCTIONS_INCLUDED
#define SHADOWSHARD_AO_MASTER_FUNCTIONS_INCLUDED

#include "AOM_Samplers.hlsl"
#include "AOM_Constants.hlsl"
#include "AOM_Parameters.hlsl"

#define SCREEN_PARAMS               GetScaledScreenParams()
#define SAMPLE_BASEMAP(uv)          half4(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, UnityStereoTransformScreenSpaceTex(uv)));
#define SAMPLE_BASEMAP_R(uv)        half(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, UnityStereoTransformScreenSpaceTex(uv)).r);
#define SAMPLE_BLUE_NOISE(uv)       SAMPLE_TEXTURE2D(_BlueNoiseTexture, sampler_PointRepeat, UnityStereoTransformScreenSpaceTex(uv)).a;

half4 PackAONormal(half ao, half3 n)
{
    n *= HALF_HALF;
    n += HALF_HALF;
    return half4(ao, n);
}

half3 GetPackedNormal(half4 p)
{
    return p.gba * HALF_TWO - HALF_ONE;
}

half GetPackedAO(half4 p)
{
    return p.r;
}

half EncodeAO(half x)
{
    #if UNITY_COLORSPACE_GAMMA
    return half(1.0 - max(LinearToSRGB(1.0 - saturate(x)), 0.0));
    #else
    return x;
    #endif
}

half CompareNormal(half3 d1, half3 d2)
{
    return smoothstep(kGeometryCoeff, HALF_ONE, dot(d1, d2));
}

float2 GetScreenSpacePosition(float2 uv)
{
    return float2(uv * SCREEN_PARAMS.xy * _Downsample);
}

// Pseudo random number generator
half GetSsaoRandomVal(half u, half sampleIndex)
{
    return SSAORandomUV[u * 20 + sampleIndex];
}

#endif
