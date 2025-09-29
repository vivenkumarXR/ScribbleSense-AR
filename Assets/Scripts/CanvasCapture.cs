using UnityEngine;

public class CanvasCapture : MonoBehaviour
{
    public RenderTexture drawingTexture;

    public Texture2D CaptureDrawing()
    {
        RenderTexture.active = drawingTexture;
        Texture2D tex = new Texture2D(drawingTexture.width, drawingTexture.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, drawingTexture.width, drawingTexture.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        return tex;
    }
}
