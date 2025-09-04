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
            DebugLog($"[CameraRegister] ERRORE: CinemachineCamera non trovato su {gameObject.name}!");
        }
        else
        {
            DebugLog($"[CameraRegister] Component trovato: {virtualCamera.name}");
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
    /// Registrazione ritardata che aspetta il CameraManager
    /// </summary>
    private IEnumerator DelayedRegistration()
    {
        // Aspetta il delay iniziale
        yield return new WaitForSeconds(registrationDelay);
        
        // Trova il CameraManager
        yield return FindCameraManager();
        
        if (cameraManager != null && cameraManager.IsInitialized())
        {
            RegisterCamera();
        }
        else
        {
            DebugLog("[CameraRegister] ⚠️ CameraManager non pronto, registrazione forzata");
            RegisterCamera(); // Prova comunque
        }
    }

    /// <summary>
    /// Trova il CameraManager nella scena corrente
    /// </summary>
    private IEnumerator FindCameraManager()
    {
        int attempts = 0;
        
        while (cameraManager == null && attempts < maxRegistrationAttempts)
        {
            // Prova prima con l'Instance
            cameraManager = CameraManager.Instance;
            
            // Se non trovato, cerca nella scena
            if (cameraManager == null)
            {
                var allManagers = Object.FindObjectsByType<CameraManager>(FindObjectsSortMode.None);
                foreach (var manager in allManagers)
                {
                    if (manager.gameObject.scene == gameObject.scene)
                    {
                        cameraManager = manager;
                        break;
                    }
                }
            }
            
            if (cameraManager == null)
            {
                attempts++;
                DebugLog($"[CameraRegister] CameraManager non trovato, tentativo {attempts}/{maxRegistrationAttempts}");
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        if (cameraManager != null)
        {
            DebugLog($"[CameraRegister] CameraManager trovato: {cameraManager.name}");
        }
        else
        {
            DebugLog("[CameraRegister] ⚠️ CameraManager non trovato dopo tutti i tentativi");
        }
    }

    /// <summary>
    /// Registra la camera con il CameraManager
    /// </summary>
    private void RegisterCamera()
    {
        if (virtualCamera == null || isRegistered) return;
        
        // Assicurati di avere un riferimento al CameraManager
        if (cameraManager == null)
        {
            cameraManager = CameraManager.Instance;
        }
        
        if (cameraManager == null)
        {
            DebugLog("[CameraRegister] ⚠️ Impossibile registrare: CameraManager non trovato");
            return;
        }
        
        DebugLog($"[CameraRegister] Registrazione {virtualCamera.name}...");
        
        cameraManager.Register(virtualCamera);
        isRegistered = true;
        
        DebugLog($"[CameraRegister] ✅ {virtualCamera.name} registrata con successo");
    }

    /// <summary>
    /// Deregistra la camera dal CameraManager
    /// </summary>
    private void UnregisterCamera()
    {
        if (virtualCamera == null || !isRegistered) return;
        
        if (cameraManager != null)
        {
            DebugLog($"[CameraRegister] Deregistrazione {virtualCamera.name}...");
            cameraManager.Unregister(virtualCamera);
        }
        
        isRegistered = false;
        DebugLog($"[CameraRegister] ✅ {virtualCamera.name} deregistrata");
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
        DebugLog("[CameraRegister] Force re-registrazione...");
        
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
    /// Ottieni il riferimento al CameraManager
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
            DebugLog("[CameraRegister] ⚠️ Impossibile attivare camera: riferimenti mancanti");
        }
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
        Debug.Log($"=== CameraRegister State ({gameObject.name}) ===\n" +
                  $"Virtual Camera: {(virtualCamera != null ? virtualCamera.name : "null")}\n" +
                  $"Is Registered: {isRegistered}\n" +
                  $"Camera Manager: {(cameraManager != null ? cameraManager.name : "null")}\n" +
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
}