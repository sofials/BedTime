using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using Unity.Cinemachine;

/// <summary>
/// SceneManager che gestisce anche le UI specifiche della scena
/// </summary>
public class SceneManager01 : MonoBehaviour
{
    [Header("Camera Events")]
public UnityEvent<CameraManager> OnCameraManagerRegistered;
public UnityEvent OnCameraSystemReady;
    [Header("Scene Configuration")]
    [SerializeField] private string sceneName = "01 - Party in Lukelandia";
    
    [Header("Manager References")]
    [SerializeField] private CheckpointManager checkpointManager;
    [SerializeField] private CollectiblesManager collectiblesManager;
    [Header("Camera Manager Integration")]
[SerializeField] private CameraManager cameraManager;
[SerializeField] private CinemachineCamera preferredSceneCamera;
[SerializeField] private bool waitForCameraManager = true;
    [SerializeField] private float cameraWaitTimeout = 10f;
// Stati camera
private bool cameraManagerReady = false;
private bool cameraSystemFullyInitialized = false;
    
    [Header("UI References - Specifiche della Scena")]
    [SerializeField] private GameObject levelTitleUI; // Opzionale
    [SerializeField] private PlayerAttack playerAttack; // Per accedere al PowerUp UI
    
    [Header("Abilities Management")]
    [SerializeField] private AbilitiesManager abilitiesManager;
    
    [Header("Auto-Setup")]
    [SerializeField] private bool autoFindManagers = true;
    [SerializeField] private bool createManagersIfMissing = true;
    [SerializeField] private bool autoFindUIElements = true;
    [SerializeField] private bool showLevelTitle = false; // Opzionale - mostra il titolo del livello
    
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    
    [Header("Scene Events")]
    public UnityEvent OnSceneInitialized;
    public UnityEvent OnSceneReady;
    public UnityEvent OnSceneCompleted;
    public UnityEvent OnUISetupComplete;
    
    // Singleton pattern
    public static SceneManager01 Instance { get; private set; }
    
    // Stato interno
    private bool sceneInitialized = false;
    private bool managersReady = false;
    private bool uiSetupComplete = false;
    
    private void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        DebugLog($"[SceneManager01] Inizializzazione per '{sceneName}'");
        
        // Ascolta gli eventi del GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnSceneReady += OnGameManagerSceneReady;
        }
    }
    
    private void Start()
    {
        StartCoroutine(InitializeSceneCoroutine());
    }
    
    private IEnumerator InitializeSceneCoroutine()
    {
        yield return null; // Aspetta un frame
        
        // 1. Setup dei manager
        SetupManagers();
        
        // 2. Setup UI
        SetupUI();
        
        // 3. Inizializza la scena
        InitializeScene();
        
        // 4. Connetti i manager
        ConnectManagers();
        
        // 5. Finalizza
        FinalizeSceneSetup();
    }
    
    // ========== SETUP UI ==========
    
    private void SetupUI()
    {
        DebugLog("[SceneManager01] Setup UI...");
        
        // Auto-trova elementi UI se abilitato
        if (autoFindUIElements)
        {
            FindUIElements();
        }
        
        // Configura stato iniziale UI
        ConfigureInitialUIState();
        
        uiSetupComplete = true;
        OnUISetupComplete?.Invoke();
        
        DebugLog("[SceneManager01] Setup UI completato");
    }
    
    private void FindUIElements()
    {
        // Trova elementi UI se non assegnati
        if (levelTitleUI == null && showLevelTitle)
        {
            levelTitleUI = GameObject.Find("LevelTitleUI");
        }
        
        if (playerAttack == null)
        {
            playerAttack = Object.FindFirstObjectByType<PlayerAttack>();
        }
        
        DebugLog($"[SceneManager01] UI trovate - Title: {levelTitleUI != null}, Player: {playerAttack != null}");
    }
    
    private void ConfigureInitialUIState()
    {
        if (levelTitleUI != null && showLevelTitle)
        {
            levelTitleUI.SetActive(false);
        }
        
        DebugLog("[SceneManager01] Stato iniziale UI configurato");
    }
    
    // ========== GESTIONE UI POWERUP ==========
    
    private void EnablePowerUpUI()
    {
        if (playerAttack != null && playerAttack.TryGetComponent<PlayerPowerUp>(out var powerUp))
        {
            if (powerUp.powerUI != null)
            {
                powerUp.powerUI.SetActive(true);
                DebugLog("[SceneManager01] UI PowerUp attivata");
            }
        }
        else
        {
            DebugLog("[SceneManager01] ⚠️ PlayerAttack o PowerUp component non trovato");
        }
    }

    private void DisablePowerUpUI()
    {
        if (playerAttack != null && playerAttack.TryGetComponent<PlayerPowerUp>(out var powerUp))
        {
            if (powerUp.powerUI != null)
            {
                powerUp.powerUI.SetActive(false);
                DebugLog("[SceneManager01] UI PowerUp disattivata");
            }
        }
    }

   /// <summary>
