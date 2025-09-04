using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using System.Collections;
using UnityEngine.Events;

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

    [Header("Transition Settings")]
    public CameraTransition defaultTransition = new CameraTransition();

    [Header("Auto Setup")]
    [SerializeField] private bool autoRegisterOnStart = true;
    [SerializeField] private bool findStartingCameraAutomatically = true;

    [Header("🆕 Scene Coordination")]
    [SerializeField] private bool enableSceneCoordination = true;
    [SerializeField] private float maxSceneWaitTime = 10f;
    [SerializeField] private bool waitForSceneManagerReady = true; // NUOVO
    [SerializeField] private bool enableDebugLogs = true;

    // 🆕 EVENTI PER COORDINAMENTO SCENA
    [Header("🆕 Camera System Events")]
    public UnityEvent OnCameraSystemInitialized;
    public UnityEvent OnCameraSystemReady;
    public UnityEvent<CinemachineCamera> OnCameraActivated;
    public UnityEvent<CinemachineCamera, CinemachineCamera> OnCameraSwitched; // from, to

    private CinemachineBrain brain;
    // Stati per coordinamento scena MIGLIORATI
private bool isCameraSystemReady = false;
private bool isRegisteredWithSceneManager = false; // NUOVO
private bool sceneManagerFoundAndReady = false; // NUOVO
    private bool isInitialized = false;
    private bool isInitializing = false;
    
    // 🆕 STATI PER COORDINAMENTO SCENA
    private bool isWaitingForSceneManager = false;
    
