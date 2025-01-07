using ShadowShard.AmbientOcclusionMaster.Runtime.Data.Settings;
using ShadowShard.AmbientOcclusionMaster.Runtime.Enums;
using ShadowShard.AmbientOcclusionMaster.Runtime.Enums.Samples;
using UnityEngine;

namespace ShadowShard.AmbientOcclusionMaster.Runtime.Data.Parameters
{
    internal struct GtaoMaterialParameters
    {
        internal Vector4 GtaoParameters;
        internal Vector4 GtaoParameters2;
        internal Vector4 GtaoDepthToViewParams;

        internal readonly bool SampleCountTwo;
        internal readonly bool SampleCountFour;
        internal readonly bool SampleCountSix;
        internal readonly bool SampleCountEight;
        internal readonly bool SampleCountTwelve;
        internal readonly bool SampleCountSixteen;

        internal GtaoMaterialParameters(AomSettings aomSettings, Camera camera)
        {
            GtaoSettings settings = aomSettings.GtaoSettings;
            int downsampleDivider = aomSettings.Downsample ? 2 : 1;
            float fovRad = camera.fieldOfView * Mathf.Deg2Rad;
            float invHalfTanFOV = 1 / Mathf.Tan(fovRad * 0.5f);

            GtaoParameters = new Vector4(
                settings.Intensity,
                settings.Radius,
                1.0f / ((int)settings.Samples + 1.0f), // Inverted Samples + One
                settings.Falloff
            );

            GtaoParameters2 = new Vector4(
                SetMaxRadius(camera.pixelWidth, camera.pixelHeight, settings.MaxRadiusPixel),
                1.0f / (GtaoParameters.y * GtaoParameters.y),
                camera.pixelHeight * invHalfTanFOV * 0.25f,
                settings.Directions
            );

            Vector2 focalLen = new(invHalfTanFOV * ((float)camera.pixelHeight / downsampleDivider / ((float)camera.pixelWidth / downsampleDivider)), invHalfTanFOV);
            Vector2 invFocalLen = new(1 / focalLen.x, 1 / focalLen.y);
            GtaoDepthToViewParams = new Vector4(2 * invFocalLen.x, 2 * invFocalLen.y, -1 * invFocalLen.x, -1 * invFocalLen.y);

            SampleCountTwo = settings.Samples == GtaoSamples.Samples2;
            SampleCountFour = settings.Samples == GtaoSamples.Samples4;
            SampleCountSix = settings.Samples == GtaoSamples.Samples6;
            SampleCountEight = settings.Samples == GtaoSamples.Samples8;
            SampleCountTwelve = settings.Samples == GtaoSamples.Samples12;
            SampleCountSixteen = settings.Samples == GtaoSamples.Samples16;
        }

        internal bool Equals(GtaoMaterialParameters other)
        {
            return GtaoParameters == other.GtaoParameters
                   && GtaoParameters2 == other.GtaoParameters2
                   && GtaoDepthToViewParams == other.GtaoDepthToViewParams
                   && SampleCountTwo == other.SampleCountTwo
                   && SampleCountFour == other.SampleCountFour
                   && SampleCountSix == other.SampleCountSix
                   && SampleCountEight == other.SampleCountEight
                   && SampleCountTwelve == other.SampleCountTwelve
                   && SampleCountSixteen == other.SampleCountSixteen;
        }

        private static float SetMaxRadius(int pixelWidth, int pixelHeight, int maxRadiusPixels)
        {
            float aspectRatio = (float)pixelWidth * pixelHeight;
            const float referenceAspectRatio = 540.0f * 960.0f;

            float aspectRatioRatio = aspectRatio / referenceAspectRatio;

            return Mathf.Max(4, maxRadiusPixels * Mathf.Sqrt(aspectRatioRatio));
        }
    }
}