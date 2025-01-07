using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HBAO.Scripts
{
    struct HBAOParameters
    {
        private static readonly int Intensity = Shader.PropertyToID("_Intensity");
        private static readonly int Radius = Shader.PropertyToID("_Radius");
        private static readonly int InvRadius2 = Shader.PropertyToID("_InvRadius2");
        private static readonly int MaxRadius = Shader.PropertyToID("_MaxRadius");
        private static readonly int AngleBias = Shader.PropertyToID("_AngleBias");
        private static readonly int FallOff = Shader.PropertyToID("_FallOff");
        private static readonly int DepthToViewParams = Shader.PropertyToID("_DepthToViewParams");
        private static readonly int FOVCorrection = Shader.PropertyToID("_FOVCorrection");
        private static readonly int CameraNormalsTexture = Shader.PropertyToID("_CameraNormalsTexture");

        public void Setup(HBAORenderSettings settings, HBAORenderPass.HBAOPassData data)
        {
            var camera = data.camera;
            var fovRad = camera.fieldOfView * Mathf.Deg2Rad;
            var invHalfTanFOV = 1 / Mathf.Tan(fovRad * 0.5f);

            var focalLen = new Vector2(invHalfTanFOV * ((float)camera.pixelHeight / camera.pixelWidth), invHalfTanFOV);
            var invFocalLen = new Vector2(1 / focalLen.x, 1 / focalLen.y);
            var depthToViewParams = new Vector4(2 * invFocalLen.x, 2 * invFocalLen.y, -1 * invFocalLen.x, -1 * invFocalLen.y);
            
            data.Material.SetFloat(Intensity, settings.intensity);
            data.Material.SetFloat(Radius, settings.radius);
            data.Material.SetFloat(InvRadius2, 1 / (settings.radius * settings.radius));
            data.Material.SetFloat(MaxRadius, settings.maxRadius);
            data.Material.SetFloat(AngleBias, settings.angleBias);
            data.Material.SetVector(DepthToViewParams, depthToViewParams);
            data.Material.SetFloat(FallOff, settings.falloff);
            data.Material.SetFloat(FOVCorrection, SetFovCorrection(camera.fieldOfView, camera.pixelHeight));
            
            // 深度信息会被自动写入，不必额外传入，此处仅手动写入法线
            data.Material.SetTexture(CameraNormalsTexture, data.CameraNormalsTexture);
        }

        private static float SetFovCorrection(float fieldOfView, int pixelHeight)
        {
            float fovRad = fieldOfView * Mathf.Deg2Rad;
            float invHalfTanFOV = 1 / Mathf.Tan(fovRad * 0.5f);

            return pixelHeight * invHalfTanFOV * 0.25f;
        }
    }
}