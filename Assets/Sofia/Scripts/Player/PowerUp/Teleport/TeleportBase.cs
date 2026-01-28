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
    [Tooltip("Raggio per rilevare quando il player è vicino a questa base")]
    [SerializeField] private float hoverDetectionRadius = 25f;

    [Header("Teleport Activation")]
    [Tooltip("Distanza massima entro cui il player può attivare il teletrasporto")]
    [SerializeField] private float teleportActivationRadius = 50f;

    [Header("Teleport Connection")]
    [Tooltip("La base gemella a cui teletrasportarsi")]
    public TeleportBase linkedBase;

    // Proprietà pubbliche
    public float TeleportActivationRadius => teleportActivationRadius;
    
    private Color originalStartColor;
    private bool isHovering = false;
    
    public static TeleportBase currentHoveredBase = null;
    
    private void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning($"[TeleportBase] '{gameObject.name}' non ha un Collider!");
        }
        
        if (particleSystem == null)
        {
            particleSystem = GetComponent<ParticleSystem>();
        }
        
        if (particleSystem != null)
        {
            var main = particleSystem.main;
            originalStartColor = main.startColor.color;
        }
    }

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
                Debug.Log($"[TeleportBase] Player vicino a '{gameObject.name}'");
            }
        }
    }
    
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
                Debug.Log($"[TeleportBase] Player lontano da '{gameObject.name}'");
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

    public bool IsPlayerInHoverRange(Vector3 playerPosition)
    {
        float distance = Vector3.Distance(transform.position, playerPosition);
        return distance <= hoverDetectionRadius;
    }
    
    public Vector3 GetTeleportPosition()
    {
        return transform.position;
    }
    
    public void EnableObject()
    {
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
            OnObjectEnabled?.Invoke();
            StartCoroutine(CheckForPlayerInside());
            
            if (showDebugLogs)
            {
                Debug.Log($"[TeleportBase] '{gameObject.name}' attivato");
            }
        }
    }

    private IEnumerator CheckForPlayerInside()
    {
        yield return new WaitForFixedUpdate();

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Vector3 expandedExtents = col.bounds.extents * 1.5f;
            expandedExtents = Vector3.Max(expandedExtents, Vector3.one * hoverDetectionRadius * 0.5f);

            Collider[] overlapping = Physics.OverlapBox(
                col.bounds.center,
                expandedExtents,
                transform.rotation
            );

            foreach (var other in overlapping)
            {
                if (other.CompareTag("Player"))
                {
                    OnCursorEnter();
                    if (showDebugLogs)
                        Debug.Log($"[TeleportBase] Player già dentro '{gameObject.name}'");
                    break;
                }
            }
        }
    }
    
    public void DisableObject()
    {
        if (gameObject.activeInHierarchy)
        {
            if (currentHoveredBase == this)
                currentHoveredBase = null;
                
            OnObjectDisabled?.Invoke();
            gameObject.SetActive(false);
            
            if (showDebugLogs)
            {
                Debug.Log($"[TeleportBase] '{gameObject.name}' disattivato");
            }
        }
    }
    
    public void ToggleObject()
    {
        if (gameObject.activeInHierarchy)
            DisableObject();
        else
            EnableObject();
    }
    
    public void SetObjectActive(bool active)
    {
        if (active)
            EnableObject();
        else
            DisableObject();
    }
    
    public bool IsObjectActive
    {
        get { return gameObject.activeInHierarchy; }
        set { SetObjectActive(value); }
    }
    
    public virtual void OnPlayerTeleported()
    {
        if (showDebugLogs)
        {
            Debug.Log($"[TeleportBase] Player arrivato su '{gameObject.name}'");
        }
    }
    
    public void SetParticleSystem(ParticleSystem ps)
    {
        particleSystem = ps;
        if (particleSystem != null)
        {
            var main = particleSystem.main;
            originalStartColor = main.startColor.color;
        }
    }

    public void SetHoverColor(Color color)
    {
        hoverColor = color;
    }

    private void OnDrawGizmosSelected()
    {
        // Raggio hover (arancione)
        Gizmos.color = isHovering ? Color.green : new Color(1f, 0.5f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, hoverDetectionRadius);

        // Raggio attivazione (rosso)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, teleportActivationRadius);

        // Centro
        Gizmos.color = isHovering ? Color.green : Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.2f);

        // Linea verso base collegata
        if (linkedBase != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, linkedBase.transform.position);
        }
    }
}