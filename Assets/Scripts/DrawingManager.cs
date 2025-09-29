using UnityEngine;
using UnityEngine.EventSystems;

public class DrawingManager : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public Texture2D drawingTexture;
    public Color drawColor = Color.black;
    public int brushSize = 8;
    private RectTransform rectTransform;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();

        // Create blank white texture
        drawingTexture = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
        ClearTexture(Color.white);
        GetComponent<UnityEngine.UI.RawImage>().texture = drawingTexture;
    }

    public void OnPointerDown(PointerEventData eventData) => Draw(eventData);
    public void OnDrag(PointerEventData eventData) => Draw(eventData);

    void Draw(PointerEventData eventData)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, null, out Vector2 localPoint))
        {
            int x = Mathf.RoundToInt((localPoint.x + rectTransform.rect.width / 2) * (drawingTexture.width / rectTransform.rect.width));
            int y = Mathf.RoundToInt((localPoint.y + rectTransform.rect.height / 2) * (drawingTexture.height / rectTransform.rect.height));

            for (int i = -brushSize; i <= brushSize; i++)
            {
                for (int j = -brushSize; j <= brushSize; j++)
                {
                    int px = Mathf.Clamp(x + i, 0, drawingTexture.width - 1);
                    int py = Mathf.Clamp(y + j, 0, drawingTexture.height - 1);
                    drawingTexture.SetPixel(px, py, drawColor);
                }
            }
            drawingTexture.Apply();
        }
    }

    public void ClearTexture(Color color)
    {
        Color[] fillColor = new Color[drawingTexture.width * drawingTexture.height];
        for (int i = 0; i < fillColor.Length; i++) fillColor[i] = color;
        drawingTexture.SetPixels(fillColor);
        drawingTexture.Apply();
    }
}
