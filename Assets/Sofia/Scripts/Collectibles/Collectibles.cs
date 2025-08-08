using UnityEngine;

// Enum per i tipi di collectible (assicurati che questo esista)
public enum CollectibleType
{
    Memory,
    Present,
    Checkpoint
}

public class Collectibles : MonoBehaviour
{
    [Header("Collectible Settings")]
    [SerializeField] private string collectibleName;
    [SerializeField] private CollectibleType collectibleType;
    [SerializeField] private bool isCollected = false;
    
    [Header("Visual/Audio Feedback")]
    [SerializeField] private GameObject collectEffect;
    [SerializeField] private AudioClip collectSound;
    [SerializeField] private float effectDuration = 1f;
    
    [Header("UI Feedback")]
    [SerializeField] private string displayMessage = "";
    
    private void Start()
    {
        // Auto-assign name se non impostato
        if (string.IsNullOrEmpty(collectibleName))
        {
            collectibleName = gameObject.name;
        }
        
        // Auto-assign display message se non impostato
        if (string.IsNullOrEmpty(displayMessage))
        {
            displayMessage = $"{collectibleType} raccolto!";
        }
        
        // Registra questo collectible con lo SceneManager se presente
        RegisterWithSceneManager();
    }
    
    private void RegisterWithSceneManager()
    {
        // Cerca lo SceneManager appropriato nella scena corrente
        if (SceneManager01.Instance != null)
        {
            SceneManager01.Instance.RegisterCollectible(this);
        }
        
        // Potresti aggiungere altri SceneManager qui per le altre scene
        // SceneManager00, SceneManager02, etc.
    }
    
    // ========== INTERAZIONE COLLECTIBLE ==========
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isCollected)
        {
            CollectItem();
        }
    }
    
    private void OnMouseDown()
    {
        if (!isCollected)
        {
            CollectItem();
        }
    }
    
    // Metodo principale per raccogliere l'oggetto
    public void CollectItem()
    {
        if (isCollected)
        {
            Debug.LogWarning($"Collectible {collectibleName} già raccolto!");
            return;
        }
        
        isCollected = true;
        
        // Feedback visivo/audio
        PlayFeedback();
        
        // Notifica allo SceneManager
        NotifySceneManager();
        
        // Nascondi o disattiva l'oggetto
        StartCoroutine(DisableAfterEffect());
    }
    
    private void PlayFeedback()
    {
        // Effetto visivo
        if (collectEffect != null)
        {
            GameObject effect = Instantiate(collectEffect, transform.position, Quaternion.identity);
            Destroy(effect, effectDuration);
        }
        
        // Suono
        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position);
        }
        
        // Messaggio UI (opzionale - potresti avere un sistema di notifiche)
        if (!string.IsNullOrEmpty(displayMessage))
        {
            Debug.Log($"[Collectible] {displayMessage}");
            // Qui potresti chiamare un sistema di UI per mostrare il messaggio
            // UIManager.Instance?.ShowMessage(displayMessage);
        }
    }
    
    private void NotifySceneManager()
    {
        // Notifica allo SceneManager appropriato
        if (SceneManager01.Instance != null)
        {
            SceneManager01.Instance.OnCollectibleCollected(collectibleName, collectibleType);
        }
        
        // Per altre scene, potresti aggiungere:
        // else if (SceneManager00.Instance != null)
        // {
        //     SceneManager00.Instance.OnCollectibleCollected(collectibleName, collectibleType);
        // }
        // else if (SceneManager02.Instance != null)
        // {
        //     SceneManager02.Instance.OnCollectibleCollected(collectibleName, collectibleType);
        // }
        
        Debug.Log($"[Collectible] {collectibleType} '{collectibleName}' raccolto e notificato allo SceneManager");
    }
    
    private System.Collections.IEnumerator DisableAfterEffect()
    {
        // Aspetta che l'effetto finisca
        yield return new WaitForSeconds(effectDuration * 0.5f);
        
        // Disattiva l'oggetto
        gameObject.SetActive(false);
    }
    
    // ========== GETTERS PUBBLICI ==========
    
    public string GetName() => collectibleName;
    public CollectibleType GetCollectibleType() => collectibleType;
    public bool IsCollected() => isCollected;
    public string GetDisplayMessage() => displayMessage;
    
    // ========== SETTERS (per editor o sistemi esterni) ==========
    
    public void SetCollectibleName(string name)
    {
        collectibleName = name;
    }
    
    public void SetCollectibleType(CollectibleType type)
    {
        collectibleType = type;
    }
    
    public void SetDisplayMessage(string message)
    {
        displayMessage = message;
    }
    
    // ========== METODI DI UTILITÀ ==========
    
    /// <summary>
    /// Forza la raccolta dell'oggetto (utile per testing o eventi speciali)
    /// </summary>
    public void ForceCollect()
    {
        if (!isCollected)
        {
            CollectItem();
        }
    }
    
    /// <summary>
    /// Reset dello stato raccolto (utile per testing)
    /// </summary>
    public void ResetCollected()
    {
        isCollected = false;
        gameObject.SetActive(true);
        Debug.Log($"[Collectible] {collectibleName} resetato");
    }
    
    /// <summary>
    /// Metodo per nascondere l'oggetto se già raccolto (chiamato dal SceneManager)
    /// </summary>
    public void HideIfCollected()
    {
        if (isCollected)
        {
            gameObject.SetActive(false);
        }
    }
    
    // ========== METODI PER COMPATIBILITÀ CON VECCHIO CODICE ==========
    
    // Se il tuo vecchio codice chiamava metodi diversi, mantienili qui
    public void OnMemoryCollected()
    {
        if (collectibleType == CollectibleType.Memory)
        {
            CollectItem();
        }
    }
    
    public void OnPresentCollected()
    {
        if (collectibleType == CollectibleType.Present)
        {
            CollectItem();
        }
    }
    
    // ========== DEBUG ==========
    
    [ContextMenu("Force Collect")]
    public void DebugForceCollect()
    {
        ForceCollect();
    }
    
    [ContextMenu("Reset Collected")]
    public void DebugResetCollected()
    {
        ResetCollected();
    }
    
    [ContextMenu("Debug Info")]
    public void DebugInfo()
    {
        Debug.Log($"=== Collectible Info ===\n" +
                  $"Name: {collectibleName}\n" +
                  $"Type: {collectibleType}\n" +
                  $"Collected: {isCollected}\n" +
                  $"Display Message: {displayMessage}");
    }
}

// Script alternativo semplificato per oggetti che si comportano diversamente
public class SimpleCollectible : MonoBehaviour
{
    [Header("Basic Settings")]
    [SerializeField] private CollectibleType type;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Collect();
        }
    }
    
    private void OnMouseDown()
    {
        Collect();
    }
    
    private void Collect()
    {
        // Notifica diretta allo SceneManager
        if (SceneManager01.Instance != null)
        {
            switch (type)
            {
                case CollectibleType.Memory:
                    SceneManager01.Instance.NotifyMemoryCollected(gameObject.name);
                    break;
                case CollectibleType.Present:
                    SceneManager01.Instance.NotifyPresentCollected(gameObject.name);
                    break;
            }
        }
        
        // Nascondi immediatamente
        gameObject.SetActive(false);
    }
}