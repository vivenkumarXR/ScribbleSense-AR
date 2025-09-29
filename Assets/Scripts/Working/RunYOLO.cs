using System;
using System.Collections.Generic;
using Unity.InferenceEngine;

using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/*
 *  YOLO Inference Script with WebCamTexture for Android
 *  ===================================================
 *
 * Place this script on the Main Camera and set the script parameters according to the tooltips.
 * Supports real-time furniture detection using Android mobile camera.
 */
public class RunYOLO : MonoBehaviour
{
    [Tooltip("Drag a YOLO model .onnx file here")]
    public ModelAsset modelAsset;
    [Tooltip("Drag the classes.txt here")]
    public TextAsset classesAsset;
    [Tooltip("Create a Raw Image in the scene and link it here")]
    public RawImage displayImage;
    [Tooltip("Drag a border box texture here")]
    public Texture2D borderTexture;
    [Tooltip("Select an appropriate font for the labels")]
    public Font font;

    const BackendType backend = BackendType.GPUCompute;
    private Transform displayLocation;
    private Worker worker;
    private string[] labels;
    private RenderTexture targetRT;
    private Sprite borderSprite;
    private WebCamTexture webcamTexture; // Added WebCamTexture

    //Image size for the model
    private const int imageWidth = 640;
    private const int imageHeight = 640;

    List<GameObject> boxPool = new();

    [Tooltip("Intersection over union threshold used for non-maximum suppression")]
    [SerializeField, Range(0, 1)]
    float iouThreshold = 0.5f;
    [Tooltip("Confidence score threshold used for non-maximum suppression")]
    [SerializeField, Range(0, 1)]
    float scoreThreshold = 0.5f;

    Tensor<float> centersToCorners;

    //bounding box data
    public struct BoundingBox
    {
        public float centerX;
        public float centerY;
        public float width;
        public float height;
        public string label;
    }

    void Start()
    {
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.Portrait;

        // Request camera permission for Android
        RequestCameraPermission();

        //Parse neural net labels
        labels = classesAsset.text.Split('\n');
        LoadModel();
        targetRT = new RenderTexture(imageWidth, imageHeight, 0);

        //Create image to display camera feed
        displayLocation = displayImage.transform;
        SetupInput();
        borderSprite = Sprite.Create(borderTexture, new Rect(0, 0, borderTexture.width, borderTexture.height), new Vector2(borderTexture.width / 2, borderTexture.height / 2));
    }

    void RequestCameraPermission()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Permission.RequestUserPermission(Permission.Camera);
        }
#elif UNITY_IOS || UNITY_WEBGL
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Application.RequestUserAuthorization(UserAuthorization.WebCam);
        }
