using UnityEngine;
using UnityEngine.Events;
 using System.Collections;

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

    [Header("Hover Detection")]
    [Tooltip("Raggio per rilevare quando il player è sopra questa base (più piccolo del collider principale)")]
    [SerializeField] private float hoverDetectionRadius = 12f;
    [Header("Teleport Connection")]
[Tooltip("La base gemella a cui teletrasportarsi quando si è sopra questa base")]
public TeleportBase linkedBase; // Base collegata per il teletrasporto bidirezionale
    private Color originalStartColor;
    private bool isHovering = false;
    
    // Riferimento statico alla base attualmente inquadrata dalla camera
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

    /// <summary>
    /// Chiamato quando la camera inquadra questa base (dal sistema TeleportAbility)
    /// </summary>
    public void OnCursorEnter()
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
                Debug.Log($"Camera INQUADRA '{gameObject.name}' - Colore particelle cambiato a hover");
            }
        }
    }
    
    /// <summary>
    /// Chiamato quando la camera smette di inquadrare questa base (dal sistema TeleportAbility)
    /// </summary>
    public void OnCursorExit()
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
                Debug.Log($"Camera NON INQUADRA PIU' '{gameObject.name}' - Colore particelle ripristinato");
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
    /// Controlla se il player è nel raggio di hover
    /// </summary>
    public bool IsPlayerInHoverRange(Vector3 playerPosition)
    {
        float distance = Vector3.Distance(transform.position, playerPosition);
        return distance <= hoverDetectionRadius;
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
        
        // NUOVO: Controlla se il player è già dentro il trigger
        StartCoroutine(CheckForPlayerInside());
        
        if (showDebugLogs)
        {
            Debug.Log($"TeleportBase GameObject '{gameObject.name}' attivato");
        }
    }
}
private IEnumerator CheckForPlayerInside()
{
    yield return new WaitForFixedUpdate(); // Aspetta che la fisica si aggiorni
    
    Collider col = GetComponent<Collider>();
    if (col != null)
    {
        // Trova tutti i collider dentro il trigger
        Collider[] overlapping = Physics.OverlapBox(
            col.bounds.center, 
            col.bounds.extents, 
            transform.rotation
        );
        
        foreach (var other in overlapping)
        {
            if (other.CompareTag("Player"))
            {
                OnCursorEnter();
                if (showDebugLogs)
                    Debug.Log($"Player trovato già dentro '{gameObject.name}' dopo attivazione!");
                break;
            }
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
    
    /// <summary>
    /// Metodo chiamato quando il player si teletrasporta su questa base
    /// </summary>
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

    private void OnDrawGizmosSelected()
    {
        // Mostra il raggio di hover detection
        Gizmos.color = isHovering ? Color.green : new Color(1f, 0.5f, 0f, 0.5f); // Arancione semi-trasparente o verde se hovering
        Gizmos.DrawWireSphere(transform.position, hoverDetectionRadius);

        // Mostra anche una sfera più piccola al centro
        Gizmos.color = isHovering ? Color.green : Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.2f);

        // Se ha una base collegata, mostra una linea
        if (linkedBase != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, linkedBase.transform.position);
        }
    }
}