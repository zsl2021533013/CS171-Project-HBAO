using System;
using ShadowShard.AmbientOcclusionMaster.Runtime.Data.Settings;
using ShadowShard.AmbientOcclusionMaster.Runtime.Enums;
using ShadowShard.AmbientOcclusionMaster.Runtime.Services.Performers;
using ShadowShard.AmbientOcclusionMaster.Runtime.Services.RenderGraphs;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ShadowShard.AmbientOcclusionMaster.Runtime
{
    internal class AmbientOcclusionMasterDebugPass : ScriptableRenderPass
    {
        private readonly ProfilingSampler _profilingSampler = new(nameof(AmbientOcclusionMasterDebugPass));

        private Material _material;
        private AomSettings _settings;

        private AomDebugRenderGraph _renderGraph;
        private AomDebugPerformer _performer;

        public bool Setup(ScriptableRenderer renderer, Material material, AomSettings defaultSettings)
        {
            _material = material;
            _settings = AomSettings.GetFromVolumeComponent(defaultSettings);
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

            _renderGraph = new AomDebugRenderGraph(material);
            _performer = new AomDebugPerformer(renderer, material);

            IAmbientOcclusionSettings aoSettings = GetAmbientOcclusionSettings();
            return _material != null
                   && aoSettings is { Intensity: > 0.0f, Radius: > 0.0f, Falloff: > 0.0f }
                   && !IsAmbientOcclusionModeNone()
                   && _settings.DebugMode;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) =>
            _renderGraph.Record(renderGraph, frameData, _profilingSampler);

        [Obsolete(
            "This rendering path is for compatibility mode only (when Render Graph is disabled). Use Render Graph API instead.",
            false)]
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) =>
            _performer.OnCameraSetup(renderingData, ConfigureTarget);

        [Obsolete(
            "This rendering path is for compatibility mode only (when Render Graph is disabled). Use Render Graph API instead.",
            false)]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) =>
            _performer.Execute(context, _profilingSampler);

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException(nameof(cmd));
        }

        private IAmbientOcclusionSettings GetAmbientOcclusionSettings()
        {
            return _settings.AmbientOcclusionMode switch
            {
                AmbientOcclusionMode.None => null,
                AmbientOcclusionMode.SSAO => _settings.SsaoSettings,
                AmbientOcclusionMode.HDAO => _settings.HdaoSettings,
                AmbientOcclusionMode.HBAO => _settings.HbaoSettings,
                AmbientOcclusionMode.GTAO => _settings.GtaoSettings,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private bool IsAmbientOcclusionModeNone() =>
            _settings.AmbientOcclusionMode == AmbientOcclusionMode.None;
    }
}