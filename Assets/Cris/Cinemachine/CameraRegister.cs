using Unity.Cinemachine;
using UnityEngine;
using System.Collections;

public class CameraRegister : MonoBehaviour
{
    [Header("Auto Registration")]
    [SerializeField] private bool autoRegisterOnEnable = true;
    [SerializeField] private bool autoUnregisterOnDisable = true;
    [SerializeField] private bool forceRegisterOnStart = true;
    
    [Header("Registration Settings")]
    [SerializeField] private float registrationDelay = 0.1f;
    [SerializeField] private int maxRegistrationAttempts = 10;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private CinemachineCamera virtualCamera;
    private bool isRegistered = false;
    private CameraManager cameraManager;

    void Awake()
    {
        virtualCamera = GetComponent<CinemachineCamera>();
        if (virtualCamera == null)
        {
            DebugLog($"[SimpleCameraRegister] ERRORE: CinemachineCamera non trovato su {gameObject.name}!");
        }
        else
        {
            DebugLog($"[SimpleCameraRegister] Component trovato: {virtualCamera.name}");
        }
    }

    void Start()
    {
        if (forceRegisterOnStart && virtualCamera != null)
        {
            StartCoroutine(DelayedRegistration());
        }
    }

    private void OnEnable()
    {
        if (autoRegisterOnEnable && virtualCamera != null && !isRegistered)
        {
            StartCoroutine(DelayedRegistration());
        }
    }

    private void OnDisable()
    {
        if (autoUnregisterOnDisable && virtualCamera != null && isRegistered)
        {
            UnregisterCamera();
        }
    }

    /// <summary>
    /// Registrazione ritardata che aspetta il SimpleCameraManager
    /// </summary>
    private IEnumerator DelayedRegistration()
    {
        // Aspetta il delay iniziale
        yield return new WaitForSeconds(registrationDelay);
        
        // Trova il SimpleCameraManager
        yield return FindCameraManager();
        
        if (cameraManager != null && cameraManager.IsInitialized())
        {
            RegisterCamera();
        }
        else
        {
            DebugLog("[SimpleCameraRegister] SimpleCameraManager non pronto, registrazione forzata");
            RegisterCamera(); // Prova comunque
        }
    }

    /// <summary>
    /// Trova il SimpleCameraManager nella scena corrente
    /// </summary>
    private IEnumerator FindCameraManager()
    {
        int attempts = 0;
        
        while (cameraManager == null && attempts < maxRegistrationAttempts)
        {
            // Cerca SimpleCameraManager nella scena
            var allManagers = Object.FindObjectsByType<CameraManager>(FindObjectsSortMode.None);
            if (allManagers.Length > 0)
            {
                cameraManager = allManagers[0];
                DebugLog($"[SimpleCameraRegister] SimpleCameraManager trovato: {cameraManager.name}");
                break;
            }
            
            attempts++;
            DebugLog($"[SimpleCameraRegister] SimpleCameraManager non trovato, tentativo {attempts}/{maxRegistrationAttempts}");
            yield return new WaitForSeconds(0.2f);
        }
        
        if (cameraManager != null)
        {
            DebugLog($"[SimpleCameraRegister] SimpleCameraManager collegato: {cameraManager.name}");
        }
        else
        {
            DebugLog("[SimpleCameraRegister] SimpleCameraManager non trovato dopo tutti i tentativi!");
        }
    }

    /// <summary>
    /// Registra la camera con il SimpleCameraManager
    /// </summary>
    private void RegisterCamera()
    {
        if (virtualCamera == null || isRegistered) return;
        
        // Assicurati di avere un riferimento al SimpleCameraManager
        if (cameraManager == null)
        {
            var managers = Object.FindObjectsByType<CameraManager>(FindObjectsSortMode.None);
            if (managers.Length > 0)
            {
                cameraManager = managers[0];
            }
        }
        
        if (cameraManager == null)
        {
            DebugLog("[SimpleCameraRegister] Impossibile registrare: SimpleCameraManager non trovato");
            return;
        }
        
        DebugLog($"[SimpleCameraRegister] Registrazione {virtualCamera.name}...");
        
        cameraManager.Register(virtualCamera);
        isRegistered = true;
        
        DebugLog($"[SimpleCameraRegister] {virtualCamera.name} registrata con successo");
    }

    /// <summary>
    /// Deregistra la camera dal SimpleCameraManager
    /// </summary>
    private void UnregisterCamera()
    {
        if (virtualCamera == null || !isRegistered) return;
        
        if (cameraManager != null)
        {
            DebugLog($"[SimpleCameraRegister] Deregistrazione {virtualCamera.name}...");
            cameraManager.Unregister(virtualCamera);
        }
        
        isRegistered = false;
        DebugLog($"[SimpleCameraRegister] {virtualCamera.name} deregistrata");
    }

    /// <summary>
    /// Registrazione manuale
    /// </summary>
    public void ManualRegister()
    {
        if (virtualCamera != null)
        {
            isRegistered = false; // Reset per permettere ri-registrazione
            StartCoroutine(DelayedRegistration());
        }
    }

    /// <summary>
    /// Deregistrazione manuale
    /// </summary>
    public void ManualUnregister()
    {
        if (virtualCamera != null)
        {
            UnregisterCamera();
        }
    }
    
