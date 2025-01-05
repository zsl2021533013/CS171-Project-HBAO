using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace HBAO
{
    public class HBAORenderPass : ScriptableRenderPass
    {
        private HBAORenderSettings renderSettings;
        private Material material;
        private ComputeBuffer noiseCB;
        
        private const string passName = "HBAO";
        private static readonly int SourceTex = Shader.PropertyToID("_MainTex");
        private static readonly int Intensity = Shader.PropertyToID("_Intensity");
        private static readonly int Radius = Shader.PropertyToID("_Radius");
        private static readonly int InvRadius2 = Shader.PropertyToID("_InvRadius2");
        private static readonly int MaxRadiusPixels = Shader.PropertyToID("_MaxRadiusPixels");
        private static readonly int AngleBias = Shader.PropertyToID("_AngleBias");
        private static readonly int MaxDistance = Shader.PropertyToID("_MaxDistance");
        private static readonly int DistanceFalloff = Shader.PropertyToID("_DistanceFalloff");
        private static readonly int NoiseCb = Shader.PropertyToID("_NoiseCB");

        private class HBAORenderPassData
        {
            public TextureHandle src;
            public TextureHandle dest;
            public Material material;
            public HBAORenderSettings renderSettings;
        }
        
        private Vector2[] GenerateNoise()
        {
            var noises = new Vector2[4 * 4];

            for (int i = 0; i < noises.Length; i++)
            {
                float x = Random.value;
                float y = Random.value;
                noises[i] = new Vector2(x, y);
            }

            return noises;
        }

        public void Init(Shader shader, HBAORenderSettings renderSettings)
        {
            if (shader)
            {
                this.material = new Material(shader);
            }
            else
            {
                this.material = new Material(Shader.Find("Standard"));
            }
            
            this.renderSettings = renderSettings;

            noiseCB?.Release();
            var noiseData = GenerateNoise();
            noiseCB = new ComputeBuffer(noiseData.Length, sizeof(float) * 2);
            noiseCB.SetData(noiseData);
            
            profilingSampler = new ProfilingSampler("HBAORenderPass");
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }
        
        public void Setup()
        {
            requiresIntermediateTexture = true;
        }

        public void Dispose()
        {
            if (noiseCB != null)
            {
                noiseCB.Release();
                noiseCB = null;
            }
            
            if (Application.isPlaying)
            {
                Object.Destroy(material);
            }
            else
            {
                Object.DestroyImmediate(material);
            }
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogError("HBAO Render Pass requires a back buffer.");
                return;
            }
            
            var source = resourceData.activeColorTexture;
            
            var destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = "HBAO";
            // destinationDesc.clearBuffer = false;
            
            var destination = renderGraph.CreateTexture(destinationDesc);
            
            var materialPropertyBlock = new MaterialPropertyBlock();
            materialPropertyBlock.SetBuffer(NoiseCb, noiseCB);
            materialPropertyBlock.SetFloat(Intensity, renderSettings.intensity);
            materialPropertyBlock.SetFloat(Radius, renderSettings.radius);
            materialPropertyBlock.SetFloat(InvRadius2, 1 / (renderSettings.radius * renderSettings.radius));
            materialPropertyBlock.SetFloat(MaxRadiusPixels, renderSettings.maxRadiusPixels);
            materialPropertyBlock.SetFloat(AngleBias, renderSettings.angleBias);
            materialPropertyBlock.SetFloat(MaxDistance, renderSettings.maxDistance);
            materialPropertyBlock.SetFloat(DistanceFalloff, renderSettings.distanceFalloff);
            
            var para = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0, 
                materialPropertyBlock, RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle);
            renderGraph.AddBlitPass(para, passName);
            
            resourceData.cameraColor = destination;
        }
    }
}