public UnityEvent OnCameraSystemRegisteredWithScene; // NUOVO

    // Singleton per scena (NON statico, viene resettato ad ogni scena)
    public static CameraManager Instance { get; private set; }

    void Awake()
    {
        // Singleton per scena - si resetta automaticamente ad ogni cambio scena
        if (Instance == null)
        {
            Instance = this;
            DebugLog($"[CameraManager] Instance impostata: {gameObject.name}");
        }
        else
        {
            DebugLog($"[CameraManager] Istanza duplicata trovata: {gameObject.name}, verrà distrutta");
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (autoRegisterOnStart)
        {
            StartCoroutine(InitializeCameraManagerCoroutine());
        }

        // Check specifico per build
#if !UNITY_EDITOR
        StartCoroutine(BuildSpecificCameraCheck());
#endif
    }

    // Check specifico per le build
#if !UNITY_EDITOR
    private IEnumerator BuildSpecificCameraCheck()
    {
        yield return new WaitForSeconds(2f); // Attesa più lunga nelle build
        
        if (activeCamera == null && cameras.Count > 0)
        {
            DebugLog("[Build Fix] No active camera found, forcing initialization");
            
            // Forza re-registrazione
            RegisterAllCamerasInScene();
            
            // Attiva la prima camera disponibile
            if (cameras.Count > 0)
            {
                SwitchCamera(cameras[0]);
                DebugLog($"[Build Fix] Activated camera: {cameras[0].name}");
            }
        }
        else if (activeCamera != null)
        {
            DebugLog($"[Build Fix] Camera system OK - Active: {activeCamera.name}");
        }
    }
#endif

    // 🆕 METODO PRINCIPALE DI INIZIALIZZAZIONE COORDINATA
    private IEnumerator InitializeCameraManagerCoroutine()
    {
        if (isInitializing) yield break;

        isInitializing = true;
        DebugLog("[CameraManager] 🚀 Inizializzazione camera system avviata");

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

        // 🆕 4. COORDINAMENTO CON SCENEMANAGER
        if (enableSceneCoordination)
        {
            yield return CoordinateWithSceneManager();
        }

        // 5. Finalizza inizializzazione
        isInitialized = true;
        isCameraSystemReady = true;
        isInitializing = false;

        DebugLog("[CameraManager] ✅ Camera system completamente inizializzato e pronto");
        
        // 🆕 NOTIFICA EVENTI
        OnCameraSystemInitialized?.Invoke();
        OnCameraSystemReady?.Invoke();

        // 🆕 NOTIFICA AL SCENEMANAGER CHE LE CAMERE SONO PRONTE
        NotifySceneManagerCamerasReady();
    }

   private IEnumerator CoordinateWithSceneManager()
{
    DebugLog("[CameraManager] Avvio coordinamento migliorato con SceneManager...");
    
    float waitTimer = 0f;
    SceneManager00 sceneManagerInstance = null;
    
    // Aspetta che SceneManager00 sia disponibile E inizializzato
    while (waitTimer < maxSceneWaitTime && !sceneManagerFoundAndReady)
    {
        sceneManagerInstance = SceneManager00.Instance;
        
        if (sceneManagerInstance != null)
        {
            // Verifica che lo SceneManager sia effettivamente pronto
            if (sceneManagerInstance.AreAllManagersReady())
            {
                sceneManagerFoundAndReady = true;
                DebugLog("[CameraManager] SceneManager00 trovato E pronto - coordinamento attivo");
                break;
            }
            else
            {
                DebugLog($"[CameraManager] SceneManager00 trovato ma non ancora pronto... ({waitTimer:F1}s)");
            }
        }
        
        waitTimer += 0.1f;
        yield return new WaitForSeconds(0.1f);
    }
    
    if (sceneManagerFoundAndReady)
    {
        // Informa lo SceneManager che stiamo inizializzando
        NotifySceneManagerCameraInitializing(sceneManagerInstance);
        
        // Attendi un momento per permettere eventuali configurazioni dallo SceneManager
        yield return new WaitForSeconds(0.2f);
        
        // Registrati con lo SceneManager
        RegisterWithSceneManager(sceneManagerInstance);
    }
    else if (waitForSceneManagerReady)
    {
        DebugLog($"[CameraManager] SceneManager00 non pronto dopo {maxSceneWaitTime}s - continuo con inizializzazione base");
    }
}
private void RegisterWithSceneManager(SceneManager00 sceneManager)
{
    if (sceneManager == null) return;

    try
    {
        sceneManager.SendMessage("RegisterCameraManager", this, SendMessageOptions.DontRequireReceiver);
        isRegisteredWithSceneManager = true;
        
        DebugLog("[CameraManager] Registrato con SceneManager");
        OnCameraSystemRegisteredWithScene?.Invoke();
    }
    catch (System.Exception e)
    {
        DebugLog($"[CameraManager] Errore registrazione con SceneManager: {e.Message}");
    }
}

private void NotifySceneManagerCameraInitializing(SceneManager00 sceneManager)
{
    if (sceneManager == null) return;

    try
    {
        sceneManager.SendMessage("OnCameraManagerInitializing", this, SendMessageOptions.DontRequireReceiver);
        DebugLog("[CameraManager] SceneManager notificato: inizializzazione camere");
    }
    catch (System.Exception e)
    {
        DebugLog($"[CameraManager] Errore notifica inizializzazione: {e.Message}");
    }
}

public void OnSceneManagerReady()
{
    DebugLog("[CameraManager] Ricevuto: SceneManager pronto");
    sceneManagerFoundAndReady = true;

    if (!isInitialized && !isInitializing)
    {
        StartCoroutine(InitializeCameraManagerCoroutine());
    }
    else if (isInitialized)
    {
        StartCoroutine(FinalCameraVerification());
    }
}
    private IEnumerator FinalCameraVerification()
    {
        DebugLog("[CameraManager] 🔍 Verifica finale sistema camere...");

        yield return new WaitForSeconds(0.3f);

        bool allGood = true;
        string issues = "";

        // Verifica camera attiva
        if (activeCamera == null)
        {
            allGood = false;
            issues += "- Nessuna camera attiva\n";

            // Tentativo di fix automatico
            if (cameras.Count > 0)
            {
                SwitchCamera(cameras[0]);
                DebugLog("[CameraManager] 🔧 Fix applicato: attivata prima camera disponibile");
            }
        }

        // Verifica brain
        if (brain == null)
        {
            allGood = false;
            issues += "- CinemachineBrain non trovato\n";
        }

        // Verifica priorità camera attiva
        if (activeCamera != null && activeCamera.Priority < activePriority)
        {
            activeCamera.Priority = activePriority;
            DebugLog("[CameraManager] 🔧 Fix applicato: priorità camera corretta");
        }

        if (allGood)
        {
            DebugLog("[CameraManager] ✅ Verifica finale SUPERATA - sistema completamente funzionale");
        }
        else
        {
            DebugLog($"[CameraManager] ⚠️ Problemi rilevati nella verifica finale:\n{issues}");
        }
    }
public bool IsRegisteredWithSceneManager()
{
    return isRegisteredWithSceneManager;
}

public bool IsSceneManagerReady()
{
    return sceneManagerFoundAndReady;
}
public void ConfigureForScene(CinemachineCamera preferredCamera = null, string sceneName = "")
{
    DebugLog($"[CameraManager] Configurazione per scena: {sceneName}");

    if (preferredCamera != null)
    {
        if (!cameras.Contains(preferredCamera))
        {
            Register(preferredCamera);
        }
        SwitchCamera(preferredCamera);
        DebugLog($"[CameraManager] Camera preferita attivata: {preferredCamera.name}");
    }
    else if (activeCamera == null && cameras.Count > 0)
    {
        SwitchCamera(cameras[0]);
        DebugLog("[CameraManager] Camera di fallback attivata");
    }
}
    // 🆕 VERIFICA CHE LA CAMERA SIA EFFETTIVAMENTE FUNZIONANTE
    private IEnumerator VerifyCameraIsWorking()
    {
        if (activeCamera == null) yield break;
        
        DebugLog($"[CameraManager] 🔍 Verifica funzionamento camera: {activeCamera.name}");
        
        // Assicurati che la camera sia attiva nel GameObject
        if (!activeCamera.gameObject.activeInHierarchy)
        {
            activeCamera.gameObject.SetActive(true);
            DebugLog($"[CameraManager] 🔧 Camera {activeCamera.name} riattivata");
        }
        
        // Verifica priorità
        if (activeCamera.Priority < activePriority)
        {
            activeCamera.Priority = activePriority;
            DebugLog($"[CameraManager] 🔧 Priorità camera corretta: {activePriority}");
        }
        
        yield return new WaitForSeconds(0.2f);
        
        // Verifica finale con il brain
        if (brain != null)
        {
            var currentBrainCamera = brain.ActiveVirtualCamera as CinemachineCamera;
            if (currentBrainCamera != activeCamera)
            {
                DebugLog($"[CameraManager] ⚠️ Brain camera mismatch! Brain: {currentBrainCamera?.name ?? "null"}, Active: {activeCamera.name}");
                // Forza nuovamente la camera
                SwitchCamera(activeCamera);
            }
            else
            {
                DebugLog($"[CameraManager] ✅ Camera verification OK: {activeCamera.name}");
            }
        }
    }

    // 🆕 ATTIVA CAMERA DI FALLBACK
    private IEnumerator ActivateFallbackCamera()
    {
        if (cameras.Count == 0)
        {
            DebugLog("[CameraManager] ❌ Nessuna camera disponibile per fallback!");
            yield break;
        }
        
        CinemachineCamera fallbackCamera = null;
        
        // Prova con la starting camera
        if (startingCamera != null && cameras.Contains(startingCamera))
        {
            fallbackCamera = startingCamera;
        }
        // Altrimenti prendi la prima disponibile
        else
        {
            fallbackCamera = cameras[0];
        }
        
        if (fallbackCamera != null)
        {
            DebugLog($"[CameraManager] 🚨 Attivazione camera di fallback: {fallbackCamera.name}");
            SwitchCamera(fallbackCamera);
            yield return new WaitForSeconds(0.2f);
        }
    }

    // 🆕 NOTIFICA AL SCENEMANAGER
    private void NotifySceneManagerCamerasReady()
    {
        if (SceneManager00.Instance != null)
        {
            // Prova prima con SendMessage per compatibilità
            SceneManager00.Instance.SendMessage("OnCameraManagerReady", this, SendMessageOptions.DontRequireReceiver);
            
            DebugLog("[CameraManager] 📢 SceneManager00 notificato che le camere sono pronte");
        }
        else
        {
            DebugLog("[CameraManager] ⚠️ SceneManager00 non disponibile per notifica");
        }
    }

    // 🆕 METODO PUBBLICO PER ASSICURARE CHE LE CAMERE SIANO PRONTE
    public void EnsureCamerasAreReady()
    {
        if (!isCameraSystemReady)
        {
            DebugLog("[CameraManager] ⏳ Camera system non ancora pronto - forzando inizializzazione");
            if (!isInitializing)
            {
                ForceInitialize();
            }
            return;
        }
        
        if (activeCamera == null && cameras.Count > 0)
        {
            DebugLog("[CameraManager] 🔧 Nessuna camera attiva - attivando la prima disponibile");
            SwitchCamera(cameras[0]);
        }
        else if (activeCamera != null)
        {
            DebugLog($"[CameraManager] ✅ Camera system OK - Active: {activeCamera.name}");
        }
        else
        {
            DebugLog("[CameraManager] ❌ Nessuna camera disponibile!");
        }
    }

    // 🆕 METODO PER VERIFICARE SE IL SISTEMA È PRONTO
    public bool IsCameraSystemReady()
    {
        return isCameraSystemReady && activeCamera != null && isInitialized;
    }

    // 🆕 METODO PER ATTENDERE CHE IL SISTEMA SIA PRONTO
    public IEnumerator WaitForCameraSystemReady()
    {
        float waitTime = 0f;
        while (!IsCameraSystemReady() && waitTime < maxSceneWaitTime)
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }
        
        if (IsCameraSystemReady())
        {
            DebugLog("[CameraManager] ✅ Camera system ready confirmed");
        }
        else
        {
            DebugLog($"[CameraManager] ⚠️ Camera system ready timeout after {maxSceneWaitTime}s");
        }
    }

    private IEnumerator FindCinemachineBrain()
    {
        int attempts = 0;
        const int maxAttempts = 10;

        while (brain == null && attempts < maxAttempts)
        {
            brain = Camera.main?.GetComponent<CinemachineBrain>();

            if (brain == null)
            {
                var allBrains = Object.FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None);
                if (allBrains.Length > 0)
                {
                    brain = allBrains[0];
                }
            }

            if (brain == null)
            {
                attempts++;
                DebugLog($"[CameraManager] CinemachineBrain non trovato, tentativo {attempts}/{maxAttempts}");
                yield return new WaitForSeconds(0.1f);
            }
        }

        if (brain != null)
        {
            DebugLog($"[CameraManager] CinemachineBrain trovato: {brain.name}");
        }
        else
        {
            DebugLog("[CameraManager] CinemachineBrain non trovato!");
        }
    }

    private IEnumerator SetupStartingCamera()
    {
        CinemachineCamera targetCamera = startingCamera;

        // Auto-trova la starting camera se non assegnata
        if (targetCamera == null && findStartingCameraAutomatically && cameras.Count > 0)
        {
            string[] preferredNames = { "PlayerOrbitalCamera", "PlayerCamera", "MainCamera" };

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
        }
        else
        {
            DebugLog("[CameraManager] Nessuna camera disponibile!");
        }

        yield return null;
    }

    public void RegisterAllCamerasInScene()
    {
        var allCameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.InstanceID);

        DebugLog($"[CameraManager] Trovate {allCameras.Length} camere nella scena");

        foreach (var cam in allCameras)
        {
            if (cam.gameObject.scene == gameObject.scene)
            {
                Register(cam);
            }
        }

        DebugLog($"[CameraManager] {cameras.Count} camere registrate");
    }

    public bool IsActiveCamera(CinemachineCamera camera)
    {
        return camera == activeCamera;
    }

    // 🆕 METODO SWITCHCAMERA MIGLIORATO CON EVENTI
    public void SwitchCamera(CinemachineCamera newCamera)
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

        CinemachineCamera previousCamera = activeCamera;
        DebugLog($"[CameraManager] Cambio camera: da {(activeCamera != null ? activeCamera.name : "nessuna")} a {newCamera.name}");

        // Verifica se la camera è registrata
        if (!cameras.Contains(newCamera))
        {
            Register(newCamera);
        }

        // Assicurati che la camera sia effettivamente attiva
        if (!newCamera.gameObject.activeInHierarchy)
        {
            DebugLog($"[CameraManager] Camera {newCamera.name} non è attiva, attivandola...");
            newCamera.gameObject.SetActive(true);
        }

        // Configura transizione
        if (defaultTransition != null && brain != null)
        {
            brain.DefaultBlend.Time = defaultTransition.blendDuration;
            brain.DefaultBlend.Style = defaultTransition.blendStyle;
        }

        // Switch diverso per build vs editor