    /// <summary>
    /// Forza re-registrazione
    /// </summary>
    public void ForceReregister()
    {
        DebugLog("[SimpleCameraRegister] Force re-registrazione...");
        
        if (isRegistered)
        {
            UnregisterCamera();
        }
        
        StartCoroutine(DelayedRegistration());
    }
    
    /// <summary>
    /// Verifica se la camera è registrata
    /// </summary>
    public bool IsRegistered() => isRegistered;
    
    /// <summary>
    /// Ottieni la camera virtuale
    /// </summary>
    public CinemachineCamera GetVirtualCamera() => virtualCamera;

    /// <summary>
    /// Ottieni il riferimento al SimpleCameraManager
    /// </summary>
    public CameraManager GetCameraManager() => cameraManager;

    /// <summary>
    /// Verifica se questa camera è attualmente attiva
    /// </summary>
    public bool IsActiveCamera()
    {
        if (cameraManager == null || virtualCamera == null)
            return false;
            
        return cameraManager.IsActiveCamera(virtualCamera);
    }

    /// <summary>
    /// Attiva questa camera
    /// </summary>
    public void ActivateThisCamera()
    {
        if (cameraManager != null && virtualCamera != null)
        {
            cameraManager.SwitchCamera(virtualCamera);
        }
        else
        {
            DebugLog("[SimpleCameraRegister] Impossibile attivare camera: riferimenti mancanti");
        }
    }

    /// <summary>
    /// Verifica se il SimpleCameraManager è pronto
    /// </summary>
    public bool IsCameraManagerReady()
    {
        return cameraManager != null && cameraManager.IsCameraSystemReady();
    }

    /// <summary>
    /// Aspetta che il SimpleCameraManager sia pronto
    /// </summary>
    public IEnumerator WaitForCameraManagerReady()
    {
        float waitTime = 0f;
        const float maxWaitTime = 5f;
        
        while (!IsCameraManagerReady() && waitTime < maxWaitTime)
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
            
            // Riprova a trovare il manager se non c'è
            if (cameraManager == null)
            {
                yield return FindCameraManager();
            }
        }
        
        if (IsCameraManagerReady())
        {
            DebugLog("[SimpleCameraRegister] SimpleCameraManager pronto");
        }
        else
        {
            DebugLog($"[SimpleCameraRegister] Timeout aspettando SimpleCameraManager dopo {maxWaitTime}s");
        }
    }

    /// <summary>
    /// Forza la ricerca di un nuovo SimpleCameraManager
    /// </summary>
    public void RefreshCameraManager()
    {
        cameraManager = null;
        StartCoroutine(FindCameraManager());
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log(message);
        }
    }

    // ========== DEBUG E TESTING ==========

    [ContextMenu("Debug Camera Register State")]
    public void DebugState()
    {
        Debug.Log($"=== SimpleCameraRegister State ({gameObject.name}) ===\n" +
                  $"Virtual Camera: {(virtualCamera != null ? virtualCamera.name : "null")}\n" +
                  $"Is Registered: {isRegistered}\n" +
                  $"Camera Manager: {(cameraManager != null ? cameraManager.name : "null")}\n" +
                  $"Manager Ready: {IsCameraManagerReady()}\n" +
                  $"Is Active Camera: {IsActiveCamera()}\n" +
                  $"Current Scene: {gameObject.scene.name}\n" +
                  $"Auto Register: {autoRegisterOnEnable}\n" +
                  $"Auto Unregister: {autoUnregisterOnDisable}\n" +
                  $"Force Start: {forceRegisterOnStart}");
    }

    [ContextMenu("Test - Manual Register")]
    public void TestManualRegister()
    {
        ManualRegister();
    }

    [ContextMenu("Test - Force Reregister")]
    public void TestForceReregister()
    {
        ForceReregister();
    }

    [ContextMenu("Test - Activate This Camera")]
    public void TestActivateCamera()
    {
        ActivateThisCamera();
    }

    [ContextMenu("Test - Find Camera Manager")]
    public void TestFindCameraManager()
    {
        StartCoroutine(FindCameraManager());
    }

    [ContextMenu("Test - Refresh Camera Manager")]
    public void TestRefreshCameraManager()
    {
        RefreshCameraManager();
    }

    [ContextMenu("Test - Wait For Manager Ready")]
    public void TestWaitForManagerReady()
    {
        StartCoroutine(WaitForCameraManagerReady());
    }

    // ========== CALLBACK EVENTS ==========
    
    /// <summary>
    /// Chiamato quando il GameObject viene attivato
    /// Utile per debug o logiche personalizzate
    /// </summary>
    private void OnValidate()
    {
        // Assicurati che ci sia una CinemachineCamera
        if (virtualCamera == null)
        {
            virtualCamera = GetComponent<CinemachineCamera>();
        }
    }

    /// <summary>
    /// Cleanup quando l'oggetto viene distrutto
    /// </summary>
    private void OnDestroy()
    {
        if (isRegistered && cameraManager != null && virtualCamera != null)
        {
            DebugLog($"[SimpleCameraRegister] Cleanup: deregistrazione {virtualCamera.name}");
            cameraManager.Unregister(virtualCamera);
        }
        
        isRegistered = false;
        cameraManager = null;
    }
}