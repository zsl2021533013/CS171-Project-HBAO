using ShadowShard.AmbientOcclusionMaster.Runtime.Data.Settings;
using ShadowShard.AmbientOcclusionMaster.Runtime.Enums.Samples;
using UnityEngine;

namespace ShadowShard.AmbientOcclusionMaster.Runtime.Data.Parameters
{
    internal struct HdaoMaterialParameters
    {
        internal Vector4 HdaoParameters;
        internal Vector4 HdaoParameters2;
        internal Vector4 HdaoDepthToViewParams;

        internal readonly bool SampleCountLow;
        internal readonly bool SampleCountMedium;
        internal readonly bool SampleCountHigh;
        internal readonly bool SampleCountUltra;
        internal readonly bool UseNormals;

        internal HdaoMaterialParameters(AomSettings aomSettings, Camera camera)
        {
            HdaoSettings settings = aomSettings.HdaoSettings;

            int downsampleDivider = aomSettings.Downsample ? 2 : 1;
            float fovRad = camera.fieldOfView * Mathf.Deg2Rad;
            float invHalfTanFOV = 1 / Mathf.Tan(fovRad * 0.5f);

            HdaoParameters = new Vector4(
                settings.Intensity,
                settings.Radius,
                settings.AcceptRadius,
                settings.Falloff
            );

            HdaoParameters2 = new Vector4(
                GetOffsetCorrection(camera.pixelWidth, camera.pixelHeight),
                settings.NormalIntensity
            );

            Vector2 focalLen = new(invHalfTanFOV * ((float)camera.pixelHeight / downsampleDivider / ((float)camera.pixelWidth / downsampleDivider)), invHalfTanFOV);
            Vector2 invFocalLen = new(1 / focalLen.x, 1 / focalLen.y);
            HdaoDepthToViewParams = new Vector4(2 * invFocalLen.x, 2 * invFocalLen.y, -1 * invFocalLen.x, -1 * invFocalLen.y);

            SampleCountLow = settings.Samples == HdaoSamples.Low;
            SampleCountMedium = settings.Samples == HdaoSamples.Medium;
            SampleCountHigh = settings.Samples == HdaoSamples.High;
            SampleCountUltra = settings.Samples == HdaoSamples.Ultra;

            UseNormals = settings.NormalIntensity > 0.001f;
        }

        internal bool Equals(HdaoMaterialParameters other)
        {
            return HdaoParameters == other.HdaoParameters
                   && HdaoParameters2 == other.HdaoParameters2
                   && HdaoDepthToViewParams == other.HdaoDepthToViewParams
                   && SampleCountLow == other.SampleCountLow
                   && SampleCountMedium == other.SampleCountMedium
                   && SampleCountHigh == other.SampleCountHigh
                   && SampleCountUltra == other.SampleCountUltra
                   && UseNormals == other.UseNormals;
        }

        private static float GetOffsetCorrection(int pixelWidth, int pixelHeight)
        {
            float aspectRatio = (float)pixelWidth * pixelHeight;
            const float referenceAspectRatio = 540.0f * 960.0f;
            float aspectRatioRatio = aspectRatio / referenceAspectRatio;

            return Mathf.Max(4, 4 * Mathf.Sqrt(aspectRatioRatio));
        }
    }
}