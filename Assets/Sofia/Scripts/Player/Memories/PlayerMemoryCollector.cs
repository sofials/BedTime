using UnityEngine;
using System;

public class PlayerMemoryCollector : MonoBehaviour
{
    [Header("Memory Collection")]
    public int totalMemories = 0;
    public int collectedMemories = 0;
    
    // Eventi per la UI
    public event Action<int, int> OnMemoryCollected; // collected, total
    public event Action<int> OnMemoriesInitialized; // total

    void Awake() 
    {
        // Conta tutte le memorie presenti nella scena all'inizio
        GameObject[] allMemories = GameObject.FindGameObjectsWithTag("Memories");
        totalMemories = allMemories.Length;
        
        Debug.Log($"[PlayerMemoryCollector] Trovate {totalMemories} memorie nella scena");
        
        // Notifica la UI del totale
        OnMemoriesInitialized?.Invoke(totalMemories);
    }

    // METODO PUBBLICO chiamato da Memories.cs
    public void NotifyMemoryCollected()
    {
        // Incrementa il contatore
        collectedMemories++;
        
        Debug.Log($"[PlayerMemoryCollector] Memoria raccolta! {collectedMemories}/{totalMemories}");
        
        // Notifica la UI
        OnMemoryCollected?.Invoke(collectedMemories, totalMemories);
        
        // Controlla se hai raccolto tutte le memorie
        if (collectedMemories >= totalMemories)
        {
            OnAllMemoriesCollected();
        }
    }

    private void OnAllMemoriesCollected()
    {
        Debug.Log("[PlayerMemoryCollector] Tutte le memorie sono state raccolte!");
        // Logica quando tutte le memorie sono raccolte
    }

    public int GetCollectedMemories() => collectedMemories;
    public int GetTotalMemories() => totalMemories;
    public float GetMemoryProgress() => totalMemories > 0 ? (float)collectedMemories / totalMemories : 0f;
}