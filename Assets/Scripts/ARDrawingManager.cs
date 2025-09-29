// ARDrawingManager.cs
// Attach this to an empty GameObject in the scene (e.g., "ARDrawingManager").
// Requires AR Foundation packages (ARFoundation, ARSubsystems, ARCore XR Plugin or ARKit XR Plugin).
// Scene setup:
// - AR Session Origin with ARRaycastManager, ARAnchorManager, ARPlaneManager components.
// - AR Camera as child of AR Session Origin.
// - LineRenderer prefab: a GameObject with LineRenderer component, configured how you like.
// - Optional SpherePrefab: small sphere to instantiate along stroke if "3D mode" is enabled.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;
using TMPro;
public class ARDrawingManager : MonoBehaviour
{
    [Header("AR Refs")]
    public ARRaycastManager raycastManager;
    public ARAnchorManager anchorManager;
    public ARPlaneManager planeManager;

    [Header("Prefabs")]
    public GameObject lineRendererPrefab; // Prefab that contains a LineRenderer component.
    public GameObject spherePrefab; // optional 3D stamp prefab.

    [Header("UI")]
    public GameObject scanPromptPanel; // Panel that asks the user to scan the environment
    public TextMeshProUGUI promptText; // "Tap to start" / "Start drawing" / etc.
    public Button doneButton; // Press when finished drawing
    public Toggle mode3DToggle; // If on, instantiate spheres instead of LineRenderer (or both)

    [Header("Stroke Settings")]
    public float minDistanceBetweenPoints = 0.01f; // reduce vertices
    public float sphereSpacing = 0.03f; // when using spheres

    // runtime
    bool isScanning = true;
    bool isDrawing = false;

    GameObject currentStrokeGO;
    LineRenderer currentLine;
    List<Vector3> currentPoints = new List<Vector3>();
    ARAnchor currentAnchor;

    // store anchors so they persist during session
    List<ARAnchor> anchors = new List<ARAnchor>();

    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

    void Awake()
    {
        if (doneButton != null) doneButton.onClick.AddListener(FinishDrawing);
        UpdatePrompt();
    }

    void Update()
    {
        // simple scanning state - if planes found we allow drawing
        if (isScanning)
        {
            if (planeManager.trackables.count > 0)
            {
                isScanning = false;
                //scanPromptPanel?.SetActive(false);
                UpdatePrompt();
            }
        }

        // Touch handling for drawing
        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);

