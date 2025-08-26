using UnityEngine;
using UnityEngine.Events;

public class TeleportBase : MonoBehaviour
{
    [Header("Debug")]
    public bool showDebugLogs = false;
    
    [Header("Activation Events")]
    public UnityEvent OnObjectEnabled;
    public UnityEvent OnObjectDisabled;
    
    private void Start()
    {
        // Verifica che l'oggetto abbia un collider per il raycast
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning($"TeleportBase '{gameObject.name}' non ha un Collider! Aggiungi un Collider per il funzionamento del teletrasporto.");
        }
        else
        {
            Debug.Log($"TeleportBase '{gameObject.name}' - Collider: {col.GetType().Name}, Enabled: {col.enabled}, IsTrigger: {col.isTrigger}");
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"TeleportBase '{gameObject.name}' inizializzata. Layer: {LayerMask.LayerToName(gameObject.layer)} (Layer {gameObject.layer})");
        }
    }
    
    /// <summary>
    /// Attiva l'intero GameObject del teleport dall'esterno
    /// </summary>
    public void EnableObject()
    {
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
            OnObjectEnabled?.Invoke();
            
            if (showDebugLogs)
            {
                Debug.Log($"TeleportBase GameObject '{gameObject.name}' attivato");
            }
        }
    }
    
    /// <summary>
    /// Disattiva l'intero GameObject del teleport dall'esterno
    /// </summary>
    public void DisableObject()
    {
        if (gameObject.activeInHierarchy)
        {
            OnObjectDisabled?.Invoke();
            gameObject.SetActive(false);
            
            if (showDebugLogs)
            {
                Debug.Log($"TeleportBase GameObject '{gameObject.name}' disattivato");
            }
        }
    }
    
    /// <summary>
    /// Toggle dello stato dell'oggetto
    /// </summary>
    public void ToggleObject()
    {
        if (gameObject.activeInHierarchy)
            DisableObject();
        else
            EnableObject();
    }
    
    /// <summary>
    /// Imposta lo stato dell'oggetto
    /// </summary>
    /// <param name="active">True per attivare, False per disattivare</param>
    public void SetObjectActive(bool active)
    {
        if (active)
            EnableObject();
        else
            DisableObject();
    }
    
    /// <summary>
    /// Controlla se l'oggetto è attivo
    /// </summary>
    public bool IsObjectActive
    {
        get { return gameObject.activeInHierarchy; }
        set { SetObjectActive(value); }
    }
    
    // Metodo chiamato quando il player si teletrasporta su questa base
    public virtual void OnPlayerTeleported()
    {
        if (showDebugLogs)
        {
            Debug.Log($"Player si è teletrasportato su: {gameObject.name}");
        }
    }
}