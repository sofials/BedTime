using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[System.Serializable]
public class CameraTransition
{
    public float blendDuration = 1f;
    public CinemachineBlendDefinition.Styles blendStyle = CinemachineBlendDefinition.Styles.EaseInOut;
}

public class CameraManager : MonoBehaviour
{
    [Header("Camera System")]
    [SerializeField] private CinemachineCamera startingCamera;
    [SerializeField] private List<CinemachineCamera> cameras = new List<CinemachineCamera>();
    [SerializeField] private Dictionary<CinemachineCamera, int> originalPriorities = new Dictionary<CinemachineCamera, int>();

    [Header("Camera Settings")]
    [SerializeField] private CinemachineCamera activeCamera = null;
    [SerializeField] private int basePriority = 10;
    [SerializeField] private int activePriority = 100;
    
    // FIX: Costanti per priorità consistenti
    private const int PRIORITY_OFFSET_EDITOR = 0;
    private const int PRIORITY_OFFSET_BUILD_INITIAL = 200;
    private const int PRIORITY_OFFSET_BUILD_RETRY = 300;
    private const int PRIORITY_OFFSET_BUILD_FORCE = 500;
    
    [Header("ThirdPersonController Integration")]
    [SerializeField] private bool autoNotifyControllers = true;

    [Header("Transition Settings")]
    public CameraTransition defaultTransition = new CameraTransition();

    [Header("Auto Setup")]
    [SerializeField] private bool autoRegisterOnStart = true;
    [SerializeField] private bool findStartingCameraAutomatically = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Camera System Events")]
    public UnityEvent OnCameraSystemInitialized;
    public UnityEvent OnCameraSystemReady;
    public UnityEvent<CinemachineCamera> OnCameraActivated;
    public UnityEvent<CinemachineCamera, CinemachineCamera> OnCameraSwitched;

    private CinemachineBrain brain;
    private bool isInitialized = false;
    private bool isInitializing = false;
    private bool isCameraSystemReady = false;

    [Header("Blend Protection")]
    [Tooltip("Se true, blocca nuovi switch camera mentre un blend è in corso")]
    [SerializeField] private bool blockSwitchDuringBlend = true;
    [Tooltip("Se true, permette di forzare lo switch anche durante un blend (solo per casi speciali)")]
    [SerializeField] private bool allowForceSwitch = true;
    
    // FIX: Traccia coroutine attive per prevenire duplicati
    private Coroutine initializationCoroutine = null;
    private int initializationRetryCount = 0;
    private const int MAX_INITIALIZATION_RETRIES = 3;
    
    // Build-specific settings (disponibili sempre per evitare errori di serializzazione)
    [Header("Build Settings")]
    [SerializeField] private bool useExtendedBuildWait = true;
    [SerializeField] private float buildExtraWaitTime = 0.5f;
    
#if !UNITY_EDITOR
    private Coroutine buildCheckCoroutine = null;
#endif
    
    void Start()
    {
        var currentScene = SceneManager.GetActiveScene();
        OnScreenDebugLogger.LogCamera($"=== CAMERA MANAGER START - SCENA: {currentScene.name} ===");
        var allCameras = Camera.allCameras;
        OnScreenDebugLogger.LogCamera($"TUTTE LE CAMERE UNITY: {allCameras.Length}");
        foreach (var cam in allCameras)
        {
            OnScreenDebugLogger.LogCamera($"Camera Unity: {cam.name}, Scene: {cam.gameObject.scene.name}, Active: {cam.gameObject.activeInHierarchy}, Tag: {cam.tag}");
        }
        var allVCams = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);

        // FIX: Salva priorità PRIMA di modificarle
        foreach (var vcam in allVCams)
        {
            if (vcam.gameObject.scene != currentScene)
            {
                if (!originalPriorities.ContainsKey(vcam))
                {
                    originalPriorities[vcam] = vcam.Priority;
                }
                DebugLog($"[Cleanup] Disattivando camera da scena precedente: {vcam.name}");
                vcam.Priority = -1000;
                vcam.gameObject.SetActive(false);
            }
        }

        // Cleanup iniziale per evitare riferimenti da scene precedenti
        cameras.Clear();
        activeCamera = null;
        brain = null;
        isInitialized = false;
        isInitializing = false;
        isCameraSystemReady = false;
        initializationRetryCount = 0;

