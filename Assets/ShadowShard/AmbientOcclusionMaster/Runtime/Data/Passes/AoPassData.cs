using ShadowShard.AmbientOcclusionMaster.Runtime.Enums;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;

namespace ShadowShard.AmbientOcclusionMaster.Runtime.Data.Passes
{
    internal class AoPassData
    {
        internal Material Material;
        internal float DirectLightingStrength;
        internal bool AfterOpaque;
        internal BlurQuality BlurQuality;
        internal TextureHandle CameraColor;
        internal TextureHandle AOTexture;
        internal TextureHandle FinalTexture;
        internal TextureHandle BlurTexture;
        internal TextureHandle CameraNormalsTexture;
    }
}