        // only consider touches that began or moved
        if (touch.phase == TouchPhase.Began)
        {
            // raycast against planes/meshes
            if (raycastManager.Raycast(touch.position, s_Hits, TrackableType.PlaneWithinPolygon | TrackableType.FeaturePoint))
            {
                Debug.Log("Raycast HIT at: " + s_Hits[0].pose.position);
                Pose hitPose = s_Hits[0].pose;

                // create an anchor at the hit position
                ARAnchor anchor = CreateAnchorAt(hitPose);
                if (anchor == null)
                {
                    Debug.LogWarning("Failed to create anchor");
                    return;
                }

                StartNewStroke(anchor, hitPose.position);
            }
            else
            {
                Debug.Log("Raycast MISS");
            }
        }
        else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
        {
            if (isDrawing)
            {
                // project current touch to AR world
                if (raycastManager.Raycast(touch.position, s_Hits, TrackableType.PlaneWithinPolygon | TrackableType.FeaturePoint))
                {
                    Vector3 worldPos = s_Hits[0].pose.position;
                    AddPointToStroke(worldPos);
                }
            }
        }
        else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
        {
            // do nothing special on touch end; user presses Done when finished
        }
    }

    ARAnchor CreateAnchorAt(Pose pose)
    {
        // Try to attach to existing plane (if hit gives plane id), or create a world anchor at pose.
        ARAnchor anchor = null;

        // If hit trackable id present in s_Hits[0]
        var hit = s_Hits[0];
        //if (hit.trackableId != TrackableId.invalidId)
        //{
        //    var plane = planeManager.GetPlane(hit.trackableId);
        //    if (plane != null)
        //    {
        //        anchor = anchorManager.AttachAnchor(plane, pose);
        //    }
        //}

        //if (anchor == null)
        //{
        //    GameObject anchorGO = new GameObject("ARAnchor");
        //    anchorGO.transform.position = pose.position;
        //    anchorGO.transform.rotation = pose.rotation;
        //    anchor = anchorGO.AddComponent<ARAnchor>();
        //}

        if (hit.trackableId != TrackableId.invalidId)
        {
            var plane = planeManager.GetPlane(hit.trackableId);
            if (plane != null)
            {
                anchor = anchorManager.AttachAnchor(plane, pose);
            }
        }
        if (anchor == null)
        {
            GameObject anchorGO = new GameObject("ARAnchor");
            anchorGO.transform.position = pose.position;
            anchorGO.transform.rotation = pose.rotation;
            anchor = anchorGO.AddComponent<ARAnchor>();
        }

        if (anchor != null) anchors.Add(anchor);
        return anchor;
    }

    void StartNewStroke(ARAnchor anchor, Vector3 startPos)
    {
        currentAnchor = anchor;

        // instantiate LineRenderer prefab and parent to anchor for stable tracking
        if (lineRendererPrefab != null)
        {
            currentStrokeGO = Instantiate(lineRendererPrefab, anchor.transform);
            Debug.Log("----------------------------Line renderer");
            currentStrokeGO.transform.localPosition = Vector3.zero; // keep anchor as the reference
            currentLine = currentStrokeGO.GetComponent<LineRenderer>();
            if (currentLine == null) Debug.LogError("LineRenderer prefab missing LineRenderer component");
        }
        else
        {
            // fallback: create a GameObject with LineRenderer
            //currentStrokeGO = new GameObject("Stroke");
            //currentStrokeGO.transform.SetParent(anchor.transform, false);
            //currentLine = currentStrokeGO.AddComponent<LineRenderer>();
            // configure defaults
            //currentLine.positionCount = 0;
            //currentLine.widthMultiplier = 0.01f;
            //currentLine.material = new Material(Shader.Find("Sprites/Default"));
            currentStrokeGO = new GameObject("Stroke");
            currentLine = currentStrokeGO.AddComponent<LineRenderer>();
            currentLine.widthMultiplier = 0.01f;
            currentLine.material = new Material(Shader.Find("Sprites/Default"));
        }

        currentPoints.Clear();
        AddPointToStroke(startPos);
        isDrawing = true;
        UpdatePrompt();
    }

    void AddPointToStroke(Vector3 worldPos)
    {
        if (currentAnchor == null || currentLine == null) return;

        // convert world position into anchor local so the stroke sticks to anchor
        Vector3 localPos = currentAnchor.transform.InverseTransformPoint(worldPos);

        if (currentPoints.Count > 0)
        {
            float dist = Vector3.Distance(currentPoints[currentPoints.Count - 1], localPos);
            if (dist < minDistanceBetweenPoints) return; // skip if too close
        }

        currentPoints.Add(localPos);
        currentLine.positionCount = currentPoints.Count;
        currentLine.SetPosition(currentPoints.Count - 1, localPos);

        // optional: instantiate spheres along the stroke for 3D feel
        if (mode3DToggle != null && mode3DToggle.isOn && spherePrefab != null)
        {
            // place sphere at world position but parent it to anchor so it moves with it
            if (currentPoints.Count == 1)
            {
                Instantiate(spherePrefab, worldPos, Quaternion.identity, currentAnchor.transform);
                Debug.Log("----------------------------spherePrefab");

            }
            else
            {
                Vector3 lastWorld = currentAnchor.transform.TransformPoint(currentPoints[currentPoints.Count - 2]);
                float spacing = Vector3.Distance(lastWorld, worldPos);
                if (spacing >= sphereSpacing)
                {
                    Instantiate(spherePrefab, worldPos, Quaternion.identity, currentAnchor.transform);
                    Debug.Log("----------------------------spherePrefab");

                }
            }
        }
    }

    public void FinishDrawing()
    {
        if (!isDrawing) return;

        isDrawing = false;

        // finalize the LineRenderer -- for optimization we could simplify points or bake to a mesh
        SimplifyStroke(currentPoints, 0.005f);

        // Optionally: add collider to stroke for interaction later
        // currentStrokeGO.AddComponent<MeshCollider>(); // needs mesh generation from line

        currentStrokeGO = null;
        currentLine = null;
        currentPoints = new List<Vector3>();
        currentAnchor = null;

        UpdatePrompt();
    }

    void SimplifyStroke(List<Vector3> points, float tolerance)
    {
        // simple Ramer–Douglas–Peucker implementation could go here. For now we skip heavy simplification.
        // Keep this as a placeholder in case you want to reduce vertices.
    }

    void UpdatePrompt()
    {
        //if (isScanning)
        //{
        //    if (scanPromptPanel != null) scanPromptPanel.SetActive(true);
        //    if (promptText != null) promptText.text = "Scan your environment: move your device to detect walls/surfaces";
        //}
        //else if (isDrawing)
        //{
        //    if (scanPromptPanel != null) scanPromptPanel.SetActive(true);
        //    if (promptText != null) promptText.text = "Drawing... Press Done when finished";
        //}
        //else
        //{
        //    if (scanPromptPanel != null) scanPromptPanel.SetActive(false);
        //    if (promptText != null) promptText.text = "Tap the wall where you want to write";
        //}
        if (isScanning) { promptText.text = "Scan your environment..."; }
        else if (isDrawing) { promptText.text = "Drawing... Press Done"; }
        else { promptText.text = "Tap the wall where you want to write"; }
    }

    // public helper to clear all drawings (for development)
    public void ClearAll()
    {
        foreach (var anchor in anchors)
        {
            if (anchor != null) Destroy(anchor.gameObject);
        }
        anchors.Clear();
    }
}


