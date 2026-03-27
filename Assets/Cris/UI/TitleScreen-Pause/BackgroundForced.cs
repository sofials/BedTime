using UnityEngine;
using Unity.Cinemachine; // nuovo namespace

[DisallowMultipleComponent]
public class BackgroundForced : MonoBehaviour
{
    [Header("Assegna le Cinemachine Camera")]
    public CinemachineCamera mainCamera;       // nuova classe
    public CinemachineCamera panoramicCamera;  // nuova classe

    [Header("Priorità menu attivo")]
    public int forcedPanoramicPriority = 20;
    public int forcedMainPriority = 0;

    int originalMainPriority;
    int originalPanoramicPriority;
    bool originalSaved = false;

    void OnEnable()
    {
        SaveOriginalPriorities();
        ForcePanoramic();
    }

    void OnDisable()
    {
        RestorePriorities();
    }

    void SaveOriginalPriorities()
    {
        if (originalSaved) return;
        if (mainCamera != null) originalMainPriority = mainCamera.Priority;
        if (panoramicCamera != null) originalPanoramicPriority = panoramicCamera.Priority;
        originalSaved = true;
    }

    public void ForcePanoramic()
    {
        if (mainCamera == null || panoramicCamera == null)
        {
            Debug.LogWarning("[BackgroundForced] Assegna le camere Cinemachine nell'Inspector.", this);
            return;
        }

        mainCamera.Priority = forcedMainPriority;
        panoramicCamera.Priority = forcedPanoramicPriority;

        Debug.Log($"[BackgroundForced] Forzata panoramica. main={mainCamera.Priority} pano={panoramicCamera.Priority}", this);
    }

    public void ForceMain()
    {
        if (!originalSaved)
        {
            if (mainCamera != null) mainCamera.Priority = 20;
            if (panoramicCamera != null) panoramicCamera.Priority = 10;
        }
        else
        {
            if (mainCamera != null) mainCamera.Priority = originalMainPriority;
            if (panoramicCamera != null) panoramicCamera.Priority = originalPanoramicPriority;
        }

        originalSaved = false;
        Debug.Log("[BackgroundForced] Forzata main camera (ripristino).", this);
    }

    void RestorePriorities()
    {
        if (!originalSaved) return;
        if (mainCamera != null) mainCamera.Priority = originalMainPriority;
        if (panoramicCamera != null) panoramicCamera.Priority = originalPanoramicPriority;
        originalSaved = false;
        Debug.Log("[BackgroundForced] Ripristinate priorità originali.", this);
    }
}
