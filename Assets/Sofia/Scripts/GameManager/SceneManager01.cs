using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SceneManager01 : MonoBehaviour
{
    [Header("Collectibles Settings")]
    [SerializeField] private int totalPresents = 0;
    [SerializeField] private int currentPresents = 0;
    [SerializeField] private int totalMemories = 0;
    [SerializeField] private int currentMemories = 0;
    
    [Header("Present Events")]
    public UnityEvent<int, int> OnPresentCountChanged; // current, total
    public UnityEvent OnAllPresentsCollected;
    
    [Header("Memory Events")]
    public UnityEvent<int, int> OnMemoryCountChanged; // current, total
    public UnityEvent OnAllMemoriesCollected;
    
    [Header("Combined Events")]
    public UnityEvent<int, int> OnAllCollectiblesCountChanged; // total collected, total available
    public UnityEvent OnAllCollectiblesCompleted;
    
    [Header("Scene References")]
    [SerializeField] private List<Collectibles> scenePresents = new List<Collectibles>();
    [SerializeField] private List<Collectibles> sceneMemories = new List<Collectibles>();
    [SerializeField] private Transform presentsParent;
    [SerializeField] private Transform memoriesParent;
    
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
        
        InitializeCollectibles();
    }
    
    private void Start()
    {
        // Aggiorna l'UI iniziale
        UpdateUI();
    }
    
    private void InitializeCollectibles()
    {
        // Trova tutti i collectibles nella scena se non sono già assegnati
        if (scenePresents.Count == 0 && sceneMemories.Count == 0)
        {
            FindAllCollectibles();
        }
        
        totalPresents = scenePresents.Count;
        totalMemories = sceneMemories.Count;
        
        // Registra eventi per ogni present
        foreach (Collectibles present in scenePresents)
        {
            if (present != null)
            {
                present.OnCollected.AddListener(OnCollectibleCollected);
            }
        }
        
        // Registra eventi per ogni memory
        foreach (Collectibles memory in sceneMemories)
        {
            if (memory != null)
            {
                memory.OnCollected.AddListener(OnCollectibleCollected);
            }
        }
        
        Debug.Log($"[SceneManager01] Inizializzati {totalPresents} presents e {totalMemories} memories");
    }
    
    private void FindAllCollectibles()
    {
        // Trova tutti i collectibles nella scena
        Collectibles[] allCollectibles = FindObjectsByType<Collectibles>(FindObjectsSortMode.None);
        
        foreach (Collectibles collectible in allCollectibles)
        {
            if (collectible.GetCollectibleType() == CollectibleType.Present)
            {
                // Controlla se è nel parent specifico dei presents
                if (presentsParent == null || collectible.transform.IsChildOf(presentsParent))
                {
                    scenePresents.Add(collectible);
                }
            }
            else if (collectible.GetCollectibleType() == CollectibleType.Memory)
            {
                // Controlla se è nel parent specifico delle memories
                if (memoriesParent == null || collectible.transform.IsChildOf(memoriesParent))
                {
                    sceneMemories.Add(collectible);
                }
            }
        }
    }
    
    private void OnCollectibleCollected(Collectibles collectible)
    {
        if (collectible == null) return;
        
        if (collectible.GetCollectibleType() == CollectibleType.Present)
        {
            HandlePresentCollected(collectible);
        }
        else if (collectible.GetCollectibleType() == CollectibleType.Memory)
        {
            HandleMemoryCollected(collectible);
        }
        
        // Aggiorna UI e controlla completamento totale
        UpdateUI();
        CheckAllCollectiblesCompletion();
    }
    
    private void HandlePresentCollected(Collectibles present)
    {
        if (scenePresents.Contains(present))
        {
            currentPresents++;
            scenePresents.Remove(present);
            
            OnPresentCountChanged?.Invoke(currentPresents, totalPresents);
            
            if (currentPresents >= totalPresents)
            {
                OnAllPresentsCompleted();
            }
            
            Debug.Log($"[SceneManager01] Present '{present.GetName()}' raccolto! Progresso: {currentPresents}/{totalPresents}");
        }
    }
    
    private void HandleMemoryCollected(Collectibles memory)
    {
        if (sceneMemories.Contains(memory))
        {
            currentMemories++;
            sceneMemories.Remove(memory);
            
            OnMemoryCountChanged?.Invoke(currentMemories, totalMemories);
            
            if (currentMemories >= totalMemories)
            {
                OnAllMemoriesCompleted();
            }
            
            Debug.Log($"[SceneManager01] Memory '{memory.GetName()}' raccolta! Progresso: {currentMemories}/{totalMemories}");
        }
    }
    
    private void OnAllPresentsCompleted()
    {
        Debug.Log("[SceneManager01] Tutti i presents sono stati raccolti!");
        OnAllPresentsCollected?.Invoke();
    }
    
    private void OnAllMemoriesCompleted()
    {
        Debug.Log("[SceneManager01] Tutte le memories sono state raccolte!");
        OnAllMemoriesCollected?.Invoke();
    }
    
    private void CheckAllCollectiblesCompletion()
    {
        bool allCompleted = (currentPresents >= totalPresents) && (currentMemories >= totalMemories);
        
        if (allCompleted && (totalPresents > 0 || totalMemories > 0))
        {
            Debug.Log("[SceneManager01] TUTTI i collectibles completati!");
            OnAllCollectiblesCompleted?.Invoke();
        }
    }
    
    private void UpdateUI()
    {
        int totalCollected = currentPresents + currentMemories;
        int totalAvailable = totalPresents + totalMemories;
        
        OnAllCollectiblesCountChanged?.Invoke(totalCollected, totalAvailable);
    }
    
    // ========== METODI PUBBLICI - PRESENTS ==========
    
    public int GetCurrentPresents() => currentPresents;
    public int GetTotalPresents() => totalPresents;
    public float GetPresentsCompletionPercentage()
    {
        if (totalPresents == 0) return 0f;
        return (float)currentPresents / totalPresents * 100f;
    }
    public bool AreAllPresentsCollected() => currentPresents >= totalPresents;
    
    // ========== METODI PUBBLICI - MEMORIES ==========
    
    public int GetCurrentMemories() => currentMemories;
    public int GetTotalMemories() => totalMemories;
    public float GetMemoriesCompletionPercentage()
    {
        if (totalMemories == 0) return 0f;
        return (float)currentMemories / totalMemories * 100f;
    }
    public bool AreAllMemoriesCollected() => currentMemories >= totalMemories;
    
    // ========== METODI PUBBLICI - COMBINATI ==========
    
    public int GetTotalCollected() => currentPresents + currentMemories;
    public int GetTotalAvailable() => totalPresents + totalMemories;
    public float GetOverallCompletionPercentage()
    {
        int total = GetTotalAvailable();
        if (total == 0) return 0f;
        return (float)GetTotalCollected() / total * 100f;
    }
    public bool AreAllCollectiblesCompleted()
    {
        return AreAllPresentsCollected() && AreAllMemoriesCollected();
    }
    
    // ========== REGISTRAZIONE DINAMICA ==========
    
    public void RegisterPresent(Collectibles present)
    {
        if (present != null && present.GetCollectibleType() == CollectibleType.Present && !scenePresents.Contains(present))
        {
            scenePresents.Add(present);
            totalPresents++;
            present.OnCollected.AddListener(OnCollectibleCollected);
            
            UpdateUI();
            Debug.Log($"[SceneManager01] Present '{present.GetName()}' registrato dinamicamente");
        }
    }
    
    public void RegisterMemory(Collectibles memory)
    {
        if (memory != null && memory.GetCollectibleType() == CollectibleType.Memory && !sceneMemories.Contains(memory))
        {
            sceneMemories.Add(memory);
            totalMemories++;
            memory.OnCollected.AddListener(OnCollectibleCollected);
            
            UpdateUI();
            Debug.Log($"[SceneManager01] Memory '{memory.GetName()}' registrata dinamicamente");
        }
    }
    
    public void RegisterCollectible(Collectibles collectible)
    {
        if (collectible == null) return;
        
        if (collectible.GetCollectibleType() == CollectibleType.Present)
        {
            RegisterPresent(collectible);
        }
        else if (collectible.GetCollectibleType() == CollectibleType.Memory)
        {
            RegisterMemory(collectible);
        }
    }
    
    // ========== RESET E DEBUG ==========
    
    public void ResetPresents()
    {
        currentPresents = 0;
        
        foreach (Collectibles present in scenePresents)
        {
            if (present != null)
            {
                present.ResetItem();
            }
        }
        
        UpdateUI();
        Debug.Log("[SceneManager01] Presents resettati");
    }
    
    public void ResetMemories()
    {
        currentMemories = 0;
        
        foreach (Collectibles memory in sceneMemories)
        {
            if (memory != null)
            {
                memory.ResetItem();
            }
        }
        
        UpdateUI();
        Debug.Log("[SceneManager01] Memories resettate");
    }
    
    public void ResetAllCollectibles()
    {
        ResetPresents();
        ResetMemories();
        Debug.Log("[SceneManager01] Tutti i collectibles resettati");
    }
    
    // ========== CLEANUP ==========
    
    private void OnDestroy()
    {
        // Pulisci gli eventi quando l'oggetto viene distrutto
        foreach (Collectibles present in scenePresents)
        {
            if (present != null)
            {
                present.OnCollected.RemoveListener(OnCollectibleCollected);
            }
        }
        
        foreach (Collectibles memory in sceneMemories)
        {
            if (memory != null)
            {
                memory.OnCollected.RemoveListener(OnCollectibleCollected);
            }
        }
    }
}