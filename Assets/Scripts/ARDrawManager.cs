using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;

[System.Serializable]
public class UnityEventVector3 : UnityEvent<Vector3> { }

public class ARDrawManager : MonoBehaviour
{
    [Header("Drawing Settings")]
    [SerializeField] private float distanceFromCamera = 0.3f;
    [SerializeField] private Material defaultLineMaterial;
    [SerializeField] private float lineWidth = 0.02f;
    [SerializeField] private int cornerVertices = 5;
    [SerializeField] private int endCapVertices = 5;
    [SerializeField] private bool allowSimplification = false;
    [SerializeField] private float tolerance = 0.1f;
    [SerializeField] private float minDistanceBeforeNewPoint = 0.02f;

    [Header("Colors")]
    [SerializeField] private bool randomizeColor = false;
    [SerializeField] private Color defaultStartColor = Color.white;
    [SerializeField] private Color defaultEndColor = Color.white;

    [Header("Events")]
    [SerializeField] private UnityEvent OnDraw;

    [Header("AR Components")]
    [SerializeField] private ARAnchorManager anchorManager;
    [SerializeField] private Camera arCamera;

    // Private variables
    private Color randomStartColor;
    private Color randomEndColor;
    private LineRenderer prevLineRenderer;
    private List<GameObject> anchorObjects = new List<GameObject>();
    private List<LineRenderer> lines = new List<LineRenderer>();
    private int positionCount = 0;
    private Vector3 prevPointDistance = Vector3.zero;

    public bool CanDraw { get; set; } = true;

    void Start()
    {
        // Initialize random colors if needed
        if (randomizeColor)
        {
            randomStartColor = new Color(Random.value, Random.value, Random.value, 1f);
            randomEndColor = new Color(Random.value, Random.value, Random.value, 1f);
        }
        else
        {
            randomStartColor = defaultStartColor;
            randomEndColor = defaultEndColor;
        }
    }

    void Update()
    {
        // Handle touch input
        if (Input.touchCount > 0)
        {
            DrawOnTouch();
        }

        // Handle mouse input for testing in editor
        if (Input.GetMouseButton(0))
        {
            DrawOnMouse();
        }
        else if (Input.GetMouseButtonUp(0))
        {
            prevLineRenderer = null;
        }
    }

    public void AllowDraw(bool isAllow)
    {
        CanDraw = isAllow;
    }

    private void SetLineSettings(LineRenderer currentLineRenderer)
    {
        currentLineRenderer.startWidth = lineWidth;
        currentLineRenderer.endWidth = lineWidth;
        currentLineRenderer.numCornerVertices = cornerVertices;
        currentLineRenderer.numCapVertices = endCapVertices;
        currentLineRenderer.material = defaultLineMaterial;
        currentLineRenderer.useWorldSpace = true;

        if (allowSimplification)
            currentLineRenderer.Simplify(tolerance);

        currentLineRenderer.startColor = randomStartColor;
        currentLineRenderer.endColor = randomEndColor;
    }

    async void DrawOnTouch()
    {
        if (!CanDraw) return;

        Touch touch = Input.GetTouch(0);
        Vector3 touchPosition = arCamera.ScreenToWorldPoint(
            new Vector3(touch.position.x, touch.position.y, distanceFromCamera));

        if (touch.phase == TouchPhase.Began)
        {
            OnDraw?.Invoke();
            await CreateAnchorAtPosition(touchPosition);
        }

        if (touch.phase == TouchPhase.Moved)
        {
            UpdateLine(touchPosition);
        }

        if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
        {
            prevLineRenderer = null;
        }
    }

    void DrawOnMouse()
    {
        if (!CanDraw) return;

        Vector3 mousePosition = arCamera.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x, Input.mousePosition.y, distanceFromCamera));

        if (Input.GetMouseButtonDown(0))
        {
            OnDraw?.Invoke();
            CreateAnchorAtPosition(mousePosition);
        }

        UpdateLine(mousePosition);
    }

    // Modern approach: Create anchor using AddComponent<ARAnchor>()
    private async System.Threading.Tasks.Task CreateAnchorAtPosition(Vector3 position)
    {
        // Create anchor GameObject
        GameObject anchorObject = new GameObject("ARDrawAnchor");
        anchorObject.transform.position = position;
        anchorObject.transform.rotation = Quaternion.identity;

        // Add ARAnchor component (modern way)
        ARAnchor anchor = anchorObject.AddComponent<ARAnchor>();

        // For AR Foundation 6.0+, you can also use TryAddAnchorAsync (more robust)
        // Uncomment the following lines if using AR Foundation 6.0+:
        /*
        var pose = new Pose(position, Quaternion.identity);
        var result = await anchorManager.TryAddAnchorAsync(pose);
        if (result.status.IsSuccess())
        {
            anchorObject = result.value.gameObject;
        }
        else
        {
            Debug.LogError($"Failed to create anchor: {result.status}");
            Destroy(anchorObject);
            return;
        }
        */

        anchorObjects.Add(anchorObject);
        AddLineRenderer(anchorObject.transform);
    }

    void UpdateLine(Vector3 newPoint)
    {
        if (prevLineRenderer == null) return;

        if (Vector3.Distance(prevPointDistance, newPoint) > minDistanceBeforeNewPoint)
        {
            AddPoint(newPoint);
            prevPointDistance = newPoint;
        }
    }

    void AddPoint(Vector3 newPoint)
    {
        prevLineRenderer.positionCount = ++positionCount;
        prevLineRenderer.SetPosition(positionCount - 1, newPoint);

        if (allowSimplification && positionCount % 10 == 0)
        {
            prevLineRenderer.Simplify(tolerance);
        }
    }

    void AddLineRenderer(Transform parent)
    {
        // Randomize colors for new line if enabled
        if (randomizeColor)
        {
            randomStartColor = new Color(Random.value, Random.value, Random.value, 1f);
            randomEndColor = new Color(Random.value, Random.value, Random.value, 1f);
        }

        // Create new GameObject for the line
        GameObject lineObject = new GameObject("ARLine");
        lineObject.transform.SetParent(parent);
        lineObject.transform.localPosition = Vector3.zero;

        // Add and configure LineRenderer component
        prevLineRenderer = lineObject.AddComponent<LineRenderer>();
        SetLineSettings(prevLineRenderer);

        // Store reference
        lines.Add(prevLineRenderer);

        // Reset position count for new line
        positionCount = 0;
    }

    public void ClearAllLines()
    {
        // Remove all line renderers
        foreach (LineRenderer line in lines)
        {
            if (line != null)
                DestroyImmediate(line.gameObject);
        }
        lines.Clear();

        // Remove all anchor GameObjects (modern way - Destroy the GameObject)
        foreach (GameObject anchorObject in anchorObjects)
        {
            if (anchorObject != null)
                Destroy(anchorObject); // This automatically removes the ARAnchor component
        }
        anchorObjects.Clear();

        prevLineRenderer = null;
        positionCount = 0;
    }

    void OnDestroy()
    {
        ClearAllLines();
    }
}
