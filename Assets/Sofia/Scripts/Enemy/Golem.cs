using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class Golem : MonoBehaviour
{
    // ============ STATE MACHINE ============
    public enum GolemState
    {
        Idle,
        Moving,
        MeleeAttacking,
        RangedAttacking,
        Hit,
        Dead
    }
    
    private GolemState currentState = GolemState.Idle;
    private GolemState previousState = GolemState.Idle;
    
    // ============ REFERENCES ============
    public Transform player;
    public NavMeshAgent agent;
    public float baseSpeed = 10f;
    
    [Header("Vision Settings")]
    public float viewRadius = 600f;  // Deve essere >= rangedRange
    public float viewAngle = 360f;
    public LayerMask playerMask;
    public LayerMask obstacleMask;

    [Header("Death Settings")]
    [Tooltip("Oggetto figlio da attivare quando il Golem muore")]
    public GameObject deathActivationObject;

    [Header("Attack Settings")]
    public float meleeRange = 20f;
    public float rangedRange = 500f;  // Range attacco a distanza
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
    
    // ============ NUOVE VARIABILI PER FIX ============
    private float rotationSpeed = 5f;
    private float stateValidationInterval = 0.1f;
    private Coroutine stateValidationCoroutine;
    
    // ============ INITIALIZATION ============
    private void Start()
    {
        currentHealth = maxHealth;
        deadLayer = LayerMask.NameToLayer("DeadEnemy");

        InitializeNavMeshAgent();
        InitializeMaterialSystem();
        InitializeAudioSystem();

        if (slowdownEffect != null)
            slowdownEffect.gameObject.SetActive(false);

        // Inizia la validazione dello stato
        stateValidationCoroutine = StartCoroutine(ValidateStateRoutine());

        // Imposta stato iniziale
        ChangeState(GolemState.Idle);
    }

    private void InitializeNavMeshAgent()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                Debug.LogError($"[Golem] NavMeshAgent non trovato su {gameObject.name}!");
                return;
            }
        }

        // Assicurati che l'agent sia configurato correttamente
        agent.speed = baseSpeed;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 1f;
        agent.autoBraking = true;

        Debug.Log($"[Golem] NavMeshAgent inizializzato: speed={agent.speed}, isOnNavMesh={agent.isOnNavMesh}");
    }
    
    private void InitializeAudioSystem()
    {
        if (attackAudioSource == null)
        {
            attackAudioSource = GetComponent<AudioSource>();
            
            if (attackAudioSource == null)
            {
                attackAudioSource = gameObject.AddComponent<AudioSource>();
                Debug.Log($"[Golem] AudioSource creato automaticamente per {gameObject.name}");
            }
        }
        
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
    
    // ============ STATE MACHINE CORE ============
    private void ChangeState(GolemState newState)
    {
        // Non permettere cambi di stato se morto (eccetto per andare a Dead)
        if (isDead && newState != GolemState.Dead) return;
        
        // Se stiamo già in questo stato, non fare nulla
        if (currentState == newState) return;
        
        Debug.Log($"[Golem] Cambio stato: {currentState} -> {newState}");
        
        // Cleanup stato precedente
        ExitState(currentState);
        
        previousState = currentState;
        currentState = newState;
        attackTimer = 0f;
        
        // Setup nuovo stato
        EnterState(newState);
    }
    
    private void ExitState(GolemState state)
    {
        switch (state)
        {
            case GolemState.MeleeAttacking:
            case GolemState.RangedAttacking:
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                }
                break;
                
            case GolemState.Moving:
                animator.SetBool("isWalking", false);
                break;
        }
    }
    
    private void EnterState(GolemState state)
    {
        switch (state)
        {
            case GolemState.Idle:
                animator.SetBool("isWalking", false);
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                }
                break;
                
            case GolemState.Moving:
                animator.SetBool("isWalking", true);
                stuckTimer = 0f; // Reset quando iniziamo a muoverci
                break;
                
            case GolemState.MeleeAttacking:
                StopMovement();
                animator.SetBool("isWalking", false); // Importante: disabilita walking prima del trigger
                animator.SetTrigger("AttackMelee");
                meleeTimer = meleeCooldown;
                break;

            case GolemState.RangedAttacking:
                StopMovement();
                animator.SetBool("isWalking", false);
                animator.SetTrigger("AttackRanged");
                rangedTimer = rangedCooldown;
                break;

            case GolemState.Hit:
                StopMovement();
                animator.SetBool("isWalking", false);
                animator.SetTrigger("Hit");
                break;
                
            case GolemState.Dead:
                HandleDeath();
                break;
        }
    }
    
    // ============ UPDATE LOOP ============
    private void Update()
    {
        if (isDead || player == null) return;
        
        // Decrementa timer solo quando non in attacco
        if (currentState != GolemState.MeleeAttacking && currentState != GolemState.RangedAttacking)
        {
            meleeTimer = Mathf.Max(0, meleeTimer - Time.deltaTime);
            rangedTimer = Mathf.Max(0, rangedTimer - Time.deltaTime);
        }
        
        // Gestisci stato corrente
        switch (currentState)
        {
            case GolemState.Dead:
                return;
                
            case GolemState.Hit:
                // Aspetta che l'animazione finisca (gestito da animation event)
                return;
                
            case GolemState.MeleeAttacking:
            case GolemState.RangedAttacking:
                // Controlla timeout
                attackTimer += Time.deltaTime;
                if (attackTimer > attackTimeout)
                {
                    Debug.LogWarning($"[Golem] Attack timeout raggiunto, reset forzato");
                    ChangeState(GolemState.Idle);
                }
                
                // Mantieni rotazione verso il player durante l'attacco
                RotateTowardsPlayer();
                return;
                
            case GolemState.Idle:
            case GolemState.Moving:
                HandleCombatDecision();
                break;
        }
    }

    private void HandleCombatDecision()
    {
        float dist = Vector3.Distance(transform.position, player.position);

        // Sempre ruota verso il player
        RotateTowardsPlayer();

        // PRIORITÀ 1: Se in range melee, attacca melee
        if (dist <= meleeRange)
        {
            if (meleeTimer <= 0f)
            {
                ChangeState(GolemState.MeleeAttacking);
            }
            else
            {
                // Cooldown melee - fermati e aspetta
                if (currentState != GolemState.Idle)
                {
                    ChangeState(GolemState.Idle);
                }
            }
            return;
        }

        // PRIORITÀ 2: Se in range ranged, attacca ranged
        if (dist <= rangedRange)
        {
            if (rangedTimer <= 0f)
            {
                ChangeState(GolemState.RangedAttacking);
            }
            else
            {
                // Cooldown ranged - avvicinati se possibile
                if (CanMoveTowardsPlayer())
                {
                    if (currentState != GolemState.Moving)
                    {
                        ChangeState(GolemState.Moving);
                    }
                    MoveTowardsPlayer();
                }
                else
                {
                    // Bloccato - aspetta cooldown
                    if (currentState != GolemState.Idle)
                    {
                        ChangeState(GolemState.Idle);
                    }
                }
            }
            return;
        }

        // PRIORITÀ 3: Player troppo lontano - avvicinati
        if (CanMoveTowardsPlayer())
        {
            if (currentState != GolemState.Moving)
            {
                ChangeState(GolemState.Moving);
            }
            MoveTowardsPlayer();
        }
        else
        {
            // Bloccato - stai fermo
            if (currentState != GolemState.Idle)
            {
                ChangeState(GolemState.Idle);
            }
        }
    }

    /// <summary>
    /// Verifica se il Golem può effettivamente muoversi verso il player.
    /// Ritorna false solo se è veramente bloccato.
    /// </summary>
    private bool CanMoveTowardsPlayer()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return false;

        // Se siamo bloccati da troppo tempo, non possiamo muoverci
        // Usa stessa soglia di MoveTowardsPlayer (1f) per evitare loop Moving->Idle->Moving
        if (stuckTimer > 1f)
            return false;

        return true;
    }

    // Timer per rilevare se siamo bloccati
    private float stuckTimer = 0f;
    private void RotateTowardsPlayer()
    {
        Vector3 dir = (player.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }
    
   private void StopMovement()
{
    if (agent != null && agent.enabled && agent.isOnNavMesh)
    {
        agent.isStopped = true;
        agent.ResetPath(); // ✅ Aggiungi questo per pulire il path
        agent.velocity = Vector3.zero;
    }
    animator.SetBool("isWalking", false);
}
    
    private void MoveTowardsPlayer()
{
    if (agent == null || !agent.enabled || !agent.isOnNavMesh)
    {
        ChangeState(GolemState.Idle);
        return;
    }

    agent.isStopped = false;
    agent.speed = isSlow ? baseSpeed * slowFactor : baseSpeed;

    // Imposta destinazione
    bool pathSet = agent.SetDestination(player.position);

    if (pathSet)
    {
        float currentSpeed = agent.velocity.magnitude;

        // Traccia se siamo bloccati (velocità bassa per troppo tempo)
        if (currentSpeed < 0.1f && agent.hasPath && agent.remainingDistance > 1f)
        {
            stuckTimer += Time.deltaTime;
        }
        else
        {
            stuckTimer = 0f;
        }

        // Se bloccati per più di 1 secondo, passa a Idle (siamo al bordo NavMesh)
        if (stuckTimer > 1f)
        {
            Debug.Log("[Golem] Bloccato al bordo NavMesh, passo a Idle");
            // NON resettare stuckTimer - così CanMoveTowardsPlayer() ritornerà false
            ChangeState(GolemState.Idle);
            return;
        }

        // Considera in movimento se ha velocità O se sta calcolando un path
        bool isActuallyMoving = currentSpeed > 0.1f || agent.pathPending ||
                                (agent.hasPath && agent.remainingDistance > agent.stoppingDistance);

        animator.SetBool("isWalking", isActuallyMoving);
    }
    else
    {
        ChangeState(GolemState.Idle);
    }
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
    
    // ============ ANIMATION EVENTS (CHIAMATI DA ANIMATOR) ============
    public void EnemyAttackHitbox()
    {
        // Valida che siamo ancora in stato melee
        if (currentState != GolemState.MeleeAttacking)
        {
            Debug.LogWarning("[Golem] Melee hitbox chiamato ma non in stato melee!");
            return;
        }
        
        if (isDead) return;
        
        // Calcola danno
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
        
        // Torna a Idle dopo aver fatto danno
        ChangeState(GolemState.Idle);
    }
    
    public void SpawnProjectile()
    {
        // Valida che siamo ancora in stato ranged
        if (currentState != GolemState.RangedAttacking)
        {
            Debug.LogWarning("[Golem] Spawn projectile chiamato ma non in stato ranged!");
            return;
        }
        
        if (isDead || projectilePrefab == null || projectileSpawnPoint == null) return;
        
        GameObject proj = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity);
        Golem_Projectile projectile = proj.GetComponent<Golem_Projectile>();
        
        if (projectile != null)
            projectile.Initialize(player.position);
    }
    
    public void EndRangedAttack()
    {
        if (currentState != GolemState.RangedAttacking)
        {
            Debug.LogWarning("[Golem] EndRangedAttack chiamato ma non in stato ranged!");
            return;
        }
        ChangeState(GolemState.Idle);
    }
    
    public void EndHit()
    {
        if (currentState != GolemState.Hit)
        {
            Debug.LogWarning("[Golem] EndHit chiamato ma non in stato Hit!");
            return;
        }
        ChangeState(GolemState.Idle);
    }
    
    // ============ AUDIO METHODS ============
    public void PlayMeleeAttackSound()
    {
        if (attackAudioSource != null && meleeAttackClip != null)
        {
            attackAudioSource.clip = meleeAttackClip;
            attackAudioSource.volume = attackVolume;
            attackAudioSource.Play();
            Debug.Log($"[Golem] Riprodotto suono attacco melee");
        }
    }
    
    public void PlayRangedAttackSound()
    {
        if (attackAudioSource != null && rangedAttackClip != null)
        {
            attackAudioSource.clip = rangedAttackClip;
            attackAudioSource.volume = attackVolume;
            attackAudioSource.Play();
            Debug.Log($"[Golem] Riprodotto suono attacco ranged");
        }
    }
    
    // ============ DAMAGE & DEATH ============
    public void TakeDamage(float amount)
    {
        if (isDead) return;
        
        currentHealth -= amount;
        Debug.Log($"[Golem] Ha subito {amount} danni. Vita rimanente: {currentHealth}");
        
        if (currentHealth <= 0)
        {
            ChangeState(GolemState.Dead);
        }
        else
        {
            ChangeState(GolemState.Hit);
        }
    }
    
    private void HandleDeath()
    {
        isDead = true;
        
        // CRITICO: Disabilita completamente il NavMeshAgent
        if (agent != null)
        {
            agent.enabled = false;
        }
        
        // Disabilita questo script per sicurezza
        enabled = false;
        
        // Cleanup overlay
        if (patinaActive)
        {
            SetOverlayActive(false);
        }
        
        // Ferma tutte le coroutine
        StopAllCoroutines();
        
        // Attiva oggetto morte
        if (deathActivationObject != null)
        {
            deathActivationObject.SetActive(true);
            Debug.Log($"[Golem] Oggetto {deathActivationObject.name} attivato alla morte");
        }
        
        // Trigger animazione morte
        animator.SetTrigger("Die");
        
        // Disabilita colliders
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            if (col.CompareTag("GolemHurtbox"))
            {
                col.enabled = false;
            }
        }
    }
    
    private void Die()
    {
        ChangeState(GolemState.Dead);
    }
    
    // ============ SLOWDOWN SYSTEM ============
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
        if (value == isSlow) return;
        
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
            
            if (agent != null && agent.enabled)
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
            
            if (agent != null && agent.enabled)
                agent.speed = 3f;
            
            animator.speed = 1f;
            slowdownEffectPlayedThisCycle = false;
            
            // Se eravamo in attacco quando il slow è finito, torna a idle
            if (currentState == GolemState.MeleeAttacking || currentState == GolemState.RangedAttacking)
            {
                ChangeState(GolemState.Idle);
            }
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
    
    // ============ MATERIAL OVERLAY SYSTEM ============
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
        if (Renderer == null) return;
        SetEmissiveOverlay(active);
    }
    
    private void SetEmissiveOverlay(bool active)
    {
        if (Renderer == null) return;
        
        if (!materialsInitialized)
        {
            originalMaterials = Renderer.sharedMaterials;
            materialsInitialized = true;
        }
        
        if (active)
        {
            Material[] newMaterials = new Material[originalMaterials.Length];
            
            for (int i = 0; i < originalMaterials.Length; i++)
            {
                Material originalMat = originalMaterials[i];
                if (originalMat == null)
                {
                    newMaterials[i] = null;
                    continue;
                }
                
                Material instanceMat;
                
                if (!materialInstances.ContainsKey(originalMat))
                {
                    Material newInstance = new Material(originalMat);
                    materialInstances[originalMat] = newInstance;
                    instanceMat = newInstance;
                }
                else
                {
                    instanceMat = materialInstances[originalMat];
                }
                
                if (instanceMat.HasProperty("_EmissionColor"))
                {
                    Color hdrEmission = overlayColor * overlayIntensity * hdrMultiplier;
                    instanceMat.SetColor("_EmissionColor", hdrEmission);
                    instanceMat.EnableKeyword("_EMISSION");
                    instanceMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                
                if (applyColorTint && instanceMat.HasProperty("_BaseColor"))
                {
                    if (originalBaseColors.ContainsKey(originalMat))
                    {
                        Color originalColor = originalBaseColors[originalMat];
                        Color tintedColor = Color.Lerp(originalColor, originalColor * overlayColor, 0.3f);
                        tintedColor.a = originalColor.a;
                        instanceMat.SetColor("_BaseColor", tintedColor);
                    }
                }
                
                newMaterials[i] = instanceMat;
            }
            
            Renderer.materials = newMaterials;
        }
        else
        {
            Renderer.materials = originalMaterials;
        }
        
        patinaActive = active;
    }
    
    // ============ STATE VALIDATION ============
    private IEnumerator ValidateStateRoutine()
    {
        while (!isDead)
        {
            yield return new WaitForSeconds(stateValidationInterval);
            
            // Validazione continua dello stato
            if (!isDead)
            {
                ValidateCurrentState();
            }
        }
    }
    
    private void ValidateCurrentState()
{
    if (agent == null || !agent.enabled) return;
    
    AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
    
    // Controlla sync tra animator e movimento reale
    bool animatorIsWalking = animator.GetBool("isWalking");
    bool agentIsMoving = agent.velocity.magnitude > 0.1f && !agent.isStopped;
    bool shouldBeWalking = currentState == GolemState.Moving && agentIsMoving;
    
    if (animatorIsWalking != shouldBeWalking)
    {
        Debug.LogWarning($"[Golem] Animator desync! AnimWalk={animatorIsWalking}, ShouldWalk={shouldBeWalking}, State={currentState}, AgentSpeed={agent.velocity.magnitude}");
        animator.SetBool("isWalking", shouldBeWalking);
    }
    
    // ✅ FIX 5: GESTISCI AGENT BLOCCATO
    if (currentState == GolemState.Moving && agent.isOnNavMesh)
    {
        // Se dovremmo muoverci ma siamo fermi da troppo tempo
        if (agent.velocity.magnitude < 0.1f && !agent.isStopped)
        {
            // Controlla se il path è completato o invalido
            if (!agent.pathPending)
            {
                if (agent.remainingDistance <= agent.stoppingDistance)
                {
                    // Siamo arrivati a destinazione, aggiorna
                    if (agent.hasPath || agent.velocity.sqrMagnitude == 0f)
                    {
                        Debug.Log("[Golem] Destinazione raggiunta, ricalcolo path");
                        agent.SetDestination(player.position);
                    }
                }
                else if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
                {
                    // Path invalido, reset
                    Debug.LogWarning($"[Golem] Path invalido, reset");
                    agent.ResetPath();
                    agent.SetDestination(player.position);
                }
            }
        }
    }
}
    
    // ============ CLEANUP ============
    private void OnDestroy()
    {
        // CRITICO: Cleanup delle istanze dei materiali
        foreach (var kvp in materialInstances)
        {
            if (kvp.Value != null)
            {
                DestroyImmediate(kvp.Value);
            }
        }
        materialInstances.Clear();
        
        // Stop tutte le coroutine
        if (stateValidationCoroutine != null)
        {
            StopCoroutine(stateValidationCoroutine);
        }
    }
    
    // ============ DEBUG ============
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
        
        // Debug current state
        if (Application.isPlaying)
        {
            Vector3 statePos = transform.position + Vector3.up * 3f;
            UnityEditor.Handles.Label(statePos, $"State: {currentState}");
        }
    }
    
    private Vector3 DirFromAngle(float angleDegrees)
    {
        angleDegrees += transform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angleDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleDegrees * Mathf.Deg2Rad));
    }
}