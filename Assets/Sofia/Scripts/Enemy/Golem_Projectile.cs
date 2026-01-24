using UnityEngine;

/// <summary>
/// Proiettile del Golem - Sistema Raycast Puro
/// Perfetto per proiettili veloci, ZERO tunneling garantito.
/// 
/// SETUP PREFAB:
/// 1. Crea un GameObject vuoto (o con mesh visiva)
/// 2. Aggiungi SOLO questo script
/// 3. NON serve Rigidbody
/// 4. NON serve Collider
/// 5. Imposta hitLayerMask nell'Inspector (Default + PlayerHurtbox + Player)
/// </summary>
public class Golem_Projectile : MonoBehaviour
{
    [Header("Damage Settings")]
    public float damage = 20f;
    public float pushForce = 10f;
    
    [Header("Movement")]
    [Tooltip("Velocità del proiettile (25-50 consigliato)")]
    public float speed = 35f;
    
    [Tooltip("Tempo di vita massimo")]
    public float lifetime = 5f;
    
    [Header("Visual Arc (opzionale)")]
    [Tooltip("Se true, aggiunge un leggero arco visivo al movimento")]
    public bool useVisualArc = true;
    
    [Tooltip("Altezza dell'arco visivo")]
    public float arcHeight = 3f;
    
    [Header("Raycast Settings")]
    [Tooltip("Layer che il proiettile può colpire (DEVE includere Default, PlayerHurtbox, Player)")]
    public LayerMask hitLayerMask;
    
    [Tooltip("Raggio dello SphereCast (0 = raycast linea, 0.1-0.3 = più facile colpire)")]
    [Range(0f, 0.5f)]
    public float sphereCastRadius = 0.15f;
    
    [Header("Debug")]
    public bool showDebug = true;

    [Header("Slowdown Settings")]
    [Tooltip("Fattore di rallentamento quando il Golem è in slow")]
    public float slowSpeedMultiplier = 0.3f;

    [Header("Slowdown Overlay")]
    [SerializeField] private Color overlayColor = new Color(0.2f, 0.5f, 1f); // Blu
    [SerializeField] private float overlayIntensity = 2f;
    [SerializeField] private float hdrMultiplier = 3f;

    // Stato interno
    private Vector3 direction;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float totalDistance;
    private float distanceTraveled = 0f;
    private bool initialized = false;
    private Vector3 previousPosition;

    // Slowdown
    private bool isSlowed = false;
    private float originalSpeed;
    private Renderer projectileRenderer;
    private Material[] originalMaterials;
    private Material[] instanceMaterials;
    
    /// <summary>
    /// Inizializza il proiettile con direzione già calcolata (dal Golem con predizione)
    /// </summary>
    public void Init(Vector3 shootDirection, float projectileSpeed)
    {
        Init(shootDirection, projectileSpeed, false);
    }

    /// <summary>
    /// Inizializza il proiettile con direzione e stato slowdown
    /// </summary>
    public void Init(Vector3 shootDirection, float projectileSpeed, bool golemIsSlowed)
    {
        direction = shootDirection.normalized;
        originalSpeed = projectileSpeed;
        speed = golemIsSlowed ? projectileSpeed * slowSpeedMultiplier : projectileSpeed;
        isSlowed = golemIsSlowed;

        startPosition = transform.position;
        previousPosition = transform.position;
        totalDistance = 0f;
        initialized = true;

        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        // Applica overlay blu se rallentato
        if (isSlowed)
        {
            InitializeSlowOverlay();
        }

        Destroy(gameObject, lifetime);

        if (showDebug)
        {
            string slowStatus = isSlowed ? " [SLOWED]" : "";
            Debug.Log($"[Golem_Projectile] Lanciato! Dir: {direction}, Speed: {speed}{slowStatus}");
            Debug.DrawRay(transform.position, direction * 20f, Color.green, 3f);
        }
    }

    private void InitializeSlowOverlay()
    {
        projectileRenderer = GetComponentInChildren<Renderer>();
        if (projectileRenderer == null) return;

        originalMaterials = projectileRenderer.sharedMaterials;
        instanceMaterials = new Material[originalMaterials.Length];

        for (int i = 0; i < originalMaterials.Length; i++)
        {
            if (originalMaterials[i] != null)
            {
                instanceMaterials[i] = new Material(originalMaterials[i]);
                ApplyEmissionOverlay(instanceMaterials[i]);
            }
        }

        projectileRenderer.materials = instanceMaterials;

        if (showDebug)
        {
            Debug.Log($"[Golem_Projectile] Overlay blu applicato!");
        }
    }

