using UnityEngine;

public class CaptureAndSend : MonoBehaviour
{
    public SimpleDrawer drawer;       // assign RawImage with SimpleDrawer
    //public OCRHandler ocrHandler;     // your OCR script
    public GeminiOCRHandler handler;
    public GameObject drawingCanvas;  // the full Canvas
    public string storedText;
    public bool buttonPressed = false;

    public void OnDoneButtonPressed()
    {
        Texture2D tex = drawer.texture;
        Texture2D cropped = TrimWhite(tex);
        byte[] imageBytes = cropped.EncodeToPNG();

        // Send to OCR
        StartCoroutine(handler.SendToGeminiOCR(imageBytes, (resultText) =>
        {
            storedText = resultText;
            Debug.Log("--------------OCR Result: " + storedText);
            buttonPressed = true;
            // Hide canvas so AR content is visible
            drawingCanvas.SetActive(false);
        }));
    }
    public static Texture2D TrimWhite(Texture2D source)
    {
        int xMin = source.width, xMax = 0, yMin = source.height, yMax = 0;

        Color32[] pixels = source.GetPixels32();
        for (int y = 0; y < source.height; y++)
        {
            for (int x = 0; x < source.width; x++)
            {
                Color32 c = pixels[y * source.width + x];
                if (c.a > 0.1f && c.r < 0.95f && c.g < 0.95f && c.b < 0.95f) // not white
                {
                    if (x < xMin) xMin = x;
                    if (x > xMax) xMax = x;
                    if (y < yMin) yMin = y;
                    if (y > yMax) yMax = y;
                }
            }
        }

        int w = xMax - xMin + 1;
        int h = yMax - yMin + 1;
        Texture2D trimmed = new Texture2D(w, h, TextureFormat.RGBA32, false);
        trimmed.SetPixels(source.GetPixels(xMin, yMin, w, h));
        trimmed.Apply();
        return trimmed;
    }

}
