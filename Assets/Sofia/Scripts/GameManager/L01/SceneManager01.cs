using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// SceneManager semplificato che usa CheckpointManager e CollectiblesManager
/// Molto più leggero e modulare del precedente
/// </summary>
public class SceneManager01 : MonoBehaviour
{
    [Header("Scene Configuration")]
    [SerializeField] private string sceneName = "01 - Party in Lukelandia";
    
    [Header("Manager References")]
    [SerializeField] private CheckpointManager checkpointManager;
    [SerializeField] private CollectiblesManager collectiblesManager;
    
    [Header("Auto-Setup")]
    [SerializeField] private bool autoFindManagers = true;
    [SerializeField] private bool createManagersIfMissing = true;
    
    [Header("Development Mode")]
    [SerializeField] private bool developmentMode = true;
    [SerializeField] private bool enableDebugLogs = true;
    
    [Header("Scene Events")]
    public UnityEvent OnSceneInitialized;
    public UnityEvent OnSceneReady;
    public UnityEvent OnSceneCompleted;
    
    // Singleton pattern
    public static SceneManager01 Instance { get; private set; }
    
    // Stato interno
    private bool sceneInitialized = false;
    private bool managersReady = false;
    
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
        
        // 2. Inizializza la scena
        InitializeScene();
        
        // 3. Connetti i manager
        ConnectManagers();
        
