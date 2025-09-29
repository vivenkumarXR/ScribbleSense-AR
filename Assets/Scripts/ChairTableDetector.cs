////// ChairTableDetector_InferenceEngine.cs
////// Works with Unity 6 / Inference Engine (com.unity.ai.inference).
////// If you use the sentis package instead, change `using Unity.InferenceEngine;` -> `using Unity.Sentis;`.

////using System;
////using System.Collections.Generic;
////using System.IO;
////using System.Linq;
////using Unity.Sentis; // If using Sentis older package: use Unity.Sentis
////using UnityEngine;
////using UnityEngine.UI;
////// NOTE: you need to add the Inference Engine (or Sentis) package via Package Manager.

////public class ChairTableDetector : MonoBehaviour
////{
////    [Header("Model Settings")]
////    public ModelAsset modelAsset;                       // assign (serialized .sentis) model in Inspector (preferred)
////    public string streamingSentisFilename = "";         // OR set filename (e.g. "yolov7_tiny.sentis") to load from StreamingAssets
////    public int inputSize = 320;                         // model input (320x320 typical for yolov7-tiny)

////    [Header("Detection Settings")]
////    public float confidenceThreshold = 0.25f;
////    public float nmsThreshold = 0.45f;

////    [Header("UI")]
////    public RawImage cameraDisplay;
////    public Text detectionText;

////    // COCO class indices (verify with your label map)
////    private readonly int CHAIR_CLASS = 56;
////    private readonly int TABLE_CLASS = 60;

////    // camera + textures
////    private WebCamTexture webCamTexture;
////    private RenderTexture rt;
////    private Texture2D inputTexture2D;

////    // inference
////    private Model runtimeModel;
////    private Worker worker;
////    private Tensor<float> inputTensor; // pre-allocated input tensor (reused)

////    void Start()
////    {
////        InitializeCamera();
////        InitializeModelAndWorker();
////    }

////    void InitializeCamera()
////    {
////        var devices = WebCamTexture.devices;
////        if (devices == null || devices.Length == 0)
////        {
////            Debug.LogError("No camera detected!");
////            return;
////        }

////        // start camera (use first device)
////        webCamTexture = new WebCamTexture(devices[0].name);
////        webCamTexture.Play();

////        // show live feed in UI if set
////        if (cameraDisplay != null)
////            cameraDisplay.texture = webCamTexture;

////        // allocate render target + texture for preprocessing
////        rt = new RenderTexture(inputSize, inputSize, 0, RenderTextureFormat.ARGB32);
////        inputTexture2D = new Texture2D(inputSize, inputSize, TextureFormat.RGBA32, false);
////    }

////    void InitializeModelAndWorker()
////    {
////        try
////        {
////            // Option A (recommended): use a serialized ModelAsset assigned in the inspector
////            if (modelAsset != null)
////            {
////                runtimeModel = ModelLoader.Load(modelAsset);
////            }
////            // Option B: load a .sentis file directly from StreamingAssets
////            else if (!string.IsNullOrEmpty(streamingSentisFilename))
////            {
////                string path = Path.Combine(Application.streamingAssetsPath, streamingSentisFilename);
////                runtimeModel = ModelLoader.Load(path);
////            }
////            else
////            {
////                Debug.LogError("No model assigned. Drag a .sentis ModelAsset to the component, or set streamingSentisFilename.");
////                return;
////            }

////            // create worker: try GPU first, fallback to CPU
////            try
////            {
////                worker = new Worker(runtimeModel, BackendType.GPUCompute);
////            }
////            catch (Exception gpuEx)
////            {
////                Debug.LogWarning("GPU worker creation failed, falling back to CPU: " + gpuEx.Message);
////                worker = new Worker(runtimeModel, BackendType.CPU);
////            }

////            // Pre-allocate input tensor (NCHW: 1 x 3 x H x W)
////            inputTensor = new Tensor<float>(new TensorShape(1, 3, inputSize, inputSize));
////        }
////        catch (Exception e)
////        {
////            Debug.LogError("Failed to initialize model/worker: " + e);
////        }
////    }

////    void Update()
////    {
////        if (webCamTexture == null || !webCamTexture.isPlaying) return;
////        if (worker == null || inputTensor == null) return;

////        // Process current frame (simple version: every frame) --
////        // for performance you might run inference every N frames.
////        RunInferenceOnFrame();
////    }

