using System;
using ShadowShard.AmbientOcclusionMaster.Runtime.Data;
using ShadowShard.AmbientOcclusionMaster.Runtime.Data.Settings;
using ShadowShard.AmbientOcclusionMaster.Runtime.Enums;
using UnityEngine.Rendering.Universal;

namespace ShadowShard.AmbientOcclusionMaster.Runtime.Services
{
    internal class AomPassSetup
    {
        private readonly AomSettings _settings;

        internal AomPassSetup(AomSettings settings) =>
            _settings = settings;

        internal void SetRenderPassEventAndNormalsSource(bool isAfterOpaque, ref DepthSource depthSource,
            out RenderPassEvent renderPassEvent)
        {
            renderPassEvent = _settings.RenderingPath == RenderingPath.Deferred
                ? isAfterOpaque ? RenderPassEvent.AfterRenderingOpaques : RenderPassEvent.AfterRenderingGbuffer
                : isAfterOpaque
                    ? RenderPassEvent.BeforeRenderingTransparents
                    : RenderPassEvent.AfterRenderingPrePasses + 1;

            if (_settings.RenderingPath == RenderingPath.Deferred ||
                _settings.AmbientOcclusionMode == AmbientOcclusionMode.HDAO)
                depthSource = DepthSource.DepthNormals;
        }

        internal void GetPassOrder(bool isAfterOpaque, out int[] textureIndices, out ShaderPasses[] shaderPasses)
        {
            switch (_settings.BlurQuality)
            {
                case BlurQuality.High: // Bilateral
                    textureIndices = AmbientOcclusionConstants.BilateralTexturesIndices;
                    shaderPasses = isAfterOpaque
                        ? AmbientOcclusionConstants.BilateralAfterOpaquePasses
                        : AmbientOcclusionConstants.BilateralPasses;
                    break;
                case BlurQuality.Medium: // Gaussian
                    textureIndices = AmbientOcclusionConstants.GaussianTexturesIndices;
                    shaderPasses = isAfterOpaque
                        ? AmbientOcclusionConstants.GaussianAfterOpaquePasses
                        : AmbientOcclusionConstants.GaussianPasses;
                    break;
                case BlurQuality.Low: // Kawase
                    textureIndices = AmbientOcclusionConstants.KawaseTexturesIndices;
                    shaderPasses = isAfterOpaque
                        ? AmbientOcclusionConstants.KawaseAfterOpaquePasses
                        : AmbientOcclusionConstants.KawasePasses;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal void ConfigureNormalsSource(Action<ScriptableRenderPassInput> configureInput)
        {
            configureInput(_settings.DepthSource switch
            {
                DepthSource.Depth => ScriptableRenderPassInput.Depth,
                DepthSource.DepthNormals => ScriptableRenderPassInput.Normal,
                _ => throw new ArgumentOutOfRangeException()
            });
        }

        internal ShaderPasses GetAmbientOcclusionPass()
        {
            return _settings.AmbientOcclusionMode switch
            {
                AmbientOcclusionMode.None => ShaderPasses.SSAO,
                AmbientOcclusionMode.SSAO => ShaderPasses.SSAO,
                AmbientOcclusionMode.HDAO => ShaderPasses.HDAO,
                AmbientOcclusionMode.HBAO => ShaderPasses.HBAO,
                AmbientOcclusionMode.GTAO => ShaderPasses.GTAO,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        internal IAmbientOcclusionSettings GetAmbientOcclusionSettings()
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

        internal bool IsAfterOpaquePass(ShaderPasses pass) =>
            pass is ShaderPasses.BilateralAfterOpaque
                or ShaderPasses.GaussianAfterOpaque
                or ShaderPasses.KawaseAfterOpaque;

        internal bool IsAfterOpaque(bool afterOpaque, bool debugMode) =>
            afterOpaque && !debugMode;
    }
}