using UnityEngine;

public class LedgeGrabZone : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Punto esatto dove si aggrappano le mani (bordo superiore)")]
    public Transform grabPoint; // Punto specifico di grab
    
    [Header("Platform Reference")]
    [Tooltip("Assegna manualmente il collider della piattaforma se non viene trovato automaticamente")]
    public Collider manualPlatformCollider; // Opzione manuale
    
    [Header("Auto Setup")]
    public bool autoCalculateGrabPoint = true;
    public float grabOffsetFromEdge = 0.1f; // Distanza dal bordo
    
    [Header("Player Detection")]
    [Tooltip("Layer della PlayerAttackHitbox da rilevare")]
    public LayerMask playerHitboxLayer = 1 << 8; // Default layer 8 per PlayerAttackHitbox
    
    [Header("Debug")]
    public bool showDebug = true;
    
    private Collider parentPlatformCollider; // Il collider del padre per arrampicata
    
    private void Start()
    {
        // PRIORITÀ 1: Se hai assegnato manualmente il collider
        if (manualPlatformCollider != null)
        {
            parentPlatformCollider = manualPlatformCollider;
            if (showDebug)
                Debug.Log($"LedgeGrabZone: Usando manual platform collider: {parentPlatformCollider.name}");
        }
        // PRIORITÀ 2: Auto-find
        else
        {
            FindParentPlatformCollider();
        }
        
        if (autoCalculateGrabPoint && grabPoint == null)
        {
            CreateAutoGrabPoint();
        }
    }
    
    private void FindParentPlatformCollider()
    {
        // Debug informazioni sul parent
        if (showDebug)
        {
            Debug.Log($"LedgeGrabZone: Parent is {(transform.parent ? transform.parent.name : "NULL")}");
        }
        
        // Trova il collider del padre (piattaforma principale)
        if (transform.parent != null)
        {
            // Prova prima il collider diretto del parent
            parentPlatformCollider = transform.parent.GetComponent<Collider>();
            
            if (parentPlatformCollider != null)
            {
                if (showDebug)
                    Debug.Log($"LedgeGrabZone: Trovato parent platform collider diretto: {parentPlatformCollider.name}");
            }
            else
            {
                // Se non c'è collider diretto, cerca in tutti i componenti del parent
                Collider[] parentColliders = transform.parent.GetComponents<Collider>();
                
                for (int i = 0; i < parentColliders.Length; i++)
                {
                    if (!parentColliders[i].isTrigger)
                    {
                        parentPlatformCollider = parentColliders[i];
                        if (showDebug)
                            Debug.Log($"LedgeGrabZone: Trovato parent platform collider #{i}: {parentPlatformCollider.name}");
                        break;
                    }
                }
                
                // Se ancora non trovato, cerca nei fratelli (stesso parent)
                if (parentPlatformCollider == null)
                {
                    Collider[] siblingColliders = transform.parent.GetComponentsInChildren<Collider>();
                    
                    for (int i = 0; i < siblingColliders.Length; i++)
                    {
                        // Escludi il nostro trigger e cerca collider non-trigger
                        if (siblingColliders[i] != GetComponent<Collider>() && !siblingColliders[i].isTrigger)
                        {
                            parentPlatformCollider = siblingColliders[i];
                            if (showDebug)
                                Debug.Log($"LedgeGrabZone: Trovato sibling platform collider: {parentPlatformCollider.name}");
                            break;
                        }
                    }
                }
            }
        }
        
        if (parentPlatformCollider == null)
        {
            Debug.LogError($"LedgeGrabZone: COLLIDER NON TROVATO! Parent: {(transform.parent ? transform.parent.name : "NULL")}. Assegna manualmente il 'Manual Platform Collider' nell'inspector!");
            
            // Debug completo della hierarchy
            if (showDebug && transform.parent != null)
            {
                Debug.Log("=== DEBUG HIERARCHY ===");
                Debug.Log($"Parent: {transform.parent.name}");
                
                Collider[] allColliders = transform.parent.GetComponentsInChildren<Collider>();
                Debug.Log($"Colliders trovati nel parent e figli: {allColliders.Length}");
                
                for (int i = 0; i < allColliders.Length; i++)
                {
                    Debug.Log($"  [{i}] {allColliders[i].name} - isTrigger: {allColliders[i].isTrigger} - GameObject: {allColliders[i].gameObject.name}");
                }
            }
        }
    }
    
    private void CreateAutoGrabPoint()
    {
        if (parentPlatformCollider == null) 
        {
            Debug.LogWarning("LedgeGrabZone: Cannot create auto grab point without parent platform collider!");
            return;
        }
        
        GameObject autoGrabPoint = new GameObject("AutoGrabPoint");
        autoGrabPoint.transform.SetParent(transform.parent); // Parent della piattaforma
        
        // Usa il collider del PADRE per calcolare la posizione di grab
        Vector3 triggerCenter = GetComponent<Collider>().bounds.center;
        Vector3 platformBounds = parentPlatformCollider.bounds.center;
        
        // Calcola il punto di grab sulla superficie superiore del padre
        Vector3 grabPos = new Vector3(
            triggerCenter.x,                                    // X del centro del trigger
            parentPlatformCollider.bounds.max.y,              // Superficie superiore del padre
            parentPlatformCollider.bounds.center.z            // Centro Z del padre
        );
        
        // Aggiusta verso il bordo più vicino al trigger
        Vector3 triggerToPlatform = platformBounds - triggerCenter;
        if (Mathf.Abs(triggerToPlatform.z) > Mathf.Abs(triggerToPlatform.x))
        {
            // Trigger è davanti/dietro alla piattaforma padre
            grabPos.z = triggerCenter.z > platformBounds.z ? 
                       parentPlatformCollider.bounds.max.z - grabOffsetFromEdge :
                       parentPlatformCollider.bounds.min.z + grabOffsetFromEdge;
        }
        else
        {
            // Trigger è a sinistra/destra della piattaforma padre
            grabPos.x = triggerCenter.x > platformBounds.x ? 
                       parentPlatformCollider.bounds.max.x - grabOffsetFromEdge :
                       parentPlatformCollider.bounds.min.x + grabOffsetFromEdge;
        }
        
        autoGrabPoint.transform.position = grabPos;
        grabPoint = autoGrabPoint.transform;
        
        if (showDebug)
            Debug.Log($"Auto-created grab point at {grabPos} using parent platform {parentPlatformCollider.name}");
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Controlla se il collider appartiene al layer PlayerAttackHitbox
        if (!IsPlayerHitboxLayer(other)) return;
        
        // Trova il controller del player
        ThirdPersonController controller = GetPlayerController(other);
        if (controller == null) return;
        
        // Verifica condizioni per ledge grab
        if (!CanGrabLedge(controller)) return;
        
        if (showDebug)
            Debug.Log($"LedgeGrabZone: Rilevata PlayerAttackHitbox layer su {other.name}");
        
        // Usa grab point per posizione precisa
        Vector3 hangPosition = grabPoint != null ? grabPoint.position : 
                              (parentPlatformCollider != null ? 
                               new Vector3(transform.position.x, parentPlatformCollider.bounds.max.y, parentPlatformCollider.bounds.center.z) :
                               transform.position);
        
        // Calcola direzione verso il centro del padre
        Vector3 platformCenter = parentPlatformCollider != null ? parentPlatformCollider.bounds.center : transform.parent.position;
        Vector3 grabDirection = (platformCenter - hangPosition).normalized;
        grabDirection.y = 0; // Solo direzione orizzontale
        
        if (showDebug)
        {
            Debug.Log($"Hang Position: {hangPosition}");
            Debug.Log($"Parent Platform: {parentPlatformCollider?.name ?? "None"}");
            Debug.Log($"Grab Direction: {grabDirection}");
            Debug.DrawLine(hangPosition, hangPosition + grabDirection, Color.blue, 2f);
        }
        
        // Passa il transform del padre (piattaforma principale)
        Transform platformTransform = parentPlatformCollider != null ? parentPlatformCollider.transform : transform.parent;
        controller.StartHangingFromTrigger(hangPosition, -grabDirection, platformTransform);
    }
    
    private bool IsPlayerHitboxLayer(Collider other)
    {
        // Controlla se il layer del collider è incluso nel LayerMask
        return (playerHitboxLayer.value & (1 << other.gameObject.layer)) != 0;
    }
    
    private ThirdPersonController GetPlayerController(Collider hitbox)
    {
        // Cerca il controller nel parent della hitbox
        ThirdPersonController controller = hitbox.GetComponentInParent<ThirdPersonController>();
        if (controller != null) return controller;
        
        // Cerca nel root se non trovato nel parent
        Transform root = hitbox.transform.root;
        return root.GetComponent<ThirdPersonController>();
    }
    
    private bool CanGrabLedge(ThirdPersonController controller)
    {
        return !controller.isHanging && 
               !controller.isClimbingUp && 
               !controller.IsGrounded() &&
               controller.velocity.y < -0.5f; // Deve stare cadendo
    }
    
    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        
        // Assicurati che parentPlatformCollider sia inizializzato anche in edit mode
        if (parentPlatformCollider == null && Application.isPlaying == false)
        {
            RefreshPlatformCollider();
        }
        
        // Mostra il trigger area (questo collider) - GIALLO
        Collider triggerCol = GetComponent<Collider>();
        if (triggerCol != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, triggerCol.bounds.size);
        }
        
        // Mostra il collider del padre (piattaforma per arrampicata) - VERDE
        if (parentPlatformCollider != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(parentPlatformCollider.bounds.center, parentPlatformCollider.bounds.size);
        }
        else
        {
            // Se non trova il parent, mostra un warning visivo
            Gizmos.color = Color.red;
            Vector3 warningPos = transform.position + Vector3.up * 1f;
            Gizmos.DrawWireSphere(warningPos, 0.5f);
        }
        
        // Mostra grab point - ROSSO
        if (grabPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(grabPoint.position, 0.15f);
            
            // Mostra direzione di hang
            if (parentPlatformCollider != null)
            {
                Vector3 direction = (parentPlatformCollider.bounds.center - grabPoint.position).normalized;
                direction.y = 0;
                
                Gizmos.color = Color.red;
                Gizmos.DrawRay(grabPoint.position, -direction * 0.5f);
            }
        }
        
