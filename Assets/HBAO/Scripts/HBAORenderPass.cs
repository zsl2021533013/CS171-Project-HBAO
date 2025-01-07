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
        private static readonly int MaxRadius = Shader.PropertyToID("_MaxRadius");
        private static readonly int AngleBias = Shader.PropertyToID("_AngleBias");
        private static readonly int FallOff = Shader.PropertyToID("_FallOff");
        private static readonly int NoiseCb = Shader.PropertyToID("_NoiseCB");
        private static readonly int BlurSize = Shader.PropertyToID("_BlurSize");
        private static readonly int AOTexture = Shader.PropertyToID("_AOTexture");
        private static readonly int DepthToViewParams = Shader.PropertyToID("_DepthToViewParams");
        private static readonly int FOVCorrection = Shader.PropertyToID("_FOVCorrection");

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
            var cameraData = frameData.Get<UniversalCameraData>();
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
            
            var camera = cameraData.camera;
            var fovRad = camera.fieldOfView * Mathf.Deg2Rad;
            var invHalfTanFOV = 1 / Mathf.Tan(fovRad * 0.5f);
            
            var focalLen = new Vector2(invHalfTanFOV * ((float)camera.pixelHeight / camera.pixelWidth), invHalfTanFOV);
            var invFocalLen = new Vector2(1 / focalLen.x, 1 / focalLen.y);
            var depthToViewParams = new Vector4(2 * invFocalLen.x, 2 * invFocalLen.y, -1 * invFocalLen.x, -1 * invFocalLen.y);

            var materialPropertyBlock = new MaterialPropertyBlock();
            materialPropertyBlock.SetBuffer(NoiseCb, noiseCB);
            materialPropertyBlock.SetFloat(Intensity, renderSettings.intensity);
            materialPropertyBlock.SetFloat(Radius, renderSettings.radius);
            materialPropertyBlock.SetFloat(InvRadius2, 1 / (renderSettings.radius * renderSettings.radius));
            materialPropertyBlock.SetFloat(MaxRadius, renderSettings.maxRadius);
            materialPropertyBlock.SetFloat(AngleBias, renderSettings.angleBias);
            materialPropertyBlock.SetVector(DepthToViewParams, depthToViewParams);
            materialPropertyBlock.SetFloat(FallOff, renderSettings.falloff);
            materialPropertyBlock.SetFloat(FOVCorrection, SetFovCorrection(camera.fieldOfView, camera.pixelHeight));

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
            
            renderGraph.AddCopyPass(source, combineTH);
            
            materialPropertyBlock = new MaterialPropertyBlock();

            para = new RenderGraphUtils.BlitMaterialParameters(blurTH, combineTH, material, 2,
                materialPropertyBlock, RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle);
            renderGraph.AddBlitPass(para, passName);

            #endregion

            resourceData.cameraColor = combineTH;
        }
        
        private static float SetFovCorrection(float fieldOfView, int pixelHeight)
        {
            float fovRad = fieldOfView * Mathf.Deg2Rad;
            float invHalfTanFOV = 1 / Mathf.Tan(fovRad * 0.5f);

            return pixelHeight * invHalfTanFOV * 0.25f;
        }
    }
}