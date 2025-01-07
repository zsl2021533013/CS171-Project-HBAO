using System;
using System.Reflection;
using ShadowShard.AmbientOcclusionMaster.Runtime.Data;
using ShadowShard.AmbientOcclusionMaster.Runtime.Data.Settings;
using ShadowShard.AmbientOcclusionMaster.Runtime.Enums;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ShadowShard.AmbientOcclusionMaster.Runtime.Services
{
    internal class AomTexturesAllocator
    {
        private readonly AomSettings _settings;

        internal AomTexturesAllocator(AomSettings settings) =>
            _settings = settings;

        internal void AllocateAoRenderGraphTextureHandles(
            RenderGraph renderGraph,
            UniversalResourceData resourceData,
            UniversalCameraData cameraData,
            bool isAfterOpaque,
            out TextureHandle aoTexture,
            out TextureHandle blurTexture,
            out TextureHandle finalTexture)
        {
            RenderTextureDescriptor finalTextureDescriptor = cameraData.cameraTargetDescriptor;
            finalTextureDescriptor.colorFormat = AmbientOcclusionConstants.SupportsR8RenderTextureFormat
                ? RenderTextureFormat.R8
                : RenderTextureFormat.ARGB32;
            finalTextureDescriptor.depthBufferBits = 0;
            finalTextureDescriptor.msaaSamples = 1;

            int downsampleDivider = _settings.Downsample ? 2 : 1;
            bool useRedComponentOnly = AmbientOcclusionConstants.SupportsR8RenderTextureFormat &&
                                       _settings.BlurQuality > BlurQuality.High;

            RenderTextureDescriptor aoBlurDescriptor = finalTextureDescriptor;
            aoBlurDescriptor.colorFormat = useRedComponentOnly ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;
            aoBlurDescriptor.width /= downsampleDivider;
            aoBlurDescriptor.height /= downsampleDivider;

            // Handles
            aoTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, aoBlurDescriptor,
                "_AOM_OcclusionTexture0", false, FilterMode.Bilinear);

            finalTexture = isAfterOpaque
                ? resourceData.activeColorTexture
                : UniversalRenderer.CreateRenderGraphTexture(renderGraph, finalTextureDescriptor,
                    AmbientOcclusionConstants.AoTextureName, false, FilterMode.Bilinear);

            blurTexture = _settings.BlurQuality != BlurQuality.Low
                ? UniversalRenderer.CreateRenderGraphTexture(renderGraph, aoBlurDescriptor, "_AOM_OcclusionTexture1",
                    false, FilterMode.Bilinear)
                : TextureHandle.nullHandle;

            if (!isAfterOpaque)
                SetSSAOTextureUsingReflection(resourceData, finalTexture);
        }

        internal void AllocateAoPerformerTextures(CommandBuffer cmd, RenderingData renderingData,
            ref RTHandle[] aoTextures)
        {
            int downsampleDivider = _settings.Downsample ? 2 : 1;
            bool useRedComponentOnly = AmbientOcclusionConstants.SupportsR8RenderTextureFormat &&
                                       _settings.BlurQuality > BlurQuality.High;

            RenderTextureDescriptor descriptor = SetupDescriptor(renderingData.cameraData.cameraTargetDescriptor,
                downsampleDivider, useRedComponentOnly);
            AllocateTexturesForAoAndBlur(ref aoTextures, descriptor);

            RenderTextureDescriptor upsampleDescriptor =
                UpsampleDescriptor(descriptor, downsampleDivider, useRedComponentOnly);
            AllocateFinalTexture(cmd, ref aoTextures, upsampleDescriptor);
        }

        internal void SetSourceSize(CommandBuffer cmd, RTHandle source)
        {
            float width = source.rt.width;
            float height = source.rt.height;

            if (source.rt.useDynamicScale)
            {
                width *= ScalableBufferManager.widthScaleFactor;
                height *= ScalableBufferManager.heightScaleFactor;
            }

            cmd.SetGlobalVector(PropertiesIDs.SourceSize, new Vector4(width, height, 1.0f / width, 1.0f / height));
        }

        private void SetSSAOTextureUsingReflection(UniversalResourceData resourceData, TextureHandle textureHandle)
        {
            Type type = typeof(UniversalResourceData);
            PropertyInfo ssaoTextureProperty = type.GetProperty("ssaoTexture",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (ssaoTextureProperty != null)
                ssaoTextureProperty.SetValue(resourceData, textureHandle);
            else
                Debug.LogError("ssaoTexture property not found.");
        }

        private RenderTextureDescriptor SetupDescriptor(RenderTextureDescriptor cameraTargetDescriptor,
            int downsampleDivider, bool useRedComponentOnly)
        {
            RenderTextureDescriptor descriptor = cameraTargetDescriptor;
            descriptor.msaaSamples = 1;
            descriptor.depthBufferBits = 0;

            descriptor.width /= downsampleDivider;
            descriptor.height /= downsampleDivider;
            descriptor.colorFormat = useRedComponentOnly ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;

            return descriptor;
        }

        private RenderTextureDescriptor UpsampleDescriptor(RenderTextureDescriptor descriptor, int downsampleDivider,
            bool useRedComponentOnly)
        {
            descriptor.width *= downsampleDivider;
            descriptor.height *= downsampleDivider;
            descriptor.colorFormat = useRedComponentOnly ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;

            return descriptor;
        }

        private void AllocateTexturesForAoAndBlur(ref RTHandle[] aoTextures, RenderTextureDescriptor descriptor)
        {
            RenderingUtils.ReAllocateHandleIfNeeded(ref aoTextures[0], descriptor, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: "_AOM_OcclusionTexture0");
            RenderingUtils.ReAllocateHandleIfNeeded(ref aoTextures[1], descriptor, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: "_AOM_OcclusionTexture1");
            RenderingUtils.ReAllocateHandleIfNeeded(ref aoTextures[2], descriptor, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: "_AOM_OcclusionTexture2");
        }

        private void AllocateFinalTexture(CommandBuffer cmd, ref RTHandle[] aoTextures,
            RenderTextureDescriptor descriptor)
        {
            RenderingUtils.ReAllocateHandleIfNeeded(ref aoTextures[AmbientOcclusionConstants.FinalTextureID],
                descriptor, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: "_AOM_OcclusionTexture");

            SetSourceSize(cmd, aoTextures[AmbientOcclusionConstants.FinalTextureID]);
        }
    }
}