//using UnityEngine;
//using System.Collections;

//public class ScribbleToAR : MonoBehaviour
//{
//    public CanvasCapture canvasCapture;
//    public OCRHandler ocrHandler;
//    public ARTextSpawner arTextSpawner;
//    public GameObject drawingCanvas;
//    public string storedText;

//    public void OnDoneButtonPressed()
//    {
//        Texture2D tex = canvasCapture.CaptureDrawing();
//        StartCoroutine(ocrHandler.SendToOCR(tex, (resultText) =>
//        {
//            storedText = resultText;
//            Debug.Log("OCR Result: " + storedText);
//            drawingCanvas.SetActive(false);
//        }));
//    }

//    void Update()
//    {
//        if (Input.touchCount == 1)
//        {
//            Touch t = Input.GetTouch(0);
//            if (t.phase == TouchPhase.Ended && !string.IsNullOrEmpty(storedText))
//            {
//                arTextSpawner.SpawnText(t.position, storedText);
//            }
//        }
//    }
//}
