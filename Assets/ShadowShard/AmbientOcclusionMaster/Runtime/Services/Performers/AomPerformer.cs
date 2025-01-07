using System;
using System.Collections.Generic;
using System.Reflection;
using ShadowShard.AmbientOcclusionMaster.Runtime.Data;
using ShadowShard.AmbientOcclusionMaster.Runtime.Data.Settings;
using ShadowShard.AmbientOcclusionMaster.Runtime.Enums;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShadowShard.AmbientOcclusionMaster.Runtime.Services.Performers
{
    internal class AomPerformer
    {
        private readonly ScriptableRenderer _renderer;
        private readonly Material _material;
        private readonly AomSettings _settings;

        private readonly AomTexturesAllocator _texturesAllocator;
        private readonly AomPassSetup _passSetup;

        private readonly AomKeywordsService _keywordsService;
        private readonly AomParametersService _parametersService;

        private RTHandle[] _aoTextures = new RTHandle[4];

        internal AomPerformer(
            ScriptableRenderer renderer,
            Material material,
            AomSettings settings,
            AomTexturesAllocator texturesAllocator,
            AomPassSetup passSetup,
            AomKeywordsService keywordsService,
            AomParametersService parametersService)
        {
            _renderer = renderer;
            _material = material;
            _settings = settings;

            _texturesAllocator = texturesAllocator;
            _passSetup = passSetup;

            _keywordsService = keywordsService;
            _parametersService = parametersService;
        }

        [Obsolete(
            "This rendering path is for compatibility mode only (when Render Graph is disabled). Use Render Graph API instead.",
            false)]
        internal void OnCameraSetup(CommandBuffer cmd, RenderingData renderingData, Action<RTHandle[]> configureTarget)
        {
            ContextContainer frameData = GetFrameDataUsingReflection(renderingData);
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            _parametersService.SetupKeywordsAndParameters(_material, _settings, cameraData);
            _texturesAllocator.AllocateAoPerformerTextures(cmd, renderingData, ref _aoTextures);
            configureTarget.Invoke(_aoTextures);
        }

        [Obsolete(
            "This rendering path is for compatibility mode only (when Render Graph is disabled). Use Render Graph API instead.",
            false)]
        internal void Execute(ScriptableRenderContext context, ProfilingSampler profilingSampler, ShaderPasses aoPass,
            bool isAfterOpaque)
        {
            if (_material == null)
            {
                Debug.LogErrorFormat(
                    "{0}.Execute(): Missing material. ShadowShard AmbientOcclusionMaster pass will not execute. Check for missing reference in the renderer resources.",
                    GetType().Name);
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, profilingSampler))
            {
                _keywordsService.SetGlobalSsaoKeyword(cmd, isAfterOpaque, IsAmbientOcclusionModeNone());
                cmd.SetGlobalTexture(AmbientOcclusionConstants.AoTextureName,
                    _aoTextures[AmbientOcclusionConstants.FinalTextureID]);

#if ENABLE_VR && ENABLE_XR_MODULE
                bool isFoveatedEnabled = HandleFoveatedRendering(cmd, renderingData);
#endif

                _passSetup.GetPassOrder(isAfterOpaque, out int[] textureIndices, out ShaderPasses[] shaderPasses);
                ExecuteAmbientOcclusion(cmd, aoPass);
                ExecuteBlurPasses(cmd, shaderPasses, textureIndices);
                _parametersService.SetAmbientOcclusionParams(cmd, _settings.DirectLightingStrength);

#if ENABLE_VR && ENABLE_XR_MODULE
                if (isFoveatedEnabled)
                    cmd.SetFoveatedRenderingMode(FoveatedRenderingMode.Disabled);
#endif
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }

        internal void Dispose()
        {
            _aoTextures[0]?.Release();
            _aoTextures[1]?.Release();
            _aoTextures[2]?.Release();
            _aoTextures[3]?.Release();
        }

        private ContextContainer GetFrameDataUsingReflection(RenderingData renderingData)
        {
            // Get the type of RenderingData
            Type renderingDataType = typeof(RenderingData);

            // Find the 'frameData' field, which is internal
            FieldInfo frameDataField =
                renderingDataType.GetField("frameData", BindingFlags.NonPublic | BindingFlags.Instance);

            if (frameDataField != null)
            {
                // Retrieve the value of the internal 'frameData' field
                return (ContextContainer)frameDataField.GetValue(renderingData);
            }
            else
            {
                Debug.LogError("frameData field not found.");
                return null;
            }
        }

        private bool HandleFoveatedRendering(CommandBuffer cmd, RenderingData renderingData)
        {
            bool isFoveatedEnabled = false;
            if (renderingData.cameraData.xr.supportsFoveatedRendering)
            {
                if (_settings.Downsample ||
                    SystemInfo.foveatedRenderingCaps == FoveatedRenderingCaps.NonUniformRaster ||
                    (SystemInfo.foveatedRenderingCaps == FoveatedRenderingCaps.FoveationImage &&
                     _settings.DepthSource == DepthSource.Depth))
                {
                    cmd.SetFoveatedRenderingMode(FoveatedRenderingMode.Disabled);
                }
                else if (SystemInfo.foveatedRenderingCaps == FoveatedRenderingCaps.FoveationImage)
                {
                    cmd.SetFoveatedRenderingMode(FoveatedRenderingMode.Enabled);
                    isFoveatedEnabled = true;
                }
            }

            if (isFoveatedEnabled)
                cmd.SetFoveatedRenderingMode(FoveatedRenderingMode.Disabled);

            return isFoveatedEnabled;
        }

        [Obsolete("Obsolete")]
        private void ExecuteAmbientOcclusion(CommandBuffer cmd, ShaderPasses aoPass)
        {
            RTHandle cameraDepthTargetHandle = _renderer.cameraDepthTargetHandle;
            RenderAndSetBaseMap(cmd, _material, ref cameraDepthTargetHandle, ref _aoTextures[0], aoPass);
        }

        [Obsolete("Obsolete")]
        private void ExecuteBlurPasses(CommandBuffer cmd, IReadOnlyList<ShaderPasses> shaderPasses,
            IReadOnlyList<int> textureIndices)
        {
            for (int i = 0; i < shaderPasses.Count; i++)
            {
                int baseMapIndex = textureIndices[i];
                int targetIndex = textureIndices[i + 1];
                RenderAndSetBaseMap(cmd, _material, ref _aoTextures[baseMapIndex], ref _aoTextures[targetIndex],
                    shaderPasses[i]);
            }
        }

        [Obsolete("Obsolete")]
        private void RenderAndSetBaseMap(CommandBuffer cmd, Material material, ref RTHandle baseMap,
            ref RTHandle target, ShaderPasses pass)
        {
            if (_passSetup.IsAfterOpaquePass(pass))
            {
                Blitter.BlitCameraTexture(cmd, baseMap, _renderer.cameraColorTargetHandle, RenderBufferLoadAction.Load,
                    RenderBufferStoreAction.Store, material, (int)pass);
            }
            else if (baseMap.rt == null)
            {
                // Obsolete usage of RTHandle aliasing a RenderTargetIdentifier
                Vector2 viewportScale = baseMap.useScaling
                    ? new Vector2(baseMap.rtHandleProperties.rtHandleScale.x,
                        baseMap.rtHandleProperties.rtHandleScale.y)
                    : Vector2.one;

                // Will set the correct camera viewport as well.
                CoreUtils.SetRenderTarget(cmd, target);
                Blitter.BlitTexture(cmd, baseMap.nameID, viewportScale, material, (int)pass);
            }
            else
                Blitter.BlitCameraTexture(cmd, baseMap, target, material, (int)pass);
        }

        private bool IsAmbientOcclusionModeNone() =>
            _settings.AmbientOcclusionMode == AmbientOcclusionMode.None;
    }
}