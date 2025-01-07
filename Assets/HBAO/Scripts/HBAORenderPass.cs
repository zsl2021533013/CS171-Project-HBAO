using HBAO.Scripts;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace HBAO
{
    public class HBAORenderPass : ScriptableRenderPass
    {
        public class HBAOPassData
        {
            public Material Material;
            public TextureHandle SourceTexture;
            public TextureHandle AOTexture;
            public TextureHandle FinalTexture;
            public TextureHandle BlurTexture;
            public TextureHandle CameraNormalsTexture;
            public Camera camera;
        }

        private HBAORenderSettings renderSettings;
        private HBAOParameters parameters;
        
        private Shader shader;
        private Material material;
        public Material Material {  
            get {
                material = CheckShaderAndCreateMaterial(shader, material);
                return material;
            }  
        }

        private const string passName = "HBAO Pass";

        public void Init(Shader shader, HBAORenderSettings renderSettings)
        {
            this.shader = shader;
            this.renderSettings = renderSettings;

            profilingSampler = new ProfilingSampler("HBAORenderPass");
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public void Setup()
        {
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        public void Dispose()
        {
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
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogError("HBAO Render Pass requires a back buffer.");
                return;
            }

            var sourceTextureDescriptor = resourceData.activeColorTexture.GetDescriptor(renderGraph);
            sourceTextureDescriptor.clearBuffer = false;
            
            sourceTextureDescriptor.name = "OcclusionTexture0";
            var aoTexture = renderGraph.CreateTexture(sourceTextureDescriptor);
            
            sourceTextureDescriptor.name = "OcclusionTexture1";
            var blurTexture = renderGraph.CreateTexture(sourceTextureDescriptor);
            
            sourceTextureDescriptor.name = "OcclusionTexture2";
            var finalTexture = renderGraph.CreateTexture(sourceTextureDescriptor);
            
            renderGraph.AddCopyPass(resourceData.activeColorTexture, finalTexture);

            using (var builder = renderGraph.AddUnsafePass(passName, out HBAOPassData passData, profilingSampler))
            {
                passData.Material = Material;
                passData.SourceTexture = resourceData.activeColorTexture;
                passData.AOTexture = aoTexture;
                passData.BlurTexture = blurTexture;
                passData.FinalTexture = finalTexture;
                passData.CameraNormalsTexture = resourceData.cameraNormalsTexture;
                passData.camera = cameraData.camera;

                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                builder.UseTexture(resourceData.cameraNormalsTexture, AccessFlags.Read);
                builder.UseTexture(passData.SourceTexture, AccessFlags.Read);
                builder.UseTexture(passData.AOTexture, AccessFlags.ReadWrite);
                builder.UseTexture(passData.BlurTexture, AccessFlags.ReadWrite);
                builder.UseTexture(passData.FinalTexture, AccessFlags.ReadWrite);

                builder.SetRenderFunc((HBAOPassData data, UnsafeGraphContext context) =>
                {
                    parameters = new HBAOParameters();
                    parameters.Setup(renderSettings, data);
                    
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                    Blitter.BlitCameraTexture(cmd, data.SourceTexture, data.AOTexture,
                        RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, data.Material, 0);

                    Blitter.BlitCameraTexture(cmd, data.AOTexture, data.BlurTexture,
                        RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, data.Material, 1);
                
                    Blitter.BlitCameraTexture(cmd, data.BlurTexture, data.FinalTexture,
                        RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, data.Material, 2);
                });
            }
            
            resourceData.cameraColor = finalTexture;
        }
        
        private Material CheckShaderAndCreateMaterial(Shader shader, Material material) {
            if (shader == null) {
                return null;
            }
		
            if (shader.isSupported && material && material.shader == shader)
                return material;
		
            if (!shader.isSupported) {
                return null;
            }
            else {
                material = new Material(shader);
                material.hideFlags = HideFlags.DontSave;
                if (material)
                    return material;
                else 
                    return null;
            }
        }
    }
}