////    void RunInferenceOnFrame()
////    {
////        // 1) Copy webcam into a RenderTexture scaled to inputSize
////        Graphics.Blit(webCamTexture, rt);

////        // 2) Read render texture into Texture2D (simple & reliable; not the fastest)
////        var prev = RenderTexture.active;
////        RenderTexture.active = rt;
////        inputTexture2D.ReadPixels(new Rect(0, 0, inputSize, inputSize), 0, 0, false);
////        inputTexture2D.Apply();
////        RenderTexture.active = prev;

////        // 3) Convert Texture2D -> input Tensor<float> (re-uses pre-allocated tensor)
////        // TextureConverter handles resizing + NCHW layout for you.
////        TextureConverter.ToTensor(inputTexture2D, inputTensor);

////        // 4) Schedule inference on the worker (non-blocking GPU path possible)
////        worker.Schedule(inputTensor);

////        // 5) Read output: get peeked output tensor and read it back to CPU for processing
////        var outTensor = worker.PeekOutput() as Tensor<float>;
////        if (outTensor == null)
////        {
////            Debug.LogWarning("No output tensor from model.");
////            return;
////        }

////        // Blocking readback + clone (simple). For better perf use ReadbackAndCloneAsync.
////        using (var cpu = outTensor.ReadbackAndClone())
////        {
////            float[] flat = cpu.DownloadToArray();          // values in row-major flat array
////            int[] dims = cpu.shape.ToArray();              // e.g. [1, 25200, 85] for YOLO
////            var detections = ParseDetectionsFromFlat(flat, dims);
////            var final = ApplyNMS(detections);
////            DisplayDetections(final);
////        }
////    }

////    List<Detection> ParseDetectionsFromFlat(float[] flat, int[] dims)
////    {
////        // dims can vary depending on how the ONNX was exported;
////        // common YOLO format: [1, numDet, (5 + numClasses)]
////        int rank = dims.Length;
////        int batchDim, numDet, channels;
////        if (rank == 3)
////        {
////            batchDim = dims[0];
////            numDet = dims[1];
////            channels = dims[2];
////        }
////        else if (rank == 2) // fallback: [numDet, channels]
////        {
////            batchDim = 1;
////            numDet = dims[0];
////            channels = dims[1];
////        }
////        else
////        {
////            Debug.LogError("Unexpected output tensor rank: " + rank);
////            return new List<Detection>();
////        }

////        int numClasses = Mathf.Max(0, channels - 5);

////        // prepare strides for flattened indexing
////        int[] strides = ComputeStrides(dims);

////        var detections = new List<Detection>();

////        for (int i = 0; i < numDet; i++)
////        {
////            float objness = GetFlatValue(flat, dims, strides, 0, i, 4); // [b, i, 4]
////            if (objness < confidenceThreshold) continue;

////            // find class with max probability
////            float maxClassProb = 0f;
////            int maxClassId = -1;
////            for (int c = 0; c < numClasses; c++)
////            {
////                float classProb = GetFlatValue(flat, dims, strides, 0, i, 5 + c);
////                if (classProb > maxClassProb)
////                {
////                    maxClassProb = classProb;
////                    maxClassId = c;
////                }
////            }

////            if (maxClassId == -1) continue;

////            float finalConf = objness * maxClassProb;
////            if (finalConf < confidenceThreshold) continue;

////            // bbox (center x, center y, w, h) — YOLO typical output is normalized [0..1]
////            float centerX = GetFlatValue(flat, dims, strides, 0, i, 0);
////            float centerY = GetFlatValue(flat, dims, strides, 0, i, 1);
////            float w = GetFlatValue(flat, dims, strides, 0, i, 2);
////            float h = GetFlatValue(flat, dims, strides, 0, i, 3);

////            // convert to pixel coordinates of the input (inputSize)
////            float x1 = (centerX - w / 2f) * inputSize;
////            float y1 = (centerY - h / 2f) * inputSize;
////            float x2 = (centerX + w / 2f) * inputSize;
////            float y2 = (centerY + h / 2f) * inputSize;

////            // Only keep chair/table matches (COCO idx check)
////            if (maxClassId != CHAIR_CLASS && maxClassId != TABLE_CLASS) continue;

