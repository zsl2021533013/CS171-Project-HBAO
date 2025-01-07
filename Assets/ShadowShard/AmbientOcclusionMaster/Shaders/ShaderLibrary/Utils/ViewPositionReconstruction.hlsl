﻿#ifndef SHADOWSHARD_AO_MASTER_VIEW_POSITION_RECONSTRUCTION_INCLUDED
#define SHADOWSHARD_AO_MASTER_VIEW_POSITION_RECONSTRUCTION_INCLUDED

#include "AOM_Constants.hlsl"
#include "AOM_Parameters.hlsl"

real3 ReconstructViewPos(real2 uv, real linearDepth)
{
    #if defined(SUPPORTS_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
    UNITY_BRANCH if (_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
    {
        uv = RemapFoveatedRenderingNonUniformToLinear(uv);
    }
    #endif

    // Screen is y-inverted.
    uv.y = 1.0 - uv.y;

    // view pos in world space
    #if defined(_ORTHOGRAPHIC_PROJECTION)
        real zScale = linearDepth * _ProjectionParams.w; // divide by far plane
        real3 viewPos = _CameraViewTopLeftCorner[unity_eyeIndex].xyz
                            + _CameraViewXExtent[unity_eyeIndex].xyz * uv.x
                            + _CameraViewYExtent[unity_eyeIndex].xyz * uv.y
                            + _CameraViewZExtent[unity_eyeIndex].xyz * zScale;
    #else
    real zScale = linearDepth * _ProjectionParams2.x; // divide by near plane
    real3 viewPos = _CameraViewTopLeftCorner[unity_eyeIndex].xyz
        + _CameraViewXExtent[unity_eyeIndex].xyz * uv.x
        + _CameraViewYExtent[unity_eyeIndex].xyz * uv.y;
    viewPos *= zScale;
    #endif

    return real3(viewPos);
}

inline real3 ReconstructViewPos(real2 uv)
{
    real linearDepth = SampleAndGetLinearEyeDepth(uv);

    return ReconstructViewPos(uv, linearDepth);
}

inline real3 GetPositionVS(real2 uv, real4 depthToView, real linearDepth)
{
    return real3((uv * depthToView.xy + depthToView.zw) * linearDepth, linearDepth);
}

inline real3 GetPositionVS(real2 uv, real4 depthToView)
{
    real linearDepth = SampleAndGetLinearEyeDepth(uv);

    return real3((uv * depthToView.xy + depthToView.zw) * linearDepth, linearDepth);
}

#endif
