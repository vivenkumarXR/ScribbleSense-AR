using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;

[RequireComponent(typeof(ARAnchorManager))]
public class DrawManager : MonoBehaviour
{
    [SerializeField] private float distanceFromCamera = 0.3f;
    [SerializeField] private Material defaultColorMaterial;
    [SerializeField] private int cornerVertices = 5;
    [SerializeField] private int endCapVertices = 5;

    [Header("Tolerance Options")]
    [SerializeField] private bool allowSimplification = false;
    [SerializeField] private float tolerance = 0.001f;
    [SerializeField] private int applySimplifyAfterPoints = 20; // changed to int
    [SerializeField, Range(0, 1f)] private float minDistanceBeforeNewPoint = 0.01f;

    [SerializeField] private UnityEvent OnDraw;
    [SerializeField] private ARAnchorManager anchorManager; // will auto-get in Awake if null
    [SerializeField] private Camera arCamera;

    [SerializeField] private Color defaultColor = Color.white;
    private Color randomStartColor = Color.white;
    private Color randomEndColor = Color.white;

    [SerializeField] private float lineWidth = 0.05f;

    private LineRenderer prevLineRender;
    private LineRenderer currentLineRenderer;
    private List<ARAnchor> anchors = new List<ARAnchor>();
    private List<LineRenderer> lines = new List<LineRenderer>();

    private int positionCount = 0;
    private Vector3 prevPointDistance = Vector3.zero;
    private bool CanDraw { get; set; }

    void Awake()
    {
        // Ensure references are set
        if (anchorManager == null) anchorManager = GetComponent<ARAnchorManager>();
        if (arCamera == null) arCamera = Camera.main;
    }

    void Update()
    {
        if (Input.touchCount > 0) DrawOnTouch();
        if (Input.GetMouseButton(0)) DrawOnMouse();
        else prevLineRender = null;
    }

    public void AllowDraw(bool isAllow) => CanDraw = isAllow;

    private void SetLineSettings(LineRenderer lr)
    {
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.numCornerVertices = cornerVertices;
        lr.numCapVertices = endCapVertices;
        lr.startColor = randomStartColor;
        lr.endColor = randomEndColor;
        if (allowSimplification) lr.Simplify(tolerance);
    }

    void DrawOnTouch()
    {
        if (!CanDraw) return;

        Touch touch = Input.GetTouch(0);
        Vector3 touchPosition = arCamera.ScreenToWorldPoint(new Vector3(touch.position.x, touch.position.y, distanceFromCamera));

        if (touch.phase == TouchPhase.Began)
        {
            OnDraw?.Invoke();

            ARAnchor anchor = TryCreateAnchor(touchPosition);
            if (anchor != null) anchors.Add(anchor);

            AddLineRenderer(anchor, touchPosition);
        }
        else
        {
            UpdateLine(touchPosition);
            Debug.LogError("-----------------------------------------UpdateLine");

        }
    }

    void DrawOnMouse()
    {
        if (!CanDraw) return;

        Vector3 mousePosition = arCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, distanceFromCamera));
        if (Input.GetMouseButton(0))
        {
            Debug.LogError("-----------------------------------------Input.GetMouseButton");

            OnDraw?.Invoke();

            if (prevLineRender == null) AddLineRenderer(null, mousePosition);
            else UpdateLine(mousePosition);
        }
    }

    ARAnchor TryCreateAnchor(Vector3 posePosition)
    {
        // Make sure AR Session is available/running in your app before relying on anchors.
        // Try using anchorManager.AddAnchor (preferred) and fall back to creating a GameObject + ARAnchor.
        //ARAnchor anchor = null;

        //if (anchorManager != null)
        //{
        //    try
        //    {
        //        // Preferred: ask the manager to create an anchor (may return null if subsystem not ready)
        //        anchor = anchorManager.AddAnchor(new Pose(posePosition, Quaternion.identity));
        //    }
        //    catch (System.MissingMethodException)
        //    {
        //        // In case AddAnchor is not present in your ARFoundation version, we'll fall back below.
        //        anchor = null;
        //    }
        //    catch (System.Exception ex)
        //    {
        //        Debug.LogWarning($"anchorManager.AddAnchor threw: {ex.Message}");
        //        anchor = null;
        //    }
        //}
        //else
        //{
        //    Debug.LogWarning("anchorManager is null. Make sure ARAnchorManager is on the same GameObject or assigned in Inspector.");
        //}

        //// Fallback: create GameObject and add ARAnchor component directly (works across versions)
        //if (anchor == null)
        //{
        //    GameObject anchorGO = new GameObject("RuntimeAnchor");
        //    anchorGO.transform.position = posePosition;
        //    anchor = anchorGO.AddComponent<ARAnchor>();
        //    if (anchor == null)
        //    {
        //        Debug.LogError("Failed to create fallback ARAnchor. AR subsystem may not be running or supported.");
        //    }
        //    else
        //    {
        //        // Optionally parent the anchor to the anchorManager/GameObject for organization
        //        if (anchorManager != null) anchorGO.transform.parent = anchorManager.transform;
        //    }
        //}

        //return anchor;
        // Create a GameObject to hold the anchor
        GameObject anchorGO = new GameObject("RuntimeAnchor");
        anchorGO.transform.position = posePosition;
        anchorGO.transform.rotation = Quaternion.identity;

        // Parent it under the anchorManager for organization
        if (anchorManager != null)
        {
            anchorGO.transform.parent = anchorManager.transform;
        }

        // Add the ARAnchor component directly
        ARAnchor anchor = anchorGO.AddComponent<ARAnchor>();

        if (anchor == null)
        {
            Debug.LogError("-----------------------------------------Failed to create ARAnchor.");
        }

        return anchor;
    }

    void UpdateLine(Vector3 touchPosition)
    {
        if (prevLineRender == null) prevPointDistance = touchPosition;

        // use && (logical AND) and compare distance properly
        if (Vector3.Distance(prevPointDistance, touchPosition) >= minDistanceBeforeNewPoint)
        {
            prevPointDistance = touchPosition;
            AddPoint(prevPointDistance);
        }
    }

    void AddPoint(Vector3 position)
    {
        positionCount++;
        currentLineRenderer.positionCount = positionCount;
        currentLineRenderer.SetPosition(positionCount - 1, position);
        Debug.LogError("-----------------------------------------AddPoint");

        if (currentLineRenderer.positionCount % applySimplifyAfterPoints == 0 && allowSimplification)
            currentLineRenderer.Simplify(tolerance);
    }

    void AddLineRenderer(ARAnchor arAnchor, Vector3 touchPosition)
    {
        positionCount = 2;
        GameObject go = new GameObject($"LineRenderer_{lines.Count}");
        go.transform.parent = arAnchor?.transform ?? transform;
        go.transform.position = touchPosition;
        go.tag = "Line";
        Debug.LogError("-----------------------------------------AddLineRenderer");

        LineRenderer goLineRenderer = go.AddComponent<LineRenderer>();
        goLineRenderer.startWidth = lineWidth;
        goLineRenderer.endWidth = lineWidth;
        goLineRenderer.material = defaultColorMaterial;
        goLineRenderer.useWorldSpace = true;
        goLineRenderer.positionCount = positionCount;
        goLineRenderer.numCapVertices = 90;
        goLineRenderer.SetPosition(0, touchPosition);
        goLineRenderer.SetPosition(1, touchPosition);

        SetLineSettings(goLineRenderer);

        currentLineRenderer = goLineRenderer;
        prevLineRender = currentLineRenderer;
        lines.Add(goLineRenderer);
    }
}
