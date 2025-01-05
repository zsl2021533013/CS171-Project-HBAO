using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

public class BlurController : MonoBehaviour
{
    public Material material;
    [Range(2, 15)] public int blurPasses = 3;
    [Range(0, 4)] public int downSample = 0;
    [Range(0.0f, 10f)] public float offset = 0.02f;

    public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
    public int injectionPointOffset = 0;
    public ScriptableRenderPassInput inputRequirements = ScriptableRenderPassInput.Color;
    public CameraType cameraType = CameraType.Game;

    private BlurPass mMyBlurPass;


    private void OnEnable()
    {
        SetupPass();

        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
    }

    public virtual void SetupPass()
    {
        mMyBlurPass = new BlurPass();

        mMyBlurPass.renderPassEvent = injectionPoint + injectionPointOffset;
        mMyBlurPass.material = material;

        mMyBlurPass.ConfigureInput(inputRequirements);
    }

    public virtual void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (mMyBlurPass == null || material == null)
            return;

        if ((cam.cameraType & cameraType) == 0) return;

        mMyBlurPass.blurPasses = blurPasses;
        mMyBlurPass.downSample = downSample;
        mMyBlurPass.offset = offset;

        cam.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(mMyBlurPass);
    }
}