/*
LineRenderer Prefab Setup (recommended):
- Add a LineRenderer component.
- Set "Use World Space" = False (optional, but we parent to anchor and use local positions)
- Set material (Sprites/Default) and width (0.01 - 0.03).
- Set corner vertices / cap vertices for rounded look.

UI Setup:
- scanPromptPanel: simple panel that shows instructional text (promptText) and optionally Done button while drawing.
- doneButton: call ARDrawingManager.FinishDrawing()
- mode3DToggle: toggles sphere instantiation to create a 3D stamped look. Note: enabling 3D stamping may cost more draw calls and memory.

Performance notes & tips:
- LineRenderer with many vertices can be heavy. Use point simplification (RDP algorithm) after finishing stroke.
- Instantiating many spheres will increase draw calls and GC churn. Consider using GPU instanced mesh or batching techniques.
- If you want decals that look painted onto curved surfaces, consider projecting a texture and baking to a mesh or using deferred decals / shader-based approach.
- To persist across app restarts you need to save anchor poses (ARCloud Anchors / ARCore Cloud Anchors / ARKit WorldMap) and rehydrate anchors on relaunch.

Alternatives & suggestions:
- Using LineRenderer is a good simple approach for 2.5D strokes anchored to surfaces. It is easy to implement and performant with simplification.
- Using sphere/stamps produces a more 3D tactile look but uses more CPU/GPU. A hybrid approach is to use a thin mesh "tube" (generate strip mesh along your polyline) and a shader for thickness, which renders efficiently.
- For fully realistic paint-on-wall look consider projecting a dynamic decal texture onto the surface. That gives true surface-adhesive painting including occlusion with geometry but is more complex.

Extra: If you want I can provide a Ramer–Douglas–Peucker simplifier and a mesh-baker that converts the LineRenderer polyline to a single mesh (which reduces draw calls). */
