//using System.Collections.Generic;
//using System.Linq;
//using TMPro;
//using Unity.Sentis;
//using UnityEngine;
//using UnityEngine.LightTransport;
//using UnityEngine.UI;

//public class ChairTableDetector_Sentis : MonoBehaviour
//{
//    [Header("Model Settings")]
//    public string modelPath = "Assets/Models/YOLOv7-Tiny.onnx"; // Sentis loads .onnx directly
//    public int inputSize = 320; // Model input size (YOLOv7-tiny = 320x320)

//    [Header("Detection Settings")]
//    public float confidenceThreshold = 0.25f;
//    public float nmsThreshold = 0.45f;

//    [Header("UI")]
//    public RawImage cameraDisplay;
//    public TextMeshProUGUI detectionText;

//    // Camera and model variables
//    private WebCamTexture webCamTexture;
//    private Model model;
//    private InferenceContext worker;

//    // COCO class indices for chair and table
//    private readonly int CHAIR_CLASS = 56;
//    private readonly int TABLE_CLASS = 60; // dining table in COCO

//    void Start()
//    {
//        InitializeCamera();
//        InitializeModel();
//    }

//    void InitializeCamera()
//    {
//        WebCamDevice[] devices = WebCamTexture.devices;
//        if (devices.Length == 0)
//        {
//            Debug.LogError("No camera detected!");
//            return;
//        }

//        webCamTexture = new WebCamTexture(devices[0].name, inputSize, inputSize, 30);
//        cameraDisplay.texture = webCamTexture;
//        webCamTexture.Play();
//    }

//    void InitializeModel()
//    {
//        model = ModelLoader.Load(modelPath);
//        worker = WorkerFactory.CreateWorker(BackendType.GPUCompute, model);
//    }

//    void Update()
//    {
//        if (webCamTexture != null && webCamTexture.isPlaying &&
//            webCamTexture.width > 0 && webCamTexture.height > 0)
//        {
//            DetectObjects();
//        }
//    }

//    void DetectObjects()
//    {
//        // Convert camera frame into Texture2D
//        Texture2D tex = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
//        tex.SetPixels(webCamTexture.GetPixels());
//        tex.Apply();

//        // Resize to match model input
//        Texture2D resizedTex = ResizeTexture(tex, inputSize, inputSize);

//        using (var inputTensor = TextureConverter.ToTensor(resizedTex, channels: 3))
//        {
//            // Run inference
//            worker.Execute(inputTensor);

//            // Get model output (YOLOv7 → float)
//            var output = worker.PeekOutput() as TensorFloat;

//            if (output == null)
//            {
//                Debug.LogError("Model output is not TensorFloat!");
//            }
//            else
//            {
//                var detections = ProcessDetections(output);
//                DisplayDetections(detections);
//                output.Dispose();
//            }
//        }

//        // Cleanup textures
//        Destroy(tex);
//        Destroy(resizedTex);
//    }

//    List<Detection> ProcessDetections(TensorFloat output)
//    {
//        var detections = new List<Detection>();

//        int numDetections = output.shape[1]; // usually 25200
//        int featureLen = output.shape[2];    // usually 85
//        int numClasses = featureLen - 5;

//        var data = output.ToReadOnlyArray();

//        for (int i = 0; i < numDetections; i++)
//        {
//            int baseIndex = i * featureLen;

//            float confidence = data[baseIndex + 4];
//            if (confidence < confidenceThreshold) continue;

//            float maxClassProb = 0f;
//            int maxClassId = -1;

//            for (int c = 0; c < numClasses; c++)
//            {
//                float classProb = data[baseIndex + 5 + c];
//                if (classProb > maxClassProb)
//                {
//                    maxClassProb = classProb;
//                    maxClassId = c;
//                }
//            }

//            if (maxClassId != CHAIR_CLASS && maxClassId != TABLE_CLASS) continue;

//            float finalConfidence = confidence * maxClassProb;
//            if (finalConfidence < confidenceThreshold) continue;

//            float centerX = data[baseIndex + 0];
//            float centerY = data[baseIndex + 1];
//            float width = data[baseIndex + 2];
//            float height = data[baseIndex + 3];

//            float x1 = (centerX - width / 2) * inputSize;
//            float y1 = (centerY - height / 2) * inputSize;
//            float x2 = (centerX + width / 2) * inputSize;
//            float y2 = (centerY + height / 2) * inputSize;

//            detections.Add(new Detection
//            {
//                x1 = x1,
//                y1 = y1,
//                x2 = x2,
//                y2 = y2,
//                confidence = finalConfidence,
//                classId = maxClassId,
//                className = maxClassId == CHAIR_CLASS ? "chair" : "table"
//            });
//        }

//        return ApplyNMS(detections);
//    }

//    List<Detection> ApplyNMS(List<Detection> detections)
//    {
//        detections = detections.OrderByDescending(d => d.confidence).ToList();
//        var result = new List<Detection>();

//        for (int i = 0; i < detections.Count; i++)
//        {
//            bool keep = true;
//            for (int j = 0; j < result.Count; j++)
//            {
//                if (IoU(detections[i], result[j]) > nmsThreshold)
//                {
//                    keep = false;
//                    break;
//                }
//            }
//            if (keep) result.Add(detections[i]);
//        }

//        return result;
//    }

//    float IoU(Detection a, Detection b)
//    {
//        float interWidth = Mathf.Max(0, Mathf.Min(a.x2, b.x2) - Mathf.Max(a.x1, b.x1));
//        float interHeight = Mathf.Max(0, Mathf.Min(a.y2, b.y2) - Mathf.Max(a.y1, b.y1));
//        float intersection = interWidth * interHeight;

//        float areaA = (a.x2 - a.x1) * (a.y2 - a.y1);
//        float areaB = (b.x2 - b.x1) * (b.y2 - b.y1);

//        return intersection / (areaA + areaB - intersection);
//    }

//    void DisplayDetections(List<Detection> detections)
//    {
//        string result = $"Detected {detections.Count} objects:\n";
//        foreach (var detection in detections)
//        {
//            result += $"{detection.className}: {detection.confidence:F2}\n";
//        }

//        if (detectionText != null)
//            detectionText.text = result;

//        Debug.Log(result);
//    }

//    void OnDestroy()
//    {
//        worker?.Dispose();
//        if (webCamTexture != null)
//        {
//            webCamTexture.Stop();
//            Destroy(webCamTexture);
//        }
//    }

//    Texture2D ResizeTexture(Texture2D src, int width, int height)
//    {
//        RenderTexture rt = RenderTexture.GetTemporary(width, height);
//        Graphics.Blit(src, rt);
//        RenderTexture.active = rt;

//        Texture2D result = new Texture2D(width, height, TextureFormat.RGB24, false);
//        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
//        result.Apply();

//        RenderTexture.active = null;
//        RenderTexture.ReleaseTemporary(rt);
//        return result;
//    }

//    [System.Serializable]
//    public class Detection
//    {
//        public float x1, y1, x2, y2;
//        public float confidence;
//        public int classId;
//        public string className;
//    }
//}
