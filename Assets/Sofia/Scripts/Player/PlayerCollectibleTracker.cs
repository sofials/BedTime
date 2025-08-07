using UnityEngine;
using System;

public class PlayerCollectibleTracker : MonoBehaviour
{
    [Header("Memory Collection")]
    public int totalMemories = 0;
    public int collectedMemories = 0;
    
    [Header("Present Collection")]
    public int totalPresents = 0;
    public int collectedPresents = 0;
    
    [Header("Scene Integration")]
    [SerializeField] private bool useSceneManagerIntegration = true;
    [SerializeField] private bool autoFindCollectiblesInScene = true;
    
    [Header("Combined Tracking")]
    [SerializeField] private bool trackCombinedProgress = true;
    
    // Eventi per le Memory
    public event Action<int, int> OnMemoryCollected; // collected, total
    public event Action<int> OnMemoriesInitialized; // total
    public event Action OnAllMemoriesCollected;
    
    // Eventi per i Present
    public event Action<int, int> OnPresentCollected; // collected, total
    public event Action<int> OnPresentsInitialized; // total
    public event Action OnAllPresentsCollected;
    
    // Eventi combinati
    public event Action<int, int, int, int> OnCollectibleUpdate; // memCurrent, memTotal, presCurrent, presTotal
    public event Action<int, int> OnTotalCollectibleUpdate; // totalCollected, totalAvailable
    public event Action OnAllCollectiblesCompleted;
    
    // Singleton per accesso globale
    public static PlayerCollectibleTracker Instance { get; private set; }
    
