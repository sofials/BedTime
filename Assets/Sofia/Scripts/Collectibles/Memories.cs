
using UnityEngine;

public class Memories : Collectibles
{
    private void Awake()
    {
        // Forza il tipo a Memory e abilita billboard
        SetCollectibleType(CollectibleType.Memory);
    }
    
    private void Start()
    {
        // Registrati al SceneManager01 se disponibile
        if (!IsCollected() && SceneManager01.Instance != null)
        {
            SceneManager01.Instance.RegisterMemory(this);
        }
        
        // Sottoscrivi all'evento di raccolta per logica specifica delle memories
        OnCollected.AddListener(OnMemoryCollected);
    }
    
    private void OnMemoryCollected(Collectibles collectible)
    {
        Debug.Log($"Memory '{GetName()}' raccolta!");
        
        // Notifica il PlayerCollectibleTracker se non usiamo SceneManager
        if (PlayerCollectibleTracker.Instance != null)
        {
            PlayerCollectibleTracker.Instance.NotifyMemoryCollected();
        }
        
        // Logica specifica per le memories (se necessaria)
        // Ad esempio: sbloccare cutscene, aggiornare narrative, etc.
        HandleMemorySpecificLogic();
    }
    
    private void HandleMemorySpecificLogic()
    {
        // Aggiungi qui logica specifica per le memories
        // Es: sblocca narrative, attiva cutscene, etc.
        
        // Esempio:
        // NarrativeManager.Instance?.UnlockMemory(GetName());
        // CutsceneManager.Instance?.TriggerMemoryScene(GetValue());
    }
    
    private void OnDestroy()
    {
        // Cleanup dell'evento
        OnCollected.RemoveListener(OnMemoryCollected);
    }
}