        if (autoRegisterOnStart)
        {
            StartInitializationSafe();
        }

    }

    private void NotifyControllersOfCameraChange(CinemachineCamera newCamera)
    {
        if (!autoNotifyControllers || newCamera == null) return;
        
        ThirdPersonController[] controllers = FindObjectsByType<ThirdPersonController>(FindObjectsSortMode.None);
        
        foreach (var controller in controllers)
        {
            if (controller != null && controller.GetAutoDetectCamera())
            {
                controller.ForceUpdateActiveCamera();
            }
        }
        
        DebugLog($"[CameraManager] Notificati {controllers.Length} controller del cambio camera: {newCamera.name}");
    }

    // FIX: Metodo sicuro per avviare inizializzazione
    private void StartInitializationSafe()
    {
        if (initializationCoroutine != null)
        {
            StopCoroutine(initializationCoroutine);
        }
        
        if (gameObject.activeInHierarchy && enabled)
        {
            initializationCoroutine = StartCoroutine(InitializeCameraManagerCoroutine());
        }
        else if (initializationRetryCount < MAX_INITIALIZATION_RETRIES)
        {
            initializationRetryCount++;
            Invoke(nameof(StartInitializationSafe), 0.1f);
        }
        else
        {
            DebugLog("[CameraManager] Max initialization retries reached, stopping attempts");
        }
    }

#if !UNITY_EDITOR
    // FIX: Metodo sicuro per avviare build check
    private void StartBuildCheckSafe()
    {
        if (buildCheckCoroutine != null)
        {
            StopCoroutine(buildCheckCoroutine);
        }
        
        if (gameObject.activeInHierarchy && enabled)
        {
            buildCheckCoroutine = StartCoroutine(BuildSpecificCameraCheck());
        }
        else
        {
            Invoke(nameof(StartBuildCheckSafe), 0.2f);
        }
    }
#endif

    private IEnumerator InitializeCameraManagerCoroutine()
    {
        // FIX: Previeni esecuzioni multiple
        if (isInitializing)
        {
            DebugLog("[CameraManager] Initialization already in progress, skipping");
            yield break;
        }

        isInitializing = true;
        DebugLog("[CameraManager] Inizializzazione camera system avviata");


        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);
        
#if !UNITY_EDITOR
        // Attesa extra per build se abilitata
        if (useExtendedBuildWait)
        {
            yield return new WaitForSeconds(buildExtraWaitTime);
        }
#endif

        yield return FindCinemachineBrain();
        RegisterAllCamerasInScene();
        yield return SetupStartingCamera();

        isInitialized = true;
        isCameraSystemReady = true;
        isInitializing = false;
        initializationCoroutine = null;

        DebugLog("[CameraManager] Camera system completamente inizializzato");
        
        OnCameraSystemInitialized?.Invoke();
        OnCameraSystemReady?.Invoke();
        
#if !UNITY_EDITOR
        yield return new WaitForSeconds(0.5f);
        yield return VerifyCameraIsWorking();
#endif
    }

#if !UNITY_EDITOR
    private IEnumerator BuildSpecificCameraCheck()
    {
        yield return new WaitForSeconds(3f);
        
        DebugLog("[Build Check] Verifica stato camera system...");
        
        if (!isCameraSystemReady)
        {
            DebugLog("[Build Check] Sistema camera non pronto, forzando inizializzazione");
            yield return InitializeCameraManagerCoroutine();
        }
        
        if (activeCamera == null && cameras.Count > 0)
        {
            DebugLog("[Build Check] Nessuna camera attiva, registrazione forzata");
            
            RegisterAllCamerasInScene();
            
            if (cameras.Count > 0)
            {
                SwitchCamera(cameras[0]);
                DebugLog($"[Build Check] Camera attivata: {cameras[0].name}");
            }
        }
        
        // FIX: Verifica brain prima di usarlo
        if (brain != null && activeCamera != null)
        {
            var brainCamera = brain.ActiveVirtualCamera as CinemachineCamera;
            if (brainCamera != activeCamera)
            {
                DebugLog($"[Build Check] Brain mismatch! Forzando: {activeCamera.name}");
                activeCamera.Priority = activePriority + PRIORITY_OFFSET_BUILD_RETRY;
            }
        }
        else if (brain == null)
        {
            DebugLog("[Build Check] Brain non trovato, cercando...");
            yield return FindCinemachineBrain();
        }
        
        yield return new WaitForSeconds(1f);
        
        if (activeCamera != null)
        {
            DebugLog($"[Build Check] Sistema confermato - Active: {activeCamera.name}");
        }
        else
        {
            DebugLog("[Build Check] Sistema non funzionante!");
        }
        
        buildCheckCoroutine = null;
    }
