using UnityEngine;
using UnityEngine.Events;

public class SceneManager01 : MonoBehaviour
{
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
    
    // Singleton pattern
    public static SceneManager01 Instance { get; private set; }
    
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
        
        InitializeScene();
    }
    
    private void Start()
    {
        // Aggiorna l'UI iniziale
        UpdateUI();
    }
    
    private void InitializeScene()
    {
        // Reset contatori per la scena corrente
        collectedPresents = 0;
        collectedMemories = 0;
        
        // Conta gli oggetti usando i tag
        CountCollectiblesByTags();
        
        DebugLog($"[SceneManager01] Scena inizializzata: {totalPresents} presents, {totalMemories} memories");
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
    
    public void NotifyPresentCollected()
    {
        collectedPresents++;
        
        DebugLog($"[SceneManager01] Present raccolto! Progresso: {collectedPresents}/{totalPresents}");
        
        // Eventi per la UI
        OnPresentCountChanged?.Invoke(collectedPresents, totalPresents);
        
        // Controlla se tutti i presents sono stati raccolti
        if (collectedPresents >= totalPresents && totalPresents > 0)
        {
            DebugLog("[SceneManager01] Tutti i presents raccolti!");
            OnAllPresentsCollected?.Invoke();
            CheckAllCollectiblesCompletion();
        }
        
        UpdateUI();
    }
    
    public void NotifyMemoryCollected()
    {
        collectedMemories++;
        
        DebugLog($"[SceneManager01] Memory raccolta! Progresso: {collectedMemories}/{totalMemories}");
        
        // Eventi per la UI
        OnMemoryCountChanged?.Invoke(collectedMemories, totalMemories);
        
        // Controlla se tutte le memories sono state raccolte
        if (collectedMemories >= totalMemories && totalMemories > 0)
        {
            DebugLog("[SceneManager01] Tutte le memories raccolte!");
            OnAllMemoriesCollected?.Invoke();
            CheckAllCollectiblesCompletion();
        }
        
        UpdateUI();
    }
    
    private void CheckAllCollectiblesCompletion()
    {
        bool presentsComplete = totalPresents == 0 || collectedPresents >= totalPresents;
        bool memoriesComplete = totalMemories == 0 || collectedMemories >= totalMemories;
        
        if (presentsComplete && memoriesComplete && (totalPresents > 0 || totalMemories > 0))
        {
            DebugLog("[SceneManager01] TUTTI i collectibles completati!");
            OnAllCollectiblesCompleted?.Invoke();
        }
    }
    
    private void UpdateUI()
    {
        int totalCollected = collectedPresents + collectedMemories;
        int totalAvailable = totalPresents + totalMemories;
        
        OnAllCollectiblesCountChanged?.Invoke(totalCollected, totalAvailable);
    }
    
    // ========== GETTERS - PRESENTS ==========
    
    public int GetCollectedPresents() => collectedPresents;
    public int GetTotalPresents() => totalPresents;
    public float GetPresentsProgress() => totalPresents > 0 ? (float)collectedPresents / totalPresents : 0f;
    public float GetPresentsCompletionPercentage() => GetPresentsProgress() * 100f;
    public bool AreAllPresentsCollected() => collectedPresents >= totalPresents && totalPresents > 0;
    
    // ========== GETTERS - MEMORIES ==========
    
    public int GetCollectedMemories() => collectedMemories;
    public int GetTotalMemories() => totalMemories;
    public float GetMemoriesProgress() => totalMemories > 0 ? (float)collectedMemories / totalMemories : 0f;
    public float GetMemoriesCompletionPercentage() => GetMemoriesProgress() * 100f;
    public bool AreAllMemoriesCollected() => collectedMemories >= totalMemories && totalMemories > 0;
    
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
        UpdateUI();
    }
    
    public void ResetSceneProgress()
    {
        DebugLog("[SceneManager01] Reset progresso scena");
        collectedPresents = 0;
        collectedMemories = 0;
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
        Debug.Log($"=== SceneManager01 State ===\n" +
                  $"Presents: {collectedPresents}/{totalPresents} ({GetPresentsCompletionPercentage():F1}%)\n" +
                  $"Memories: {collectedMemories}/{totalMemories} ({GetMemoriesCompletionPercentage():F1}%)\n" +
                  $"Total: {GetTotalCollected()}/{GetTotalAvailable()} ({GetOverallCompletionPercentage():F1}%)\n" +
                  $"All Complete: {AreAllCollectiblesCompleted()}");
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
        
        DebugLog("[SceneManager01] Cleanup completato");
    }
}