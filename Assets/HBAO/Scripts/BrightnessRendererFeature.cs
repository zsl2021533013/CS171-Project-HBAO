using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HBAO
{
    [System.Serializable]
    public class BrightnessRendererSettings
    {
        [Range(0f, 5f)] public float brightness = 1f;
        [Range(0f, 1f)] public float saturation = 1f;
        [Range(0f, 1f)] public float contrast = 1f;
    }

    public class BrightnessRendererFeature : ScriptableRendererFeature
    {
        public Shader shader;
        [HideLabel] public BrightnessRendererSettings renderSettings;
        private BrightnessPass renderPass;

        public override void Create()
        {
            renderPass = new BrightnessPass();
            renderPass.Init(shader, renderSettings);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            renderPass.Setup();
        }

        protected override void Dispose(bool disposing)
        {
            renderPass.Dispose();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            SetupRenderPasses(renderer, renderingData);
            renderer.EnqueuePass(renderPass);
        }
    }
}