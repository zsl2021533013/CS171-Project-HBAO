using System.IO;
using ShadowShard.AmbientOcclusionMaster.Runtime.Enums;
using ShadowShard.AmbientOcclusionMaster.Runtime.Volume;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using static ShadowShard.AmbientOcclusionMaster.Runtime.Enums.AmbientOcclusionMode;
using RenderingPath = ShadowShard.AmbientOcclusionMaster.Runtime.Enums.RenderingPath;

namespace ShadowShard.AmbientOcclusionMaster.Editor.Volume
{
    [CustomEditor(typeof(AmbientOcclusionMasterComponent))]
    internal class AomComponentEditor : VolumeComponentEditor
    {
        private Texture2D _coverImage;

        // AO Mode
        private SerializedDataParameter _mode;

        // SSAO Settings
        private SerializedDataParameter _ssaoIntensity;
        private SerializedDataParameter _ssaoRadius;
        private SerializedDataParameter _ssaoFalloff;
        private SerializedDataParameter _ssaoSamples;

        // HBAO Settings
        private SerializedDataParameter _hdaoIntensity;
        private SerializedDataParameter _hdaoNormalIntensity;
        private SerializedDataParameter _hdaoRadius;
        private SerializedDataParameter _hdaoAcceptRadius;
        private SerializedDataParameter _hdaoFalloff;
        private SerializedDataParameter _hdaoSamples;

        // HBAO Settings
        private SerializedDataParameter _hbaoIntensity;
        private SerializedDataParameter _hbaoRadius;
        private SerializedDataParameter _hbaoMaxRadiusInPixels;
        private SerializedDataParameter _hbaoAngleBias;
        private SerializedDataParameter _hbaoFalloff;
        private SerializedDataParameter _hbaoDirections;
        private SerializedDataParameter _hbaoSamples;

        // GTAO Settings
        private SerializedDataParameter _gtaoIntensity;
        private SerializedDataParameter _gtaoRadius;
        private SerializedDataParameter _gtaoMaxRadiusInPixels;
        private SerializedDataParameter _gtaoFalloff;
        private SerializedDataParameter _gtaoDirections;
        private SerializedDataParameter _gtaoSamples;

        // General AOM settings
        //private SerializedDataParameter _color;
        private SerializedDataParameter _directLightingStrength;
        private SerializedDataParameter _noiseType;
        private SerializedDataParameter _blurMode;

        // Rendering AOM settings
        private SerializedDataParameter _debugMode;
        private SerializedDataParameter _renderPath;
        private SerializedDataParameter _afterOpaque;
        private SerializedDataParameter _downsample;
        private SerializedDataParameter _source;
        private SerializedDataParameter _normalsQuality;

        public override void OnEnable()
        {
            PropertyFetcher<AmbientOcclusionMasterComponent> o = new(serializedObject);

            string publisherPath = FindAmbientOcclusionMasterPath();
            if (string.IsNullOrEmpty(publisherPath))
            {
                Debug.LogError("ShadowShard folder not found.");
            }

            string filePath = FindFilePath(publisherPath, "ShadowShardAmbientOcclusionMasterLogo.png");
            _coverImage = AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);

            // Mode
            _mode = Unpack(o.Find(x => x.Mode));

            // SSAO
            _ssaoIntensity = Unpack(o.Find(x => x.SsaoIntensity));
            _ssaoRadius = Unpack(o.Find(x => x.SsaoRadius));
            _ssaoFalloff = Unpack(o.Find(x => x.SsaoFalloff));
            _ssaoSamples = Unpack(o.Find(x => x.SsaoSamplesCount));

            // HDAO
            _hdaoIntensity = Unpack(o.Find(x => x.HdaoIntensity));
            _hdaoNormalIntensity = Unpack(o.Find(x => x.HdaoNormalIntensity));
            _hdaoRadius = Unpack(o.Find(x => x.HdaoRejectRadius));
            _hdaoAcceptRadius = Unpack(o.Find(x => x.HdaoAcceptRadius));
            _hdaoFalloff = Unpack(o.Find(x => x.HdaoFalloff));
            _hdaoSamples = Unpack(o.Find(x => x.HdaoSamples));

            // HBAO
            _hbaoIntensity = Unpack(o.Find(x => x.HbaoIntensity));
            _hbaoRadius = Unpack(o.Find(x => x.HbaoRadius));
            _hbaoMaxRadiusInPixels = Unpack(o.Find(x => x.HbaoMaxRadiusInPixels));
            _hbaoAngleBias = Unpack(o.Find(x => x.HbaoAngleBias));
            _hbaoFalloff = Unpack(o.Find(x => x.HbaoFalloff));
            _hbaoDirections = Unpack(o.Find(x => x.HbaoDirections));
            _hbaoSamples = Unpack(o.Find(x => x.HbaoSamples));

