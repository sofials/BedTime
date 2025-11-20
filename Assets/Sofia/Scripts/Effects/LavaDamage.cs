using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LavaDamage : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Riferimento al LavaGeyser parent")]
    public LavaGeyser lavaGeyser;

    [Header("Movement")]
    [Tooltip("Punto di partenza (posizione locale)")]
    public Vector3 startPoint = Vector3.zero;
    [Tooltip("Punto finale (posizione locale)")]
    public Vector3 endPoint = Vector3.up * 10f;
    [Tooltip("Velocità di movimento")]
    public float moveSpeed = 5f;

    private Vector3 worldStartPoint;
    private Vector3 worldEndPoint;
    private bool isMoving = false;
    private float currentDistance = 0f;
    private float totalDistance = 0f;

    private void Awake()
    {
        // Assicurati che il collider sia un trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        // Trova il LavaGeyser se non assegnato
        if (lavaGeyser == null)
        {
            lavaGeyser = GetComponentInParent<LavaGeyser>();
        }
        
        if (lavaGeyser == null)
        {
            lavaGeyser = transform.root.GetComponentInChildren<LavaGeyser>();
        }
        
        if (lavaGeyser == null)
        {
            Debug.LogError("LavaDamage: Impossibile trovare LavaGeyser! Assegnalo manualmente nell'Inspector.");
        }

        // Calcola i punti in world space
        CalculateWorldPoints();
        
        // Posiziona allo start point
        transform.position = worldStartPoint;
    }

    private void CalculateWorldPoints()
    {
        // Converte i punti locali in world space basandosi sul parent
        Transform parent = lavaGeyser != null ? lavaGeyser.transform : transform.parent;
        if (parent != null)
        {
            worldStartPoint = parent.TransformPoint(startPoint);
            worldEndPoint = parent.TransformPoint(endPoint);
        }
        else
        {
            worldStartPoint = startPoint;
            worldEndPoint = endPoint;
        }
        
        totalDistance = Vector3.Distance(worldStartPoint, worldEndPoint);
    }

    public void StartMovement()
    {
        CalculateWorldPoints(); // Ricalcola in caso il parent si sia mosso
        transform.position = worldStartPoint;
        currentDistance = 0f;
        isMoving = true;
    }

    public void StopMovement()
    {
        isMoving = false;
        transform.position = worldStartPoint; // Torna allo start
    }

    private void Update()
    {
        if (isMoving && totalDistance > 0)
        {
            // Muovi verso l'end point
            currentDistance += moveSpeed * Time.deltaTime;
            
            if (currentDistance >= totalDistance)
            {
                // Arrivato all'end point, torna allo start
                currentDistance = 0f;
                transform.position = worldStartPoint;
            }
            else
            {
                // Interpolazione lineare tra start e end
                float t = currentDistance / totalDistance;
                transform.position = Vector3.Lerp(worldStartPoint, worldEndPoint, t);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (lavaGeyser == null) return;

        if (other.CompareTag("PlayerHurtbox"))
        {
            HurtBox hurtbox = other.GetComponent<HurtBox>();
            if (hurtbox != null)
            {
                // Push verso l'alto (lava che spinge)
                Vector3 pushDir = Vector3.up;
                hurtbox.OnHit(pushDir, lavaGeyser.pushForce, lavaGeyser.damage);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Visualizza il percorso nell'editor
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(worldStartPoint, worldEndPoint);
            Gizmos.DrawWireSphere(worldStartPoint, 0.3f);
            Gizmos.DrawWireSphere(worldEndPoint, 0.3f);
        }
        else
        {
            // In edit mode, usa il parent per calcolare i punti
            Transform parent = transform.parent;
            if (parent != null)
            {
                Vector3 start = parent.TransformPoint(startPoint);
                Vector3 end = parent.TransformPoint(endPoint);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(start, end);
                Gizmos.DrawWireSphere(start, 0.3f);
                Gizmos.DrawWireSphere(end, 0.3f);
            }
        }
    }
}