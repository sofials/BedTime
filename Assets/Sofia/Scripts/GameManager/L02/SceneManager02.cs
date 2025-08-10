using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class SceneManager02 : MonoBehaviour
{
    [Header("Scene Configuration")]
    [SerializeField] private string sceneName;

    
    [Header("Current Scene Progress")]
    [SerializeField] private int totalMemories = 0;
    [SerializeField] private int collectedMemories = 0;
    
    [Header("Memory Events")]
    public UnityEvent<int, int> OnMemoryCountChanged; // collected, total
    public UnityEvent OnAllMemoriesCollected;
    
    [Header("Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    
    [Header("Development Mode")]
    [SerializeField] private bool developmentMode = true;
    [SerializeField] private bool skipSyncOnStart = true;
    
    // Tracking degli oggetti raccolti per nome (per GameManager)
    private List<string> collectedMemoryNames = new List<string>();
    
    // Singleton pattern
    public static SceneManager02 Instance { get; private set; }
    
    // Flag per sapere se il GameManager è pronto
    private bool gameManagerReady = false;
    private bool sceneInitialized = false;
    
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
    }
    
    private void Start()
    {
        InitializeScene();
        
        // 🔥 NOTIFICA AL PLAYERUI CHE SIAMO PRONTI
        NotifyPlayerUIConnection();
        
        // Controlla se GameManager è già pronto
        if (GameManager.Instance != null)
        {
            InitializeWithGameManager();
        }
    }
    
    // 🔥 NUOVO METODO: Notifica al PlayerUI che siamo disponibili
    private void NotifyPlayerUIConnection()
    {
        // Aspetta un frame per assicurarsi che tutto sia inizializzato
        StartCoroutine(NotifyPlayerUIAfterFrame());
    }
    
    private System.Collections.IEnumerator NotifyPlayerUIAfterFrame()
    {
        yield return null; // Aspetta 1 frame
        
        // Cerca il PlayerUI e connettilo manualmente
        PlayerUI playerUI = PlayerUI.Instance;
        if (playerUI == null)
        {
            playerUI = Object.FindFirstObjectByType<PlayerUI>();
        }
        
        if (playerUI != null)
        {
            // Usa il metodo di connessione manuale del PlayerUI
            bool connected = playerUI.ConnectToSceneManager(this);
            if (connected)
            {
                DebugLog($"[SceneManager02] ✅ PlayerUI connesso manualmente con successo!");
                
                // Forza un update iniziale dell'UI
                playerUI.UpdateCountersManually(collectedMemories, totalMemories, 0, 0);
            }
            else
            {
                DebugLog($"[SceneManager02] ❌ Fallita connessione manuale con PlayerUI");
            }
        }
        else
        {
            DebugLog($"[SceneManager02] ⚠️ PlayerUI non trovato nella scena!");
        }
    }
    
    // Chiamato dal GameManager quando è pronto
    public void OnGameManagerReady()
    {
        gameManagerReady = true;
        InitializeWithGameManager();
    }
    
    private void InitializeScene()
    {
        if (sceneInitialized) return;
        
        // Reset contatori per la scena corrente
        collectedMemories = 0;
        collectedMemoryNames.Clear();
        
        // Conta gli oggetti usando i tag
        CountCollectiblesByTags();
        
        DebugLog($"[SceneManager02] Scena inizializzata: {totalMemories} memories");
        sceneInitialized = true;
    }
    
    private void InitializeWithGameManager()
    {
        if (!gameManagerReady || !sceneInitialized || GameManager.Instance == null) return;
        
        // Crea liste di nomi per GameManager (basati sui GameObject trovati)
        List<string> allMemoryNames = GetAllMemoryNames();
        
        // Notifica al GameManager i totali di questa scena
        GameManager.Instance.InitializeSceneMemories(sceneName, totalMemories, allMemoryNames);
        
        // 🔧 MODALITÀ SVILUPPO: Salta la sincronizzazione se richiesto
        if (developmentMode && skipSyncOnStart)
        {
            DebugLog("🔧 [DEV MODE] Sincronizzazione saltata - tutte le memorie saranno visibili");
        }
        else
        {
            // ⭐ SOLO SINCRONIZZA I CONTATORI - NON TOCCARE I GAMEOBJECT ⭐
            SyncCountersWithGameManager();
        }
        
        DebugLog($"[SceneManager02] Sincronizzazione con GameManager completata");
        
        // Aggiorna l'UI iniziale
        UpdateUI();
    }
    
    /// <summary>
    /// ⭐ VERSIONE CORRETTA: Sincronizza SOLO i contatori, NON modifica i GameObject ⭐
    /// </summary>
    private void SyncCountersWithGameManager()
    {
        if (GameManager.Instance == null) return;
        
        DebugLog("[SceneManager02] === SINCRONIZZAZIONE CONTATORI (NO GAMEOBJECT MODIFICATION) ===");
        
        // Sincronizza SOLO i contatori delle memorie già raccolte
        List<string> globalCollectedMemories = GameManager.Instance.GetCollectedMemoriesNamesInScene(sceneName);
        foreach (string memoryName in globalCollectedMemories)
        {
            if (!collectedMemoryNames.Contains(memoryName))
            {
                collectedMemoryNames.Add(memoryName);
                DebugLog($"[SceneManager02] ✅ Memory già raccolta registrata: {memoryName}");
            }
        }
        collectedMemories = collectedMemoryNames.Count;
        
        DebugLog($"[SceneManager02] ✅ Sincronizzazione contatori completata: {collectedMemories} memories");
        DebugLog("[SceneManager02] ⚠️ NOTA: I GameObject rimangono ATTIVI - la visibilità è gestita dalle classi Collectibles");
    }
    
    private List<string> GetAllMemoryNames()
    {
        List<string> memoryNames = new List<string>();
        
        // Cerca tutti gli oggetti Memory nella scena
        GameObject[] memoryObjects = GameObject.FindGameObjectsWithTag("Memories");
        foreach (GameObject obj in memoryObjects)
        {
            memoryNames.Add(obj.name);
        }
        
        // Fallback con classe Collectibles
        if (memoryNames.Count == 0)
        {
            Collectibles[] allCollectibles = FindObjectsByType<Collectibles>(FindObjectsSortMode.None);
            foreach (Collectibles collectible in allCollectibles)
            {
                if (collectible.GetCollectibleType() == CollectibleType.Memory)
                {
                    memoryNames.Add(collectible.gameObject.name);
                }
            }
        }
        
        return memoryNames;
    }
    
    private void CountCollectiblesByTags()
    {
        // Conta le Memories usando il tag
        GameObject[] memoryObjects = GameObject.FindGameObjectsWithTag("Memories");
        totalMemories = memoryObjects?.Length ?? 0;
        
        DebugLog($"[SceneManager02] Conteggio tramite tag completato: {totalMemories} memories");
        
        // Se non troviamo niente con i tag, prova con le classi come fallback
        if (totalMemories == 0)
        {
            CountCollectiblesByClass();
        }
    }
    
    private void CountCollectiblesByClass()
    {
        // Fallback: conta usando la classe Collectibles
        Collectibles[] allCollectibles = FindObjectsByType<Collectibles>(FindObjectsSortMode.None);
        int memoryCount = 0;
        
        foreach (Collectibles collectible in allCollectibles)
        {
            if (!collectible.IsCollected())
            {
                if (collectible.GetCollectibleType() == CollectibleType.Memory)
                {
                    memoryCount++;
                }
            }
        }
        
        totalMemories = memoryCount;
        
        DebugLog($"[SceneManager02] Fallback conteggio con classe: {totalMemories} memories");
    }
    
    // ========== METODI CHIAMATI DAL PLAYER/COLLECTIBLES ==========
    
    /// <summary>
    /// ⭐ VERSIONE CORRETTA: Solo tracking, NO gestione GameObject ⭐
    /// </summary>
    public void NotifyMemoryCollected(string memoryName = "")
    {
        // Se non viene fornito un nome, genera uno generico
        if (string.IsNullOrEmpty(memoryName))
        {
            memoryName = $"Memory_{collectedMemories + 1}";
        }
        
        // Evita duplicati
        if (collectedMemoryNames.Contains(memoryName))
        {
            DebugLog($"[SceneManager02] Memory {memoryName} già raccolta, ignorata");
            return;
        }
        
        collectedMemoryNames.Add(memoryName);
        collectedMemories++;
        
        DebugLog($"[SceneManager02] ✅ Memory '{memoryName}' TRACCIATA come raccolta! Progresso: {collectedMemories}/{totalMemories}");
        
        // Notifica al GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnSceneMemoryCollected(sceneName, memoryName);
        }
        
        // ⭐ RIMOSSO: NON disattiviamo più il GameObject! ⭐
        // La classe Collectibles gestisce da sola la propria visibilità
        DebugLog($"[SceneManager02] ℹ️ GameObject {memoryName} rimane ATTIVO - gestione visibilità delegata alla classe Collectibles");
        
        // 🔥 FORZA L'AGGIORNAMENTO UI IMMEDIATO
        ForceUIUpdate();
        
        // Eventi per la UI
        OnMemoryCountChanged?.Invoke(collectedMemories, totalMemories);
        
        // Controlla se tutte le memories sono state raccolte
        if (collectedMemories >= totalMemories && totalMemories > 0)
        {
            DebugLog("[SceneManager02] 🎉 Tutte le memories raccolte!");
            OnAllMemoriesCollected?.Invoke();
        }
        
        UpdateUI();
    }
    
    // 🔥 NUOVO METODO: Forza aggiornamento UI immediato
    private void ForceUIUpdate()
    {
        PlayerUI playerUI = PlayerUI.Instance;
        if (playerUI != null)
        {
            // Aggiorna manualmente i contatori nel PlayerUI
            playerUI.UpdateCountersManually(collectedMemories, totalMemories, 0, 0);
            DebugLog($"[SceneManager02] 🔥 UI aggiornata forzatamente: M={collectedMemories}/{totalMemories}");
        }
        else
        {
            DebugLog("[SceneManager02] ⚠️ PlayerUI non trovato per aggiornamento forzato");
        }
    }
    
    // Metodi di compatibilità per il codice esistente
    public void NotifyMemoryCollected()
    {
        NotifyMemoryCollected("");
    }
    
    // Metodo per notificare checkpoint
    public void OnCheckpointReached(string checkpointName)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifySceneCheckpoint(sceneName, checkpointName);
        }
        DebugLog($"[SceneManager02] Checkpoint {checkpointName} raggiunto");
    }
    
    private void UpdateUI()
    {
        // 🔥 ASSICURATI CHE ANCHE IL PLAYERUI SIA AGGIORNATO
        ForceUIUpdate();
    }
    
    // ========== GETTERS - MEMORIES ==========
    
    public int GetCollectedMemories() => collectedMemories;
    public int GetTotalMemories() => totalMemories;
    public float GetMemoriesProgress() => totalMemories > 0 ? (float)collectedMemories / totalMemories : 0f;
    public float GetMemoriesCompletionPercentage() => GetMemoriesProgress() * 100f;
    public bool AreAllMemoriesCollected() => collectedMemories >= totalMemories && totalMemories > 0;
    public List<string> GetCollectedMemoryNames() => new List<string>(collectedMemoryNames);
    
    // ========== GETTERS - OVERALL ==========
    
    public int GetTotalCollected() => collectedMemories;
    public int GetTotalAvailable() => totalMemories;
    public float GetOverallProgress() => GetMemoriesProgress();
    public float GetOverallCompletionPercentage() => GetMemoriesCompletionPercentage();
    public bool AreAllCollectiblesCompleted() => AreAllMemoriesCollected();
    
    // ========== UTILITY METHODS ==========
    
    public void RefreshSceneCounts()
    {
        DebugLog("[SceneManager02] Aggiornamento conteggi scena");
        CountCollectiblesByTags();
        
        // Re-sincronizza con GameManager se necessario
        if (GameManager.Instance != null && gameManagerReady)
        {
            SyncCountersWithGameManager();
        }
        
        UpdateUI();
    }
    
    public void ResetSceneProgress()
    {
        DebugLog("[SceneManager02] Reset progresso scena");
        collectedMemories = 0;
        collectedMemoryNames.Clear();
        UpdateUI();
    }
    
    public void ResetAndRefresh()
    {
        DebugLog("[SceneManager02] Reset completo e refresh");
        ResetSceneProgress();
        RefreshSceneCounts();
    }
    
    // ========== REGISTRAZIONE COLLECTIBLES ==========
    
    // Metodo chiamato dai collectibles per registrarsi (per compatibilità)
    public void RegisterCollectible(Collectibles collectible)
    {
        if (collectible == null) return;
        
        // Questo metodo esiste per compatibilità con il codice esistente
        // ma il conteggio principale avviene tramite tag
        DebugLog($"[SceneManager02] Collectible registrato: {collectible.GetName()} (Tipo: {collectible.GetCollectibleType()})");
    }
    
    // ========== METODI PER COLLECTIBLES ESTERNI ==========
    
    /// <summary>
    /// Metodo che i collectibles possono chiamare per notificare la raccolta
    /// ⭐ SOLO TRACKING - NO GESTIONE GAMEOBJECT ⭐
    /// </summary>
    /// <param name="collectibleName">Nome dell'oggetto raccolto</param>
    /// <param name="collectibleType">Tipo di collectible</param>
    public void OnCollectibleCollected(string collectibleName, CollectibleType collectibleType)
    {
        DebugLog($"[SceneManager02] ✅ Collectible raccolto notificato: {collectibleName} ({collectibleType})");
        
        switch (collectibleType)
        {
            case CollectibleType.Memory:
                NotifyMemoryCollected(collectibleName);
                break;
                
            default:
                DebugLog($"[SceneManager02] ⚠️ Tipo collectible non supportato in questa scena: {collectibleType}");
                break;
        }
    }
    
    // ========== DEBUG ==========
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log(message);
        }
    }
    
    public void SetDebugLogs(bool enabled)
    {
        enableDebugLogs = enabled;
    }
    
    [ContextMenu("Debug Current State")]
    public void DebugCurrentState()
    {
        string memoriesList = string.Join(", ", collectedMemoryNames);
        
        Debug.Log($"=== SceneManager02 State ===\n" +
                  $"Memories: {collectedMemories}/{totalMemories} ({GetMemoriesCompletionPercentage():F1}%)\n" +
                  $"Collected Memories: [{memoriesList}]\n" +
                  $"All Complete: {AreAllMemoriesCollected()}\n" +
                  $"GameManager Ready: {gameManagerReady}\n" +
                  $"⚠️ NOTA: SceneManager02 traccia SOLO i contatori - GameObject gestiti dalle classi Collectibles");
    }
    
    [ContextMenu("🔥 Debug - Force UI Update")]
    public void DebugForceUIUpdate()
    {
        ForceUIUpdate();
        DebugCurrentState();
    }
    
    [ContextMenu("🔥 Debug - Reconnect PlayerUI")]
    public void DebugReconnectPlayerUI()
    {
        StartCoroutine(NotifyPlayerUIAfterFrame());
    }
    
    [ContextMenu("Refresh Scene Counts")]
    public void DebugRefreshCounts()
    {
        RefreshSceneCounts();
        DebugCurrentState();
    }
    
    [ContextMenu("Reset Scene Progress")]
    public void DebugResetProgress()
    {
        ResetSceneProgress();
        DebugCurrentState();
    }
    
    [ContextMenu("Force GameManager Sync")]
    public void DebugForceGameManagerSync()
    {
        if (GameManager.Instance != null)
        {
            InitializeWithGameManager();
        }
        else
        {
            Debug.LogWarning("GameManager non trovato!");
        }
    }
    
    // ========== CLEANUP ==========
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        
        DebugLog("[SceneManager02] Cleanup completato");
    }
}