#if UNITY_EDITOR
        Vector3 labelPos = transform.position + Vector3.up * 0.8f;
        string platformInfo = parentPlatformCollider != null ? $"\nParent Platform: {parentPlatformCollider.name}" : "\nNO PARENT PLATFORM FOUND!";
        string layerInfo = $"\nDetecting Layer: {LayerMask.LayerToName(Mathf.RoundToInt(Mathf.Log(playerHitboxLayer.value, 2)))}";
        UnityEditor.Handles.Label(labelPos, $"Ledge Grab Zone{platformInfo}{layerInfo}");
        
        if (grabPoint != null)
        {
            UnityEditor.Handles.Label(grabPoint.position + Vector3.up * 0.3f, "GRAB POINT");
        }
        
        if (parentPlatformCollider != null)
        {
            UnityEditor.Handles.Label(parentPlatformCollider.bounds.center + Vector3.up * 0.2f, "PARENT PLATFORM");
        }
        else
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, "⚠️ PARENT NOT FOUND");
        }
#endif
    }
    
    // Metodo per refreshare il platform collider anche in edit mode
    private void RefreshPlatformCollider()
    {
        if (manualPlatformCollider != null)
        {
            parentPlatformCollider = manualPlatformCollider;
        }
        else
        {
            FindParentPlatformCollider();
        }
    }
}