/// Chiamato quando il GameManager ha completato il setup della scena
/// </summary>
/// <summary>
/// MODIFICATO: Callback GameManager con verifica camere COORDINATA
/// </summary>
private void OnGameManagerSceneReady(string sceneName)
{
    if (sceneName == this.sceneName || sceneName == UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)
    {
        DebugLog("[SceneManager] GameManager pronto - verifica coordinamento camera");

        // NUOVO: Prima verifica che il CameraManager sia pronto
        CameraManager cameraManager = CameraManager.Instance;
        if (cameraManager != null && cameraManager.IsCameraSystemReady())
        {
            DebugLog("[SceneManager] ✅ CameraManager confermato pronto - attivazione UI di gioco");
            
            EnablePowerUpUI();

            if (levelTitleUI != null && showLevelTitle)
            {
                StartCoroutine(ShowLevelTitleCoroutine());
            }
        }
        else
        {
            DebugLog("[SceneManager] ⚠️ CameraManager non pronto - avvio verifica coordinata");
            StartCoroutine(EnsureCamerasAreReadyCoroutine());
        }
    }
}

    /// <summary>
    /// Metodo chiamato dal GameManager tramite SendMessage
    /// </summary>
    public void OnGameManagerReady()
    {
        DebugLog("[SceneManager01] GameManager pronto (via SendMessage)");
        OnGameManagerSceneReady(sceneName);
    }
    /// <summary>