            // GTAO
            _gtaoIntensity = Unpack(o.Find(x => x.GtaoIntensity));
            _gtaoRadius = Unpack(o.Find(x => x.GtaoRadius));
            _gtaoMaxRadiusInPixels = Unpack(o.Find(x => x.GtaoMaxRadiusInPixels));
            _gtaoFalloff = Unpack(o.Find(x => x.GtaoFalloff));
            _gtaoDirections = Unpack(o.Find(x => x.GtaoDirections));
            _gtaoSamples = Unpack(o.Find(x => x.GtaoSamples));

            // General AOM
            //_color = Unpack(o.Find(x => x.AmbientOcclusionColor));
            _directLightingStrength = Unpack(o.Find(x => x.DirectLightingStrength));
            _noiseType = Unpack(o.Find(x => x.NoiseType));
            _blurMode = Unpack(o.Find(x => x.BlurMode));

            // Rendering AOM
            _debugMode = Unpack(o.Find(x => x.DebugMode));
            _renderPath = Unpack(o.Find(x => x.RenderPath));
            _afterOpaque = Unpack(o.Find(x => x.AfterOpaque));
            _downsample = Unpack(o.Find(x => x.Downsample));
            _source = Unpack(o.Find(x => x.Source));
            _normalsQuality = Unpack(o.Find(x => x.NormalsQuality));
        }

