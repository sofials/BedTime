using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class Golem : MonoBehaviour
{
    // ============ STATE MACHINE ============
    public enum GolemState
    {
        Dormant,
        Awakening,
        Idle,
        Moving,
        MeleeAttacking,
        RangedAttacking,
        Hit,
        Dead
    }
    
    private GolemState currentState = GolemState.Dormant;
    private GolemState previousState = GolemState.Dormant;
    
    private bool isAwakened = false;
    private bool isCombatActive = false;
    
    public Transform player;
    public NavMeshAgent agent;
    public float baseSpeed = 20f;
    
    [Header("Vision Settings")]
    public float viewRadius = 600f;
    public float viewAngle = 360f;
    public LayerMask playerMask;
    public LayerMask obstacleMask;

    [Header("Death Settings")]
[Tooltip("DialogueSystem da attivare quando l'animazione di morte è completata")]
public DialogueSystem deathDialogueSystem;
    
    [Header("Death Events")]
    [Tooltip("Evento invocato quando il Golem muore (inizio animazione morte)")]
    public UnityEvent OnGolemDeath;
    [Tooltip("Evento invocato quando animazione di morte completata")]
    public UnityEvent OnGolemDeathAnimationComplete;

    [Header("Attack Settings")]
    public float meleeRange = 20f;
    public float rangedRange = 500f;
    public float meleeCooldown = 0.6f;
    public float rangedCooldown = 0.2f;
    
    private float meleeTimer = 0f;
    private float rangedTimer = 0f;
    
    [Header("Hit Recovery")]
    public float hitRecoveryTime = 0.3f;
    [Tooltip("Tempo di invincibilità dopo essere stato colpito (i-frames)")]
    public float hitInvincibilityTime = 0.8f;
    private float lastHitTime = -999f;
    
    [Header("Attack Effects")]
    public float damage = 20f;
    public float pushForce = 5f;
    
    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    public float projectileSpeed = 35f;
    public float targetHeightOffset = 1.0f;
    
    [Header("Predictive Aiming")]
    public bool usePredictiveAiming = true;
    [Range(0f, 2f)]
    public float aimErrorAmount = 0.3f;
    
    [Header("Animation")]
    public Animator animator;
    
    [Header("Audio Settings")]
    public AudioSource attackAudioSource;
    
    [Header("Attack Audio Clips")]
    public AudioClip meleeAttackClip;
    public AudioClip rangedAttackClip;
    public AudioClip footstepsClip;
    
    [Header("Awakening Audio")]
    public AudioClip awakeningClip;
    [Range(0f, 1f)]
    public float awakeningVolume = 1f;
    
    [Header("Awakening Delay")]
    [Range(0f, 3f)]
    public float awakeningDelay = 0.3f;
    
    [Header("Death Audio")]
    public AudioClip deathClip;
    [Range(0f, 1f)]
    public float deathVolume = 1f;
    
    [Header("Audio Volume Settings")]
    [Range(0f, 1f)]
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
    public float customSlowdownDuration = 15f;
    
    [Header("FX & Material System")]
    public Renderer Renderer;
    [SerializeField] private CFXR_EffectController slowdownEffect;
    
    [Header("Overlay Emission")]
    [SerializeField] private Color overlayColor = Color.blue;
    [SerializeField] private float overlayIntensity = 2f;
    [SerializeField] private float hdrMultiplier = 3f;
    [SerializeField] private bool applyColorTint = true;
    
    private bool patinaActive = false;
    private Coroutine fxCoroutine;
    private Coroutine slowCoroutine;
    private Coroutine blinkCoroutine;
    private Coroutine hitRecoveryCoroutine;
    
    [SerializeField] private float blinkDurationBeforeEnd = 2f;
    
    private Dictionary<Material, Material> materialInstances = new Dictionary<Material, Material>();
    private Dictionary<Material, Color> originalBaseColors = new Dictionary<Material, Color>();
    private Dictionary<Material, Color> originalEmissionColors = new Dictionary<Material, Color>();
    private Material[] originalMaterials = null;
    private bool materialsInitialized = false;
    
    private int deadLayer;
    private float attackTimeout = 3f;
    private float attackTimer = 0f;
    private float rotationSpeed = 5f;
    private float stateValidationInterval = 0.1f;
    private Coroutine stateValidationCoroutine;
    private float footstepsTimer = 0f;
    private float footstepsInterval = 0.4f;
    
    private PlayerVelocityTracker playerVelocityTracker;
    private Vector3 lastPlayerPosition;
    private Vector3 calculatedPlayerVelocity;
    private float stuckTimer = 0f;
    
    private bool deathObjectActivated = false;
    private Coroutine awakeningDelayCoroutine;
    
    private void Start()
    {
        currentHealth = maxHealth;
        deadLayer = LayerMask.NameToLayer("DeadEnemy");
        InitializeNavMeshAgent();
        InitializeMaterialSystem();
        InitializeAudioSystem();
        InitializePlayerTracking();
        if (slowdownEffect != null) slowdownEffect.gameObject.SetActive(false);
        stateValidationCoroutine = StartCoroutine(ValidateStateRoutine());
        ChangeState(GolemState.Dormant);
        Debug.Log("[Golem] Inizializzato in stato DORMANT");
    }
    
    public void TriggerAwakening()
    {
        if (isDead || isAwakened || awakeningDelayCoroutine != null) return;
        Debug.Log("[Golem] RISVEGLIO RICHIESTO!");
        if (awakeningDelay > 0f)
            awakeningDelayCoroutine = StartCoroutine(TriggerAwakeningWithDelay());
        else
            ExecuteAwakening();
    }
    
    private IEnumerator TriggerAwakeningWithDelay()
    {
        yield return new WaitForSeconds(awakeningDelay);
        ExecuteAwakening();
        awakeningDelayCoroutine = null;
    }
    
    private void ExecuteAwakening()
    {
        if (isDead || isAwakened) return;
        Debug.Log("[Golem] Eseguo animazione risveglio!");
        if (animator != null)
        {
            animator.ResetTrigger("Awaken");
            animator.SetTrigger("Awaken");
        }
        ChangeState(GolemState.Awakening);
    }
    
    public void ActivateCombat()
    {
        if (isDead || isCombatActive) return;
        isCombatActive = true;
        Debug.Log("[Golem] COMBATTIMENTO ATTIVATO!");
        if (currentState == GolemState.Idle || currentState == GolemState.Dormant)
            ChangeState(GolemState.Idle);
    }
    
    public void OnAwakeningComplete()
    {
        if (currentState != GolemState.Awakening) return;
        isAwakened = true;
        Debug.Log("[Golem] Risveglio completato!");
        ChangeState(GolemState.Idle);
    }
    
    public void ForceCompleteAwakening()
    {
        if (isDead) return;
        isAwakened = true;
        if (currentState == GolemState.Awakening || currentState == GolemState.Dormant)
            ChangeState(GolemState.Idle);
    }
    
    public bool IsAwakened() => isAwakened;
    public bool IsCombatActive() => isCombatActive;
    public bool IsDead() => isDead;
    public GolemState GetCurrentState() => currentState;
    
    private void InitializePlayerTracking()
    {
        if (player != null)
        {
            playerVelocityTracker = player.GetComponent<PlayerVelocityTracker>();
            lastPlayerPosition = player.position;
        }
    }

    private void InitializeNavMeshAgent()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent == null) return;
        agent.speed = baseSpeed;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 1f;
        agent.autoBraking = true;
    }
    
    private void InitializeAudioSystem()
    {
        if (attackAudioSource == null) attackAudioSource = GetComponent<AudioSource>();
        if (attackAudioSource == null) attackAudioSource = gameObject.AddComponent<AudioSource>();
        if (attackAudioSource != null)
        {
            attackAudioSource.playOnAwake = false;
            attackAudioSource.loop = false;
            attackAudioSource.volume = attackVolume;
        }
    }
    
    private void InitializeMaterialSystem()
    {
        if (Renderer == null) return;
        foreach (Material mat in Renderer.sharedMaterials)
        {
            if (mat != null)
            {
                if (mat.HasProperty("_BaseColor")) originalBaseColors[mat] = mat.GetColor("_BaseColor");
                if (mat.HasProperty("_EmissionColor")) originalEmissionColors[mat] = mat.GetColor("_EmissionColor");
            }
        }
    }
    
    private Vector3 PredictTargetPosition(Vector3 shooterPos, float bulletSpeed, Vector3 targetPos, Vector3 targetVelocity)
    {
        float distance = Vector3.Distance(shooterPos, targetPos);
        float timeToTarget = distance / bulletSpeed;
        return targetPos + targetVelocity * timeToTarget;
    }
    
    private Vector3 GetPlayerVelocity()
    {
        if (playerVelocityTracker != null) return playerVelocityTracker.SmoothedVelocity;
        if (player != null)
        {
            calculatedPlayerVelocity = (player.position - lastPlayerPosition) / Time.deltaTime;
            lastPlayerPosition = player.position;
            return calculatedPlayerVelocity;
        }
        return Vector3.zero;
    }
    
    private Vector3 CalculateShootDirection()
    {
        if (player == null || projectileSpawnPoint == null) return transform.forward;
        Vector3 shooterPos = projectileSpawnPoint.position;
        Vector3 targetPos = player.position + Vector3.up * targetHeightOffset;
        Vector3 finalTargetPos;
        if (usePredictiveAiming)
        {
            Vector3 playerVel = GetPlayerVelocity();
            finalTargetPos = PredictTargetPosition(shooterPos, projectileSpeed, targetPos, playerVel);
            if (aimErrorAmount > 0) finalTargetPos += Random.insideUnitSphere * aimErrorAmount;
        }
        else
        {
            finalTargetPos = targetPos;
            if (aimErrorAmount > 0) finalTargetPos += Random.insideUnitSphere * aimErrorAmount;
        }
        return (finalTargetPos - shooterPos).normalized;
    }
    
    private void ChangeState(GolemState newState)
    {
        if (isDead && newState != GolemState.Dead) return;
        if (currentState == newState) return;
        Debug.Log($"[Golem] Stato: {currentState} -> {newState}");
        ExitState(currentState);
        previousState = currentState;
        currentState = newState;
        attackTimer = 0f;
        EnterState(newState);
    }
    
    private void ExitState(GolemState state)
    {
        switch (state)
        {
            case GolemState.MeleeAttacking:
            case GolemState.RangedAttacking:
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
                break;
            case GolemState.Moving:
                animator.SetBool("isWalking", false);
                footstepsTimer = 0f;
                break;
            case GolemState.Hit:
                if (hitRecoveryCoroutine != null) { StopCoroutine(hitRecoveryCoroutine); hitRecoveryCoroutine = null; }
                break;
        }
    }
    
    private void EnterState(GolemState state)
    {
        switch (state)
        {
            case GolemState.Dormant:
            case GolemState.Awakening:
            case GolemState.Idle:
                animator.SetBool("isWalking", false);
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
                break;
            case GolemState.Moving:
                animator.SetBool("isWalking", true);
                stuckTimer = 0f;
                break;
            case GolemState.MeleeAttacking:
                StopMovement();
                animator.SetBool("isWalking", false);
                animator.SetTrigger("AttackMelee");
                PlayMeleeAttackSound();
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
                hitRecoveryCoroutine = StartCoroutine(RecoverFromHit());
                break;
            case GolemState.Dead:
                HandleDeath();
                break;
        }
    }
    
    private IEnumerator RecoverFromHit()
    {
        yield return new WaitForSeconds(hitRecoveryTime);
        if (!isDead && currentState == GolemState.Hit) ChangeState(GolemState.Idle);
        hitRecoveryCoroutine = null;
    }
    
    private void Update()
    {
        if (isDead || player == null) return;
        if (currentState == GolemState.Dormant || currentState == GolemState.Awakening) return;
        if (!isCombatActive) { if (currentState != GolemState.Idle) ChangeState(GolemState.Idle); return; }
        if (playerVelocityTracker == null) GetPlayerVelocity();
        if (currentState != GolemState.MeleeAttacking && currentState != GolemState.RangedAttacking)
        {
            meleeTimer = Mathf.Max(0, meleeTimer - Time.deltaTime);
            rangedTimer = Mathf.Max(0, rangedTimer - Time.deltaTime);
        }
        switch (currentState)
        {
            case GolemState.Dead:
            case GolemState.Hit:
                return;
            case GolemState.MeleeAttacking:
            case GolemState.RangedAttacking:
                attackTimer += Time.deltaTime;
                if (attackTimer > attackTimeout) ChangeState(GolemState.Idle);
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
        RotateTowardsPlayer();
        if (dist <= meleeRange)
        {
            if (meleeTimer <= 0f) ChangeState(GolemState.MeleeAttacking);
            else if (currentState != GolemState.Idle) ChangeState(GolemState.Idle);
            return;
        }
        if (dist <= rangedRange)
        {
            if (rangedTimer <= 0f) ChangeState(GolemState.RangedAttacking);
            else if (CanMoveTowardsPlayer()) { if (currentState != GolemState.Moving) ChangeState(GolemState.Moving); MoveTowardsPlayer(); }
            else if (currentState != GolemState.Idle) ChangeState(GolemState.Idle);
            return;
        }
        if (CanMoveTowardsPlayer()) { if (currentState != GolemState.Moving) ChangeState(GolemState.Moving); MoveTowardsPlayer(); }
        else if (currentState != GolemState.Idle) ChangeState(GolemState.Idle);
    }

    private bool CanMoveTowardsPlayer() => agent != null && agent.enabled && agent.isOnNavMesh && stuckTimer <= 1f;
    
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
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
        animator.SetBool("isWalking", false);
    }
    
    private void MoveTowardsPlayer()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) { ChangeState(GolemState.Idle); return; }
        float distToPlayer = Vector3.Distance(transform.position, player.position);
        footstepsTimer -= Time.deltaTime;
        if (footstepsTimer <= 0f && footstepsClip != null && attackAudioSource != null && distToPlayer <= 100f)
        {
            attackAudioSource.clip = footstepsClip;
            attackAudioSource.volume = attackVolume * 0.6f;
            attackAudioSource.Play();
            footstepsTimer = footstepsInterval;
        }
        agent.isStopped = false;
        agent.speed = isSlow ? baseSpeed * slowFactor : baseSpeed;
        bool pathSet = agent.SetDestination(player.position);
        if (pathSet)
        {
            float currentSpeed = agent.velocity.magnitude;
            if (currentSpeed < 0.1f && agent.hasPath && agent.remainingDistance > 1f) stuckTimer += Time.deltaTime;
            else stuckTimer = 0f;
            if (stuckTimer > 1f)
            {
                if (distToPlayer <= meleeRange && meleeTimer <= 0f) ChangeState(GolemState.MeleeAttacking);
                else if (rangedTimer <= 0f) ChangeState(GolemState.RangedAttacking);
                else ChangeState(GolemState.Idle);
                return;
            }
            animator.SetBool("isWalking", currentSpeed > 0.1f || agent.pathPending || (agent.hasPath && agent.remainingDistance > agent.stoppingDistance));
        }
        else ChangeState(GolemState.Idle);
    }
    
    public void EnemyAttackHitbox()
    {
        if (currentState != GolemState.MeleeAttacking || isDead) return;
        Collider[] hits = Physics.OverlapBox(transform.position + transform.forward * (meleeRange * 0.5f), new Vector3(10f, 10f, 10f), transform.rotation, LayerMask.GetMask("PlayerHurtbox"));
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
        ChangeState(GolemState.Idle);
    }
    
    public void SpawnProjectile()
    {
        if (currentState != GolemState.RangedAttacking || isDead || projectilePrefab == null || projectileSpawnPoint == null) return;
        Vector3 shootDirection = CalculateShootDirection();
        GameObject proj = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.LookRotation(shootDirection));
        Golem_Projectile projectile = proj.GetComponent<Golem_Projectile>();
        if (projectile != null) projectile.Init(shootDirection, projectileSpeed, isSlow);
        PlayRangedAttackSound();
    }
    
    public void EndRangedAttack() { if (currentState == GolemState.RangedAttacking) ChangeState(GolemState.Idle); }
    public void EndHit() { if (currentState == GolemState.Hit) ChangeState(GolemState.Idle); }
    
    // ============ DEATH ANIMATION EVENTS ============
    
 public void OnDeathAnimationComplete()
{
    if (!isDead || deathObjectActivated) return;
    Debug.Log("[Golem] Animazione morte COMPLETATA!");
    
    // Attiva il dialogo se configurato
    if (deathDialogueSystem != null)
    {
        deathDialogueSystem.TriggerDialogue();
        Debug.Log($"[Golem] Dialogo '{deathDialogueSystem.name}' attivato!");
    }
    
    deathObjectActivated = true;
    OnGolemDeathAnimationComplete?.Invoke();
}
    
    public void PlayDeathSound()
    {
        if (attackAudioSource != null && deathClip != null)
        {
            attackAudioSource.clip = deathClip;
            attackAudioSource.volume = deathVolume;
            attackAudioSource.Play();
        }
    }
    
    public void PlayAwakeningSound()
    {
        if (attackAudioSource != null && awakeningClip != null)
        {
            attackAudioSource.clip = awakeningClip;
            attackAudioSource.volume = awakeningVolume;
            attackAudioSource.Play();
        }
    }
    
    public void PlayMeleeAttackSound()
    {
        if (attackAudioSource != null && meleeAttackClip != null)
        {
            attackAudioSource.clip = meleeAttackClip;
            attackAudioSource.volume = attackVolume;
            attackAudioSource.Play();
        }
    }
    
    public void PlayRangedAttackSound()
    {
        if (attackAudioSource != null && rangedAttackClip != null)
        {
            attackAudioSource.clip = rangedAttackClip;
            attackAudioSource.volume = attackVolume;
            attackAudioSource.Play();
        }
    }
    
    public void TakeDamage(float amount)
    {
        if (isDead) return;

        // Check invincibilità (i-frames)
        if (Time.time - lastHitTime < hitInvincibilityTime)
        {
            Debug.Log("[Golem] Colpo ignorato - ancora invincibile!");
            return;
        }

        lastHitTime = Time.time;
        currentHealth -= amount;
        Debug.Log($"[Golem] Danni: {amount}. HP: {currentHealth}");

        if (currentHealth <= 0)
        {
            isDead = true;
            ChangeState(GolemState.Dead);
        }
        else ChangeState(GolemState.Hit);
    }
    
    private void HandleDeath()
    {
        isDead = true;
        Debug.Log("[Golem] MORTO!");
        if (agent != null) agent.enabled = false;
        if (patinaActive) SetOverlayActive(false);
        OnGolemDeath?.Invoke();

        // Resetta tutti i trigger per evitare conflitti con l'animazione di morte
        animator.ResetTrigger("Hit");
        animator.ResetTrigger("AttackMelee");
        animator.ResetTrigger("AttackRanged");
        animator.ResetTrigger("Awaken");
        animator.SetTrigger("Die");
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders) { if (col.CompareTag("GolemHurtbox")) col.enabled = false; }
        if (stateValidationCoroutine != null) { StopCoroutine(stateValidationCoroutine); stateValidationCoroutine = null; }
        if (awakeningDelayCoroutine != null) { StopCoroutine(awakeningDelayCoroutine); awakeningDelayCoroutine = null; }
    }
    
    public void StartSlow(float duration, SlowdownAbility sourceAbility)
    {
        if (isSlow && slowCoroutine != null) StopCoroutine(slowCoroutine);
        else SetSlow(true);
        activeSlowdownAbility = sourceAbility;
        slowCoroutine = StartCoroutine(SlowDurationRoutine(duration));
    }
    
    private IEnumerator SlowDurationRoutine(float duration)
    {
        float normalDuration = duration - blinkDurationBeforeEnd;
        if (normalDuration > 0) yield return new WaitForSeconds(normalDuration);
        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        blinkCoroutine = StartCoroutine(BlinkOverlayWhileSlow());
        yield return new WaitForSeconds(blinkDurationBeforeEnd);
        SetSlow(false);
        activeSlowdownAbility = null;
        slowCoroutine = null;
        if (blinkCoroutine != null) { StopCoroutine(blinkCoroutine); blinkCoroutine = null; }
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
                if (fxCoroutine != null) StopCoroutine(fxCoroutine);
                fxCoroutine = StartCoroutine(PlayEffectOnce());
                slowdownEffectPlayedThisCycle = true;
            }
            if (agent != null && agent.enabled) agent.speed = 3f * slowFactor;
            animator.speed = animationSlowFactor;
        }
        else
        {
            SetOverlayActive(false);
            if (slowdownEffect != null) { slowdownEffect.StopEffect(); slowdownEffect.gameObject.SetActive(false); }
            if (agent != null && agent.enabled) agent.speed = 3f;
            animator.speed = 1f;
            slowdownEffectPlayedThisCycle = false;
            if (currentState == GolemState.MeleeAttacking || currentState == GolemState.RangedAttacking) ChangeState(GolemState.Idle);
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
        while (isSlow) { SetOverlayActive(state); state = !state; yield return new WaitForSeconds(blinkRate); }
        SetOverlayActive(false);
    }
    
    public void SetOverlayActive(bool active) { if (Renderer != null) SetEmissiveOverlay(active); }
    
    private void SetEmissiveOverlay(bool active)
    {
        if (Renderer == null) return;
        if (!materialsInitialized) { originalMaterials = Renderer.sharedMaterials; materialsInitialized = true; }
        if (active)
        {
            Material[] newMaterials = new Material[originalMaterials.Length];
            for (int i = 0; i < originalMaterials.Length; i++)
            {
                Material originalMat = originalMaterials[i];
                if (originalMat == null) { newMaterials[i] = null; continue; }
                Material instanceMat;
                if (!materialInstances.ContainsKey(originalMat)) { materialInstances[originalMat] = new Material(originalMat); }
                instanceMat = materialInstances[originalMat];
                if (instanceMat.HasProperty("_EmissionColor"))
                {
                    instanceMat.SetColor("_EmissionColor", overlayColor * overlayIntensity * hdrMultiplier);
                    instanceMat.EnableKeyword("_EMISSION");
                    instanceMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                if (applyColorTint && instanceMat.HasProperty("_BaseColor") && originalBaseColors.ContainsKey(originalMat))
                {
                    Color originalColor = originalBaseColors[originalMat];
                    Color tintedColor = Color.Lerp(originalColor, originalColor * overlayColor, 0.3f);
                    tintedColor.a = originalColor.a;
                    instanceMat.SetColor("_BaseColor", tintedColor);
                }
                newMaterials[i] = instanceMat;
            }
            Renderer.materials = newMaterials;
        }
        else Renderer.materials = originalMaterials;
        patinaActive = active;
    }
    
    private IEnumerator ValidateStateRoutine()
    {
        while (!isDead)
        {
            yield return new WaitForSeconds(stateValidationInterval);
            if (!isDead) ValidateCurrentState();
        }
    }
    
    private void ValidateCurrentState()
    {
        if (agent == null || !agent.enabled || currentState == GolemState.Dormant || currentState == GolemState.Awakening) return;
        bool animatorIsWalking = animator.GetBool("isWalking");
        bool agentIsMoving = agent.velocity.magnitude > 0.1f && !agent.isStopped;
        bool shouldBeWalking = currentState == GolemState.Moving && agentIsMoving;
        if (animatorIsWalking != shouldBeWalking) animator.SetBool("isWalking", shouldBeWalking);
    }
    
    private void OnDestroy()
    {
        foreach (var kvp in materialInstances) { if (kvp.Value != null) DestroyImmediate(kvp.Value); }
        materialInstances.Clear();
        if (stateValidationCoroutine != null) StopCoroutine(stateValidationCoroutine);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, meleeRange);
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, rangedRange);
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, viewRadius);
    }
    
    [ContextMenu("Test - Trigger Awakening")] public void TestTriggerAwakening() => TriggerAwakening();
    [ContextMenu("Test - Activate Combat")] public void TestActivateCombat() => ActivateCombat();
    [ContextMenu("Test - Force Death")] public void TestForceDeath() { currentHealth = 0; ChangeState(GolemState.Dead); }
    [ContextMenu("Test - Death Animation Complete")] public void TestDeathAnimationComplete() => OnDeathAnimationComplete();
}