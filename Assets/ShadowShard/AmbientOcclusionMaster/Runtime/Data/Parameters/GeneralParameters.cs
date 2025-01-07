using ShadowShard.AmbientOcclusionMaster.Runtime.Data.Settings;
using ShadowShard.AmbientOcclusionMaster.Runtime.Enums;

namespace ShadowShard.AmbientOcclusionMaster.Runtime.Data.Parameters
{
    internal readonly struct GeneralParameters
    {
        //internal readonly Color AoColor;

        internal readonly bool NoiseMethodPseudoRandom;
        internal readonly bool NoiseMethodBlueNoise;

        internal readonly bool OrthographicCamera;
        internal readonly bool Downsample;

        internal readonly bool SourceDepthNormals;
        internal readonly bool SourceDepthHigh;
        internal readonly bool SourceDepthMedium;
        internal readonly bool SourceDepthLow;

        internal readonly bool DebugMode;

        internal GeneralParameters(AomSettings settings, bool isOrthographic)
        {
            //AoColor = settings.AoColor;

            NoiseMethodPseudoRandom = settings.NoiseMethod == NoiseMethod.PseudoRandom;
            NoiseMethodBlueNoise = settings.NoiseMethod == NoiseMethod.BlueNoise;

            OrthographicCamera = isOrthographic;
            Downsample = settings.Downsample;

            bool isUsingDepthNormals = settings.DepthSource == DepthSource.DepthNormals;
            SourceDepthNormals = isUsingDepthNormals;
            SourceDepthHigh = !isUsingDepthNormals && settings.NormalQuality == NormalQuality.High;
            SourceDepthMedium = !isUsingDepthNormals && settings.NormalQuality == NormalQuality.Medium;
            SourceDepthLow = !isUsingDepthNormals && settings.NormalQuality == NormalQuality.Low;
            ;

            DebugMode = settings.DebugMode;
        }

        internal bool Equals(GeneralParameters other)
        {
            return //AoColor == other.AoColor
                NoiseMethodPseudoRandom == other.NoiseMethodPseudoRandom
                && NoiseMethodBlueNoise == other.NoiseMethodBlueNoise
                && OrthographicCamera == other.OrthographicCamera
                && Downsample == other.Downsample
                && SourceDepthNormals == other.SourceDepthNormals
                && SourceDepthHigh == other.SourceDepthHigh
                && SourceDepthMedium == other.SourceDepthMedium
                && SourceDepthLow == other.SourceDepthLow
                && DebugMode == other.DebugMode;
        }
    }
}