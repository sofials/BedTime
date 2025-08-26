using UnityEngine;
using UnityEngine.Events;

public class TeleportBase : MonoBehaviour
{
    [Header("Debug")]
    public bool showDebugLogs = false;
    
    [Header("Activation Events")]
    public UnityEvent OnObjectEnabled;
    public UnityEvent OnObjectDisabled;
    
    [Header("Particle System Hover")]
    [SerializeField] private new ParticleSystem particleSystem;
    [SerializeField] private Color hoverColor = Color.white; // FFFFFF
    
    private Color originalStartColor;
    private bool isHovering = false;
    
    // Riferimento statico alla base attualmente sotto hover
    public static TeleportBase currentHoveredBase = null;
    
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
        
        // Trova il particle system se non è assegnato
        if (particleSystem == null)
        {
            particleSystem = GetComponent<ParticleSystem>();
        }
        
        // Salva il colore originale del particle system
        if (particleSystem != null)
        {
            var main = particleSystem.main;
            originalStartColor = main.startColor.color;
            
            if (showDebugLogs)
            {
                Debug.Log($"TeleportBase '{gameObject.name}' - Particle System trovato. Colore originale: {originalStartColor}");
            }
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning($"TeleportBase '{gameObject.name}' - Nessun Particle System trovato!");
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"TeleportBase '{gameObject.name}' inizializzata. Layer: {LayerMask.LayerToName(gameObject.layer)} (Layer {gameObject.layer})");
        }
    }
    
    private void OnMouseEnter()
    {
        if (!isHovering && particleSystem != null)
        {
            isHovering = true;
            currentHoveredBase = this; // Imposta questa base come quella sotto hover
            SetParticleStartColor(hoverColor);
            
            if (showDebugLogs)
            {
                Debug.Log($"Mouse hover iniziato su '{gameObject.name}' - Colore cambiato a bianco");
            }
        }
    }
    
    private void OnMouseExit()
    {
        if (isHovering && particleSystem != null)
        {
            isHovering = false;
            if (currentHoveredBase == this)
                currentHoveredBase = null; // Rimuovi il riferimento se è questa base
            SetParticleStartColor(originalStartColor);
            
            if (showDebugLogs)
            {
                Debug.Log($"Mouse hover terminato su '{gameObject.name}' - Colore ripristinato");
            }
        }
    }
    
    private void SetParticleStartColor(Color color)
    {
        if (particleSystem != null)
        {
            var main = particleSystem.main;
            main.startColor = color;
        }
    }
    
    /// <summary>
    /// Ottieni la posizione di teletrasporto per questa base
    /// </summary>
    public Vector3 GetTeleportPosition()
    {
        // Puoi personalizzare questo metodo per ogni base se necessario
        // Per esempio, potresti avere un Transform specifico come punto di spawn
        return transform.position;
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
    
    /// <summary>
    /// Imposta manualmente il riferimento al particle system
    /// </summary>
    /// <param name="ps">Il particle system da utilizzare</param>
    public void SetParticleSystem(ParticleSystem ps)
    {
        particleSystem = ps;
        if (particleSystem != null)
        {
            var main = particleSystem.main;
            originalStartColor = main.startColor.color;
        }
    }
    
    /// <summary>
    /// Imposta il colore hover personalizzato
    /// </summary>
    /// <param name="color">Il colore da usare durante l'hover</param>
    public void SetHoverColor(Color color)
    {
        hoverColor = color;
    }
}