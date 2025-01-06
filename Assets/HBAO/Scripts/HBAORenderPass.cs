using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace HBAO
{
    public class HBAORenderPass : ScriptableRenderPass
    {
        class PassData
        {
            public static int sourceTexturePropertyID;
            public TextureHandle source;
            public TextureHandle destination;
            public Material material;
            public int shaderPass;
            public MaterialPropertyBlock propertyBlock;
            public TextureHandle blurTexture;
        }

        private HBAORenderSettings renderSettings;
        private Material material;
        private ComputeBuffer noiseCB;

        private const string passName = "HBAO";
        private static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");
        private static readonly int Intensity = Shader.PropertyToID("_Intensity");
        private static readonly int Radius = Shader.PropertyToID("_Radius");
        private static readonly int InvRadius2 = Shader.PropertyToID("_InvRadius2");
        private static readonly int MaxRadiusPixels = Shader.PropertyToID("_MaxRadiusPixels");
        private static readonly int AngleBias = Shader.PropertyToID("_AngleBias");
        private static readonly int AOMultiplier = Shader.PropertyToID("_AOMultiplier");
        private static readonly int MaxDistance = Shader.PropertyToID("_MaxDistance");
        private static readonly int DistanceFalloff = Shader.PropertyToID("_DistanceFalloff");
        private static readonly int NoiseCb = Shader.PropertyToID("_NoiseCB");
        private static readonly int BlurSize = Shader.PropertyToID("_BlurSize");
        private static readonly int AOTexture = Shader.PropertyToID("_AOTexture");

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

            #region Ambient Occlusion

            var aoDesc = renderGraph.GetTextureDesc(source);
            aoDesc.name = "Ambient Occlusion";
            var aoTH = renderGraph.CreateTexture(aoDesc);

            var materialPropertyBlock = new MaterialPropertyBlock();
            materialPropertyBlock.SetBuffer(NoiseCb, noiseCB);
            materialPropertyBlock.SetFloat(Intensity, renderSettings.intensity);
            materialPropertyBlock.SetFloat(Radius, renderSettings.radius);
            materialPropertyBlock.SetFloat(InvRadius2, 1 / (renderSettings.radius * renderSettings.radius));
            materialPropertyBlock.SetFloat(MaxRadiusPixels, renderSettings.maxRadiusPixels);
            materialPropertyBlock.SetFloat(AngleBias, renderSettings.angleBias);
            materialPropertyBlock.SetFloat(AOMultiplier, 2 / (1 - renderSettings.angleBias));
            materialPropertyBlock.SetFloat(MaxDistance, renderSettings.maxDistance);

            var para = new RenderGraphUtils.BlitMaterialParameters(source, aoTH, material, 0,
                materialPropertyBlock, RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle);
            renderGraph.AddBlitPass(para, passName);

            #endregion

            #region Blur

            var blurDesc = renderGraph.GetTextureDesc(source);
            blurDesc.name = "Blur";
            var blurTH = renderGraph.CreateTexture(blurDesc);

            materialPropertyBlock = new MaterialPropertyBlock();
            materialPropertyBlock.SetFloat(BlurSize, renderSettings.blurSize);

            para = new RenderGraphUtils.BlitMaterialParameters(aoTH, blurTH, material, 1,
                materialPropertyBlock, RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle);
            renderGraph.AddBlitPass(para, passName);

            #endregion

            #region Combine
            
            var combineDesc = renderGraph.GetTextureDesc(source);
            combineDesc.name = "Combine";
            var combineTH = renderGraph.CreateTexture(combineDesc);
            
            using (var builder = renderGraph.AddRasterRenderPass("Combine", out PassData passData, profilingSampler))
            {
                passData.source = source;
                passData.destination = combineTH;
                passData.blurTexture = blurTH;
                passData.material = material;
                passData.shaderPass = 2;

                builder.UseTexture(passData.source);
                builder.UseTexture(passData.blurTexture);
                builder.UseTexture(combineTH, AccessFlags.ReadWrite);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecuteBlurPass(data, context));
            }

            #endregion

            resourceData.cameraColor = combineTH;
        }
        
        private static void ExecuteBlurPass(PassData passData, RasterGraphContext context)
        {
            passData.material.SetTexture(AOTexture, passData.blurTexture);
            Blitter.BlitTexture(context.cmd, passData.source, new Vector4(1, 1, 0, 0), passData.material, 2);
        }
    }
}