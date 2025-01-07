using System;
using ShadowShard.AmbientOcclusionMaster.Runtime.Data;
using ShadowShard.AmbientOcclusionMaster.Runtime.Data.Settings;
using ShadowShard.AmbientOcclusionMaster.Runtime.Enums;
using ShadowShard.AmbientOcclusionMaster.Runtime.Services;
using ShadowShard.AmbientOcclusionMaster.Runtime.Services.Performers;
using ShadowShard.AmbientOcclusionMaster.Runtime.Services.RenderGraphs;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ShadowShard.AmbientOcclusionMaster.Runtime
{
    internal class AmbientOcclusionMasterPass : ScriptableRenderPass
    {
        private readonly ProfilingSampler _profilingSampler = new(nameof(AmbientOcclusionMasterPass));

        private ScriptableRenderer _renderer;
        private Material _material;
        private AomSettings _settings;

        private AomTexturesAllocator _texturesAllocator;
        private AomPassSetup _passSetup;

        private AomKeywordsService _keywordsService;
        private AomParametersService _parametersService;

        private AomRenderGraph _renderGraph;
        private AomPerformer _performer;

        private ShaderPasses _aoPass;
        private bool _isAfterOpaque;

        internal bool Setup(
            ScriptableRenderer renderer,
            Material material,
            AomSettings defaultSettings, Texture2D[] blueNoiseTextures)
        {
            _renderer = renderer;
            _material = material;
            _settings = AomSettings.GetFromVolumeComponent(defaultSettings);

            _texturesAllocator = new AomTexturesAllocator(_settings);
            _passSetup = new AomPassSetup(_settings);

            _keywordsService = new AomKeywordsService();
            _parametersService = new AomParametersService(_keywordsService, blueNoiseTextures);

            _renderGraph = new AomRenderGraph(material, _settings, _texturesAllocator, _parametersService);
            _performer = new AomPerformer(renderer, material, _settings, _texturesAllocator,
                _passSetup, _keywordsService, _parametersService);

            ConfigurePass();

            IAmbientOcclusionSettings aoSettings = _passSetup.GetAmbientOcclusionSettings();
            return _material != null && aoSettings is { Intensity: > 0.0f, Radius: > 0.0f, Falloff: > 0.0f } &&
                   !IsAmbientOcclusionModeNone();
        }

        private void ConfigurePass()
        {
            _isAfterOpaque = _passSetup.IsAfterOpaque(_settings.AfterOpaque, _settings.DebugMode);
            _passSetup.SetRenderPassEventAndNormalsSource(_isAfterOpaque, ref _settings.DepthSource,
                out RenderPassEvent passEvent);
            renderPassEvent = passEvent;

            _passSetup.ConfigureNormalsSource(ConfigureInput);
            _aoPass = _passSetup.GetAmbientOcclusionPass();
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) =>
            _renderGraph.Record(renderGraph, frameData, _profilingSampler, _aoPass, _isAfterOpaque);

        [Obsolete(
            "This rendering path is for compatibility mode only (when Render Graph is disabled). Use Render Graph API instead.",
            false)]
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) =>
            _performer.OnCameraSetup(cmd, renderingData, ConfigureTargetAndClear);

        [Obsolete(
            "This rendering path is for compatibility mode only (when Render Graph is disabled). Use Render Graph API instead.",
            false)]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) =>
            _performer.Execute(context, _profilingSampler, _aoPass, _isAfterOpaque);

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException(nameof(cmd));

            if (!_isAfterOpaque)
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.ScreenSpaceOcclusion, false);
        }

        internal void Dispose() =>
            _performer?.Dispose();

        private void ConfigureTargetAndClear(RTHandle[] aoTextures)
        {
#pragma warning disable CS0618
            ConfigureTarget(_isAfterOpaque
                ? _renderer.cameraColorTargetHandle
                : aoTextures[AmbientOcclusionConstants.FinalTextureID]);
            ConfigureClear(ClearFlag.None, Color.white);
#pragma warning restore CS0618
        }

        private bool IsAmbientOcclusionModeNone() =>
            _settings.AmbientOcclusionMode == AmbientOcclusionMode.None;
    }
}