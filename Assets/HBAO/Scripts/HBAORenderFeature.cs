using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HBAO
{
    [System.Serializable]
    public class HBAORenderSettings
    {
        [Range(0.0f, 5.0f)] public float intensity = 1.0f;
        [Range(0.25f, 5.0f)] public float radius = 1.2f;
        [Range(16f, 256f)] public float maxRadiusPixels = 256;
        [Range(0.0f, 0.5f)] public float angleBias = 0.05f;
        [Range(0f, 2000f)] public float maxDistance = 150.0f;
        [Range(0f, 500f)] public float distanceFalloff = 50.0f;
        [Range(0, 1f)] public float blurSize = 1.0f;
    }

    public class HBAORenderFeature : ScriptableRendererFeature
    {
        public Shader shader;
        [HideLabel] public HBAORenderSettings renderSettings;
        private HBAORenderPass renderPass;

        public override void Create()
        {
            renderPass = new HBAORenderPass();
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