#if !UNITY_EDITOR
        // Prima disattiva tutte le altre camere (nelle build)
        foreach (CinemachineCamera cam in cameras.Where(c => c != newCamera && c != null))
        {
            cam.Priority = basePriority;
        }
        
        // Attiva la nuova camera con priorità extra alta nelle build
        newCamera.Priority = activePriority + 50;
#else
        // Nell'editor: comportamento normale
        newCamera.Priority = activePriority;
#endif

        activeCamera = newCamera;

        // Disattiva tutte le altre camere (se non già fatto nell'editor)
#if UNITY_EDITOR
        foreach (CinemachineCamera cam in cameras.Where(c => c != newCamera && c != null))
        {
            int priority = originalPriorities.ContainsKey(cam) ? originalPriorities[cam] : basePriority;
            cam.Priority = priority;
        }
#endif

        LogRegisteredCameras();

        // 🆕 NOTIFICA EVENTI DI CAMBIO CAMERA
        OnCameraActivated?.Invoke(newCamera);
        if (previousCamera != null)
        {
            OnCameraSwitched?.Invoke(previousCamera, newCamera);
        }

        // Notifica i controller del cambio camera
        NotifyControllersOfCameraChange();

        // Verifica finale per le build
#if !UNITY_EDITOR
        StartCoroutine(VerifyCameraSwitchCoroutine(newCamera));
