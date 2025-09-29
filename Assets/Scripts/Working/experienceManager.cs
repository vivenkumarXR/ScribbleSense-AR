using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;

[RequireComponent(typeof(ARPlaneManager))]

public class experienceManager : MonoBehaviour
{
    [SerializeField]
    private UnityEvent OnInitialized;

    [SerializeField]
    private UnityEvent OnRetarded;

    private ARPlaneManager arPlaneManager;

    private bool Initialized { get; set; }

    [System.Obsolete]
    void Awake()
    {
        arPlaneManager = GetComponent<ARPlaneManager>();
        arPlaneManager.planesChanged += PlanesChanged;

#if UNITY_EDITOR
        OnInitialized?.Invoke();
        Initialized = true;
        arPlaneManager.enabled = false;

#endif
    }

    [System.Obsolete]
    void PlanesChanged(ARPlanesChangedEventArgs args)
    {
        if (!Initialized)
        {
            Activate();
        }
    }

    private void Activate()
    {
        OnInitialized?.Invoke();
        Initialized = true;
        arPlaneManager.enabled = false;
    }

    public void Restart()
    {
        OnRetarded?.Invoke();
        Initialized = false;
        arPlaneManager.enabled = true;
    }

}