////            detections.Add(new Detection
////            {
////                x1 = x1,
////                y1 = y1,
////                x2 = x2,
////                y2 = y2,
////                confidence = finalConf,
////                classId = maxClassId,
////                className = (maxClassId == CHAIR_CLASS ? "chair" : "table")
////            });
////        }

////        return detections;
////    }

////    // compute row-major strides for dims array
////    int[] ComputeStrides(int[] dims)
////    {
////        int n = dims.Length;
////        var strides = new int[n];
////        strides[n - 1] = 1;
////        for (int k = n - 2; k >= 0; k--)
////            strides[k] = strides[k + 1] * dims[k + 1];
////        return strides;
////    }

////    // read a value from flat array using dims + strides and indices provided as (b, i, ch)
////    float GetFlatValue(float[] flat, int[] dims, int[] strides, int b, int i, int ch)
////    {
////        // Build index vector depending on rank (support rank==3 and rank==2)
////        if (dims.Length == 3)
////        {
////            int idx = b * strides[0] + i * strides[1] + ch * strides[2];
////            return flat[idx];
////        }
////        else if (dims.Length == 2)
////        {
////            // dims = [numDet, channels] -> interpret idx i,ch
////            int idx = i * strides[0] + ch * strides[1];
////            return flat[idx];
////        }
////        return 0f;
////    }

////    List<Detection> ApplyNMS(List<Detection> dets)
////    {
////        dets = dets.OrderByDescending(d => d.confidence).ToList();
////        var res = new List<Detection>();
////        for (int i = 0; i < dets.Count; i++)
////        {
////            bool keep = true;
////            for (int j = 0; j < res.Count; j++)
////            {
////                if (IoU(dets[i], res[j]) > nmsThreshold)
////                {
////                    keep = false;
////                    break;
////                }
////            }
////            if (keep) res.Add(dets[i]);
////        }
////        return res;
////    }

////    float IoU(Detection a, Detection b)
////    {
////        float interW = Mathf.Max(0, Mathf.Min(a.x2, b.x2) - Mathf.Max(a.x1, b.x1));
////        float interH = Mathf.Max(0, Mathf.Min(a.y2, b.y2) - Mathf.Max(a.y1, b.y1));
////        float inter = interW * interH;
////        float union = (a.x2 - a.x1) * (a.y2 - a.y1) + (b.x2 - b.x1) * (b.y2 - b.y1) - inter;
////        return union <= 0 ? 0f : inter / union;
////    }

////    void DisplayDetections(List<Detection> detections)
////    {
////        string result = $"Detected {detections.Count} objects:\n";
////        foreach (var d in detections)
////            result += $"{d.className}: {d.confidence:F2}\n";

////        if (detectionText != null) detectionText.text = result;
////        Debug.Log(result);
////    }

////    void OnDestroy()
////    {
////        try
////        {
////            worker?.Dispose();
////            inputTensor?.Dispose();
////            if (rt != null) rt.Release();
////            if (webCamTexture != null) { webCamTexture.Stop(); Destroy(webCamTexture); }
////        }
////        catch (Exception e)
////        {
////            Debug.LogWarning("Error disposing inference resources: " + e.Message);
////        }
////    }

////    [Serializable]
////    public class Detection
////    {
////        public float x1, y1, x2, y2;
////        public float confidence;
////        public int classId;
////        public string className;
////    }
////}
//using UnityEngine;
//using Unity.Sentis;
//using UnityEngine.UI;
//using System.Collections.Generic;
//using System.Linq;

//public class ChairTableDetector : MonoBehaviour
//{
//    [Header("Model Settings")]
//    public ModelAsset modelAsset;
//    public int inputSize = 320;

//    [Header("Detection Settings")]
//    public float confidenceThreshold = 0.25f;
//    public float nmsThreshold = 0.45f;

//    [Header("UI")]
//    public RawImage cameraDisplay;
//    public Text detectionText;

//    private WebCamTexture webCamTexture;
//    private InferenceSession worker;

//    private readonly int CHAIR_CLASS = 56;
//    private readonly int TABLE_CLASS = 60;

//    void Start()
//    {
//        InitializeCamera();
//        InitializeModel();
//    }

//    void InitializeCamera()
//    {
//        var devices = WebCamTexture.devices;
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
//        var runtimeModel = modelAsset.Load();
//        worker = new InferenceSession(runtimeModel);
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
//        using var input = TextureConverter.ToTensor(webCamTexture, channels: 3);
//        worker.Execute(input);
//        var output = worker.PeekOutput() as Tensor<float>;
//        var detections = ProcessDetections(output);
//        DisplayDetections(detections);
//        output.Dispose();
//    }

