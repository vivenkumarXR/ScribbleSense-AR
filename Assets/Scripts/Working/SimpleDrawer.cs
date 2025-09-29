using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SimpleDrawer : MonoBehaviour, IDragHandler, IPointerDownHandler
{
    public RawImage rawImage;
    public int penSize = 10;
    public Texture2D texture;
    private Color penColor = Color.black;
    private Vector2 lastPoint;

    void Start()
    {
        // Create a white texture to draw on
        texture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                texture.SetPixel(x, y, Color.white);
            }
        }
        texture.Apply();
        rawImage.texture = texture;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        lastPoint = GetLocalPoint(eventData);
        Draw(lastPoint);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 point = GetLocalPoint(eventData);
        Draw(point);
        lastPoint = point;
    }

    private Vector2 GetLocalPoint(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rawImage.rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        // Convert to texture coordinates (0–width, 0–height)
        Rect rect = rawImage.rectTransform.rect;
        float x = (localPoint.x - rect.x) * texture.width / rect.width;
        float y = (localPoint.y - rect.y) * texture.height / rect.height;
        return new Vector2(x, y);
    }

    private void Draw(Vector2 point)
    {
        int x = Mathf.RoundToInt(point.x);
        int y = Mathf.RoundToInt(point.y);

        for (int i = -penSize; i <= penSize; i++)
        {
            for (int j = -penSize; j <= penSize; j++)
            {
                if (i * i + j * j <= penSize * penSize) // circular brush
                {
                    int px = x + i;
                    int py = y + j;
                    if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                    {
                        texture.SetPixel(px, py, penColor);
                    }
                }
            }
        }
        texture.Apply();
    }
}
