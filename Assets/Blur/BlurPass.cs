using System;

using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BlurPass : ScriptableRenderPass
{
    private class PassData
    {
        public TextureHandle target;
        public TextureHandle source;

        public TextureHandle finalDrawTexture;

        public int blurPasses;

        public Material material;
    }

    public Material material;
    [Range(2, 15)] public int blurPasses = 3;
    [Range(1, 4)] public int downSample = 1;
    [Range(0.0f, 10f)] public float offset = 0.2f;


    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var resourceData = frameData.Get<UniversalResourceData>();

        var w = Screen.width >> downSample;
        var h = Screen.height >> downSample;

        RenderTextureDescriptor textureProperties = new RenderTextureDescriptor(w, h, RenderTextureFormat.Default, 0);
        var rt1 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, textureProperties, "MyBlurPassTempRt1", false);

        textureProperties = new RenderTextureDescriptor(w, h, RenderTextureFormat.Default, 0);
        var rt2 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, textureProperties, "MyBlurPassTempRt2", false);

        //URP17还有一个接口renderGraph.AddBlitPass，但不能外部传PassData，因此目前版本只能用UnsafePass调用旧的Blit API来写
        using (var builder = renderGraph.AddUnsafePass("Blur Pass Begin", out PassData passData, profilingSampler))
        {
            passData.source = resourceData.activeColorTexture;
            passData.target = rt1;
            passData.material = material;

            builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Read);
            builder.UseTexture(rt1, AccessFlags.Write);
            builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecuteUnsafeBlitPass(data, context));
        }

        material.SetFloat("_SampleOffset", offset);

        using (var builder = renderGraph.AddUnsafePass("Blur Pass Iterate", out PassData passData, profilingSampler))
        {
            passData.source = rt1;
            passData.target = rt2;
            passData.material = material;
            passData.blurPasses = blurPasses;

            builder.UseTexture(rt1, AccessFlags.ReadWrite);
            builder.UseTexture(rt2, AccessFlags.ReadWrite);
            builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecuteUnsafeBlitPassIterate(data, context));
        }

        //通过直接绘制的方式，将模糊RT绘制到屏幕上
        using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out PassData passData))
        {
            passData.finalDrawTexture = rt1;

            builder.UseTexture(passData.finalDrawTexture);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);

            builder.SetRenderFunc<PassData>((passData, context) =>
            {
                MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
                materialPropertyBlock.SetTexture("_BlitTexture", passData.finalDrawTexture);
                materialPropertyBlock.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));

                context.cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, materialPropertyBlock);
            });
        }
    }

    private static void ExecuteUnsafeBlitPass(PassData passData, UnsafeGraphContext context)
    {
        CommandBuffer unsafeCmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

        TextureHandle source = passData.source;
        TextureHandle target = passData.target;

        Blitter.BlitCameraTexture(unsafeCmd, source, target, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, passData.material, 0);
    }

    private static void ExecuteUnsafeBlitPassIterate(PassData passData, UnsafeGraphContext context)
    {
        CommandBuffer unsafeCmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

        TextureHandle source = passData.source;
        TextureHandle target = passData.target;

        Blitter.BlitCameraTexture(unsafeCmd, source, target, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, passData.material, 0);

        for (int i = 0; i < passData.blurPasses; ++i)
        {
            Blitter.BlitCameraTexture(unsafeCmd, source, target, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, passData.material, 0);
            Blitter.BlitCameraTexture(unsafeCmd, target, source, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, passData.material, 0);
        }
    }
}