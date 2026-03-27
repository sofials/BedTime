using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class TurtleShell : MonoBehaviour
{
    [Header("Movement & Patrol")]
    public NavMeshAgent agent;
    public float waitTimeAtPoint = 3f;
    public float rotateTime = 2f;
    public float walkSpeed = 4f;
    public float runSpeed = 6f;
    public Transform[] waypoints;
    
    [Header("Slowdown Custom Duration")]
    [Tooltip("Durata personalizzata per lo slowdown (0 = usa durata default dell'abilità)")]
    public float customSlowdownDuration = 15f;

    [Header("Vision & Attack")]
    public float viewRadius = 10f;
    public float viewAngle = 90f;
    public float attackRange = 2f;
    public LayerMask playerMask;
    public LayerMask obstacleMask;
    public float pushForce = 60f;

    [Header("Slowdown Settings")]
    public bool isSlow = false;
    public float slowFactor = 0.5f;

    [Header("Attack Settings")]
    public float attackDamage = 10f;
    public float slowedAttackDamage = 5f;
    public float attackCooldown = 1.5f;

    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;
    [Header("Custom Audio - Multiple Sources")]
public AudioSource[] customAudioSources; // Array di AudioSources
public AudioClip[] customAudioClips;     // Array di AudioClips corrispondenti
[Tooltip("Se true, alterna in ordine sequenziale. Se false, sceglie casualmente")]
public bool useSequentialOrder = true;
private int currentCustomAudioIndex = 0;

    [Header("VFX")]
    public Renderer Renderer;
    public CFXR_EffectController deathEffectController;
    [SerializeField] private CFXR_EffectController slowdownEffect;
    public SlowdownAbility activeSlowdownAbility;
     [Header("Overlay Emission - Nuovo Sistema")]
    [SerializeField] private Color overlayColor = Color.red;
    [SerializeField] private float overlayIntensity = 2f;
    [Tooltip("Moltiplicatore aggiuntivo per HDR emission (valori alti = più luce)")]
    [SerializeField] private float hdrMultiplier = 3f;
    [Tooltip("Se true, mantiene anche il tint del Base Color oltre all'emission")]
    [SerializeField] private bool applyColorTint = true;

    [Header("Warning System")]
    [Tooltip("Time before warning can be triggered again (should be ~20s)")]
    public float warningResetTime = 20f;
    public float visionPersistenceTime = 3f;
    public GameObject enemyChildObjectToActivate; // Visual indicator for warning
    
    [Header("Warning Audio")]
    public AudioSource warningAudioSource;
    public AudioClip warningAudioClip;
    public AudioSource secondAudioSource;
    public AudioClip secondAudioClip;

    [Header("🔍 WARNING DEBUG")]
    [Tooltip("Enable detailed warning system debugging")]
    public bool enableWarningDebug = false;
    [Tooltip("Show debug logs for vision changes")]
    public bool debugVisionChanges = false;

    private bool isDead = false;
    private bool isStunned = false;
    public bool hasBeenHitWhileSlow = false;

    private int currentWaypoint = 0;
    private float waitTimer;
    private Transform player;
    private bool playerVisible = false;
    private bool isPatrolling = true;
    private bool isAttacking = false;
    private float attackTimer = 0f;
    private Animator animator;
    private bool patinaActive = false;
    
    // Warning System Variables
    private float playerLastSeenTime = 0f;
    private bool hasEverSeenPlayer = false;
    private bool hasPlayedWarningEver = false;
    private float lastWarningTime = -999f;
    private float playerFirstSeenTime = 0f;
    private bool isCurrentlyInWarningCooldown = false;
    private Coroutine currentWarningCoroutine = null;
    
    // Debug variables
    private int warningCallCount = 0;
    private float lastWarningAttemptTime = 0f;
    private string lastWarningBlockReason = "";
    private Dictionary<Material, Material> materialInstances = new Dictionary<Material, Material>();
    private Dictionary<Material, Color> originalBaseColors = new Dictionary<Material, Color>();
    private Dictionary<Material, Color> originalEmissionColors = new Dictionary<Material, Color>();
    private Material[] originalMaterials = null;
    private bool materialsInitialized = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        ForceResetAllWarningVariables();
        InitializeMaterialSystem();
    }