        // 4. Finalizza
        FinalizeSceneSetup();
    }
    
    // ========== SETUP MANAGER ==========
    
    private void SetupManagers()
    {
        DebugLog("[SceneManager01] Setup manager...");
        
        // Auto-trova i manager se abilitato
        if (autoFindManagers)
        {
            FindManagers();
        }
        
        // Crea i manager se mancanti e abilitato
        if (createManagersIfMissing)
        {
            CreateMissingManagers();
        }
        
        // Configura i manager trovati/creati
        ConfigureManagers();
        
        managersReady = checkpointManager != null && collectiblesManager != null;
        DebugLog($"[SceneManager01] Manager pronti: {managersReady}");
    }
    
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
        // Crea CheckpointManager se mancante
        if (checkpointManager == null)
        {
            GameObject checkpointGO = new GameObject("CheckpointManager");
            checkpointGO.transform.parent = transform;
            checkpointManager = checkpointGO.AddComponent<CheckpointManager>();
            DebugLog("[SceneManager01] ✅ CheckpointManager creato automaticamente");
        }
        
        // Crea CollectiblesManager se mancante
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
        // Configura CheckpointManager
        if (checkpointManager != null)
        {
            checkpointManager.SetSceneName(sceneName);
            checkpointManager.SetDebugLogsEnabled(enableDebugLogs);
            DebugLog("[SceneManager01] CheckpointManager configurato");
        }
        
        // Configura CollectiblesManager
        if (collectiblesManager != null)
        {
            collectiblesManager.SetSceneName(sceneName);
            collectiblesManager.SetDebugLogsEnabled(enableDebugLogs);
            DebugLog("[SceneManager01] CollectiblesManager configurato");
        }
    }
    
    // ========== CONNESSIONE MANAGER ==========
    
    private void ConnectManagers()
    {
        if (!managersReady) return;
        
        DebugLog("[SceneManager01] Connessione manager...");
        
        // Connetti eventi del CheckpointManager
        if (checkpointManager != null)
        {
            checkpointManager.OnCheckpointActivated.AddListener(OnCheckpointActivated);
            checkpointManager.OnCheckpointCleared.AddListener(OnCheckpointCleared);
        }
        
        // Connetti eventi del CollectiblesManager
        if (collectiblesManager != null)
        {
            collectiblesManager.OnAllCollectiblesCompleted.AddListener(OnAllCollectiblesCompleted);
            collectiblesManager.OnPresentCollected.AddListener(OnPresentCollected);
            collectiblesManager.OnMemoryCollected.AddListener(OnMemoryCollected);
        }
        
        DebugLog("[SceneManager01] Manager connessi con successo");
    }
    
    private void InitializeScene()
    {
        if (sceneInitialized) return;
        
        DebugLog("[SceneManager01] Inizializzazione scena...");
        
        // Qui puoi aggiungere la logica specifica della tua scena
        // Ad esempio: attivare oggetti, impostare stati iniziali, ecc.
        
        sceneInitialized = true;
        OnSceneInitialized?.Invoke();
        
        DebugLog("[SceneManager01] Scena inizializzata");
    }
    
    private void FinalizeSceneSetup()
    {
        DebugLog("[SceneManager01] Finalizzazione setup scena...");
        
        // Verifica che tutto sia pronto
        bool allReady = sceneInitialized && managersReady;
        
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
        
        // Qui puoi aggiungere logica specifica per quando si raggiunge un checkpoint
        // Ad esempio: salvare altri dati, attivare eventi, ecc.
    }
    
    private void OnCheckpointCleared(string checkpointName)
    {
        DebugLog($"[SceneManager01] Checkpoint cancellato: '{checkpointName}'");
    }
    
    private void OnPresentCollected(string presentName)
    {
        DebugLog($"[SceneManager01] Present raccolto: '{presentName}'");
        
        // Logica specifica per i presents se necessaria
    }
    
    private void OnMemoryCollected(string memoryName)
    {
        DebugLog($"[SceneManager01] Memory raccolta: '{memoryName}'");
        
        // Logica specifica per le memories se necessaria
    }
    
    private void OnAllCollectiblesCompleted()
    {
        DebugLog("[SceneManager01] 🏆 Tutti i collectibles completati!");
        
        // Logica per quando tutti i collectibles sono stati raccolti
        // Ad esempio: sbloccare aree, attivare cutscene, ecc.
        OnSceneCompleted?.Invoke();
    }
    
    // ========== METODI PUBBLICI PER COMPATIBILITÀ ==========
    
    /// <summary>
    /// Metodi di compatibilità con il vecchio SceneManager
    /// </summary>
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
    
    // ========== UTILITY METHODS ==========
    
    /// <summary>
    /// Refresh completo di tutti i sistemi
    /// </summary>
    public void RefreshScene()
    {
        DebugLog("[SceneManager01] Refresh completo scena");
        
        if (collectiblesManager != null)
        {
            collectiblesManager.RefreshCollectiblesSystem();
        }
        
        // Non c'è bisogno di refresh per il checkpoint manager
        // ma puoi aggiungere logica se necessaria
    }
    
    /// <summary>
    /// Reset completo della scena
    /// </summary>
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
    }
    
    /// <summary>
    /// Forza il salvataggio di tutti i dati
    /// </summary>
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
    
    /// <summary>
    /// Verifica lo stato di tutti i manager
    /// </summary>
    public bool AreAllManagersReady()
    {
        return managersReady && checkpointManager != null && collectiblesManager != null;
    }
    
    /// <summary>
    /// Ottieni riferimenti ai manager (per accesso diretto se necessario)
    /// </summary>
    public CheckpointManager GetCheckpointManager() => checkpointManager;
    public CollectiblesManager GetCollectiblesManager() => collectiblesManager;
    
    // ========== SETTINGS ==========
    
    /// <summary>
    /// Configura le impostazioni di debug
    /// </summary>
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
        
        DebugLog($"[SceneManager01] Debug mode {(enabled ? "abilitato" : "disabilitato")}");
    }
    
    /// <summary>
    /// Configura il salvataggio automatico
    /// </summary>
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
                  $"All Ready: {AreAllManagersReady()}\n" +
                  $"CheckpointManager: {(checkpointManager != null ? "✅" : "❌")}\n" +
                  $"CollectiblesManager: {(collectiblesManager != null ? "✅" : "❌")}\n" +
                  $"Current Checkpoint: '{GetCurrentCheckpoint()}'\n" +
                  $"Collectibles: {GetTotalCollected()}/{GetTotalAvailable()} ({GetOverallProgress() * 100:F1}%)\n" +
                  $"Presents: {GetCollectedPresents()}/{GetTotalPresents()}\n" +
                  $"Memories: {GetCollectedMemories()}/{GetTotalMemories()}");
    }
    
    [ContextMenu("Debug Manager States")]
    public void DebugManagerStates()
    {
        Debug.Log("=== Manager States ===");
        
        if (checkpointManager != null)
        {
            checkpointManager.DebugCurrentState();
        }
        else
        {
            Debug.Log("CheckpointManager: NULL");
        }
        
        if (collectiblesManager != null)
        {
            collectiblesManager.DebugCurrentState();
        }
        else
        {
            Debug.Log("CollectiblesManager: NULL");
        }
    }
    
    [ContextMenu("Test - Collect Random Present")]
    public void DebugCollectRandomPresent()
    {
        string testName = "DebugPresent_" + System.DateTime.Now.Ticks;
        NotifyPresentCollected(testName);
    }
    
    [ContextMenu("Test - Collect Random Memory")]
    public void DebugCollectRandomMemory()
    {
        string testName = "DebugMemory_" + System.DateTime.Now.Ticks;
        NotifyMemoryCollected(testName);
    }
    
    [ContextMenu("Test - Set Random Checkpoint")]
    public void DebugSetRandomCheckpoint()
    {
        string testName = "DebugCheckpoint_" + System.DateTime.Now.Ticks;
        NotifySceneCheckpoint(testName);
    }
    
    [ContextMenu("Force Refresh Scene")]
    public void DebugRefreshScene()
    {
        RefreshScene();
        DebugCurrentState();
    }
    
    [ContextMenu("Force Reset Scene")]
    public void DebugResetScene()
    {
        ResetScene();
        DebugCurrentState();
    }
    
    [ContextMenu("Force Save All")]
    public void DebugForceSaveAll()
    {
        ForceSaveAll();
    }
    
    [ContextMenu("Recreate Managers")]
    public void DebugRecreateManagers()
    {
        // Distruggi manager esistenti se presenti
        if (checkpointManager != null && checkpointManager.transform.parent == transform)
        {
            DestroyImmediate(checkpointManager.gameObject);
            checkpointManager = null;
        }
        
        if (collectiblesManager != null && collectiblesManager.transform.parent == transform)
        {
            DestroyImmediate(collectiblesManager.gameObject);
            collectiblesManager = null;
        }
        
        // Ricrea i manager
        SetupManagers();
        ConnectManagers();
        
        DebugCurrentState();
    }
    
    // ========== CLEANUP ==========
    
    private void OnDestroy()
    {
        // Salvataggio finale
        if (developmentMode)
        {
            ForceSaveAll();
        }
        
        // Disconnetti eventi per evitare errori
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
        
        if (Instance == this)
        {
            Instance = null;
        }
        
        DebugLog("[SceneManager01] Cleanup completato");
    }
}