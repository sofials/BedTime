using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class Golem : MonoBehaviour
{
    public Transform player;
    public NavMeshAgent agent;

    [Header("Vision Settings")]
    public float viewRadius = 300f;
    public float viewAngle = 360f;
    public LayerMask playerMask;
    public LayerMask obstacleMask;

    [Header("Attack Settings")]
    public float meleeRange = 20f;
    public float rangedRange = 280f;
    public float meleeCooldown = 2f;
    public float rangedCooldown = 3f;

    private float meleeTimer = 0f;
    private float rangedTimer = 0f;

    [Header("Attack Effects")]
    public float damage = 20f;
    public float pushForce = 5f;

    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;

    [Header("Animation")]
    public Animator animator;

    [Header("Audio Settings")]
    [Space(5)]
    [Tooltip("AudioSource per i suoni di attacco")]
    public AudioSource attackAudioSource;
    
    [Header("Attack Audio Clips")]
    [Tooltip("Suono per l'attacco corpo a corpo")]
    public AudioClip meleeAttackClip;
    
    [Tooltip("Suono per l'attacco a distanza")]
    public AudioClip rangedAttackClip;
    
    [Header("Audio Volume Settings")]
    [Range(0f, 1f)]
    [Tooltip("Volume per i suoni di attacco")]
    public float attackVolume = 1f;

    [Header("Stats")]
    public float maxHealth = 30f;
    private float currentHealth;
    private bool isDead = false;

    private bool playerVisible = false;

    private bool isMeleeAttacking = false;
    private bool isRangedAttacking = false;

    [Header("Slowdown")]
    public bool isSlow = false;
    public float slowFactor = 0.5f;
    public float animationSlowFactor = 0.3f;
    public SlowdownAbility activeSlowdownAbility;
    
    [Header("Slowdown Custom Duration")]
    [Tooltip("Durata personalizzata per lo slowdown (0 = usa durata default dell'abilità)")]
    public float customSlowdownDuration = 15f;

    [Header("FX & Material System")]
    public Renderer Renderer;
    [SerializeField] private CFXR_EffectController slowdownEffect;
    
    [Header("Overlay Emission - Sistema Unificato")]
    [SerializeField] private Color overlayColor = Color.blue;
    [SerializeField] private float overlayIntensity = 2f;
    [Tooltip("Moltiplicatore aggiuntivo per HDR emission (valori alti = più luce)")]
    [SerializeField] private float hdrMultiplier = 3f;
    [Tooltip("Se true, mantiene anche il tint del Base Color oltre all'emission")]
    [SerializeField] private bool applyColorTint = true;

    private bool patinaActive = false;
    private Coroutine fxCoroutine;
    private Coroutine slowCoroutine;
    private Coroutine blinkCoroutine;
    
    [SerializeField] private float blinkDurationBeforeEnd = 2f;

    // Sistema gestione materiali unificato
    private Dictionary<Material, Material> materialInstances = new Dictionary<Material, Material>();
    private Dictionary<Material, Color> originalBaseColors = new Dictionary<Material, Color>();
    private Dictionary<Material, Color> originalEmissionColors = new Dictionary<Material, Color>();
    private Material[] originalMaterials = null;
    private bool materialsInitialized = false;

    private int deadLayer;
    private float attackTimeout = 3f;
    private float attackTimer = 0f;

    private void Start()
    {
        currentHealth = maxHealth;
        deadLayer = LayerMask.NameToLayer("DeadEnemy");
        
        InitializeMaterialSystem();
        InitializeAudioSystem();

        if (slowdownEffect != null)
            slowdownEffect.gameObject.SetActive(false);
    }

    private void InitializeAudioSystem()
    {
        // Se non è stato assegnato un AudioSource, cerca di crearne uno automaticamente
        if (attackAudioSource == null)
        {
            attackAudioSource = GetComponent<AudioSource>();
            
            // Se non esiste, creane uno
            if (attackAudioSource == null)
            {
                attackAudioSource = gameObject.AddComponent<AudioSource>();
                Debug.Log($"[Golem] AudioSource creato automaticamente per {gameObject.name}");
            }
        }

        // Configura l'AudioSource se esiste
        if (attackAudioSource != null)
        {
            attackAudioSource.playOnAwake = false;
            attackAudioSource.loop = false;
            attackAudioSource.volume = attackVolume;
        }
    }

    private void InitializeMaterialSystem()
    {
        if (Renderer == null)
        {
            Debug.LogWarning($"[Golem] Nessun Renderer trovato su {gameObject.name}");
            return;
        }
        
        // Salva i colori originali dei materiali SHARED (non istanze)
        foreach (Material mat in Renderer.sharedMaterials)
        {
            if (mat != null)
            {
                if (mat.HasProperty("_BaseColor"))
                    originalBaseColors[mat] = mat.GetColor("_BaseColor");
                if (mat.HasProperty("_EmissionColor"))
                    originalEmissionColors[mat] = mat.GetColor("_EmissionColor");
            }
        }
        
        Debug.Log($"[Golem] Sistema materiali inizializzato per {gameObject.name}");
    }

    private void Update()
    {
        if (isDead || player == null) return;

        meleeTimer -= Time.deltaTime;
        rangedTimer -= Time.deltaTime;

        // Forza reset se l'attacco dura troppo
        if (isMeleeAttacking || isRangedAttacking)
        {
            attackTimer += Time.deltaTime;
            if (attackTimer > attackTimeout)
            {
                isMeleeAttacking = false;
                isRangedAttacking = false;
                attackTimer = 0f;
            }
            return;
        }
        else
        {
            attackTimer = 0f;
        }

        UpdatePlayerVisibility();

        float dist = Vector3.Distance(transform.position, player.position);

        Vector3 dir = (player.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);

        if (!playerVisible)
        {
            MoveTowardsPlayer();
            return;
        }

        if (dist <= meleeRange && meleeTimer <= 0f)
            DoMeleeAttack();
        else if (dist <= rangedRange && rangedTimer <= 0f)
            DoRangedAttack();
        else
            MoveTowardsPlayer();
    }

    private void DoMeleeAttack()
    {
        StopAndFacePlayer();
        isMeleeAttacking = true;
        meleeTimer = meleeCooldown;
        animator.SetTrigger("AttackMelee");
    }

    private void DoRangedAttack()
    {
        StopAndFacePlayer();
        isRangedAttacking = true;
        rangedTimer = rangedCooldown;
        animator.SetTrigger("AttackRanged");
    }

    // ===== METODI AUDIO PER ANIMATION EVENTS =====
    
    /// <summary>
    /// Riproduce il suono dell'attacco corpo a corpo.
    /// Chiamare questo metodo da Animation Event nell'animazione di attacco melee.
    /// </summary>
    public void PlayMeleeAttackSound()
    {
        if (attackAudioSource != null && meleeAttackClip != null)
        {
            attackAudioSource.clip = meleeAttackClip;
            attackAudioSource.volume = attackVolume;
            attackAudioSource.Play();
            Debug.Log($"[Golem] Riprodotto suono attacco melee");
        }
        else
        {
            Debug.LogWarning($"[Golem] Impossibile riprodurre suono melee: AudioSource={attackAudioSource != null}, Clip={meleeAttackClip != null}");
        }
    }

    /// <summary>
    /// Riproduce il suono dell'attacco a distanza.
    /// Chiamare questo metodo da Animation Event nell'animazione di attacco ranged.
    /// </summary>
    public void PlayRangedAttackSound()
    {
        if (attackAudioSource != null && rangedAttackClip != null)
        {
            attackAudioSource.clip = rangedAttackClip;
            attackAudioSource.volume = attackVolume;
            attackAudioSource.Play();
            Debug.Log($"[Golem] Riprodotto suono attacco ranged");
        }
        else
        {
            Debug.LogWarning($"[Golem] Impossibile riprodurre suono ranged: AudioSource={attackAudioSource != null}, Clip={rangedAttackClip != null}");
        }
    }

    /// <summary>
    /// Metodo generico per riprodurre un suono di attacco.
    /// Utile se vuoi usare lo stesso metodo per entrambi i tipi di attacco.
    /// </summary>
    /// <param name="useRanged">Se true, usa il clip ranged, altrimenti usa quello melee</param>
    public void PlayAttackSound(bool useRanged = false)
    {
        if (useRanged)
            PlayRangedAttackSound();
        else
            PlayMeleeAttackSound();
    }

    // ================================================

    private void StopAndFacePlayer()
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.isStopped = true;

        animator.SetBool("isWalking", false);
    }

    private void MoveTowardsPlayer()
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = isSlow ? 3f * slowFactor : 3f;
            agent.SetDestination(player.position);
        }

        animator.SetBool("isWalking", true);
    }

    private void UpdatePlayerVisibility()
    {
        playerVisible = false;
        Collider[] hits = Physics.OverlapSphere(transform.position, viewRadius, playerMask);
        foreach (var hit in hits)
        {
            Vector3 dirToPlayer = (hit.transform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dirToPlayer) < viewAngle / 2f)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, hit.transform.position);
                if (!Physics.Raycast(transform.position, dirToPlayer, distanceToPlayer, obstacleMask))
                {
                    playerVisible = true;
                    return;
                }
            }
        }
    }

    public void EnemyAttackHitbox()
    {
        if (isDead) return;

        Collider[] hits = Physics.OverlapBox(
            transform.position + transform.forward * (meleeRange * 0.5f),
            new Vector3(10f, 10f, 10f),
            transform.rotation,
            LayerMask.GetMask("PlayerHurtbox")
        );

        foreach (var hit in hits)
        {
            if (hit.gameObject.CompareTag("PlayerHurtbox"))
            {
                var hurtbox = hit.GetComponent<HurtBox>();
                if (hurtbox != null)
                {
                    Vector3 pushDir = (hurtbox.transform.position - transform.position).normalized;
                    hurtbox.OnHit(pushDir, pushForce, damage);
                }
            }
        }

        isMeleeAttacking = false;
    }

    public void SpawnProjectile()
    {
        if (isDead || projectilePrefab == null || projectileSpawnPoint == null) return;

        GameObject proj = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity);
        Golem_Projectile projectile = proj.GetComponent<Golem_Projectile>();

        if (projectile != null)
            projectile.Initialize(player.position);
    }

    public void EndRangedAttack()
    {
        isRangedAttacking = false;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        Debug.Log($"Golem ha subito {amount} danni. Vita rimanente: {currentHealth}");

        isMeleeAttacking = false;
        isRangedAttacking = false;

        if (currentHealth <= 0)
            Die();
        else
            animator.SetTrigger("Hit");
    }

    public void StartSlow(float duration, SlowdownAbility sourceAbility)
    {
        if (isSlow)
        {
            if (slowCoroutine != null)
                StopCoroutine(slowCoroutine);
        }
        else
        {
            SetSlow(true);
        }

        activeSlowdownAbility = sourceAbility;
        slowCoroutine = StartCoroutine(SlowDurationRoutine(duration));
    }

    private IEnumerator SlowDurationRoutine(float duration)
    {
        float normalDuration = duration - blinkDurationBeforeEnd;

        if (normalDuration > 0)
            yield return new WaitForSeconds(normalDuration);

        if (blinkCoroutine != null)
            StopCoroutine(blinkCoroutine);
        blinkCoroutine = StartCoroutine(BlinkOverlayWhileSlow());

        yield return new WaitForSeconds(blinkDurationBeforeEnd);

        SetSlow(false);
        activeSlowdownAbility = null;
        slowCoroutine = null;

        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
    }

    private bool slowdownEffectPlayedThisCycle = false;

    public void SetSlow(bool value)
    {
        if (value == isSlow)
            return;

        isSlow = value;

        if (isSlow)
        {
            SetOverlayActive(true);

            if (!slowdownEffectPlayedThisCycle && slowdownEffect != null)
            {
                if (fxCoroutine != null)
                    StopCoroutine(fxCoroutine);
                fxCoroutine = StartCoroutine(PlayEffectOnce());
                slowdownEffectPlayedThisCycle = true;
            }

            if (agent != null)
                agent.speed = 3f * slowFactor;

            animator.speed = animationSlowFactor;
        }
        else
        {
            SetOverlayActive(false);

            if (slowdownEffect != null)
            {
                slowdownEffect.StopEffect();
                slowdownEffect.gameObject.SetActive(false);
            }

            if (agent != null)
                agent.speed = 3f;

            animator.speed = 1f;
            slowdownEffectPlayedThisCycle = false;

            // Reset attacchi bloccati
            isMeleeAttacking = false;
            isRangedAttacking = false;
        }
    }

    private IEnumerator PlayEffectOnce()
    {
        slowdownEffect.gameObject.SetActive(true);
        slowdownEffect.PlayEffect();
        yield return null;
        slowdownEffect.StopEffect();
        slowdownEffect.gameObject.SetActive(false);
        fxCoroutine = null;
    }

    private IEnumerator BlinkOverlayWhileSlow()
    {
        if (Renderer == null) yield break;

        bool state = true;
        float blinkRate = 0.2f;

        while (isSlow)
        {
            SetOverlayActive(state);
            state = !state;
            yield return new WaitForSeconds(blinkRate);
        }

        SetOverlayActive(false);
    }

    public void SetOverlayActive(bool active)
    {
        Debug.Log($"[Golem] *** SetOverlayActive({active}) chiamato su {gameObject.name} ***");
        
        if (Renderer == null) 
        {
            Debug.LogWarning($"[Golem] Renderer nullo su {gameObject.name}");
            return;
        }
        
        Debug.Log($"[Golem] Renderer OK, chiamando SetEmissiveOverlay({active})");
        SetEmissiveOverlay(active);
    }

    private void SetEmissiveOverlay(bool active)
    {
        Debug.Log($"[Golem] SetEmissiveOverlay({active}) - inizio processing su {gameObject.name}");
        
        if (Renderer == null) 
        {
            Debug.LogWarning($"[Golem] Renderer nullo su {gameObject.name}");
            return;
        }
        
        // INIZIALIZZA i materiali originali solo la prima volta
        if (!materialsInitialized)
        {
            originalMaterials = Renderer.sharedMaterials; // USA sharedMaterials per ottenere gli originali
            materialsInitialized = true;
            Debug.Log($"[Golem] Materiali originali salvati: {originalMaterials.Length}");
        }

        if (active)
        {
            // ATTIVAZIONE: Crea istanze e applica effetto
            Material[] newMaterials = new Material[originalMaterials.Length];
            bool materialsChanged = false;
            
            Debug.Log($"[Golem] ATTIVANDO overlay - creando istanze materiali");

            for (int i = 0; i < originalMaterials.Length; i++)
            {
                Material originalMat = originalMaterials[i];
                if (originalMat == null) 
                {
                    newMaterials[i] = null;
                    continue;
                }

                Debug.Log($"[Golem] ATTIVANDO - Processando materiale {i}: {originalMat.name}");

                Material instanceMat;

                // Crea istanza del materiale SOLO se non esiste ancora
                if (!materialInstances.ContainsKey(originalMat))
                {
                    Material newInstance = new Material(originalMat);
                    materialInstances[originalMat] = newInstance;
                    instanceMat = newInstance;
                    materialsChanged = true;
                    Debug.Log($"[Golem] Creata NUOVA istanza per materiale {originalMat.name}");
                }
                else
                {
                    // Usa l'istanza esistente
                    instanceMat = materialInstances[originalMat];
                    Debug.Log($"[Golem] Usando istanza ESISTENTE per materiale {originalMat.name}");
                }

                // EMISSION LUMINOSO (principale)
                if (instanceMat.HasProperty("_EmissionColor"))
                {
                    // Calcola colore emission HDR per massima luminosità
                    Color hdrEmission = overlayColor * overlayIntensity * hdrMultiplier;
                    instanceMat.SetColor("_EmissionColor", hdrEmission);
                    
                    // Abilita emission
                    instanceMat.EnableKeyword("_EMISSION");
                    
                    // Forza il material a essere emission-enabled
                    instanceMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    
                    Debug.Log($"[Golem] Emission attivata con colore {hdrEmission}");
                }

                // BASE COLOR TINT (opzionale, per colorare anche la texture)
                if (applyColorTint && instanceMat.HasProperty("_BaseColor"))
                {
                    if (originalBaseColors.ContainsKey(originalMat))
                    {
                        Color originalColor = originalBaseColors[originalMat];
                        // Mescola il colore originale con l'overlay
                        Color tintedColor = Color.Lerp(originalColor, originalColor * overlayColor, 0.3f);
                        tintedColor.a = originalColor.a;
                        instanceMat.SetColor("_BaseColor", tintedColor);
                        Debug.Log($"[Golem] BaseColor tint applicato");
                    }
                }

                newMaterials[i] = instanceMat;
                materialsChanged = true;
            }

            if (materialsChanged)
            {
                Renderer.materials = newMaterials;
                Debug.Log($"[Golem] Materiali istanza applicati al renderer");
            }
        }
        else
        {
            // DISATTIVAZIONE: Ripristina materiali originali SHARED
            Debug.Log($"[Golem] DISATTIVANDO overlay - ripristinando materiali SHARED originali");
            
            // IMPORTANTE: Torna ai materiali SHARED originali, non alle istanze
            Renderer.materials = originalMaterials;
            
            Debug.Log($"[Golem] Materiali SHARED originali ripristinati nel renderer");
        }
        
        patinaActive = active;
        Debug.Log($"[Golem] SetEmissiveOverlay completato - patinaActive = {patinaActive}");
    }

    public void EndHit()
    {
        isMeleeAttacking = false;
        isRangedAttacking = false;
    }

    private void Die()
    {
        isDead = true;
        animator.SetTrigger("Die");

        // Cleanup overlay al momento della morte
        if (patinaActive)
        {
            SetOverlayActive(false);
        }

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.isStopped = true;

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            if (col.CompareTag("GolemHurtbox"))
            {
                col.isTrigger = false;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, rangedRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Vector3 viewAngleA = DirFromAngle(-viewAngle / 2);
        Vector3 viewAngleB = DirFromAngle(viewAngle / 2);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, transform.position + viewAngleA * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + viewAngleB * viewRadius);
    }

    private Vector3 DirFromAngle(float angleDegrees)
    {
        angleDegrees += transform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angleDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleDegrees * Mathf.Deg2Rad));
    }
}