public void PlayCustomAudio()
{
    if (isDead) return;

    // Controlla che gli array non siano vuoti
    if (customAudioSources == null || customAudioSources.Length == 0)
    {
        Debug.LogWarning($"[{name}] CustomAudioSources array is empty!");
        return;
    }

    if (customAudioClips == null || customAudioClips.Length == 0)
    {
        Debug.LogWarning($"[{name}] CustomAudioClips array is empty!");
        return;
    }

    int sourceIndex;
    int clipIndex;

    if (useSequentialOrder)
    {
        // Modalità sequenziale
        sourceIndex = currentCustomAudioIndex % customAudioSources.Length;
        clipIndex = currentCustomAudioIndex % customAudioClips.Length;
        
        // Incrementa l'indice per la prossima volta
        currentCustomAudioIndex = (currentCustomAudioIndex + 1) % Mathf.Max(customAudioSources.Length, customAudioClips.Length);
    }
    else
    {
        // Modalità casuale
        sourceIndex = Random.Range(0, customAudioSources.Length);
        clipIndex = Random.Range(0, customAudioClips.Length);
    }

    // Ottieni l'AudioSource e l'AudioClip selezionati
    AudioSource selectedSource = customAudioSources[sourceIndex];
    AudioClip selectedClip = customAudioClips[clipIndex];

    // Controlla che non siano null
    if (selectedSource != null && selectedClip != null)
    {
        // Ferma l'audio corrente se in riproduzione
        if (selectedSource.isPlaying)
        {
            selectedSource.Stop();
        }

        selectedSource.PlayOneShot(selectedClip);
        
        Debug.Log($"[{name}] Playing custom audio - Source: {sourceIndex}, Clip: '{selectedClip.name}'");
    }
    else
    {
        Debug.LogWarning($"[{name}] Selected audio source or clip is null! Source: {selectedSource}, Clip: {selectedClip}");
    }
}
    private void InitializeMaterialSystem()
    {
        if (Renderer == null)
        {
            Debug.LogWarning($"[TurtleShell] Nessun Renderer trovato su {gameObject.name}");
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
        
        Debug.Log($"[TurtleShell] Sistema materiali inizializzato per {gameObject.name}");
    }
    private void Start()
    {
        waitTimer = waitTimeAtPoint;

        if (waypoints != null && waypoints.Length > 0)
        {
            // ✅ Verifica che l'agent sia sulla NavMesh prima di impostare la destinazione
            if (agent.isOnNavMesh)
            {
                agent.SetDestination(waypoints[currentWaypoint].position);
            }
            else
            {
                // Se non è sulla NavMesh, ritenta nel prossimo frame
                StartCoroutine(SetDestinationWhenReady());
            }
        }

        agent.speed = walkSpeed;
        currentHealth = maxHealth;

        if (deathEffectController != null)
            deathEffectController.StopEffect();

        if (slowdownEffect != null)
            slowdownEffect.gameObject.SetActive(false);
            
        if (enemyChildObjectToActivate != null)
            enemyChildObjectToActivate.SetActive(false);

        if (enableWarningDebug)
        {
            WarningDebugLog($"🔧 INITIALIZED - hasPlayedWarningEver: {hasPlayedWarningEver}, hasEverSeenPlayer: {hasEverSeenPlayer}");
        }
    }

    /// <summary>
    /// Attende che il NavMeshAgent sia sulla NavMesh prima di impostare la destinazione
    /// </summary>
    private IEnumerator SetDestinationWhenReady()
    {
        // Attendi fino a quando l'agent è sulla NavMesh
        while (!agent.isOnNavMesh)
        {
            yield return null; // Aspetta il prossimo frame
        }

        // Ora che l'agent è sulla NavMesh, imposta la destinazione
        if (waypoints != null && waypoints.Length > 0 && currentWaypoint >= 0 && currentWaypoint < waypoints.Length)
        {
            agent.SetDestination(waypoints[currentWaypoint].position);
        }
    }

    private void ForceResetAllWarningVariables()
    {
        lastWarningTime = -999f;
        playerFirstSeenTime = 0f;
        isCurrentlyInWarningCooldown = false;

        if (currentWarningCoroutine != null)
        {
            StopCoroutine(currentWarningCoroutine);
            currentWarningCoroutine = null;
        }

        player = null;
        playerVisible = false;
        isPatrolling = true;
        isAttacking = false;

        if (enableWarningDebug)
        {
            WarningDebugLog("🔄 RESET - Warning variables reset (keeping lifetime flags)");
        }
    }

    private void Update()
    {
        if (isDead || isStunned)
        {
            agent.isStopped = true;
            return;
        }

        attackTimer -= Time.deltaTime;
        
        // Force reset if player too far
        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer > viewRadius * 2f)
            {
                if (enableWarningDebug)
                {
                    WarningDebugLog($"🔄 FORCE RESET: Player too far {distanceToPlayer:F2}m > {viewRadius * 2f}m");
                }
                player = null;
                playerVisible = false;
                ResetToPatrol();
                return;
            }
        }

        UpdatePlayerVisibility();

        // Check warning cooldown
        if (isCurrentlyInWarningCooldown && Time.time - lastWarningTime >= warningResetTime)
        {
            isCurrentlyInWarningCooldown = false;
            if (enableWarningDebug)
            {
                WarningDebugLog("⏰ COOLDOWN ENDED - Ready for next warning");
            }
        }

        if (playerVisible)
        {
            if (isPatrolling)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, player.position);
                isPatrolling = false;
                playerFirstSeenTime = Time.time;

                // Warning system trigger
                if (CanTriggerWarningFirstTime(distanceToPlayer))
                {
                    TriggerWarningSequence();
                }
                else if (!hasEverSeenPlayer)
                {
                    hasEverSeenPlayer = true;
                    if (enableWarningDebug)
                    {
                        WarningDebugLog($"👁️ FIRST SIGHT - No audio trigger (reason: {lastWarningBlockReason})");
                    }
                }
            }
            ChasePlayer();
        }
        else
        {
            if (!isPatrolling && !IsPlayerRecentlyLost())
            {
                ResetToPatrol();
            }
            else if (!isPatrolling && IsPlayerRecentlyLost())
            {
                ChaseLastKnownPosition();
            }
            else
            {
                Patrol();
            }
        }
    }

    private bool CanTriggerWarningFirstTime(float playerDistance = -1f)
    {
        warningCallCount++;
        lastWarningAttemptTime = Time.time;

        if (enableWarningDebug)
        {
            WarningDebugLog($"🔍 WARNING CHECK #{warningCallCount} - Starting validation...");
        }

        // Check 1: Enemy alive
        if (isDead)
        {
            lastWarningBlockReason = "Enemy is dead";
            if (enableWarningDebug) WarningDebugLog($"❌ BLOCKED: {lastWarningBlockReason}");
            return false;
        }

        // Check 2: Never played warning before (MAIN CHECK)
        if (hasPlayedWarningEver)
        {
            lastWarningBlockReason = "Warning already played in lifetime";
            if (enableWarningDebug) WarningDebugLog($"❌ BLOCKED: {lastWarningBlockReason}");
            return false;
        }

        // Check 3: Player visibility
        if (!playerVisible || player == null)
        {
            lastWarningBlockReason = "Player not visible";
            if (enableWarningDebug) WarningDebugLog($"❌ BLOCKED: {lastWarningBlockReason}");
            return false;
        }

        // Check 4: Distance validation
        if (playerDistance < 0f)
        {
            playerDistance = Vector3.Distance(transform.position, player.position);
        }
        if (playerDistance > viewRadius)
        {
            lastWarningBlockReason = $"Distance {playerDistance:F2}m > viewRadius {viewRadius}m";
            if (enableWarningDebug) WarningDebugLog($"❌ BLOCKED: {lastWarningBlockReason}");
            return false;
        }

        // Check 5: Audio components
        if (warningAudioSource == null || warningAudioClip == null)
        {
            lastWarningBlockReason = "Missing audio components";
            if (enableWarningDebug) 
            {
                WarningDebugLog($"❌ BLOCKED: {lastWarningBlockReason} (source: {warningAudioSource != null}, clip: {warningAudioClip != null})");
            }
            return false;
        }

        // ✅ All checks passed
        if (enableWarningDebug)
        {
            WarningDebugLog($"✅ VALIDATION PASSED - Distance: {playerDistance:F2}m, All components ready");
        }
        
        lastWarningBlockReason = "All checks passed";
        return true;
    }

    private void TriggerWarningSequence()
    {
        // Final safety checks
        if (isDead)
        {
            if (enableWarningDebug) WarningDebugLog("🚨 TRIGGER ABORTED: Enemy is dead");
            return;
        }

        if (player == null)
        {
            if (enableWarningDebug) WarningDebugLog("🚨 TRIGGER ABORTED: No player reference");
            return;
        }

        float finalDistance = Vector3.Distance(transform.position, player.position);
        if (finalDistance > viewRadius)
        {
            if (enableWarningDebug) 
            {
                WarningDebugLog($"🚨 TRIGGER ABORTED: Final distance check failed {finalDistance:F2}m > {viewRadius}m");
            }
            
            playerVisible = false;
            player = null;
            ResetToPatrol();
            return;
        }

        // Mark warning as played forever
        hasPlayedWarningEver = true;
        hasEverSeenPlayer = true;

        WarningDebugLog($"🔊 WARNING TRIGGERED! 🔊 Distance: {finalDistance:F2}m, Time: {Time.time:F2}s");
        lastWarningTime = Time.time;
        isCurrentlyInWarningCooldown = true;

        // Visual indicator
        if (enemyChildObjectToActivate != null)
            enemyChildObjectToActivate.SetActive(true);

        // Audio playback
        if (warningAudioSource != null && warningAudioClip != null)
        {
            warningAudioSource.Stop();
            warningAudioSource.PlayOneShot(warningAudioClip);
            
            if (enableWarningDebug)
            {
                WarningDebugLog($"🎧 AUDIO PLAYED - Clip: '{warningAudioClip.name}', Length: {warningAudioClip.length:F2}s");
            }

            // Start coroutine sequence
            if (currentWarningCoroutine != null)
                StopCoroutine(currentWarningCoroutine);

            currentWarningCoroutine = StartCoroutine(WarningSequenceCoroutine());
        }
        else
        {
            if (enableWarningDebug)
            {
                WarningDebugLog("🚨 AUDIO FAILED - Missing components!");
            }
        }
    }

    private IEnumerator WarningSequenceCoroutine()
    {
        float waitTime = warningAudioClip != null ? warningAudioClip.length : 2f;
        
        if (enableWarningDebug)
        {
            WarningDebugLog($"⏳ WARNING SEQUENCE - Waiting {waitTime:F2}s for audio to complete...");
        }
        
        yield return new WaitForSeconds(waitTime);

        if (playerVisible || IsPlayerRecentlyLost())
        {
            PlaySecondAudio();
        }

        if (enemyChildObjectToActivate != null)
            enemyChildObjectToActivate.SetActive(false);

        if (enableWarningDebug)
        {
            WarningDebugLog("✅ WARNING SEQUENCE COMPLETE");
        }

        currentWarningCoroutine = null;
    }

    private void PlaySecondAudio()
    {
        if (secondAudioSource != null && secondAudioClip != null)
        {
            if (secondAudioSource.isPlaying)
                secondAudioSource.Stop();

            secondAudioSource.PlayOneShot(secondAudioClip);
            
            if (enableWarningDebug)
            {
                WarningDebugLog($"🎧 SECOND AUDIO PLAYED - Clip: '{secondAudioClip.name}'");
            }
        }
        else
        {
            if (enableWarningDebug)
            {
                WarningDebugLog("🚨 SECOND AUDIO FAILED - Missing components!");
            }
        }
    }

    private bool IsPlayerRecentlyLost()
    {
        if (player == null) return false;
        float timeSinceLastSeen = Time.time - playerLastSeenTime;
        return !playerVisible && timeSinceLastSeen < visionPersistenceTime;
    }

    private void ChaseLastKnownPosition()
    {
        if (player != null && agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = isSlow ? walkSpeed * slowFactor : runSpeed;
            agent.SetDestination(player.position);
        }
    }

    private void UpdatePlayerVisibility()
    {
        if (isDead)
        {
            playerVisible = false;
            player = null;
            playerLastSeenTime = 0f;
            return;
        }

        bool previouslyVisible = playerVisible;
        playerVisible = false;
        Transform detectedPlayer = null;

        Collider[] hits = Physics.OverlapSphere(transform.position, viewRadius, playerMask);
        
        foreach (var hit in hits)
        {
            Vector3 dirToPlayer = (hit.transform.position - transform.position);
            float distance = dirToPlayer.magnitude;

            if (distance > viewRadius) continue;

            Vector3 dirNormalized = dirToPlayer.normalized;
            float angle = Vector3.Angle(transform.forward, dirNormalized);

            if (angle <= viewAngle / 2)
            {
                if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, dirNormalized, distance, obstacleMask))
                {
                    playerVisible = true;
                    detectedPlayer = hit.transform;

                    if (!hasEverSeenPlayer)
                    {
                        hasEverSeenPlayer = true;
                        if (enableWarningDebug)
                        {
                            WarningDebugLog($"👁️ FIRST SIGHT EVER - Player detected at {distance:F2}m");
                        }
                    }

                    if (debugVisionChanges && !previouslyVisible)
                    {
                        WarningDebugLog($"👁️ VISION ACQUIRED - Player at {distance:F2}m, angle: {angle:F1}°");
                    }
                    break;
                }
            }
        }

        // Update player reference
        if (playerVisible && detectedPlayer != null)
        {
            player = detectedPlayer;
            playerLastSeenTime = Time.time;
        }

        // Handle vision loss
        if (!playerVisible && player != null)
        {
            float timeSinceLastSeen = Time.time - playerLastSeenTime;
            if (timeSinceLastSeen >= visionPersistenceTime)
            {
                if (debugVisionChanges)
                {
                    WarningDebugLog($"👁️ VISION LOST - Player lost for {timeSinceLastSeen:F1}s");
                }
                player = null;
                playerFirstSeenTime = 0f;
            }
        }
    }

    private void ChasePlayer()
    {
        if (isStunned || player == null) return;

        RotateTowards(player.position);
        float dist = Vector3.Distance(transform.position, player.position);

        if (dist <= attackRange)
        {
            if (isSlow)
            {
                InflictPushToPlayer();
                agent.isStopped = false;
                agent.speed = walkSpeed * slowFactor;
                agent.SetDestination(player.position);

                if (isAttacking)
                {
                    isAttacking = false;
                    animator.SetBool("isAttacking", false);
                }
                return;
            }

            if (attackTimer <= 0f)
            {
                if (!isAttacking)
                {
                    isAttacking = true;
                    animator.SetBool("isAttacking", true);
                }

                animator.SetTrigger("Attack");
                agent.isStopped = true;
                attackTimer = attackCooldown;
            }
            return;
        }

        if (isAttacking)
        {
            isAttacking = false;
            animator.SetBool("isAttacking", false);
        }

        agent.isStopped = false;
        agent.speed = isSlow ? walkSpeed * slowFactor : runSpeed;
        agent.SetDestination(player.position);
    }

    private void CleanupWarningSequence()
    {
        if (enemyChildObjectToActivate != null && enemyChildObjectToActivate.activeSelf)
        {
            enemyChildObjectToActivate.SetActive(false);
        }

        if (currentWarningCoroutine != null)
        {
            StopCoroutine(currentWarningCoroutine);
            currentWarningCoroutine = null;
        }

        if (warningAudioSource != null && warningAudioSource.isPlaying)
        {
            warningAudioSource.Stop();
        }

        if (secondAudioSource != null && secondAudioSource.isPlaying)
        {
            secondAudioSource.Stop();
        }
    }

    public void EnemyAttackHitbox()
    {
        if (isStunned || player == null) return;

        HurtBox playerHurtBox = player.GetComponentInChildren<HurtBox>();
        if (playerHurtBox != null)
        {
            Vector3 pushDir = (player.position - transform.position).normalized;
            float damageToApply = isSlow ? 0f : slowedAttackDamage;
            playerHurtBox.OnHit(pushDir, pushForce, damageToApply);
        }
        else
        {
            Debug.LogWarning("Player hurtbox non trovata!");
        }
    }

    private void InflictPushToPlayer()
    {
        if (player == null) return;

        HurtBox playerHurtBox = player.GetComponentInChildren<HurtBox>();
        if (playerHurtBox != null)
        {
            Vector3 pushDir = (player.position - transform.position).normalized;
            playerHurtBox.OnHit(pushDir, pushForce, 0f);
        }
    }

    private void Patrol()
    {
        if (!agent.isOnNavMesh || isStunned) return;

        agent.speed = walkSpeed;

        if (!agent.hasPath || agent.remainingDistance < agent.stoppingDistance + 0.1f)
        {
            if (waitTimer <= 0f)
            {
                GoToNextWaypoint();
                waitTimer = waitTimeAtPoint;
            }
            else
            {
                agent.isStopped = true;
                waitTimer -= Time.deltaTime;
            }
        }
        else
        {
            agent.isStopped = false;
        }
    }

    private void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
        agent.SetDestination(waypoints[currentWaypoint].position);
    }

    private void ResetToPatrol()
    {
        isPatrolling = true;
        isAttacking = false;
        waitTimer = waitTimeAtPoint;
        animator.SetBool("isAttacking", false);
        agent.isStopped = false;
        agent.speed = walkSpeed;
        
        CleanupWarningSequence();
        FindClosestWaypoint();
    }

    private void FindClosestWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        float minDist = float.MaxValue;
        for (int i = 0; i < waypoints.Length; i++)
        {
            float dist = Vector3.Distance(transform.position, waypoints[i].position);
            if (dist < minDist)
            {
                minDist = dist;
                currentWaypoint = i;
            }
        }
        agent.SetDestination(waypoints[currentWaypoint].position);
    }

    public void SetSlow(bool slow)
    {
        Debug.Log($"SetSlow chiamato con valore: {slow}");
        isSlow = slow;
        agent.speed = slow ? walkSpeed * slowFactor : walkSpeed;
        animator.SetBool("isSlow", slow);

        if (slow)
        {
            SetOverlayActive(true);
            PlaySlowdownEffect(1f);
        }
        else
        {
            SetOverlayActive(false);
        }

        if (!slow)
            hasBeenHitWhileSlow = false;
    }

    public void StartBlinkingOverlay(float duration)
    {
        if (Renderer == null) return;
        StartCoroutine(BlinkOverlay(duration));
    }

    private IEnumerator BlinkOverlay(float duration)
    {
        float elapsed = 0f;
        float blinkRate = 0.2f;
        bool state = true;

        while (elapsed < duration)
        {
            SetOverlayActive(state);
            state = !state;
            yield return new WaitForSeconds(blinkRate);
            elapsed += blinkRate;
        }

        SetOverlayActive(false);
    }
 public void SetOverlayActive(bool active)
    {
        Debug.Log($"[TurtleShell] *** SetOverlayActive({active}) chiamato su {gameObject.name} ***");
        
        if (Renderer == null) 
        {
            Debug.LogWarning($"[TurtleShell] Renderer nullo su {gameObject.name}");
            return;
        }
        
        Debug.Log($"[TurtleShell] Renderer OK, chiamando SetEmissiveOverlay({active})");
        SetEmissiveOverlay(active);
    }

    private void SetEmissiveOverlay(bool active)
    {
        Debug.Log($"[TurtleShell] SetEmissiveOverlay({active}) - inizio processing su {gameObject.name}");
        
        // INIZIALIZZA i materiali originali solo la prima volta
        if (!materialsInitialized)
        {
            originalMaterials = Renderer.sharedMaterials; // USA sharedMaterials per ottenere gli originali
            materialsInitialized = true;
            Debug.Log($"[TurtleShell] Materiali originali salvati: {originalMaterials.Length}");
        }
        
        Material[] currentMaterials = Renderer.materials; // Questi possono essere istanze
        bool materialsChanged = false;

        Debug.Log($"[TurtleShell] Materiali da processare: {currentMaterials.Length}");

        for (int i = 0; i < originalMaterials.Length; i++)
        {
            Material originalMat = originalMaterials[i];
            if (originalMat == null) continue;

            Debug.Log($"[TurtleShell] Processando materiale {i}: {originalMat.name}");

            Material instanceMat;

            // Crea istanza del materiale SOLO se non esiste ancora
            if (!materialInstances.ContainsKey(originalMat))
            {
                Material newInstance = new Material(originalMat);
                materialInstances[originalMat] = newInstance;
                currentMaterials[i] = newInstance;
                materialsChanged = true;
                instanceMat = newInstance;
                Debug.Log($"[TurtleShell] Creata PRIMA istanza per materiale {originalMat.name}");
            }
            else
            {
                // Usa l'istanza esistente
                instanceMat = materialInstances[originalMat];
                if (currentMaterials[i] != instanceMat)
                {
                    currentMaterials[i] = instanceMat;
                    materialsChanged = true;
                }
                Debug.Log($"[TurtleShell] Usando istanza ESISTENTE per materiale {originalMat.name}");
            }

            if (active)
            {
                Debug.Log($"[TurtleShell] ATTIVANDO overlay per materiale {instanceMat.name}");
                
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
                    
                    Debug.Log($"[TurtleShell] Emission attivata con colore {hdrEmission}");
                }
                else
                {
                    Debug.LogWarning($"[TurtleShell] Materiale {instanceMat.name} non ha _EmissionColor");
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
                        Debug.Log($"[TurtleShell] BaseColor tint applicato");
                    }
                }
            }
            else
            {
                Debug.Log($"[TurtleShell] DISATTIVANDO overlay per materiale {instanceMat.name}");
                
                // Ripristina colori originali
                if (instanceMat.HasProperty("_EmissionColor") && originalEmissionColors.ContainsKey(originalMat))
                {
                    Color originalEmission = originalEmissionColors[originalMat];
                    instanceMat.SetColor("_EmissionColor", originalEmission);
                    
                    // Se l'originale non aveva emission, disabilitalo
                    if (originalEmission == Color.black || originalEmission.maxColorComponent <= 0.01f)
                    {
                        instanceMat.DisableKeyword("_EMISSION");
                        instanceMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    }
                    
                    Debug.Log($"[TurtleShell] Emission disattivata, ripristinato colore originale {originalEmission}");
                }

                if (instanceMat.HasProperty("_BaseColor") && originalBaseColors.ContainsKey(originalMat))
                {
                    instanceMat.SetColor("_BaseColor", originalBaseColors[originalMat]);
                    Debug.Log($"[TurtleShell] BaseColor ripristinato");
                }
            }
        }

        if (materialsChanged)
        {
            Renderer.materials = currentMaterials;
            Debug.Log($"[TurtleShell] Materiali aggiornati nel renderer");
        }
        
        patinaActive = active;
        Debug.Log($"[TurtleShell] SetEmissiveOverlay completato - patinaActive = {patinaActive}");
    }
    public void PlaySlowdownEffect(float duration = 1f)
    {
        if (slowdownEffect == null) return;
        StartCoroutine(PlayEffectRoutine(duration));
    }

    private IEnumerator PlayEffectRoutine(float duration)
    {
        slowdownEffect.gameObject.SetActive(true);
        slowdownEffect.PlayEffect();

        yield return new WaitForSeconds(duration);

        slowdownEffect.StopEffect();
        slowdownEffect.gameObject.SetActive(false);
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        if (isSlow)
        {
            if (!hasBeenHitWhileSlow)
            {
                hasBeenHitWhileSlow = true;
                animator.SetTrigger("GetHitReal");

                SetSlow(false);
                agent.isStopped = true;
                isStunned = true;

                if (activeSlowdownAbility != null && activeSlowdownAbility.IsActive)
                {
                    activeSlowdownAbility.Deactivate();
                }
            }
            return;
        }

        currentHealth -= damage;
        animator.SetTrigger("GetHit");

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            isDead = true;
            StartCoroutine(HandleDeath());
        }
    }

    private IEnumerator HandleDeath()
    {
        // Cleanup warning system
        CleanupWarningSequence();
        ForceResetAllWarningVariables();
         if (patinaActive)
        {
            SetOverlayActive(false);
        }
        
        agent.isStopped = true;
        
        if (enableWarningDebug)
        {
            WarningDebugLog("💀 ENEMY DIED - All systems stopped");
        }
        
        yield return null;
    }

    public void TriggerDie()
    {
        if (isDead) return;
        isDead = true;
        isStunned = true;
        
        CleanupWarningSequence();
        ForceResetAllWarningVariables();
        
        agent.isStopped = true;
        animator.SetTrigger("Die");
    }

    public void OnDeathAnimationFinished()
    {
        if (deathEffectController != null)
            deathEffectController.PlayEffect();

        if (Renderer != null)
            Renderer.enabled = false;

        StartCoroutine(DelayedDestroy());
    }

    private IEnumerator DelayedDestroy()
    {
        if (deathEffectController != null)
        {
            ParticleSystem ps = deathEffectController.GetComponent<ParticleSystem>();
            if (ps != null)
                yield return new WaitUntil(() => !ps.isPlaying);
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }

        GemManager.Instance?.SpawnLifeGem(transform.position);
        Destroy(gameObject);
    }

    private void RotateTowards(Vector3 target)
    {
        Vector3 dir = (target - transform.position).normalized;
        dir.y = 0;
        if (dir == Vector3.zero) return;
        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 5f);
    }

    // Debug utility method
    private void WarningDebugLog(string message)
    {
        if (enableWarningDebug)
        {
            Debug.Log($"🎵 <color=yellow>[{name}]</color> {message} <color=gray>(T:{Time.time:F2})</color>");
        }
    }

    // Debug context menu methods
    [ContextMenu("🔍 Debug Warning State")]
    public void DebugWarningState()
    {
        float playerDist = player != null ? Vector3.Distance(transform.position, player.position) : -1f;
        
        string statusIcon = isDead ? "💀" : (hasPlayedWarningEver ? "🔇" : "🔊");
        
        Debug.Log($"🎵 <color=white>[WARNING STATE DEBUG]</color> {name} {statusIcon}\n" +
                 $"🎯 LIFETIME STATUS:\n" +
                 $"  ├─ hasPlayedWarningEver: {hasPlayedWarningEver}\n" +
                 $"  ├─ hasEverSeenPlayer: {hasEverSeenPlayer}\n" +
                 $"  └─ Can play warning: {(!hasPlayedWarningEver && !isDead ? "✅ YES" : "❌ NO")}\n" +
                 $"👁️ CURRENT DETECTION:\n" +
                 $"  ├─ playerVisible: {playerVisible}\n" +
                 $"  ├─ Player distance: {(playerDist >= 0 ? $"{playerDist:F2}m" : "N/A")}\n" +
                 $"  ├─ Within view radius ({viewRadius}m): {(playerDist >= 0 && playerDist <= viewRadius ? "✅" : "❌")}\n" +
                 $"  └─ isDead: {isDead}");
    }

    [ContextMenu("🔄 Reset Warning System")]
    public void ResetWarningSystemForDebug()
    {
        Debug.Log($"🔄 <color=red>[MANUAL RESET]</color> {name} - Resetting warning system for testing...");
        
        hasEverSeenPlayer = false;
        hasPlayedWarningEver = false;
        warningCallCount = 0;
        lastWarningAttemptTime = 0f;
        lastWarningBlockReason = "";
        
        ForceResetAllWarningVariables();
        
        Debug.Log($"🔄 Warning system completely reset. hasPlayedWarningEver: {hasPlayedWarningEver}");
    }

    private void OnDrawGizmosSelected()
    {
        // View radius
        if (viewRadius > 0)
        {
            Gizmos.color = playerVisible ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, viewRadius);

            // View angle
            Vector3 leftBoundary = Quaternion.AngleAxis(-viewAngle / 2, Vector3.up) * transform.forward * viewRadius;
            Vector3 rightBoundary = Quaternion.AngleAxis(viewAngle / 2, Vector3.up) * transform.forward * viewRadius;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
            Gizmos.DrawLine(transform.position, transform.position + rightBoundary);
        }

        // Attack range
        if (attackRange > 0)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }

        // Warning system status indicator
        if (hasPlayedWarningEver)
        {
            // Purple cube = warning already played
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
        }
        else if (isDead)
        {
            // Red X = dead
            Gizmos.color = Color.red;
            Vector3 pos = transform.position + Vector3.up * 2f;
            Gizmos.DrawLine(pos + Vector3.left * 0.3f, pos + Vector3.right * 0.3f);
            Gizmos.DrawLine(pos + Vector3.forward * 0.3f, pos + Vector3.back * 0.3f);
        }
        else
        {
            // Green sphere = ready for warning
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.3f);
        }

        // Current waypoint
        if (waypoints != null && waypoints.Length > 0 && currentWaypoint < waypoints.Length && waypoints[currentWaypoint] != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, waypoints[currentWaypoint].position);
            Gizmos.DrawWireSphere(waypoints[currentWaypoint].position, 0.5f);
        }
        
        // Player line of sight
        if (player != null)
        {
            Gizmos.color = playerVisible ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, player.position + Vector3.up * 0.5f);
        }
    }
}