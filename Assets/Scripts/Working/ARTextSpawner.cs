using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARTextSpawner : MonoBehaviour
{
    public ARRaycastManager raycastManager;
    public CaptureAndSend captureAndSend;
    public GameObject textPrefab;
    private bool hasSpawned = false;
    void Update()
    {
        
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended && captureAndSend.buttonPressed == true)
        {
            Debug.Log("-----------------------------------------captureAndSend.buttonPressed " + captureAndSend.buttonPressed);

            if (hasSpawned)
            {
                return;
            }
            Debug.Log("-----------------------------------------ARTextSpawner ");

            List<ARRaycastHit> hits = new List<ARRaycastHit>();
            if (raycastManager.Raycast(Input.GetTouch(0).position, hits, UnityEngine.XR.ARSubsystems.TrackableType.PlaneWithinPolygon))
            {
                hasSpawned = true;
                Pose pose = hits[0].pose;
                Vector3 spawnPos = pose.position + Vector3.up * 0.05f;
                GameObject txtObj = Instantiate(textPrefab, spawnPos, pose.rotation);
                txtObj.transform.GetComponentInChildren<TextMeshPro>().text = captureAndSend.storedText;
                //hasSpawned = true;
            }
        }
    }
}