//    List<Detection> ProcessDetections(Tensor<float> output)
//    {
//        var detections = new List<Detection>();
//        int numDetections = output.shape[1];
//        int numClasses = output.shape[2] - 5;

//        for (int i = 0; i < numDetections; i++)
//        {
//            float confidence = output[0, i, 4];
//            if (confidence < confidenceThreshold) continue;

//            float maxClassProb = 0f;
//            int maxClassId = -1;
//            for (int c = 0; c < numClasses; c++)
//            {
//                float classProb = output[0, i, 5 + c];
//                if (classProb > maxClassProb)
//                {
//                    maxClassProb = classProb;
//                    maxClassId = c;
//                }
//            }

//            if (maxClassId != CHAIR_CLASS && maxClassId != TABLE_CLASS) continue;

//            float finalConfidence = confidence * maxClassProb;
//            if (finalConfidence < confidenceThreshold) continue;

//            float centerX = output[0, i, 0];
//            float centerY = output[0, i, 1];
//            float width = output[0, i, 2];
//            float height = output[0, i, 3];

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
//        float intersectionArea = Mathf.Max(0, Mathf.Min(a.x2, b.x2) - Mathf.Max(a.x1, b.x1)) *
//                                 Mathf.Max(0, Mathf.Min(a.y2, b.y2) - Mathf.Max(a.y1, b.y1));
//        float unionArea = (a.x2 - a.x1) * (a.y2 - a.y1) +
//                          (b.x2 - b.x1) * (b.y2 - b.y1) - intersectionArea;
//        return intersectionArea / unionArea;
//    }

//    void DisplayDetections(List<Detection> detections)
//    {
//        string result = $"Detected {detections.Count} objects:\n";
//        foreach (var detection in detections)
//            result += $"{detection.className}: {detection.confidence:F2}\n";

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

//    [System.Serializable]
//    public class Detection
//    {
//        public float x1, y1, x2, y2;
//        public float confidence;
//        public int classId;
//        public string className;
//    }
//}
//using System.Collections.Generic;
//using System.Linq;
//using Unity.Barracuda;
////using Unity.Sentis;
//using UnityEngine;
//using UnityEngine.UI;
//using TMPro;

//public class ChairTableDetector : MonoBehaviour
//{
//    [Header("Model Settings")]
//    public NNModel modelAsset;
//    public int inputSize = 320; // Model input size (320x320 for yolov7-tiny)

//    [Header("Detection Settings")]
//    public float confidenceThreshold = 0.25f;
//    public float nmsThreshold = 0.45f;

//    [Header("UI")]
//    public RawImage cameraDisplay;
//    public TextMeshProUGUI detectionText;

//    // Camera and model variables
//    private WebCamTexture webCamTexture;
//    private Model model;
//    private IWorker worker;

//    // COCO class indices for chair and table
//    private readonly int CHAIR_CLASS = 56;
//    private readonly int TABLE_CLASS = 60; // dining table in COCO
//    private readonly string[] targetClasses = { "chair", "dining table" };

//    void Start()
//    {
//        InitializeCamera();
//        InitializeModel();
//    }

//    void InitializeCamera()
//    {
//        // Get available cameras
//        WebCamDevice[] devices = WebCamTexture.devices;
//        if (devices.Length == 0)
//        {
//            Debug.LogError("No camera detected!");
//            return;
//        }

//        // Create and start camera
//        webCamTexture = new WebCamTexture(devices[0].name, inputSize, inputSize, 30);
//        cameraDisplay.texture = webCamTexture;
//        webCamTexture.Play();
//    }

//    void InitializeModel()
//    {
//        model = ModelLoader.Load(modelAsset);
//        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.ComputePrecompiled, model);
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
//        // Create input tensor from camera texture
//        using (var input = new Tensor(webCamTexture, channels: 3))
//        {
//            // Run inference
//            worker.Execute(input);

//            // Get output tensor (YOLOv7 typically has output shape [1, 25200, 85])
//            var output = worker.PeekOutput();

//            // Process detections
//            var detections = ProcessDetections(output);

//            // Display results
//            DisplayDetections(detections);

//            // Clean up
//            output.Dispose();
//        }
//    }

