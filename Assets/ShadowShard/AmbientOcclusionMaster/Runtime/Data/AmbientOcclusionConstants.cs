using ShadowShard.AmbientOcclusionMaster.Runtime.Enums;
using UnityEngine;

namespace ShadowShard.AmbientOcclusionMaster.Runtime.Data
{
    internal static class AmbientOcclusionConstants
    {
        internal const int FinalTextureID = 3;
        internal const string AoTextureName = "_ScreenSpaceOcclusionTexture";
        internal const string AmbientOcclusionParamName = "_AmbientOcclusionParam";

        internal static readonly int[] BilateralTexturesIndices = { 0, 1, 2, 3 };

        internal static readonly ShaderPasses[] BilateralPasses =
        {
            ShaderPasses.BilateralBlurHorizontal, ShaderPasses.BilateralBlurVertical, ShaderPasses.BilateralBlurFinal
        };

        internal static readonly ShaderPasses[] BilateralAfterOpaquePasses =
        {
            ShaderPasses.BilateralBlurHorizontal, ShaderPasses.BilateralBlurVertical, ShaderPasses.BilateralAfterOpaque
        };

        internal static readonly int[] GaussianTexturesIndices = { 0, 1, 3, 3 };

        internal static readonly ShaderPasses[] GaussianPasses =
            { ShaderPasses.GaussianBlurHorizontal, ShaderPasses.GaussianBlurVertical };

        internal static readonly ShaderPasses[] GaussianAfterOpaquePasses =
            { ShaderPasses.GaussianBlurHorizontal, ShaderPasses.GaussianAfterOpaque };

        internal static readonly int[] KawaseTexturesIndices = { 0, 3 };
        internal static readonly ShaderPasses[] KawasePasses = { ShaderPasses.KawaseBlur };
        internal static readonly ShaderPasses[] KawaseAfterOpaquePasses = { ShaderPasses.KawaseAfterOpaque };

        internal static readonly bool SupportsR8RenderTextureFormat =
            SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8);
    }
}