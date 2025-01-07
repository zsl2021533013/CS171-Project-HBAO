#ifndef SHADOWSHARD_AO_MASTER_GTAO_INCLUDED
#define SHADOWSHARD_AO_MASTER_GTAO_INCLUDED

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

#include "GtaoParameters.hlsl"

// --------------------------------------------------
// Helper Functions
// --------------------------------------------------
real IntegrateArcCosWeighted(real horizon1, real horizon2, real N, real cosN)
{
    real h1 = horizon1 * 2.0;
    real h2 = horizon2 * 2.0;
    real sinN = sin(N);

    return 0.25 * ((-cos(h1 - N) + cosN + h1 * sinN) + (-cos(h2 - N) + cosN + h2 * sinN));
}

real GTAOFastAcos(real x)
{
    real outVal = -0.156583 * abs(x) + HALF_PI;
    outVal *= sqrt(1.0 - abs(x));

    return x >= 0 ? outVal : PI - outVal;
}

// --------------------------------------------
// Get sample start offset
// --------------------------------------------
inline real GetOffset(uint2 positionSS)
{
    real offset = 0.25 * ((positionSS.y - positionSS.x) & 0x3);

    return frac(offset);
}

real2 GetDirection(real2 uv, uint2 positionSS, int offset)
{
    half noise = GetNoiseMethod(uv, positionSS);
    real rotations[] = {60.0, 300.0, 180.0, 240.0, 120.0, 0.0};

    real rotation = (rotations[offset] / 360.0);

    noise += rotation;
    noise *= PI;

    return real2(cos(noise), sin(noise));
}

// --------------------------------------------
// Input generation functions
// --------------------------------------------
real UpdateHorizon(real maxH, real candidateH, real distSq)
{
    real falloff = saturate(1.0 - distSq * INV_RADIUS_SQ);

    return (candidateH > maxH) ? lerp(maxH, candidateH, falloff) : lerp(maxH, candidateH, 0.03f);
    // TODO: Thickness heuristic here.
}

real HorizonLoop(real3 positionVS, real3 V, real2 rayStart, real2 rayDir, real rayOffset, real rayStep)
{
    real maxHorizon = -1.0f; // cos(pi)
    real t = rayOffset * rayStep + rayStep;

    for (uint i = 0; i < SAMPLES; i++)
    {
        // Calculate the screen-space position from the UV coordinates and the current ray distance.
        real2 samplePos = max(2, min(rayStart + t * rayDir, _SourceSize.xy - 2));

        // Convert the screen-space position back to normalized UV coordinates.
        real2 uvSamplePos = samplePos * _SourceSize.zw;

        #if defined(SUPPORTS_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
        UNITY_BRANCH if (_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
        {
            uvSamplePos = RemapFoveatedRenderingResolve(uvSamplePos);
        }
        #endif

        real3 samplePosVS = GetPositionVS(uvSamplePos, _DepthToViewParams);

        real3 deltaPos = samplePosVS - positionVS;
        real deltaLenSq = dot(deltaPos, deltaPos);

        real currHorizon = dot(deltaPos, V) * rsqrt(deltaLenSq);
        maxHorizon = UpdateHorizon(maxHorizon, currHorizon, deltaLenSq);

        t += rayStep;
    }

    return maxHorizon;
}

half4 GTAO(Varyings input) : SV_Target
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

    real3 normal = SampleNormal(uv, linearDepth_o, pixelDensity);
    real3 normalVS = GetNormalVS(normal);
    real3 positionVS = GetPositionVS(uv, _DepthToViewParams, linearDepth_o);
    real3 viewDirection = normalize(-positionVS);

    const real fovCorrectedRadiusSS = clamp(RADIUS * FOV_CORRECTION * rcp(linearDepth_o), SAMPLES, MAX_RADIUS);
    const real stepSize = max(1, fovCorrectedRadiusSS * INV_SAMPLE_COUNT_PLUS_ONE);

    real2 rayStart = uv * _SourceSize.xy;
    half offset = GetOffset(rayStart);

    half ao = HALF_ZERO;

    const int dirCount = DIRECTIONS;
    const half rcp_directions_count = half(rcp(DIRECTIONS));

    UNITY_UNROLL
    for (int i = 0; i < dirCount; i++)
    {
        real2 direction = GetDirection(uv, rayStart, i);
        real2 negDir = -direction + 1e-30;

        // Find horizons
        real2 maxHorizons;
        maxHorizons.x = HorizonLoop(positionVS, viewDirection, rayStart, direction, offset, stepSize);
        maxHorizons.y = HorizonLoop(positionVS, viewDirection, rayStart, negDir, offset, stepSize);

        // Integrate horizons
        real3 planeNormal = normalize(cross(real3(direction.xy, 0.0f), viewDirection));
        real3 projectedNormal = normalVS - planeNormal * dot(normalVS, planeNormal);
        real projectedNormalLength = length(projectedNormal);
        real cosN = dot(projectedNormal / projectedNormalLength, viewDirection);

        real3 T = cross(viewDirection, planeNormal);
        real N = -sign(dot(projectedNormal, T)) * acos(cosN);

        // Now we find the actual horizon angles
        maxHorizons.x = -GTAOFastAcos(maxHorizons.x);
        maxHorizons.y = GTAOFastAcos(maxHorizons.y);
        maxHorizons.x = N + max(maxHorizons.x - N, -HALF_PI);
        maxHorizons.y = N + min(maxHorizons.y - N, HALF_PI);
        ao += AnyIsNaN(maxHorizons) ? 1 : IntegrateArcCosWeighted(maxHorizons.x, maxHorizons.y, N, cosN);
    }

    half falloff = CalculateDepthFalloff(half_linear_depth_o, FALLOFF);
    ao = HALF_ONE - saturate(ao * rcp_directions_count);
    ao = PositivePow(ao * INTENSITY * falloff, kContrast);

    // Return the packed ao + normals
    return PackAONormal(ao, normal);
}

#endif
