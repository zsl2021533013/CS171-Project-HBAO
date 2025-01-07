using Sirenix.OdinInspector;
using UnityEngine;

namespace HBAO.Scripts
{
    public class SaveCamTexture : MonoBehaviour
    {
        [Button("Save Cam Texture")]
        public void CaptureScreen()
        {
            var filename = "CaptureScreen.png";
            ScreenCapture.CaptureScreenshot(filename);
            print($"success: {filename}");
        }
    }
}