/// NUOVO: Verifica che le camere siano pronte e funzionanti
/// </summary>
/// <summary>
/// SOSTITUISCI COMPLETAMENTE: Verifica che le camere siano pronte e funzionanti
/// </summary>
private IEnumerator EnsureCamerasAreReadyCoroutine()
{
    DebugLog("[SceneManager] Verifica stato camere...");
    
    yield return new WaitForSeconds(0.5f);
    
    // Usa il riferimento diretto al CameraManager
    if (cameraManager != null)
    {
        cameraManager.EnsureCamerasAreReady();
    }
    else
    {
        DebugLog("[SceneManager] ⚠️ CameraManager non disponibile");
    }
    
    yield return new WaitForSeconds(0.2f);
    
    // Verifica finale
    if (cameraManager != null && cameraManager.GetActiveCamera() != null)
    {
        DebugLog($"[SceneManager] ✅ Camera attiva confermata: {cameraManager.GetActiveCamera().name}");
    }
    else
    {
        DebugLog("[SceneManager] ⚠️ Nessuna camera attiva rilevata!");
        
        if (cameraManager != null)
        {
            var cameras = cameraManager.GetAllCameras();
            if (cameras.Count > 0)
            {
                cameraManager.SwitchCamera(cameras[0]);
                DebugLog($"[SceneManager] 🔧 Fix applicato - attivata: {cameras[0].name}");
            }
        }
    }
}
    private IEnumerator ShowLevelTitleCoroutine()
    {
        if (levelTitleUI != null)
        {
            levelTitleUI.SetActive(true);
            DebugLog("[SceneManager01] Level Title mostrato");
            
            // Mostra per qualche secondo poi nascondi
            yield return new WaitForSeconds(3f);
            
            levelTitleUI.SetActive(false);
            DebugLog("[SceneManager01] Level Title nascosto");
        }
    }

    // ========== SETUP MANAGER ==========

    private void SetupManagers()
    {
        DebugLog("[SceneManager01] Setup manager...");

        if (autoFindManagers)
        {
            FindManagers();
        }

        if (createManagersIfMissing)
        {
            CreateMissingManagers();
        }

        ConfigureManagers();

        managersReady = checkpointManager != null && collectiblesManager != null;
        DebugLog($"[SceneManager01] Manager pronti: {managersReady}");

        // Setup AbilitiesManager
        if (abilitiesManager == null && autoFindManagers)
        {
            abilitiesManager = FindFirstObjectByType<AbilitiesManager>();
        }

        if (abilitiesManager == null && createManagersIfMissing)
        {
            GameObject abilitiesGO = new GameObject("AbilitiesManager");
            abilitiesGO.transform.parent = transform;
            abilitiesManager = abilitiesGO.AddComponent<AbilitiesManager>();
            DebugLog("[SceneManager01] ✅ AbilitiesManager creato automaticamente");
        }
        // Setup CameraManager
if (cameraManager == null && autoFindManagers)
{
    cameraManager = CameraManager.Instance;
    if (cameraManager == null)
    {
        cameraManager = FindFirstObjectByType<CameraManager>();
    }
}

if (cameraManager != null)
{
    DebugLog($"[SceneManager01] CameraManager trovato: {cameraManager.name}");
    cameraManagerReady = true;
}
    }
    public void RegisterCameraManager(CameraManager camManager)
{
    DebugLog("[SceneManager01] CameraManager registrato");
    cameraManager = camManager;
    cameraManagerReady = true;
    
    // Configura la camera preferita se specificata
    if (preferredSceneCamera != null)
    {
        cameraManager.ConfigureForScene(preferredSceneCamera, sceneName);
    }
    
    OnCameraManagerRegistered?.Invoke(cameraManager);
}

public void OnCameraManagerInitializing(CameraManager camManager)
{
    DebugLog("[SceneManager01] CameraManager in inizializzazione...");
}

public void OnCameraManagerFullyReady(CameraManager camManager)
{
    DebugLog("[SceneManager01] CameraManager completamente pronto");
    cameraSystemFullyInitialized = true;
    OnCameraSystemReady?.Invoke();
}

public CameraManager GetCameraManager() => cameraManager;

public bool IsCameraManagerReady() => cameraManagerReady && cameraSystemFullyInitialized;
    private void FindManagers()
    {
        if (checkpointManager == null)
        {
            checkpointManager = CheckpointManager.Instance;
            if (checkpointManager == null)
            {
                checkpointManager = Object.FindFirstObjectByType<CheckpointManager>();
            }
        }
        
        if (collectiblesManager == null)
        {
            collectiblesManager = CollectiblesManager.Instance;
            if (collectiblesManager == null)
            {
                collectiblesManager = Object.FindFirstObjectByType<CollectiblesManager>();
            }
        }
        
        DebugLog($"[SceneManager01] Manager trovati - Checkpoint: {checkpointManager != null}, Collectibles: {collectiblesManager != null}");
    }
    
    private void CreateMissingManagers()
    {
        if (checkpointManager == null)
        {
            GameObject checkpointGO = new GameObject("CheckpointManager");
            checkpointGO.transform.parent = transform;
            checkpointManager = checkpointGO.AddComponent<CheckpointManager>();
            DebugLog("[SceneManager01] ✅ CheckpointManager creato automaticamente");
        }
        
        if (collectiblesManager == null)
        {
            GameObject collectiblesGO = new GameObject("CollectiblesManager");
            collectiblesGO.transform.parent = transform;
            collectiblesManager = collectiblesGO.AddComponent<CollectiblesManager>();
            DebugLog("[SceneManager01] ✅ CollectiblesManager creato automaticamente");
        }
    }
    
    private void ConfigureManagers()
    {
        if (checkpointManager != null)
        {
            checkpointManager.SetSceneName(sceneName);
            checkpointManager.SetDebugLogsEnabled(enableDebugLogs);
            DebugLog("[SceneManager01] CheckpointManager configurato");
        }
        
        if (collectiblesManager != null)
        {
            collectiblesManager.SetSceneName(sceneName);
            collectiblesManager.SetDebugLogsEnabled(enableDebugLogs);
            DebugLog("[SceneManager01] CollectiblesManager configurato");
        }
    }
    /// <summary>
