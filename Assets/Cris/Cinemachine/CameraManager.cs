using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.SceneManagement; // AGGIUNGI QUESTA RIGA

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
    [Header("ThirdPersonController Integration")]
[SerializeField] private bool autoNotifyControllers = true;

    [Header("Transition Settings")]
    public CameraTransition defaultTransition = new CameraTransition();

    [Header("Auto Setup")]
    [SerializeField] private bool autoRegisterOnStart = true;
    [SerializeField] private bool findStartingCameraAutomatically = true;

    [Header("Build-Specific Settings")]
    [SerializeField] private bool useExtendedBuildWait = true;
    [SerializeField] private float buildExtraWaitTime = 1f;

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

        foreach (var vcam in allVCams)
        {
            if (vcam.gameObject.scene != currentScene)
            {
                DebugLog($"[Cleanup] Disattivando camera da scena precedente: {vcam.name}");
                vcam.gameObject.SetActive(false);
                vcam.Priority = -1000; // Priorità bassissima
            }
        }

        // NUOVO: Cleanup iniziale per evitare riferimenti da scene precedenti
        cameras.Clear();
        originalPriorities.Clear();
        activeCamera = null;
        brain = null;
        isInitialized = false;
        isInitializing = false;
        isCameraSystemReady = false;

        if (autoRegisterOnStart)
        {
            // Aspetta un frame per assicurarsi che il GameObject sia completamente attivo
            Invoke(nameof(StartInitialization), 0.1f);
        }

#if !UNITY_EDITOR
    Invoke(nameof(StartBuildCheck), 0.2f);
#endif
    }
private void NotifyControllersOfCameraChange(CinemachineCamera newCamera)
{
    if (!autoNotifyControllers) return;
    
    // Trova tutti i ThirdPersonController nella scena
    ThirdPersonController[] controllers = FindObjectsByType<ThirdPersonController>(FindObjectsSortMode.None);
    
    foreach (var controller in controllers)
    {
        if (controller.GetAutoDetectCamera()) // Verifica se ha auto-detect abilitato
        {
            controller.ForceUpdateActiveCamera();
        }
    }
    
    DebugLog($"[CameraManager] Notificati {controllers.Length} controller del cambio camera: {newCamera.name}");
}

    // AGGIUNGI QUESTI METODI
    private void StartInitialization()
    {
        if (gameObject.activeInHierarchy && enabled)
        {
            StartCoroutine(InitializeCameraManagerCoroutine());
        }
        else
        {
            // Riprova dopo un altro frame
            Invoke(nameof(StartInitialization), 0.1f);
        }
    }
#if !UNITY_EDITOR
private void StartBuildCheck()
{
    if (gameObject.activeInHierarchy && enabled)
    {
        StartCoroutine(BuildSpecificCameraCheck());
    }
    else
    {
        // Riprova dopo un altro frame
        Invoke(nameof(StartBuildCheck), 0.1f);
    }
}
#endif
    // INIZIALIZZAZIONE PRINCIPALE
    private IEnumerator InitializeCameraManagerCoroutine()
    {
        if (isInitializing) yield break;

        isInitializing = true;
        DebugLog("[SimpleCameraManager] Inizializzazione camera system avviata");

        // Attesa extra per le build
        #if !UNITY_EDITOR
        if (useExtendedBuildWait)
        {
            yield return new WaitForSeconds(buildExtraWaitTime);
        }
        #endif

        // Aspetta che la scena sia completamente caricata
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        // 1. Trova il CinemachineBrain
        yield return FindCinemachineBrain();

        // 2. Registra tutte le camere nella scena
        RegisterAllCamerasInScene();

        // 3. Imposta la camera iniziale
        yield return SetupStartingCamera();

        // 4. Finalizza inizializzazione
        isInitialized = true;
        isCameraSystemReady = true;
        isInitializing = false;

        DebugLog("[SimpleCameraManager] Camera system completamente inizializzato");
        
        OnCameraSystemInitialized?.Invoke();
        OnCameraSystemReady?.Invoke();
        
        // Verifica finale per le build
        #if !UNITY_EDITOR
        yield return new WaitForSeconds(0.5f);
        yield return VerifyCameraIsWorking();
        #endif
    }

    // CHECK SPECIFICO PER BUILD
    #if !UNITY_EDITOR
    private IEnumerator BuildSpecificCameraCheck()
    {
        // Attesa più lunga nelle build
        yield return new WaitForSeconds(3f);
        
        DebugLog("[Build Check] Verifica stato camera system...");
        
        // Verifica 1: Sistema generale
        if (!isCameraSystemReady)
        {
            DebugLog("[Build Check] Sistema camera non pronto, forzando inizializzazione");
            yield return InitializeCameraManagerCoroutine();
        }
        
        // Verifica 2: Camera attiva
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
        
        // Verifica 3: Brain connectivity
        if (brain != null && activeCamera != null)
        {
            var brainCamera = brain.ActiveVirtualCamera as CinemachineCamera;
            if (brainCamera != activeCamera)
            {
                DebugLog($"[Build Check] Brain mismatch! Forzando: {activeCamera.name}");
                activeCamera.Priority = activePriority + 200; // Priorità extra alta
            }
        }
        
        // Verifica finale
        yield return new WaitForSeconds(1f);
        
        if (activeCamera != null)
        {
            DebugLog($"[Build Check] Sistema confermato - Active: {activeCamera.name}");
        }
        else
        {
            DebugLog("[Build Check] Sistema non funzionante!");
        }
    }
    #endif

    // TROVA CINEMACHINE BRAIN