#endif
    }

    void LoadModel()
    {
        //Load model
        var model1 = ModelLoader.Load(modelAsset);
        centersToCorners = new Tensor<float>(new TensorShape(4, 4),
        new float[]
        {
            1,      0,      1,      0,
            0,      1,      0,      1,
            -0.5f,  0,      0.5f,   0,
            0,      -0.5f,  0,      0.5f
        });

        //Here we transform the output of the model1 by feeding it through a Non-Max-Suppression layer.
        var graph = new FunctionalGraph();
        var inputs = graph.AddInputs(model1);
        var modelOutput = Functional.Forward(model1, inputs)[0];                        //shape=(1,84,8400)
        var boxCoords = modelOutput[0, 0..4, ..].Transpose(0, 1);               //shape=(8400,4)
        var allScores = modelOutput[0, 4.., ..];                                //shape=(80,8400)
        var scores = Functional.ReduceMax(allScores, 0);                                //shape=(8400)
        var classIDs = Functional.ArgMax(allScores, 0);                                 //shape=(8400)
        var boxCorners = Functional.MatMul(boxCoords, Functional.Constant(centersToCorners));   //shape=(8400,4)
        var indices = Functional.NMS(boxCorners, scores, iouThreshold, scoreThreshold); //shape=(N)
        var coords = Functional.IndexSelect(boxCoords, 0, indices);                     //shape=(N,4)
        var labelIDs = Functional.IndexSelect(classIDs, 0, indices);                    //shape=(N)

        //Create worker to run model
        worker = new Worker(graph.Compile(coords, labelIDs), backend);
    }

    [Obsolete]
    void SetupInput()
    {
        // Initialize WebCamTexture for mobile camera
        if (webcamTexture == null)
        {
            webcamTexture = new WebCamTexture();
            WebCamDevice[] devices = WebCamTexture.devices;

            Debug.Log($"--------------------------------Total cameras found: {devices.Length}");
            if (devices.Length == 0)
            {
                Debug.LogWarning("No cameras detected on this device!");
                return;
            }

            for (int i = 0; i < devices.Length; i++)
            {
                WebCamDevice device = devices[i];

                Debug.Log($"=== Camera {i} ===");
                Debug.Log($"Name: {device.name}");
                Debug.Log($"Is Front Facing: {device.isFrontFacing}");
                //Debug.Log($"Auto Focus Available: {device.autoFocusPointSupported}");
                Debug.Log($"Kind: {device.kind}");

                // Get supported resolutions
                Resolution[] resolutions = device.availableResolutions;
                if (resolutions != null && resolutions.Length > 0)
                {
                    Debug.Log($"Available Resolutions ({resolutions.Length}):");
                    foreach (Resolution res in resolutions)
                    {
                        Debug.Log($"  - {res.width}x{res.height} @ {res.refreshRate}Hz");
                    }
                }
                else
                {
                    Debug.Log("No resolution info available");
                }

                Debug.Log("==================");
            }
            // Optional: Specify camera parameters for better performance
            // webcamTexture = new WebCamTexture(null, 1280, 720, 30);

            webcamTexture.Play();
        }
    }

    private void Update()
    {
        // Check if camera permission is granted before executing ML
        bool hasPermission = false;

#if UNITY_ANDROID
        hasPermission = Permission.HasUserAuthorizedPermission(Permission.Camera);
#elif UNITY_IOS || UNITY_WEBGL
        hasPermission = Application.HasUserAuthorization(UserAuthorization.WebCam);
#else
        hasPermission = true; // For editor/standalone
#endif

        if (hasPermission && webcamTexture != null && webcamTexture.isPlaying)
        {
            ExecuteML();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
    }

    public void ExecuteML()
    {
        ClearAnnotations();

        // Check if webcam is ready and providing texture
        if (webcamTexture != null && webcamTexture.didUpdateThisFrame)
        {
            // Calculate aspect ratio from webcam
            float aspect = webcamTexture.width * 1f / webcamTexture.height;

            // Blit webcam texture to render texture
            Graphics.Blit(webcamTexture, targetRT, new Vector2(1f / aspect, 1), new Vector2(0, 0));
            displayImage.texture = targetRT;
        }
        else return;

        using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, imageHeight, imageWidth));
        TextureConverter.ToTensor(targetRT, inputTensor, default);
        worker.Schedule(inputTensor);

        using var output = (worker.PeekOutput("output_0") as Tensor<float>).ReadbackAndClone();
        using var labelIDs = (worker.PeekOutput("output_1") as Tensor<int>).ReadbackAndClone();

        float displayWidth = displayImage.rectTransform.rect.width;
        float displayHeight = displayImage.rectTransform.rect.height;
        float scaleX = displayWidth / imageWidth;
        float scaleY = displayHeight / imageHeight;

        int boxesFound = output.shape[0];

        //Draw the bounding boxes
        for (int n = 0; n < Mathf.Min(boxesFound, 200); n++)
        {
            var box = new BoundingBox
            {
                centerX = output[n, 0] * scaleX - displayWidth / 2,
                centerY = output[n, 1] * scaleY - displayHeight / 2,
                width = output[n, 2] * scaleX,
                height = output[n, 3] * scaleY,
                label = labels[labelIDs[n]],
            };
            DrawBox(box, n, displayHeight * 0.05f);
        }
    }

    public void DrawBox(BoundingBox box, int id, float fontSize)
    {
        //Create the bounding box graphic or get from pool
        GameObject panel;
        if (id < boxPool.Count)
        {
            panel = boxPool[id];
            panel.SetActive(true);
        }
        else
        {
            panel = CreateNewBox(Color.yellow);
        }

        //Set box position
        panel.transform.localPosition = new Vector3(box.centerX, -box.centerY);

        //Set box size
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(box.width, box.height);

        //Set label text
        var label = panel.GetComponentInChildren<Text>();
        label.text = box.label;
        label.fontSize = (int)fontSize;
    }

    public GameObject CreateNewBox(Color color)
    {
        //Create the box and set image
        var panel = new GameObject("ObjectBox");
        panel.AddComponent<CanvasRenderer>();
        Image img = panel.AddComponent<Image>();
        img.color = color;
        img.sprite = borderSprite;
        img.type = Image.Type.Sliced;
        panel.transform.SetParent(displayLocation, false);

        //Create the label
        var text = new GameObject("ObjectLabel");
        text.AddComponent<CanvasRenderer>();
        text.transform.SetParent(panel.transform, false);
        Text txt = text.AddComponent<Text>();
        txt.font = font;
        txt.color = color;
        txt.fontSize = 40;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        RectTransform rt2 = text.GetComponent<RectTransform>();
        rt2.offsetMin = new Vector2(20, rt2.offsetMin.y);
        rt2.offsetMax = new Vector2(0, rt2.offsetMax.y);
        rt2.offsetMin = new Vector2(rt2.offsetMin.x, 0);
        rt2.offsetMax = new Vector2(rt2.offsetMax.x, 30);
        rt2.anchorMin = new Vector2(0, 0);
        rt2.anchorMax = new Vector2(1, 1);

        boxPool.Add(panel);
        return panel;
    }

    public void ClearAnnotations()
    {
        foreach (var box in boxPool)
        {
            box.SetActive(false);
        }
    }

    void OnDestroy()
    {
        // Stop webcam and dispose resources
        if (webcamTexture != null)
        {
            webcamTexture.Stop();
            webcamTexture = null;
        }
        centersToCorners?.Dispose();
        worker?.Dispose();
    }
}