//    //List<Detection> ProcessDetections(Tensor output)
//    //{
//    //    var detections = new List<Detection>();
//    //    int numDetections = output.shape[1]; // Usually 25200 for YOLOv7
//    //    int numClasses = output.shape[2] - 5; // 85 - 5 = 80 classes for COCO

//    //    for (int i = 0; i < numDetections; i++)
//    //    {
//    //        // Extract detection data
//    //        float confidence = output[0, i, 4]; // Objectness score

//    //        if (confidence < confidenceThreshold) continue;

//    //        // Get class probabilities
//    //        float maxClassProb = 0f;
//    //        int maxClassId = -1;

//    //        for (int c = 0; c < numClasses; c++)
//    //        {
//    //            float classProb = output[0, i, 5 + c];
//    //            if (classProb > maxClassProb)
//    //            {
//    //                maxClassProb = classProb;
//    //                maxClassId = c;
//    //            }
//    //        }

//    //        // Only detect chairs and tables
//    //        if (maxClassId != CHAIR_CLASS && maxClassId != TABLE_CLASS) continue;

//    //        float finalConfidence = confidence * maxClassProb;
//    //        if (finalConfidence < confidenceThreshold) continue;

//    //        // Extract bounding box (center format)
//    //        float centerX = output[0, i, 0];
//    //        float centerY = output[0, i, 1];
//    //        float width = output[0, i, 2];
//    //        float height = output[0, i, 3];

//    //        // Convert to corner format
//    //        float x1 = (centerX - width / 2) * inputSize;
//    //        float y1 = (centerY - height / 2) * inputSize;
//    //        float x2 = (centerX + width / 2) * inputSize;
//    //        float y2 = (centerY + height / 2) * inputSize;

//    //        detections.Add(new Detection
//    //        {
//    //            x1 = x1,
//    //            y1 = y1,
//    //            x2 = x2,
//    //            y2 = y2,
//    //            confidence = finalConfidence,
//    //            classId = maxClassId,
//    //            className = maxClassId == CHAIR_CLASS ? "chair" : "table"
//    //        });
//    //    }

//    //    // Apply Non-Maximum Suppression
//    //    return ApplyNMS(detections);
//    //}

//    List<Detection> ProcessDetections(Tensor output)
//    {
//        var detections = new List<Detection>();

//        int numDetections = output.shape[1];         // e.g. 25200 (number of boxes)
//        int featureLen = output.shape[2];             // e.g. 85 (4 bbox + 1 objectness + 80 class scores)
//        int numClasses = featureLen - 5;              // 80 classes

//        // Get raw output data as 1D array
//        var data = output.ToReadOnlyArray();

//        for (int i = 0; i < numDetections; i++)
//        {
//            int baseIndex = i * featureLen;

//            // Objectness confidence
//            float confidence = data[baseIndex + 4];
//            if (confidence < confidenceThreshold)
//                continue;

//            // Find class with max probability
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

//            // Only care about chair and table classes
//            if (maxClassId != CHAIR_CLASS && maxClassId != TABLE_CLASS)
//                continue;

//            float finalConfidence = confidence * maxClassProb;
//            if (finalConfidence < confidenceThreshold)
//                continue;

//            // Extract bounding box (center x,y and width/height)
//            float centerX = data[baseIndex + 0];
//            float centerY = data[baseIndex + 1];
//            float width = data[baseIndex + 2];
//            float height = data[baseIndex + 3];

//            // Convert to corners
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

//        // Apply Non-Maximum Suppression (your existing implementation)
//        return ApplyNMS(detections);
//    }


//    List<Detection> ApplyNMS(List<Detection> detections)
//    {
//        // Simple NMS implementation
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
//        float intersectionArea = Mathf.Max(0, Mathf.Min(a.x2, b.x2) - Mathf.Max(a.x1, b.x1)) *
//                                Mathf.Max(0, Mathf.Min(a.y2, b.y2) - Mathf.Max(a.y1, b.y1));
//        float unionArea = (a.x2 - a.x1) * (a.y2 - a.y1) +
//                         (b.x2 - b.x1) * (b.y2 - b.y1) - intersectionArea;
//        return intersectionArea / unionArea;
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

//    [System.Serializable]
//    public class Detection
//    {
//        public float x1, y1, x2, y2;
//        public float confidence;
//        public int classId;
//        public string className;
//    }
//}