private IEnumerator FindCinemachineBrain()
{
    brain = null; // NUOVO: Reset esplicito
    var currentScene = SceneManager.GetActiveScene(); // NUOVO: Riferimento scena corrente
    int attempts = 0;
    const int maxAttempts = 15;

    while (brain == null && attempts < maxAttempts)
    {
        brain = Camera.main?.GetComponent<CinemachineBrain>();

        // NUOVO: Verifica che il brain sia nella scena corrente
        if (brain != null && brain.gameObject.scene != currentScene)
        {
            brain = null;
        }

        if (brain == null)
        {
            var allBrains = Object.FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None);
            // MODIFICATO: Cerca solo brain nella scena corrente
            foreach (var brainCandidate in allBrains)
            {
                if (brainCandidate.gameObject.scene == currentScene)
                {
                    brain = brainCandidate;
                    break;
                }
            }
        }

        if (brain == null)
        {
            attempts++;
            DebugLog($"[SimpleCameraManager] CinemachineBrain non trovato nella scena {currentScene.name}, tentativo {attempts}/{maxAttempts}");
            yield return new WaitForSeconds(0.2f);
        }
    }

    if (brain != null)
    {
        DebugLog($"[SimpleCameraManager] CinemachineBrain trovato nella scena: {currentScene.name}");
    }
    else
    {
        DebugLog($"[SimpleCameraManager] CinemachineBrain non trovato nella scena {currentScene.name}!");
    }
}

    public void RegisterAllCamerasInScene()
{
    var currentScene = SceneManager.GetActiveScene(); // AGGIUNGI QUESTA RIGA
    var allCameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.InstanceID);

    DebugLog($"[SimpleCameraManager] Scansione camere nella scena {currentScene.name}: {allCameras.Length} trovate");

    int registered = 0;
    foreach (var cam in allCameras)
    {
        // AGGIUNGI QUESTO CONTROLLO
        if (cam.gameObject.scene == currentScene)
        {
            Register(cam);
            registered++;
        }
    }

    DebugLog($"[SimpleCameraManager] {registered} camere registrate nella scena {currentScene.name}");
}

    // SWITCH CAMERA PRINCIPALE
    public void SwitchCamera(CinemachineCamera newCamera)
    {
        if (newCamera == null)
        {
            DebugLog("[SimpleCameraManager] SwitchCamera: newCamera is null!");
            return;
        }

        if (newCamera == activeCamera)
        {
            DebugLog($"[SimpleCameraManager] Camera {newCamera.name} è già attiva");
            return;
        }

        CinemachineCamera previousCamera = activeCamera;
        DebugLog($"[SimpleCameraManager] Cambio camera: da {(activeCamera != null ? activeCamera.name : "nessuna")} a {newCamera.name}");

        if (!cameras.Contains(newCamera))
        {
            Register(newCamera);
        }

        if (!newCamera.gameObject.activeInHierarchy)
        {
            DebugLog($"[SimpleCameraManager] Camera {newCamera.name} non è attiva, attivandola...");
            newCamera.gameObject.SetActive(true);
        }

        if (defaultTransition != null && brain != null)
        {
            brain.DefaultBlend.Time = defaultTransition.blendDuration;
            brain.DefaultBlend.Style = defaultTransition.blendStyle;
        }

        // GESTIONE PRIORITÀ DIFFERENZIATA PER BUILD
#if !UNITY_EDITOR
          // Nelle build: priorità molto alta e disattivazione completa delle altre
         foreach (CinemachineCamera cam in cameras.Where(c => c != newCamera && c != null))
         {
              cam.Priority = basePriority - 10; // Priorità molto bassa
         }
             newCamera.Priority = activePriority + 200; // Priorità molto alta
#else
        // Nell'editor: comportamento normale
        newCamera.Priority = activePriority;
        foreach (CinemachineCamera cam in cameras.Where(c => c != newCamera && c != null))
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

        // VERIFICA IMMEDIATA PER BUILD - MODIFICATO
#if !UNITY_EDITOR
        if (gameObject.activeInHierarchy && enabled)
        {
           StartCoroutine(VerifyCameraSwitchCoroutine(newCamera));
        }
#endif
        if (gameObject.activeInHierarchy && enabled)
        {
            // Delay leggermente la notifica per assicurarsi che il cambio sia completato
            StartCoroutine(DelayedControllerNotification(newCamera));
        }

    }
    private IEnumerator DelayedControllerNotification(CinemachineCamera newCamera)
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForFixedUpdate();
        NotifyControllersOfCameraChange(newCamera);
    }