    private void ApplyEmissionOverlay(Material material)
    {
        if (material.HasProperty("_EmissionColor"))
        {
            Color hdrEmission = overlayColor * overlayIntensity * hdrMultiplier;
            material.SetColor("_EmissionColor", hdrEmission);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        if (material.HasProperty("_BaseColor"))
        {
            Color baseColor = material.GetColor("_BaseColor");
            Color tintedColor = Color.Lerp(baseColor, overlayColor, 0.4f);
            tintedColor.a = baseColor.a;
            material.SetColor("_BaseColor", tintedColor);
        }
    }
    
    /// <summary>
    /// Inizializza il proiettile verso un target (calcola direzione internamente)
    /// </summary>
    public void Initialize(Vector3 targetPos)
    {
        targetPosition = targetPos;
        startPosition = transform.position;
        previousPosition = transform.position;
        direction = (targetPos - startPosition).normalized;
        totalDistance = Vector3.Distance(startPosition, targetPos);
        initialized = true;
        
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
        
        Destroy(gameObject, lifetime);
        
        if (showDebug)
        {
            Debug.Log($"[Golem_Projectile] 🎯 Target: {targetPos}, Distanza: {totalDistance:F1}");
            Debug.DrawLine(startPosition, targetPos, Color.green, 3f);
        }
    }
    
    private void Update()
    {
        if (!initialized) return;
        
        float step = speed * Time.deltaTime;
        
        // Calcola la prossima posizione BASE (linea retta)
        Vector3 baseNextPosition = transform.position + direction * step;
        Vector3 nextPosition = baseNextPosition;
        
        // Arco visivo opzionale
        float arcDelta = 0f;
        if (useVisualArc && totalDistance > 0)
        {
            distanceTraveled += step;
            float progress = Mathf.Clamp01(distanceTraveled / totalDistance);
            float prevProgress = Mathf.Clamp01((distanceTraveled - step) / totalDistance);
            
            // Parabola: sale nella prima metà, scende nella seconda
            float arcOffset = arcHeight * 4f * progress * (1f - progress);
            float prevArcOffset = arcHeight * 4f * prevProgress * (1f - prevProgress);
            arcDelta = arcOffset - prevArcOffset;
            
            nextPosition.y += arcDelta;
        }
        
        // ========== RAYCAST COLLISION CHECK ==========
        Vector3 rayOrigin = previousPosition;
        Vector3 rayDirection = nextPosition - previousPosition;
        float rayDistance = rayDirection.magnitude;
        
        if (rayDistance > 0.001f)
        {
            bool didHit = false;
            RaycastHit hit = default;
            
            if (sphereCastRadius > 0)
            {
                // SphereCast per avere un po' di "spessore" al proiettile
                // QueryTriggerInteraction.Collide per rilevare HurtBox trigger!
                didHit = Physics.SphereCast(rayOrigin, sphereCastRadius, rayDirection.normalized, 
                    out hit, rayDistance, hitLayerMask, QueryTriggerInteraction.Collide);
            }
            else
            {
                // Raycast puro (linea sottile)
                didHit = Physics.Raycast(rayOrigin, rayDirection.normalized, 
                    out hit, rayDistance, hitLayerMask, QueryTriggerInteraction.Collide);
            }
            
            if (didHit)
            {
                HandleHit(hit);
                return;
            }
        }
        
        // ========== AGGIORNA POSIZIONE ==========
        previousPosition = transform.position;
        transform.position = nextPosition;
        
        // Rotazione per seguire il movimento (incluso arco)
        if (rayDirection.magnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(rayDirection.normalized);
        }
        
        // Debug visivo
        if (showDebug)
        {
            Debug.DrawLine(previousPosition, transform.position, Color.yellow, 0.5f);
        }
    }
    
    private void HandleHit(RaycastHit hit)
    {
        GameObject hitObject = hit.collider.gameObject;
        
        if (showDebug)
        {
            Debug.Log($"[Golem_Projectile] 💥 HIT: {hitObject.name} (tag: {hitObject.tag}, layer: {LayerMask.LayerToName(hitObject.layer)})");
            Debug.DrawLine(previousPosition, hit.point, Color.red, 2f);
        }
        
        // Posiziona il proiettile nel punto di impatto (per eventuali effetti visivi)
        transform.position = hit.point;
        
        // ========== COLPITO IL PLAYER ==========
        if (hitObject.CompareTag("PlayerHurtbox") || hitObject.CompareTag("Player"))
        {
            // Prova prima con HurtBox
            HurtBox hurtbox = hitObject.GetComponent<HurtBox>();
            if (hurtbox != null)
            {
                hurtbox.OnHit(direction, pushForce, damage);
                Debug.Log($"[Golem_Projectile] ✅ COLPITO PLAYER via HurtBox! Danno: {damage}");
            }
            else
            {
                // Fallback: cerca ThirdPersonController nel parent
                ThirdPersonController player = hitObject.GetComponentInParent<ThirdPersonController>();
                if (player != null)
                {
                    player.TakeDamage(damage);
                    player.ApplyExternalPush(direction * pushForce);
                    Debug.Log($"[Golem_Projectile] ✅ COLPITO PLAYER diretto! Danno: {damage}");
                }
                else
                {
                    Debug.LogWarning($"[Golem_Projectile] ⚠️ Tag Player ma nessun componente trovato su {hitObject.name}");
                }
            }
            
            // Spawn effetto impatto qui se vuoi
            // Instantiate(hitEffect, hit.point, Quaternion.LookRotation(hit.normal));
            
            Destroy(gameObject);
            return;
        }
        
        // ========== COLPITO AMBIENTE (muri, terreno, etc) ==========
        if (showDebug)
        {
            Debug.Log($"[Golem_Projectile] 🧱 Impatto ambiente: {hitObject.name}");
        }
        
        // Spawn effetto impatto ambiente qui se vuoi
        // Instantiate(envHitEffect, hit.point, Quaternion.LookRotation(hit.normal));
        
        Destroy(gameObject);
    }
    
    // ========== DEBUG GIZMOS ==========
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !initialized) return;
        
        // Posizione attuale
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sphereCastRadius > 0 ? sphereCastRadius : 0.1f);
        
        // Direzione
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, direction * 5f);
        
        // Target (se impostato)
        if (totalDistance > 0)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetPosition, 0.5f);
            
            Gizmos.color = Color.green;
            Gizmos.DrawLine(startPosition, targetPosition);
        }
    }
    
    // ========== VALIDAZIONE INSPECTOR ==========
    private void OnValidate()
    {
        // Warning se hitLayerMask è vuota
        if (hitLayerMask == 0)
        {
            Debug.LogWarning($"[Golem_Projectile] ⚠️ hitLayerMask è vuota! Il proiettile non colpirà nulla. Imposta almeno Default + PlayerHurtbox.");
        }
    }
}