#endif
    }

    // Verifica che il cambio camera sia avvenuto correttamente
#if !UNITY_EDITOR
    private IEnumerator VerifyCameraSwitchCoroutine(CinemachineCamera expectedCamera)
    {
        yield return new WaitForSeconds(1f);
        
        if (activeCamera != expectedCamera)
        {
            DebugLog($"[Build] Camera switch verification failed! Expected: {expectedCamera.name}, Got: {(activeCamera != null ? activeCamera.name : "null")}");
            
            // Forza nuovamente il cambio
            expectedCamera.Priority = activePriority + 100;
            activeCamera = expectedCamera;
        }
        else
        {
            DebugLog($"[Build] Camera switch verified: {expectedCamera.name}");
        }
    }
#endif

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
            if (cameras.Count > 0)
            {
                SwitchCamera(cameras[0]);
            }
        }

        DebugLog($"[CameraManager] Camera deregistrata: {camera.name}");
    }

    public CinemachineCamera GetCameraByName(string name)
    {
        return cameras.FirstOrDefault(cam => cam != null && cam.name == name);
    }

    public List<CinemachineCamera> GetAllCameras()
    {
        // Rimuovi camere null prima di restituire la lista
        cameras.RemoveAll(cam => cam == null);
        return new List<CinemachineCamera>(cameras);
    }

    public CinemachineCamera GetActiveCamera()
    {
        return activeCamera;
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

    public bool IsInitialized()
    {
        return isInitialized;
    }

    public void ForceInitialize()
    {
        if (!isInitializing)
        {
            StartCoroutine(InitializeCameraManagerCoroutine());
        }
    }

    private void LogRegisteredCameras()
    {
        for (int i = 0; i < cameras.Count; i++)
        {
            var cam = cameras[i];
            DebugLog($"[CameraManager] Camera [{i}]: {(cam != null ? cam.name : "null")} - Priority: {(cam != null ? cam.Priority : -1)}");
        }
    }

    private void NotifyControllersOfCameraChange()
    {
        // Trova tutti i controller e notifica del cambio camera
        var controllers = Object.FindObjectsByType<ThirdPersonController>(FindObjectsSortMode.None);
        foreach (var controller in controllers)
        {
            if (controller != null)
            {
                // Assumendo che ThirdPersonController abbia un metodo per gestire il cambio camera
                controller.SendMessage("OnCameraChanged", activeCamera, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    // ========== METODI PER COMPATIBILITÀ CON SCENEMANAGER ==========

    public void InitializeForNewScene(CinemachineCamera preferredStartingCamera = null)
    {
        DebugLog("[CameraManager] Inizializzazione per nuova scena");

        if (preferredStartingCamera != null)
        {
            startingCamera = preferredStartingCamera;
        }

        if (!isInitialized && !isInitializing)
        {
            ForceInitialize();
        }
        else
        {
            // Re-registra tutte le camere e imposta quella iniziale
            RegisterAllCamerasInScene();

            if (startingCamera != null && cameras.Contains(startingCamera))
            {
                SwitchCamera(startingCamera);
            }
            else if (cameras.Count > 0)
            {
                SwitchCamera(cameras[0]);
            }
        }
    }

    // 🆕 METODI PUBBLICI PER SCENEMANAGER
    public void SetSceneCoordinationEnabled(bool enabled)
    {
        enableSceneCoordination = enabled;
        DebugLog($"[CameraManager] Scene coordination {(enabled ? "abilitato" : "disabilitato")}");
    }

    public void SetMaxSceneWaitTime(float waitTime)
    {
        maxSceneWaitTime = waitTime;
        DebugLog($"[CameraManager] Max scene wait time: {waitTime}s");
    }

    // ========== DEBUG ==========

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log(message);
        }
    }

    public void SetDebugLogsEnabled(bool enabled)
    {
        enableDebugLogs = enabled;
    }

    [ContextMenu("Debug Camera State")]
    public void DebugCameraState()
    {
        Debug.Log($"=== Camera Manager State ===\n" +
                  $"Initialized: {isInitialized}\n" +
                  $"Initializing: {isInitializing}\n" +
                  $"System Ready: {isCameraSystemReady}\n" +
                  $"Active Camera: {(activeCamera != null ? activeCamera.name : "null")}\n" +
                  $"Starting Camera: {(startingCamera != null ? startingCamera.name : "null")}\n" +
                  $"Registered Cameras: {cameras.Count}\n" +
                  $"Brain: {(brain != null ? brain.name : "null")}\n" +
                  $"Scene Coordination: {enableSceneCoordination}\n" +
                  $"Scene: {gameObject.scene.name}");

        LogRegisteredCameras();
    }

    [ContextMenu("🆕 Test - Camera System Ready")]
    public void DebugTestCameraSystemReady()
    {
        Debug.Log($"Camera System Ready: {IsCameraSystemReady()}");
        if (!IsCameraSystemReady())
        {
            EnsureCamerasAreReady();
        }
    }

    [ContextMenu("Force Register All Cameras")]
    public void ForceRegisterAllCameras()
    {
        RegisterAllCamerasInScene();
    }

    [ContextMenu("Switch to First Camera")]
    public void SwitchToFirstCamera()
    {
        if (cameras.Count > 0)
        {
            SwitchCamera(cameras[0]);
        }
    }

    private void OnDestroy()
    {
        DebugLog($"[CameraManager] {gameObject.name} distrutto - cleanup...");

        if (Instance == this)
        {
            Instance = null;
        }

        // Cleanup delle liste
        cameras.Clear();
        originalPriorities.Clear();
        activeCamera = null;
        
        // Reset stati
        isInitialized = false;
        isCameraSystemReady = false;
    }

    void LateUpdate()
    {
        // Verifica continua nelle build
        if (isInitialized && activeCamera == null && cameras.Count > 0)
        {
            DebugLog("[CameraManager] Camera persa, ripristinando automaticamente...");
            SwitchCamera(cameras[0]);
        }
    }
}