// Metodo pubblico per forzare la notifica
public void ForceNotifyAllControllers()
{
    if (activeCamera != null)
    {
        NotifyControllersOfCameraChange(activeCamera);
    }
}

    // VERIFICA CAMBIO CAMERA PER BUILD
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
            
            // Forza di nuovo con priorità ancora più alta
            expectedCamera.Priority = activePriority + 300;
            activeCamera = expectedCamera;
            
            // Disattiva completamente le altre camere
            foreach (var cam in cameras.Where(c => c != expectedCamera && c != null))
            {
                cam.gameObject.SetActive(false);
            }
            
            yield return new WaitForSeconds(0.5f);
            
            // Riattiva le altre con priorità bassa
            foreach (var cam in cameras.Where(c => c != expectedCamera && c != null))
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

    // VERIFICA CHE LA CAMERA SIA EFFETTIVAMENTE FUNZIONANTE
    private IEnumerator VerifyCameraIsWorking()
    {
        if (activeCamera == null) yield break;
        
        DebugLog($"[SimpleCameraManager] Verifica funzionamento camera: {activeCamera.name}");
        
        if (!activeCamera.gameObject.activeInHierarchy)
        {
            activeCamera.gameObject.SetActive(true);
            DebugLog($"[SimpleCameraManager] Camera {activeCamera.name} riattivata");
        }
        
        if (activeCamera.Priority < activePriority)
        {
            activeCamera.Priority = activePriority + 100;
            DebugLog($"[SimpleCameraManager] Priorità camera corretta: {activeCamera.Priority}");
        }
        
        yield return new WaitForSeconds(0.3f);
        
        if (brain != null)
        {
            var currentBrainCamera = brain.ActiveVirtualCamera as CinemachineCamera;
            if (currentBrainCamera != activeCamera)
            {
                DebugLog($"[SimpleCameraManager] Brain mismatch! Brain: {currentBrainCamera?.name ?? "null"}, Active: {activeCamera.name}");
                activeCamera.Priority = activePriority + 500;
            }
            else
            {
                DebugLog($"[SimpleCameraManager] Camera verification OK: {activeCamera.name}");
            }
        }
    }

    // REGISTRA UNA CAMERA
    public void Register(CinemachineCamera camera)
    {
        if (camera == null) return;

        if (cameras.Contains(camera))
        {
            DebugLog($"[SimpleCameraManager] Camera {camera.name} già registrata");
            return;
        }

        cameras.Add(camera);

        if (!originalPriorities.ContainsKey(camera))
            originalPriorities[camera] = camera.Priority;

        camera.Priority = basePriority;

        DebugLog($"[SimpleCameraManager] Camera registrata: {camera.name} (Priority: {camera.Priority})");
    }

   // DEREGISTRA UNA CAMERA
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

    DebugLog($"[SimpleCameraManager] Camera deregistrata: {camera.name}");
}

    // SETUP CAMERA INIZIALE
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
                    DebugLog($"[SimpleCameraManager] Starting camera trovata automaticamente: {targetCamera.name}");
                    break;
                }
            }

            if (targetCamera == null)
            {
                targetCamera = cameras[0];
                DebugLog($"[SimpleCameraManager] Usando prima camera disponibile: {targetCamera.name}");
            }
        }

        if (targetCamera != null)
        {
            SwitchCamera(targetCamera);
            yield return new WaitForSeconds(0.2f);
        NotifyControllersOfCameraChange(targetCamera);
        }
        else
        {
            DebugLog("[SimpleCameraManager] Nessuna camera disponibile!");
        }

        yield return null;
    }

    // METODI PUBBLICI DI UTILITY
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
            DebugLog($"[SimpleCameraManager] Camera '{cameraName}' non trovata!");
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
        if (!isInitializing)
        {
            StartCoroutine(InitializeCameraManagerCoroutine());
        }
    }

    public void EnsureCamerasAreReady()
    {
        if (!isCameraSystemReady)
        {
            DebugLog("[SimpleCameraManager] Sistema non pronto - forzando inizializzazione");
            if (!isInitializing)
            {
                ForceInitialize();
            }
            return;
        }

        if (activeCamera == null && cameras.Count > 0)
        {
            DebugLog("[SimpleCameraManager] Nessuna camera attiva - attivando la prima disponibile");
            SwitchCamera(cameras[0]);
        }
        else if (activeCamera != null)
        {
            DebugLog($"[SimpleCameraManager] Sistema OK - Active: {activeCamera.name}");
        }
        else
        {
            DebugLog("[SimpleCameraManager] Nessuna camera disponibile!");
        }
    }

    // METODI DEBUG
    [ContextMenu("Debug Camera State")]
    public void DebugCameraState()
    {
        Debug.Log($"=== Simple Camera Manager State ===\n" +
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

    // VERIFICA CONTINUA IN LATEUPDATE
    void LateUpdate()
    {
        if (isInitialized && activeCamera == null && cameras.Count > 0)
        {
            DebugLog("[SimpleCameraManager] Camera persa, ripristinando automaticamente...");
            SwitchCamera(cameras[0]);
        }
    }

 // DEBUG LOG - TROVA QUESTO METODO E SOSTITUISCILO
private void DebugLog(string message)
{
    if (enableDebugLogs)
    {
        Debug.Log(message);
        
        // AGGIUNGI SOLO QUESTA RIGA
        OnScreenDebugLogger.LogCamera(message);
    }
}
// GESTIONE CAMBIO SCENA
void OnEnable()
{
    // Registrati per gli eventi di cambio scena
    UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
}
    void OnDisable()
    {
        // Deregistrati dagli eventi
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        DebugLog($"[CameraManager] Scena caricata: {scene.name}");

        // Dopo il caricamento della scena, forza la notifica ai controller
        if (activeCamera != null)
        {
            StartCoroutine(NotifyControllersAfterSceneLoad());
        }
    }
private IEnumerator NotifyControllersAfterSceneLoad()
{
    // Aspetta che tutti i GameObject siano inizializzati
    yield return new WaitForSeconds(0.5f);
    
    // Forza l'aggiornamento di tutti i controller
    ForceNotifyAllControllers();
    
    DebugLog("[CameraManager] Controller notificati dopo caricamento scena");
}
    // CLEANUP
    void OnDestroy()
    {
        DebugLog($"[SimpleCameraManager] {gameObject.name} distrutto - cleanup...");
        
        if (cameras != null) cameras.Clear();
        if (originalPriorities != null) originalPriorities.Clear();
        
        activeCamera = null;
        brain = null;
        
        isInitialized = false;
        isCameraSystemReady = false;
        isInitializing = false;
    }
}