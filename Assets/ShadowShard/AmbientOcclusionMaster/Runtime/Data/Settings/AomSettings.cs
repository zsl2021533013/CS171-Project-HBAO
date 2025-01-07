using System;
using ShadowShard.AmbientOcclusionMaster.Runtime.Enums;
using ShadowShard.AmbientOcclusionMaster.Runtime.Volume;
using UnityEngine;
using UnityEngine.Rendering;
using RenderingPath = ShadowShard.AmbientOcclusionMaster.Runtime.Enums.RenderingPath;

namespace ShadowShard.AmbientOcclusionMaster.Runtime.Data.Settings
{
    [Serializable]
    public class AomSettings
    {
        [SerializeField] public AmbientOcclusionMode AmbientOcclusionMode;

        [SerializeField] public SsaoSettings SsaoSettings = new();

        [SerializeField] public HdaoSettings HdaoSettings = new();

        [SerializeField] public HbaoSettings HbaoSettings = new();

        [SerializeField] public GtaoSettings GtaoSettings = new();

        //[SerializeField] 
        //public Color AoColor = Color.black;

        [SerializeField] public float DirectLightingStrength = 0.25f;

        [SerializeField] public NoiseMethod NoiseMethod = NoiseMethod.InterleavedGradient;

        [SerializeField] public BlurQuality BlurQuality = BlurQuality.High;

        [SerializeField] public bool DebugMode;

        [SerializeField] public RenderingPath RenderingPath = RenderingPath.Forward;

        [SerializeField] public bool AfterOpaque = true;

        [SerializeField] public bool Downsample;

        [SerializeField] public DepthSource DepthSource = DepthSource.Depth;

        [SerializeField] public NormalQuality NormalQuality = NormalQuality.Medium;

        public static AomSettings GetFromVolumeComponent(AomSettings defaultSettings)
        {
            AmbientOcclusionMasterComponent volumeComponent =
                AmbientOcclusionMasterComponent.GetAmbientOcclusionMasterComponent();
            if (volumeComponent == null)
                return defaultSettings;

            SsaoSettings ssaoSettings = SsaoSettings.GetFromVolumeComponent(volumeComponent, defaultSettings);
            HdaoSettings hdaoSettings = HdaoSettings.GetFromVolumeComponent(volumeComponent, defaultSettings);
            HbaoSettings hbaoSettings = HbaoSettings.GetFromVolumeComponent(volumeComponent, defaultSettings);
            GtaoSettings gtaoSettings = GtaoSettings.GetFromVolumeComponent(volumeComponent, defaultSettings);

            return new AomSettings
            {
                AmbientOcclusionMode = GetSetting(volumeComponent.Mode, defaultSettings.AmbientOcclusionMode),

                SsaoSettings = ssaoSettings,
                HdaoSettings = hdaoSettings,
                HbaoSettings = hbaoSettings,
                GtaoSettings = gtaoSettings,

                //AoColor = GetSetting(volumeComponent.AmbientOcclusionColor, defaultSettings.AoColor),
                DirectLightingStrength = GetSetting(volumeComponent.DirectLightingStrength,
                    defaultSettings.DirectLightingStrength),
                NoiseMethod = GetSetting(volumeComponent.NoiseType, defaultSettings.NoiseMethod),
                BlurQuality = GetSetting(volumeComponent.BlurMode, defaultSettings.BlurQuality),

                DebugMode = GetSetting(volumeComponent.DebugMode, defaultSettings.DebugMode),
                RenderingPath = GetSetting(volumeComponent.RenderPath, defaultSettings.RenderingPath),
                AfterOpaque = GetSetting(volumeComponent.AfterOpaque, defaultSettings.AfterOpaque),
                Downsample = GetSetting(volumeComponent.Downsample, defaultSettings.Downsample),
                DepthSource = GetSetting(volumeComponent.Source, defaultSettings.DepthSource),
                NormalQuality = GetSetting(volumeComponent.NormalsQuality, defaultSettings.NormalQuality)
            };
        }

        public static T GetSetting<T>(VolumeParameter<T> setting, T defaultValue) =>
            setting.overrideState ? setting.value : defaultValue;
    }
}