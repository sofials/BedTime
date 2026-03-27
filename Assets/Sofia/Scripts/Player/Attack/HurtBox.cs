using UnityEngine;

public class HurtBox : MonoBehaviour
{
    public ThirdPersonController playerController;
    
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponentInParent<ThirdPersonController>();
            
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[HurtBox] {gameObject.name} - Collider dovrebbe essere Trigger!");
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[HurtBox] Inizializzata su {gameObject.name}");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[HurtBox] === TRIGGER ENTER ===");
            Debug.Log($"Oggetto colpito: {other.gameObject.name}");
            Debug.Log($"Tag: {other.tag}");
            Debug.Log($"Layer: {LayerMask.LayerToName(other.gameObject.layer)}");
        }
        
        // Controlla se è una piattaforma rotante
        RotatingObject rotatingObj = other.GetComponent<RotatingObject>();
        if (rotatingObj != null)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[HurtBox] ✅ PIATTAFORMA ROTANTE RILEVATA: {rotatingObj.gameObject.name}");
            }
            
            HandleRotatingPlatform(rotatingObj, other);
            return;
        }
        
        // Controlla se è un DamageDealer generico
        DamageDealer damageDealer = other.GetComponent<DamageDealer>();
        if (damageDealer != null)
        {
            Vector3 pushDirection = (playerController.transform.position - other.transform.position).normalized;
            OnHit(pushDirection, damageDealer.pushForce, damageDealer.damage);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[HurtBox] ✅ DamageDealer generico - danno {damageDealer.damage}");
            }
            return;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[HurtBox] Oggetto {other.gameObject.name} non riconosciuto come fonte di danno");
        }
    }

    private void HandleRotatingPlatform(RotatingObject rotatingObj, Collider platformCollider)
    {
        PlatformType platformType = rotatingObj.GetPlatformType();
        
        bool canCauseDamage = rotatingObj.CanCauseDamage();
        bool shouldTriggerHitWithoutDamage = rotatingObj.ShouldTriggerHitWithoutDamage();
        bool isInSlowdown = rotatingObj.IsInSlowdown;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[HurtBox] Configurazione piattaforma:");
            Debug.Log($"   - Tipo: {platformType}");
            Debug.Log($"   - È in slowdown: {isInSlowdown}");
            Debug.Log($"   - Può fare danno: {canCauseDamage}");
            Debug.Log($"   - Danno: {rotatingObj.damageAmount}");
        }
        
        if (!canCauseDamage && !shouldTriggerHitWithoutDamage)
        {
            if (enableDebugLogs)
            {
                string reason = isInSlowdown ? "in SLOWDOWN" : "disabilitata";
                Debug.Log($"[HurtBox] ⚪ Piattaforma {rotatingObj.gameObject.name} {reason} - NESSUNA INTERAZIONE");
            }
            return;
        }
        
        Vector3 pushDirection = CalculatePushDirection(platformCollider, rotatingObj);
        float pushForce = CalculatePushForce(platformCollider, rotatingObj, platformType);
        
        if (platformType == PlatformType.ObstaclePlatform)
        {
            HandleObstaclePlatform(rotatingObj, platformCollider, pushDirection, pushForce, canCauseDamage, shouldTriggerHitWithoutDamage);
        }
        else if (platformType == PlatformType.SupportPlatform)
        {
            HandleSupportPlatform(rotatingObj, platformCollider, pushDirection, pushForce, canCauseDamage, shouldTriggerHitWithoutDamage);
        }
    }

    private Vector3 CalculatePushDirection(Collider platformCollider, RotatingObject rotatingObj)
    {
        Vector3 baseDirection = (playerController.transform.position - platformCollider.transform.position).normalized;
        
        if (rotatingObj.rotationSpeed > 0)
        {
            Vector3 contactPoint = platformCollider.ClosestPoint(playerController.transform.position);
            Vector3 radiusVector = contactPoint - platformCollider.transform.position;
            Vector3 tangentialDirection = Vector3.Cross(rotatingObj.rotationAxis.normalized, radiusVector).normalized;
            
            Vector3 finalDirection = Vector3.Lerp(baseDirection, tangentialDirection, 0.3f).normalized;
            return finalDirection;
        }
        
        return baseDirection;
    }

    private float CalculatePushForce(Collider platformCollider, RotatingObject rotatingObj, PlatformType platformType)
    {
        float basePushForce = platformType == PlatformType.ObstaclePlatform ? 5f : 2f;
        float rotationSpeedFactor = Mathf.Clamp(rotatingObj.rotationSpeed / 360f, 0.5f, 2f);
        float finalPushForce = basePushForce * rotationSpeedFactor;
        
        Rigidbody rb = platformCollider.GetComponent<Rigidbody>();
        if (rb != null && rb.isKinematic)
        {
            float massMultiplier = Mathf.Clamp(rb.mass / 1f, 0.8f, 1.5f);
            finalPushForce *= massMultiplier;
        }
        
        return finalPushForce;
    }

    private void HandleObstaclePlatform(RotatingObject rotatingObj, Collider platformCollider, Vector3 pushDirection, float pushForce, bool canCauseDamage, bool shouldTriggerHitWithoutDamage)
    {
        if (rotatingObj.GetDetachPlayerOnHit())
        {
            playerController.DetachFromPlatform(platformCollider.transform);
        }
        
        if (canCauseDamage)
        {
            float damage = rotatingObj.damageAmount;
            OnHit(pushDirection, pushForce, damage);
        }
        else if (shouldTriggerHitWithoutDamage)
        {
            OnHit(pushDirection, pushForce, 0f);
        }
    }

    private void HandleSupportPlatform(RotatingObject rotatingObj, Collider platformCollider, Vector3 pushDirection, float pushForce, bool canCauseDamage, bool shouldTriggerHitWithoutDamage)
    {
        if (canCauseDamage)
        {
            float damage = rotatingObj.damageAmount;
            OnHit(pushDirection, pushForce, damage);
        }
        else if (shouldTriggerHitWithoutDamage)
        {
            OnHit(pushDirection, pushForce, 0f);
        }
    }

    public void OnHit(Vector3 push, float force, float damage = 0f)
    {
        if (playerController == null)
        {
            Debug.LogError("[HurtBox] PlayerController non trovato!");
            return;
        }

        Vector3 finalPush = push * force;
        playerController.ApplyExternalPush(finalPush);

        if (damage > 0f)
        {
            playerController.TakeDamage(damage);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[HurtBox] ✅ DANNO applicato: {damage}");
            }
        }
        else
        {
            Animator playerAnimator = playerController.GetComponentInChildren<Animator>();
            if (playerAnimator != null)
            {
                playerAnimator.SetTrigger("Hit");
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[HurtBox] ✅ HIT TRIGGER attivato senza danno");
                }
            }
        }
    }

    [ContextMenu("Toggle Debug Logs")]
    private void ToggleDebugLogs()
    {
        enableDebugLogs = !enableDebugLogs;
        Debug.Log($"[HurtBox] Debug logs: {(enableDebugLogs ? "ABILITATI" : "DISABILITATI")}");
    }
}