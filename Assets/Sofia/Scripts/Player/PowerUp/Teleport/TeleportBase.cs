using UnityEngine;

public class TeleportBase : MonoBehaviour
{
    [Header("Debug")]
    public bool showDebugLogs = false;
    
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
    
    // Metodo chiamato quando il player si teletrasporta su questa base
    public virtual void OnPlayerTeleported()
    {
        if (showDebugLogs)
        {
            Debug.Log($"Player si è teletrasportato su: {gameObject.name}");
        }
    }
}