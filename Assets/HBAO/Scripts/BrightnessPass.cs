using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace HBAO
{
    public class BrightnessPass : ScriptableRenderPass
    {
        private BrightnessRendererSettings renderSettings;
        private Material material;
        private ComputeBuffer noiseCB;
        
        private const string passName = "Brightness";
        private static readonly int SourceTex = Shader.PropertyToID("_BlitTexture");
        private static readonly int Brightness = Shader.PropertyToID("_Brightness");
        private static readonly int Saturation = Shader.PropertyToID("_Saturation");
        private static readonly int Contrast = Shader.PropertyToID("_Contrast");
        
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

        public void Init(Shader shader, BrightnessRendererSettings renderSettings)
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
            
            profilingSampler = new ProfilingSampler("BrightnessRenderPass");
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }
        
        public void Setup()
        {
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
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
            materialPropertyBlock.SetFloat(Brightness, renderSettings.brightness);
            materialPropertyBlock.SetFloat(Saturation, renderSettings.saturation);
            materialPropertyBlock.SetFloat(Contrast, renderSettings.contrast);
            
            using (var builder = renderGraph.AddUnsafePass(passName, out PassData passData))
            {
                passData.source = source;
                passData.destination = destination;
                passData.material = material;
                
                builder.UseTexture(passData.source);
                builder.UseTexture(passData.destination, AccessFlags.Write);
                
                // passData.material.SetTexture(SourceTex, passData.source);
                
                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    passData.material.SetTexture(SourceTex, passData.source);
                    var unsafeCmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    Blitter.BlitCameraTexture(unsafeCmd, data.source, data.destination, RenderBufferLoadAction.DontCare, 
                        RenderBufferStoreAction.Store, data.material, 0);
                });
            }
            
            resourceData.cameraColor = destination;
        }
    }

    public class PassData
    {
        public TextureHandle source;
        public TextureHandle destination;
        
        public Material material;
    }
}