/// NUOVO: Callback per quando il CameraManager è pronto
/// </summary>
public void OnCameraManagerReady(CameraManager cameraManager)
{
    DebugLog($"[SceneManager] CameraManager pronto e coordinato: {cameraManager.name}");
    
    // Verifica che tutto sia OK
    if (cameraManager.IsCameraSystemReady())
    {
        DebugLog("[SceneManager] ✅ Sistema camera confermato pronto");
        
        // Ora possiamo procedere con l'attivazione della scena
        OnGameManagerSceneReady(sceneName);
    }
    else
    {
        DebugLog("[SceneManager] ⚠️ CameraManager non completamente pronto - attendendo...");
        StartCoroutine(WaitForCameraManagerReady(cameraManager));
    }
}
/// <summary>
/// NUOVO: Attende che il CameraManager sia effettivamente pronto
/// </summary>
private IEnumerator WaitForCameraManagerReady(CameraManager cameraManager)
{
    yield return cameraManager.WaitForCameraSystemReady();
    
    if (cameraManager.IsCameraSystemReady())
    {
        DebugLog("[SceneManager] ✅ CameraManager ora pronto dopo attesa");
        OnGameManagerSceneReady(sceneName);
    }
    else
    {
        DebugLog("[SceneManager] ❌ CameraManager timeout - continuo comunque");
        OnGameManagerSceneReady(sceneName);
    }
}
    private void ConnectManagers()
    {
        if (!managersReady) return;
        
        DebugLog("[SceneManager01] Connessione manager...");
        
        if (checkpointManager != null)
        {
            checkpointManager.OnCheckpointActivated.AddListener(OnCheckpointActivated);
            checkpointManager.OnCheckpointCleared.AddListener(OnCheckpointCleared);
        }
        
        if (collectiblesManager != null)
        {
            collectiblesManager.OnAllCollectiblesCompleted.AddListener(OnAllCollectiblesCompleted);
            collectiblesManager.OnPresentCollected.AddListener(OnPresentCollected);
            collectiblesManager.OnMemoryCollected.AddListener(OnMemoryCollected);
        }
        
        if (abilitiesManager != null)
        {
            abilitiesManager.OnAbilityActivatedByEvent.AddListener(OnAbilityActivated);
            abilitiesManager.SetDebugLogsEnabled(enableDebugLogs);
        }
        
        DebugLog("[SceneManager01] Manager connessi con successo");
    }
    
    private void InitializeScene()
    {
        if (sceneInitialized) return;
        
        DebugLog("[SceneManager01] Inizializzazione scena...");
        
        sceneInitialized = true;
        OnSceneInitialized?.Invoke();
        
        DebugLog("[SceneManager01] Scena inizializzata");
    }
    
    private void FinalizeSceneSetup()
    {
        DebugLog("[SceneManager01] Finalizzazione setup scena...");
        
        bool allReady = sceneInitialized && managersReady && uiSetupComplete;
        
        if (allReady)
        {
            DebugLog("[SceneManager01] ✅ Scena completamente pronta!");
            OnSceneReady?.Invoke();
        }
        else
        {
            DebugLog("[SceneManager01] ⚠️ Scena non completamente pronta - verificare setup");
        }
    }
    
    // ========== EVENT HANDLERS ==========
    
    private void OnCheckpointActivated(string checkpointName)
    {
        DebugLog($"[SceneManager01] Checkpoint raggiunto: '{checkpointName}'");
    }
    
    private void OnCheckpointCleared(string checkpointName)
    {
        DebugLog($"[SceneManager01] Checkpoint cancellato: '{checkpointName}'");
    }
    
    private void OnPresentCollected(string presentName)
    {
        DebugLog($"[SceneManager01] Present raccolto: '{presentName}'");
    }
    
    private void OnMemoryCollected(string memoryName)
    {
        DebugLog($"[SceneManager01] Memory raccolta: '{memoryName}'");
    }
    
    private void OnAllCollectiblesCompleted()
    {
        DebugLog("[SceneManager01] 🏆 Tutti i collectibles completati!");
        OnSceneCompleted?.Invoke();
    }
    
    private void OnAbilityActivated(string abilityName)
    {
        DebugLog($"[SceneManager01] Abilità attivata: '{abilityName}'");
    }
    
    // ========== METODI PUBBLICI PER COMPATIBILITÀ ==========
    
    public void NotifyPresentCollected(string presentName = "")
    {
        if (collectiblesManager != null)
        {
            collectiblesManager.NotifyPresentCollected(presentName);
        }
        else
        {
            DebugLog("[SceneManager01] ⚠️ CollectiblesManager non disponibile");
        }
    }
    
    public void NotifyMemoryCollected(string memoryName = "")
    {
        if (collectiblesManager != null)
        {
            collectiblesManager.NotifyMemoryCollected(memoryName);
        }
        else
        {
            DebugLog("[SceneManager01] ⚠️ CollectiblesManager non disponibile");
        }
    }
    
    public void OnCollectibleCollected(string collectibleName, CollectibleType collectibleType)
    {
        if (collectiblesManager != null)
        {
            collectiblesManager.OnCollectibleCollected(collectibleName, collectibleType);
        }
        else
        {
            DebugLog("[SceneManager01] ⚠️ CollectiblesManager non disponibile");
        }
    }
    
    public void NotifySceneCheckpoint(string checkpointName)
    {
        if (checkpointManager != null)
        {
            checkpointManager.ActivateCheckpoint(checkpointName);
        }
        else
        {
            DebugLog("[SceneManager01] ⚠️ CheckpointManager non disponibile");
        }
    }
    
    public void OnCheckpointReached(string checkpointName)
    {
        NotifySceneCheckpoint(checkpointName);
    }
    
    // ========== METODI DI NAVIGAZIONE SCENE (AGGIORNATI) ==========
    
    public void RestartLevel()
    {
        DebugLog("[SceneManager01] Riavvio livello");
        
        // Reset dei manager
        if (collectiblesManager != null)
        {
            collectiblesManager.ResetCollectiblesProgress();
        }
        
        if (checkpointManager != null)
        {
            checkpointManager.ResetCheckpointSystem();
        }
        
        // Usa il GameManager per riavviare con fade
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartCurrentScene();
        }
        else
        {
            // Fallback diretto
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            UnityEngine.SceneManagement.SceneManager.LoadScene(currentScene);
        }
    }
    
    public void ReturnToMainMenu()
    {
        DebugLog("[SceneManager01] Ritorno al menu principale");
        
        // Usa il GameManager per tornare al menu con fade
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnToMainMenu();
        }
        else
        {
            // Fallback diretto
            UnityEngine.SceneManagement.SceneManager.LoadScene("Title Screen");
        }
    }
    
    public void LoadNextLevel()
    {
        DebugLog("[SceneManager01] Caricamento livello successivo");
        
        string nextLevel = GetNextLevelName();
        
        if (!string.IsNullOrEmpty(nextLevel))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadSceneWithFade(nextLevel);
            }
            else
            {
                // Fallback diretto
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextLevel);
            }
        }
        else
        {
            DebugLog("[SceneManager01] Nessun livello successivo trovato");
            ReturnToMainMenu();
        }
    }
    
    private string GetNextLevelName()
    {
        return "02 - Next Level"; // Aggiorna con il nome del livello successivo
    }
    
    public void ShowCollectibleNotification(string type, string name)
    {
        DebugLog($"[SceneManager01] Notifica collectible: {type} - {name}");
    }
    
    // ========== GETTERS - COLLECTIBLES ==========
    
    public int GetCollectedPresents() => collectiblesManager?.GetCollectedPresents() ?? 0;
    public int GetTotalPresents() => collectiblesManager?.GetTotalPresents() ?? 0;
    public int GetCollectedMemories() => collectiblesManager?.GetCollectedMemories() ?? 0;
    public int GetTotalMemories() => collectiblesManager?.GetTotalMemories() ?? 0;
    public int GetTotalCollected() => collectiblesManager?.GetTotalCollected() ?? 0;
    public int GetTotalAvailable() => collectiblesManager?.GetTotalAvailable() ?? 0;
    
    public float GetPresentsProgress() => collectiblesManager?.GetPresentsProgress() ?? 0f;
    public float GetMemoriesProgress() => collectiblesManager?.GetMemoriesProgress() ?? 0f;
    public float GetOverallProgress() => collectiblesManager?.GetOverallProgress() ?? 0f;
    
    public bool AreAllPresentsCollected() => collectiblesManager?.AreAllPresentsCollected() ?? false;
    public bool AreAllMemoriesCollected() => collectiblesManager?.AreAllMemoriesCollected() ?? false;
    public bool AreAllCollectiblesCompleted() => collectiblesManager?.AreAllCollectiblesCompleted() ?? false;
    
    // ========== GETTERS - CHECKPOINT ==========
    
    public string GetCurrentCheckpoint() => checkpointManager?.GetCurrentCheckpoint() ?? "";
    public bool HasActiveCheckpoint() => checkpointManager?.HasActiveCheckpoint() ?? false;
    public Vector3 GetCurrentSpawnPosition() => checkpointManager?.GetCurrentSpawnPosition() ?? Vector3.zero;
    public Quaternion GetCurrentSpawnRotation() => checkpointManager?.GetCurrentSpawnRotation() ?? Quaternion.identity;
    
    // ========== GETTERS - UI ==========
    
    public bool IsUISetupComplete() => uiSetupComplete;
    public bool IsLevelTitleEnabled() => showLevelTitle;
    
    // ========== GETTERS - ABILITIES ==========
    
    public AbilitiesManager GetAbilitiesManager() => abilitiesManager;
    
    // ========== UTILITY METHODS ==========
    
    public void RefreshScene()
    {
        DebugLog("[SceneManager01] Refresh completo scena");
        
        if (collectiblesManager != null)
        {
            collectiblesManager.RefreshCollectiblesSystem();
        }
    }
    
    public void ResetScene()
    {
        DebugLog("[SceneManager01] Reset completo scena");
        
        if (collectiblesManager != null)
        {
            collectiblesManager.ResetCollectiblesProgress();
        }
        
        if (checkpointManager != null)
        {
            checkpointManager.ResetCheckpointSystem();
        }
        
        // Reset UI
        ConfigureInitialUIState();
    }
    
    public void ForceSaveAll()
    {
        DebugLog("[SceneManager01] Salvataggio forzato di tutti i dati");
        
        if (collectiblesManager != null)
        {
            collectiblesManager.ForceSave();
        }
        
        if (checkpointManager != null)
        {
            checkpointManager.SaveCheckpointData();
        }
    }
    
    public bool AreAllManagersReady()
    {
        return managersReady && checkpointManager != null && collectiblesManager != null;
    }
    
    public CheckpointManager GetCheckpointManager() => checkpointManager;
    public CollectiblesManager GetCollectiblesManager() => collectiblesManager;
    
    // ========== SETTINGS ==========
    
    public void SetDebugMode(bool enabled)
    {
        enableDebugLogs = enabled;
        
        if (checkpointManager != null)
        {
            checkpointManager.SetDebugLogsEnabled(enabled);
        }
        
        if (collectiblesManager != null)
        {
            collectiblesManager.SetDebugLogsEnabled(enabled);
        }
        
        if (abilitiesManager != null)
        {
            abilitiesManager.SetDebugLogsEnabled(enabled);
        }
        
        DebugLog($"[SceneManager01] Debug mode {(enabled ? "abilitato" : "disabilitato")}");
    }
    
    public void SetAutoSave(bool enabled)
    {
        if (checkpointManager != null)
        {
            checkpointManager.SetAutoSaveEnabled(enabled);
        }
        
        if (collectiblesManager != null)
        {
            collectiblesManager.SetAutoSaveEnabled(enabled);
        }
        
        DebugLog($"[SceneManager01] Auto-save {(enabled ? "abilitato" : "disabilitato")}");
    }
    
    // ========== DEBUG ==========
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log(message);
        }
    }
    
    [ContextMenu("Debug Current State")]
    public void DebugCurrentState()
    {
        Debug.Log($"=== SceneManager01 State ===\n" +
                  $"Scene: '{sceneName}'\n" +
                  $"Initialized: {sceneInitialized}\n" +
                  $"Managers Ready: {managersReady}\n" +
                  $"UI Setup Complete: {uiSetupComplete}\n" +
                  $"All Ready: {AreAllManagersReady()}\n" +
                  $"CheckpointManager: {(checkpointManager != null ? "✅" : "❌")}\n" +
                  $"CollectiblesManager: {(collectiblesManager != null ? "✅" : "❌")}\n" +
                  $"AbilitiesManager: {(abilitiesManager != null ? "✅" : "❌")}\n" +
                  $"Current Checkpoint: '{GetCurrentCheckpoint()}'\n" +
                  $"Collectibles: {GetTotalCollected()}/{GetTotalAvailable()} ({GetOverallProgress() * 100:F1}%)\n" +
                  $"Presents: {GetCollectedPresents()}/{GetTotalPresents()}\n" +
                  $"Memories: {GetCollectedMemories()}/{GetTotalMemories()}\n" +
                  $"Level Title Enabled: {showLevelTitle}");
    }
    
    [ContextMenu("Test - Enable PowerUp UI")]
    public void DebugEnablePowerUpUI()
    {
        EnablePowerUpUI();
    }
    
    [ContextMenu("Test - Disable PowerUp UI")]
    public void DebugDisablePowerUpUI()
    {
        DisablePowerUpUI();
    }
    
    [ContextMenu("Test - Show Level Title")]
    public void DebugShowLevelTitle()
    {
        if (levelTitleUI != null && showLevelTitle)
        {
            StartCoroutine(ShowLevelTitleCoroutine());
        }
        else
        {
            DebugLog("Level Title UI non disponibile o disabilitato");
        }
    }
    
    [ContextMenu("Test - Restart Level")]
    public void DebugRestartLevel()
    {
        RestartLevel();
    }
    
    [ContextMenu("Test - Next Level")]
    public void DebugLoadNextLevel()
    {
        LoadNextLevel();
    }
    
    [ContextMenu("Test - Main Menu")]
    public void DebugReturnToMainMenu()
    {
        ReturnToMainMenu();
    }
    
    // ========== CLEANUP ==========
    
    private void OnDestroy()
    {
        // Disconnetti eventi del GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnSceneReady -= OnGameManagerSceneReady;
        }
        
        // Disconnetti eventi dei manager
        if (checkpointManager != null)
        {
            checkpointManager.OnCheckpointActivated.RemoveListener(OnCheckpointActivated);
            checkpointManager.OnCheckpointCleared.RemoveListener(OnCheckpointCleared);
        }
        
        if (collectiblesManager != null)
        {
            collectiblesManager.OnAllCollectiblesCompleted.RemoveListener(OnAllCollectiblesCompleted);
            collectiblesManager.OnPresentCollected.RemoveListener(OnPresentCollected);
            collectiblesManager.OnMemoryCollected.RemoveListener(OnMemoryCollected);
        }
        
        if (abilitiesManager != null)
        {
            abilitiesManager.OnAbilityActivatedByEvent.RemoveListener(OnAbilityActivated);
        }
        
        if (Instance == this)
        {
            Instance = null;
        }
        
        DebugLog("[SceneManager01] Cleanup completato");
    }
}