#endif

    private IEnumerator FindCinemachineBrain()
    {
        brain = null;
        var currentScene = SceneManager.GetActiveScene();
        int attempts = 0;
        const int maxAttempts = 15;

        while (brain == null && attempts < maxAttempts)
        {
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                brain = mainCamera.GetComponent<CinemachineBrain>();
                
                // FIX: Verifica che sia nella scena corrente
                if (brain != null && brain.gameObject.scene != currentScene)
                {
                    DebugLog($"[CameraManager] Brain trovato ma in scena diversa: {brain.gameObject.scene.name}");
                    brain = null;
                }
            }

            if (brain == null)
            {
                var allBrains = Object.FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None);
                foreach (var brainCandidate in allBrains)
                {
                    if (brainCandidate != null && brainCandidate.gameObject.scene == currentScene)
                    {
                        brain = brainCandidate;
                        break;
                    }
                }
            }

            if (brain == null)
            {
                attempts++;
                DebugLog($"[CameraManager] CinemachineBrain non trovato nella scena {currentScene.name}, tentativo {attempts}/{maxAttempts}");
                yield return new WaitForSeconds(0.2f);
            }
        }

        if (brain != null)
        {
            DebugLog($"[CameraManager] CinemachineBrain trovato nella scena: {currentScene.name}");
        }
        else
        {
            DebugLog($"[CameraManager] ERRORE: CinemachineBrain non trovato nella scena {currentScene.name}!");
        }
    }

    public void RegisterAllCamerasInScene()
    {
        var currentScene = SceneManager.GetActiveScene();
        var allCameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.InstanceID);

        DebugLog($"[CameraManager] Scansione camere nella scena {currentScene.name}: {allCameras.Length} trovate");

        int registered = 0;
        foreach (var cam in allCameras)
        {
            if (cam != null && cam.gameObject.scene == currentScene)
            {
                Register(cam);
                registered++;
            }
        }

        DebugLog($"[CameraManager] {registered} camere registrate nella scena {currentScene.name}");
    }

    /// <summary>
    /// Controlla se il CinemachineBrain sta attualmente blendando tra due camere
    /// </summary>
    public bool IsBlending()
    {
        if (brain == null) return false;
        return brain.IsBlending;
    }

    public void SwitchCamera(CinemachineCamera newCamera)
    {
        SwitchCamera(newCamera, false);
    }

    public void SwitchCamera(CinemachineCamera newCamera, bool forceSwitch)
    {
        if (newCamera == null)
        {
            DebugLog("[CameraManager] SwitchCamera: newCamera is null!");
            return;
        }

        if (newCamera == activeCamera)
        {
            DebugLog($"[CameraManager] Camera {newCamera.name} è già attiva");
            return;
        }

        // ✅ PROTEZIONE BLEND: blocca switch se un blend è in corso (a meno che non sia forzato)
        if (blockSwitchDuringBlend && IsBlending() && !forceSwitch)
        {
            DebugLog($"[CameraManager] Switch a {newCamera.name} BLOCCATO - blend in corso. Usa ForceSwitchCamera() per forzare.");
            return;
        }

        CinemachineCamera previousCamera = activeCamera;
        DebugLog($"[CameraManager] Cambio camera: da {(activeCamera != null ? activeCamera.name : "nessuna")} a {newCamera.name}");

        if (!cameras.Contains(newCamera))
        {
            Register(newCamera);
        }

        if (!newCamera.gameObject.activeInHierarchy)
        {
            DebugLog($"[CameraManager] Camera {newCamera.name} non è attiva, attivandola...");
            newCamera.gameObject.SetActive(true);
        }

        // FIX: Verifica brain prima di usarlo
        if (defaultTransition != null && brain != null)
        {
            brain.DefaultBlend.Time = defaultTransition.blendDuration;
            brain.DefaultBlend.Style = defaultTransition.blendStyle;
        }

        // FIX: Usa costanti per priorità consistenti
#if !UNITY_EDITOR
        foreach (CinemachineCamera cam in cameras.Where(c => c != null && c != newCamera))
        {
            cam.Priority = basePriority - 10;
        }
        newCamera.Priority = activePriority + PRIORITY_OFFSET_BUILD_INITIAL;
#else
        newCamera.Priority = activePriority + PRIORITY_OFFSET_EDITOR;
        foreach (CinemachineCamera cam in cameras.Where(c => c != null && c != newCamera))
        {
            int priority = originalPriorities.ContainsKey(cam) ? originalPriorities[cam] : basePriority;
            cam.Priority = priority;
        }
#endif

        activeCamera = newCamera;

        OnCameraActivated?.Invoke(newCamera);
        if (previousCamera != null)
        {
            OnCameraSwitched?.Invoke(previousCamera, newCamera);
        }

#if !UNITY_EDITOR
        if (gameObject.activeInHierarchy && enabled)
        {
            StartCoroutine(VerifyCameraSwitchCoroutine(newCamera));
        }
#endif
        
        // FIX: Notifica controller solo una volta
        if (gameObject.activeInHierarchy && enabled)
        {
            StartCoroutine(DelayedControllerNotification(newCamera));
        }
    }

    /// <summary>
    /// Cambia camera con blend style e durata personalizzati (per transizioni specifiche)
    /// </summary>
    public void SwitchCameraWithCustomBlend(CinemachineCamera newCamera, CinemachineBlendDefinition.Styles blendStyle, float blendDuration)
    {
        if (newCamera == null)
        {
            DebugLog("[CameraManager] SwitchCameraWithCustomBlend: newCamera is null!");
            return;
        }

        if (newCamera == activeCamera)
        {
            DebugLog($"[CameraManager] Camera {newCamera.name} è già attiva");
            return;
        }

        // Protezione blend
        if (blockSwitchDuringBlend && IsBlending())
        {
            DebugLog($"[CameraManager] Switch a {newCamera.name} BLOCCATO - blend in corso");
            return;
        }

        // Salva le impostazioni originali
        float originalDuration = defaultTransition != null ? defaultTransition.blendDuration : 1f;
        CinemachineBlendDefinition.Styles originalStyle = defaultTransition != null ? defaultTransition.blendStyle : CinemachineBlendDefinition.Styles.EaseInOut;

        // Applica le impostazioni custom temporaneamente
        if (brain != null)
        {
            brain.DefaultBlend.Time = blendDuration;
            brain.DefaultBlend.Style = blendStyle;
            DebugLog($"[CameraManager] Custom blend applicato: {blendStyle}, {blendDuration}s");
        }

        // Esegui lo switch (senza passare per SwitchCamera per evitare che sovrascriva il blend)
        CinemachineCamera previousCam = activeCamera;

        if (!cameras.Contains(newCamera))
        {
            Register(newCamera);
        }

        if (!newCamera.gameObject.activeInHierarchy)
        {
            newCamera.gameObject.SetActive(true);
        }

#if !UNITY_EDITOR
        foreach (CinemachineCamera cam in cameras.Where(c => c != null && c != newCamera))
        {
            cam.Priority = basePriority - 10;
        }
        newCamera.Priority = activePriority + PRIORITY_OFFSET_BUILD_INITIAL;
#else
        newCamera.Priority = activePriority + PRIORITY_OFFSET_EDITOR;
        foreach (CinemachineCamera cam in cameras.Where(c => c != null && c != newCamera))
        {
            int priority = originalPriorities.ContainsKey(cam) ? originalPriorities[cam] : basePriority;
            cam.Priority = priority;
        }
#endif

        activeCamera = newCamera;
        DebugLog($"[CameraManager] Cambio camera (custom blend): da {(previousCam != null ? previousCam.name : "nessuna")} a {newCamera.name}");

        OnCameraActivated?.Invoke(newCamera);
        if (previousCam != null)
        {
            OnCameraSwitched?.Invoke(previousCam, newCamera);
        }

        // Ripristina le impostazioni originali dopo il blend
        if (gameObject.activeInHierarchy && enabled)
        {
            StartCoroutine(RestoreDefaultBlendAfterDelay(originalStyle, originalDuration, blendDuration + 0.1f));
            StartCoroutine(DelayedControllerNotification(newCamera));
        }
    }

    private IEnumerator RestoreDefaultBlendAfterDelay(CinemachineBlendDefinition.Styles originalStyle, float originalDuration, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (brain != null && defaultTransition != null)
        {
            brain.DefaultBlend.Time = originalDuration;
            brain.DefaultBlend.Style = originalStyle;
            DebugLog($"[CameraManager] Blend ripristinato: {originalStyle}, {originalDuration}s");
        }
    }

    private IEnumerator DelayedControllerNotification(CinemachineCamera newCamera)
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForFixedUpdate();
        NotifyControllersOfCameraChange(newCamera);
    }

    public void ForceNotifyAllControllers()
    {
        if (activeCamera != null)
        {
            NotifyControllersOfCameraChange(activeCamera);
        }
    }