        public override void OnInspectorGUI()
        {
            DrawAssetLogo();

            serializedObject.Update();
            DrawAmbientOcclusionMode();
            AmbientOcclusionMode ambientOcclusionMode = GetAmbientOcclusionMode();
            if (ambientOcclusionMode == None)
                return;

            DrawAmbientOcclusionSettings(ambientOcclusionMode);
            DrawGeneralSettings();
            DrawRenderSettings(ambientOcclusionMode);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAssetLogo()
        {
            if (_coverImage == null)
                return;

            const float scrollbarWidth = 15f;
            float availableWidth = EditorGUIUtility.currentViewWidth - scrollbarWidth - 20;

            float aspectRatio = (float)_coverImage.width / _coverImage.height;
            float height = availableWidth / aspectRatio;

            Rect rect = GUILayoutUtility.GetRect(availableWidth, height);

            GUI.DrawTexture(rect, _coverImage, ScaleMode.ScaleToFit);
        }

        private void DrawAmbientOcclusionMode() =>
            PropertyField(_mode);

        private void DrawAmbientOcclusionSettings(AmbientOcclusionMode ambientOcclusionMode)
        {
            switch (ambientOcclusionMode)
            {
                case SSAO:
                    DrawSsaoSettings(ambientOcclusionMode);
                    break;
                case HDAO:
                    DrawHdaoSettings(ambientOcclusionMode);
                    break;
                case HBAO:
                    DrawHbaoSettings(ambientOcclusionMode);
                    break;
                case GTAO:
                    DrawGtaoSettings(ambientOcclusionMode);
                    break;
            }
        }

        private void DrawSsaoSettings(AmbientOcclusionMode ambientOcclusionMode)
        {
            EditorGUILayout.LabelField("SSAO Settings", EditorStyles.boldLabel);
            PropertyField(_ssaoIntensity, new GUIContent("Intensity"));
            PropertyField(_ssaoRadius, new GUIContent("Radius"));
            PropertyField(_ssaoFalloff, new GUIContent("Falloff"));
            PropertyField(_ssaoSamples, new GUIContent("Samples"));
            AomWarnings.DisplayAoSettingsWarningIfNeeded(_ssaoIntensity, _ssaoRadius, _ssaoFalloff,
                ambientOcclusionMode, SSAO);
        }

        private void DrawHdaoSettings(AmbientOcclusionMode ambientOcclusionMode)
        {
            EditorGUILayout.LabelField("HDAO Settings", EditorStyles.boldLabel);
            PropertyField(_hdaoIntensity, new GUIContent("Intensity"));
            PropertyField(_hdaoNormalIntensity, new GUIContent("Normal Intensity"));

            if (_hdaoNormalIntensity.value.floatValue > 0.001f)
            {
                const string infoMessage =
                    "The impact of HDAO normals is significant and costly. It is recommended to set the value to 0 for normal intensity";
                AomWarnings.DisplayHdaoInfo(ambientOcclusionMode, infoMessage);
            }

            PropertyField(_hdaoRadius, new GUIContent("Reject Radius"));
            PropertyField(_hdaoAcceptRadius, new GUIContent("Accept Radius"));
            PropertyField(_hdaoFalloff, new GUIContent("Falloff"));
            PropertyField(_hdaoSamples, new GUIContent("Samples"));
            AomWarnings.DisplayAoSettingsWarningIfNeeded(_hdaoIntensity, _hdaoRadius, _hdaoFalloff,
                ambientOcclusionMode, HDAO);
        }

        private void DrawHbaoSettings(AmbientOcclusionMode ambientOcclusionMode)
        {
            EditorGUILayout.LabelField("HBAO Settings", EditorStyles.boldLabel);
            PropertyField(_hbaoIntensity, new GUIContent("Intensity"));
            PropertyField(_hbaoRadius, new GUIContent("Radius"));
            PropertyField(_hbaoMaxRadiusInPixels, new GUIContent("Max Radius In Pixels"));
            PropertyField(_hbaoAngleBias, new GUIContent("Angle Bias"));
            PropertyField(_hbaoFalloff, new GUIContent("Falloff"));
            PropertyField(_hbaoDirections, new GUIContent("Directions"));
            PropertyField(_hbaoSamples, new GUIContent("Samples"));
            AomWarnings.DisplayAoSettingsWarningIfNeeded(_hbaoIntensity, _hbaoRadius, _hbaoFalloff,
                ambientOcclusionMode, HBAO);
        }

        private void DrawGtaoSettings(AmbientOcclusionMode ambientOcclusionMode)
        {
            EditorGUILayout.LabelField("GTAO Settings", EditorStyles.boldLabel);
            PropertyField(_gtaoIntensity, new GUIContent("Intensity"));
            PropertyField(_gtaoRadius, new GUIContent("Radius"));
            PropertyField(_gtaoMaxRadiusInPixels, new GUIContent("Max Radius In Pixels"));
            PropertyField(_gtaoFalloff, new GUIContent("Falloff"));
            PropertyField(_gtaoDirections, new GUIContent("Directions"));
            PropertyField(_gtaoSamples, new GUIContent("Samples"));
            AomWarnings.DisplayAoSettingsWarningIfNeeded(_gtaoIntensity, _gtaoRadius, _gtaoFalloff,
                ambientOcclusionMode, GTAO);
        }

        private void DrawGeneralSettings()
        {
            EditorGUILayout.LabelField("General Settings", EditorStyles.boldLabel);
            //PropertyField(_color, new GUIContent("AO Color"));
            PropertyField(_directLightingStrength, new GUIContent("Direct Lighting Strength"));
            PropertyField(_noiseType, new GUIContent("Noise Type"));
            PropertyField(_blurMode, new GUIContent("Blur Mode"));
        }

        private void DrawRenderSettings(AmbientOcclusionMode ambientOcclusionMode)
        {
            EditorGUILayout.LabelField("Rendering Settings", EditorStyles.boldLabel);
            PropertyField(_debugMode, new GUIContent("Debug Mode"));
            PropertyField(_renderPath, new GUIContent("Render Path"));
            PropertyField(_afterOpaque, new GUIContent("After Opaque"));
            PropertyField(_downsample, new GUIContent("Downsample"));

            const string infoMessage =
                "HDAO does not support normal reconstruction from depth. HDAO uses DepthNormals Prepass";
            AomWarnings.DisplayHdaoInfo(ambientOcclusionMode, infoMessage);

            if (GetRenderingPath() == RenderingPath.Forward && GetAmbientOcclusionMode() != HDAO)
                DrawDepthNormalsSource();
        }

        private void DrawDepthNormalsSource()
        {
            PropertyField(_source, new GUIContent("Depth Source"));

            if (GetDepthSource() == DepthSource.Depth)
                PropertyField(_normalsQuality, new GUIContent("Normal Quality"));
        }

        private AmbientOcclusionMode GetAmbientOcclusionMode() =>
            (AmbientOcclusionMode)_mode.value.boxedValue;

        private RenderingPath GetRenderingPath() =>
            (RenderingPath)_renderPath.value.boxedValue;

        private DepthSource GetDepthSource() =>
            (DepthSource)_source.value.boxedValue;

        private string FindAmbientOcclusionMasterPath()
        {
            string[] urpPlusPaths = AssetDatabase.FindAssets("Ambient Occlusion Master");
            foreach (string urpPlusGuid in urpPlusPaths)
            {
                string path = AssetDatabase.GUIDToAssetPath(urpPlusGuid);
                if (Directory.Exists(path))
                    return path;
            }

            return string.Empty;
        }

        private string FindFilePath(string folderPath, string filename)
        {
            string[] files = Directory.GetFiles(folderPath, filename, SearchOption.AllDirectories);
            return files.Length > 0 ? files[0] : string.Empty;
        }
    }
}