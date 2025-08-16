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
            
        // Verifica setup collider della HurtBox
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

    // ✅ LA HURTBOX (TRIGGER) RILEVA COLLISIONI CON OGGETTI NORMALI
    private void OnTriggerEnter(Collider other)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[HurtBox] === TRIGGER ENTER ===");
            Debug.Log($"Oggetto colpito: {other.gameObject.name}");
            Debug.Log($"Tag: {other.tag}");
            Debug.Log($"Layer: {LayerMask.LayerToName(other.gameObject.layer)}");
            
            // Analizza componenti dell'oggetto
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Debug.Log($"Rigidbody trovato - isKinematic: {rb.isKinematic}, massa: {rb.mass}");
            }
            
            Collider otherCollider = other;
            Debug.Log($"Collider tipo: {otherCollider.GetType().Name}, isTrigger: {otherCollider.isTrigger}");
        }
        
        // ✅ CONTROLLA SE È UNA PIATTAFORMA ROTANTE
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
        
        // ✅ GESTIONE DI ALTRI OGGETTI DANNOSI (proiettili, trappole, ecc.)
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

    // ✅ GESTIONE SPECIFICA PER PIATTAFORME ROTANTI
    private void HandleRotatingPlatform(RotatingObject rotatingObj, Collider platformCollider)
    {
        // Verifica se la piattaforma può fare danno
        if (!rotatingObj.CanDamagePlayer)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[HurtBox] Piattaforma {rotatingObj.gameObject.name} non può fare danno - IGNORATA");
            }
            return;
        }
        
        PlatformType platformType = rotatingObj.GetPlatformType();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[HurtBox] Configurazione piattaforma:");
            Debug.Log($"   - Tipo: {platformType}");
            Debug.Log($"   - Può fare danno: {rotatingObj.CanDamagePlayer}");
            Debug.Log($"   - Danno: {rotatingObj.damageAmount}");
            Debug.Log($"   - Sganciamento: {rotatingObj.GetDetachPlayerOnHit()}");
            Debug.Log($"   - Hit senza danno: {rotatingObj.triggerHitWhenNoDamage}");
        }
        
        // ✅ CALCOLA DIREZIONE E FORZA DEL PUSH
        Vector3 pushDirection = CalculatePushDirection(platformCollider, rotatingObj);
        float pushForce = CalculatePushForce(platformCollider, rotatingObj, platformType);
        
        // ✅ GESTIONE BASATA SUL TIPO DI PIATTAFORMA
        if (platformType == PlatformType.ObstaclePlatform)
        {
            HandleObstaclePlatform(rotatingObj, platformCollider, pushDirection, pushForce);
        }
        else if (platformType == PlatformType.SupportPlatform)
        {
            HandleSupportPlatform(rotatingObj, platformCollider, pushDirection, pushForce);
        }
    }

    // ✅ CALCOLA DIREZIONE DEL PUSH OTTIMALE
    private Vector3 CalculatePushDirection(Collider platformCollider, RotatingObject rotatingObj)
    {
        // Direzione base: dal centro della piattaforma al player
        Vector3 baseDirection = (playerController.transform.position - platformCollider.transform.position).normalized;
        
        // ✅ Per oggetti rotanti, considera anche la direzione tangenziale
        if (rotatingObj.rotationSpeed > 0)
        {
            // Calcola velocità tangenziale nel punto di contatto
            Vector3 contactPoint = platformCollider.ClosestPoint(playerController.transform.position);
            Vector3 radiusVector = contactPoint - platformCollider.transform.position;
            Vector3 tangentialDirection = Vector3.Cross(rotatingObj.rotationAxis.normalized, radiusVector).normalized;
            
            // Mescola direzione radiale con tangenziale per un push più realistico
            Vector3 finalDirection = Vector3.Lerp(baseDirection, tangentialDirection, 0.3f).normalized;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[HurtBox] Push direction calcolata con componente tangenziale");
                Debug.Log($"   - Base: {baseDirection}");
                Debug.Log($"   - Tangenziale: {tangentialDirection}");
                Debug.Log($"   - Finale: {finalDirection}");
            }
            
            return finalDirection;
        }
        
        return baseDirection;
    }

    // ✅ CALCOLA FORZA DEL PUSH BASATA SU VELOCITÀ E TIPO
    private float CalculatePushForce(Collider platformCollider, RotatingObject rotatingObj, PlatformType platformType)
    {
        float basePushForce = platformType == PlatformType.ObstaclePlatform ? 5f : 2f;
        
        // ✅ MODIFICA FORZA BASATA SULLA VELOCITÀ DI ROTAZIONE
        float rotationSpeedFactor = Mathf.Clamp(rotatingObj.rotationSpeed / 360f, 0.5f, 2f);
        float finalPushForce = basePushForce * rotationSpeedFactor;
        
        // ✅ Se ha Rigidbody kinematic, considera la "massa virtuale"
        Rigidbody rb = platformCollider.GetComponent<Rigidbody>();
        if (rb != null && rb.isKinematic)
        {
            // Per oggetti kinematic, la massa non influenza il movimento ma può indicare "peso percepito"
            float massMultiplier = Mathf.Clamp(rb.mass / 1f, 0.8f, 1.5f);
            finalPushForce *= massMultiplier;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[HurtBox] Push force modificata per Rigidbody kinematic (massa: {rb.mass})");
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[HurtBox] Push force calcolata: {finalPushForce}");
            Debug.Log($"   - Base: {basePushForce}");
            Debug.Log($"   - Fattore velocità rotazione: {rotationSpeedFactor}");
        }
        
        return finalPushForce;
    }

    // ✅ GESTIONE OBSTACLE PLATFORM
    private void HandleObstaclePlatform(RotatingObject rotatingObj, Collider platformCollider, Vector3 pushDirection, float pushForce)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[HurtBox] 🔴 OBSTACLE PLATFORM - Applicando effetti ostacolo");
        }
        
        // ✅ SGANCIAMENTO SE NECESSARIO
        if (rotatingObj.GetDetachPlayerOnHit())
        {
            playerController.DetachFromPlatform(platformCollider.transform);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[HurtBox] Player sganciato dalla piattaforma ostacolo");
            }
        }
        
        // ✅ APPLICA DANNO E/O HIT
        float damage = rotatingObj.damageAmount;
        
        if (damage > 0f)
        {
            OnHit(pushDirection, pushForce, damage);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[HurtBox] ✅ Obstacle platform - DANNO {damage} applicato con push {pushForce}");
            }
        }
        else if (rotatingObj.triggerHitWhenNoDamage)
        {
            OnHit(pushDirection, pushForce, 0f); // Solo hit senza danno
            
            if (enableDebugLogs)
            {
                Debug.Log($"[HurtBox] ✅ Obstacle platform - SOLO HIT senza danno, push {pushForce}");
            }
        }
        else
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[HurtBox] ⚠️ Obstacle platform configurata ma nessun effetto (danno=0, hit disabilitato)");
            }
        }
    }

    // ✅ GESTIONE SUPPORT PLATFORM
    private void HandleSupportPlatform(RotatingObject rotatingObj, Collider platformCollider, Vector3 pushDirection, float pushForce)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[HurtBox] 🟢 SUPPORT PLATFORM - Player può rimanere attaccato");
        }
        
        // Support platform può fare danno ma con effetti più leggeri
        if (rotatingObj.CanDamagePlayer)
        {
            float damage = rotatingObj.damageAmount;
            
            OnHit(pushDirection, pushForce, damage);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[HurtBox] ✅ Support platform - danno leggero {damage} applicato, push {pushForce}");
            }
        }
        else
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[HurtBox] Support platform - danno disabilitato, nessun effetto");
            }
        }
        
        // NON sganciamo mai il player dalle support platform
    }

    // ✅ METODO PRINCIPALE PER APPLICARE DANNO/HIT
    public void OnHit(Vector3 push, float force, float damage = 0f)
    {
        if (playerController == null)
        {
            Debug.LogError("[HurtBox] PlayerController non trovato!");
            return;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[HurtBox] === OnHit CHIAMATO ===");
            Debug.Log($"Push direction: {push}");
            Debug.Log($"Push force: {force}");
            Debug.Log($"Damage: {damage}");
            Debug.Log($"Push finale: {push * force}");
        }

        // 1) ✅ APPLICA LA SPINTA ESTERNA
        Vector3 finalPush = push * force;
        playerController.ApplyExternalPush(finalPush);

        // 2) ✅ APPLICA DANNO O HIT
        if (damage > 0f)
        {
            // Danno con animazioni gestite da TakeDamage
            float healthBefore = playerController.CurrentHealth;
            playerController.TakeDamage(damage);
            float healthAfter = playerController.CurrentHealth;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[HurtBox] ✅ DANNO applicato: {damage}");
                Debug.Log($"Salute: {healthBefore} -> {healthAfter}");
            }
        }
        else
        {
            // Solo hit senza danno - trigger animazione manualmente
            Animator playerAnimator = playerController.GetComponentInChildren<Animator>();
            if (playerAnimator != null)
            {
                playerAnimator.SetTrigger("Hit");
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[HurtBox] ✅ HIT TRIGGER attivato senza danno");
                }
            }
            else
            {
                Debug.LogError("[HurtBox] Animator non trovato sul player!");
            }
        }
    }

    // ✅ METODI DI DEBUG PUBBLICI
    [ContextMenu("Test Hit Nearby RotatingObject")]
    private void TestHitNearbyRotatingObject()
    {
        RotatingObject[] rotatingObjects = FindObjectsByType<RotatingObject>(FindObjectsSortMode.None);
        RotatingObject closest = null;
        float closestDistance = float.MaxValue;
        
        foreach (var rotObj in rotatingObjects)
        {
            float distance = Vector3.Distance(transform.position, rotObj.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = rotObj;
            }
        }
        
        if (closest != null && closestDistance < 10f)
        {
            Debug.Log($"[HurtBox] Test con RotatingObject più vicino: {closest.gameObject.name} (distanza: {closestDistance:F2})");
            
            Collider col = closest.GetComponent<Collider>();
            if (col != null)
            {
                HandleRotatingPlatform(closest, col);
            }
        }
        else
        {
            Debug.LogWarning("[HurtBox] Nessun RotatingObject trovato nelle vicinanze");
        }
    }

    [ContextMenu("Toggle Debug Logs")]
    private void ToggleDebugLogs()
    {
        enableDebugLogs = !enableDebugLogs;
        Debug.Log($"[HurtBox] Debug logs: {(enableDebugLogs ? "ABILITATI" : "DISABILITATI")}");
    }
}

// ✅ CLASSE DI SUPPORTO PER ALTRI OGGETTI DANNOSI
[System.Serializable]
public class DamageDealer : MonoBehaviour
{
    [Header("Damage Settings")]
    public float damage = 10f;
    public float pushForce = 3f;
    
    [Header("Optional Settings")]
    [Tooltip("Se true, l'oggetto si distrugge dopo aver fatto danno")]
    public bool destroyOnHit = false;
    
    private void OnTriggerEnter(Collider other)
    {
        // Questo può essere usato per proiettili che si autodistruggono
        if (destroyOnHit && other.GetComponent<ThirdPersonController>() != null)
        {
            Destroy(gameObject);
        }
    }
}