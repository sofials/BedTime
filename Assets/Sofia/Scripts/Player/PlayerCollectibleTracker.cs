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
    
    [Header("Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // Eventi per notificare la UI o altri sistemi
    public event Action<int, int> OnMemoryCollected; // collected, total
    public event Action<int, int> OnPresentCollected; // collected, total
    public event Action OnAllMemoriesCollected;
    public event Action OnAllPresentsCollected;
    public event Action OnAllCollectiblesCompleted;
    
    // Singleton per accesso globale
    public static PlayerCollectibleTracker Instance { get; private set; }
    
    void Awake() 
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        InitializeTracker();
    }
    
    private void InitializeTracker()
    {
        // Reset contatori per la scena corrente
        collectedMemories = 0;
        collectedPresents = 0;
        
        // Conta i collectibles presenti nella scena
        CountCollectiblesInScene();
        
        DebugLog($"[PlayerCollectibleTracker] Inizializzato: {totalMemories} memorie, {totalPresents} presents");
    }
    
    private void CountCollectiblesInScene()
    {
        // Conta tutti i collectibles usando la classe Collectibles
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
        
        DebugLog($"[PlayerCollectibleTracker] Contati nella scena: {totalMemories} memorie, {totalPresents} presents");
        
        // Fallback con tag se nessun collectible trovato con la classe
        if (totalMemories == 0 && totalPresents == 0)
        {
            GameObject[] taggedMemories = GameObject.FindGameObjectsWithTag("Memories");
            GameObject[] taggedPresents = GameObject.FindGameObjectsWithTag("Present");
            totalMemories = taggedMemories?.Length ?? 0;
            totalPresents = taggedPresents?.Length ?? 0;
            
            DebugLog($"[PlayerCollectibleTracker] Fallback con tag: {totalMemories} memorie, {totalPresents} presents");
        }
    }

    // METODI PUBBLICI chiamati dalla classe Collectibles
    public void NotifyMemoryCollected()
    {
        collectedMemories++;
        
        DebugLog($"[PlayerCollectibleTracker] Memoria raccolta! {collectedMemories}/{totalMemories}");
        
        // Notifica gli eventi
        OnMemoryCollected?.Invoke(collectedMemories, totalMemories);
        
        // Controlla se tutte le memorie sono state raccolte
        if (collectedMemories >= totalMemories && totalMemories > 0)
        {
            DebugLog("[PlayerCollectibleTracker] Tutte le memorie raccolte!");
            OnAllMemoriesCollected?.Invoke();
            CheckAllCollectiblesCompletion();
        }
    }
    
    public void NotifyPresentCollected()
    {
        collectedPresents++;
        
        DebugLog($"[PlayerCollectibleTracker] Present raccolto! {collectedPresents}/{totalPresents}");
        
        // Notifica gli eventi
        OnPresentCollected?.Invoke(collectedPresents, totalPresents);
        
        // Controlla se tutti i presents sono stati raccolti
        if (collectedPresents >= totalPresents && totalPresents > 0)
        {
            DebugLog("[PlayerCollectibleTracker] Tutti i presents raccolti!");
            OnAllPresentsCollected?.Invoke();
            CheckAllCollectiblesCompletion();
        }
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
    
    private void CheckAllCollectiblesCompletion()
    {
        bool memoriesComplete = totalMemories == 0 || collectedMemories >= totalMemories;
        bool presentsComplete = totalPresents == 0 || collectedPresents >= totalPresents;
        
        if (memoriesComplete && presentsComplete && (totalMemories > 0 || totalPresents > 0))
        {
            DebugLog("[PlayerCollectibleTracker] TUTTI i collectibles completati!");
            OnAllCollectiblesCompleted?.Invoke();
        }
    }
    
    // ========== GETTERS ==========
    
    // Memory getters
    public int GetCollectedMemories() => collectedMemories;
    public int GetTotalMemories() => totalMemories;
    public float GetMemoryProgress() => totalMemories > 0 ? (float)collectedMemories / totalMemories : 0f;
    public bool AreAllMemoriesCollected() => collectedMemories >= totalMemories && totalMemories > 0;
    
    // Present getters
    public int GetCollectedPresents() => collectedPresents;
    public int GetTotalPresents() => totalPresents;
    public float GetPresentProgress() => totalPresents > 0 ? (float)collectedPresents / totalPresents : 0f;
    public bool AreAllPresentsCollected() => collectedPresents >= totalPresents && totalPresents > 0;
    
    // Combined getters
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
    
    // ========== UTILITY METHODS ==========
    
    public void ResetScene()
    {
        DebugLog("[PlayerCollectibleTracker] Reset per nuova scena");
        InitializeTracker();
    }
    
    public void RefreshCounts()
    {
        DebugLog("[PlayerCollectibleTracker] Aggiornamento conteggi");
        CountCollectiblesInScene();
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
        Debug.Log($"=== PlayerCollectibleTracker State ===\n" +
                  $"Memories: {collectedMemories}/{totalMemories} ({GetMemoryProgress():P1})\n" +
                  $"Presents: {collectedPresents}/{totalPresents} ({GetPresentProgress():P1})\n" +
                  $"Total: {GetTotalCollected()}/{GetTotalAvailable()} ({GetOverallProgress():P1})\n" +
                  $"All Complete: {AreAllCollectiblesCompleted()}");
    }
    
    [ContextMenu("Refresh Collectibles Count")]
    public void DebugRefreshCounts()
    {
        RefreshCounts();
        DebugCurrentState();
    }
    
    // ========== CLEANUP ==========
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        
        DebugLog("[PlayerCollectibleTracker] Cleanup completato");
    }
}