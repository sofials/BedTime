using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class SceneManager01 : MonoBehaviour
{
    [Header("Scene Configuration")]
    [SerializeField] private string sceneName;

    
    [Header("Current Scene Progress")]
    [SerializeField] private int totalPresents = 0;
    [SerializeField] private int collectedPresents = 0;
    [SerializeField] private int totalMemories = 0;
    [SerializeField] private int collectedMemories = 0;
    
    [Header("Present Events")]
    public UnityEvent<int, int> OnPresentCountChanged; // collected, total
    public UnityEvent OnAllPresentsCollected;
    
    [Header("Memory Events")]
    public UnityEvent<int, int> OnMemoryCountChanged; // collected, total
    public UnityEvent OnAllMemoriesCollected;
    
    [Header("Combined Events")]
    public UnityEvent<int, int> OnAllCollectiblesCountChanged; // total collected, total available
    public UnityEvent OnAllCollectiblesCompleted;
    
    [Header("Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    
    [Header("Development Mode")]
    [SerializeField] private bool developmentMode = true;
    [SerializeField] private bool skipSyncOnStart = true;
    
    // Tracking degli oggetti raccolti per nome (per GameManager)
    private List<string> collectedPresentNames = new List<string>();
    private List<string> collectedMemoryNames = new List<string>();
    
    // Singleton pattern
    public static SceneManager01 Instance { get; private set; }
    
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
                DebugLog($"[SceneManager01] ✅ PlayerUI connesso manualmente con successo!");
                
                // Forza un update iniziale dell'UI
                playerUI.UpdateCountersManually(collectedMemories, totalMemories, collectedPresents, totalPresents);
            }
            else
            {
                DebugLog($"[SceneManager01] ❌ Fallita connessione manuale con PlayerUI");
            }
        }
        else
        {
            DebugLog($"[SceneManager01] ⚠️ PlayerUI non trovato nella scena!");
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
        collectedPresents = 0;
        collectedMemories = 0;
        collectedPresentNames.Clear();
        collectedMemoryNames.Clear();
        
        // Conta gli oggetti usando i tag
        CountCollectiblesByTags();
        
        DebugLog($"[SceneManager01] Scena inizializzata: {totalPresents} presents, {totalMemories} memories");
        sceneInitialized = true;
    }
    
    private void InitializeWithGameManager()
    {
        if (!gameManagerReady || !sceneInitialized || GameManager.Instance == null) return;
        
        // Crea liste di nomi per GameManager (basati sui GameObject trovati)
        List<string> allMemoryNames = GetAllMemoryNames();
        List<string> allPresentNames = GetAllPresentNames();
        
        // Notifica al GameManager i totali di questa scena
        GameManager.Instance.InitializeSceneMemories(sceneName, totalMemories, allMemoryNames);
        GameManager.Instance.InitializeScene01Presents(totalPresents, allPresentNames);
        
        // 🔧 MODALITÀ SVILUPPO: Salta la sincronizzazione se richiesto
        if (developmentMode && skipSyncOnStart)
        {
            DebugLog("🔧 [DEV MODE] Sincronizzazione saltata - tutti i regali saranno visibili");
        }
        else
        {
            // ⭐ SOLO SINCRONIZZA I CONTATORI - NON TOCCARE I GAMEOBJECT ⭐
            SyncCountersWithGameManager();
        }
        
        DebugLog($"[SceneManager01] Sincronizzazione con GameManager completata");
        
        // Aggiorna l'UI iniziale
        UpdateUI();
    }
    
    /// <summary>
    /// ⭐ VERSIONE CORRETTA: Sincronizza SOLO i contatori, NON modifica i GameObject ⭐
    /// </summary>
    private void SyncCountersWithGameManager()
    {
        if (GameManager.Instance == null) return;
        
        DebugLog("[SceneManager01] === SINCRONIZZAZIONE CONTATORI (NO GAMEOBJECT MODIFICATION) ===");
        
        // Sincronizza SOLO i contatori delle memorie già raccolte
        List<string> globalCollectedMemories = GameManager.Instance.GetCollectedMemoriesNamesInScene(sceneName);
        foreach (string memoryName in globalCollectedMemories)
        {
            if (!collectedMemoryNames.Contains(memoryName))
            {
                collectedMemoryNames.Add(memoryName);
                DebugLog($"[SceneManager01] ✅ Memory già raccolta registrata: {memoryName}");
            }
        }
        collectedMemories = collectedMemoryNames.Count;
        
        // Sincronizza SOLO i contatori dei presents già raccolti
        List<string> globalCollectedPresents = GameManager.Instance.GetCollectedScene01PresentNames();
        foreach (string presentName in globalCollectedPresents)
        {
            if (!collectedPresentNames.Contains(presentName))
            {
                collectedPresentNames.Add(presentName);
                DebugLog($"[SceneManager01] ✅ Present già raccolto registrato: {presentName}");
            }
        }
        collectedPresents = collectedPresentNames.Count;
        
        DebugLog($"[SceneManager01] ✅ Sincronizzazione contatori completata: {collectedMemories} memories e {collectedPresents} presents");
        DebugLog("[SceneManager01] ⚠️ NOTA: I GameObject rimangono ATTIVI - la visibilità è gestita dalle classi Collectibles");
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
    
    private List<string> GetAllPresentNames()
    {
        List<string> presentNames = new List<string>();
        
        // Cerca tutti gli oggetti Present nella scena
        GameObject[] presentObjects = GameObject.FindGameObjectsWithTag("Present");
        foreach (GameObject obj in presentObjects)
        {
            presentNames.Add(obj.name);
        }
        
        // Fallback con classe Collectibles
        if (presentNames.Count == 0)
        {
            Collectibles[] allCollectibles = FindObjectsByType<Collectibles>(FindObjectsSortMode.None);
            foreach (Collectibles collectible in allCollectibles)
            {
                if (collectible.GetCollectibleType() == CollectibleType.Present)
                {
                    presentNames.Add(collectible.gameObject.name);
                }
            }
        }
        
        return presentNames;
    }
    
    private void CountCollectiblesByTags()
    {
        // Conta i Present usando il tag
        GameObject[] presentObjects = GameObject.FindGameObjectsWithTag("Present");
        totalPresents = presentObjects?.Length ?? 0;
        
        // Conta le Memories usando il tag
        GameObject[] memoryObjects = GameObject.FindGameObjectsWithTag("Memories");
        totalMemories = memoryObjects?.Length ?? 0;
        
        DebugLog($"[SceneManager01] Conteggio tramite tag completato: {totalPresents} presents, {totalMemories} memories");
        
        // Se non troviamo niente con i tag, prova con le classi come fallback
        if (totalPresents == 0 && totalMemories == 0)
        {
            CountCollectiblesByClass();
        }
    }
    
    private void CountCollectiblesByClass()
    {
        // Fallback: conta usando la classe Collectibles
        Collectibles[] allCollectibles = FindObjectsByType<Collectibles>(FindObjectsSortMode.None);
        int presentCount = 0;
        int memoryCount = 0;
        
        foreach (Collectibles collectible in allCollectibles)
        {
            if (!collectible.IsCollected())
            {
                if (collectible.GetCollectibleType() == CollectibleType.Present)
                {
                    presentCount++;
                }
                else if (collectible.GetCollectibleType() == CollectibleType.Memory)
                {
                    memoryCount++;
                }
            }
        }
        
        totalPresents = presentCount;
        totalMemories = memoryCount;
        
        DebugLog($"[SceneManager01] Fallback conteggio con classe: {totalPresents} presents, {totalMemories} memories");
    }
    
    // ========== METODI CHIAMATI DAL PLAYER/COLLECTIBLES ==========
    
    /// <summary>
    /// ⭐ VERSIONE CORRETTA: Solo tracking, NO gestione GameObject ⭐
    /// </summary>
    public void NotifyPresentCollected(string presentName = "")
    {
        // Se non viene fornito un nome, genera uno generico
        if (string.IsNullOrEmpty(presentName))
        {
            presentName = $"Present_{collectedPresents + 1}";
        }
        
        // Evita duplicati
        if (collectedPresentNames.Contains(presentName))
        {
            DebugLog($"[SceneManager01] Present {presentName} già raccolto, ignorato");
            return;
        }
        
        collectedPresentNames.Add(presentName);
        collectedPresents++;
        
        DebugLog($"[SceneManager01] ✅ Present '{presentName}' TRACCIATO come raccolto! Progresso: {collectedPresents}/{totalPresents}");
        
        // Notifica al GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnScene01PresentCollected(presentName);
        }
        
        // ⭐ RIMOSSO: NON disattiviamo più il GameObject! ⭐
        // La classe Collectibles gestisce da sola la propria visibilità
        DebugLog($"[SceneManager01] ℹ️ GameObject {presentName} rimane ATTIVO - gestione visibilità delegata alla classe Collectibles");
        
        // 🔥 FORZA L'AGGIORNAMENTO UI IMMEDIATO
        ForceUIUpdate();
        
        // Eventi per la UI
        OnPresentCountChanged?.Invoke(collectedPresents, totalPresents);
        
        // Controlla se tutti i presents sono stati raccolti
        if (collectedPresents >= totalPresents && totalPresents > 0)
        {
            DebugLog("[SceneManager01] 🎉 Tutti i presents raccolti!");
            OnAllPresentsCollected?.Invoke();
            CheckAllCollectiblesCompletion();
        }
        
        UpdateUI();
    }
    
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
            DebugLog($"[SceneManager01] Memory {memoryName} già raccolta, ignorata");
            return;
        }
        
        collectedMemoryNames.Add(memoryName);
        collectedMemories++;
        
        DebugLog($"[SceneManager01] ✅ Memory '{memoryName}' TRACCIATA come raccolta! Progresso: {collectedMemories}/{totalMemories}");
        
        // Notifica al GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnSceneMemoryCollected(sceneName, memoryName);
        }
        
        // ⭐ RIMOSSO: NON disattiviamo più il GameObject! ⭐
        // La classe Collectibles gestisce da sola la propria visibilità
        DebugLog($"[SceneManager01] ℹ️ GameObject {memoryName} rimane ATTIVO - gestione visibilità delegata alla classe Collectibles");
        
        // 🔥 FORZA L'AGGIORNAMENTO UI IMMEDIATO
        ForceUIUpdate();
        
        // Eventi per la UI
        OnMemoryCountChanged?.Invoke(collectedMemories, totalMemories);
        
        // Controlla se tutte le memories sono state raccolte
        if (collectedMemories >= totalMemories && totalMemories > 0)
        {
            DebugLog("[SceneManager01] 🎉 Tutte le memories raccolte!");
            OnAllMemoriesCollected?.Invoke();
            CheckAllCollectiblesCompletion();
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
            playerUI.UpdateCountersManually(collectedMemories, totalMemories, collectedPresents, totalPresents);
            DebugLog($"[SceneManager01] 🔥 UI aggiornata forzatamente: M={collectedMemories}/{totalMemories}, P={collectedPresents}/{totalPresents}");
        }
        else
        {
            DebugLog("[SceneManager01] ⚠️ PlayerUI non trovato per aggiornamento forzato");
        }
    }
    
    // Metodi di compatibilità per il codice esistente
    public void NotifyPresentCollected()
    {
        NotifyPresentCollected("");
    }
    
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
        DebugLog($"[SceneManager01] Checkpoint {checkpointName} raggiunto");
    }
    
    private void CheckAllCollectiblesCompletion()
    {
        bool presentsComplete = totalPresents == 0 || collectedPresents >= totalPresents;
        bool memoriesComplete = totalMemories == 0 || collectedMemories >= totalMemories;
        
        if (presentsComplete && memoriesComplete && (totalPresents > 0 || totalMemories > 0))
        {
            DebugLog("[SceneManager01] 🏆 TUTTI i collectibles completati!");
            OnAllCollectiblesCompleted?.Invoke();
        }
    }
    
    private void UpdateUI()
    {
        int totalCollected = collectedPresents + collectedMemories;
        int totalAvailable = totalPresents + totalMemories;
        
        OnAllCollectiblesCountChanged?.Invoke(totalCollected, totalAvailable);
        
        // 🔥 ASSICURATI CHE ANCHE IL PLAYERUI SIA AGGIORNATO
        ForceUIUpdate();
    }
    
    // ========== GETTERS - PRESENTS ==========
    
    public int GetCollectedPresents() => collectedPresents;
    public int GetTotalPresents() => totalPresents;
    public float GetPresentsProgress() => totalPresents > 0 ? (float)collectedPresents / totalPresents : 0f;
    public float GetPresentsCompletionPercentage() => GetPresentsProgress() * 100f;
    public bool AreAllPresentsCollected() => collectedPresents >= totalPresents && totalPresents > 0;
    public List<string> GetCollectedPresentNames() => new List<string>(collectedPresentNames);
    
    // ========== GETTERS - MEMORIES ==========
    
    public int GetCollectedMemories() => collectedMemories;
    public int GetTotalMemories() => totalMemories;
    public float GetMemoriesProgress() => totalMemories > 0 ? (float)collectedMemories / totalMemories : 0f;
    public float GetMemoriesCompletionPercentage() => GetMemoriesProgress() * 100f;
    public bool AreAllMemoriesCollected() => collectedMemories >= totalMemories && totalMemories > 0;
    public List<string> GetCollectedMemoryNames() => new List<string>(collectedMemoryNames);
    
    // ========== GETTERS - COMBINED ==========
    
    public int GetTotalCollected() => collectedPresents + collectedMemories;
    public int GetTotalAvailable() => totalPresents + totalMemories;
    public float GetOverallProgress() 
    {
        int total = GetTotalAvailable();
        return total > 0 ? (float)GetTotalCollected() / total : 0f;
    }
    public float GetOverallCompletionPercentage() => GetOverallProgress() * 100f;
    public bool AreAllCollectiblesCompleted()
    {
        return AreAllPresentsCollected() && AreAllMemoriesCollected();
    }
    
    // ========== UTILITY METHODS ==========
    
    public void RefreshSceneCounts()
    {
        DebugLog("[SceneManager01] Aggiornamento conteggi scena");
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
        DebugLog("[SceneManager01] Reset progresso scena");
        collectedPresents = 0;
        collectedMemories = 0;
        collectedPresentNames.Clear();
        collectedMemoryNames.Clear();
        UpdateUI();
    }
    
    public void ResetAndRefresh()
    {
        DebugLog("[SceneManager01] Reset completo e refresh");
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
        DebugLog($"[SceneManager01] Collectible registrato: {collectible.GetName()} (Tipo: {collectible.GetCollectibleType()})");
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
        DebugLog($"[SceneManager01] ✅ Collectible raccolto notificato: {collectibleName} ({collectibleType})");
        
        switch (collectibleType)
        {
            case CollectibleType.Present:
                NotifyPresentCollected(collectibleName);
                break;
                
            case CollectibleType.Memory:
                NotifyMemoryCollected(collectibleName);
                break;
                
            default:
                DebugLog($"[SceneManager01] ⚠️ Tipo collectible non riconosciuto: {collectibleType}");
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
        string presentsList = string.Join(", ", collectedPresentNames);
        string memoriesList = string.Join(", ", collectedMemoryNames);
        
        Debug.Log($"=== SceneManager01 State ===\n" +
                  $"Presents: {collectedPresents}/{totalPresents} ({GetPresentsCompletionPercentage():F1}%)\n" +
                  $"Collected Presents: [{presentsList}]\n" +
                  $"Memories: {collectedMemories}/{totalMemories} ({GetMemoriesCompletionPercentage():F1}%)\n" +
                  $"Collected Memories: [{memoriesList}]\n" +
                  $"Total: {GetTotalCollected()}/{GetTotalAvailable()} ({GetOverallCompletionPercentage():F1}%)\n" +
                  $"All Complete: {AreAllCollectiblesCompleted()}\n" +
                  $"GameManager Ready: {gameManagerReady}\n" +
                  $"⚠️ NOTA: SceneManager01 traccia SOLO i contatori - GameObject gestiti dalle classi Collectibles");
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
        
        DebugLog("[SceneManager01] Cleanup completato");
    }
}