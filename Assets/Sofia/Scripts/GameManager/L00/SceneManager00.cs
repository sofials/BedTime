using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class SceneManager00 : MonoBehaviour
{
    [Header("Scene Configuration")]
    [SerializeField] private string sceneName = "00 - Landing in the Dreamworld";
    
    [Header("Current Scene Progress")]
    [SerializeField] private int totalMemories = 0;
    [SerializeField] private int collectedMemories = 0;
    
    [Header("Memory Events")]
    public UnityEvent<int, int> OnMemoryCountChanged; // collected, total
    public UnityEvent OnAllMemoriesCollected;
    
    [Header("Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // Tracking degli oggetti raccolti per nome (per GameManager)
    private List<string> collectedMemoryNames = new List<string>();
    
    // Singleton pattern
    public static SceneManager00 Instance { get; private set; }
    
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
        
        // Controlla se GameManager è già pronto
        if (GameManager.Instance != null)
        {
            InitializeWithGameManager();
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
        CountMemoriesByTags();
        
        DebugLog($"[SceneManager00] Scena inizializzata: {totalMemories} memories");
        sceneInitialized = true;
    }
    
    private void InitializeWithGameManager()
    {
        if (!gameManagerReady || !sceneInitialized || GameManager.Instance == null) return;
        
        // Crea lista di nomi per GameManager (basati sui GameObject trovati)
        List<string> allMemoryNames = GetAllMemoryNames();
        
        // Notifica al GameManager i totali di questa scena
        GameManager.Instance.InitializeSceneMemories(sceneName, totalMemories, allMemoryNames);
        
        // Sincronizza con i dati già raccolti dal GameManager
        SyncWithGameManager();
        
        DebugLog($"[SceneManager00] Sincronizzazione con GameManager completata");
        
        // Aggiorna l'UI iniziale
        UpdateUI();
    }
    
    private void SyncWithGameManager()
    {
        if (GameManager.Instance == null) return;
        
        // Sincronizza le memorie già raccolte
        List<string> globalCollectedMemories = GameManager.Instance.GetCollectedMemoriesNamesInScene(sceneName);
        foreach (string memoryName in globalCollectedMemories)
        {
            if (!collectedMemoryNames.Contains(memoryName))
            {
                collectedMemoryNames.Add(memoryName);
                
                // Nascondi l'oggetto se esiste
                GameObject memoryObj = GameObject.Find(memoryName);
                if (memoryObj != null)
                {
                    memoryObj.SetActive(false);
                }
            }
        }
        collectedMemories = collectedMemoryNames.Count;
        
        DebugLog($"[SceneManager00] Sincronizzazione: {collectedMemories} memories già raccolte");
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
        
        // Fallback con classe Collectibles se non trova niente con i tag
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
    
    private void CountMemoriesByTags()
    {
        // Conta le Memories usando il tag
        GameObject[] memoryObjects = GameObject.FindGameObjectsWithTag("Memories");
        totalMemories = memoryObjects?.Length ?? 0;
        
        DebugLog($"[SceneManager00] Conteggio tramite tag completato: {totalMemories} memories");
        
        // Se non troviamo niente con i tag, prova con le classi come fallback
        if (totalMemories == 0)
        {
            CountMemoriesByClass();
        }
    }
    
    private void CountMemoriesByClass()
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
        
        DebugLog($"[SceneManager00] Fallback conteggio con classe: {totalMemories} memories");
    }
    
    // ========== GESTIONE CHECKPOINT ==========
    
    public void OnCheckpointReached(string checkpointName)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifySceneCheckpoint(sceneName, checkpointName);
        }
        DebugLog($"[SceneManager00] Checkpoint {checkpointName} raggiunto");
    }
    
    // ========== GESTIONE MEMORIE ==========
    
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
            DebugLog($"[SceneManager00] Memory {memoryName} già raccolta, ignorata");
            return;
        }
        
        collectedMemoryNames.Add(memoryName);
        collectedMemories++;
        
        DebugLog($"[SceneManager00] Memory '{memoryName}' raccolta! Progresso: {collectedMemories}/{totalMemories}");
        
        // Notifica al GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnSceneMemoryCollected(sceneName, memoryName);
        }
        
        // Nascondi l'oggetto
        GameObject memoryObj = GameObject.Find(memoryName);
        if (memoryObj != null)
        {
            memoryObj.SetActive(false);
        }
        
        // Eventi per la UI
        OnMemoryCountChanged?.Invoke(collectedMemories, totalMemories);
        
        // Controlla se tutte le memories sono state raccolte
        if (collectedMemories >= totalMemories && totalMemories > 0)
        {
            DebugLog("[SceneManager00] Tutte le memories raccolte!");
            OnAllMemoriesCollected?.Invoke();
        }
        
        UpdateUI();
    }
    
    // Metodo per collectibles esterni
    public void OnCollectibleCollected(string collectibleName, CollectibleType collectibleType)
    {
        switch (collectibleType)
        {
            case CollectibleType.Memory:
                NotifyMemoryCollected(collectibleName);
                break;
                
            default:
                DebugLog($"[SceneManager00] Tipo collectible non riconosciuto: {collectibleType}");
                break;
        }
    }
    
    // Metodo chiamato dai collectibles per registrarsi
    public void RegisterCollectible(Collectibles collectible)
    {
        if (collectible == null) return;
        
        DebugLog($"[SceneManager00] Collectible registrato: {collectible.GetName()} (Tipo: {collectible.GetCollectibleType()})");
    }
    
    private void UpdateUI()
    {
        // Aggiorna UI se necessario
        OnMemoryCountChanged?.Invoke(collectedMemories, totalMemories);
    }
    
    // ========== GETTERS ==========
    
    public int GetCollectedMemories() => collectedMemories;
    public int GetTotalMemories() => totalMemories;
    public float GetMemoriesProgress() => totalMemories > 0 ? (float)collectedMemories / totalMemories : 0f;
    public float GetMemoriesCompletionPercentage() => GetMemoriesProgress() * 100f;
    public bool AreAllMemoriesCollected() => collectedMemories >= totalMemories && totalMemories > 0;
    public List<string> GetCollectedMemoryNames() => new List<string>(collectedMemoryNames);
    
    // ========== UTILITY METHODS ==========
    
    public void RefreshSceneCounts()
    {
        DebugLog("[SceneManager00] Aggiornamento conteggi scena");
        CountMemoriesByTags();
        
        if (GameManager.Instance != null && gameManagerReady)
        {
            SyncWithGameManager();
        }
        
        UpdateUI();
    }
    
    public void ResetSceneProgress()
    {
        DebugLog("[SceneManager00] Reset progresso scena");
        collectedMemories = 0;
        collectedMemoryNames.Clear();
        UpdateUI();
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
        
        Debug.Log($"=== SceneManager00 State ===\n" +
                  $"Memories: {collectedMemories}/{totalMemories} ({GetMemoriesCompletionPercentage():F1}%)\n" +
                  $"Collected Memories: [{memoriesList}]\n" +
                  $"All Complete: {AreAllMemoriesCollected()}\n" +
                  $"GameManager Ready: {gameManagerReady}");
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
    
    // ========== CLEANUP ==========
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        
        DebugLog("[SceneManager00] Cleanup completato");
    }
}