#if !UNITY_EDITOR
    private IEnumerator VerifyCameraSwitchCoroutine(CinemachineCamera expectedCamera)
    {
        yield return new WaitForSeconds(1.5f);
        
        if (expectedCamera == null || expectedCamera.gameObject == null)
        {
            DebugLog("[Build] Camera switch verification failed! Expected camera is null");
            yield break;
        }
        
        if (activeCamera != expectedCamera)
        {
            string activeCameraName = "null";
            if (activeCamera != null && activeCamera.gameObject != null)
            {
                activeCameraName = activeCamera.name;
            }
            
            DebugLog($"[Build] Camera switch failed! Expected: {expectedCamera.name}, Got: {activeCameraName}");
            
            expectedCamera.Priority = activePriority + PRIORITY_OFFSET_BUILD_RETRY;
            activeCamera = expectedCamera;
            
            foreach (var cam in cameras.Where(c => c != null && c != expectedCamera))
            {
                cam.gameObject.SetActive(false);
            }
            
            yield return new WaitForSeconds(0.5f);
            
            foreach (var cam in cameras.Where(c => c != null && c != expectedCamera))
            {
                cam.gameObject.SetActive(true);
                cam.Priority = basePriority - 20;
            }
        }
        else
        {
            DebugLog($"[Build] Camera switch verified: {expectedCamera.name}");
        }
    }
