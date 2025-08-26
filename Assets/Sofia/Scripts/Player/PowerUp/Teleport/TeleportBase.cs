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
    [SerializeField] private Color hoverColor = Color.white;
    
    private Color originalStartColor;
    private bool isHovering = false;
    
    // Riferimento statico alla base attualmente sotto il cursore invisibile
    public static TeleportBase currentHoveredBase = null;
    
    private void Start()
    {
        // Verifica che l'oggetto abbia un collider per il raycast
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning($"TeleportBase '{gameObject.name}' non ha un Collider! Aggiungi un Collider per il funzionamento del teletrasporto.");
        }
        else if (showDebugLogs)
        {
            Debug.Log($"TeleportBase '{gameObject.name}' - Collider: {col.GetType().Name}, Enabled: {col.enabled}");
        }
        
        // Setup particle system
        if (particleSystem == null)
        {
            particleSystem = GetComponent<ParticleSystem>();
        }
        
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
    }

    void Update()
    {
        // Controlla se il cursore invisibile è sopra questa base
        CheckInvisibleCursorHover();
    }

    /// <summary>
    /// Controlla se il cursore invisibile sta facendo hover su questa base
    /// </summary>
    private void CheckInvisibleCursorHover()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        // Raycast dalla posizione del mouse invisibile
        Ray mouseRay = mainCamera.ScreenPointToRay(Input.mousePosition);
        
        // Controlla se il raycast colpisce questo oggetto
        Collider myCollider = GetComponent<Collider>();
        if (myCollider != null && myCollider.Raycast(mouseRay, out RaycastHit hit, Mathf.Infinity))
        {
            // Il cursore invisibile è sopra questa base
            if (!isHovering)
            {
                OnCursorEnter();
            }
        }
        else
        {
            // Il cursore invisibile non è sopra questa base
            if (isHovering && currentHoveredBase == this)
            {
                OnCursorExit();
            }
        }
    }

    /// <summary>
    /// Chiamato quando il cursore invisibile entra su questa base
    /// </summary>
    private void OnCursorEnter()
    {
        if (!isHovering)
        {
            isHovering = true;
            currentHoveredBase = this;
            
            if (particleSystem != null)
            {
                SetParticleStartColor(hoverColor);
            }
            
            if (showDebugLogs)
            {
                Debug.Log($"Cursore invisibile HOVER su '{gameObject.name}' - Colore cambiato");
            }
        }
    }
    
    /// <summary>
    /// Chiamato quando il cursore invisibile esce da questa base
    /// </summary>
    private void OnCursorExit()
    {
        if (isHovering)
        {
            isHovering = false;
            if (currentHoveredBase == this)
                currentHoveredBase = null;
            
            if (particleSystem != null)
            {
                SetParticleStartColor(originalStartColor);
            }
            
            if (showDebugLogs)
            {
                Debug.Log($"Cursore invisibile USCITO da '{gameObject.name}' - Colore ripristinato");
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
            // Rimuovi riferimento se stiamo disattivando questa base
            if (currentHoveredBase == this)
                currentHoveredBase = null;
                
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
    public void SetHoverColor(Color color)
    {
        hoverColor = color;
    }
}