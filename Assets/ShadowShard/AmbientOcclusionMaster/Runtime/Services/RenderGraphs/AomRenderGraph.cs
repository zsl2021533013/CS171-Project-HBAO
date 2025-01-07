using System;
using ShadowShard.AmbientOcclusionMaster.Runtime.Data;
using ShadowShard.AmbientOcclusionMaster.Runtime.Data.Passes;
using ShadowShard.AmbientOcclusionMaster.Runtime.Data.Settings;
using ShadowShard.AmbientOcclusionMaster.Runtime.Enums;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ShadowShard.AmbientOcclusionMaster.Runtime.Services.RenderGraphs
{
    internal class AomRenderGraph
    {
        private readonly Material _material;
        private readonly AomSettings _settings;
        private readonly AomTexturesAllocator _texturesAllocator;
        private readonly AomParametersService _parametersService;

        internal AomRenderGraph(
            Material material,
            AomSettings settings,
            AomTexturesAllocator texturesAllocator,
            AomParametersService parametersService)
        {
            _material = material;
            _settings = settings;
            _texturesAllocator = texturesAllocator;
            _parametersService = parametersService;
        }

        internal void Record(RenderGraph renderGraph,
            ContextContainer frameData,
            ProfilingSampler profilingSampler,
            ShaderPasses aoPass,
            bool isAfterOpaque)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            // Allocate render graph texture handles
            _texturesAllocator.AllocateAoRenderGraphTextureHandles(renderGraph,
                resourceData,
                cameraData,
                isAfterOpaque,
                out TextureHandle aoTexture,
                out TextureHandle blurTexture,
                out TextureHandle finalTexture);

            // Get the resources
            TextureHandle cameraDepthTexture = resourceData.cameraDepthTexture;
            TextureHandle cameraNormalsTexture = resourceData.cameraNormalsTexture;

            // Update keywords and other shader params
            _parametersService.SetupKeywordsAndParameters(_material, _settings, cameraData);

            using IUnsafeRenderGraphBuilder builder =
                renderGraph.AddUnsafePass("Blit AOM", out AoPassData passData, profilingSampler);

            builder.AllowGlobalStateModification(true);
            builder.AllowPassCulling(false);

            // Setup pass data
            SetupAoPassData(passData, resourceData.cameraColor, aoTexture, blurTexture, finalTexture, isAfterOpaque);

            // Declare texture inputs and outputs
            DeclarePassTextures(builder, passData, cameraDepthTexture, cameraNormalsTexture);

            builder.SetRenderFunc((AoPassData data, UnsafeGraphContext rgContext) =>
            {
                CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(rgContext.cmd);
                RenderBufferLoadAction finalLoadAction =
                    data.AfterOpaque ? RenderBufferLoadAction.Load : RenderBufferLoadAction.DontCare;

                // Setup camera textures and shader parameters
                SetupCameraTextures(cmd, data);

                // Execute AO Pass and Blur passes
                ExecuteAoPass(aoPass, cmd, data);
                ExecuteBlurPasses(cmd, data, finalLoadAction);

                // We only want URP shaders to sample AO if After Opaque is disabled...
                if (data.AfterOpaque)
                    return;

                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.ScreenSpaceOcclusion, true);
                _parametersService.SetAmbientOcclusionParams(cmd, data.DirectLightingStrength);
            });
        }

        private void SetupAoPassData(
            AoPassData passData,
            TextureHandle cameraColor,
            TextureHandle aoTexture,
            TextureHandle blurTexture,
            TextureHandle finalTexture,
            bool isAfterOpaque)
        {
            passData.Material = _material;
            passData.BlurQuality = _settings.BlurQuality;
            passData.AfterOpaque = isAfterOpaque;
            passData.DirectLightingStrength = _settings.DirectLightingStrength;
            passData.CameraColor = cameraColor;
            passData.AOTexture = aoTexture;
            passData.FinalTexture = finalTexture;
            passData.BlurTexture = blurTexture;
        }

        private void DeclarePassTextures(
            IUnsafeRenderGraphBuilder builder,
            AoPassData passData,
            TextureHandle cameraDepthTexture,
            TextureHandle cameraNormalsTexture)
        {
            builder.UseTexture(passData.AOTexture, AccessFlags.ReadWrite);

            if (passData.BlurQuality != BlurQuality.Low)
                builder.UseTexture(passData.BlurTexture, AccessFlags.ReadWrite);

            if (cameraDepthTexture.IsValid())
                builder.UseTexture(cameraDepthTexture, AccessFlags.Read);

            bool useDepthNormals = _settings.DepthSource == DepthSource.DepthNormals;
            if (useDepthNormals && cameraNormalsTexture.IsValid())
            {
                builder.UseTexture(cameraNormalsTexture, AccessFlags.Read);
                passData.CameraNormalsTexture = cameraNormalsTexture;
            }

            if (!passData.AfterOpaque && passData.FinalTexture.IsValid())
            {
                builder.UseTexture(passData.FinalTexture, AccessFlags.ReadWrite);
                builder.SetGlobalTextureAfterPass(passData.FinalTexture, PropertiesIDs.AoFinalTexture);
            }
        }

        private void SetupCameraTextures(CommandBuffer cmd, AoPassData data)
        {
            if (data.CameraColor.IsValid())
                _texturesAllocator.SetSourceSize(cmd, data.CameraColor);

            if (data.CameraNormalsTexture.IsValid())
                data.Material.SetTexture(PropertiesIDs.CameraNormalsTexture, data.CameraNormalsTexture);
        }

        private void ExecuteAoPass(ShaderPasses aoPass, CommandBuffer cmd, AoPassData data) =>
            Blitter.BlitCameraTexture(cmd, data.AOTexture, data.AOTexture,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, data.Material, (int)aoPass);

        private void ExecuteBlurPasses(CommandBuffer cmd, AoPassData data, RenderBufferLoadAction finalLoadAction)
        {
            switch (data.BlurQuality)
            {
                case BlurQuality.High:
                    PerformBilateralBlur(cmd, data, finalLoadAction);
                    break;

                case BlurQuality.Medium:
                    PerformGaussianBlur(cmd, data, finalLoadAction);
                    break;

                case BlurQuality.Low:
                    PerformKawaseBlur(cmd, data, finalLoadAction);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void PerformBilateralBlur(CommandBuffer cmd, AoPassData data, RenderBufferLoadAction finalLoadAction)
        {
            Blitter.BlitCameraTexture(cmd, data.AOTexture, data.BlurTexture,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                data.Material, (int)ShaderPasses.BilateralBlurHorizontal);

            Blitter.BlitCameraTexture(cmd, data.BlurTexture, data.AOTexture,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                data.Material, (int)ShaderPasses.BilateralBlurVertical);

            Blitter.BlitCameraTexture(cmd, data.AOTexture, data.FinalTexture, finalLoadAction,
                RenderBufferStoreAction.Store, data.Material,
                (int)(data.AfterOpaque ? ShaderPasses.BilateralAfterOpaque : ShaderPasses.BilateralBlurFinal));
        }

        private void PerformGaussianBlur(CommandBuffer cmd, AoPassData data, RenderBufferLoadAction finalLoadAction)
        {
            Blitter.BlitCameraTexture(cmd, data.AOTexture, data.BlurTexture,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                data.Material, (int)ShaderPasses.GaussianBlurHorizontal);

            Blitter.BlitCameraTexture(cmd, data.BlurTexture, data.FinalTexture, finalLoadAction,
                RenderBufferStoreAction.Store, data.Material,
                (int)(data.AfterOpaque ? ShaderPasses.GaussianAfterOpaque : ShaderPasses.GaussianBlurVertical));
        }

        private void PerformKawaseBlur(CommandBuffer cmd, AoPassData data, RenderBufferLoadAction finalLoadAction) =>
            Blitter.BlitCameraTexture(cmd, data.AOTexture, data.FinalTexture, finalLoadAction,
                RenderBufferStoreAction.Store, data.Material,
                (int)(data.AfterOpaque ? ShaderPasses.KawaseAfterOpaque : ShaderPasses.KawaseBlur));
    }
}