#endif

    private IEnumerator VerifyCameraIsWorking()
    {
        if (activeCamera == null) yield break;
        
        DebugLog($"[CameraManager] Verifica funzionamento camera: {activeCamera.name}");
        
        if (!activeCamera.gameObject.activeInHierarchy)
        {
            activeCamera.gameObject.SetActive(true);
            DebugLog($"[CameraManager] Camera {activeCamera.name} riattivata");
        }
        
        if (activeCamera.Priority < activePriority)
        {
            activeCamera.Priority = activePriority + 100;
            DebugLog($"[CameraManager] Priorità camera corretta: {activeCamera.Priority}");
        }
        
        yield return new WaitForSeconds(0.3f);
        
        // FIX: Verifica brain prima di usarlo
        if (brain != null)
        {
            var currentBrainCamera = brain.ActiveVirtualCamera as CinemachineCamera;
            if (currentBrainCamera != activeCamera)
            {
                DebugLog($"[CameraManager] Brain mismatch! Brain: {currentBrainCamera?.name ?? "null"}, Active: {activeCamera.name}");
                activeCamera.Priority = activePriority + PRIORITY_OFFSET_BUILD_FORCE;
            }
            else
            {
                DebugLog($"[CameraManager] Camera verification OK: {activeCamera.name}");
            }
        }
        else
        {
            DebugLog("[CameraManager] Brain non disponibile per verifica");
        }
    }

    public void Register(CinemachineCamera camera)
    {
        if (camera == null) return;

        if (cameras.Contains(camera))
        {
            DebugLog($"[CameraManager] Camera {camera.name} già registrata");
            return;
        }

        cameras.Add(camera);

        if (!originalPriorities.ContainsKey(camera))
            originalPriorities[camera] = camera.Priority;

        camera.Priority = basePriority;

        DebugLog($"[CameraManager] Camera registrata: {camera.name} (Priority: {camera.Priority})");
    }

    public void Unregister(CinemachineCamera camera)
    {
        if (camera == null) return;

        cameras.Remove(camera);
        originalPriorities.Remove(camera);

        if (activeCamera == camera)
        {
            activeCamera = null;
            if (cameras.Count > 0 && gameObject.activeInHierarchy && enabled)
            {
                SwitchCamera(cameras[0]);
            }
        }

        DebugLog($"[CameraManager] Camera deregistrata: {camera.name}");
    }

    private IEnumerator SetupStartingCamera()
    {
        CinemachineCamera targetCamera = startingCamera;

        if (targetCamera == null && findStartingCameraAutomatically && cameras.Count > 0)
        {
            string[] preferredNames = { "PlayerOrbitalCamera", "PlayerCamera", "MainCamera", "GameCamera" };

            foreach (string name in preferredNames)
            {
                targetCamera = cameras.FirstOrDefault(cam => cam != null && cam.name.Contains(name));
                if (targetCamera != null)
                {
                    DebugLog($"[CameraManager] Starting camera trovata automaticamente: {targetCamera.name}");
                    break;
                }
            }

            if (targetCamera == null)
            {
                targetCamera = cameras[0];
                DebugLog($"[CameraManager] Usando prima camera disponibile: {targetCamera.name}");
            }
        }

        if (targetCamera != null)
        {
            SwitchCamera(targetCamera);
            yield return new WaitForSeconds(0.2f);
            // FIX: Rimossa notifica duplicata (già gestita da SwitchCamera)
        }
        else
        {
            DebugLog("[CameraManager] Nessuna camera disponibile!");
        }

        yield return null;
    }

    public bool IsCameraSystemReady()
    {
        return isCameraSystemReady && activeCamera != null && isInitialized;
    }

    public bool IsInitialized() => isInitialized;
    
    public CinemachineCamera GetActiveCamera() => activeCamera;
    
    public List<CinemachineCamera> GetAllCameras()
    {
        cameras.RemoveAll(cam => cam == null);
        return new List<CinemachineCamera>(cameras);
    }

    public CinemachineCamera GetCameraByName(string name)
    {
        return cameras.FirstOrDefault(cam => cam != null && cam.name == name);
    }

    public void SwitchCameraByName(string cameraName)
    {
        var camera = GetCameraByName(cameraName);
        if (camera != null)
        {
            SwitchCamera(camera);
        }
        else
        {
            DebugLog($"[CameraManager] Camera '{cameraName}' non trovata!");
        }
    }

    /// <summary>
    /// Forza lo switch della camera anche se un blend è in corso
    /// </summary>
    public void ForceSwitchCamera(CinemachineCamera newCamera)
    {
        if (!allowForceSwitch)
        {
            DebugLog("[CameraManager] ForceSwitchCamera non permesso (allowForceSwitch = false)");
            return;
        }
        SwitchCamera(newCamera, true);
    }

    /// <summary>
    /// Forza lo switch della camera per nome anche se un blend è in corso
    /// </summary>
    public void ForceSwitchCameraByName(string cameraName)
    {
        var camera = GetCameraByName(cameraName);
        if (camera != null)
        {
            ForceSwitchCamera(camera);
        }
        else
        {
            DebugLog($"[CameraManager] Camera '{cameraName}' non trovata!");
        }
    }

    public bool IsActiveCamera(CinemachineCamera camera)
    {
        return camera == activeCamera;
    }

    public void SetCameraPriority(CinemachineCamera camera, int priority)
    {
        if (camera != null && cameras.Contains(camera))
        {
            originalPriorities[camera] = priority;
            if (camera != activeCamera)
                camera.Priority = priority;
        }
    }

    public void ForceInitialize()
    {
        if (!isInitializing && gameObject.activeInHierarchy && enabled)
        {
            StartInitializationSafe();
        }
    }

    public void EnsureCamerasAreReady()
    {
        if (!isCameraSystemReady)
        {
            DebugLog("[CameraManager] Sistema non pronto - forzando inizializzazione");
            if (!isInitializing)
            {
                ForceInitialize();
            }
            return;
        }

        if (activeCamera == null && cameras.Count > 0)
        {
            DebugLog("[CameraManager] Nessuna camera attiva - attivando la prima disponibile");
            SwitchCamera(cameras[0]);
        }
        else if (activeCamera != null)
        {
            DebugLog($"[CameraManager] Sistema OK - Active: {activeCamera.name}");
        }
        else
        {
            DebugLog("[CameraManager] Nessuna camera disponibile!");
        }
    }

    [ContextMenu("Debug Camera State")]
    public void DebugCameraState()
    {
        Debug.Log($"=== Camera Manager State ===\n" +
                  $"Initialized: {isInitialized}\n" +
                  $"Initializing: {isInitializing}\n" +
                  $"System Ready: {isCameraSystemReady}\n" +
                  $"Active Camera: {(activeCamera != null ? activeCamera.name : "null")}\n" +
                  $"Registered Cameras: {cameras.Count}\n" +
                  $"Brain: {(brain != null ? brain.name : "null")}");

        for (int i = 0; i < cameras.Count; i++)
        {
            var cam = cameras[i];
            Debug.Log($"Camera [{i}]: {(cam != null ? cam.name : "null")} - Priority: {(cam != null ? cam.Priority : -1)} - Active: {(cam != null ? cam.gameObject.activeInHierarchy : false)}");
        }
    }

    [ContextMenu("Register All Cameras")]
    public void DebugRegisterAllCameras()
    {
        RegisterAllCamerasInScene();
    }

    [ContextMenu("Switch to First Camera")]
    public void DebugSwitchToFirstCamera()
    {
        if (cameras.Count > 0)
        {
            SwitchCamera(cameras[0]);
        }
        else
        {
            Debug.Log("Nessuna camera disponibile");
        }
    }

    [ContextMenu("Force Initialize")]
    public void DebugForceInitialize()
    {
        ForceInitialize();
    }

    void LateUpdate()
    {
        if (isInitialized && activeCamera == null && cameras.Count > 0)
        {
            DebugLog("[CameraManager] Camera persa, ripristinando automaticamente...");
            SwitchCamera(cameras[0]);
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log(message);
            OnScreenDebugLogger.LogCamera(message);
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        // FIX: Ferma tutte le coroutine quando disabilitato
        if (initializationCoroutine != null)
        {
            StopCoroutine(initializationCoroutine);
            initializationCoroutine = null;
        }
        
#if !UNITY_EDITOR
        if (buildCheckCoroutine != null)
        {
            StopCoroutine(buildCheckCoroutine);
            buildCheckCoroutine = null;
        }
#endif
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // FIX: Usa gameObject.scene invece di GetActiveScene
        if (gameObject.scene != scene)
        {
            DebugLog($"[CameraManager] Ignoro evento per scena diversa: {scene.name}");
            return;
        }
        
        DebugLog($"[CameraManager] Scena caricata: {scene.name}");

        if (activeCamera != null && gameObject.activeInHierarchy && enabled)
        {
            StartCoroutine(NotifyControllersAfterSceneLoad());
        }
    }

    private IEnumerator NotifyControllersAfterSceneLoad()
    {
        yield return new WaitForSeconds(0.5f);
        ForceNotifyAllControllers();
        DebugLog("[CameraManager] Controller notificati dopo caricamento scena");
    }

    public void SetPreferredCamera(CinemachineCamera camera)
    {
        if (camera != null)
        {
            Register(camera);
            SwitchCamera(camera);
            DebugLog($"[CameraManager] Camera preferita impostata: {camera.name}");
        }
    }

    void OnDestroy()
    {
        DebugLog($"[CameraManager] {gameObject.name} distrutto - cleanup...");
        
        // FIX: Ferma tutte le coroutine e invocazioni
        StopAllCoroutines();
        CancelInvoke();
        
        if (cameras != null) cameras.Clear();
        if (originalPriorities != null) originalPriorities.Clear();
        
        activeCamera = null;
        brain = null;
        
        isInitialized = false;
        isCameraSystemReady = false;
        isInitializing = false;
        
        initializationCoroutine = null;
    }
}