    void Awake() 
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
            // Non distruggere tra le scene se è un GameManager persistente
            // DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        InitializeCollectibles();
    }
    
    void Start()
    {
        // Connessione con SceneManager01 se disponibile
        if (useSceneManagerIntegration && SceneManager01.Instance != null)
        {
            ConnectToSceneManager();
        }
    }
    
    private void InitializeCollectibles()
    {
        // Reset per ogni nuova scena
        collectedMemories = 0;
        collectedPresents = 0;
        
        if (useSceneManagerIntegration && SceneManager01.Instance != null)
        {
            // Prendi i totali dal SceneManager se disponibile
            totalMemories = SceneManager01.Instance.GetTotalMemories();
            totalPresents = SceneManager01.Instance.GetTotalPresents();
            Debug.Log($"[PlayerCollectibleTracker] Totale dal SceneManager: {totalMemories} memorie, {totalPresents} presents");
        }
        else if (autoFindCollectiblesInScene)
        {
            // Fallback: conta i collectibles nella scena
            CountCollectiblesInScene();
        }
        
        // Notifica la UI dei totali
        if (totalMemories > 0)
        {
            OnMemoriesInitialized?.Invoke(totalMemories);
            Debug.Log($"[PlayerCollectibleTracker] Inizializzato con {totalMemories} memorie");
        }
        
        if (totalPresents > 0)
        {
            OnPresentsInitialized?.Invoke(totalPresents);
            Debug.Log($"[PlayerCollectibleTracker] Inizializzato con {totalPresents} presents");
        }
        
        // Evento combinato
        if (trackCombinedProgress)
        {
            UpdateCombinedProgress();
        }
    }
    
    private void CountCollectiblesInScene()
    {
        // Nuovo metodo: usa la classe Collectibles invece dei tag
        Collectibles[] allCollectibles = FindObjectsByType<Collectibles>(FindObjectsSortMode.None);
        int memoryCount = 0;
        int presentCount = 0;
        
        foreach (Collectibles collectible in allCollectibles)
        {
            if (!collectible.IsCollected())
            {
                if (collectible.GetCollectibleType() == CollectibleType.Memory)
                {
                    memoryCount++;
                }
                else if (collectible.GetCollectibleType() == CollectibleType.Present)
                {
                    presentCount++;
                }
            }
        }
        
        totalMemories = memoryCount;
        totalPresents = presentCount;
        Debug.Log($"[PlayerCollectibleTracker] Contati con Collectibles: {totalMemories} memorie, {totalPresents} presents");
        
        // Fallback con tag se nessun collectible trovato
        if (totalMemories == 0 && totalPresents == 0)
        {
            GameObject[] taggedMemories = GameObject.FindGameObjectsWithTag("Memories");
            GameObject[] taggedPresents = GameObject.FindGameObjectsWithTag("Present");
            totalMemories = taggedMemories.Length;
            totalPresents = taggedPresents.Length;
            Debug.Log($"[PlayerCollectibleTracker] Fallback con tag: {totalMemories} memorie, {totalPresents} presents");
        }
    }
    
    private void ConnectToSceneManager()
    {
        if (SceneManager01.Instance != null)
        {
            // Collegati agli eventi del SceneManager
            SceneManager01.Instance.OnMemoryCountChanged.AddListener(OnSceneManagerMemoryUpdate);
            SceneManager01.Instance.OnPresentCountChanged.AddListener(OnSceneManagerPresentUpdate);
            SceneManager01.Instance.OnAllMemoriesCollected.AddListener(OnSceneManagerAllMemoriesCollected);
            SceneManager01.Instance.OnAllPresentsCollected.AddListener(OnSceneManagerAllPresentsCollected);
            SceneManager01.Instance.OnAllCollectiblesCompleted.AddListener(OnSceneManagerAllCollectiblesCompleted);
            
            Debug.Log("[PlayerCollectibleTracker] Connesso al SceneManager01");
        }
    }
    
    private void OnSceneManagerMemoryUpdate(int current, int total)
    {
        // Sincronizza con i dati del SceneManager
        collectedMemories = current;
        totalMemories = total;
        
        // Rilancia gli eventi per la UI
        OnMemoryCollected?.Invoke(collectedMemories, totalMemories);
        
        Debug.Log($"[PlayerCollectibleTracker] Memorie sincronizzate: {collectedMemories}/{totalMemories}");
        
        if (trackCombinedProgress)
        {
            UpdateCombinedProgress();
        }
    }
    
    private void OnSceneManagerPresentUpdate(int current, int total)
    {
        // Sincronizza con i dati del SceneManager
        collectedPresents = current;
        totalPresents = total;
        
        // Rilancia gli eventi per la UI
        OnPresentCollected?.Invoke(collectedPresents, totalPresents);
        
        Debug.Log($"[PlayerCollectibleTracker] Presents sincronizzati: {collectedPresents}/{totalPresents}");
        
        if (trackCombinedProgress)
        {
            UpdateCombinedProgress();
        }
    }
    
    private void OnSceneManagerAllMemoriesCollected()
    {
        Debug.Log("[PlayerCollectibleTracker] Tutte le memorie completate (dal SceneManager)");
        OnAllMemoriesCollected?.Invoke();
        CheckAllCollectiblesCompletion();
    }
    
    private void OnSceneManagerAllPresentsCollected()
    {
        Debug.Log("[PlayerCollectibleTracker] Tutti i presents completati (dal SceneManager)");
        OnAllPresentsCollected?.Invoke();
        CheckAllCollectiblesCompletion();
    }
    
    private void OnSceneManagerAllCollectiblesCompleted()
    {
        Debug.Log("[PlayerCollectibleTracker] Tutti i collectibles completati (dal SceneManager)");
        OnAllCollectiblesCompleted?.Invoke();
    }
    
    private void UpdateCombinedProgress()
    {
        // Evento dettagliato con tutti i dati
        OnCollectibleUpdate?.Invoke(collectedMemories, totalMemories, collectedPresents, totalPresents);
        
        // Evento semplificato con totali combinati
        int totalCollected = collectedMemories + collectedPresents;
        int totalAvailable = totalMemories + totalPresents;
        OnTotalCollectibleUpdate?.Invoke(totalCollected, totalAvailable);
    }
    
    private void CheckAllCollectiblesCompletion()
    {
        bool memoriesComplete = totalMemories == 0 || collectedMemories >= totalMemories;
        bool presentsComplete = totalPresents == 0 || collectedPresents >= totalPresents;
        
        if (memoriesComplete && presentsComplete && (totalMemories > 0 || totalPresents > 0))
        {
            Debug.Log("[PlayerCollectibleTracker] TUTTI i collectibles completati!");
            OnAllCollectiblesCompleted?.Invoke();
            
            // Notifica il SceneManager del completamento totale
            if (SceneManager01.Instance != null)
            {
                SceneManager01.Instance.OnAllCollectiblesCompletedByTracker();
                Debug.Log("[PlayerCollectibleTracker] SceneManager01 notificato del completamento totale");
            }
        }
    }

    // METODI PUBBLICI chiamati dalla classe Collectibles
    public void NotifyMemoryCollected()
    {
        // Incrementa sempre il contatore locale
        collectedMemories++;
        
        Debug.Log($"[PlayerCollectibleTracker] Memoria raccolta! {collectedMemories}/{totalMemories}");
        
        // Notifica la UI direttamente
        OnMemoryCollected?.Invoke(collectedMemories, totalMemories);
        
        // Notifica il SceneManager01 se disponibile
        if (SceneManager01.Instance != null)
        {
            SceneManager01.Instance.OnMemoryCollectedByTracker(collectedMemories, totalMemories);
            Debug.Log("[PlayerCollectibleTracker] SceneManager01 notificato della raccolta memory");
        }
        
        // Controlla se hai raccolto tutte le memorie
        if (collectedMemories >= totalMemories && totalMemories > 0)
        {
            OnAllMemoriesCollected?.Invoke();
            
            // Notifica il SceneManager del completamento
            if (SceneManager01.Instance != null)
            {
                SceneManager01.Instance.OnAllMemoriesCompletedByTracker();
            }
        }
        
        if (trackCombinedProgress)
        {
            UpdateCombinedProgress();
        }
        
        CheckAllCollectiblesCompletion();
    }
    
    public void NotifyPresentCollected()
    {
        // Incrementa sempre il contatore locale
        collectedPresents++;
        
        Debug.Log($"[PlayerCollectibleTracker] Present raccolto! {collectedPresents}/{totalPresents}");
        
        // Notifica la UI direttamente
        OnPresentCollected?.Invoke(collectedPresents, totalPresents);
        
        // Notifica il SceneManager01 se disponibile
        if (SceneManager01.Instance != null)
        {
            SceneManager01.Instance.OnPresentCollectedByTracker(collectedPresents, totalPresents);
            Debug.Log("[PlayerCollectibleTracker] SceneManager01 notificato della raccolta present");
        }
        
        // Controlla se hai raccolto tutti i presents
        if (collectedPresents >= totalPresents && totalPresents > 0)
        {
            OnAllPresentsCollected?.Invoke();
            
            // Notifica il SceneManager del completamento
            if (SceneManager01.Instance != null)
            {
                SceneManager01.Instance.OnAllPresentsCompletedByTracker();
            }
        }
        
        if (trackCombinedProgress)
        {
            UpdateCombinedProgress();
        }
        
        CheckAllCollectiblesCompletion();
    }
    
    public void NotifyCollectibleCollected(CollectibleType type)
    {
        if (type == CollectibleType.Memory)
        {
            NotifyMemoryCollected();
        }
        else if (type == CollectibleType.Present)
        {
            NotifyPresentCollected();
        }
    }
    
    // ========== METODI PUBBLICI - MEMORY ==========
    public int GetCollectedMemories() => collectedMemories;
    public int GetTotalMemories() => totalMemories;
    public float GetMemoryProgress() => totalMemories > 0 ? (float)collectedMemories / totalMemories : 0f;
    public bool AreAllMemoriesCollected() => collectedMemories >= totalMemories && totalMemories > 0;
    
    // ========== METODI PUBBLICI - PRESENT ==========
    public int GetCollectedPresents() => collectedPresents;
    public int GetTotalPresents() => totalPresents;
    public float GetPresentProgress() => totalPresents > 0 ? (float)collectedPresents / totalPresents : 0f;
    public bool AreAllPresentsCollected() => collectedPresents >= totalPresents && totalPresents > 0;
    
    // ========== METODI PUBBLICI - COMBINATI ==========
    public int GetTotalCollected() => collectedMemories + collectedPresents;
    public int GetTotalAvailable() => totalMemories + totalPresents;
    public float GetOverallProgress() 
    {
        int total = GetTotalAvailable();
        return total > 0 ? (float)GetTotalCollected() / total : 0f;
    }
    public bool AreAllCollectiblesCompleted()
    {
        return AreAllMemoriesCollected() && AreAllPresentsCollected();
    }
    
    // ========== METODI per cambio scena ==========
    public void OnSceneChanged()
    {
        Debug.Log("[PlayerCollectibleTracker] Cambio scena rilevato - reinizializzo");
        InitializeCollectibles();
    }
    
    public void ResetForNewScene()
    {
        collectedMemories = 0;
        collectedPresents = 0;
        totalMemories = 0;
        totalPresents = 0;
        InitializeCollectibles();
    }
    
    // ========== METODI di configurazione ==========
    public void SetSceneManagerIntegration(bool enabled)
    {
        useSceneManagerIntegration = enabled;
        if (enabled && SceneManager01.Instance != null)
        {
            ConnectToSceneManager();
        }
    }
    
    public void SetAutoFindCollectibles(bool enabled)
    {
        autoFindCollectiblesInScene = enabled;
    }
    
    public void SetCombinedTracking(bool enabled)
    {
        trackCombinedProgress = enabled;
        if (enabled)
        {
            UpdateCombinedProgress();
        }
    }
    
    // ========== RESET E DEBUG ==========
    public void ResetMemories()
    {
        collectedMemories = 0;
        OnMemoryCollected?.Invoke(collectedMemories, totalMemories);
        if (trackCombinedProgress) UpdateCombinedProgress();
    }
    
    public void ResetPresents()
    {
        collectedPresents = 0;
        OnPresentCollected?.Invoke(collectedPresents, totalPresents);
        if (trackCombinedProgress) UpdateCombinedProgress();
    }
    
    public void ResetAll()
    {
        ResetMemories();
        ResetPresents();
    }
    
    // Cleanup degli eventi
    private void OnDestroy()
    {
        if (SceneManager01.Instance != null)
        {
            SceneManager01.Instance.OnMemoryCountChanged.RemoveListener(OnSceneManagerMemoryUpdate);
            SceneManager01.Instance.OnPresentCountChanged.RemoveListener(OnSceneManagerPresentUpdate);
            SceneManager01.Instance.OnAllMemoriesCollected.RemoveListener(OnSceneManagerAllMemoriesCollected);
            SceneManager01.Instance.OnAllPresentsCollected.RemoveListener(OnSceneManagerAllPresentsCollected);
            SceneManager01.Instance.OnAllCollectiblesCompleted.RemoveListener(OnSceneManagerAllCollectiblesCompleted);
        }
        
        // Pulizia del singleton solo se questo è l'instance corrente
        if (Instance == this)
        {
            Instance = null;
        }
    }
    
    // Metodo per debug nell'inspector
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugCurrentState()
    {
        Debug.Log($"=== PlayerCollectibleTracker Debug ===\n" +
                  $"Memories: {collectedMemories}/{totalMemories} ({GetMemoryProgress():P2})\n" +
                  $"Presents: {collectedPresents}/{totalPresents} ({GetPresentProgress():P2})\n" +
                  $"Overall: {GetTotalCollected()}/{GetTotalAvailable()} ({GetOverallProgress():P2})\n" +
                  $"Use SceneManager: {useSceneManagerIntegration}\n" +
                  $"Auto Find: {autoFindCollectiblesInScene}\n" +
                  $"Combined Tracking: {trackCombinedProgress}\n" +
                  $"All Completed: {AreAllCollectiblesCompleted()}");
    }
}